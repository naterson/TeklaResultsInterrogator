using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TeklaResultsInterrogator.Core;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Structure;
using TSD.API.Remoting.Solver;
using System.Collections.Immutable;
using System.Formats.Asn1;
using System.ComponentModel;
using Microsoft.VisualBasic;
using System.Runtime.Versioning;
using TeklaResultsInterrogator.Utils;
using static TeklaResultsInterrogator.Utils.Utils;

namespace TeklaResultsInterrogator.Commands
{


public class BaseReactions : ForceInterrogator  // Should inherit a parent Interrogator class
    {
        // Should not declare any public properties here

        // Leave class constructor parameterless
        public BaseReactions()
        {
            HasOutput = true;  // Only explicitly declare properties in constructor body

        }

        // Main routines here to be called after initialization
        public override async Task ExecuteAsync()
        {
            // Initialize parents
            await InitializeAsync();

            IEnumerable<INode> Nodes = await SolverModel.GetNodesAsync(null);
            List<INode> allSupports = Nodes.Where(x => x.HasSupport(SupportType.Structure3D)).ToList();
            List<ILoadingCase> loadingCases = AskLoading(SolvedCases, SolvedCombinations, SolvedEnvelopes);

            List<int> mysupportIDs = new List<int>();

            foreach (INode support in allSupports) {
                mysupportIDs.Add(support.Index);
            }

            IEnumerable < IConstructionPoint > constructionPoints = await Model.GetConstructionPointsAsync(null);
            List < IConstructionPoint > constructionPointsList = constructionPoints.Where(pt=>mysupportIDs.Contains(pt.SolverNodeIndex.Value)).ToList();

            //foreach (IConstructionPoint constructionPoint in constructionPointsList) {

                List<object[]> reactions = new List<object[]>();

                foreach (ILoadcase loadcase in loadingCases)
                {
                    foreach (INode support in allSupports)
                    {
                        IForce3DGlobal reaction = await support.GetSupportReactionAsync(loadcase.Id, false);
                        IConstructionPoint my_point = constructionPoints.Where(pt => pt.SolverNodeIndex.Value.Equals(support.Index)).First();
                        object[] support_reactions = { support.Index, my_point.Name,
                                                       mm2ft(support.Coordinates.X), mm2ft(support.Coordinates.Y), mm2ft(support.Coordinates.Z),
                                                       loadcase.Name, ToK(reaction.Fx), ToK(reaction.Fy), ToK(reaction.Fz),
                                                       ToKFt(reaction.Mx), ToKFt(reaction.My), ToKFt(reaction.Mz) };
                        reactions.Add(support_reactions);
                    }
                }
            //}
                        
            var header = new List<string>() {"SolverNodeId", "Support Name", "x","y","z","Loading", "Fx", "Fy", "Fz", "Mx", "My", "Mz"};

            string file = SaveDirectory  + @"\Reactions_" + FileName + ".csv";

            WriteToCsv(header, reactions, file);

            // Check for null properties
            if (Flag)
            {
                return;
            }

            // Data setup and diagnostics initialization; declare locals here
            Stopwatch stopwatch = Stopwatch.StartNew();

            // Call all command routines and subroutines here within this method

            // Finish up
            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;

            Check();

            return;
        }


    }
}
