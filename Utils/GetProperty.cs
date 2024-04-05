using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSD.API.Remoting.Common.Properties;

namespace TeklaResultsInterrogator.Utils
{
    public static partial class Utils
    {
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
