using System.Diagnostics;
using System.Text;

using TSD.API.Remoting;
using TSD.API.Remoting.Document;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Solver;

using AnalysisType = TSD.API.Remoting.Solver.AnalysisType;

namespace TeklaResultsInterrogator.Core
{
    public class VibrationInterrogator : BaseInterrogator
    {
        public AnalysisType AnalysisType = AnalysisType.FirstOrderVibration;
        public TSD.API.Remoting.Solver.IModel? SolverModel {  get; set; }
        public IEnumerable<INode>? Nodes { get; set; }
        public ILoadingVibration? LoadingVibration { get; set; }

        public VibrationInterrogator() { }

        public override async Task InitializeAsync()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            await InitializeBaseAsync();

            // Get Vibration SolverModel
            Console.WriteLine("Searching for vibration solver model...");
            if (Model == null)
            {
                Console.WriteLine("No model found!");
                return;
            }
            IEnumerable<TSD.API.Remoting.Solver.IModel> solverModels = await Model.GetSolverModelsAsync(new[] { AnalysisType });
            if (!solverModels.Any())
            {
                Console.WriteLine("No solver models found!");
                return;
            }
            SolverModel = solverModels.FirstOrDefault();
            if (SolverModel == null)
            {
                Console.WriteLine("No vibration solver model found!");
                return;
            }

            // Get mesh nodes from solver model
            Console.WriteLine("Searching for vibration solver model geometry...");
            Nodes = (await SolverModel.GetNodesAsync(null)).OrderBy(n => n.Index);
            if (!Nodes.Any() || Nodes == null)
            {
                Console.WriteLine("No solver model geometry could be found!");
                return;
            }

            // Get Vibration Results
            Console.WriteLine("Searching for solved vibration results...");
            IAnalysisResults? solverResults = await SolverModel.GetResultsAsync();
            if (solverResults == null)
            {
                Console.WriteLine("No solver results found!");
                return;
            }
            IVibrationResults? vibrationResults = await solverResults.GetVibrationAsync();
            if (vibrationResults == null)
            {
                Console.WriteLine("No vibration results found!");
                return;
            }
            IEnumerable<Guid> solvedLoadingIDs = await vibrationResults.GetSolvedLoadingIdsAsync();
            if (!solvedLoadingIDs.Any())
            {
                Console.WriteLine("No solved vibration loading found!");
                return;
            }
            Guid solvedLoadingID = solvedLoadingIDs.FirstOrDefault();
            LoadingVibration = await vibrationResults.GetLoadingVibrationAsync(solvedLoadingID);

            stopwatch.Stop();
            InitializationTime = stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine($"Initialization completed in {Math.Round(InitializationTime, 3)} seconds.\n");

            Check();

            return;
        }
    }
}
