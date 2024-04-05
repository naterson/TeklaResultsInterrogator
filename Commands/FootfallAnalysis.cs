using System.Diagnostics;
using System.Text;
using TeklaResultsInterrogator.Core;
using TeklaResultsInterrogator.Utils;
using static TeklaResultsInterrogator.Utils.Utils;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Solver;

namespace TeklaResultsInterrogator.Commands
{
    public class FootfallAnalysis : VibrationInterrogator
    {
        public FootfallAnalysis()
        {
            HasOutput = true;
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

            // Data setup and diagnostics
            Stopwatch stopwatch = Stopwatch.StartNew();
            int bufferSize = 65536;

            // Unpacking vibration data
            FancyWriteLine("Vibration Data Summary:", TextColor.Title);
            Console.WriteLine("Unpacking vibration data...");
            IReadOnlyList<IVibrationMode> modes = LoadingVibration.Modes;
            IReadOnlyDictionary<int, INodeVibration> nodeVibrations = LoadingVibration.NodeVibrations;

            double summedActiveMass = LoadingVibration.SummedActiveMass.Mz;
            double summedTotalMass = LoadingVibration.SummedTotalMass.Mz;
            double massUtilization = Math.Round(summedActiveMass / summedTotalMass * 100, 2);

            List<IVibrationMode> sortedModes = modes.OrderBy(m => m.Frequency).ToList();
            double lowestFreq = sortedModes.First().Frequency;
            double highestFreq = sortedModes.Last().Frequency;

            Console.WriteLine($"{Nodes.Count()} nodes found.");
            Console.WriteLine($"{modes.Count} vibration modes found:");
            Console.WriteLine($"  Slowest: {Math.Round(lowestFreq, 2)} Hz");
            Console.WriteLine($"  Fastest: {Math.Round(highestFreq, 3)} Hz");
            Console.WriteLine($"Active mass is {massUtilization}% of total.");

            double timeUnpack = Math.Round(stopwatch.Elapsed.TotalSeconds, 3);
            Console.WriteLine($"Vibration data unpacked in {timeUnpack} seconds.\n");

            // Table 1: Modal Information
            FancyWriteLine("Modal Information Table:", TextColor.Title);
            Console.WriteLine("Writing modal information table...");
            double start1 = stopwatch.Elapsed.TotalSeconds;
            string file1 = SaveDirectory + @"FootfallAnalysis-ModalInformation_" + FileName + ".csv";
            string header1 = String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}\n", "Mode No.", "Frequency [Hz]", "Modal Mass [N]", "Mx [%]", "My [%]", "Mz [%]", "Rx [%]", "Ry [%]", "Rz [%]");
            File.WriteAllText(file1, "");
            File.AppendAllText(file1, header1);

            // Iterating through each mode and writing data to file
            using (StreamWriter sw1 = new StreamWriter(file1, true, Encoding.UTF8, bufferSize))
            {
                int modeId = 1;  // Manually indexing mode ID starting at 1
                foreach (IVibrationMode mode in modes)
                {
                    double f = mode.Frequency;                    // Modal frequency [Hz]
                    double m = mode.ModalMass;                    // Modal mass [N];
                    double mx = mode.MassParticipation.Mx * 100;  // Mass participation factor [%]
                    double my = mode.MassParticipation.My * 100;
                    double mz = mode.MassParticipation.Mz * 100;
                    double rx = mode.MassParticipation.Rx * 100;  // Mass Participation Rotation factor [%]
                    double ry = mode.MassParticipation.Ry * 100;
                    double rz = mode.MassParticipation.Rz * 100;

                    string line1 = String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}", modeId, f, m, mx, my, mz, rx, ry, rz);
                    sw1.WriteLine(line1);

                    modeId++;
                }
            }

            // Output diagnostics to console
            FancyWriteLine("Saved to: ", file1, "", TextColor.Path);
            double size1 = Math.Round((double)new FileInfo(file1).Length / 1024, 2);
            Console.WriteLine($"File size: {size1} KB");
            double time1 = Math.Round(stopwatch.Elapsed.TotalSeconds - start1, 3);
            Console.WriteLine($"Modal information table written in {time1} seconds.\n");

