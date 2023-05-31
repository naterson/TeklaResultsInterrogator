using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeklaResultsInterrogator.Core;

namespace TeklaResultsInterrogator.Commands
{
    public class CommandTemplate : ParentTemplate  // Should inherit a parent Interrogator class
    {
        // Should not declare any additional public properties here

        // Leave class constructor parameterless and empty
        public CommandTemplate() { }

        // Treat this as the parameterless constructor
        public override async Task ExecuteAsync()
        {
            // Initialize parents
            await InitializeAsync();

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

            return;
        }


    }
}
