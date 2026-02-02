using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeklaResultsInterrogator.Utils
{
    public static partial class ConsoleUtils
    {
        /// <summary>
        /// Writes a list of data rows to a CSV file including a header row.
        /// </summary>
        /// <param name="headers">List of column headers.</param>
        /// <param name="data">List of object arrays representing rows.</param>
        /// <param name="filePath">Target file path.</param>
        public static void WriteToCsv(List<string> headers, List<object[]> data, string filePath)
        {
            using var writer = new StreamWriter(filePath);

            // Write headers
            writer.WriteLine(string.Join(",", headers));

            // Write data rows
            foreach (var row in data)
            {
                writer.WriteLine(string.Join(",", row));
            }
        }
        /// <summary>
        /// Escapes a value for CSV format (handling commas, quotes, newlines).
        /// </summary>
        /// <param name="value">The string value to escape.</param>
        /// <returns>The escaped string.</returns>
        public static string EscapeCsvValue(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('\"') || value.Contains('\n') || value.Contains('\r'))
            {
                value = value.Replace("\"", "\"\"");
                return $"\"{value}\"";
            }
            return value;
        }
    }
}
