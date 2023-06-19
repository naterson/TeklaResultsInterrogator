using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeklaResultsInterrogator.Core
{
    internal class BeamForces
    {
        public ForceInfo MomentMajor { get; set; }
        public ForceInfo MomentMinor { get; set; }
        public ForceInfo Torsion { get; set; }
        public ForceInfo ShearMajor { get; set; }
        public ForceInfo ShearMinor { get; set; }
        public ForceInfo Axial { get; set; }

        public BeamForces()
        {
            MomentMajor = new ForceInfo();
            MomentMinor = new ForceInfo();
            Torsion = new ForceInfo();
            ShearMajor = new ForceInfo();
            ShearMinor = new ForceInfo();
            Axial = new ForceInfo();
        }
    }

    internal class ForceInfo
    {
        public double MaxValue { get; set; }
        public double MinValue { get; set; }
        public double AbsValue { get; set; }
        public string? MaxCritCase { get; set; }
        public string? MinCritCase { get; set; }
        public string? AbsCritCase { get; set; }
    }

}
