using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Formats.Asn1;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using TeklaResultsInterrogator.Core;
using TeklaResultsInterrogator.Utils;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Solver;
using TSD.API.Remoting.Structure;
using static TeklaResultsInterrogator.Utils.ConsoleUtils;

namespace TeklaResultsInterrogator.Commands
{

    /// <summary>
    /// Interrogates Support Reactions for solved loading cases.
    /// </summary>
    public class Reactions : SolverInterrogator
    {

        /// <inheritdoc/>
        public override bool ShowInMenu() { return true; }

        /// <summary>Initializes a new instance of the <see cref="Reactions"/> class.</summary>
        public Reactions()
        {
            HasOutput = true;
        }

        // Main routines here to be called after initialization
        /// <summary>
        /// Executes the Reactions interrogation, mapping supports to construction points and writing reactions to CSV.
        /// </summary>
        public override async Task ExecuteAsync()
        {
            // Initialize parents
            await InitializeAsync();

            Stopwatch stopwatch = Stopwatch.StartNew();
            IEnumerable<INode> Nodes = await SolverModel!.GetNodesAsync(null);
            List<INode> allSupports = Nodes.Where(x => x.HasSupport(SupportType.Structure3D)).ToList();

            stopwatch.Stop();
            List<ILoadingCase> loadingCases = AskLoading(SolvedCases, SolvedCombinations, SolvedEnvelopes);
            stopwatch.Start();

            List<int> mysupportIDs = new List<int>();

            foreach (INode support in allSupports)
            {
                mysupportIDs.Add(support.Index);
            }

            IEnumerable<IConstructionPoint> constructionPoints = await Model!.GetConstructionPointsAsync(null);
            List<IConstructionPoint> constructionPointsList = constructionPoints.Where(pt => pt.SolverNodeIndex != null && mysupportIDs.Contains(pt.SolverNodeIndex.Value)).ToList();

            List<object[]> reactions = new List<object[]>();

            foreach (ILoadingCase loadcase in loadingCases)
            {
                foreach (INode support in allSupports)
                {
                    IForce3DGlobal reaction = await support.GetSupportReactionAsync(loadcase.Id, false);
                    IConstructionPoint my_point = constructionPoints.Where(pt => pt.SolverNodeIndex != null && pt.SolverNodeIndex.Value.Equals(support.Index)).First();
                    object[] support_reactions = { support.Index, my_point.Name,
                                                       MmToFt(support.Coordinates.X), MmToFt(support.Coordinates.Y), MmToFt(support.Coordinates.Z),
                                                       loadcase.Name, ToK(reaction.Fx), ToK(reaction.Fy), ToK(reaction.Fz),
                                                       ToKFt(reaction.Mx), ToKFt(reaction.My), ToKFt(reaction.Mz) };
                    reactions.Add(support_reactions);
                }
            }

            var header = new List<string>() { "SolverNodeId", "Support Name", "x", "y", "z", "Loading", "Fx", "Fy", "Fz", "Mx", "My", "Mz" };

            string file = SaveDirectory + @"\Reactions_" + OutputFileName + ".csv";

            WriteToCsv(header, reactions, file);



            // Finish up
            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;

            Check();

            return;
        }


    }
}
