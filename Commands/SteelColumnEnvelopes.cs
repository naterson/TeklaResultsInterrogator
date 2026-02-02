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
    /// Generates envelope forces (max/min axial, moments, shears) for steel column lifts
    /// </summary>
    public class SteelColumnEnvelopes : SolverInterrogator
    {
        /// <inheritdoc/>
        public override bool ShowInMenu() => true;

        /// <summary>Initializes a new instance of the <see cref="SteelColumnEnvelopes"/> class.</summary>
        public SteelColumnEnvelopes()
        {
            HasOutput = true;
            RequestedMemberType = new List<MemberConstruction>() { MemberConstruction.SteelColumn };
        }

        /// <summary>
        /// Executes the command to retrieve and envelope steel column forces.
        /// </summary>
        public override async Task ExecuteAsync()
        {
            await InitializeAsync();
            if (Flag) return;

            Stopwatch stopwatch = Stopwatch.StartNew();
            int bufferSize = 65536 * 2;

            // Loading Summary
            FancyWriteLine("Loading Summary:", TextColor.Title);
            Console.WriteLine("Unpacking loading data...");
            Console.WriteLine($"{AllLoadcases!.Count} loadcases found, {SolvedCases!.Count} solved.");
            Console.WriteLine($"{AllCombinations!.Count} load combinations found, {SolvedCombinations!.Count} solved.");
            Console.WriteLine($"{AllEnvelopes!.Count} load envelopes found, {SolvedEnvelopes!.Count} solved.\n");

            stopwatch.Stop();
            var loadingCases = AskLoading(SolvedCases, SolvedCombinations, SolvedEnvelopes);
            bool reduced = AskReduced();

            // Unpacking member data
            FancyWriteLine("\nMember summary:", TextColor.Title);
            Console.WriteLine("Unpacking member data...");

            stopwatch.Start();
            var steelColumns = AskAndFilterMembers(true, true);

            string? filterField = AskUser("What UDA field to filter on?");
            string? filterValue = AskUser("What UDA value to filter on?");

            Console.WriteLine($"{AllMembers!.Count} structural members found in model.");
            Console.WriteLine($"{steelColumns.Count} steel columns found.");

            var levels = (await Model!.GetLevelsAsync()).ToList();
            double timeUnpack = Math.Round(stopwatch.Elapsed.TotalSeconds, 3);
            Console.WriteLine($"Loading and member data unpacked in {timeUnpack} seconds.\n");

            // Organize Column Lifts & Pre-Fetch Geometry
            FancyWriteLine("Organizing Column Lifts & Geometry...", TextColor.Title);

            // Phase 1: Organize Spans and Collect Indices
            var columnData = new System.Collections.Concurrent.ConcurrentBag<(IMember Member, ColumnSpansSteel Spans, List<ColumnLift> Lifts)>();
            var allPointIndices = new System.Collections.Concurrent.ConcurrentBag<int>();

            var preTasks = new List<Task>();
            object consoleLock = new();

            foreach (var col in steelColumns)
            {
                preTasks.Add(Task.Run(async () =>
                {
                    var colSpans = new ColumnSpansSteel(col);
                    await colSpans.OrganizeSpansAsync();
                    var lifts = colSpans.CreateLifts();

                    columnData.Add((col, colSpans, lifts));

                    // Log progress
                    lock (consoleLock)
                    {
                        string spliceText = colSpans.HasSplice ? "has splice" : "no splice";
                        Console.WriteLine($"Column {col.Name}: {colSpans.Spans.Count} spans, {spliceText}");
                    }

                    // Collect indices (Start AND End nodes)
                    foreach (var lift in lifts)
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

            FancyWriteLine("\nQuerying Steel Column Forces (Parallel)...", TextColor.Title);

            // Setting up file
            string file1 = SaveDirectory + @"SteelColumnEnvelopes_" + OutputFileName + ".csv";
            string header1 = "Tekla GUID,Part Mark,UDA Filter,Member Name,Lift Name,Start Level,End Level,Shape,Material," +
                 "Start Node,Start Node Fixity,X_StartNode,Y_StartNode,Z_StartNode," +
                 "End Node,End Node Fixity,X_EndNode,Y_EndNode,Z_EndNode," +
                 "Lift Length [ft],Span Rotation [deg],Loading Name," +
                 "Column Axial Max [k],Column Axial Min [k],Column Major Moment Max [k-ft]," +
                 "Column Major Shear Max [k],Column Minor Moment Max [k-ft],Column Minor Shear Max [k]\n";
            File.WriteAllText(file1, header1);

            var tasks = new List<Task<List<string>>>();
            foreach (var (col, colSpans, lifts) in columnData)
            {
                tasks.Add(Task.Run(() => ProcessColumnAsync(col, lifts, loadingCases, RequestedAnalysisType, reduced, filterField, filterValue, levels, pointsDict)));
            }

            var results = await Task.WhenAll(tasks);
            double endWatch = Math.Round(stopwatch.Elapsed.TotalSeconds, 3);

            FancyWriteLine("Writing steel Column forces table...", TextColor.Title);

            using (StreamWriter sw1 = new(file1, true, Encoding.UTF8, bufferSize))
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
            double timeCSV = Math.Round(stopwatch.Elapsed.TotalSeconds - endWatch, 3);
            Console.WriteLine($"Steel Column table written in {timeCSV} seconds.\n");

            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;
            Check();
        }

        private static async Task<List<string>> ProcessColumnAsync(
            IMember member,
            List<ColumnLift> lifts,
            List<ILoadingCase> loadingCases,
            AnalysisType analysisType,
            bool reduced,
            string? filterField,
            string? filterValue,
            List<IHorizontalConstructionPlane> levels,
            Dictionary<int, IConstructionPoint> pointsDict)
        {
            var lines = new List<string>();

            foreach (var lift in lifts)
            {
                var firstSpan = lift.Spans.First();

                // Check UDA filter
                var udas = await firstSpan.GetUserDefinedAttributesAsync();
                if (!string.IsNullOrEmpty(filterValue))
                {
                    bool match = udas.Any(c =>
                        (c as IUserDefinedTextAttribute)?.Text.Equals(filterValue, StringComparison.CurrentCultureIgnoreCase) == true &&
                        c?.AttributeDefinitionName.Equals(filterField, StringComparison.CurrentCultureIgnoreCase) == true);
                    if (!match) continue;
                }

                // Get basic lift information
                Guid id = firstSpan.Id;
                string partMark = lifts.Count == 1 ? member.Name : firstSpan.Name;

                // Get node coordinates and levels using cached dictionary
                // Start Node
                int startNodeIdx = lift.StartNode?.ConstructionPointIndex?.Value ?? -1;
                IConstructionPoint? startPoint = (startNodeIdx != -1 && pointsDict.ContainsKey(startNodeIdx)) ? pointsDict[startNodeIdx] : null;

                double startX = 0, startY = 0, startZ = 0;
                string startLevelName = "Unknown";
                string startNodeName = $"{startNodeIdx}";

                if (startPoint != null)
                {
                    startX = MmToFt(startPoint.Coordinates.Value.X);
                    startY = MmToFt(startPoint.Coordinates.Value.Y);
                    startZ = MmToFt(startPoint.Coordinates.Value.Z);
                    startLevelName = GetLevelName(startPoint.Coordinates.Value.Z, startPoint, levels);
                }

                // End Node
                int endNodeIdx = lift.EndNode?.ConstructionPointIndex?.Value ?? -1;
                IConstructionPoint? endPoint = (endNodeIdx != -1 && pointsDict.ContainsKey(endNodeIdx)) ? pointsDict[endNodeIdx] : null;

                double endX = 0, endY = 0, endZ = 0;
                string endLevelName = "Unknown";
                string endNodeName = $"{endNodeIdx}";

                if (endPoint != null)
                {
                    endX = MmToFt(endPoint.Coordinates.Value.X);
                    endY = MmToFt(endPoint.Coordinates.Value.Y);
                    endZ = MmToFt(endPoint.Coordinates.Value.Z);
                    endLevelName = GetLevelName(endPoint.Coordinates.Value.Z, endPoint, levels);
                }

                // Get section and material
                string sectionName = "Unknown";
                string materialName = "Unknown";

                if (firstSpan.ElementSection.Value != null)
                {
                    var elementSection = (IMemberSection)firstSpan.ElementSection.Value;
                    var physicalSection = (ISection)elementSection.PhysicalSection.Value;
                    sectionName = physicalSection.LongName ?? "Unknown";
                }

                if (firstSpan.Material?.Value != null)
                {
                    materialName = firstSpan.Material.Value.Name ?? "Unknown";
                }

                double lengthFt = MmToFt(lift.Length);
                double rotationDeg = RadToDeg(firstSpan.RotationAngle?.Value ?? 0.0);

                // Fixity
                string startNodeFixity = GetNodeFixityDescription(firstSpan.StartReleases.Value);
                string endNodeFixity = GetNodeFixityDescription(lift.Spans.Last().EndReleases.Value);

                foreach (var loadingCase in loadingCases)
                {
                    IMemberLoading memberLoading = await member.GetLoadingAsync(loadingCase.Id, analysisType, LoadingResultType.Base);

                    // Get envelope forces 
                    var axialTask = GetMinMaxForceInLift(memberLoading, LoadingValueType.Force, LoadingDirection.Axial, lift, reduced);
                    var majorMomentTask = GetMaxForceInLift(memberLoading, LoadingValueType.Moment, LoadingDirection.Major, lift, reduced);
                    var majorShearTask = GetMaxForceInLift(memberLoading, LoadingValueType.Force, LoadingDirection.Major, lift, reduced);
                    var minorMomentTask = GetMaxForceInLift(memberLoading, LoadingValueType.Moment, LoadingDirection.Minor, lift, reduced);
                    var minorShearTask = GetMaxForceInLift(memberLoading, LoadingValueType.Force, LoadingDirection.Minor, lift, reduced);

                    await Task.WhenAll(axialTask, majorMomentTask, majorShearTask, minorMomentTask, minorShearTask);

                    var (axialMax, axialMin) = axialTask.Result;
                    var majorMomentMax = majorMomentTask.Result;
                    var majorShearMax = majorShearTask.Result;
                    var minorMomentMax = minorMomentTask.Result;
                    var minorShearMax = minorShearTask.Result;



                    string line = $"{EscapeCsvValue(id.ToString())},{EscapeCsvValue(partMark)},{EscapeCsvValue(filterValue)},{EscapeCsvValue(member.Name)},{EscapeCsvValue(lift.Name)}," +
                          $"{EscapeCsvValue(startLevelName)},{EscapeCsvValue(endLevelName)},{EscapeCsvValue(sectionName)},{EscapeCsvValue(materialName)}," +
                          $"{EscapeCsvValue(startNodeName)},{EscapeCsvValue(startNodeFixity)},{startX:F3},{startY:F3},{startZ:F3}," +
                          $"{EscapeCsvValue(endNodeName)},{EscapeCsvValue(endNodeFixity)},{endX:F3},{endY:F3},{endZ:F3}," +
                          $"{lengthFt:F3},{rotationDeg:F3},{EscapeCsvValue(loadingCase.Name)}," +
                          $"{axialMax},{axialMin},{majorMomentMax},{majorShearMax},{minorMomentMax},{minorShearMax}";

                    lines.Add(line);
                }
            }
            return lines;
        }

        private static async Task<double> GetMaxForceInLift(
            IMemberLoading loading,
            LoadingValueType valueType,
            LoadingDirection direction,
            ColumnLift lift,
            bool reduced)
        {
            var tasks = new List<Task<double>>();
            foreach (var span in lift.Spans)
            {
                tasks.Add(Task.Run(async () =>
                {
                    int samplePoints = 10;
                    double spanLength = span.Length.Value;
                    var option = LoadingValueOptions.StaticValue(valueType, direction, reduced);

                    var pointTasks = new List<Task<double?>>();
                    for (int i = 0; i <= samplePoints; i++)
                    {
                        double pos = (i * spanLength) / samplePoints;
                        pointTasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                var vals = await loading.GetValueAsync(option, span.Index, pos);
                                if (vals.Any())
                                {
                                    return (double?)Math.Abs(vals.MaxBy(v => Math.Abs(v.Value))?.Value ?? 0.0);
                                }
                            }
                            catch { }
                            return (double?)null;
                        }));
                    }
                    var results = await Task.WhenAll(pointTasks);
                    return results.Where(r => r.HasValue).Select(r => r.GetValueOrDefault()).DefaultIfEmpty(0.0).Max();
                }));
            }

            var results = await Task.WhenAll(tasks);
            // Result is already Abs Max
            return results.DefaultIfEmpty(0.0).Max() * ConversionFactor(valueType);
        }

        private static async Task<(double max, double min)> GetMinMaxForceInLift(
            IMemberLoading loading,
            LoadingValueType valueType,
            LoadingDirection direction,
            ColumnLift lift,
            bool reduced)
        {
            var tasks = new List<Task<(double max, double? min)>>();
            foreach (var span in lift.Spans)
            {
                tasks.Add(Task.Run(async () =>
                {
                    int samplePoints = 10;
                    double spanLength = span.Length.Value;
                    var option = LoadingValueOptions.StaticValue(valueType, direction, reduced);

                    var pointTasks = new List<Task<double?>>();
                    for (int i = 0; i <= samplePoints; i++)
                    {
                        double pos = (i * spanLength) / samplePoints;
                        pointTasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                var vals = await loading.GetValueAsync(option, span.Index, pos);
                                if (vals.Any())
                                {
                                    // Pick the value with largest magnitude
                                    return (double?)vals.MaxBy(v => Math.Abs(v.Value))?.Value;
                                }
                            }
                            catch { }
                            return (double?)null;
                        }));
                    }

                    var results = await Task.WhenAll(pointTasks);
                    var validValues = results.OfType<double>().ToList();

                    if (validValues.Any())
                    {
                        double spanMax = validValues.Where(v => v > 0).DefaultIfEmpty(0.0).Max();
                        double spanMin = validValues.Min();
                        return (spanMax, (double?)spanMin);
                    }
                    return (0.0, (double?)null);
                }));
            }

            var results = await Task.WhenAll(tasks);
            double globalMax = results.Select(r => r.max).DefaultIfEmpty(0.0).Max();
            double globalMin = results.Select(r => r.min).OfType<double>().DefaultIfEmpty(0.0).Min();

            double valCon = ConversionFactor(valueType);
            return (globalMax * valCon, globalMin * valCon);
        }

    }
}