using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

using TSD.API.Remoting;
using TSD.API.Remoting.Solver;
using TSD.API.Remoting.Structure;
using TSD.API.Remoting.Document;
using TSD.Rpc.Analysis;
using AnalysisType = TSD.API.Remoting.Solver.AnalysisType;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Sections;
using TSD.API.Remoting.Common;
using System.Data.Common;

using TeklaResultsInterrogator.Core;
using TeklaResultsInterrogator.Commands;
using System.Reflection;
using System.Diagnostics;
using TeklaResultsInterrogator.Utils;
using static TeklaResultsInterrogator.Utils.Utils;


namespace TeklaResultsInterrogator
{
    internal class Program
    {
        public static async Task Main()
        {
            // Initialize Menu and get Command property on completion of Menu constructor
            Menu menu = new Menu();
            var command = menu.Command;
            if (command != null )
            {
               await command.ExecuteAsync();  // Execute command
            }
            else
            {
                Console.WriteLine("Aborted.");
                HaltExit(true);
                return;
            }
            command.Check();
            // If command executed successfully, wrap it up and exit
            if (!command.Flag)
            {
                double time = Math.Round(command.InitializationTime + command.ExecutionTime, 3);
                command.MakeHeader(true);
                FancyWriteLine("Command ", command.Name, $" executed successfully in {time} seconds.\nThe application will now terminate.", TextColor.Command);
                command.MakeHeader(true);
                HaltExit(true);
                return;
            }
            else
            {
                FancyWriteLine($"{command.Name} failed to execute completely. Task aborted.", TextColor.Error);
                HaltExit(false);
                return;
            }
            
        }
        private static void HaltExit(bool success)
        {
            string name = Process.GetCurrentProcess().ProcessName;
            string path = Environment.ProcessPath;
            int process = Process.GetCurrentProcess().Id;
            string status = (success == true) ? "successfully" : "unsuccessfully";
            Console.WriteLine($"\n{name} at {path} (process {process}) executed {status}.");
            Console.WriteLine("The application will now terminate.\nPress any key to close this window . . .");
            Console.ReadKey(true);
        }
    }
}