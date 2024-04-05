using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeklaResultsInterrogator.Core
{
    public class MaxSpanInfoData
    {
        public double Position { get; set; }
        public double Value { get; set; }
        public double MaxPosition { get; set; }
        public double MaxValue { get; set; }
        public double MinPosition { get; set; }
        public double MinValue { get; set; }

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

        public MaxSpanInfoData()
        {
            Position = 0;
            Value = 0;
            MaxPosition = 0;
            MaxValue = 0;
            MinPosition = 0;
            MinValue = 0;
        }

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
