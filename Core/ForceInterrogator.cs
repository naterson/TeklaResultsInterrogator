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

        public ForceInterrogator() { }

        public override async Task InitializeAsync()  // to get solver model and other stuff here
        {
            await InitializeBaseAsync();
        }
    }
}
