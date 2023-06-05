using System.Diagnostics;
using System.Text;

using TSD.API.Remoting;
using TSD.API.Remoting.Document;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Solver;

using AnalysisType = TSD.API.Remoting.Solver.AnalysisType;

namespace TeklaResultsInterrogator.Core
{
    public class ForceInterrogator : BaseInterrogator
    {
        public AnalysisType AnalysisType = AnalysisType.FirstOrderLinear;
        protected TSD.API.Remoting.Solver.IModel? SolverModel { get; set; }
        protected List<ILoadcase>? SolvedCases { get; set; }
        protected List<ICombination>? SolvedCombinations { get; set; }

        public ForceInterrogator() { }

        public override async Task InitializeAsync()  // to get solver model and other stuff here
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            await InitializeBaseAsync();

            // Get 1st Order Linerar SolverModel
            Console.WriteLine("Searching for analysis solver model...");
            if (Model == null)
            {
                FancyWriteLine("No model found!", TextColor.Error);
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
            if (SolverModel == null)
            {
                FancyWriteLine("No solver model found!", TextColor.Error);
                Flag = true;
                return;
            }
            SolverModel = SolverModel;

            // Get Analysis Results
            Console.WriteLine("Searching for analysis results...");
            IAnalysisResults? solverResults = await SolverModel.GetResultsAsync();
            if (solverResults == null)
            {
                FancyWriteLine("No results found for requested analysis type!", TextColor.Error);
                return;
            }
            IAnalysis3DResults? analysis3Dresults = await solverResults.GetAnalysis3DAsync();
            if (analysis3Dresults == null)
            {
                FancyWriteLine("No 3-D analysis results found for requested analysis type!", TextColor.Error);
                return;
            }
            var solvedLoadingGuids = await analysis3Dresults.GetSolvedLoadingIdsAsync();
            if (!solvedLoadingGuids.Any())
            {
                FancyWriteLine("No solved loading GUIDs found!", TextColor.Error);
                return;
            }

            // Get solved loadcases
            Console.WriteLine("Searching for solved loadcases...");
            IEnumerable<ILoadcase> loadingCases = await Model.GetLoadcasesAsync(null);
            if (!loadingCases.Any() || loadingCases == null)
            {
                FancyWriteLine("No loadcases found!", TextColor.Error);
                return;
            }
            List<ILoadcase> solvedCases = loadingCases.Where(c => solvedLoadingGuids.Contains(c.Id)).ToList();
            if (!solvedCases.Any() || solvedCases == null)
            {
                FancyWriteLine("No solved loadcases found!", TextColor.Error);
                return;
            }
            SolvedCases = solvedCases;

            // Get solved load combos
            Console.WriteLine("Searching for solved load combinations...");
            IEnumerable<ICombination> loadingCombinations = await Model.GetCombinationsAsync(null);
            if (!loadingCombinations.Any() || loadingCombinations == null)
            {
                FancyWriteLine("No load combinations found!", TextColor.Error);
                return;
            }
            List<ICombination> solvedCombinations = loadingCombinations.Where(c => solvedLoadingGuids.Contains(c.Id)).ToList();
            if (!solvedCombinations.Any() || solvedCombinations == null)
            {
                FancyWriteLine("No solved load combinations found!", TextColor.Error);
                return;
            }
            SolvedCombinations = solvedCombinations;

            // Write to Console
            stopwatch.Stop();
            InitializationTime = stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine($"Initialization completed in {Math.Round(InitializationTime, 3)} seconds.\n");

            Check();

            return;
        }
    }
}
