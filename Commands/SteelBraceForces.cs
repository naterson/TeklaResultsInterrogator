﻿using System;
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
using TSD.API.Remoting.Common;

namespace TeklaResultsInterrogator.Commands
{
    public class SteelBraceForces : ForceInterrogator
    {

        public SteelBraceForces() 
        {
            HasOutput = true;
            AnalysisType = AnalysisType.FirstOrderLinear;
            RequestedMemberType = new List<MemberConstruction>() {MemberConstruction.SteelBrace};
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
            int bufferSize = 65536*2;

            // Unpacking loading data
            FancyWriteLine("Loading Summary:", TextColor.Title);
            Console.WriteLine("Unpacking loading data...");
            Console.WriteLine($"{AllLoadcases.Count} loadcases found, {SolvedCases.Count} solved.");
            Console.WriteLine($"{AllCombinations.Count} load combinations found, {SolvedCombinations.Count} solved.");
            Console.WriteLine($"{AllEnvelopes.Count} load envelopes found, {SolvedEnvelopes.Count} solved.\n");

            stopwatch.Stop();
            List<ILoadingCase> loadingCases = AskLoading(SolvedCases, SolvedCombinations, SolvedEnvelopes);
            bool reduced = false; // Braces do not have live load reductions so this is automatically set to false
            stopwatch.Start();
            // Unpacking member data
            FancyWriteLine("\nMember summary:", TextColor.Title);
            Console.WriteLine("Unpacking member data...");

            bool? GravityOnlyState = AskGravityOnly();
            bool? AutoDesignState = AskAutoDesign();

            List<IMember> steelBraces = null;

            if (GravityOnlyState == null & AutoDesignState == null)
            {
               steelBraces = AllMembers.Where(c => RequestedMemberType.Contains(c.Data.Value.Construction.Value)).ToList();
            }
            else if (AutoDesignState == null)
            {
                steelBraces = AllMembers.Where(c => RequestedMemberType.Contains(c.Data.Value.Construction.Value) & c.Data.Value.GravityOnly.Value == GravityOnlyState).ToList();
            }
            else if (GravityOnlyState == null)
            {
                steelBraces = AllMembers.Where(c => RequestedMemberType.Contains(c.Data.Value.Construction.Value) & c.Data.Value.AutoDesign.Value == AutoDesignState).ToList();
            }
            else
            {
                steelBraces = AllMembers.Where(c => RequestedMemberType.Contains(c.Data.Value.Construction.Value) & c.Data.Value.AutoDesign.Value == AutoDesignState & c.Data.Value.GravityOnly.Value == GravityOnlyState).ToList();
            };

            Console.WriteLine($"{AllMembers.Count} structural members found in model.");
            Console.WriteLine($"{steelBraces.Count} steel braces found.");

            double timeUnpack = Math.Round(stopwatch.Elapsed.TotalSeconds, 3);
            Console.WriteLine($"Loading and member data unpacked in {timeUnpack} seconds.\n");

            // Extracting internal forces
            FancyWriteLine("Retrieving internal forces...", TextColor.Title);
            stopwatch.Stop();
            int subdivisions = AskPoints(1);  // Setting maximum number of stations to 1
            stopwatch.Start();
            FancyWriteLine($"Asked for {subdivisions} points.", TextColor.Warning);

            // Setting up file
            double start1 = timeUnpack;
            string file1 = SaveDirectory + @"SteelBraceForces_" + FileName + ".csv";
            string header1 = String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10} \n",
                "Tekla GUID", "Member Name", "Level", "Shape", "Material", 
                "Start Node", "End Node","Span Length [ft]", "Span Rotation [deg]","Loading Name", 
                "Axial Force [k]");
            File.WriteAllText(file1, ""); // Bug will throw an exception if the user has the excel file open ekj
            File.AppendAllText(file1, header1);

            // Getting internal forces and writing table

            FancyWriteLine("\nWriting internal forces table...", TextColor.Title);
            using (StreamWriter sw1 = new StreamWriter(file1, true, Encoding.UTF8, bufferSize))
            {
                foreach (IMember member in steelBraces)
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
                        //string spanName = span.Name;
                        int spanIdx = span.Index;
                        double length = span.Length.Value;
                        double lengthFt = length * 0.00328084; // Converting from [mm] to [ft]
                        double rot = Math.Round(span.RotationAngle.Value * 57.2958, 3); // Converting from [rad] to [deg]
                        IMemberSection section = (IMemberSection)span.ElementSection.Value;
                        string sectionName = section.PhysicalSection.Value.LongName;
                        string materialGrade = span.Material.Value.Name;

                        int startNodeIdx = span.StartMemberNode.ConstructionPointIndex.Value;
                        int endNodeIdx = span.EndMemberNode.ConstructionPointIndex.Value;

                        string spanLineOnly = String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                            id, name, levelName, sectionName, materialGrade,
                            startNodeIdx, endNodeIdx, lengthFt, rot);

                        if (subdivisions == 0)
                        {
                            sw1.WriteLine(spanLineOnly);
                        }

                        else
                        {
                            foreach (ILoadingCase loadingCase in loadingCases)
                            {
                                string loadName = loadingCase.Name.Replace(',', '`');
                                SpanResults spanResults = new SpanResults(span, subdivisions, loadingCase, reduced, AnalysisType, member);

                                if (subdivisions >= 1)
                                {
                                    // Getting maximum internal forces and displacements and locations
                                    MaxSpanInfo maxSpanInfo = await spanResults.GetMaxima();
                                    string maxLine = spanLineOnly + "," + String.Format("{0},{1}",
                                        loadName, 
                                        maxSpanInfo.AxialForce.Value);
                                    sw1.WriteLine(maxLine);
                                }
                                
                            }
                        }
                    }
                }
            }

            // Output diagnostics to console
            FancyWriteLine("Saved to: ", file1, "", TextColor.Path);
            double size1 = Math.Round((double)new FileInfo(file1).Length / 1024, 2);
            Console.WriteLine($"File size: {size1} KB");
            double time1 = Math.Round(stopwatch.Elapsed.TotalSeconds - start1, 3);
            Console.WriteLine($"Steel Brace table written in {time1} seconds.\n");

            // Finish up
            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;

            Check();

            return;
        }
    }
}
