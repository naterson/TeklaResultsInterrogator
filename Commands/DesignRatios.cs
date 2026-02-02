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
using TeklaResultsInterrogator.Utils;
using TSD.API.Remoting.Bim;
using TSD.API.Remoting.Common;
using TSD.API.Remoting.Common.Properties;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Sections;
using TSD.API.Remoting.Solver;
using TSD.API.Remoting.Structure;
using static TeklaResultsInterrogator.Utils.ConsoleUtils;

namespace TeklaResultsInterrogator.Commands
{
    /// <summary>
    /// Interrogates the model to retrieve and export Design Utilization Ratios.
    /// </summary>
    internal class DesignRatios : SolverInterrogator
    {
        /// <inheritdoc/>
        public override bool ShowInMenu() { return true; }

        /// <summary>Initializes a new instance of the <see cref="DesignRatios"/> class.</summary>
        public DesignRatios()
        {
            HasOutput = true;
            RequestedMemberType = new List<MemberConstruction>
            {
                MemberConstruction.ColdFormedBeam,
                MemberConstruction.ColdFormedBrace,
                MemberConstruction.ColdFormedColumn,
                MemberConstruction.ColdFormedGablePost,
                MemberConstruction.ColdFormedParapetPost,
                MemberConstruction.ColdFormedTrussInternal,
                MemberConstruction.ColdFormedTrussMemberBottom,
                MemberConstruction.ColdFormedTrussMemberSide,
                MemberConstruction.ColdFormedTrussMemberTop,
                MemberConstruction.ColdRolledBeam,
                MemberConstruction.ColdRolledColumn,
                MemberConstruction.CompositeBeam,
                MemberConstruction.CompositeColumn,
                MemberConstruction.ConcreteBeam,
                MemberConstruction.ConcreteColumn,
                MemberConstruction.CouplingBeam,
                MemberConstruction.EavesBeam,
                MemberConstruction.GeneralMaterialBeam,
                MemberConstruction.GeneralMaterialColumn,
                MemberConstruction.GeneralMaterialBrace,
                MemberConstruction.GroundBeam,
                MemberConstruction.Purlin,
                MemberConstruction.Rail,
                MemberConstruction.SteelBeam,
                MemberConstruction.SteelBrace,
                MemberConstruction.SteelColumn,
                MemberConstruction.SteelGablePost,
                MemberConstruction.SteelJoist,
                MemberConstruction.SteelParapetPost,
                MemberConstruction.SteelTie,
                MemberConstruction.SteelTrussInternal,
                MemberConstruction.SteelTrussMemberBottom,
                MemberConstruction.SteelTrussMemberSide,
                MemberConstruction.SteelTrussMemberTop,
                MemberConstruction.StiffeningBeam,
                MemberConstruction.TimberBeam,
                MemberConstruction.TimberColumn,
                MemberConstruction.TimberBrace,
                MemberConstruction.TimberGablePost,
                MemberConstruction.TimberTrussInternal,
                MemberConstruction.TimberTrussMemberBottom,
                MemberConstruction.TimberTrussMemberSide,
                MemberConstruction.TimberTrussMemberTop
            };
        }

        /// <summary>
        /// Executes the Design Ratio interrogation, retrieving static check results and writing to CSV.
        /// </summary>
        public override async Task ExecuteAsync()
        {
            // Initialize parents
            await InitializeAsync();

            if (Flag) return;

            // Data setup and diagnostics initialization
            Stopwatch stopwatch = Stopwatch.StartNew();
            int bufferSize = 65536 * 2;

            // Unpacking loading data
            FancyWriteLine("Loading Summary:", TextColor.Title);
            Console.WriteLine("Unpacking loading data...");
            Console.WriteLine($"{AllLoadcases!.Count} loadcases found, {SolvedCases!.Count} solved.");
            Console.WriteLine($"{AllCombinations!.Count} load combinations found, {SolvedCombinations!.Count} solved.");
            Console.WriteLine($"{AllEnvelopes!.Count} load envelopes found, {SolvedEnvelopes!.Count} solved.\n");

            // Unpacking member data
            FancyWriteLine("\nMember summary:", TextColor.Title);
            Console.WriteLine("Unpacking member data...");

            List<IMember> members = AllMembers!.Where(c => RequestedMemberType.Contains(GetProperty(c.Data.Value.Construction))).ToList();

            Console.WriteLine($"{AllMembers!.Count} structural members found in model.");
            Console.WriteLine($"{members.Count} members will be interrogated.");

            double timeUnpack = Math.Round(stopwatch.Elapsed.TotalSeconds, 3);
            Console.WriteLine($"Loading and member data unpacked in {timeUnpack} seconds.\n");

            // Setting up output file
            double start1 = timeUnpack;

            string file1 = SaveDirectory + @"DesignRatios-static_" + FileName + ".csv";
            string header1 = String.Format("{0},{1},{2},{3}\n",
                "Tekla GUID", "Span Name", "Utilization Ratio (Static)", "Section");



            File.WriteAllText(file1, header1);

            // Getting Utilization Ratios and writing table
            FancyWriteLine("\nWriting Design Check Utilization Ratio table...", TextColor.Title);
            using (StreamWriter sw1 = new(file1, true, Encoding.UTF8, bufferSize))
            {
                foreach (IMember member in members)
                {
                    IEnumerable<IMemberSpan> spans = await member.GetSpanAsync();
                    foreach (IMemberSpan span in spans)
                    {
                        // Get Utilization Ratio
                        double? staticRatio = null;
                        IEnumerable<IKeyValuePair<CheckResultType, IReadOnlyProperty<ICheckResult>>> checkResults = span.CheckResults.Value;
                        IKeyValuePair<CheckResultType, IReadOnlyProperty<ICheckResult>>? staticCheckResult = checkResults.Where(r => r.Key == CheckResultType.Static).FirstOrDefault();
                        if (staticCheckResult != null)
                        {
                            ICheckResult? checkResultValue = GetProperty(staticCheckResult.Value);
                            if (checkResultValue != null)
                            {
                                staticRatio = GetProperty(checkResultValue.UtilizationRatio);
                            }
                        }

                        // Get Span identifiers
                        string spanName = span.Name.Replace(',', '`');
                        Guid spanID = span.Id;
                        IMemberSection section = (IMemberSection)span.ElementSection.Value;
                        string sectionName = section.PhysicalSection.Value.LongName;

                        // Write if ratio
                        if (staticRatio != null)
                        {
                            string line = String.Format("{0},{1},{2}, {3}", spanID, spanName, staticRatio, sectionName);
                            sw1.WriteLine(line);
                        }
                    }
                }
            }

            // Output diagnostics to console
            FancyWriteLine("Saved to: ", file1, "", TextColor.Path);
            double size1 = Math.Round((double)new FileInfo(file1).Length / 1024, 2);
            Console.WriteLine($"File size: {size1} KB");
            double time1 = Math.Round(stopwatch.Elapsed.TotalSeconds - start1, 3);
            Console.WriteLine($"Design Check Utilization Ratio table table written in {time1} seconds.\n");

            // Finish up
            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;

            Check();

            return;
        }
    }
}
