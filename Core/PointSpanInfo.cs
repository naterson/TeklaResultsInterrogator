using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeklaResultsInterrogator.Core
{
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
