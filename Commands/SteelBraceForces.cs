using System;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using MathNet.Numerics.LinearAlgebra;
using TeklaResultsInterrogator.Core;
using TeklaResultsInterrogator.Utils;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Sections;
using TSD.API.Remoting.Structure;
using TSD.API.Remoting.UserDefinedAttributes;
using static TeklaResultsInterrogator.Utils.ConsoleUtils;
using AnalysisType = TSD.API.Remoting.Solver.AnalysisType;



namespace TeklaResultsInterrogator.Commands
{
    /// <summary>
    /// Interrogates Steel Brace forces, calculating axial forces.
    /// </summary>
    public class SteelBraceForces : SolverInterrogator
    {

        /// <inheritdoc/>
        public override bool ShowInMenu() { return true; }

        /// <summary>Initializes a new instance of the <see cref="SteelBraceForces"/> class.</summary>
        public SteelBraceForces()
        {
            HasOutput = true;
            RequestedMemberType = new List<MemberConstruction>() { MemberConstruction.SteelBrace };
        }

        /// <summary>
        /// Executes the Steel Brace Forces interrogation, writing results to CSV.
        /// </summary>
        public override async Task ExecuteAsync()
        {
            // Initialize parents
            await InitializeAsync();

            // Check for null properties
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
            bool reduced = false; // Braces do not support live load reductions

            List<IMember> steelBraces = AskAndFilterMembers(true, true);
            List<IConstructionPoint> allConstructionPoints = (await Model!.GetConstructionPointsAsync(null)).ToList();

            string? filterField = AskUser("What UDA field to filter on?");
            string? filterValue = AskUser("What UDA value to filter on?");

            stopwatch.Start();
            Console.WriteLine($"{AllMembers!.Count} structural members found in model.");
            Console.WriteLine($"{steelBraces.Count} steel braces found.");

            double timeUnpack = Math.Round(stopwatch.Elapsed.TotalSeconds, 3);
            Console.WriteLine($"Loading and member data unpacked in {timeUnpack} seconds.\n");

            // Extracting internal forces
            FancyWriteLine("Retrieving internal forces...", TextColor.Title);
            int subdivisions = 1;
            FancyWriteLine($"Asked for {subdivisions} points.", TextColor.Warning);

            // Setting up file
            double start1 = timeUnpack;
            string file1 = SaveDirectory + @"SteelBraceForces_" + OutputFileName + ".csv";
            string header1 = String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17} \n",
                "Tekla GUID", "Member Name", "Level", "Grid", "Shape", "Material",
                "Start Node", "Start Node X", "Start Node Y", "Start Node Z", "End Node", "End Node X", "End Node Y", "End Node Z",
                "Span Length [ft]", "Span Rotation [deg]", "Loading Name", "Axial Force [k]");

            // Phase 1: Pre-fetch Identification (Parallel)
            var braceData = new List<(IMember Member, IEnumerable<IMemberSpan> Spans)>();
            var allPointIndices = new List<int>();

