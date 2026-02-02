using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeklaResultsInterrogator.Core;
using TeklaResultsInterrogator.Utils;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Sections;
using TSD.API.Remoting.Solver;
using TSD.API.Remoting.Structure;
using TSD.API.Remoting.UserDefinedAttributes;
using static TeklaResultsInterrogator.Utils.ConsoleUtils;

namespace TeklaResultsInterrogator.Commands
{
    /// <summary>
    /// Command to calculate steel column shortening (PL/AE) with basic lift information.
    /// 
    /// UNIT SYSTEM:
    /// - Forces: API value × ConversionFactor → kips
    /// - Lengths: API returns mm → converted to inches for formula
    /// - Areas: API always returns mm² → converted to in² for formula
    /// - Modulus: Hard-coded 29,000 ksi (steel only)
    /// 
    /// Formula: Shortening (inches) = P(kips) × L(in) / [A(in²) × E(ksi)]
    /// </summary>
    public class SteelColumnShortening : SolverInterrogator
    {
        private const double STEEL_MODULUS_E = 29000.0; // ksi - constant for all steel


        /// <summary>
        /// Determines if this command should be shown in the menu.
        /// </summary>
        public override bool ShowInMenu() => true;

        /// <summary>
        /// Constructor sets up output and requested member type.
        /// </summary>
        public SteelColumnShortening()
        {
            HasOutput = true;
            RequestedMemberType = new List<MemberConstruction>() { MemberConstruction.SteelColumn };
        }

        /// <summary>
        /// Represents span-level shortening calculation details.
        /// </summary>
        private class SpanShorteningDetail
        {
            public double Force { get; set; }
            public double LengthFt { get; set; }
            public double Shortening { get; set; }
        }

