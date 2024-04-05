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
using TSD.API.Remoting.Sections;
using TeklaResultsInterrogator.Utils;
using static TeklaResultsInterrogator.Utils.Utils;

namespace TeklaResultsInterrogator.Commands
{
    internal class TimberBeamForces : ForceInterrogator
    {
        public TimberBeamForces()
        {
            HasOutput = true;
            RequestedMemberType = new List<MemberConstruction>() { MemberConstruction.TimberBeam };
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

            List<IMember> timberBeams = AllMembers.Where(c => RequestedMemberType.Contains(c.Data.Value.Construction.Value)).ToList();

            Console.WriteLine($"{AllMembers.Count} structural members found in model.");
            Console.WriteLine($"{timberBeams.Count} timber beams found.");

            double timeUnpack = Math.Round(stopwatch.Elapsed.TotalSeconds, 3);
            Console.WriteLine($"Loading and member data unpacked in {timeUnpack} seconds.\n");

            // Extracting internal forces
            FancyWriteLine("Retrieving internal forces...", TextColor.Title);

            // Setting up file
            double start1 = timeUnpack;
            string file1 = SaveDirectory + @"TimberBeamForces_" + FileName + ".csv";
            string header1 = String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21}\n",
                "Tekla GUID", "Member Name", "Level", "Section", "Breadth [in]", "Depth [in]", "Span Name",
                "Start Node", "Start Node Fixity", "End Node", "End Node Fixity",
                "Span Length [ft]", "Span Rotation [deg]", "Loading Name",
                "Shear Major [k]", "Shear Minor [k]", "Moment Major [k-ft]", "Moment Minor [k-ft]",
                "Axial Force [k]", "Torsion [k-ft]", "Deflection Major [in]", "Deflection Minor [in]");
            File.WriteAllText(file1, "");
            File.AppendAllText(file1, header1);

            // Getting internal forces and writing table
            FancyWriteLine("\nWriting internal forces table...", TextColor.Title);
            using (StreamWriter sw1 = new StreamWriter(file1, true, Encoding.UTF8, bufferSize))
            {
                foreach (IMember member in timberBeams)
                {
                    string name = member.Name;
                    Guid id = member.Id;
                    IEnumerable<IMemberSpan> spans = await member.GetSpanAsync();

                    int constructionPointIndex = member.MemberNodes.Value.First().Value.ConstructionPointIndex.Value;
                    IEnumerable<IConstructionPoint> constructionPoints = await Model.GetConstructionPointsAsync(new List<int>() { constructionPointIndex });
                    int planeId = constructionPoints.First().PlaneInfo.Value.Index;
                    IEnumerable<IHorizontalConstructionPlane> level = await Model.GetLevelsAsync(new List<int>() { planeId });
                    string levelName;
                    if (level.Any())
                    {
                        levelName = level.First().Name;
                    }
                    else
                    {
                        levelName = "Not Associated";
                    }

                    foreach (IMemberSpan span in spans)
                    {
                        string spanName = span.Name;
                        int spanIdx = span.Index;
                        double length = span.Length.Value;
                        double lengthFt = length * 0.00328084; // Converting from [mm] to [ft]
                        double rot = Math.Round(span.RotationAngle.Value * 57.2958, 3); // Converting from [rad] to [deg]
                        IMemberSection generalSection = (IMemberSection)span.ElementSection.Value;
                        ITimberBeamSection section = (ITimberBeamSection)generalSection.PhysicalSection.Value;
                        string sectionName = section.LongName;
                        double breadth = Math.Round(section.Breadth * 0.0393701, 4);  // Converting from [mm] to [in]
                        double depth = Math.Round(section.Depth * 0.0393701, 4);  // Converting from [mm] to [in]

                        int startNodeIdx = span.StartMemberNode.ConstructionPointIndex.Value;
                        string startNodeFixity = span.StartReleases.Value.DegreeOfFreedom.Value.ToString();
                        if (span.StartReleases.Value.Cantilever.Value == true)
                        {
                            startNodeFixity += " (Cantilever end)";
                        }
                        startNodeFixity = startNodeFixity.Replace(',', '|');
                        int endNodeIdx = span.EndMemberNode.ConstructionPointIndex.Value;
                        string endNodeFixity = span.EndReleases.Value.DegreeOfFreedom.Value.ToString();
                        if (span.EndReleases.Value.Cantilever.Value == true)
                        {
                            endNodeFixity += " (Cantilever end)";
                        }
                        endNodeFixity = endNodeFixity.Replace(',', '|');

                        string spanLineOnly = String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}",
                            id, name, levelName, sectionName, breadth, depth, spanName,
                            startNodeIdx, startNodeFixity, endNodeIdx, endNodeFixity, lengthFt, rot);

                        Console.WriteLine(spanName);

                        foreach (ILoadingCase loadingCase in loadingCases)
                        {
                            string loadName = loadingCase.Name.Replace(',', '`');
                            SpanResults spanResults = new SpanResults(span, 1, loadingCase, reduced, AnalysisType, member);

                            // Getting maximum internal forces and displacements and locations
                            MaxSpanInfo maxSpanInfo = await spanResults.GetMaxima();
                            string maxLine = spanLineOnly + "," + String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                                loadName,
                                maxSpanInfo.ShearMajor.Value,
                                maxSpanInfo.ShearMinor.Value,
                                maxSpanInfo.MomentMajor.Value,
                                maxSpanInfo.MomentMinor.Value,
                                maxSpanInfo.AxialForce.Value,
                                maxSpanInfo.Torsion.Value,
                                maxSpanInfo.DeflectionMajor.Value,
                                maxSpanInfo.DeflectionMinor.Value);
                            sw1.WriteLine(maxLine);
                        }
                    }
                }
            }

            // Output diagnostics to console
            FancyWriteLine("Saved to: ", file1, "", TextColor.Path);
            double size1 = Math.Round((double)new FileInfo(file1).Length / 1024, 2);
            Console.WriteLine($"File size: {size1} KB");
            double time1 = Math.Round(stopwatch.Elapsed.TotalSeconds - start1, 3);
            Console.WriteLine($"Timber Beam table written in {time1} seconds.\n");

            // Finish up
            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;

            Check();

            return;
        }
    }
}
