using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeklaResultsInterrogator.Utils
{
    public static partial class Utils
    {
        public static void FancyWriteLine(string beforeText, string fancyText, string afterText, TextColor fancyColor)
        {
            Console.ForegroundColor = (ConsoleColor)TextColor.Text;
            Console.Write(beforeText);
            Console.ForegroundColor = (ConsoleColor)fancyColor;
            Console.Write(fancyText);
            Console.ForegroundColor = (ConsoleColor)TextColor.Text;
            Console.WriteLine(afterText);
        }

        public static void FancyWriteLine(string text, TextColor fancyColor)
        {
            Console.ForegroundColor = (ConsoleColor)fancyColor;
            Console.WriteLine(text);
            Console.ForegroundColor = (ConsoleColor)TextColor.Text;
        }
    }
}
