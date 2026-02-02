using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSD.API.Remoting.Loading;

namespace TeklaResultsInterrogator.Core
{
    /// <summary>
    /// Holds force and displacement data for a specific point along a member span.
    /// </summary>
    public class PointSpanInfo
    {
        /// <summary>Name of the load case/combination.</summary>
        public string LoadName { get; }
        /// <summary>Position along the span (ft).</summary>
        public double Position { get; set; }
        /// <summary>Major Axis Shear.</summary>
        public double ShearMajor { get; set; }
        /// <summary>Minor Axis Shear.</summary>
        public double ShearMinor { get; set; }
        /// <summary>Major Axis Moment.</summary>
        public double MomentMajor { get; set; }
        /// <summary>Minor Axis Moment.</summary>
        public double MomentMinor { get; set; }
        /// <summary>Axial Force.</summary>
        public double AxialForce { get; set; }
        /// <summary>Torsion.</summary>
        public double Torsion { get; set; }
        /// <summary>Major Axis Deflection.</summary>
        public double DeflectionMajor { get; set; }
        /// <summary>Minor Axis Deflection.</summary>
        public double DeflectionMinor { get; set; }
        /// <summary>Major Axis Displacement.</summary>
        public double DisplacementMajor { get; set; }
        /// <summary>Minor Axis Displacement.</summary>
        public double DisplacementMinor { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PointSpanInfo"/> class.
        /// </summary>
        /// <param name="loadingCase">Loading case source.</param>
        /// <param name="position">Position along the span.</param>
        /// <param name="shearMajor">Shear Major.</param>
        /// <param name="shearMinor">Shear Minor.</param>
        /// <param name="momentMajor">Moment Major.</param>
        /// <param name="momentMinor">Moment Minor.</param>
        /// <param name="axialForce">Axial Force.</param>
        /// <param name="torsion">Torsion.</param>
        /// <param name="deflectionMajor">Deflection Major.</param>
        /// <param name="deflectionMinor">Deflection Minor.</param>
        /// <param name="displacementMajor">Displacement Major.</param>
        /// <param name="displacementMinor">Displacement Minor.</param>
        public PointSpanInfo(ILoadingCase loadingCase, double position, double shearMajor, double shearMinor, double momentMajor, double momentMinor, double axialForce, double torsion, double deflectionMajor, double deflectionMinor, double displacementMajor, double displacementMinor)
        {
            LoadName = loadingCase.Name.Replace(',', '`');
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
