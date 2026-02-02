using System.Diagnostics;
using System.Text;
using Google.Protobuf.WellKnownTypes;
using MathNet.Numerics.Providers.SparseSolver;
using TeklaResultsInterrogator.Utils;
using TSD.API.Remoting;
using TSD.API.Remoting.Common;
using TSD.API.Remoting.Document;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Solver;
using TSD.API.Remoting.Structure;
using static TeklaResultsInterrogator.Utils.ConsoleUtils;
using AnalysisType = TSD.API.Remoting.Solver.AnalysisType;

namespace TeklaResultsInterrogator.Core
{
    /// <summary>
    /// Base class for interrogators that interface with the TSD Solver (Analysis Results).
    /// </summary>
    public class SolverInterrogator : BaseInterrogator
    {

        /// <summary>The TSD Solver Model interface.</summary>
        protected TSD.API.Remoting.Solver.IModel? SolverModel { get; private set; }
        /// <summary>The user-selected Analysis Type.</summary>
        protected AnalysisType RequestedAnalysisType { get; private set; }
        /// <summary>All Loadcases found in the model.</summary>
        protected List<ILoadcase>? AllLoadcases { get; private set; }
        /// <summary>Solved Loadcases found in the model.</summary>
        protected List<ILoadcase>? SolvedCases { get; private set; }
        /// <summary>All Combinations found in the model.</summary>
        protected List<ICombination>? AllCombinations { get; private set; }
        /// <summary>Solved Combinations found in the model.</summary>
        protected List<ICombination>? SolvedCombinations { get; private set; }
        /// <summary>All Envelopes found in the model.</summary>
        protected List<IEnvelope>? AllEnvelopes { get; private set; }
        /// <summary>Solved Envelopes found in the model.</summary>
        protected List<IEnvelope>? SolvedEnvelopes { get; private set; }
        /// <summary>All 1D Members found in the model.</summary>
        protected List<IMember>? AllMembers { get; set; }

        /// <summary>The list of member types to include in the query.</summary>
        protected List<MemberConstruction> RequestedMemberType = new();

        /// <summary>Initializes a new instance of the <see cref="SolverInterrogator"/> class.</summary>
        public SolverInterrogator() { }

