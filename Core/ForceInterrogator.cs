using System.Diagnostics;
using System.Text;

using TSD.API.Remoting;
using TSD.API.Remoting.Document;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Solver;
using TSD.API.Remoting.Structure;
using TSD.API.Remoting.Common;
using AnalysisType = TSD.API.Remoting.Solver.AnalysisType;

namespace TeklaResultsInterrogator.Core
{
    public class ForceInterrogator : BaseInterrogator
    {
        protected AnalysisType AnalysisType = AnalysisType.FirstOrderLinear;
        protected List<MemberConstruction> RequestedMemberType = new List<MemberConstruction>();
        protected TSD.API.Remoting.Solver.IModel? SolverModel { get; set; }
        protected List<ILoadcase>? AllLoadcases { get; set; }
        protected List<ILoadcase>? SolvedCases { get; set; }
        protected List<ICombination>? AllCombinations { get; set; }
        protected List<ICombination>? SolvedCombinations { get; set; }
        protected List<IEnvelope>? AllEnvelopes { get; set; }
        protected List<IEnvelope>? SolvedEnvelopes { get; set; }
        protected List<IMember>? AllMembers { get; set; }

        public ForceInterrogator() { }

        public override void Initialize()  // to get solver model and other stuff here
        {
            // Set up
            Stopwatch stopwatch = Stopwatch.StartNew();
            InitializeBase();

            // Get 1st Order Linear SolverModel
            Console.WriteLine("Searching for analysis solver model...");
            if (Model == null)
            {
                FancyWriteLine("No model found!", TextColor.Error);
                Flag = true;
                return;
            }
            IEnumerable<TSD.API.Remoting.Solver.IModel> solverModels = Model.GetSolverModelsAsync(new[] { AnalysisType }).Result;
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
            IAnalysisResults? solverResults = SolverModel.GetResultsAsync().Result;
            if (solverResults == null)
            {
                FancyWriteLine("No results found for requested analysis type!", TextColor.Error);
                Flag = true;
                return;
            }
            IAnalysis3DResults? analysis3Dresults = solverResults.GetAnalysis3DAsync().Result;
            if (analysis3Dresults == null)
            {
                FancyWriteLine("No 3-D analysis results found for requested analysis type!", TextColor.Error);
                Flag = true;
                return;
            }
            var solvedLoadingGuids = analysis3Dresults.GetSolvedLoadingIdsAsync().Result;
            if (!solvedLoadingGuids.Any())
            {
                FancyWriteLine("No solved loading GUIDs found!", TextColor.Error);
                Flag = true;
                return;
            }

            // Get members
            Console.WriteLine("Searching for members...");
            IEnumerable<IMember>? allMembers = Model.GetMembersAsync(null).Result;
            if (!allMembers.Any() || allMembers == null)
            {
                FancyWriteLine("No members found!", TextColor.Error);
                Flag = true;
                return;
            }
            AllMembers = allMembers.ToList();

            // Get solved loadcases
            Console.WriteLine("Searching for solved loadcases...");
            IEnumerable<ILoadcase> loadingCases = Model.GetLoadcasesAsync(null).Result;
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
            IEnumerable<ICombination> loadingCombinations = Model.GetCombinationsAsync(null).Result;
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
            IEnumerable<IEnvelope> loadingEnvelopes = Model.GetEnvelopesAsync(null).Result;
            if (!loadingEnvelopes.Any())
            {
                FancyWriteLine("No load envelopes found!", TextColor.Warning);
                Flag = false;  // Do not raise flag for no envelopes
            }
            AllEnvelopes = loadingEnvelopes.ToList();
            List<IEnvelope> solvedEnvelopes = new List<IEnvelope>();
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

        public List<ILoadingCase> AskLoading(List<ILoadcase> solvedCases, List<ICombination> solvedCombinations, List<IEnvelope> solvedEnvelopes)
        {
            Dictionary<string, List<ILoadingCase>> loadingOptions = new Dictionary<string, List<ILoadingCase>>();

            if (solvedCases.Count > 0)
            {
                loadingOptions.Add("Loadcases", solvedCases.Cast<ILoadingCase>().ToList());
            }
            if (solvedCombinations.Count > 0)
            {
                loadingOptions.Add("Combinations", solvedCombinations.Cast<ILoadingCase>().ToList());
            }
            if (solvedEnvelopes.Count > 0)
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
                string? readIn = AskUser("Choose an available loading condition: ");
                if (readIn != null && loadingOptions.Keys.Contains(readIn))
                {
                    loadingCases = loadingOptions[readIn];
                }
                else
                {
                    FancyWriteLine("Loading Condition ", $"{readIn}", " not found.", TextColor.Command);
                }
            } while (loadingCases == null);

            return loadingCases;
        }

        public int AskPoints(int maxPoints)
        {
            FancyWriteLine("Select the number of points along beam span at which forces and displacements will be calculated.", TextColor.Text);
            FancyWriteLine("Enter ", "1", " to return maxima only.", TextColor.Command);
            FancyWriteLine("Enter ", "2", $" or greater (max. {maxPoints}) to subdivide spans.", TextColor.Command);
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

        public bool AskReduced()
        {
            bool? reduced = null;
            do
            {
                string? readIn = AskUser("Enter Y to query reduced forces, or N to query nonreduced forces: ");
                if (readIn == "Y")
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
    }
}
