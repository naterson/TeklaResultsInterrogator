using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSD.API.Remoting.Common.Properties;

namespace TeklaResultsInterrogator.Utils
{
    public static partial class ConsoleUtils
    {
        /// <summary>
        /// safely retrieves the value from a generic read-only property if applicable, otherwise returns default.
        /// </summary>
        /// <typeparam name="T">The type of the property value.</typeparam>
        /// <param name="property">The property to check.</param>
        /// <returns>The value if applicable, otherwise default(T).</returns>
        public static T? GetProperty<T>(IReadOnlyProperty<T> property)
        {
            if (property.IsApplicable == true)
            {
                return property.Value;
            }
            else
            {
                return default;
            }
        }

        /// <summary>
        /// Safely retrieves the value from a generic property if applicable, otherwise returns default.
        /// </summary>
        /// <typeparam name="T">The type of the property value.</typeparam>
        /// <param name="property">The property to check.</param>
        /// <returns>The value if applicable, otherwise default(T).</returns>
        public static T? GetProperty<T>(IProperty<T> property)
        {
            if (property.IsApplicable == true)
            {
                return property.Value;
            }
            else
            {
                return default;
            }
        }

        /// <summary>
        /// Safely retrieves a boolean value from a read-only property if applicable, otherwise returns null.
        /// </summary>
        /// <param name="property">The boolean property to check.</param>
        /// <returns>The boolean value if applicable, otherwise null.</returns>
        public static bool? GetProperty(IReadOnlyProperty<bool> property)
        {
            if (property.IsApplicable == true)
            {
                return property.Value;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Safely retrieves a boolean value from a property if applicable, otherwise returns null.
        /// </summary>
        /// <param name="property">The boolean property to check.</param>
        /// <returns>The boolean value if applicable, otherwise null.</returns>
        public static bool? GetProperty(IProperty<bool> property)
        {
            if (property.IsApplicable == true)
            {
                return property.Value;
            }
            else
            {
                return null;
            }
        }
    }
}