            // Table 2: Modal Shapes
            FancyWriteLine("Modal Shape Table:", TextColor.Title);
            Console.WriteLine("Writing modal shape table...");
            double start2 = stopwatch.Elapsed.TotalSeconds;
            string file2 = SaveDirectory + @"FootfallAnalysis-ModalShapes_" + FileName + ".csv";
            string header2 = String.Format("{0},{1},{2},{3},{4},{5},{6},{7}\n", "Joint ID", "Mode No.", "Ux [m]", "Uy [m]", "Uz [m]", "Rx [rad]", "Ry [rad]", "Rz [rad]");
            File.WriteAllText(file2, "");
            File.WriteAllText(file2, header2);

            // Iterating through each node and getting nodal information
            using (StreamWriter sw2 = new StreamWriter(file2, true, Encoding.UTF8, bufferSize))
            {
                foreach (KeyValuePair<int, INodeVibration> kvp in nodeVibrations)
                {
                    int nodeId = kvp.Key;
                    INodeVibration vibration = kvp.Value;
                    IReadOnlyList<IDisplacement> displacements = vibration.Displacements;

                    // Iterating through each mode and getting modal information and writing to file
                    int nodeModeId = 1;  // Manually indexing mode ID starting at 1
                    foreach (IDisplacement displacement in displacements)
                    {
                        double ux = displacement.Mx;  // Nodal displacements [m]
                        double uy = displacement.My;
                        double uz = displacement.Mz;
                        double rx = displacement.Rx;  // Nodal rotations [rad]
                        double ry = displacement.Ry;
                        double rz = displacement.Rz;

                        string line2 = String.Format("{0},{1},{2},{3},{4},{5},{6},{7}", nodeId, nodeModeId, ux.ToString("E10"), uy.ToString("E10"), uz.ToString("E10"), rx.ToString("E10"), ry.ToString("E10"), rz.ToString("E10"));
                        sw2.WriteLine(line2);

                        nodeModeId++;
                    }
                }
            }

            // Output diagnostics to console
            FancyWriteLine("Saved to: ", file2, "", TextColor.Path);
            double size2 = Math.Round((double)new FileInfo(file2).Length / 1024, 2);
            Console.WriteLine($"File size: {size2} KB");
            double time2 = Math.Round(stopwatch.Elapsed.TotalSeconds - start2, 3);
            Console.WriteLine($"Modal shape table completed in {time2} seconds.\n");

            // Table 3: Joint Coordinator
            FancyWriteLine("Joint Coordinator Table:", TextColor.Title);
            Console.WriteLine("Writing joint coordinator table...");
            double start3 = stopwatch.Elapsed.TotalSeconds;
            string file3 = SaveDirectory + @"FootFallAnalysis-JointCoordinator_" + FileName + ".csv";
            string header3 = String.Format("{0},{1},{2},{3}\n", "Joint ID", "X [ft]", "Y [ft]", "Z [ft]");
            File.WriteAllText(file3, "");
            File.AppendAllText(file3, header3);

            // Iterating through each node and writing data to file
            using (StreamWriter sw3 = new StreamWriter(file3, true, Encoding.UTF8, bufferSize))
            {
                foreach (INode node in Nodes)
                {
                    int id = node.Index;  // Node index [-]
                    double ux = node.Coordinates.X * 0.00328084;  // Nodal coordinates [ft]
                    double uy = node.Coordinates.Y * 0.00328084;
                    double uz = node.Coordinates.Z * 0.00328084;

                    string line3 = String.Format("{0},{1},{2},{3}", id, ux, uy, uz);
                    sw3.WriteLine(line3);
                }
            }

            // Output diagnostics to console
            FancyWriteLine("Saved to: ", file3, "", TextColor.Path);
            double size3 = Math.Round((double)new FileInfo(file3).Length / 1024, 2);
            Console.WriteLine($"File size: {size3} KB");
            double time3 = Math.Round(stopwatch.Elapsed.TotalSeconds - start3, 3);
            Console.WriteLine($"Joint coordinator table completed in {time3} seconds.");

            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;

            return;
        }
    }
}
