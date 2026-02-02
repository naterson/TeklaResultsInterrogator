using System;

namespace TeklaResultsInterrogator.Utils
{
    /// <summary>
    /// Displays the application branding header.
    /// </summary>
    public static class AppHeader
    {
        /// <summary>
        /// Prints the application header with ASCII art to the console.
        /// </summary>
        public static void PrintHeader()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine(@"  _          __  __ ");
            Console.WriteLine(@" | |        |  \/  |");
            Console.WriteLine(@" | |     ___| \  / |");
            Console.WriteLine(@" | |    / _ \ |\/| |");
            Console.WriteLine(@" | |___|  __/ |  | |");
            Console.WriteLine(@" |______\___|_|  |_|");
            Console.WriteLine(@"");

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" Tekla Results Interrogator");
            Console.WriteLine(" __________________________");
            Console.WriteLine("");
        }
    }
}
