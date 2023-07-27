using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TeklaResultsInterrogator.Core;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Solver;
using TSD.API.Remoting.Structure;

namespace TeklaResultsInterrogator.Core
{
    public class SpanResults
    {
        public string Name { get; set; }
        public double Length { get; set; }
        public bool Reduced { get; set; }
        public int Subdivisions { get; set; }
        public ILoadingCase Loading { get; set; }
        public AnalysisType AnalysisType { get; set; }
        public IMemberSpan Span { get; set; }
        public IMember ParentMember { get; set; }


        public SpanResults(IMemberSpan span, int subdivisions, ILoadingCase loading, bool reduced, AnalysisType analysisType, IMember parentMember)
        {
            Span = span;
            Name = Span.Name;
            Length = Span.Length.Value * 0.00328084;
            Reduced = reduced;
            Subdivisions = subdivisions;
            Loading = loading;
            AnalysisType = analysisType;
            ParentMember = parentMember;
        }

        public async Task<MaxSpanInfo> GetMaxima()
        {
            IMemberLoading memberLoading = await ParentMember.GetLoadingAsync(Loading.Id, AnalysisType, LoadingResultType.Base);

            LoadingValueOptions shearMajorValueOption = LoadingValueOptions.StaticValue(LoadingValueType.Force, LoadingDirection.Major, Reduced);
            LoadingValueOptions shearMinorValueOption = LoadingValueOptions.StaticValue(LoadingValueType.Force, LoadingDirection.Minor, Reduced);
            LoadingValueOptions momentMajorValueOption = LoadingValueOptions.StaticValue(LoadingValueType.Moment, LoadingDirection.Major, Reduced);
            LoadingValueOptions momentMinorValueOption = LoadingValueOptions.StaticValue(LoadingValueType.Moment, LoadingDirection.Minor, Reduced);
            LoadingValueOptions axialValueOption = LoadingValueOptions.StaticValue(LoadingValueType.Force, LoadingDirection.Axial, Reduced);
            LoadingValueOptions torsionValueOption = LoadingValueOptions.StaticValue(LoadingValueType.Moment, LoadingDirection.Axial, Reduced);
            LoadingValueOptions deflectionMajorValueOption = LoadingValueOptions.StaticValue(LoadingValueType.Displacement, LoadingDirection.Major, Reduced);
            LoadingValueOptions deflectionMinorValueOption = LoadingValueOptions.StaticValue(LoadingValueType.Displacement, LoadingDirection.Minor, Reduced);

            MaxSpanInfoData shearMajor = await CalculateMaximum(memberLoading, shearMajorValueOption);
            MaxSpanInfoData shearMinor = await CalculateMaximum(memberLoading, shearMinorValueOption);
            MaxSpanInfoData momentMajor = await CalculateMaximum(memberLoading, momentMajorValueOption);
            MaxSpanInfoData momentMinor = await CalculateMaximum(memberLoading, momentMinorValueOption);
            MaxSpanInfoData axialForce = await CalculateMaximum(memberLoading, axialValueOption);
            MaxSpanInfoData torsion = await CalculateMaximum(memberLoading, torsionValueOption);
            MaxSpanInfoData deflectionMajor = await CalculateMaximum(memberLoading, deflectionMajorValueOption);
            MaxSpanInfoData deflectionMinor = await CalculateMaximum(memberLoading, deflectionMinorValueOption);

            return new MaxSpanInfo(shearMajor, shearMinor, momentMajor, momentMinor, axialForce, torsion, deflectionMajor, deflectionMinor);
        }

        private async Task<MaxSpanInfoData> CalculateMaximum(IMemberLoading loading, LoadingValueOptions option)
        {
            // Get Points of Interest
            List<IPointOfInterest> points = new List<IPointOfInterest>();
            points.AddRange((await loading.GetPointsOfInterest(option, PointOfInterestType.Maximum)).ToList());
            points.AddRange((await loading.GetPointsOfInterest(option, PointOfInterestType.Minimum)).ToList());
            points = points.Where(p => p.SpanIndex == Span.Index).ToList();

            // Get Positions
            List<ValueTuple<int, double>> positions = new List<ValueTuple<int, double>>();
            foreach (IPointOfInterest point in points)
            {
                ValueTuple<int, double> position = new ValueTuple<int, double>(Span.Index, point.Position);
                positions.Add(position);
            }

            // Get Values
            IEnumerable<ILoadingValue> values = await loading.GetValueAsync(option, positions);
            values.OrderByDescending(lv => lv.Value).ToList();
            double maxValue = 0;
            double maxPosition = 0;
            double minValue = 0;
            double minPosition = 0;

            if (values.Any())
            {
                maxValue = values.First().Value;  // need to convert units of values and positions
                maxPosition = values.First().Position;
                minValue = values.Last().Value;
                minPosition = values.Last().Position;
            }
            
            return new MaxSpanInfoData(maxValue, maxPosition, minValue, minPosition);
        }
    }

    public class MaxSpanInfo
    {
        public MaxSpanInfoData ShearMajor { get; set; }
        public MaxSpanInfoData ShearMinor { get; set; }
        public MaxSpanInfoData MomentMajor { get; set; }
        public MaxSpanInfoData MomentMinor { get; set; }
        public MaxSpanInfoData AxialForce { get; set; }
        public MaxSpanInfoData Torsion { get; set; }
        public MaxSpanInfoData DeflectionMajor { get; set; }
        public MaxSpanInfoData DeflectionMinor { get; set; }

        public MaxSpanInfo(MaxSpanInfoData shearMajor, MaxSpanInfoData shearMinor, MaxSpanInfoData momentMajor, MaxSpanInfoData momentMinor, MaxSpanInfoData axialForce, MaxSpanInfoData torsion, MaxSpanInfoData deflectionMajor, MaxSpanInfoData deflectionMinor)
        {
            ShearMajor = shearMajor;
            ShearMinor = shearMinor;
            MomentMajor = momentMajor;
            MomentMinor = momentMinor;
            AxialForce = axialForce;
            Torsion = torsion;
            DeflectionMajor = deflectionMajor;
            DeflectionMinor = deflectionMinor;
        }
    }

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
    }

    internal class PointSpanInfo
    {
        public double Position { get; set; }
        public double ShearMajor { get; set; }
        public double ShearMinor { get; set; }
        public double MomentMajor { get; set; }
        public double MomentMinor { get; set; }
        public double AxialForce { get; set; }
        public double Torsion { get; set; }
        public double DeflectionMajor { get; set; }
        public double DeflectionMinor { get; set; }
    }
}
