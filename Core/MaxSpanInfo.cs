using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeklaResultsInterrogator.Core
{
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

        public MaxSpanInfo()
        {
            ShearMajor = new MaxSpanInfoData();
            ShearMinor = new MaxSpanInfoData();
            MomentMajor = new MaxSpanInfoData();
            MomentMinor = new MaxSpanInfoData();
            AxialForce = new MaxSpanInfoData();
            Torsion = new MaxSpanInfoData();
            DeflectionMajor = new MaxSpanInfoData();
            DeflectionMinor = new MaxSpanInfoData();
            DisplacementMajor = new MaxSpanInfoData();
            DisplacementMinor = new MaxSpanInfoData();
        }

        public void EnvelopeAndUpdate(MaxSpanInfo other)
        {
            ShearMajor.CompareAndUpdate(other.ShearMajor);
            ShearMinor.CompareAndUpdate(other.ShearMinor);
            MomentMajor.CompareAndUpdate(other.MomentMajor);
            MomentMinor.CompareAndUpdate(other.MomentMinor);
            AxialForce.CompareAndUpdate(other.AxialForce);
            Torsion.CompareAndUpdate(other.Torsion);
            DeflectionMajor.CompareAndUpdate(other.DeflectionMajor);
            DeflectionMinor.CompareAndUpdate(other.DeflectionMinor);
            DisplacementMajor.CompareAndUpdate(other.DisplacementMajor);
            DisplacementMinor.CompareAndUpdate(other.DisplacementMinor);
        }
    }
}
