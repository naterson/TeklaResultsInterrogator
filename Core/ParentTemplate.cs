using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeklaResultsInterrogator.Core
{
    public class ParentTemplate : BaseInterrogator
    {
        // Declare properties here

        // Leave parameterless constructor empty
        public ParentTemplate() { }

        // Treat this as the parameterless constructor
        public override void Initialize()
        {
            // Initialize base class
            Stopwatch stopwatch = Stopwatch.StartNew();
            InitializeBase();
            
            // Populate class properties here

            // Complete Initialization
            stopwatch.Stop();
            InitializationTime = stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine($"Initialization completed in {Math.Round(InitializationTime, 3)} seconds.\n");

            // Ensure no declared properties are null
            Check();

            return;
        }


    }
}
