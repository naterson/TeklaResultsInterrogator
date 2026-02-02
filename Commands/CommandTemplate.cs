using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeklaResultsInterrogator.Core;

namespace TeklaResultsInterrogator.Commands
{
    /// <summary>
    /// A template class for creating new Interrogator commands.
    /// </summary>
    public class CommandTemplate : SolverInterrogator
    {
        // Declare internal/private properties here

        /// <summary>Initializes a new instance of the <see cref="CommandTemplate"/> class.</summary>
        public CommandTemplate()
        {
            HasOutput = false;
        }

        /// <summary>
        /// Executes the command routines.
        /// </summary>
        public override async Task ExecuteAsync()
        {
            await InitializeAsync();

            if (Flag)
            {
                return;
            }

            // Data setup and diagnostics initialization
            Stopwatch stopwatch = Stopwatch.StartNew();

            // Unpacking loading data
            LogLoadingSummary();

            stopwatch.Stop();

            // Prompt for user input (excluded from execution timer)
            // var ... = AskUser(...);

            stopwatch.Start();

            // Unpacking member data
            // var members = AskAndFilterMembers(true, true);



            double timeUnpack = Math.Round(stopwatch.Elapsed.TotalSeconds, 3);
            Console.WriteLine($"Loading and member data unpacked in {timeUnpack} seconds.\n");

            // Call all command routines and subroutines here within this method

            // Recommended Pattern:
            // 1. Phase 1: Organize data & Collect indices (e.g. Construction Points)
            // 2. Phase 2: Batch Fetch geometry (e.g. GetConstructionPointsAsync)
            // 3. Phase 3: Parallel Execution
            //    // Option A: Task.WhenAll (Best for simple collections)
            //    var tasks = new List<Task>();
            //    foreach (var item in data) { tasks.Add(ProcessItemAsync(item, ...)); }
            //    await Task.WhenAll(tasks);
            //
            //    // Option B: Parallel.ForEachAsync (.NET 6+) (Compact & Throttled)
            //    await Parallel.ForEachAsync(data, async (item, token) => 
            //    {
            //        await ProcessItemAsync(item, ...);
            //    });

            // Finish up
            stopwatch.Stop();
            ExecutionTime = stopwatch.Elapsed.TotalSeconds;

            Check();

            return;
        }


    }
}
