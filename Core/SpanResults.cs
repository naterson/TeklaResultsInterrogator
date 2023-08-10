﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TeklaResultsInterrogator.Core;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Solver;
using TSD.API.Remoting.Structure;

using MathNet.Numerics;

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

        private LoadingValueOptions ShearMajorValueOption { get; set; }
        private LoadingValueOptions ShearMinorValueOption { get; set; }
        private LoadingValueOptions MomentMajorValueOption { get; set; }
        private LoadingValueOptions MomentMinorValueOption { get; set; }
        private LoadingValueOptions AxialValueOption { get; set; }
        private LoadingValueOptions TorsionValueOption { get; set; }
        private LoadingValueOptions DeflectionMajorValueOption { get; set; }
        private LoadingValueOptions DeflectionMinorValueOption { get; set; }
        private LoadingValueOptions DisplacementMajorValueOption { get; set; }
        private LoadingValueOptions DisplacementMinorValueOption { get; set; }

        public SpanResults(IMemberSpan span, int subdivisions, ILoadingCase loading, bool reduced, AnalysisType analysisType, IMember parentMember)
        {
            Span = span;
            Name = Span.Name;
            Length = Span.Length.Value;  // Note this is in base units [mm]
            Reduced = reduced;
            Subdivisions = subdivisions;
            Loading = loading;
            AnalysisType = analysisType;
            ParentMember = parentMember;

            ShearMajorValueOption = LoadingValueOptions.StaticValue(LoadingValueType.Force, LoadingDirection.Major, Reduced);
            ShearMinorValueOption = LoadingValueOptions.StaticValue(LoadingValueType.Force, LoadingDirection.Minor, Reduced);
            MomentMajorValueOption = LoadingValueOptions.StaticValue(LoadingValueType.Moment, LoadingDirection.Major, Reduced);
            MomentMinorValueOption = LoadingValueOptions.StaticValue(LoadingValueType.Moment, LoadingDirection.Minor, Reduced);
            AxialValueOption = LoadingValueOptions.StaticValue(LoadingValueType.Force, LoadingDirection.Axial, Reduced);
            TorsionValueOption = LoadingValueOptions.StaticValue(LoadingValueType.Moment, LoadingDirection.Axial, Reduced);
            DeflectionMajorValueOption = LoadingValueOptions.StaticValue(LoadingValueType.Deflection, LoadingDirection.Major, Reduced);
            DeflectionMinorValueOption = LoadingValueOptions.StaticValue(LoadingValueType.Deflection, LoadingDirection.Minor, Reduced);
            DisplacementMajorValueOption = LoadingValueOptions.StaticValue(LoadingValueType.Displacement, LoadingDirection.Major, Reduced);
            DisplacementMinorValueOption = LoadingValueOptions.StaticValue(LoadingValueType.Displacement, LoadingDirection.Minor, Reduced);
        }

        public async Task<MaxSpanInfo> GetMaxima()
        {
            // Get Loading
            IMemberLoading memberLoading = await ParentMember.GetLoadingAsync(Loading.Id, AnalysisType, LoadingResultType.Base);

            // Calculate maxima
            MaxSpanInfoData shearMajor = await CalculateMaximum(memberLoading, ShearMajorValueOption);
            MaxSpanInfoData shearMinor = await CalculateMaximum(memberLoading, ShearMinorValueOption);
            MaxSpanInfoData momentMajor = await CalculateMaximum(memberLoading, MomentMajorValueOption);
            MaxSpanInfoData momentMinor = await CalculateMaximum(memberLoading, MomentMinorValueOption);
            MaxSpanInfoData axialForce = await CalculateMaximum(memberLoading, AxialValueOption);
            MaxSpanInfoData torsion = await CalculateMaximum(memberLoading, TorsionValueOption);
            MaxSpanInfoData deflectionMajor = await CalculateMaximum(memberLoading, DeflectionMajorValueOption);
            MaxSpanInfoData deflectionMinor = await CalculateMaximum(memberLoading, DeflectionMinorValueOption);
            MaxSpanInfoData displacementMajor = await CalculateMaximum(memberLoading, DisplacementMajorValueOption);
            MaxSpanInfoData displacementMinor = await CalculateMaximum(memberLoading, DisplacementMinorValueOption);

            // Instantiate and return MaxSpanInfo object
            return new MaxSpanInfo(shearMajor, shearMinor, momentMajor, momentMinor, axialForce, torsion, deflectionMajor, deflectionMinor, displacementMajor, displacementMinor);
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

            // Get Values in base units
            IEnumerable<ILoadingValue> values = await loading.GetValueAsync(option, positions);
            values = values.OrderByDescending(lv => lv.Value);
            double maxValue = 0;
            double maxPosition = 0;
            double minValue = 0;
            double minPosition = 0;

            // Convert units
            double posCon = 0.00328084; // Converting from [mm] to [ft]
            double valCon = ConversionFactor(option.Type);

            if (values.Any())
            {
                maxValue = values.First().Value * valCon;
                maxPosition = values.First().Position * posCon;
                minValue = values.Last().Value * valCon;
                minPosition = values.Last().Position * posCon;
            }
            
            return new MaxSpanInfoData(maxValue, maxPosition, minValue, minPosition);
        }

        private double ConversionFactor(LoadingValueType loadingValueType)
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
                    BaseInterrogator.FancyWriteLine("Warning: units not converted from base units. Refer to Tekla Structural Designer API documentation for default units.", BaseInterrogator.TextColor.Warning);
                    break;
            }
            return valCon;
        }

        public async Task<List<PointSpanInfo>> GetStations()
        {
            // Calculate station positions
            IEnumerable<double> positions = Generate.LinearSpaced(Subdivisions, 0, Length);

            // Get Loading
            IMemberLoading memberLoading = await ParentMember.GetLoadingAsync(Loading.Id, AnalysisType, LoadingResultType.Base);

            //Instantiate output list
            List<PointSpanInfo> stationData = new List<PointSpanInfo>();

            foreach(double position in positions)
            {

                // Calculate values
                double shearMajor = await GetLoadingValues(memberLoading, ShearMajorValueOption, position);
                double shearMinor = await GetLoadingValues(memberLoading, ShearMinorValueOption, position);
                double momentMajor = await GetLoadingValues(memberLoading, MomentMajorValueOption, position);
                double momentMinor = await GetLoadingValues(memberLoading, MomentMinorValueOption, position);
                double axialForce = await GetLoadingValues(memberLoading, AxialValueOption, position);
                double torsion = await GetLoadingValues(memberLoading, TorsionValueOption, position);
                double deflectionMajor = await GetLoadingValues(memberLoading, DeflectionMajorValueOption, position);
                double deflectionMinor = await GetLoadingValues(memberLoading, DeflectionMinorValueOption, position);
                double displacementMajor = await GetLoadingValues(memberLoading, DisplacementMajorValueOption, position);
                double displacementMinor = await GetLoadingValues(memberLoading, DisplacementMinorValueOption, position);

                double positionFt = position * 0.00328084; // Converting from [mm] to [ft]
                PointSpanInfo stationInfo = new PointSpanInfo(positionFt, shearMajor, shearMinor, momentMajor, momentMinor, axialForce, torsion, deflectionMajor, deflectionMinor, displacementMajor, displacementMinor);

                stationData.Add(stationInfo);
            }

            return stationData;
        }

        private async Task<double> GetLoadingValues(IMemberLoading loading, LoadingValueOptions option, double position)
        {
            // Get Loading
            IEnumerable<ILoadingValue> values = await loading.GetValueAsync(option, Span.Index, position);
            values = values.OrderByDescending(lv => lv.Value);

            // Convert units
            double valCon = ConversionFactor(option.Type);

            double value = 0;
            if (values.Any())
            {
                value = values.First().Value * valCon;
            }

            return value;
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
        public MaxSpanInfoData DisplacementMajor { get; set; }
        public MaxSpanInfoData DisplacementMinor { get; set; }

        public MaxSpanInfo(MaxSpanInfoData shearMajor, MaxSpanInfoData shearMinor, MaxSpanInfoData momentMajor, MaxSpanInfoData momentMinor, MaxSpanInfoData axialForce, MaxSpanInfoData torsion, MaxSpanInfoData deflectionMajor, MaxSpanInfoData deflectionMinor, MaxSpanInfoData displacementMajor, MaxSpanInfoData displacementMinor)
        {
            ShearMajor = shearMajor;
            ShearMinor = shearMinor;
            MomentMajor = momentMajor;
            MomentMinor = momentMinor;
            AxialForce = axialForce;
            Torsion = torsion;
            DeflectionMajor = deflectionMajor;
            DeflectionMinor = deflectionMinor;
            DisplacementMajor = displacementMajor;
            DisplacementMinor = displacementMinor;
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

    public class PointSpanInfo
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
        public double DisplacementMajor { get; set; }
        public double DisplacementMinor { get; set; }

        public PointSpanInfo(double position, double shearMajor, double shearMinor, double momentMajor, double momentMinor, double axialForce, double torsion, double deflectionMajor, double deflectionMinor, double displacementMajor, double displacementMinor)
        {
            Position = position;
            ShearMajor = shearMajor;
            ShearMinor = shearMinor;
            MomentMajor = momentMajor;
            MomentMinor = momentMinor;
            AxialForce = axialForce;
            Torsion = torsion;
            DeflectionMajor = deflectionMajor;
            DeflectionMinor = deflectionMinor;
            DisplacementMajor = displacementMajor;
            DisplacementMinor = displacementMinor;
        }
    }
}