        /// <summary>
        /// Main execution method for the command.
        /// </summary>
        public override async Task ExecuteAsync()
        {
            await InitializeAsync();
            if (Flag) return;

            Stopwatch stopwatch = Stopwatch.StartNew();
            int bufferSize = 65536 * 2;

            FancyWriteLine("Loading Summary:", TextColor.Title);
            Console.WriteLine("Unpacking loading data...");
            Console.WriteLine($"{AllLoadcases!.Count} loadcases found, {SolvedCases!.Count} solved.");
            Console.WriteLine($"{AllCombinations!.Count} load combinations found, {SolvedCombinations!.Count} solved.");
            Console.WriteLine($"{AllEnvelopes!.Count} load envelopes found, {SolvedEnvelopes!.Count} solved.\n");

            stopwatch.Stop();
            var loadingCases = AskLoading(SolvedCases, SolvedCombinations, SolvedEnvelopes);
            bool reduced = AskReduced();

            // Member Data
            FancyWriteLine("\nMember summary:", TextColor.Title);
            Console.WriteLine("Unpacking member data...");

            string? filterField = AskUser("What UDA field to filter on?");
            string? filterValue = AskUser("What UDA value to filter on?");

            stopwatch.Start();
            var steelColumns = AllMembers!.Where(c => RequestedMemberType.Contains(GetProperty(c.Data.Value.Construction))).ToList();

            Console.WriteLine($"{AllMembers!.Count} structural members found in model.");
            Console.WriteLine($"{steelColumns.Count} steel columns found.");

            // Organize Levels
            var rawLevels = await Model!.GetLevelsAsync();
            var levels = new List<IHorizontalConstructionPlane>();
            foreach (var item in rawLevels)
            {
                if (item is IHorizontalConstructionPlane hcp) levels.Add(hcp);
            }

            double timeUnpack = Math.Round(stopwatch.Elapsed.TotalSeconds, 3);
            Console.WriteLine($"Loading and member data unpacked in {timeUnpack} seconds.\n");

            FancyWriteLine("Organizing Column Lifts & Geometry...", TextColor.Title);

            // Phase 1: Organize Spans and Collect Indices
            System.Collections.Concurrent.ConcurrentBag<(IMember Member, ColumnSpansSteel Spans)> columnData = new();
            System.Collections.Concurrent.ConcurrentBag<int> allPointIndices = new();

            List<Task> preTasks = new();
            object consoleLock = new();

            foreach (var col in steelColumns)
            {
                preTasks.Add(Task.Run(async () =>
                {
                    var colSpans = new ColumnSpansSteel(col);
                    await colSpans.OrganizeSpansAsync();

                    columnData.Add((col, colSpans));

                    // Log progress
                    lock (consoleLock)
                    {
                        var lifts = colSpans.CreateLifts();
                        Console.WriteLine($"Column {col.Name}: {colSpans.Spans.Count} spans, {lifts.Count} lifts");
                    }

                    // Collect indices from spans (Start/End nodes of each span/lift)
                    // We need points for Lifts (start/end of lift)
                    foreach (var lift in colSpans.CreateLifts())
                    {
                        if (lift.StartNode?.ConstructionPointIndex != null)
                            allPointIndices.Add(lift.StartNode.ConstructionPointIndex.Value);
                        if (lift.EndNode?.ConstructionPointIndex != null)
                            allPointIndices.Add(lift.EndNode.ConstructionPointIndex.Value);
                    }
                }));
            }
            await Task.WhenAll(preTasks);

            // Phase 2: Batch Fetch Construction Points
            var uniqueIndices = allPointIndices.Distinct().ToList();
            Console.WriteLine($"\nFetching coordinates for {uniqueIndices.Count} unique points...");
            var pointsList = await Model.GetConstructionPointsAsync(uniqueIndices);
            var pointsDict = pointsList.ToDictionary(p => p.Index, p => p);

            // Find maximum number of spans across all lifts (for header)
            int maxSpanCount = 0;
            foreach (var (_, spans) in columnData)
            {
                var lifts = spans.CreateLifts();
                foreach (var lift in lifts)
                {
                    maxSpanCount = Math.Max(maxSpanCount, lift.Spans.Count);
                }
            }

            // Prepare output CSV file
            string file1 = SaveDirectory + @"SteelColumnShortening_" + OutputFileName + ".csv";

            // Build dynamic header with span columns based on actual max spans found
            var headerParts = new List<string>
            {
                "Tekla GUID",
                "Part Mark",
                "UDA Filter",
                "Member Name",
                "Lift Name",
                "Start Level",
                "End Level",
                "Shape",
                "Material",
                "Start Node",
                "X_StartNode",
                "Y_StartNode",
                "Z_StartNode",
                "End Node",
                "X_EndNode",
                "Y_EndNode",
                "Z_EndNode",
                "Lift Length [ft]",
                "Cross Section Area [in²]",
                "Shortening [in]"
            };

            // Add span columns based on actual max spans in model
            for (int i = 1; i <= maxSpanCount; i++)
            {
                headerParts.Add($"Force[k] Span {i}");
                headerParts.Add($"Length[ft] Span {i}");
                headerParts.Add($"Shortening[in] Span {i}");
            }

            string header1 = string.Join(",", headerParts) + "\n";
            File.WriteAllText(file1, header1);

            FancyWriteLine("Calculating Column Shortening (Parallel)...", TextColor.Title);

            // Use the first loading case for shortening calculations
            var targetLoadingCase = loadingCases.First();

            // Phase 3: Calculate Shortening (Parallel)
            List<Task<List<string>>> processTasks = new();
            foreach (var (_, spans) in columnData)
            {
                processTasks.Add(Task.Run(() => ProcessColumnShorteningAsync(
                    spans,
                    targetLoadingCase,
                    reduced,
                    filterField,
                    filterValue,
                    levels,
                    maxSpanCount,
                    pointsDict)));
            }

            var results = await Task.WhenAll(processTasks);

            using StreamWriter sw1 = new(file1, true, Encoding.UTF8, bufferSize);
            {
                foreach (var result in results)
                {
                    foreach (var line in result)
                    {
                        sw1.WriteLine(line);
                    }
                }
            }

            FancyWriteLine("Saved to: ", file1, "", TextColor.Path);
            double sizeKB = Math.Round(new FileInfo(file1).Length / 1024.0, 2);
            Console.WriteLine($"File size: {sizeKB} KB");

            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;
            Check();
        }

