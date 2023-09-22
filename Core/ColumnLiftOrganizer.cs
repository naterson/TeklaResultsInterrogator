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

            // Get all spans of member
            // iterate through each span
            // look at fixity of span
            // bottom of span or top of previous span is pinned, add to new lift
            //
        }

        public async Task OrganizeByFixity()
        {
            IEnumerable<IMemberSpan> spans = await ParentMember.GetSpanAsync();
            spans = spans.OrderBy(s => s.Index);
            bool previousSpanTopFixed = false;
            foreach (var span in spans)
            {
                bool thisBotFixed = CheckFixity(span, StackEnd.Bottom);
                bool thisTopFixed = CheckFixity(span, StackEnd.Top);
                
                if (!thisBotFixed || !previousSpanTopFixed)
                {
                    // if bottom of this span is NOT fixed, OR if top of previous span is NOT fixed, add this span to start of new lift and name the lift list
                }
                else
                {
                    // else add this span to end of previous lift
                }

                previousSpanTopFixed = thisTopFixed;
            }

            // sort each stack in each lift by stack index
            // sort each lift in list by index of first stack in lift


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

        private enum StackEnd
        {
            Bottom,
            Top,
        }
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
