using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeklaResultsInterrogator.Utils
{
    /// <summary>
    /// A generic list wrapper with a name property.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    public class NamedList<T>
    {
        /// <summary>
        /// Gets or sets the name of the list.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the values in the list.
        /// </summary>
        public List<T> Values { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NamedList{T}"/> class with a specified name.
        /// </summary>
        /// <param name="name">The name of the list.</param>
        public NamedList(string name)
        {
            Name = name;
            Values = new List<T>();
        }

        /// <summary>
        /// Adds an item to the list.
        /// </summary>
        /// <param name="value">The item to add.</param>
        public void Add(T value)
        {
            Values.Add(value);
        }
    }
}
