using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TeklaResultsInterrogator.Core;
using TSD.API.Remoting.Loading;
using TSD.API.Remoting.Solver;
using TSD.API.Remoting.Structure;

namespace TeklaResultsInterrogator.Core
{
    public class ColumnLifts
    {
        public IMember ParentMember { get; set; }
        public List<NamedList<IMemberSpan>> Lifts { get; set; }

        public ColumnLifts(IMember parentMember)
        {
            ParentMember = parentMember;
            Lifts = new List<NamedList<IMemberSpan>>();
        }

        public void OrganizeByFixity()
        {
            IEnumerable<IMemberSpan> spans = ParentMember.GetSpanAsync().Result;
            spans = spans.OrderBy(s => s.Index);
            bool previousSpanTopFixed = false;
            foreach (var span in spans)
            {
                bool thisBotFixed = CheckFixity(span, StackEnd.Bottom);
                bool thisTopFixed = CheckFixity(span, StackEnd.Top);
                
                if (!thisBotFixed || !previousSpanTopFixed)
                {
                    string liftName = $"L{Lifts.Count + 1}";
                    NamedList<IMemberSpan> lift = new NamedList<IMemberSpan>(liftName);
                    lift.Add(span);
                    Lifts.Add(lift);
                }
                else
                {
                    Lifts.Last().Add(span);
                }

                previousSpanTopFixed = thisTopFixed;
            }

            return;
        }

        private bool CheckFixity(IMemberSpan span, StackEnd end)
        {
            bool isFixed = false;
            ISpanReleases? releases = null;

            switch (end)
            {
                case StackEnd.Bottom:
                    releases = span.StartReleases.Value;
                    break;
                case StackEnd.Top:
                    releases = span.EndReleases.Value;
                    break;
                default:
                    break;
            }

            if (releases != null)
            {
                string releaseValueString = releases.DegreeOfFreedom.Value.ToString();
                if (releaseValueString.Contains("Mx") && releaseValueString.Contains("My") && releaseValueString.Contains("Mz"))
                {
                    isFixed = true;
                }
            }

            return isFixed;
        }
    }

    public enum StackEnd
    {
        Bottom,
        Top,
    }

    public class NamedList<T>
    {
        public string Name { get; set; }
        public List<T> Values { get; set; }

        public NamedList(string name)
        {
            Name = name;
            Values = new List<T>();
        }

        public void Add(T value)
        {
            Values.Add(value);
        }
    }
}
