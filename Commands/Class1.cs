using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeklaResultsInterrogator.Core;

namespace TeklaResultsInterrogator.Commands
{
    public class Class1 : VibrationInterrogator
    {
        public Class1()
        {

        }

        public override async Task ExecuteAsync()
        {
            await InitializeAsync();

            return;

        }
    }
}
