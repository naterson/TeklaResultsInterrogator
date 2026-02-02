using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSD.API.Remoting.Loading;

namespace TeklaResultsInterrogator.Utils
{
    public static partial class ConsoleUtils
    {
        /// <summary>
        /// Gets the conversion factor for a given loading value type (to Imperial units).
        /// </summary>
        /// <param name="loadingValueType">The value type to convert (Force, Moment, etc.).</param>
        /// <returns>The conversion factor as a double.</returns>
        public static double ConversionFactor(LoadingValueType loadingValueType)
        {
            double valCon = 1;
            switch (loadingValueType)
            {
                case LoadingValueType.Force:
                    valCon = 0.0002248089; // Converting from [N] to [k]
                    break;
                case LoadingValueType.Moment:
                    valCon = 0.000000737562149277; // Converting from [N-mm] to [k-ft]
                    break;
                case LoadingValueType.Displacement:
                    valCon = 0.0393701; // Converting from [mm] to [in]
                    break;
                case LoadingValueType.Deflection:
                    valCon = 0.0393701; // Converting from [mm] to [in]
                    break;
                default:
                    FancyWriteLine("Warning: units not converted from base units. Refer to Tekla Structural Designer API documentation for default units.", TextColor.Warning);
                    break;
            }
            return valCon;
        }

        /// <summary>
        /// Converts Newtons [N] to Kips [k].
        /// </summary>
        /// <param name="newton">Value in Newtons.</param>
        /// <returns>Value in Kips.</returns>
        public static double ToK(double newton)
        {
            return newton * 0.0002248089;
        }

        /// <summary>
        /// Converts Newton-millimeters [N-mm] to Kip-feet [k-ft].
        /// </summary>
        /// <param name="newtonmillimeters">Value in Newton-millimeters.</param>
        /// <returns>Value in Kip-feet.</returns>
        public static double ToKFt(double newtonmillimeters)
        {
            return newtonmillimeters * 0.000000737562149277;
        }

        /// <summary>
        /// Converts millimeters [mm] to inches [in].
        /// </summary>
        /// <param name="mm">Value in millimeters.</param>
        /// <returns>Value in inches.</returns>
        public static double MmToIn(double mm)
        {
            return mm * 0.0393701;
        }

        /// <summary>
        /// Converts square millimeters [mm²] to square inches [in²].
        /// </summary>
        /// <param name="mm2">Value in square millimeters.</param>
        /// <returns>Value in square inches.</returns>
        public static double MmSqToInSq(double mm2)
        {
            return mm2 * 0.00155;
        }

        /// <summary>
        /// Converts millimeters [mm] to feet [ft].
        /// </summary>
        /// <param name="mm">Value in millimeters.</param>
        /// <returns>Value in feet.</returns>
        public static double MmToFt(double mm)
        {
            return mm * 0.00328084;
        }

        /// <summary>
        /// Converts radians to degrees.
        /// </summary>
        /// <param name="radians">Value in radians.</param>
        /// <returns>Value in degrees.</returns>
        public static double RadToDeg(double radians)
        {
            return radians * (180.0 / Math.PI);
        }
    }
}
