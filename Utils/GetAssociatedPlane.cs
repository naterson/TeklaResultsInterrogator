using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSD.API.Remoting.Structure;

namespace TeklaResultsInterrogator.Utils
{
    public static partial class ConsoleUtils
    {
        /// <summary>
        /// Attempts to find the associated horizontal construction plane (Level) for a given node index.
        /// </summary>
        /// <param name="nodeIdx">The index of the node to check.</param>
        /// <param name="points">List of construction points to search within.</param>
        /// <param name="levels">List of available levels.</param>
        /// <param name="exactOnly">If true, only returns exact matches; otherwise finds closest level.</param>
        /// <returns>An <see cref="AssociatedPlaneWrapper"/> containing the level name and object.</returns>
        public static AssociatedPlaneWrapper GetAssociatedLevel(int nodeIdx, List<IConstructionPoint> points, List<IHorizontalConstructionPlane> levels, bool exactOnly = false)
        {
            IList<IConstructionPoint> associatedPoints = points.Where(p => p.Index == nodeIdx).ToList();
            IList<int> associatedPlaneIds = associatedPoints
                .Where(p => p.PlaneInfo.Value.Type == TSD.API.Remoting.Common.EntityType.HorizontalConstructionPlane)
                .Select(p => p.PlaneInfo.Value.Index).ToList();

            string associatedLevelName;
            IHorizontalConstructionPlane? associatedLevel;
            bool exactMatch;

            if (associatedPlaneIds.Any())
            {
                associatedLevel = levels.Where(l => l.Index == associatedPlaneIds.First()).First();
                associatedLevelName = associatedLevel.Name;
                exactMatch = true;
            }
            else
            {
                if (exactOnly)
                {
                    associatedLevel = null;
                    associatedLevelName = "No exact level";
                    exactMatch = false;
                }
                else
                {
                    double z = associatedPoints.First().Coordinates.Value.Z;
                    associatedLevel = levels.MinBy(l => Math.Abs(z - l.Level.Value))!;
                    double offset = Math.Round(MmToFt(z - associatedLevel.Level.Value), 2);
                    string modifier = (offset >= 0) ? "+" : "-";
                    offset = Math.Abs(offset);
                    associatedLevelName = $"~{associatedLevel.Name} ({modifier}{offset} ft)";
                    exactMatch = false;
                }
            }

            AssociatedPlaneWrapper res = new AssociatedPlaneWrapper(associatedLevelName, associatedLevel, exactMatch);
            return res;
        }
    }

    /// <summary>
    /// Wrapper class for holding associated plane information, including exact match status.
    /// </summary>
    public class AssociatedPlaneWrapper
    {
        /// <summary>The name of the associated plane/level.</summary>
        public string Name { get; set; }
        /// <summary>The associated plane object, or null if none strictly associated.</summary>
        public IHorizontalConstructionPlane? Plane { get; set; }
        /// <summary>True if the node is exactly on the level, false if closest match.</summary>
        public bool ExactMatch { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssociatedPlaneWrapper"/> class.
        /// </summary>
        /// <param name="name">Name of the plane.</param>
        /// <param name="plane">The plane object.</param>
        /// <param name="exactMatch">Whether it is an exact match.</param>
        public AssociatedPlaneWrapper(string name, IHorizontalConstructionPlane? plane, bool exactMatch)
        {
            Name = name;
            Plane = plane;
            ExactMatch = exactMatch;
        }
    }
}
