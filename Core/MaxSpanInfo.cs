using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSD.API.Remoting.Loading;

namespace TeklaResultsInterrogator.Core
{
    /// <summary>
    ///  Encapsulates calculated maximum values for a span across various force and displacement types.
    /// </summary>
    public class MaxSpanInfo
    {
        /// <summary>Name of the loading condition.</summary>
        public string LoadName { get; }
        /// <summary>Maximum Major Axis Shear data.</summary>
        public MaxSpanInfoData ShearMajor { get; set; }
        /// <summary>Maximum Minor Axis Shear data.</summary>
        public MaxSpanInfoData ShearMinor { get; set; }
        /// <summary>Maximum Major Axis Moment data.</summary>
        public MaxSpanInfoData MomentMajor { get; set; }
        /// <summary>Maximum Minor Axis Moment data.</summary>
        public MaxSpanInfoData MomentMinor { get; set; }
        /// <summary>Maximum Axial Force data.</summary>
        public MaxSpanInfoData AxialForce { get; set; }
        /// <summary>Maximum Torsion data.</summary>
        public MaxSpanInfoData Torsion { get; set; }
        /// <summary>Maximum Major Axis Deflection data.</summary>
        public MaxSpanInfoData DeflectionMajor { get; set; }
        /// <summary>Maximum Minor Axis Deflection data.</summary>
        public MaxSpanInfoData DeflectionMinor { get; set; }
        /// <summary>Maximum Major Axis Displacement data.</summary>
        public MaxSpanInfoData DisplacementMajor { get; set; }
        /// <summary>Maximum Minor Axis Displacement data.</summary>
        public MaxSpanInfoData DisplacementMinor { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MaxSpanInfo"/> class with specific data.
        /// </summary>
        /// <param name="loadingCase">The loading case.</param>
        /// <param name="shearMajor">Shear Major data.</param>
        /// <param name="shearMinor">Shear Minor data.</param>
        /// <param name="momentMajor">Moment Major data.</param>
        /// <param name="momentMinor">Moment Minor data.</param>
        /// <param name="axialForce">Axial Force data.</param>
        /// <param name="torsion">Torsion data.</param>
        /// <param name="deflectionMajor">Deflection Major data.</param>
        /// <param name="deflectionMinor">Deflection Minor data.</param>
        /// <param name="displacementMajor">Displacement Major data.</param>
        /// <param name="displacementMinor">Displacement Minor data.</param>
        public MaxSpanInfo(ILoadingCase loadingCase, MaxSpanInfoData shearMajor, MaxSpanInfoData shearMinor, MaxSpanInfoData momentMajor, MaxSpanInfoData momentMinor, MaxSpanInfoData axialForce, MaxSpanInfoData torsion, MaxSpanInfoData deflectionMajor, MaxSpanInfoData deflectionMinor, MaxSpanInfoData displacementMajor, MaxSpanInfoData displacementMinor)
        {
            LoadName = loadingCase.Name.Replace(',', '`');
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

        /// <summary>
        /// Initializes a new empty instance of the <see cref="MaxSpanInfo"/> class for a loading case.
        /// </summary>
        /// <param name="loadingCase">The loading case.</param>
        public MaxSpanInfo(ILoadingCase loadingCase)
        {
            LoadName = loadingCase.Name.Replace(',', '`');
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

        /// <summary>
        /// Updates the current instance by enveloping it with another <see cref="MaxSpanInfo"/> object.
        /// </summary>
        /// <param name="other">The other info object to compare against.</param>
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
