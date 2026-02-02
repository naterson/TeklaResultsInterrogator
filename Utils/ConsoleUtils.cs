using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeklaResultsInterrogator.Utils
{
    /// <summary>
    /// Static utility methods for console I/O, data conversion, and formatting.
    /// </summary>
    public static partial class ConsoleUtils
    {
        /// <summary>
        /// Writes a line to the console with a specific middle section colored differently.
        /// </summary>
        /// <param name="beforeText">Text before the colored section.</param>
        /// <param name="fancyText">Text to be colored.</param>
        /// <param name="afterText">Text after the colored section.</param>
        /// <param name="fancyColor">The color of the middle section.</param>
        public static void FancyWriteLine(string beforeText, string fancyText, string afterText, TextColor fancyColor)
        {
            Console.ForegroundColor = (ConsoleColor)TextColor.Text;
            Console.Write(beforeText);
            Console.ForegroundColor = (ConsoleColor)fancyColor;
            Console.Write(fancyText);
            Console.ForegroundColor = (ConsoleColor)TextColor.Text;
            Console.WriteLine(afterText);
        }

        /// <summary>
        /// Writes a line to the console in a specific color.
        /// </summary>
        /// <param name="text">The text to write.</param>
        /// <param name="fancyColor">The color to use.</param>
        public static void FancyWriteLine(string text, TextColor fancyColor)
        {
            Console.ForegroundColor = (ConsoleColor)fancyColor;
            Console.WriteLine(text);
            Console.ForegroundColor = (ConsoleColor)TextColor.Text;
        }

        /// <summary>
        /// Prompts the user with a message and reads a line of input from the console.
        /// </summary>
        /// <param name="prompt">The message to display to the user.</param>
        /// <param name="candidates">Optional list of candidates for tab-autocomplete and ghost text.</param>
        /// <returns>The user's input string, converted to uppercase, or null if empty/null.</returns>
        public static string? AskUser(string prompt, IEnumerable<string>? candidates = null)
        {
            if (candidates != null && candidates.Any())
            {
                if (!prompt.EndsWith(" ")) prompt += " ";
                if (candidates.Count() > 2) prompt = prompt.TrimEnd() + " (Tab for suggestions): ";
                Console.Write(prompt);
                Console.ForegroundColor = (ConsoleColor)TextColor.Command;
                string readIn = ReadInputInteractive(candidates.ToList());
                Console.ForegroundColor = (ConsoleColor)TextColor.Text;
                return string.IsNullOrEmpty(readIn) ? null : readIn.ToUpper();
            }
            else
            {
                Console.Write(prompt);
                Console.ForegroundColor = (ConsoleColor)TextColor.Command;
                string? readIn = Console.ReadLine();
                Console.ForegroundColor = (ConsoleColor)TextColor.Text;
                return string.IsNullOrEmpty(readIn) ? null : readIn.ToUpper();
            }
        }

        /// <summary>
        /// Reads user input interactively, providing tab-autocomplete and ghost text hints based on the provided candidates.
        /// </summary>
        /// <param name="candidates">The list of possible completions.</param>
        /// <returns>The confirmed input string.</returns>
        public static string ReadInputInteractive(List<string> candidates)
        {
            StringBuilder input = new();
            int tabIndex = -1;
            List<string> currentMatches = new();

            while (true)
            {
                // -- VISUALS START --
                string? suggestion = null;
                string? ghostText = null;

                if (input.Length > 0)
                {
                    // Prioritize StartsWith matches
                    suggestion = candidates.FirstOrDefault(c => c.StartsWith(input.ToString(), StringComparison.OrdinalIgnoreCase));
                    if (suggestion != null)
                    {
                        // Use consistent parenthesized style for all suggestions
                        // Only show if not a complete match
                        if (!suggestion.Equals(input.ToString(), StringComparison.OrdinalIgnoreCase))
                        {
                            ghostText = $" ({suggestion})";
                        }
                    }
                    else
                    {
                        // Fallback to Contains matches
                        suggestion = candidates.FirstOrDefault(c => c.Contains(input.ToString(), StringComparison.OrdinalIgnoreCase));
                        if (suggestion != null)
                        {
                            ghostText = $" ({suggestion})";
                        }
                    }
                }

                int currentLeft = Console.CursorLeft;
                int currentTop = Console.CursorTop;

                if (ghostText != null)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write(ghostText);
                    Console.SetCursorPosition(currentLeft, currentTop);
                }
                // -- VISUALS END --

                ConsoleKeyInfo key = Console.ReadKey(intercept: true);

                // -- CLEANUP START --
                if (ghostText != null)
                {
                    Console.Write(new string(' ', ghostText.Length));
                    Console.SetCursorPosition(currentLeft, currentTop);
                }
                // -- CLEANUP END --

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return input.ToString();
                }
                else if (key.Key == ConsoleKey.Tab)
                {
                    if (candidates.Count == 0) continue;

                    if (tabIndex == -1)
                    {
                        string currentInput = input.ToString();
                        // Prioritize StartsWith matches for consistency with visual suggestion
                        currentMatches = candidates
                            .Where(c => c.StartsWith(currentInput, StringComparison.OrdinalIgnoreCase))
                            .OrderBy(c => c)
                            .ToList();

                        // Fallback to Contains if no StartsWith found
                        if (currentMatches.Count == 0)
                        {
                            currentMatches = candidates
                                .Where(c => c.Contains(currentInput, StringComparison.OrdinalIgnoreCase))
                                .OrderBy(c => c)
                                .ToList();
                        }

                        if (currentMatches.Count == 0) continue;
                        tabIndex = 0;
                    }
                    else
                    {
                        tabIndex = (tabIndex + 1) % currentMatches.Count;
                    }

                    string match = currentMatches[tabIndex];

                    // Clear current input from console
                    while (input.Length > 0)
                    {
                        input.Length--;
                        Console.Write("\b \b");
                    }

                    input.Append(match);
                    Console.ForegroundColor = (ConsoleColor)TextColor.Command;
                    Console.Write(input.ToString());
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    if (input.Length > 0)
                    {
                        input.Length--;
                        Console.Write("\b \b");
                        tabIndex = -1;
                    }
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    input.Append(key.KeyChar);
                    Console.ForegroundColor = (ConsoleColor)TextColor.Command;
                    Console.Write(key.KeyChar);
                    tabIndex = -1;
                }
            }
        }
    }
}