        private async Task<List<string>> ProcessColumnShorteningAsync(
            ColumnSpansSteel columnSpans,
            ILoadingCase loadingCase,
            bool reduced,
            string? filterField,
            string? filterValue,
            List<IHorizontalConstructionPlane> levels,
            int maxSpanCount,
            Dictionary<int, IConstructionPoint> pointsDict)
        {
            List<string> lines = new();
            var member = columnSpans.ParentMember;
            var lifts = columnSpans.CreateLifts();

            /* Pre-fetch member loading ONCE for the column if possible?
               Actually GetLoadingAsync needs to be called on member.
               We can call it once per member. */
            IMemberLoading memberLoading = await member.GetLoadingAsync(loadingCase.Id, RequestedAnalysisType, LoadingResultType.Base);

            for (int liftIndex = 0; liftIndex < lifts.Count; liftIndex++)
            {
                var lift = lifts[liftIndex];
                var firstSpanforID = lift.Spans.First();

                // Check UDA filter
                var udas = await firstSpanforID.GetUserDefinedAttributesAsync();
                if (!string.IsNullOrEmpty(filterValue))
                {
                    bool match = udas.Any(c =>
                        (c as IUserDefinedTextAttribute)?.Text.Equals(filterValue, StringComparison.CurrentCultureIgnoreCase) == true &&
                        c?.AttributeDefinitionName.Equals(filterField, StringComparison.CurrentCultureIgnoreCase) == true);
                    if (!match) continue;
                }

                Guid id = firstSpanforID.Id;
                string partMark = lifts.Count == 1 ? member.Name : firstSpanforID.Name;

                // Get start and end node construction points for the lift
                int startNodeIdx = lift.StartNode!.ConstructionPointIndex.Value;
                IConstructionPoint? startPoint = pointsDict.ContainsKey(startNodeIdx) ? pointsDict[startNodeIdx] : null;
                double startX = 0, startY = 0, startZ = 0;
                string startLevelName = "Unknown";

                if (startPoint != null)
                {
                    startX = MmToFt(startPoint.Coordinates.Value.X);
                    startY = MmToFt(startPoint.Coordinates.Value.Y);
                    startZ = MmToFt(startPoint.Coordinates.Value.Z);
                    startLevelName = GetLevelName(startPoint.Coordinates.Value.Z, startPoint, levels);
                }

                int endNodeIdx = lift.EndNode!.ConstructionPointIndex.Value;
                IConstructionPoint? endPoint = pointsDict.ContainsKey(endNodeIdx) ? pointsDict[endNodeIdx] : null;
                double endX = 0, endY = 0, endZ = 0;
                string endLevelName = "Unknown";

                if (endPoint != null)
                {
                    endX = MmToFt(endPoint.Coordinates.Value.X);
                    endY = MmToFt(endPoint.Coordinates.Value.Y);
                    endZ = MmToFt(endPoint.Coordinates.Value.Z);
                    endLevelName = GetLevelName(endPoint.Coordinates.Value.Z, endPoint, levels);
                }

                // Get section and material info from the first span
                string sectionName = "Unknown";
                string materialName = "Unknown";
                if (firstSpanforID.ElementSection.Value != null)
                {
                    var elementSection = (IMemberSection)firstSpanforID.ElementSection.Value;
                    var physicalSection = (ISection)elementSection.PhysicalSection.Value;
                    sectionName = physicalSection.LongName ?? "Unknown";
                }
                if (firstSpanforID.Material?.Value != null)
                {
                    materialName = firstSpanforID.Material.Value.Name ?? "Unknown";
                }

                double lengthFt = MmToFt(lift.Length);
                string startNodeName = $"{startNodeIdx}";
                string endNodeName = $"{endNodeIdx}";


                // Calculate shortening
                double area = await GetCrossSectionalArea(lift);
                var (shortening, spanDetails) = await CalculateLiftShorteningWithDetails(memberLoading, lift, reduced, area);

                // Build base line with lift data
                string line = $"{EscapeCsvValue(id.ToString())},{EscapeCsvValue(partMark)},{EscapeCsvValue(filterValue)},{EscapeCsvValue(member.Name)},{EscapeCsvValue(lift.Name)},{EscapeCsvValue(startLevelName)},{EscapeCsvValue(endLevelName)},{EscapeCsvValue(sectionName)},{EscapeCsvValue(materialName)}," +
                              $"{EscapeCsvValue(startNodeName)},{startX:F3},{startY:F3},{startZ:F3}," +
                              $"{EscapeCsvValue(endNodeName)},{endX:F3},{endY:F3},{endZ:F3}," +
                              $"{lengthFt:F3},{area:F3},{shortening:F4}";

                // Add span data columns
                for (int spanIdx = 0; spanIdx < maxSpanCount; spanIdx++)
                {
                    if (spanIdx < spanDetails.Count)
                    {
                        var span = spanDetails[spanIdx];
                        line += $",{span.Force:F1},{span.LengthFt:F2},{span.Shortening:F4}";
                    }
                    else
                    {
                        line += ",0,0,0";
                    }
                }
                lines.Add(line);
            }
            return lines;
        }

