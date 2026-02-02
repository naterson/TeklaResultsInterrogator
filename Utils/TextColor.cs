using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeklaResultsInterrogator.Utils
{
    /// <summary>
    /// Custom console text colors for standardized application output.
    /// </summary>
    public enum TextColor
    {
        /// <summary>Standard text color (White).</summary>
        Text = ConsoleColor.White,
        /// <summary>Command/Option color (Green).</summary>
        Command = ConsoleColor.Green,
        /// <summary>Title/Header color (DarkCyan).</summary>
        Title = ConsoleColor.DarkCyan,
        /// <summary>File path color (DarkYellow).</summary>
        Path = ConsoleColor.DarkYellow,
        /// <summary>Error message color (DarkRed).</summary>
        Error = ConsoleColor.DarkRed,
        /// <summary>Warning message color (Yellow).</summary>
        Warning = ConsoleColor.Yellow,
    }
}
