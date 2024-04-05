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
    public class TimberColumnForces : ForceInterrogator
    {
        public TimberColumnForces()
        {
            HasOutput = true;
            RequestedMemberType = new List<MemberConstruction>() { MemberConstruction.TimberColumn };
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

            List<IMember> timberColumns = AllMembers.Where(c => RequestedMemberType.Contains(c.Data.Value.Construction.Value)).ToList();

            Console.WriteLine($"{AllMembers.Count} structural members found in model.");
            Console.WriteLine($"{timberColumns.Count} timber columns found.");

            List<IHorizontalConstructionPlane> levels = (await Model.GetLevelsAsync()).ToList();

            double timeUnpack = Math.Round(stopwatch.Elapsed.TotalSeconds, 3);
            Console.WriteLine($"Loading and member data unpacked in {timeUnpack} seconds.\n");

            // Organizing column stacks into lists of lifts
            double startStack = timeUnpack;
            FancyWriteLine("Organizing Column Stacks...", TextColor.Title);
            List<ColumnLifts> timberColumnLifts = new List<ColumnLifts>();
            foreach (IMember column in timberColumns)
            {
                ColumnLifts lifts = new ColumnLifts(column);
                await lifts.OrganizeByFixity();
                timberColumnLifts.Add(lifts);
            }
            double endStack = Math.Round(stopwatch.Elapsed.TotalSeconds, 3);
            Console.WriteLine($"Column stacks organized in {Math.Round(endStack - startStack, 3)} seconds.\n");

            // Set up file
            double start1 = endStack;
            string file1 = SaveDirectory + @"TimberColumnForces_" + FileName + ".csv";
            string header1 = String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16}\n",
                "Tekla GUID", "Member Name", "Lift Name", "Included Spans", "Start Level", "End Level",
                "Section", "Breadth [in]", "Depth [in]", "Length [ft]", "Loading Name",
                "Shear Major [k]", "Shear Minor [k]",
                "Moment Major [k-ft]", "Moment Minor [k-ft]",
                "Axial Force [k]", "Torsion [k-ft]");
            File.WriteAllText(file1, "");
            File.AppendAllText(file1, header1);

            // Getting internal forces and writing table
            FancyWriteLine("Writing internal forces table...", TextColor.Title);
            using (StreamWriter sw1 = new StreamWriter(file1, true, Encoding.UTF8, bufferSize))
            {
                foreach (ColumnLifts columnLifts in timberColumnLifts)
                {
                    IMember member = columnLifts.ParentMember;
                    List<NamedList<IMemberSpan>> lifts = columnLifts.Lifts;
                    string memberName = member.Name;
                    Guid id = member.Id;

                    foreach (NamedList<IMemberSpan> lift in lifts)
                    {
                        int startNodeIdx = lift.Values.First().StartMemberNode.ConstructionPointIndex.Value;
                        IEnumerable<IConstructionPoint> startConstructionPoints = await Model.GetConstructionPointsAsync(new List<int>() { startNodeIdx });
                        IEnumerable<int> startPlaneIds = startConstructionPoints.Where(p => p.PlaneInfo.Value.Type == TSD.API.Remoting.Common.EntityType.HorizontalConstructionPlane).Select(p => p.PlaneInfo.Value.Index);
                        string startLevelName;
                        if (startPlaneIds.Any())
                        {
                            IHorizontalConstructionPlane startLevel = (await Model.GetLevelsAsync(startPlaneIds)).First();
                            startLevelName = startLevel.Name;
                        }
                        else
                        {
                            double zStart = startConstructionPoints.First().Coordinates.Value.Z;
                            IHorizontalConstructionPlane closestLevel = levels.OrderBy(l => Math.Abs(zStart - l.Level.Value)).First();
                            startLevelName = $"~{closestLevel.Name}";
                        }

                        int endNodeIdx = lift.Values.Last().EndMemberNode.ConstructionPointIndex.Value;
                        IEnumerable<IConstructionPoint> endConstructionPoints = await Model.GetConstructionPointsAsync(new List<int> { endNodeIdx });
                        IEnumerable<int> endPlaneIds = endConstructionPoints.Where(p => p.PlaneInfo.Value.Type == TSD.API.Remoting.Common.EntityType.HorizontalConstructionPlane).Select(p => p.PlaneInfo.Value.Index);
                        string endLevelName;
                        if (endPlaneIds.Any())
                        {
                            IHorizontalConstructionPlane endLevel = (await Model.GetLevelsAsync(endPlaneIds)).First();
                            endLevelName = endLevel.Name;
                        }
                        else
                        {
                            double zEnd = endConstructionPoints.First().Coordinates.Value.Z;
                            IHorizontalConstructionPlane closestLevel = levels.OrderBy(l => Math.Abs(zEnd - l.Level.Value)).First();
                            endLevelName = $"~ {closestLevel.Name}";
                        }

                        string liftName = memberName + $"-{lift.Name}";
                        string includedSpans = $"({lift.Values.Count}): " + String.Join("; ", lift.Values.Select(s => s.Name));
                        double length = lift.Values.Select(l => l.Length.Value).Sum() * 0.00328084; // Converting from [mm] to [ft]
                        List<IMemberSection> generalSections = lift.Values.Select(v => (IMemberSection)v.ElementSection.Value).ToList();
                        generalSections = generalSections.OrderBy(v => v.CrossSectionalArea.Value).ToList();
                        ITimberBeamSection section = (ITimberBeamSection)generalSections.First().PhysicalSection.Value;
                        string sectionName = section.LongName;
                        double breadth = Math.Round(section.Breadth * 0.0393701, 4);  // Converting from [mm] to [in]
                        double depth = Math.Round(section.Depth * 0.0393701, 4);  // Converting from [mm] to [in]

                        string liftLineOnly = String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}",
                            id, memberName, liftName, includedSpans, startLevelName, endLevelName, sectionName,
                            Math.Round(breadth, 3), Math.Round(depth, 3), Math.Round(length, 3));

                        foreach (ILoadingCase loadingCase in loadingCases)
                        {
                            string loadName = loadingCase.Name.Replace(',', '`');
                            MaxSpanInfo maxLiftInfo = new MaxSpanInfo();

                            foreach (IMemberSpan span in lift.Values)
                            {
                                SpanResults spanResults = new SpanResults(span, 1, loadingCase, reduced, AnalysisType, member);
                                MaxSpanInfo maxSpanInfo = await spanResults.GetMaxima();
                                maxLiftInfo.EnvelopeAndUpdate(maxSpanInfo);
                            }

                            string maxLine = liftLineOnly + "," + String.Format("{0},{1},{2},{3},{4},{5},{6}",
                                loadName,
                                maxLiftInfo.ShearMajor.Value,
                                maxLiftInfo.ShearMinor.Value,
                                maxLiftInfo.MomentMajor.Value,
                                maxLiftInfo.MomentMinor.Value,
                                maxLiftInfo.AxialForce.Value,
                                maxLiftInfo.Torsion.Value);
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
            Console.WriteLine($"Steel Beam table written in {time1} seconds.\n");

            // Finish up
            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;

            Check();

            return;
        }
    }
}
