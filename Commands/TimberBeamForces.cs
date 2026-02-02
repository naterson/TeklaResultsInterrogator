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
    /// Interrogates Timber Beam forces.
    /// </summary>
    internal class TimberBeamForces : SolverInterrogator
    {

        /// <inheritdoc/>
        public override bool ShowInMenu() { return true; }

        /// <summary>Initializes a new instance of the <see cref="TimberBeamForces"/> class.</summary>
        public TimberBeamForces()
        {
            HasOutput = true;
            RequestedMemberType = new List<MemberConstruction>() { MemberConstruction.TimberBeam };
        }

        /// <summary>
        /// Executes the Timber Beam Forces interrogation.
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

            List<IMember> timberBeams = AskAndFilterMembers(false, false);

            string? filterField = AskUser("What UDA field to filter on?");
            string? filterValue = AskUser("What UDA value to filter on?");

            stopwatch.Start();
            Console.WriteLine($"{AllMembers!.Count} structural members found in model.");
            Console.WriteLine($"{timberBeams.Count} timber beams found.");

            double timeUnpack = Math.Round(stopwatch.Elapsed.TotalSeconds, 3);
            Console.WriteLine($"Loading and member data unpacked in {timeUnpack} seconds.\n");

            // Extracting internal forces
            FancyWriteLine("Retrieving internal forces...", TextColor.Title);

            // Setting up file
            double start1 = timeUnpack;
            string file1 = SaveDirectory + @"TimberBeamForces_" + OutputFileName + ".csv";
            string header1 = String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22}\n",
                "Tekla GUID", "Member Name", "Filter UDA Name", "Level", "Section", "Breadth [in]", "Depth [in]", "Span Name",
                "Start Node", "Start Node Fixity", "End Node", "End Node Fixity",
                "Span Length [ft]", "Span Rotation [deg]", "Loading Name",
                "Shear Major [k]", "Shear Minor [k]", "Moment Major [k-ft]", "Moment Minor [k-ft]",
                "Axial Force [k]", "Torsion [k-ft]", "Deflection Major [in]", "Deflection Minor [in]");
            File.WriteAllText(file1, "");
            File.AppendAllText(file1, header1);

            // Phase 1: Pre-fetch Identification (Parallel)
            var timberData = new List<(IMember Member, IEnumerable<IMemberSpan> Spans)>();
            var allPointIndices = new List<int>();

            // Collect spans and point indices
            FancyWriteLine("Identifying spans and nodes...", TextColor.Title);
            var preTasks = new List<Task>();
            foreach (var beam in timberBeams)
            {
                preTasks.Add(Task.Run(async () =>
                {
                    var spans = await beam.GetSpanAsync();
                    lock (timberData)
                    {
                        timberData.Add((beam, spans));
                    }
                    lock (allPointIndices)
                    {
                        // Member Start Node (for Level)
                        if (beam.MemberNodes.Value.First().Value.ConstructionPointIndex != null)
                            allPointIndices.Add(beam.MemberNodes.Value.First().Value.ConstructionPointIndex.Value);

                        // Span Start/End Nodes
                        foreach (var span in spans)
                        {
                            if (span.StartMemberNode?.ConstructionPointIndex != null)
                                allPointIndices.Add(span.StartMemberNode.ConstructionPointIndex.Value);
                            if (span.EndMemberNode?.ConstructionPointIndex != null)
                                allPointIndices.Add(span.EndMemberNode.ConstructionPointIndex.Value);
                        }
                    }
                }));
            }
            await Task.WhenAll(preTasks);

            // Phase 2: Batch Fetch Construction Points & Levels
            var uniqueIndices = allPointIndices.Distinct().ToList();
            Console.WriteLine($"\nFetching coordinates for {uniqueIndices.Count} unique points...");
            var pointsList = await Model!.GetConstructionPointsAsync(uniqueIndices);
            var pointsDict = pointsList.ToDictionary(p => p.Index, p => p);

            // Identify unique planes (Levels) from member start nodes
            var planeIndices = pointsList.Where(p => p.PlaneInfo.Value.Type == TSD.API.Remoting.Common.EntityType.HorizontalConstructionPlane)
                                         .Select(p => p.PlaneInfo.Value.Index).Distinct().ToList();
            var levelsList = await Model.GetLevelsAsync(planeIndices);
            var levelsDict = levelsList.ToDictionary(l => l.Index, l => l); // Map Plane Index -> Level Object

            // Getting internal forces and writing table
            FancyWriteLine("\nQuerying Timber Beam Forces (Parallel)...", TextColor.Title);

            // Phase 3: Process Forces (Parallel)
            var tasks = new List<Task<List<string>>>();



            foreach (var (member, spans) in timberData)
            {
                tasks.Add(Task.Run(() => ProcessMemberAsync(member, spans, loadingCases, reduced, RequestedAnalysisType, filterField, filterValue, pointsDict, levelsDict)));
            }

            var results = await Task.WhenAll(tasks);

            // Phase 4: Output
            FancyWriteLine("Writing internal forces table...", TextColor.Title);
            using (StreamWriter sw1 = new(file1, true, Encoding.UTF8, bufferSize))
            {
                // Collective output of results

                foreach (var batch in results)
                {
                    foreach (var line in batch)
                    {
                        sw1.WriteLine(line);
                    }
                }
            }

            // Output diagnostics to console
            FancyWriteLine("Saved to: ", file1, "", TextColor.Path);
            double size1 = Math.Round((double)new FileInfo(file1).Length / 1024, 2);
            Console.WriteLine($"File size: {size1} KB");
            double time1 = Math.Round(stopwatch.Elapsed.TotalSeconds - start1, 3);
            Console.WriteLine($"Timber Beam table written in {time1} seconds.\n");

            // Finish up
            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;

            Check();

            return;
        }

        private static async Task<List<string>> ProcessMemberAsync(IMember member, IEnumerable<IMemberSpan> spans, List<ILoadingCase> loadingCases, bool reduced, AnalysisType analysisType, string? filterField, string? filterValue, Dictionary<int, IConstructionPoint> pointsDict, Dictionary<int, IHorizontalConstructionPlane> levelsDict)
        {
            var lines = new List<string>();
            string name = member.Name;
            Guid id = member.Id;

            // Get Level Name
            string levelName = "Not Associated";
            if (member.MemberNodes.Value.First().Value.ConstructionPointIndex != null)
            {
                int startNodeIdx = member.MemberNodes.Value.First().Value.ConstructionPointIndex.Value;
                if (pointsDict.TryGetValue(startNodeIdx, out var startPoint))
                {
                    int planeId = startPoint.PlaneInfo.Value.Index;
                    if (levelsDict.TryGetValue(planeId, out var level))
                    {
                        levelName = level.Name;
                    }
                }
            }

            foreach (IMemberSpan span in spans)
            {
                var udas = await span.GetUserDefinedAttributesAsync();


                if (!string.IsNullOrEmpty(filterValue))
                {
                    bool udaMatchingFilterValueExists = udas.Where(c =>
                        (c as IUserDefinedTextAttribute)?.Text.Equals(filterValue, StringComparison.CurrentCultureIgnoreCase) == true
                        && c?.AttributeDefinitionName.Equals(filterField, StringComparison.CurrentCultureIgnoreCase) == true)
                        ?.Any() == true;
                    if (!udaMatchingFilterValueExists)
                    {
                        continue;
                    }
                }

                string spanName = span.Name;
                double length = span.Length.Value;
                double lengthFt = MmToFt(length); // Converting from [mm] to [ft]
                double rot = Math.Round(RadToDeg(span.RotationAngle.Value), 3); // Converting from [rad] to [deg]
                IMemberSection generalSection = (IMemberSection)span.ElementSection.Value;
                ITimberBeamSection section = (ITimberBeamSection)generalSection.PhysicalSection.Value;
                string sectionName = section.LongName;
                double breadth = Math.Round(MmToIn(section.Breadth), 4);  // Converting from [mm] to [in]
                double depth = Math.Round(MmToIn(section.Depth), 4);  // Converting from [mm] to [in]

                int startNodeIdx = span.StartMemberNode.ConstructionPointIndex.Value;
                string startNodeFixity = GetProperty(span.StartReleases.Value.DegreeOfFreedom).ToString();
                if (GetProperty(span.StartReleases.Value.Cantilever) == true)
                {
                    startNodeFixity += " (Cantilever end)";
                }
                startNodeFixity = startNodeFixity.Replace(',', '|');
                int endNodeIdx = span.EndMemberNode.ConstructionPointIndex.Value;
                string endNodeFixity = GetProperty(span.EndReleases.Value.DegreeOfFreedom).ToString();
                if (GetProperty(span.EndReleases.Value.Cantilever) == true)
                {
                    endNodeFixity += " (Cantilever end)";
                }
                endNodeFixity = endNodeFixity.Replace(',', '|');

                string spanLineOnly = $"{id},{name},{filterValue},{levelName},{sectionName},{breadth},{depth},{spanName},{startNodeIdx},{startNodeFixity},{endNodeIdx},{endNodeFixity},{lengthFt},{rot}";

                foreach (ILoadingCase loadingCase in loadingCases)
                {
                    string loadName = loadingCase.Name.Replace(',', '`');
                    SpanResults spanResults = new(span, 1, loadingCase, reduced, analysisType, member);

                    // Getting maximum internal forces and displacements and locations
                    MaxSpanInfo maxSpanInfo = await spanResults.GetMaxima();
                    string maxLine = spanLineOnly + "," +
                            $"{loadName},{maxSpanInfo.ShearMajor.Value},{maxSpanInfo.ShearMinor.Value},{maxSpanInfo.MomentMajor.Value},{maxSpanInfo.MomentMinor.Value},{maxSpanInfo.AxialForce.Value},{maxSpanInfo.Torsion.Value},{maxSpanInfo.DeflectionMajor.Value},{maxSpanInfo.DeflectionMinor.Value}";
                    lines.Add(maxLine);
                }
            }
            return lines;
        }
    }
}
