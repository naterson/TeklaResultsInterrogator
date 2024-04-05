using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeklaResultsInterrogator.Utils
{
    public static partial class Utils
    {
        public static string? AskUser(string prompt)
        {
            Console.Write(prompt);
            Console.ForegroundColor = (ConsoleColor)TextColor.Command;
            string? readIn = Console.ReadLine();
            Console.ForegroundColor = (ConsoleColor)TextColor.Text;
            return readIn;
        }
    }
}
