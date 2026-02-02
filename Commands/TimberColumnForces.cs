using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
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
    /// Interrogates Timber Column forces, calculating forces at stations along the column.
    /// </summary>
    public class TimberColumnForces : SolverInterrogator
    {
        /// <inheritdoc/>
        public override bool ShowInMenu() { return true; }

        /// <summary>Initializes a new instance of the <see cref="TimberColumnForces"/> class.</summary>
        public TimberColumnForces()
        {
            HasOutput = true;
            RequestedMemberType = new List<MemberConstruction>() { MemberConstruction.TimberColumn };
        }

        /// <summary>
        /// Executes the Timber Column Forces interrogation.
        /// </summary>
        public override async Task ExecuteAsync()
        {
            await InitializeAsync();

            if (Flag)
            {
                return;
            }

            // Data setup and diagnostics initialization
            Stopwatch stopwatch = Stopwatch.StartNew();
            int bufferSize = 65536 * 2;

            // Unpacking loading data
            LogLoadingSummary();

            stopwatch.Stop();
            List<ILoadingCase> loadingCases = AskLoading(SolvedCases, SolvedCombinations, SolvedEnvelopes);
            bool reduced = AskReduced();

            // Unpacking member data
            List<IMember> timberColumns = AskAndFilterMembers(false, false);

            // Apply UDA filters
            string? filterField = AskUser("What UDA field to filter on?");
            string? filterValue = AskUser("What UDA value to filter on?");

            stopwatch.Start();

            Console.WriteLine($"{AllMembers!.Count} structural members found in model.");
            Console.WriteLine($"{timberColumns.Count} timber columns found.");

            // Organize Levels
            List<IHorizontalConstructionPlane> levels = (await Model!.GetLevelsAsync()).ToList();

            double timeUnpack = Math.Round(stopwatch.Elapsed.TotalSeconds, 3);
            Console.WriteLine($"Loading and member data unpacked in {timeUnpack} seconds.\n");

            // Phase 1: Organize Column Lifts & Pre-Fetch Geometry
            FancyWriteLine("Organizing Column Lifts...", TextColor.Title);

            var liftData = new System.Collections.Concurrent.ConcurrentBag<ColumnLifts>();
            var allPointIndices = new System.Collections.Concurrent.ConcurrentBag<int>();

            var preTasks = new List<Task>();
            object consoleLock = new();

            foreach (var col in timberColumns)
            {
                preTasks.Add(Task.Run(async () =>
                {
                    var lifts = new ColumnLifts(col);
                    await lifts.OrganizeBySpliceAsync();

                    liftData.Add(lifts);

                    foreach (var lift in lifts.Lifts)
                    {
                        if (lift.Values.FirstOrDefault()?.StartMemberNode?.ConstructionPointIndex != null)
                            allPointIndices.Add(lift.Values.First().StartMemberNode.ConstructionPointIndex.Value);
                        if (lift.Values.LastOrDefault()?.EndMemberNode?.ConstructionPointIndex != null)
                            allPointIndices.Add(lift.Values.Last().EndMemberNode.ConstructionPointIndex.Value);
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
            FancyWriteLine("\nQuerying Timber Column Forces (Parallel)...", TextColor.Title);

            // Prepare CSV
            string file1 = SaveDirectory + @"TimberColumnForces_" + OutputFileName + ".csv";
            string header1 = "Tekla GUID,UDA Filter,Member Name,Lift Name,Included Spans,Start Level,End Level,Section,Breadth [in],Depth [in],Length [ft],Loading Name,Shear Major [k],Shear Minor [k],Moment Major [k-ft],Moment Minor [k-ft],Axial Force [k],Torsion [k-ft]\n";
            File.WriteAllText(file1, "");
            File.AppendAllText(file1, header1);

            var tasks = new List<Task<List<string>>>();
            foreach (var lifts in liftData)
            {
                tasks.Add(Task.Run(() => ProcessTimberColumnAsync(lifts.ParentMember, lifts, loadingCases, reduced, filterField, filterValue, levels, pointsDict)));
            }

            var results = await Task.WhenAll(tasks);
            double endWatch = Math.Round(stopwatch.Elapsed.TotalSeconds, 3);

            // Writing Results
            FancyWriteLine("Writing internal forces table...", TextColor.Title);
            using (StreamWriter sw1 = new(file1, true, Encoding.UTF8, bufferSize))
            {
                foreach (var res in results)
                {
                    foreach (var line in res)
                    {
                        sw1.WriteLine(line);
                    }
                }
            }

            FancyWriteLine("Saved to: ", file1, "", TextColor.Path);
            double sizeKB = Math.Round(new FileInfo(file1).Length / 1024.0, 2);
            Console.WriteLine($"File size: {sizeKB} KB");
            double timeCSV = Math.Round(stopwatch.Elapsed.TotalSeconds - endWatch, 3);
            Console.WriteLine($"Timber Column table written in {timeCSV} seconds.\n");

            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;

            Check();

        }

        private async Task<List<string>> ProcessTimberColumnAsync(
            IMember member,
            ColumnLifts columnLifts,
            List<ILoadingCase> loadingCases,
            bool reduced,
            string? filterField,
            string? filterValue,
            List<IHorizontalConstructionPlane> levels,
            Dictionary<int, IConstructionPoint> pointsDict)
        {
            var lines = new List<string>();
            string memberName = member.Name;
            Guid id = member.Id;

            // Ensure lifts are organized by splice before processing

            foreach (var lift in columnLifts.Lifts)
            {

                foreach (IMemberSpan span in lift.Values)
                {
                    var udas = await span.GetUserDefinedAttributesAsync();

                    // Filter Check
                    if (!string.IsNullOrEmpty(filterValue) && filterField != null)
                    {
                        bool match = udas.Any(c =>
                            (c as IUserDefinedTextAttribute)?.Text.Equals(filterValue, StringComparison.CurrentCultureIgnoreCase) == true &&
                            c?.AttributeDefinitionName.Equals(filterField, StringComparison.CurrentCultureIgnoreCase) == true);
                        if (!match) continue;
                    }

                    // Resolve Span Start Level
                    int startNodeIdx = span.StartMemberNode.ConstructionPointIndex.Value;
                    IConstructionPoint? startPoint = pointsDict.ContainsKey(startNodeIdx) ? pointsDict[startNodeIdx] : null;
                    string startLevelName = "Unknown";
                    if (startPoint != null)
                    {
                        if (startPoint.PlaneInfo.IsApplicable && startPoint.PlaneInfo.Value.Type == TSD.API.Remoting.Common.EntityType.HorizontalConstructionPlane)
                        {
                            var lvl = levels.FirstOrDefault(l => l.Index == startPoint.PlaneInfo.Value.Index);
                            if (lvl != null) startLevelName = lvl.Name;
                        }
                        if (startLevelName == "Unknown" && levels.Any())
                        {
                            double zStart = startPoint.Coordinates.Value.Z;
                            IHorizontalConstructionPlane? closestLevel = levels.MinBy(l => Math.Abs(zStart - l.Level.Value));
                            if (closestLevel != null) startLevelName = $"~{closestLevel.Name}";
                        }
                    }

                    // Resolve Span End Level
                    int endNodeIdx = span.EndMemberNode.ConstructionPointIndex.Value;
                    IConstructionPoint? endPoint = pointsDict.ContainsKey(endNodeIdx) ? pointsDict[endNodeIdx] : null;
                    string endLevelName = "Unknown";
                    if (endPoint != null)
                    {
                        if (endPoint.PlaneInfo.IsApplicable && endPoint.PlaneInfo.Value.Type == TSD.API.Remoting.Common.EntityType.HorizontalConstructionPlane)
                        {
                            var lvl = levels.FirstOrDefault(l => l.Index == endPoint.PlaneInfo.Value.Index);
                            if (lvl != null) endLevelName = lvl.Name;
                        }
                        if (endLevelName == "Unknown" && levels.Any())
                        {
                            double zEnd = endPoint.Coordinates.Value.Z;
                            IHorizontalConstructionPlane? closestLevel = levels.MinBy(l => Math.Abs(zEnd - l.Level.Value));
                            if (closestLevel != null) endLevelName = $"~{closestLevel.Name}";
                        }
                    }

                    // Span Geometry
                    double length = MmToFt(span.Length.Value); // [ft]

                    // Section Info (Per span)
                    string sectionName = "Unknown";
                    double breadth = 0;
                    double depth = 0;

                    if (span.ElementSection.Value != null)
                    {
                        var elementSection = (IMemberSection)span.ElementSection.Value;
                        if (elementSection.PhysicalSection.IsApplicable && elementSection.PhysicalSection.Value is ITimberBeamSection section)
                        {
                            sectionName = section.LongName;
                            breadth = Math.Round(MmToIn(section.Breadth), 4);
                            depth = Math.Round(MmToIn(section.Depth), 4);
                        }
                    }

                    string spanName = span.Name;
                    // Construct lift name for reporting consistency

                    string liftName = $"{memberName}-{lift.Name}"; // Keeping consistent with previous logic
                    string includedSpanText = span.Name;

                    foreach (ILoadingCase loadingCase in loadingCases)
                    {
                        // Calculate Envelope for THIS span only
                        SpanResults spanResults = new(span, 1, loadingCase, reduced, RequestedAnalysisType, member);
                        MaxSpanInfo maxSpanInfo = await spanResults.GetMaxima();

                        // Write Row
                        string liftLineOnly = $"{id},{filterValue},{memberName},{liftName},{includedSpanText},{startLevelName},{endLevelName},{sectionName},{Math.Round(breadth, 3)},{Math.Round(depth, 3)},{Math.Round(length, 3)}";
                        string maxLine = liftLineOnly + "," + $"{maxSpanInfo.LoadName},{maxSpanInfo.ShearMajor.Value},{maxSpanInfo.ShearMinor.Value},{maxSpanInfo.MomentMajor.Value},{maxSpanInfo.MomentMinor.Value},{maxSpanInfo.AxialForce.Value},{maxSpanInfo.Torsion.Value}";
                        lines.Add(maxLine);
                    }
                }
            }
            return lines;
        }
    }
}
