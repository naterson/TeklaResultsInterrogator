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
    /// Generates envelope eccentricity moments (max/min) for steel column lifts.
    /// </summary>
    public class SteelColumnEccentricityMoments : SolverInterrogator
    {
        /// <inheritdoc/>
        public override bool ShowInMenu() => true;

        /// <summary>Initializes a new instance of the <see cref="SteelColumnEccentricityMoments"/> class.</summary>
        public SteelColumnEccentricityMoments()
        {
            HasOutput = true;
            RequestedMemberType = new List<MemberConstruction>() { MemberConstruction.SteelColumn };
        }

        /// <summary>
        /// Executes the command to retrieve and export eccentricity moments.
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

            FancyWriteLine("Querying Steel Column Eccentricity Moments (Parallel)...", TextColor.Title);

            string file1 = SaveDirectory + @"SteelColumnEccentricityMoments_" + OutputFileName + ".csv";
            string header1 = "Tekla GUID,Part Mark,UDA Filter,Member Name,Lift Name,Start Level,End Level,Shape,Material," +
                             "Start Node,X_StartNode,Y_StartNode,Z_StartNode," +
                             "End Node,X_EndNode,Y_EndNode,Z_EndNode," +
                             "Lift Length [ft],Loading Name," +
                             "Column Ecc Mz Max [k-ft],Column Ecc Mz Min [k-ft],Column Ecc My Max [k-ft],Column Ecc My Min [k-ft]\n";

            File.WriteAllText(file1, header1);

            // Phase 3: Process Logic (Parallel)
            var tasks = new List<Task<List<string>>>();
            foreach (var (member, spans) in columnData)
            {
                tasks.Add(Task.Run(() => ProcessColumnAsync(member, spans, loadingCases, reduced, filterField, filterValue, levels, pointsDict)));
            }

            var results = await Task.WhenAll(tasks);

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

            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;
            Check();
        }

        private async Task<List<string>> ProcessColumnAsync(
            IMember member,
            ColumnSpansSteel colSpans,
            List<ILoadingCase> loadingCases,
            bool reduced,
            string? filterField,
            string? filterValue,
            List<IHorizontalConstructionPlane> levels,
            Dictionary<int, IConstructionPoint> pointsDict)
        {
            var lines = new List<string>();
            var lifts = colSpans.CreateLifts();

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

                // Geometry and Levels
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
                if (firstSpan.ElementSection.Value is IMemberSection elementSection &&
                    elementSection.PhysicalSection.Value is ISection physicalSection)
                {
                    sectionName = physicalSection.LongName;
                }
                if (firstSpan.Material?.Value != null)
                {
                    materialName = firstSpan.Material.Value.Name;
                }
                double lengthFt = MmToFt(lift.Length);

                foreach (var loadingCase in loadingCases)
                {
                    IMemberLoading memberLoading = await member.GetLoadingAsync(loadingCase.Id, RequestedAnalysisType, LoadingResultType.Base);

                    var mzTask = GetMinMaxEccentricMomentInLift(memberLoading, LoadingDirection.Major, lift, reduced);
                    var myTask = GetMinMaxEccentricMomentInLift(memberLoading, LoadingDirection.Minor, lift, reduced);

                    await Task.WhenAll(mzTask, myTask);
                    var (eccMzMax, eccMzMin) = mzTask.Result;
                    var (eccMyMax, eccMyMin) = myTask.Result;

                    string line = $"{EscapeCsvValue(id.ToString())},{EscapeCsvValue(partMark)},{EscapeCsvValue(filterValue)},{EscapeCsvValue(member.Name)},{EscapeCsvValue(lift.Name)}," +
                                $"{EscapeCsvValue(startLevelName)},{EscapeCsvValue(endLevelName)},{EscapeCsvValue(sectionName)},{EscapeCsvValue(materialName)}," +
                                $"{startNodeIdx},{startX:F3},{startY:F3},{startZ:F3}," +
                                $"{endNodeIdx},{endX:F3},{endY:F3},{endZ:F3}," +
                                $"{lengthFt:F3},{EscapeCsvValue(loadingCase.Name)}," +
                                $"{eccMzMax},{eccMzMin},{eccMyMax},{eccMyMin}";
                    lines.Add(line);
                }
            }
            return lines;
        }

        private static async Task<(double max, double min)> GetMinMaxEccentricMomentInLift(
            IMemberLoading loading,
            LoadingDirection direction,
            ColumnLift lift,
            bool reduced)
        {
            double Nmm_to_kft = ConversionFactor(LoadingValueType.Moment);
            var tasks = new List<Task<(double? max, double? min)>>();

            foreach (var span in lift.Spans)
            {
                tasks.Add(Task.Run(async () =>
                {
                    int samplePoints = 10;
                    double spanLength = span.Length.Value;
                    var positionTasks = new List<Task<double?>>();

                    for (int i = 0; i <= samplePoints; i++)
                    {
                        double pos = (i * spanLength) / samplePoints;
                        positionTasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                var option = LoadingValueOptions.StaticValue(LoadingValueType.EccentricityMoment, direction, reduced);
                                var vals = await loading.GetValueAsync(option, span.Index, pos);
                                return vals.Any() ? (double?)vals.MaxBy(v => Math.Abs(v.Value))!.Value : null;
                            }
                            catch { return null; }
                        }));
                    }

                    var results = await Task.WhenAll(positionTasks);
                    var validValues = results.OfType<double>().Select(r => r * Nmm_to_kft).ToList();

                    if (validValues.Any())
                    {
                        return ((double?)validValues.Max(), (double?)validValues.Min());
                    }
                    return (null, null);
                }));
            }

            var spanResults = await Task.WhenAll(tasks);
            double globalMax = spanResults.Select(r => r.max).OfType<double>().DefaultIfEmpty(0.0).Max();
            double globalMin = spanResults.Select(r => r.min).OfType<double>().DefaultIfEmpty(0.0).Min();

            return (globalMax, globalMin);
        }


    }
}