        /// <summary>
        /// Initializes the Solver Interrogator by connecting to the TSD Solver, resolving Analysis Type, and unpacking Loading/Member data.
        /// </summary>
        public override async Task InitializeAsync()
        {
            // Set up
            Stopwatch stopwatch = Stopwatch.StartNew();
            await InitializeBaseAsync();

            // Get Model
            Console.WriteLine("Searching for analysis solver model...");
            if (Model == null)
            {
                FancyWriteLine("No model found!", TextColor.Error);
                Flag = true;
                return;
            }

            AnalysisType defaultAnalysisType = AnalysisType.FirstOrderLinear;// Default

            List<AnalysisType> solvedAnalysisTypes = new();

            foreach (AnalysisType analysisType in System.Enum.GetValues(typeof(AnalysisType)))
            {
                if (analysisType != AnalysisType.Unknown && analysisType != AnalysisType.None)
                {

                    IEnumerable<TSD.API.Remoting.Solver.IModel> solverModels2 = await Model.GetSolverModelsAsync(new[] { analysisType });
                    if (solverModels2.Any())
                    {

                        solvedAnalysisTypes.Add(analysisType);
                    }
                }
            }

            int count = 0;

            foreach (AnalysisType analysisType in solvedAnalysisTypes)
            {
                count++;
                FancyWriteLine(count.ToString() + " - " + analysisType.ToString(), TextColor.Text);
            }
            stopwatch.Stop();
            string? readIn = AskUser("Input a number or hit Enter to use default 1st Order Linear. ");
            stopwatch.Start();
            bool keepReading = int.TryParse(readIn, out int intOption);
            if (!string.IsNullOrEmpty(readIn) && keepReading)
            {
                RequestedAnalysisType = solvedAnalysisTypes[intOption - 1];
            }
            else
            {
                RequestedAnalysisType = defaultAnalysisType;
            }

            IEnumerable<TSD.API.Remoting.Solver.IModel> solverModels = await Model.GetSolverModelsAsync(new[] { defaultAnalysisType });
            if (!solverModels.Any())
            {
                FancyWriteLine("No solver models found!", TextColor.Error);
                Flag = true;
                return;
            }

            TSD.API.Remoting.Solver.IModel? solverModel = solverModels.FirstOrDefault();
            if (solverModel == null)
            {
                FancyWriteLine("No solver model found!", TextColor.Error);
                Flag = true;
                return;
            }
            SolverModel = solverModel;

            // Get Analysis Results
            Console.WriteLine("Searching for analysis results...");
            IAnalysisResults? solverResults = await SolverModel.GetResultsAsync();
            if (solverResults == null)
            {
                FancyWriteLine("No results found for requested analysis type!", TextColor.Error);
                Flag = true;
                return;
            }
            IAnalysis3DResults? analysis3Dresults = await solverResults.GetAnalysis3DAsync();
            if (analysis3Dresults == null)
            {
                FancyWriteLine("No 3-D analysis results found for requested analysis type!", TextColor.Error);
                Flag = true;
                return;
            }
            var solvedLoadingGuids = await analysis3Dresults.GetSolvedLoadingIdsAsync();
            if (!solvedLoadingGuids.Any())
            {
                FancyWriteLine("No solved loading GUIDs found!", TextColor.Error);
                Flag = true;
                return;
            }

            // Get members
            Console.WriteLine("Searching for members...");
            IEnumerable<IMember>? allMembers = await Model.GetMembersAsync(null);
            if (allMembers == null || !allMembers.Any())
            {
                FancyWriteLine("No members found!", TextColor.Error);
                Flag = true;
                return;
            }
            AllMembers = allMembers.ToList();

            // Get solved loadcases
            Console.WriteLine("Searching for solved loadcases...");
            IEnumerable<ILoadcase> loadingCases = await Model.GetLoadcasesAsync(null);
            if (!loadingCases.Any())
            {
                FancyWriteLine("No loadcases found!", TextColor.Warning);
            }
            AllLoadcases = loadingCases.Where(lc => lc.Name != "0 ").ToList();  // Eliminating "0" slab unit load and roof unit load loadcases
            List<ILoadcase> solvedCases = AllLoadcases.Where(c => solvedLoadingGuids.Contains(c.Id)).ToList();
            if (!solvedCases.Any())
            {
                FancyWriteLine("No solved loadcases found!", TextColor.Warning);
            }
            SolvedCases = solvedCases;

            // Get solved load combos
            Console.WriteLine("Searching for solved load combinations...");
            IEnumerable<ICombination> loadingCombinations = await Model.GetCombinationsAsync(null);
            if (!loadingCombinations.Any())
            {
                FancyWriteLine("No load combinations found!", TextColor.Warning);
            }
            AllCombinations = loadingCombinations.ToList();
            List<ICombination> solvedCombinations = loadingCombinations.Where(c => solvedLoadingGuids.Contains(c.Id)).ToList();
            if (!solvedCombinations.Any())
            {
                FancyWriteLine("No solved load combinations found!", TextColor.Warning);
            }
            SolvedCombinations = solvedCombinations;

            // Get solved load envelopes
            Console.WriteLine("Searching for solved load envelopes...");
            IEnumerable<IEnvelope> loadingEnvelopes = await Model.GetEnvelopesAsync(null);
            if (!loadingEnvelopes.Any())
            {
                FancyWriteLine("No load envelopes found!", TextColor.Warning);
                Flag = false;  // Do not raise flag for no envelopes
            }
            AllEnvelopes = loadingEnvelopes.ToList();
            List<IEnvelope> solvedEnvelopes = new();
            foreach (IEnvelope envelope in AllEnvelopes)
            {
                List<TSD.API.Remoting.Common.Properties.IReadOnlyProperty<Guid>> combinationIds = envelope.CombinationIds.ToList();
                if (combinationIds.All(id => solvedLoadingGuids.Contains(id.Value)))
                {
                    solvedEnvelopes.Add(envelope);
                }
            }
            if (!solvedEnvelopes.Any())
            {
                FancyWriteLine("No solved load envelopes found!", TextColor.Warning);
            }
            SolvedEnvelopes = solvedEnvelopes;

            // Check to make sure there are solved load cases
            if (AllLoadcases.Count + AllCombinations.Count + AllEnvelopes.Count == 0)
            {
                FancyWriteLine("No loading found!", TextColor.Error);
                Flag = true;
                return;
            }
            if (SolvedCases.Count + SolvedCombinations.Count + SolvedEnvelopes.Count == 0)
            {
                FancyWriteLine("No solved loading found!", TextColor.Error);
                Flag = true;
                return;
            }

            // Finish up
            stopwatch.Stop();
            InitializationTime = stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine($"Initialization completed in {Math.Round(InitializationTime, 3)} seconds.\n");

            Check();

            return;
        }