            FancyWriteLine("Identifying spans and nodes...", TextColor.Title);
            var preTasks = new List<Task>();
            foreach (var brace in steelBraces)
            {
                preTasks.Add(Task.Run(async () =>
                {
                    var spans = await brace.GetSpanAsync();
                    lock (braceData)
                    {
                        braceData.Add((brace, spans));
                    }
                    lock (allPointIndices)
                    {
                        // Member Start Node (for Level/Grid info via construction point)
                        if (brace.MemberNodes.Value.First().Value.ConstructionPointIndex != null)
                            allPointIndices.Add(brace.MemberNodes.Value.First().Value.ConstructionPointIndex.Value);

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

            // Phase 2: Batch Fetch Data
            var uniqueIndices = allPointIndices.Distinct().ToList();
            Console.WriteLine($"\nFetching coordinates for {uniqueIndices.Count} unique points...");
            var pointsList = await Model!.GetConstructionPointsAsync(uniqueIndices);
            var pointsDict = pointsList.ToDictionary(p => p.Index, p => p);

            // Fetch All Levels (Horizontal Planes) and Grids (Vertical Planes/Frames)
            // Original code fetched specific levels but all frames.
            // Getting all levels is efficient enough typically.
            Console.WriteLine("Fetching all Levels and Grids...");
            var levels = await Model.GetLevelsAsync(null);
            var levelsDict = levels.ToDictionary(l => l.Index, l => l); // Map Plane Index -> Level Object

            var grids = await Model.GetFramesAsync(null);
            // Grids are checked by geometry (point on plane), not just by index association.

            // Phase 3: Process Forces (Parallel)
            FancyWriteLine("\nQuerying Steel Brace Forces (Parallel)...", TextColor.Title);

            // Prepare CSV
            // Bug fix: The original code wrote an empty string first, then appended header. 
            // We can just write the header.
            File.WriteAllText(file1, header1);

            var tasks = new List<Task<List<string>>>();

            foreach (var (member, spans) in braceData)
            {
                tasks.Add(Task.Run(() => ProcessMemberAsync(member, spans, loadingCases, reduced, RequestedAnalysisType, filterField, filterValue, pointsDict, levelsDict, grids, subdivisions)));
            }

            var results = await Task.WhenAll(tasks);

            // Phase 4: Output
            FancyWriteLine("Writing internal forces table...", TextColor.Title);
            using (StreamWriter sw1 = new(file1, true, Encoding.UTF8, bufferSize))
            {
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
            Console.WriteLine($"Steel Brace table written in {time1} seconds.\n");

            // Finish up
            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;

            Check();

            return;
        }

        private static async Task<List<string>> ProcessMemberAsync(IMember member, IEnumerable<IMemberSpan> spans, List<ILoadingCase> loadingCases, bool reduced, AnalysisType analysisType, string? filterField, string? filterValue, Dictionary<int, IConstructionPoint> pointsDict, Dictionary<int, IHorizontalConstructionPlane> levelsDict, IEnumerable<IVerticalConstructionPlane> grids, int subdivisions)
        {
            var lines = new List<string>();
            string name = member.Name;
            Guid id = member.Id;

            int constructionPointIndex = member.MemberNodes.Value.First().Value.ConstructionPointIndex.Value;

            // Resolve Level
            string levelName = "Not Associated";
            if (pointsDict.TryGetValue(constructionPointIndex, out var constructionPoint))
            {
                int planeId = constructionPoint.PlaneInfo.Value.Index;
                if (levelsDict.TryGetValue(planeId, out var level))
                {
                    levelName = level.Name;
                }
            }

            // Grids are resolved by geometry (point on plane) to ensure accuracy across frames.

            foreach (IMemberSpan span in spans)
            {
                var udas = await span.GetUserDefinedAttributesAsync();

                // if there isn't at least one uda matching filterValue, skip code below
                if (!string.IsNullOrEmpty(filterValue))
                {
                    bool udaMatchingFilterValueExists = udas.Where(c =>
                        (c as IUserDefinedTextAttribute)?.Text.Equals(filterValue, StringComparison.CurrentCultureIgnoreCase) == true
                        && c?.AttributeDefinitionName.Equals(filterField, StringComparison.CurrentCultureIgnoreCase) == true)
                        ?.Any() == true;
                    if (!udaMatchingFilterValueExists) continue;
                }

                int spanIdx = span.Index;
                double length = span.Length.Value;
                double lengthFt = MmToFt(length); // Converting from [mm] to [ft]
                double rot = Math.Round(RadToDeg(span.RotationAngle.Value), 3); // Converting from [rad] to [deg]
                IMemberSection section = (IMemberSection)span.ElementSection.Value;
                string sectionName = section.PhysicalSection.Value.LongName;
                string materialGrade = span.Material.Value.Name;

                int startNodeIdx = span.StartMemberNode.ConstructionPointIndex.Value;
                int endNodeIdx = span.EndMemberNode.ConstructionPointIndex.Value;

                // Points must exist in dictionary because we fetched based on these indices
                IConstructionPoint startConstructionPoint = pointsDict[startNodeIdx];
                IConstructionPoint endConstructionPoint = pointsDict[endNodeIdx];

                double sux = startConstructionPoint.Coordinates.Value.X;  // Nodal coordinates [base units]
                double suy = startConstructionPoint.Coordinates.Value.Y;
                double suz = startConstructionPoint.Coordinates.Value.Z;

                double eux = endConstructionPoint.Coordinates.Value.X;  // Nodal coordinates [base units]
                double euy = endConstructionPoint.Coordinates.Value.Y;
                double euz = endConstructionPoint.Coordinates.Value.Z;

                string gridName = "";

                foreach (IConstructionPlane plane in grids)
                {
                    // Gets the components of the normal vector to the plane
                    double N_X = plane.Plane.Value.Normal.Value.X;
                    double N_Y = plane.Plane.Value.Normal.Value.Y;
                    double N_Z = plane.Plane.Value.Normal.Value.Z;

                    // Gets the coordinates of the origin of the plane
                    double gridOX = plane.Plane.Value.Origin.Value.X;
                    double gridOY = plane.Plane.Value.Origin.Value.Y;
                    double gridOZ = plane.Plane.Value.Origin.Value.Z;

                    // Use the DOT product to test if the line is on the plane
                    double line_on_plane_test = (eux - gridOX) * N_X + (euy - gridOY) * N_Y + (euz - gridOZ) * N_Z;
                    // Using tolerance for double equality check
                    if (Math.Abs(line_on_plane_test) < 1e-6)
                    {
                        gridName = plane.Name;
                        // Original code kept looping, so last matching grid name wins?
                        // "gridName = plane.Name;" -> yes.
                    }
                }

                string spanLineOnly = String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15}",
                    id, name, levelName, gridName, sectionName, materialGrade,
                    startNodeIdx, MmToFt(sux), MmToFt(suy), MmToFt(suz), endNodeIdx, MmToFt(eux), MmToFt(euy), MmToFt(euz), lengthFt, rot);

                if (subdivisions == 0)
                {
                    lines.Add(spanLineOnly);
                }
                else
                {
                    foreach (ILoadingCase loadingCase in loadingCases)
                    {
                        string loadName = loadingCase.Name.Replace(',', '`');
                        SpanResults spanResults = new(span, subdivisions, loadingCase, reduced, analysisType, member);

                        if (subdivisions >= 1)
                        {
                            // Getting maximum internal forces and displacements and locations
                            MaxSpanInfo maxSpanInfo = await spanResults.GetMaxima();
                            string maxLine = spanLineOnly + "," + String.Format("{0},{1}",
                                loadName,
                                maxSpanInfo.AxialForce.Value);
                            lines.Add(maxLine);
                        }

                    }
                }
            }
            return lines;
        }
    }
}