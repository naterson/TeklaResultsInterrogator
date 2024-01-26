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
        private AnalysisType AnalysisType = AnalysisType.FirstOrderVibration;
        protected TSD.API.Remoting.Solver.IModel? SolverModel {  get; set; }
        protected IEnumerable<INode>? Nodes { get; set; }
        protected ILoadingVibration? LoadingVibration { get; set; }

        public VibrationInterrogator() { }

        public override void Initialize()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            InitializeBase();

            // Get Vibration SolverModel
            Console.WriteLine("Searching for vibration solver model...");
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
            SolverModel = solverModels.FirstOrDefault();
            if (SolverModel == null)
            {
                FancyWriteLine("No vibration solver model found!", TextColor.Error);
                Flag = true;
                return;
            }

            // Get mesh nodes from solver model
            Console.WriteLine("Searching for vibration solver model geometry...");
            Nodes = (SolverModel.GetNodesAsync(null).Result).OrderBy(n => n.Index);
            if (!Nodes.Any() || Nodes == null)
            {
                FancyWriteLine("No solver model geometry could be found!", TextColor.Error);
                Flag = true;
                return;
            }

            // Get Vibration Results
            Console.WriteLine("Searching for solved vibration results...");
            IAnalysisResults? solverResults = SolverModel.GetResultsAsync().Result;
            if (solverResults == null)
            {
                FancyWriteLine("No solver results found!", TextColor.Error);
                Flag = true;
                return;
            }
            IVibrationResults? vibrationResults = solverResults.GetVibrationAsync().Result;
            
            if (vibrationResults == null)
            {
                FancyWriteLine("No vibration results found!", TextColor.Error);
                Flag = true;
                return;
            }
            IEnumerable<Guid> solvedLoadingIDs = vibrationResults.GetSolvedLoadingIdsAsync().Result;
            if (!solvedLoadingIDs.Any())
            {
                FancyWriteLine("No solved vibration loading found!", TextColor.Error);
                Flag = true;
                return;
            }
            Guid solvedLoadingID = solvedLoadingIDs.FirstOrDefault();
            LoadingVibration = vibrationResults.GetLoadingVibrationAsync(solvedLoadingID).Result;

            // Finish up
            stopwatch.Stop();
            InitializationTime = stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine($"Initialization completed in {Math.Round(InitializationTime, 3)} seconds.\n");

            Check();

            return;
        }
    }
}