        /// <summary>
        /// Prompts the user to select from available loading conditions (Cases, Combinations, Envelopes).
        /// </summary>
        /// <param name="solvedCases">List of solved loadcases.</param>
        /// <param name="solvedCombinations">List of solved load combinations.</param>
        /// <param name="solvedEnvelopes">List of solved load envelopes.</param>
        /// <returns>A list of selected loading cases.</returns>
        public static List<ILoadingCase> AskLoading(List<ILoadcase>? solvedCases, List<ICombination>? solvedCombinations, List<IEnvelope>? solvedEnvelopes)
        {
            Dictionary<string, List<ILoadingCase>> loadingOptions = new(StringComparer.InvariantCultureIgnoreCase);

            if (solvedCases != null && solvedCases.Count > 0)
            {
                loadingOptions.Add("Cases", solvedCases.Cast<ILoadingCase>().ToList());
            }
            if (solvedCombinations != null && solvedCombinations.Count > 0)
            {
                loadingOptions.Add("Combos", solvedCombinations.Cast<ILoadingCase>().ToList());
            }
            if (solvedEnvelopes != null && solvedEnvelopes.Count > 0)
            {
                loadingOptions.Add("Envelopes", solvedEnvelopes.Cast<ILoadingCase>().ToList());
            }

            List<ILoadingCase>? loadingCases = null;

            FancyWriteLine("Available loading conditions:", TextColor.Text);
            foreach (string condition in loadingOptions.Keys)
            {
                FancyWriteLine($"  {condition}", TextColor.Command);
            }

            do
            {
                string? readIn = AskUser("Choose an available loading condition:", loadingOptions.Keys);
                if (readIn != null && loadingOptions.ContainsKey(readIn))
                {

                    loadingCases = loadingOptions[readIn];

                    FancyWriteLine("Available loading:", TextColor.Text);
                    foreach (var load in loadingCases.OrderBy(o => o.ReferenceIndex))
                    {
                        FancyWriteLine(load.Name, TextColor.Text);
                    }
                    readIn = AskUser("Input a number or hit Enter to get all: ");
                    if (!string.IsNullOrEmpty(readIn))
                    {
                        loadingCases = loadingCases.Where(load => load.ReferenceIndex.Equals(Convert.ToInt32(readIn))).ToList();
                    }
                }
                else
                {
                    FancyWriteLine("Loading Condition", $" {readIn}", " not found.", TextColor.Command);
                }
            } while (loadingCases == null);

            return loadingCases;
        }

        /// <summary>
        /// Prompts user to filter by Gravity Only members.
        /// </summary>
        /// <returns>True for Gravity Only, False for Lateral Only, Null for All.</returns>
        public static bool? AskGravityOnly()
        {
            string? readIn = AskUser("Query Gravity Only members [Y/N, Enter for All]:", new List<string> { "Y", "N" });

            if (readIn == "Y")
            {
                return true;
            }
            else if (readIn == "N")
            {
                return false;
            }
            else if (string.IsNullOrEmpty(readIn))
            {
                return null;
            }
            else
            {
                FancyWriteLine("Input ", $"{readIn}", " not recognized. All members will be returned", TextColor.Command);
                return null;
            }
        }

        /// <summary>
        /// Prompts user to filter by AutoDesign members.
        /// </summary>
        /// <returns>True for AutoDesign, False for Non-AutoDesign, Null for All.</returns>
        public static bool? AskAutoDesign()
        {
            string? readIn = AskUser("Query Autodesign members only [Y/N, Enter for All]:", new List<string> { "Y", "N" });

            if (readIn == "Y")
            {
                return true;
            }
            else if (readIn == "N")
            {
                return false;
            }
            else if (string.IsNullOrEmpty(readIn))
            {
                return null;
            }
            else
            {
                FancyWriteLine("Input ", $"{readIn}", " not recognized. All members will be returned", TextColor.Command);
                return null;
            }
        }


