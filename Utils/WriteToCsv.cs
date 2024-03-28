using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeklaResultsInterrogator.Utils
{
    public static partial class Utils
    {
        public static void WriteToCsv(List<string> headers, List<object[]> data, string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                // Write headers
                writer.WriteLine(string.Join(",", headers));

                // Write data rows
                foreach (var row in data)
                {
                    writer.WriteLine(string.Join(",", row));
                }
            }
        }
    }
}
