using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace TeklaResultsInterrogator.Utils
{
    /// <summary>
    /// Parser for reading Structural Analysis data from CXL xml files.
    /// </summary>
    public class CxlDataParser
    {
        /// <summary>
        /// The path to the CXL file.
        /// </summary>
        public string FilePath { get; }

        /// <summary>List of parsed members.</summary>
        public List<MemberData> Members { get; private set; } = new List<MemberData>();

        /// <summary>Dictionary of node Z coordinates (Elevation) by Node ID.</summary>
        public Dictionary<int, double> NodeZCoordinates { get; private set; } = new Dictionary<int, double>();

        /// <summary>List of parsed levels.</summary>
        public List<LevelData> Levels { get; private set; } = new List<LevelData>();

        /// <summary>List of parsed load cases.</summary>
        public List<LoadCaseData> LoadCases { get; private set; } = new List<LoadCaseData>();

        /// <summary>List of parsed base reaction forces.</summary>
        public List<BaseForceData> BaseForces { get; private set; } = new List<BaseForceData>();

        /// <summary>
        /// Initializes a new instance of the <see cref="CxlDataParser"/> class.
        /// </summary>
        /// <param name="filePath">Path to the CXL file.</param>
        public CxlDataParser(string filePath)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"CXL file not found: {filePath}");
        }

        /// <summary>
        /// Parses the CXL file properties and populates the data lists.
        /// </summary>
        public void Parse()
        {
            XDocument doc = XDocument.Load(FilePath);

            ParseNodes(doc);
            ParseLevels(doc);
            ParseMembers(doc);
            ParseLoadCases(doc);
            ParseBaseForces(doc);
        }

        private void ParseNodes(XDocument doc)
        {
            NodeZCoordinates.Clear();

            var coords = doc.Descendants("Coord");
            foreach (var c in coords)
            {
                if (int.TryParse(c.Attribute("Id")?.Value, out int nodeId))
                {
                    var zStr = c.Element("Z")?.Value;
                    if (double.TryParse(zStr, out double z))
                    {
                        NodeZCoordinates[nodeId] = z;
                    }
                }
            }
        }

        private void ParseLevels(XDocument doc)
        {
            Levels.Clear();

            var uniqueZs = NodeZCoordinates.Values.Select(z => Math.Round(z, 2)).Distinct().OrderBy(z => z).ToList();
            int idx = 0;
            foreach (var z in uniqueZs)
            {
                Levels.Add(new LevelData { Name = $"Level_{idx + 1}", Elevation = z });
                idx++;
            }
        }

        private void ParseMembers(XDocument doc)
        {
            Members.Clear();

            var members = doc.Descendants("Member");
            foreach (var m in members)
            {
                string id = m.Attribute("MemberId")?.Value ?? "";
                int startNode = int.Parse(m.Element("StartNode")?.Value ?? "0");
                int endNode = int.Parse(m.Element("EndNode")?.Value ?? "0");
                string partMark = m.Descendants("PART_MARK").FirstOrDefault()?.Value ?? "";
                string designGrp = m.Descendants("DESIGN_GRP").FirstOrDefault()?.Value ?? "";
                string sectionSize = m.Element("SectionSize")?.Value ?? "";
                string materialGrade = m.Element("MaterialGrade")?.Value ?? "";
                string memberType = m.Descendants("MBR_TYPE").FirstOrDefault()?.Value ?? "";

                Members.Add(new MemberData
                {
                    Id = id,
                    StartNode = startNode,
                    EndNode = endNode,
                    PartMark = partMark,
                    DesignGroup = designGrp,
                    SectionSize = sectionSize,
                    MaterialGrade = materialGrade,
                    MemberType = memberType
                });
            }
        }

        private void ParseLoadCases(XDocument doc)
        {
            LoadCases.Clear();

            var loadcases = doc.Descendants("Case");
            foreach (var c in loadcases)
            {
                if (int.TryParse(c.Attribute("CaseNo")?.Value, out int caseNo))
                {
                    string title = c.Element("Title")?.Value ?? "";
                    string type = c.Element("Type")?.Value ?? "";

                    LoadCases.Add(new LoadCaseData
                    {
                        CaseNumber = caseNo,
                        Title = title,
                        Type = type
                    });
                }
            }
        }

        private void ParseBaseForces(XDocument doc)
        {
            BaseForces.Clear();

            var bfBases = doc.Descendants("BFBase");
            foreach (var bfBase in bfBases)
            {
                int nodeNo = int.Parse(bfBase.Attribute("NodeNo")?.Value ?? "0");
                string mark = bfBase.Attribute("Mark")?.Value ?? "";

                var bfCases = bfBase.Descendants("BFCase");
                foreach (var bfCase in bfCases)
                {
                    int caseNo = int.Parse(bfCase.Attribute("CaseNo")?.Value ?? "0");
                    double.TryParse(bfCase.Attribute("Fv")?.Value, out double fv);
                    double.TryParse(bfCase.Attribute("Fmaj")?.Value, out double fmaj);
                    double.TryParse(bfCase.Attribute("Fmin")?.Value, out double fmin);
                    double.TryParse(bfCase.Attribute("Mmaj")?.Value, out double mmaj);
                    double.TryParse(bfCase.Attribute("Mmin")?.Value, out double mmin);

                    BaseForces.Add(new BaseForceData
                    {
                        NodeNo = nodeNo,
                        Mark = mark,
                        CaseNumber = caseNo,
                        ShearMajor = fmaj,
                        ShearMinor = fmin,
                        MomentMajor = mmaj,
                        MomentMinor = mmin,
                        AxialForce = fv,
                        Torsion = 0
                    });
                }
            }
        }
    }

    /// <summary>
    /// Represents member data extracted from CXL.
    /// </summary>
    public class MemberData
    {
        /// <summary>Member GUID/ID.</summary>
        public string Id { get; set; } = "";
        /// <summary>Start Node Index.</summary>
        public int StartNode { get; set; }
        /// <summary>End Node Index.</summary>
        public int EndNode { get; set; }
        /// <summary>Part Mark.</summary>
        public string PartMark { get; set; } = "";
        /// <summary>Design Group Name.</summary>
        public string DesignGroup { get; set; } = "";
        /// <summary>Section Size.</summary>
        public string SectionSize { get; set; } = "";
        /// <summary>Material Grade.</summary>
        public string MaterialGrade { get; set; } = "";
        /// <summary>Member Type.</summary>
        public string MemberType { get; set; } = "";
    }

    /// <summary>
    /// Represents level data extracted from CXL.
    /// </summary>
    public class LevelData
    {
        /// <summary>Level Name.</summary>
        public string Name { get; set; } = "";
        /// <summary>Level Elevation.</summary>
        public double Elevation { get; set; }
    }

    /// <summary>
    /// Represents load case data extracted from CXL.
    /// </summary>
    public class LoadCaseData
    {
        /// <summary>Case Number.</summary>
        public int CaseNumber { get; set; }
        /// <summary>Case Title.</summary>
        public string Title { get; set; } = "";
        /// <summary>Case Type.</summary>
        public string Type { get; set; } = "";
    }

    /// <summary>
    /// Represents base force data extracted from CXL.
    /// </summary>
    public class BaseForceData
    {
        /// <summary>Node Number.</summary>
        public int NodeNo { get; set; }
        /// <summary>Mark.</summary>
        public string Mark { get; set; } = "";
        /// <summary>Load Case Number.</summary>
        public int CaseNumber { get; set; }
        /// <summary>Shear Major.</summary>
        public double ShearMajor { get; set; }
        /// <summary>Shear Minor.</summary>
        public double ShearMinor { get; set; }
        /// <summary>Moment Major.</summary>
        public double MomentMajor { get; set; }
        /// <summary>Moment Minor.</summary>
        public double MomentMinor { get; set; }
        /// <summary>Axial Force.</summary>
        public double AxialForce { get; set; }
        /// <summary>Torsion.</summary>
        public double Torsion { get; set; }
    }
}
