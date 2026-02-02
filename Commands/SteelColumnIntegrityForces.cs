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
    /// Simplified command to generate a summary of steel columns with basic lift information/integrity forces.
    /// </summary>
    public class SteelColumnIntegrityForces : SolverInterrogator
    {
        /// <inheritdoc/>
        public override bool ShowInMenu() => true;

        /// <summary>Initializes a new instance of the <see cref="SteelColumnIntegrityForces"/> class.</summary>
        public SteelColumnIntegrityForces()
        {
            HasOutput = true;
            RequestedMemberType = new List<MemberConstruction>() { MemberConstruction.SteelColumn };
        }

        /// <summary>
        /// Executes the simplified column integrity forces interrogation.
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
            var reduced = AskReduced();

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

            FancyWriteLine("Organizing Column Data...", TextColor.Title);

            // Phase 1: Organize Spans and Collect Indices
            var columnData = new System.Collections.Concurrent.ConcurrentBag<(IMember Member, ColumnSpansSteel Spans)>();
            var allPointIndices = new System.Collections.Concurrent.ConcurrentBag<int>();

            var preTasks = new List<Task>();
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

                    // Collect indices from lifts
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

            FancyWriteLine("Querying Steel Column Integrity Forces (Parallel)...", TextColor.Title);

            // Prepare output CSV file
            string file1 = SaveDirectory + @"SteelColumnIntegrityForces_" + OutputFileName + ".csv";
            string header1 = "Tekla GUID,Part Mark,UDA Filter,Member Name,Lift Name,Start Level,End Level,Shape,Material," +
                           "Start Node,X_StartNode,Y_StartNode,Z_StartNode," +
                           "End Node,X_EndNode,Y_EndNode,Z_EndNode," +
                           "Lift Length [ft],Integrity Force [k]\n";

            File.WriteAllText(file1, header1);

            var integrityForceCase = loadingCases.FirstOrDefault(lc =>
                    lc.Name.Equals("Integrity Force", StringComparison.CurrentCultureIgnoreCase) ||
                    lc.Name.Contains("Integrity", StringComparison.CurrentCultureIgnoreCase));

            // Phase 3: Process Logic (Parallel)
            var tasks = new List<Task<List<string>>>();
            foreach (var (member, spans) in columnData)
            {
                tasks.Add(Task.Run(() => ProcessColumnAsync(
                    member, spans, integrityForceCase, reduced, filterField, filterValue, levels, pointsDict)));
            }

            var results = await Task.WhenAll(tasks);

            using (StreamWriter sw1 = new(file1, true, Encoding.UTF8, bufferSize))
            {
                foreach (var result in results)
                {
                    foreach (var line in result) sw1.WriteLine(line);
                }
            }

            FancyWriteLine("Saved to: ", file1, "", TextColor.Path);
            double sizeKB = Math.Round(new FileInfo(file1).Length / 1024.0, 2);
            Console.WriteLine($"File size: {sizeKB} KB");

            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;
            Check();
        }

        private async Task<List<string>> ProcessColumnAsync(
            IMember member,
            ColumnSpansSteel colSpans,
            ILoadingCase? integrityForceCase,
            bool reduced,
            string? filterField,
            string? filterValue,
            List<IHorizontalConstructionPlane> levels,
            Dictionary<int, IConstructionPoint> pointsDict)
        {
            var lines = new List<string>();
            var lifts = colSpans.CreateLifts();

            // Calculate integrity forces
            var integrityForces = new Dictionary<string, double>();
            if (integrityForceCase != null && colSpans.HasSplice)
            {
                integrityForces = await CalculateIntegrityForcesWithSpliceOffsets(member, lifts, integrityForceCase, reduced, colSpans);
            }

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

                Guid id = firstSpan.Id;
                string partMark = lifts.Count == 1 ? member.Name : firstSpan.Name;

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

                string sectionName = "Unknown";
                string materialName = "Unknown";
                if (firstSpan.ElementSection.Value != null)
                {
                    var elementSection = (IMemberSection)firstSpan.ElementSection.Value;
                    var physicalSection = (ISection)elementSection.PhysicalSection.Value;
                    sectionName = physicalSection.LongName;
                }
                if (firstSpan.Material?.Value != null)
                {
                    materialName = firstSpan.Material.Value.Name;
                }

                double lengthFt = MmToFt(lift.Length);
                string startNodeName = $"{startNodeIdx}";
                string endNodeName = $"{endNodeIdx}";

                double integrityForce = 0.0;
                if (integrityForceCase != null && integrityForces.ContainsKey(lift.Name))
                {
                    integrityForce = -1 * integrityForces[lift.Name];
                }

                string line = $"{EscapeCsvValue(id.ToString())},{EscapeCsvValue(partMark)},{EscapeCsvValue(filterValue)},{EscapeCsvValue(member.Name)},{EscapeCsvValue(lift.Name)},{EscapeCsvValue(startLevelName)},{EscapeCsvValue(endLevelName)},{EscapeCsvValue(sectionName)},{EscapeCsvValue(materialName)}," +
                            $"{EscapeCsvValue(startNodeName)},{startX:F3},{startY:F3},{startZ:F3}," +
                            $"{EscapeCsvValue(endNodeName)},{endX:F3},{endY:F3},{endZ:F3}," +
                            $"{lengthFt:F3},{integrityForce}";
                lines.Add(line);
            }
            return lines;
        }

        private async Task<Dictionary<string, double>> CalculateIntegrityForcesWithSpliceOffsets(
            IMember member,
            List<ColumnLift> lifts,
            ILoadingCase? integrityForceCase,
            bool reduced,
            ColumnSpansSteel columnSpans)
        {
            var integrityForces = new Dictionary<string, double>();
            if (lifts.Count <= 1 || integrityForceCase == null) return integrityForces;

            try
            {
                IMemberLoading memberLoading = await member.GetLoadingAsync(integrityForceCase.Id, RequestedAnalysisType, LoadingResultType.Base);
                double valCon = ConversionFactor(LoadingValueType.Force);
                integrityForces[lifts[0].Name] = 0.0;

                // Start node force
                var firstSpan = lifts[0].Spans.First();
                double startNodeForce = await FetchForce(memberLoading, firstSpan.Index, 0.0, reduced) * valCon;

                // Get forces at all splice locations (Parallel)
                var spliceTasks = new List<Task<double>>();
                foreach (var lift in lifts)
                {
                    foreach (var span in lift.Spans)
                    {
                        if (columnSpans.SpanSpliceInfo.ContainsKey(span.Index) &&
                            columnSpans.SpanSpliceInfo[span.Index].HasSplice)
                        {
                            double spliceOffset = columnSpans.SpanSpliceInfo[span.Index].SpliceOffset;
                            spliceTasks.Add(Task.Run(async () =>
                                await FetchForce(memberLoading, span.Index, spliceOffset, reduced) * valCon));
                        }
                    }
                }

                var spliceForces = (await Task.WhenAll(spliceTasks)).ToList();

                for (int i = 1; i < lifts.Count; i++)
                {
                    if (i == 1 && spliceForces.Count > 0)
                    {
                        integrityForces[lifts[i].Name] = startNodeForce - spliceForces[0];
                    }
                    else if (i - 1 < spliceForces.Count && i - 2 >= 0 && i - 2 < spliceForces.Count)
                    {
                        integrityForces[lifts[i].Name] = spliceForces[i - 2] - spliceForces[i - 1];
                    }
                    else
                    {
                        integrityForces[lifts[i].Name] = 0.0;
                    }
                }
            }
            catch (Exception)
            {
                // console log if needed
                foreach (var l in lifts) integrityForces[l.Name] = 0.0;
            }
            return integrityForces;
        }

        private static async Task<double> FetchForce(IMemberLoading loading, int spanIndex, double pos, bool reduced)
        {
            try
            {
                var option = LoadingValueOptions.StaticValue(LoadingValueType.Force, LoadingDirection.Axial, reduced);
                var values = await loading.GetValueAsync(option, spanIndex, pos);
                return values.MaxBy(v => Math.Abs(v.Value))?.Value ?? 0.0;
            }
            catch { }
            return 0.0;
        }


    }
}