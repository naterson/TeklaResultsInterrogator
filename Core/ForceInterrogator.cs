using System.Diagnostics;
using System.Text;

using TSD.API.Remoting;
using TSD.API.Remoting.Document;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Solver;
using TSD.API.Remoting.Structure;
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

        public override async Task InitializeAsync()  // to get solver model and other stuff here
        {
            // Set up
            Stopwatch stopwatch = Stopwatch.StartNew();
            await InitializeBaseAsync();

            // Get 1st Order Linear SolverModel
            Console.WriteLine("Searching for analysis solver model...");
            if (Model == null)
            {
                FancyWriteLine("No model found!", TextColor.Error);
                Flag = true;
                return;
            }
            IEnumerable<TSD.API.Remoting.Solver.IModel> solverModels = await Model.GetSolverModelsAsync(new[] { AnalysisType });
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
            if (!allMembers.Any() || allMembers == null)
            {
                FancyWriteLine("No members found!", TextColor.Error);
                Flag = true;
                return;
            }
            AllMembers = allMembers.ToList();

            // Get solved loadcases
            Console.WriteLine("Searching for solved loadcases...");
            IEnumerable<ILoadcase> loadingCases = await Model.GetLoadcasesAsync(null);
            if (!loadingCases.Any() || loadingCases == null)
            {
                FancyWriteLine("No loadcases found!", TextColor.Error);
                Flag = true;
                return;
            }
            AllLoadcases = loadingCases.ToList();
            List<ILoadcase> solvedCases = loadingCases.Where(c => solvedLoadingGuids.Contains(c.Id)).ToList();
            if (!solvedCases.Any() || solvedCases == null)
            {
                FancyWriteLine("No solved loadcases found!", TextColor.Error);
                Flag = true;
                return;
            }
            SolvedCases = solvedCases;

            // Get solved load combos
            Console.WriteLine("Searching for solved load combinations...");
            IEnumerable<ICombination> loadingCombinations = await Model.GetCombinationsAsync(null);
            if (!loadingCombinations.Any() || loadingCombinations == null)
            {
                FancyWriteLine("No load combinations found!", TextColor.Error);
                Flag = true;
                return;
            }
            AllCombinations = loadingCombinations.ToList();
            List<ICombination> solvedCombinations = loadingCombinations.Where(c => solvedLoadingGuids.Contains(c.Id)).ToList();
            if (!solvedCombinations.Any() || solvedCombinations == null)
            {
                FancyWriteLine("No solved load combinations found!", TextColor.Error);
                Flag = true;
                return;
            }
            SolvedCombinations = solvedCombinations;

            // Get solved load envelopes
            Console.WriteLine("Searching for solved load envelopes...");
            IEnumerable<IEnvelope> loadingEnvelopes = await Model.GetEnvelopesAsync(null);
            if (!loadingEnvelopes.Any() || loadingEnvelopes == null)
            {
                FancyWriteLine("No load envelopes found!", TextColor.Warning);
                Flag = false;  // Do not raise flag for no envelopes
            }
            AllEnvelopes = loadingEnvelopes.ToList();
            List<IEnvelope> solvedEnvelopes = loadingEnvelopes.Where(c => solvedLoadingGuids.Contains(c.Id)).ToList();
            if (!solvedEnvelopes.Any() || solvedEnvelopes == null)
            {
                FancyWriteLine("No solved load envelopes found!", TextColor.Warning);
                Flag = false;  // Do not raise flag for no solved envelopes
            }
            SolvedEnvelopes = solvedEnvelopes;

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
                    FancyWriteLine("Input ", $"{readIn}", " not recodnized.", TextColor.Command);
                }
            } while (reduced == null);
            return (bool)reduced;
        }
    }
}
