using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using TeklaResultsInterrogator.Core;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Structure;
using TSD.API.Remoting.Solver;
using System.Diagnostics.Metrics;
using TSD.API.Remoting.Geometry;

namespace TeklaResultsInterrogator.Commands
{
    public class BRForces : ForceInterrogator  // Should inherit a parent Interrogator class
    {
        // Should not declare any public properties here

        // Leave class constructor parameterless
        public BRForces()
        {
            HasOutput = false;  // Only explicitly declare properties in constructor body
            AnalysisType = AnalysisType.SecondOrderLinear;
            RequestedMemberType = new List<MemberConstruction>() { MemberConstruction.SteelBeam, MemberConstruction.SteelColumn };
        }

        // Main routines here to be called after initialization
        public override async Task ExecuteAsync()
        {
            // Initialize parents
            await InitializeAsync();

            // Check for null properties
            if (Flag)
            {
                return;
            }

            // Data setup and diagnostics initialization
            Stopwatch stopwatch = Stopwatch.StartNew();
            int bufferSize = 65536 * 2;

            // Unpacking loading data
            FancyWriteLine("Loading Summary:", TextColor.Title);
            Console.WriteLine("Unpacking loading data...");
            Console.WriteLine($"{AllLoadcases.Count} loadcases found, {SolvedCases.Count} solved.");
            Console.WriteLine($"{AllCombinations.Count} load combinations found, {SolvedCombinations.Count} solved.");
            Console.WriteLine($"{AllEnvelopes.Count} load envelopes found, {SolvedEnvelopes.Count} solved.\n");

            stopwatch.Stop();
            List<ILoadingCase> loadingCases = AskLoading(SolvedCases, SolvedCombinations, SolvedEnvelopes);
            bool reduced = AskReduced();
            stopwatch.Start();

            // Unpacking member data
            FancyWriteLine("\nMember summary:", TextColor.Title);
            Console.WriteLine("Unpacking member data...");

            // Hardcoding filter: TODO change this
            List<string> filterIDs = new List<string>();
            using (StreamReader reader = new StreamReader(@"C:\Users\nnickerson\OneDrive - LeMessurier Consultants\Desktop\Fenway\Filter-br3br5.csv"))
            {
                while (!reader.EndOfStream)
                {
                    string? line = reader.ReadLine();
                    if (line != null)
                    {
                        filterIDs.Add(line);
                    }
                }
            }

            List<IMember> steelMembers = AllMembers.Where(c => RequestedMemberType.Contains(c.Data.Value.Construction.Value)).ToList();
            steelMembers = steelMembers.Where(c => filterIDs.Contains(c.Name)).ToList();
            List<IMember> steelBeams = steelMembers.Where(c => c.Data.Value.Construction.Value == MemberConstruction.SteelBeam).ToList();
            List<IMember> steelColumns = steelMembers.Where(c => c.Data.Value.Construction.Value == MemberConstruction.SteelColumn).ToList();

            List<IHorizontalConstructionPlane> levels = (await Model.GetLevelsAsync()).ToList();

            Dictionary<string, double> frameCoords  = new Dictionary<string, double>();
            Dictionary<string, double> gridCoords = new Dictionary<string, double>();
            Dictionary<string, double> levelCoords = new Dictionary<string, double>();

            foreach (IMember col in steelColumns)
            {
                string name = col.Name;
                int idx = name.LastIndexOf('-');
                string loc = name.Substring(idx + 1);
                idx = loc.LastIndexOf('/');
                string grid = loc.Substring(0, idx);
                string frame = "BR-" + loc.Substring(idx + 1);
                int constructionPointIndex = col.MemberNodes.Value.First().Value.ConstructionPointIndex.Value;
                IEnumerable<IConstructionPoint> constructionPoints = await Model.GetConstructionPointsAsync(new List<int>() { constructionPointIndex });
                Point3D p = constructionPoints.First().Coordinates.Value;
                double x = p.X;
                double y = p.Y;
                if (!gridCoords.ContainsKey(grid))
                {
                    gridCoords.Add(grid, x);
                }
                if (!frameCoords.ContainsKey(frame))
                {
                    frameCoords.Add(frame, y);
                }
            }

            Console.WriteLine($"{AllMembers.Count} structural members found in model.");
            Console.WriteLine($"{steelMembers.Count} steel members found and filtered.");
            Console.WriteLine($"{steelBeams.Count} steel beams in search filter.");
            Console.WriteLine($"{steelColumns.Count} steel columns in search filter.\n");

            foreach (string grid in gridCoords.Keys)
            {
                Console.WriteLine($"Grid {grid}: x = {Math.Round(gridCoords[grid], 0)} [mm]");
            }
            foreach (string frame in frameCoords.Keys)
            {
                Console.WriteLine($"Frame {frame}: y = {Math.Round(frameCoords[frame], 0)} [mm]");
            }

            foreach (IMember beam in steelBeams)
            {
                string name = beam.Name;
                int firstPtIdx = beam.MemberNodes.Value.First().Value.ConstructionPointIndex.Value;
                IEnumerable<IConstructionPoint> constructionPoints = await Model.GetConstructionPointsAsync(new List<int>() { firstPtIdx });



            }

            double timeUnpack = Math.Round(stopwatch.Elapsed.TotalSeconds, 3);
            Console.WriteLine($"\nLoading and member data unpacked in {timeUnpack} seconds.\n");

            // Finish up
            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;

            Check();

            return;
        }


    }
}
