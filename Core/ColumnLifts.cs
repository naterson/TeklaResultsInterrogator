using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeklaResultsInterrogator.Utils;
using TSD.API.Remoting.Structure;

/// <summary>
/// Represents the collection of lifts for a column member, organizing spans based on splices.
/// </summary>
public class ColumnLifts
{
    /// <summary>
    /// The parent member (column) this object represents.
    /// </summary>
    public IMember ParentMember { get; }

    /// <summary>
    /// The list of organized lifts, where each lift contains a list of member spans.
    /// </summary>
    public List<NamedList<IMemberSpan>> Lifts { get; } = new();

    /// <summary>
    /// Indicates if the column has any splices.
    /// </summary>
    public bool HasSplice { get; private set; }

    /// <summary>
    /// Stores splice information per span index. Key is Span Index, Value is (HasSplice, SpliceOffset).
    /// </summary>
    public Dictionary<int, (bool HasSplice, double SpliceOffset)> SpanSpliceInfo { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ColumnLifts"/> class.
    /// </summary>
    /// <param name="parentMember">The parent column member.</param>
    public ColumnLifts(IMember parentMember)
    {
        ParentMember = parentMember;
    }

    /// <summary>
    /// Organizing the column spans into lifts based on splice locations.
    /// This populates the <see cref="Lifts"/> property.
    /// </summary>
    public async Task OrganizeBySpliceAsync()
    {
        var spans = (await ParentMember.GetSpanAsync())
            .OrderBy(s => s.Index)
            .ToList();

        HasSplice = false;
        SpanSpliceInfo.Clear();

        // Collect splice info and track which spans have splices
        var spliceIndices = new HashSet<int>();

        foreach (var span in spans)
        {
            bool spanHasSplice = false;
            double spliceOffset = 0;

            if (span.Data?.Value is ISteelColumnStackData stackData)
            {
                if (stackData.HasSplice.IsApplicable)
                    spanHasSplice = stackData.HasSplice.Value;

                if (stackData.SpliceOffset.IsApplicable)
                    spliceOffset = stackData.SpliceOffset.Value;
            }

            SpanSpliceInfo[span.Index] = (spanHasSplice, spliceOffset);
            if (spanHasSplice)
            {
                HasSplice = true;
                spliceIndices.Add(span.Index);
            }
        }

        Lifts.Clear();

        if (!HasSplice)
        {
            // No splices → single lift with all spans
            var singleLift = new NamedList<IMemberSpan>("L1");
            foreach (var span in spans)
                singleLift.Add(span);
            Lifts.Add(singleLift);
            return;
        }

        // Sort splice indices to process in order
        var sortedSplices = spliceIndices.OrderBy(i => i).ToList();

        // We will create lifts starting at:
        // - first span index
        // - every splice index (except the first if it is the very first span)
        // Each lift ends *just before* the next splice start span

        var liftStartIndices = new List<int> { spans.First().Index };
        // Add splice starts after the first span if not already first
        foreach (var spliceIdx in sortedSplices)
        {
            if (spliceIdx != spans.First().Index)
                liftStartIndices.Add(spliceIdx);
        }
        liftStartIndices = liftStartIndices.OrderBy(i => i).ToList();

        for (int i = 0; i < liftStartIndices.Count; i++)
        {
            int startIdx = liftStartIndices[i];
            int endIdxExclusive = (i + 1 < liftStartIndices.Count) ? liftStartIndices[i + 1] : spans.Last().Index + 1;

            var lift = new NamedList<IMemberSpan>($"L{i + 1}");

            // Add spans with index >= startIdx and < endIdxExclusive
            foreach (var span in spans)
            {
                if (span.Index >= startIdx && span.Index < endIdxExclusive)
                    lift.Add(span);
            }

            Lifts.Add(lift);
        }
    }

}
