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
    /// Interrogates Steel Column forces, calculating forces at stations along the column.
    /// </summary>
    public class SteelColumnForces : SolverInterrogator
    {
        /// <inheritdoc/>
        public override bool ShowInMenu() => true;
        /// <summary>Initializes a new instance of the <see cref="SteelColumnForces"/> class.</summary>
        public SteelColumnForces()
        {
            HasOutput = true;
            RequestedMemberType = new List<MemberConstruction>() { MemberConstruction.SteelColumn };
        }

        /// <summary>
        /// Executes the Steel Column Forces interrogation, writing results to CSV.
        /// </summary>
        public override async Task ExecuteAsync()
        {
            await InitializeAsync();
            if (Flag) return;

            Stopwatch stopwatch = Stopwatch.StartNew();
            int bufferSize = 65536 * 2;

            LogLoadingSummary();
            stopwatch.Stop();

            var loadingCases = AskLoading(SolvedCases, SolvedCombinations, SolvedEnvelopes);
            var reduced = AskReduced();

            List<IMember> steelColumns = AskAndFilterMembers(true, true);

            string? filterField = AskUser("What UDA field to filter on?");
            string? filterValue = AskUser("What UDA value to filter on?");

            stopwatch.Start();
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

            // Organize Column Lifts & Pre-Fetch Geometry
            FancyWriteLine("Organizing Column Spans and Splices...", TextColor.Title);

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
                        string spliceText = colSpans.HasSplice ? "has splice" : "no splice";
                        Console.WriteLine($"Column {col.Name}: {colSpans.Spans.Count} spans, {spliceText}");
                    }

                    // Collect indices from spans (Start/End nodes of each span)
                    foreach (var span in colSpans.Spans)
                    {
                        if (span.StartMemberNode?.ConstructionPointIndex != null)
                            allPointIndices.Add(span.StartMemberNode.ConstructionPointIndex.Value);
                        if (span.EndMemberNode?.ConstructionPointIndex != null)
                            allPointIndices.Add(span.EndMemberNode.ConstructionPointIndex.Value);
                    }
                }));
            }
            await Task.WhenAll(preTasks);

            // Phase 2: Batch Fetch Construction Points
            var uniqueIndices = allPointIndices.Distinct().ToList();
            Console.WriteLine($"\nFetching coordinates for {uniqueIndices.Count} unique points...");
            var pointsList = await Model.GetConstructionPointsAsync(uniqueIndices);
            var pointsDict = pointsList.ToDictionary(p => p.Index, p => p);

            // Phase 3: Process Forces (Parallel)
            FancyWriteLine("\nQuerying Steel Column Forces (Parallel)...", TextColor.Title);

            // Prepare CSV
            string file1 = SaveDirectory + @"SteelColumnForces_" + OutputFileName + ".csv";
            string header1 = "Tekla GUID,UDA Filter,Member Name,Span Name,Start Level,End Level,Shape,Material," +
                             "Start Node,Start Node Fixity,X_StartNode,Y_StartNode,Z_StartNode," +
                             "End Node,End Node Fixity,X_EndNode,Y_EndNode,Z_EndNode," +
                             "Span Length [ft],Span Rotation [deg],Loading Name,Location," +
                             "Axial Force [k],Shear Major [k],Shear Minor [k],Moment Major [k-ft],Moment Minor [k-ft],Torsion [k-ft],Has Splice?\n";
            File.WriteAllText(file1, "");
            File.AppendAllText(file1, header1);



            // Parallel Execution
            var tasks = new List<Task<List<string>>>();
            foreach (var (member, spans) in columnData)
            {
                tasks.Add(Task.Run(() => ProcessColumnAsync(member, spans, loadingCases, reduced, filterField, filterValue, levels, pointsDict)));
            }

            var results = await Task.WhenAll(tasks);
            double endWatch = Math.Round(stopwatch.Elapsed.TotalSeconds, 3);

            // Writing Results
            FancyWriteLine("Writing internal forces table...", TextColor.Title);
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

        private async Task<List<string>> ProcessColumnAsync(
            IMember member,
            ColumnSpansSteel columnSpans,
            List<ILoadingCase> loadingCases,
            bool reduced,
            string? filterField,
            string? filterValue,
            List<IHorizontalConstructionPlane> levels,
            Dictionary<int, IConstructionPoint> pointsDict)
        {
            var lines = new List<string>();
            // Note: columnSpans already organized

            var allSpans = columnSpans.Spans.OrderBy(s => s.Index);
            bool hasSplice = columnSpans.HasSplice;
            string hasSpliceText = hasSplice ? "Yes" : "No";

            var spanTasks = new List<Task<List<string>>>();
            foreach (var span in allSpans)
            {
                spanTasks.Add(GetSpanForcesAsync(
                    member, span, columnSpans, loadingCases, reduced, filterField, filterValue, levels, hasSpliceText, pointsDict));
            }

            var spanResults = await Task.WhenAll(spanTasks);
            foreach (var res in spanResults) lines.AddRange(res);

            return lines;
        }

        private async Task<List<string>> GetSpanForcesAsync(
            IMember member,
            IMemberSpan span,
            ColumnSpansSteel columnSpans,
            List<ILoadingCase> loadingCases,
            bool reduced,
            string? filterField,
            string? filterValue,
            List<IHorizontalConstructionPlane> levels,
            string hasSpliceText,
            Dictionary<int, IConstructionPoint> pointsDict)
        {
            var lines = new List<string>();

            // Filter Check
            var udas = await span.GetUserDefinedAttributesAsync();
            if (!string.IsNullOrEmpty(filterValue))
            {
                bool match = udas.Any(c =>
                    (c as IUserDefinedTextAttribute)?.Text.Equals(filterValue, StringComparison.CurrentCultureIgnoreCase) == true &&
                    c?.AttributeDefinitionName.Equals(filterField, StringComparison.CurrentCultureIgnoreCase) == true);
                if (!match) return lines;
            }

            Guid id = span.Id;
            var (hasSplice, spliceOffset) = columnSpans.SpanSpliceInfo.TryGetValue(span.Index, out var si)
                ? si
                : (HasSplice: false, SpliceOffset: 0.0);

            // Nodes & Geometry
            int startNodeIdx = span.StartMemberNode.ConstructionPointIndex.Value;
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

            int endNodeIdx = span.EndMemberNode.ConstructionPointIndex.Value;
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

            // Section & Material
            string sectionName = "Unknown";
            string materialName = "Unknown";
            if (span.ElementSection.Value != null)
            {
                var elementSection = (IMemberSection)span.ElementSection.Value;
                var physicalSection = (ISection)elementSection.PhysicalSection.Value;
                sectionName = physicalSection.LongName;
            }
            if (span.Material?.Value != null)
            {
                materialName = span.Material.Value.Name;
            }

            double lengthFt = MmToFt(span.Length.Value);
            double rotationDeg = RadToDeg(span.RotationAngle.Value);
            string startNodeFixity = GetNodeFixityDescription(span.StartReleases.Value);
            string endNodeFixity = GetNodeFixityDescription(span.EndReleases.Value);
            string startNodeName = $"{startNodeIdx}";
            string endNodeName = $"{endNodeIdx}";

            // Prepare Positions
            var positions = new List<(string LocationName, double PositionMm)>
            {
                ("Start", 0.0)
            };
            // Splice
            if (hasSplice && spliceOffset > 0 && spliceOffset < span.Length.Value)
            {
                positions.Insert(1, ("Splice", spliceOffset)); // Insert in middle if between start/end
            }
            positions.Add(("End", span.Length.Value));

            var positionsMm = positions.Select(p => p.PositionMm).ToList();

            foreach (var loadingCase in loadingCases)
            {
                IMemberLoading memberLoading = await member.GetLoadingAsync(loadingCase.Id, RequestedAnalysisType, LoadingResultType.Base);

                // Batch Fetch (Parallel calls as fallback)
                var axialForcesTask = GetBatchedLoadingValues(memberLoading, LoadingValueType.Force, LoadingDirection.Axial, positionsMm, reduced, span.Index);
                var majorShearsTask = GetBatchedLoadingValues(memberLoading, LoadingValueType.Force, LoadingDirection.Major, positionsMm, reduced, span.Index);
                var minorShearsTask = GetBatchedLoadingValues(memberLoading, LoadingValueType.Force, LoadingDirection.Minor, positionsMm, reduced, span.Index);
                var majorMomentsTask = GetBatchedLoadingValues(memberLoading, LoadingValueType.Moment, LoadingDirection.Major, positionsMm, reduced, span.Index);
                var minorMomentsTask = GetBatchedLoadingValues(memberLoading, LoadingValueType.Moment, LoadingDirection.Minor, positionsMm, reduced, span.Index);
                var torsionsTask = GetBatchedLoadingValues(memberLoading, LoadingValueType.Moment, LoadingDirection.Axial, positionsMm, reduced, span.Index);

                await Task.WhenAll(axialForcesTask, majorShearsTask, minorShearsTask, majorMomentsTask, minorMomentsTask, torsionsTask);

                var axialForces = axialForcesTask.Result;
                var majorShears = majorShearsTask.Result;
                var minorShears = minorShearsTask.Result;
                var majorMoments = majorMomentsTask.Result;
                var minorMoments = minorMomentsTask.Result;
                var torsions = torsionsTask.Result;

                for (int i = 0; i < positions.Count; i++)
                {
                    var (locationName, positionMm) = positions[i];
                    double axialForce = axialForces.ElementAtOrDefault(i) * ConversionFactor(LoadingValueType.Force);
                    double shearMajor = majorShears.ElementAtOrDefault(i) * ConversionFactor(LoadingValueType.Force);
                    double shearMinor = minorShears.ElementAtOrDefault(i) * ConversionFactor(LoadingValueType.Force);
                    double momentMajor = majorMoments.ElementAtOrDefault(i) * ConversionFactor(LoadingValueType.Moment);
                    double momentMinor = minorMoments.ElementAtOrDefault(i) * ConversionFactor(LoadingValueType.Moment);
                    double torsion = torsions.ElementAtOrDefault(i) * ConversionFactor(LoadingValueType.Moment);

                    string line = $"{EscapeCsvValue(id.ToString())},{EscapeCsvValue(filterValue)},{EscapeCsvValue(member.Name)},{EscapeCsvValue(span.Name)},{EscapeCsvValue(startLevelName)},{EscapeCsvValue(endLevelName)},{EscapeCsvValue(sectionName)},{EscapeCsvValue(materialName)}," +
                                  $"{EscapeCsvValue(startNodeName)},{EscapeCsvValue(startNodeFixity)},{startX:F3},{startY:F3},{startZ:F3}," +
                                  $"{EscapeCsvValue(endNodeName)},{EscapeCsvValue(endNodeFixity)},{endX:F3},{endY:F3},{endZ:F3}," +
                                  $"{lengthFt:F3},{rotationDeg:F3},{EscapeCsvValue(loadingCase.Name)},{EscapeCsvValue(locationName)}," +
                                  $"{axialForce:F3},{shearMajor:F3},{shearMinor:F3},{momentMajor:F3},{momentMinor:F3},{torsion:F3},{EscapeCsvValue(hasSpliceText)}";
                    lines.Add(line);
                }
            }

            return lines;
        }

        private static async Task<IEnumerable<double>> GetBatchedLoadingValues(
            IMemberLoading loading,
            LoadingValueType valueType,
            LoadingDirection direction,
            IEnumerable<double> positionsMm,
            bool reduced,
            int spanIndex)
        {
            var option = LoadingValueOptions.StaticValue(valueType, direction, reduced);
            // Parallel requests for each position
            var tasks = new List<Task<double>>();
            foreach (var pos in positionsMm)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var values = await loading.GetValueAsync(option, spanIndex, pos);
                    // Select the value with the largest magnitude (Absolute Max).
                    // This ensures we capture large negative values (e.g. Max Tension) which are 
                    // significant but would be ignored by a simple algebraic Max().
                    // The original sign is preserved in the returned value.
                    return values.MaxBy(lv => Math.Abs(lv.Value))?.Value ?? 0.0;
                }));
            }
            return await Task.WhenAll(tasks);
        }

        // Helper to get level name safely

    }
}
