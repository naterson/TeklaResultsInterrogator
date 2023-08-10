using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TeklaResultsInterrogator.Core;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Solver;
using TSD.API.Remoting.Structure;

namespace TeklaResultsInterrogator.Commands
{
    public class SteelBeamForces : ForceInterrogator
    {
        public SteelBeamForces()
        {
            HasOutput = true;
            RequestedMemberType = new List<MemberConstruction>() { MemberConstruction.SteelBeam };
        }

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
            int bufferSize = 65536;

            // Unpacking loading data
            FancyWriteLine("Loading Summary:", TextColor.Title);
            Console.WriteLine("Unpacking loading data...");
            Console.WriteLine($"{AllLoadcases.Count} loadcases found, {SolvedCases.Count} solved.");
            Console.WriteLine($"{AllCombinations.Count} load combinations found, {SolvedCombinations.Count} solved.");
            Console.WriteLine($"{AllEnvelopes.Count} load envelopes found, {SolvedEnvelopes.Count} solved.\n");

            List<ILoadingCase> loadingCases = AskLoading(SolvedCases, SolvedCombinations, SolvedEnvelopes);

            bool reduced = AskReduced();

            // Unpacking member data
            FancyWriteLine("Member summary:", TextColor.Title);
            Console.WriteLine("Unpacking member data...");

            List<IMember> steelBeams = AllMembers.Where(c => RequestedMemberType.Contains(c.Data.Value.Construction.Value)).ToList();

            Console.WriteLine($"{AllMembers.Count} structural members found in model.");
            Console.WriteLine($"{steelBeams.Count} steel beams found.\n");



            #region span length test

            FancyWriteLine("Testing member force spans", TextColor.Title);
            int subdivisions = AskPoints(20);  // Setting maximum number of stations to 20
            FancyWriteLine($"Asked for {subdivisions} points.", TextColor.Warning);

            foreach (IMember member in steelBeams)
            {
                string name = member.Name;
                IEnumerable<IMemberSpan> spans = await member.GetSpanAsync();

                foreach (IMemberSpan span in spans)
                {
                    string spanName = span.Name;
                    int spanIdx = span.Index;
                    double length = span.Length.Value;

                    foreach (ILoadingCase loadingCase in loadingCases)
                    {
                        string loadName = loadingCase.Name;
                        SpanResults spanResults = new SpanResults(span, subdivisions, loadingCase, reduced, AnalysisType, member);
                        if (subdivisions >= 1)
                        {
                            // Getting maximum internal forces and displacements and locations
                            MaxSpanInfo maxSpanInfo = await spanResults.GetMaxima();
                        }
                        if (subdivisions >= 2)
                        {
                            // Getting internal forces and displacements at each station
                            List<PointSpanInfo> pointSpanInfo = await spanResults.GetStations();

                        }
                        

                    }
                }

            }

            #endregion




            // Call all command routines and subroutines here within this method

            // Finish up
            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;

            Check();

            return;
        }
    }
}
