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
    /// Generates a summary of steel columns with lift information, internal forces, and eccentricity moments.
    /// </summary>
    public class SteelColumnSummary : SolverInterrogator
    {
        /// <inheritdoc/>
        public override bool ShowInMenu() => true;

        /// <summary>Initializes a new instance of the <see cref="SteelColumnSummary"/> class.</summary>
        public SteelColumnSummary()
        {
            HasOutput = true;
            RequestedMemberType = new List<MemberConstruction>() { MemberConstruction.SteelColumn };
        }

        /// <summary>
        /// Executes the steel column summary interrogation, writing results to CSV.
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

            FancyWriteLine("Organizing Column Summary Data...", TextColor.Title);

            // Phase 1: Organize Spans and Collect Indices
            var columnData = new System.Collections.Concurrent.ConcurrentBag<(IMember Member, ColumnSpansSteel Spans)>();
            var allPointIndices = new System.Collections.Concurrent.ConcurrentBag<int>();

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
                        Console.WriteLine($"Column {col.Name}:");
                        Console.WriteLine($"  -> Has splice? {(colSpans.HasSplice ? "Yes" : "No")}");
                        Console.WriteLine($"  -> {colSpans.Spans.Count} spans total");
                        Console.WriteLine($"  -> {lifts.Count} lifts created");

                        if (colSpans.HasSplice)
                        {
                            Console.WriteLine($"  -> Splice locations:");
                            foreach (var spliceInfo in colSpans.SpanSpliceInfo.Where(si => si.Value.HasSplice))
                            {
                                var span = colSpans.Spans.FirstOrDefault(s => s.Index == spliceInfo.Key);
                                string spanName = span?.Name ?? "Unknown";
                                double offsetInches = spliceInfo.Value.SpliceOffset / 25.4; // mm to inches
                                Console.WriteLine($"     Span {spliceInfo.Key} ({spanName}): splice at {offsetInches:F1}\" from start");
                            }

                            Console.WriteLine($"  -> Lift breakdown:");
                            for (int i = 0; i < lifts.Count; i++)
                            {
                                var lift = lifts[i];
                                double lengthFt = MmToFt(lift.Length);
                                var spanNames = string.Join(", ", lift.Spans.Select(s => s.Name));
                                Console.WriteLine($"     {lift.Name}: {lengthFt:F1}ft ({spanNames})");
                            }
                        }
                    }

                    // Collect indices from lifts (Start/End nodes)
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

            FancyWriteLine("Querying Steel Column Summary (Parallel)...", TextColor.Title);

            // Prepare output CSV file
            string file1 = SaveDirectory + @"SteelColumnSummary_" + OutputFileName + ".csv";
            string header1 = "Tekla GUID,Part Mark,UDA Filter,Member Name,Lift Name,Start Level,End Level,Shape,Material," +
                 "Start Node,Start Node Fixity,X_StartNode,Y_StartNode,Z_StartNode," +
                 "End Node,End Node Fixity,X_EndNode,Y_EndNode,Z_EndNode," +
                 "Lift Length [ft],Span Rotation [deg],Loading Name," +
                 "Column Axial Max [k],Column Axial Min [k],Column Major Moment Max [k-ft]," +
                 "Column Major Shear Max [k],Column Minor Moment Max [k-ft],Column Minor Shear Max [k]," +
                 "Column Ecc Mz Max [k-ft],Column Ecc Mz Min [k-ft],Column Ecc My Max [k-ft],Column Ecc My Min [k-ft]," +
                 "Integrity Force [k],Has Splice?\n";

            File.WriteAllText(file1, header1);

            var integrityForceCase = loadingCases.FirstOrDefault(lc =>
                    lc.Name.Equals("Integrity Force", StringComparison.CurrentCultureIgnoreCase) ||
                    lc.Name.Contains("Integrity", StringComparison.CurrentCultureIgnoreCase));

            // Phase 3: Process Summary (Parallel)
            var tasks = new List<Task<List<string>>>();
            foreach (var (member, spans) in columnData)
            {
                tasks.Add(Task.Run(() => ProcessColumnAsync(member, spans, loadingCases, reduced, filterField, filterValue, levels, integrityForceCase, pointsDict)));
            }

            var results = await Task.WhenAll(tasks);
            double endWatch = Math.Round(stopwatch.Elapsed.TotalSeconds, 3);

            FancyWriteLine("Writing Summary tables...", TextColor.Title);

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
            Console.WriteLine($"Summary table written in {timeCSV} seconds.\n");

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
            ILoadingCase? integrityForceCase,
            Dictionary<int, IConstructionPoint> pointsDict)
        {
            var lines = new List<string>();
            // colSpans already organized via OrganizeSpansAsync
            var lifts = colSpans.CreateLifts();

            // Calculate integrity forces if case exists
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
                string hasSpliceText = colSpans.HasSplice ? "Yes" : "No";

                // Geometry
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

                // Section
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
                double rotationDeg = RadToDeg(firstSpan.RotationAngle.Value);

                string startNodeFixity = GetNodeFixityDescription(firstSpan.StartReleases.Value);
                string endNodeFixity = GetNodeFixityDescription(lift.Spans.Last().EndReleases.Value);

                string startNodeName = $"{startNodeIdx}";
                string endNodeName = $"{endNodeIdx}";

                double integrityForce = 0.0;
                if (integrityForceCase != null && integrityForces.ContainsKey(lift.Name))
                {
                    integrityForce = integrityForces[lift.Name];
                }

                foreach (var loadingCase in loadingCases)
                {
                    IMemberLoading memberLoading = await member.GetLoadingAsync(loadingCase.Id, RequestedAnalysisType, LoadingResultType.Base);

                    // Calculations

                    var axialTask = GetMinMaxForceInLift(memberLoading, LoadingValueType.Force, LoadingDirection.Axial, lift, reduced);
                    var majorMomentTask = GetMaxForceInLift(memberLoading, LoadingValueType.Moment, LoadingDirection.Major, lift, reduced);
                    var majorShearTask = GetMaxForceInLift(memberLoading, LoadingValueType.Force, LoadingDirection.Major, lift, reduced);
                    var minorMomentTask = GetMaxForceInLift(memberLoading, LoadingValueType.Moment, LoadingDirection.Minor, lift, reduced);
                    var minorShearTask = GetMaxForceInLift(memberLoading, LoadingValueType.Force, LoadingDirection.Minor, lift, reduced);
                    var eccMajorTask = GetMinMaxEccentricMomentInLift(memberLoading, LoadingDirection.Major, lift, reduced);
                    var eccMinorTask = GetMinMaxEccentricMomentInLift(memberLoading, LoadingDirection.Minor, lift, reduced);

                    await Task.WhenAll(axialTask, majorMomentTask, majorShearTask, minorMomentTask, minorShearTask, eccMajorTask, eccMinorTask);

                    var (axialMax, axialMin) = axialTask.Result;
                    var majorMomentMax = majorMomentTask.Result;
                    var majorShearMax = majorShearTask.Result;
                    var minorMomentMax = minorMomentTask.Result;
                    var minorShearMax = minorShearTask.Result;
                    var (eccMajorMax, eccMajorMin) = eccMajorTask.Result;
                    var (eccMinorMax, eccMinorMin) = eccMinorTask.Result;

                    string line = $"{EscapeCsvValue(id.ToString())},{EscapeCsvValue(partMark)},{EscapeCsvValue(filterValue)},{EscapeCsvValue(member.Name)},{EscapeCsvValue(lift.Name)},{EscapeCsvValue(startLevelName)},{EscapeCsvValue(endLevelName)},{EscapeCsvValue(sectionName)},{EscapeCsvValue(materialName)}," +
                         $"{EscapeCsvValue(startNodeName)},{EscapeCsvValue(startNodeFixity)},{startX:F3},{startY:F3},{startZ:F3}," +
                         $"{EscapeCsvValue(endNodeName)},{EscapeCsvValue(endNodeFixity)},{endX:F3},{endY:F3},{endZ:F3}," +
                         $"{lengthFt:F3},{rotationDeg:F3},{EscapeCsvValue(loadingCase.Name)}," +
                         $"{axialMax},{axialMin},{majorMomentMax},{majorShearMax},{minorMomentMax},{minorShearMax}," +
                         $"{eccMajorMax},{eccMajorMin},{eccMinorMax},{eccMinorMin},{integrityForce},{EscapeCsvValue(hasSpliceText)}";

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
                                    return (double?)Math.Abs(vals.MaxBy(v => Math.Abs(v.Value))!.Value);
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
            return results.DefaultIfEmpty(0.0).Max() * ConversionFactor(valueType);
        }

        private static async Task<(double max, double min)> GetMinMaxForceInLift(
            IMemberLoading loading,
            LoadingValueType valueType,
            LoadingDirection direction,
            ColumnLift lift,
            bool reduced)
        {
            var tasks = new List<Task<(double max, double min)>>();
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
                                    return (double?)vals.MaxBy(v => Math.Abs(v.Value))?.Value;
                                }
                            }
                            catch { }
                            return (double?)null;
                        }));
                    }
                    var results = await Task.WhenAll(pointTasks);
                    var validResults = results.OfType<double>().ToList();

                    if (validResults.Any())
                    {
                        double maxVal = validResults.Max();
                        double minVal = validResults.Min();
                        return (maxVal > 0 ? maxVal : 0.0, minVal);
                    }

                    return (0.0, double.NaN); // Use NaN to signal no data for min aggregation
                }));
            }

            var results = await Task.WhenAll(tasks);
            double globalMax = results.Select(r => r.max).DefaultIfEmpty(0.0).Max();
            double globalMin = results.Select(r => r.min).Where(m => !double.IsNaN(m)).DefaultIfEmpty(0.0).Min();

            double valCon = ConversionFactor(valueType);
            return (globalMax * valCon, globalMin * valCon);
        }

        private static async Task<(double max, double min)> GetMinMaxEccentricMomentInLift(
            IMemberLoading loading,
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
                    var option = LoadingValueOptions.StaticValue(LoadingValueType.EccentricityMoment, direction, reduced);

                    var pointTasks = new List<Task<double?>>();
                    for (int i = 0; i <= samplePoints; i++)
                    {
                        double pos = (i * spanLength) / samplePoints;
                        pointTasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                var vals = await loading.GetValueAsync(option, span.Index, pos);
                                if (vals.Any()) return (double?)vals.First().Value;
                            }
                            catch { }
                            return (double?)null;
                        }));
                    }
                    var results = await Task.WhenAll(pointTasks);
                    var validResults = results.OfType<double>().ToList();

                    if (validResults.Any())
                    {
                        return (validResults.Max(), (double?)validResults.Min());
                    }
                    return (0.0, (double?)null);
                }));
            }

            var results = await Task.WhenAll(tasks);
            double globalMax = results.Select(r => r.max).DefaultIfEmpty(0.0).Max();
            double globalMin = results.Select(r => r.min).OfType<double>().DefaultIfEmpty(0.0).Min();

            double factor = ConversionFactor(LoadingValueType.Moment);
            return (globalMax * factor, globalMin * factor);
        }

        private static async Task<double> GetMaxEccentricMomentInLift(
            IMemberLoading loading,
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
                    var option = LoadingValueOptions.StaticValue(LoadingValueType.EccentricityMoment, direction, reduced);

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
                                    return (double?)Math.Abs(vals.MaxBy(v => Math.Abs(v.Value))!.Value);
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
            return results.DefaultIfEmpty(0.0).Max() * ConversionFactor(LoadingValueType.Moment);
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

                // Splice forces
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
            catch
            {
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
                if (values.Any()) return values.MaxBy(v => Math.Abs(v.Value))?.Value ?? 0.0;
            }
            catch { }
            return 0.0;
        }


    }
}