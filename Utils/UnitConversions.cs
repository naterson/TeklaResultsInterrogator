using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSD.API.Remoting.Loading;

namespace TeklaResultsInterrogator.Utils
{
    public static partial class Utils
    {
        public static double ConversionFactor(LoadingValueType loadingValueType)
        {
            double valCon = 1;
            switch (loadingValueType)
            {
                case LoadingValueType.Force:
                    valCon = 0.0002248089; // Converting from [N] to [k]
                    break;
                case LoadingValueType.Moment:
                    valCon = 0.000000737562121169657; // Converting from [N-mm] to [k-ft]
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

        public static double ToK(double newton)
        {
            return newton * 0.0002248089;
        }

        public static double ToKFt(double newtonmillimeters)
        {
            return newtonmillimeters * 0.000000737562121169657;
        }

        public static double mm2ft(double mm)
        {
            return mm * 0.00328084;
        }
    }
}