        private static async Task<(double TotalShortening, List<SpanShorteningDetail> SpanDetails)> CalculateLiftShorteningWithDetails(
            IMemberLoading memberLoading,
            ColumnLift lift,
            bool reduced,
            double area)
        {
            if (area <= 0) return (0.0, new List<SpanShorteningDetail>());

            double valCon = ConversionFactor(LoadingValueType.Force);
            List<Task<SpanShorteningDetail>> tasks = new();

            foreach (var span in lift.Spans)
            {
                tasks.Add(Task.Run(async () =>
                {
                    double spanForce = 0.0;
                    try
                    {
                        // Get force at start of span (position 0.0)
                        var option = LoadingValueOptions.StaticValue(LoadingValueType.Force, LoadingDirection.Axial, reduced);
                        var values = await memberLoading.GetValueAsync(option, span.Index, 0.0);
                        if (values.Any())
                            spanForce = Math.Abs(values.MaxBy(v => Math.Abs(v.Value))?.Value ?? 0.0) * valCon;
                    }
                    catch { }

                    double spanLengthInches = MmToIn(span.Length.Value);
                    double spanLengthFt = MmToFt(span.Length.Value);
                    double spanShortening = (spanForce * spanLengthInches) / (area * STEEL_MODULUS_E);

                    return new SpanShorteningDetail
                    {
                        Force = spanForce,
                        LengthFt = spanLengthFt,
                        Shortening = spanShortening
                    };
                }));
            }

            var results = await Task.WhenAll(tasks);
            var spanDetails = results.ToList();
            double totalShortening = spanDetails.Sum(s => s.Shortening);

            return (totalShortening, spanDetails);
        }

        private static async Task<double> GetCrossSectionalArea(ColumnLift lift)
        {
            try
            {
                var firstSpan = lift.Spans.First();
                if (firstSpan.ElementSection.Value is IMemberSection elementSection)
                {
                    if (elementSection.PhysicalSection.Value is ISection physicalSection)
                    {
                        // API returns area in mm², convert to in²
                        double areaMm2 = physicalSection.CrossSectionalArea;
                        double areaIn2 = MmSqToInSq(areaMm2);
                        return areaIn2 > 0 ? areaIn2 : 0.0;
                    }
                }
            }
            catch { }
            return 0.0;
        }


    }
}