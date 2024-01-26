using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeklaResultsInterrogator.Core;

namespace TeklaResultsInterrogator.Commands
{
    internal class ConsoleColors : BaseInterrogator
    {
        public ConsoleColors() { }
        public override Task Execute()
        {
            Initialize();

            ConsoleColor[] colors = (ConsoleColor[])Enum.GetValues(typeof(ConsoleColor));
            foreach (ConsoleColor color in colors)
            {
                Console.ForegroundColor = color;
                Console.WriteLine($"The ConsoleColor is {color}");
            }

            return Task.CompletedTask;

        }
    }
}
