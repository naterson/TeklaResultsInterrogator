using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeklaResultsInterrogator.Core
{
    /// <summary>
    /// Stores discrete maximum and minimum value data (position and magnitude).
    /// </summary>
    public class MaxSpanInfoData
    {
        /// <summary>Position of the absolute maximum value (either min or max).</summary>
        public double Position { get; set; }
        /// <summary>Absolute maximum value (either min or max).</summary>
        public double Value { get; set; }
        /// <summary>Position of the maximum (positive) value.</summary>
        public double MaxPosition { get; set; }
        /// <summary>Maximum (positive) value.</summary>
        public double MaxValue { get; set; }
        /// <summary>Position of the minimum (negative) value.</summary>
        public double MinPosition { get; set; }
        /// <summary>Minimum (negative) value.</summary>
        public double MinValue { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MaxSpanInfoData"/> class with known values.
        /// </summary>
        /// <param name="maxValue">Maximum value.</param>
        /// <param name="maxPosition">Position of maximum value.</param>
        /// <param name="minValue">Minimum value.</param>
        /// <param name="minPosition">Position of minimum value.</param>
        public MaxSpanInfoData(double maxValue, double maxPosition, double minValue, double minPosition)
        {
            MaxValue = maxValue;
            MaxPosition = maxPosition;
            MinValue = minValue;
            MinPosition = minPosition;
            if (Math.Abs(minValue) > Math.Abs(maxValue))
            {
                Value = minValue;
                Position = minPosition;
            }
            else
            {
                Value = maxValue;
                Position = maxPosition;
            }
        }

        /// <summary>
        /// Initializes a new empty instance of the <see cref="MaxSpanInfoData"/> class.
        /// </summary>
        public MaxSpanInfoData()
        {
            Position = 0;
            Value = 0;
            MaxPosition = 0;
            MaxValue = 0;
            MinPosition = 0;
            MinValue = 0;
        }

        /// <summary>
        /// Compares with another data object and updates to keep the envelope (extreme values).
        /// </summary>
        /// <param name="other">The other data object.</param>
        public void CompareAndUpdate(MaxSpanInfoData other)
        {
            // TODO: if enveloping multiple spans (such as a multi-stack column lift) the position will need to be offset
            if (other.MaxValue > MaxValue)
            {
                MaxValue = other.MaxValue;
                MaxPosition = other.MaxPosition;
            }
            if (other.MinValue < MinValue)
            {
                MinValue = other.MinValue;
                MinPosition = other.MinPosition;
            }
            if (Math.Abs(MinValue) > Math.Abs(MaxValue))
            {
                Value = MinValue;
                Position = MinPosition;
            }
            else
            {
                Value = MaxValue;
                Position = MaxPosition;
            }
        }
    }
}