        /// <summary>
        /// Prompts user for the number of points of interest along the span.
        /// </summary>
        /// <param name="maxPoints">Maximum allowed points (deprecated/informative).</param>
        /// <returns>The number of points (0 to ignore).</returns>
        public static int AskPoints(int maxPoints)
        {
            FancyWriteLine("Select the number of points along beam span at which forces and displacements will be calculated.", TextColor.Text);
            FancyWriteLine("Enter ", "1", " to return maxima only.", TextColor.Command);
            if (maxPoints >= 2)
            {
                FancyWriteLine("Enter ", "2", $" or greater (max. {maxPoints}) to subdivide spans.", TextColor.Command);
            }
            FancyWriteLine("Enter ", "0", " to ignore force and displacement data.", TextColor.Command);

            int numPoints = -1;

            do
            {
                string? readIn = AskUser($"Enter an integer between 0 and {maxPoints}: ");
                if (int.TryParse(readIn, out _))
                {
                    numPoints = int.Parse(readIn);
                }
                if (numPoints < 0 | numPoints > maxPoints)
                {
                    FancyWriteLine("Illegal input: ", $"{readIn}", "", TextColor.Command);
                }
            } while (numPoints < 0 | numPoints > maxPoints);

            return numPoints;
        }

        /// <summary>
        /// Prompts user whether to use Reduced forces.
        /// </summary>
        /// <returns>True if reduced forces requested, false otherwise.</returns>
        public static bool AskReduced()
        {
            bool? reduced = null;
            do
            {
                string? readIn = AskUser("Enter N to query nonreduced forces, or hit Enter to get reduced forces (where applicable):", new List<string> { "N" });
                if (string.IsNullOrEmpty(readIn))
                {
                    reduced = true;
                }
                else if (readIn == "N")
                {
                    reduced = false;
                }
                else
                {
                    FancyWriteLine("Input ", $"{readIn}", " not recognized.", TextColor.Command);
                }
            } while (reduced == null);
            return (bool)reduced;
        }

        /// <summary>
        /// Logs a summary of the unpacking process for loadcases, combinations, and envelopes.
        /// </summary>
        protected void LogLoadingSummary()
        {
            FancyWriteLine("Loading Summary:", TextColor.Title);
            Console.WriteLine("Unpacking loading data...");
            Console.WriteLine($"{AllLoadcases!.Count} loadcases found, {SolvedCases!.Count} solved.");
            Console.WriteLine($"{AllCombinations!.Count} load combinations found, {SolvedCombinations!.Count} solved.");
            Console.WriteLine($"{AllEnvelopes!.Count} load envelopes found, {SolvedEnvelopes!.Count} solved.\n");
        }

        /// <summary>
        /// Prompts user for standard member filters (Gravity Only, AutoDesign) and returns the filtered list of members.
        /// </summary>
        /// <param name="askGravity">Whether to ask for Gravity Only filtering.</param>
        /// <param name="askAutoDesign">Whether to ask for AutoDesign filtering.</param>
        /// <returns>A filtered list of members.</returns>
        protected List<IMember> AskAndFilterMembers(bool askGravity = true, bool askAutoDesign = true)
        {
            FancyWriteLine("\nMember summary:", TextColor.Title);
            Console.WriteLine("Unpacking member data...");

            List<IMember> filteredMembers = new();

            bool? GravityOnlyState = null;
            bool? AutoDesignState = null;

            if (askGravity) GravityOnlyState = AskGravityOnly();
            if (askAutoDesign) AutoDesignState = AskAutoDesign();

            if (GravityOnlyState == null & AutoDesignState == null)
            {
                filteredMembers = AllMembers!.Where(c => RequestedMemberType.Contains(GetProperty(c.Data.Value.Construction))).ToList();
            }
            else if (AutoDesignState == null)
            {
                filteredMembers = AllMembers!.Where(c => RequestedMemberType.Contains(GetProperty(c.Data.Value.Construction)) & GetProperty(c.Data.Value.GravityOnly) == GravityOnlyState).ToList();
            }
            else if (GravityOnlyState == null)
            {
                filteredMembers = AllMembers!.Where(c => RequestedMemberType.Contains(GetProperty(c.Data.Value.Construction)) & GetProperty(c.Data.Value.AutoDesign) == AutoDesignState).ToList();
            }
            else
            {
                filteredMembers = AllMembers!.Where(c => RequestedMemberType.Contains(GetProperty(c.Data.Value.Construction)) & GetProperty(c.Data.Value.GravityOnly) == GravityOnlyState & GetProperty(c.Data.Value.AutoDesign) == AutoDesignState).ToList();
            }

            return filteredMembers;
        }

