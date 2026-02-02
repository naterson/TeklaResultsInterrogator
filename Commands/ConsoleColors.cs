using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeklaResultsInterrogator.Core;

namespace TeklaResultsInterrogator.Commands
{
    /// <summary>
    /// A diagnostic command to display all available Console colors.
    /// </summary>
    internal class ConsoleColors : BaseInterrogator
    {



        public ConsoleColors()
        {




        }

        /// <summary>
        /// Executes the command to print console colors.
        /// </summary>
        public override async Task ExecuteAsync()
        {
            await InitializeAsync();

            ConsoleColor[] colors = (ConsoleColor[])Enum.GetValues(typeof(ConsoleColor));
            foreach (ConsoleColor color in colors)
            {
                Console.ForegroundColor = color;
                Console.WriteLine($"The ConsoleColor is {color}");
            }

            return;
        }
    }
}



