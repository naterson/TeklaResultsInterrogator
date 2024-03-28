using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeklaResultsInterrogator.Utils
{
    public class NamedList<T>
    {
        public string Name { get; set; }
        public List<T> Values { get; set; }

        public NamedList(string name)
        {
            Name = name;
            Values = new List<T>();
        }

        public void Add(T value)
        {
            Values.Add(value);
        }
    }
}