        /// <summary>
        /// Gets the name of the level at a specific Z-coordinate, or "Unknown" if no match is found.
        /// </summary>
        /// <param name="z">The Z-coordinate to check.</param>
        /// <param name="point">The construction point (optional context).</param>
        /// <param name="levels">The list of levels to search.</param>
        /// <returns>The level name or "Unknown".</returns>
        protected static string GetLevelName(double z, IConstructionPoint point, List<IHorizontalConstructionPlane> levels)
        {
            try
            {
                if (point.PlaneInfo.Value.Type == TSD.API.Remoting.Common.EntityType.HorizontalConstructionPlane)
                {
                    int planeId = point.PlaneInfo.Value.Index;
                    var exactMatch = levels.FirstOrDefault(l => l.Index == planeId);
                    if (exactMatch != null)
                        return exactMatch.Name;

                    var nearest = levels.MinBy(l => Math.Abs(z - l.Level.Value));
                    if (nearest != null && Math.Abs(z - nearest.Level.Value) < 1000)
                        return $"~{nearest.Name}";
                }

                var nearestZ = levels.MinBy(l => Math.Abs(z - l.Level.Value));
                if (nearestZ != null && Math.Abs(z - nearestZ.Level.Value) < 1000)
                    return $"~{nearestZ.Name}";

                return "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Gets a descriptive string for the fixity of a node based on its releases.
        /// </summary>
        /// <param name="releases">The span releases.</param>
        /// <returns>A string description (e.g., "Pinned", "Fixed", "Moment", "Free").</returns>
        protected static string GetNodeFixityDescription(ISpanReleases releases)
        {
            if (releases == null) return "Fixed";
            var fixityParts = new List<string>();
            try
            {
                var hasTransX = HasTranslationalRelease(releases, "X");
                var hasTransY = HasTranslationalRelease(releases, "Y");
                var hasTransZ = HasTranslationalRelease(releases, "Z");
                var hasRotX = HasRotationalRelease(releases, "X");
                var hasRotY = HasRotationalRelease(releases, "Y");
                var hasRotZ = HasRotationalRelease(releases, "Z");
                if (!hasTransX) fixityParts.Add("Tx");
                if (!hasTransY) fixityParts.Add("Ty");
                if (!hasTransZ) fixityParts.Add("Tz");
                if (!hasRotX) fixityParts.Add("Rx");
                if (!hasRotY) fixityParts.Add("Ry");
                if (!hasRotZ) fixityParts.Add("Rz");
                if (fixityParts.Count == 6)
                    return "Fixed";
                else if (fixityParts.Count == 0)
                    return "Pinned";
                else
                    return string.Join("-", fixityParts);
            }
            catch
            {
                return "Unknown";
            }
        }

        private static bool HasTranslationalRelease(ISpanReleases releases, string direction)
        {
            try
            {
                var type = releases.GetType();
                var property = type.GetProperty($"Translation{direction}") ??
                              type.GetProperty($"Translation{direction}Released") ??
                              type.GetProperty($"IsTranslation{direction}Released");
                if (property != null)
                {
                    var value = property.GetValue(releases);
                    if (value is bool boolValue)
                        return boolValue;
                    else if (value != null)
                    {
                        var valProp = value.GetType().GetProperty("Value");
                        if (valProp != null)
                        {
                            var innerVal = valProp.GetValue(value);
                            if (innerVal is bool innerBool) return innerBool;
                        }
                    }
                }
            }
            catch { }
            return false; // Default to not released (fixed)
        }

        private static bool HasRotationalRelease(ISpanReleases releases, string direction)
        {
            try
            {
                var type = releases.GetType();
                var property = type.GetProperty($"Rotation{direction}") ??
                              type.GetProperty($"Rotation{direction}Released") ??
                              type.GetProperty($"IsRotation{direction}Released");
                if (property != null)
                {
                    var value = property.GetValue(releases);
                    if (value is bool boolValue)
                        return boolValue;
                    else if (value != null)
                    {
                        var valProp = value.GetType().GetProperty("Value");
                        if (valProp != null)
                        {
                            var innerVal = valProp.GetValue(value);
                            if (innerVal is bool innerBool) return innerBool;
                        }
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
