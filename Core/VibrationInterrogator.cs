using System.Diagnostics;
using System.Text;
using TeklaResultsInterrogator.Utils;
using static TeklaResultsInterrogator.Utils.Utils;

using TSD.API.Remoting;
using TSD.API.Remoting.Document;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Solver;

using AnalysisType = TSD.API.Remoting.Solver.AnalysisType;

namespace TeklaResultsInterrogator.Core
{
    public class VibrationInterrogator : BaseInterrogator
    {
        private AnalysisType AnalysisType = AnalysisType.FirstOrderVibration;
        protected TSD.API.Remoting.Solver.IModel? SolverModel { get; set; }
        protected IEnumerable<INode>? Nodes { get; set; }
        protected ILoadingVibration? LoadingVibration { get; set; }

        public VibrationInterrogator() { }

        public override async Task InitializeAsync()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            await InitializeBaseAsync();

            // Get Vibration SolverModel
            Console.WriteLine("Searching for vibration solver model...");
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
            SolverModel = solverModels.FirstOrDefault();
            if (SolverModel == null)
            {
                FancyWriteLine("No vibration solver model found!", TextColor.Error);
                Flag = true;
                return;
            }

            // Get mesh nodes from solver model
            Console.WriteLine("Searching for vibration solver model geometry...");
            Nodes = (await SolverModel.GetNodesAsync(null)).OrderBy(n => n.Index);
            if (!Nodes.Any() || Nodes == null)
            {
                FancyWriteLine("No solver model geometry could be found!", TextColor.Error);
                Flag = true;
                return;
            }

            // Get Vibration Results
            Console.WriteLine("Searching for solved vibration results...");
            IAnalysisResults? solverResults = await SolverModel.GetResultsAsync();
            if (solverResults == null)
            {
                FancyWriteLine("No solver results found!", TextColor.Error);
                Flag = true;
                return;
            }
            IVibrationResults? vibrationResults = await solverResults.GetVibrationAsync();
            if (vibrationResults == null)
            {
                FancyWriteLine("No vibration results found!", TextColor.Error);
                Flag = true;
                return;
            }
            IEnumerable<Guid> solvedLoadingIDs = await vibrationResults.GetSolvedLoadingIdsAsync();
            if (!solvedLoadingIDs.Any())
            {
                FancyWriteLine("No solved vibration loading found!", TextColor.Error);
                Flag = true;
                return;
            }
            Guid solvedLoadingID = solvedLoadingIDs.FirstOrDefault();
            LoadingVibration = await vibrationResults.GetLoadingVibrationAsync(solvedLoadingID);

            // Finish up
            stopwatch.Stop();
            InitializationTime = stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine($"Initialization completed in {Math.Round(InitializationTime, 3)} seconds.\n");

            Check();

            return;
        }
    }
}
