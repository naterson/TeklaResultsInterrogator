using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSD.API.Remoting.Structure;

namespace TeklaResultsInterrogator.Utils
{
    public static partial class Utils
    {
        public static AssociatedPlaneWrapper GetAssociatedLevel(int nodeIdx, List<IConstructionPoint> points, List<IHorizontalConstructionPlane> levels, bool exactOnly=false)
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
                    associatedLevel = levels.OrderBy(l => Math.Abs(z - l.Level.Value)).First();
                    double offset = Math.Round(mm2ft(z - associatedLevel.Level.Value), 2);
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

    public class AssociatedPlaneWrapper
    {
        public string Name { get; set; }
        public IHorizontalConstructionPlane? Plane { get; set; }
        public bool ExactMatch { get; set; }

        public AssociatedPlaneWrapper(string name, IHorizontalConstructionPlane? plane, bool exactMatch)
        {
            Name = name;
            Plane = plane;
            ExactMatch = exactMatch;
        }
    }
}
