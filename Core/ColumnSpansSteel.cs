using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeklaResultsInterrogator.Utils;
using TSD.API.Remoting.Structure;
using TSD.API.Remoting.UserDefinedAttributes;

namespace TeklaResultsInterrogator.Core
{
    /// <summary>
    /// Manages span organization and splice detection for steel columns.
    /// </summary>
    public class ColumnSpansSteel
    {
        /// <summary>The parent member (Column).</summary>
        public IMember ParentMember { get; }
        /// <summary>The list of spans belonging to this column.</summary>
        public List<IMemberSpan> Spans { get; private set; } = new();
        /// <summary>Indicates if the column has a splice.</summary>
        public bool HasSplice { get; private set; }

        /// <summary>
        /// Store splice info per span index for reporting or output.
        /// Key: Span Index, Value: (HasSplice, SpliceOffset).
        /// </summary>
        public Dictionary<int, (bool HasSplice, double SpliceOffset)> SpanSpliceInfo { get; } = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ColumnSpansSteel"/> class.
        /// </summary>
        /// <param name="parentMember">The parent member.</param>
        public ColumnSpansSteel(IMember parentMember)
        {
            ParentMember = parentMember;
        }

        /// <summary>
        /// Organizes spans by detecting splices via TSD data, naming conventions, or span index gaps.
        /// </summary>
        public async Task OrganizeSpansAsync()
        {
            var spans = (await ParentMember.GetSpanAsync())
                .OrderBy(s => s.Index)
                .ToList();

            HasSplice = false;
            SpanSpliceInfo.Clear();

            // First pass: Check existing splice data from API
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
                    HasSplice = true;
            }

            // Second pass: Check for name-based splice detection
            await DetectSplicesFromNamesAsync(spans);

            // Third pass: Check for span number discontinuity
            DetectSplicesFromSpanNumbers(spans);

            Spans = spans;
        }

        private async Task DetectSplicesFromNamesAsync(List<IMemberSpan> spans)
        {
            var spliceKeywords = new[] { "splice", "connection", "joint", "lift", "piece", "stack" };

            foreach (var span in spans)
            {
                try
                {
                    bool nameIndicatesSplice = false;
                    double nameBasedSpliceOffset = span.Length.Value;

                    // Check span name
                    string spanName = span.Name?.ToLowerInvariant() ?? "";
                    if (spliceKeywords.Any(keyword => spanName.Contains(keyword)))
                    {
                        nameIndicatesSplice = true;
                    }

                    // Check UDAs
                    var udas = await span.GetUserDefinedAttributesAsync();
                    foreach (var uda in udas)
                    {
                        if (uda is IUserDefinedTextAttribute textUda)
                        {
                            string udaValue = textUda.Text?.ToLowerInvariant() ?? "";
                            string udaName = uda.AttributeDefinitionName?.ToLowerInvariant() ?? "";

                            if (spliceKeywords.Any(keyword => udaValue.Contains(keyword) || udaName.Contains(keyword)))
                            {
                                nameIndicatesSplice = true;

                                // Try to extract splice offset
                                if (uda.AttributeDefinitionName?.ToLowerInvariant().Contains("offset") == true ||
                                    uda.AttributeDefinitionName?.ToLowerInvariant().Contains("distance") == true)
                                {
                                    if (double.TryParse(textUda.Text, out double offsetValue))
                                    {
                                        nameBasedSpliceOffset = offsetValue;
                                    }
                                }
                            }
                        }
                    }

                    if (nameIndicatesSplice)
                    {
                        HasSplice = true;
                        var existingInfo = SpanSpliceInfo.TryGetValue(span.Index, out var existing)
                            ? existing
                            : (false, 0.0);

                        double finalOffset = existingInfo.Item1 ? existingInfo.Item2 : nameBasedSpliceOffset;
                        SpanSpliceInfo[span.Index] = (true, finalOffset);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error checking span {span.Index} for name-based splice indicators: {ex.Message}");
                }
            }
        }

        private void DetectSplicesFromSpanNumbers(List<IMemberSpan> spans)
        {
            if (spans.Count <= 1) return;

            var spanIndices = spans.Select(s => s.Index).OrderBy(i => i).ToList();

            for (int i = 0; i < spanIndices.Count - 1; i++)
            {
                int currentSpan = spanIndices[i];
                int nextSpan = spanIndices[i + 1];

                if (nextSpan - currentSpan > 1)
                {
                    HasSplice = true;
                    var spanBeforeGap = spans.FirstOrDefault(s => s.Index == currentSpan);
                    if (spanBeforeGap != null)
                    {
                        var existingInfo = SpanSpliceInfo.TryGetValue(spanBeforeGap.Index, out var existing)
                            ? existing
                            : (false, spanBeforeGap.Length.Value);

                        double finalOffset = existingInfo.Item1 ? existingInfo.Item2 : spanBeforeGap.Length.Value;
                        SpanSpliceInfo[spanBeforeGap.Index] = (true, finalOffset);
                    }
                }
            }
        }

        /// <summary>
        /// Creates column lifts based on organized spans and splice locations.
        /// </summary>
        /// <returns>A list of <see cref="ColumnLift"/> objects.</returns>
        public List<ColumnLift> CreateLifts()
        {
            var lifts = new List<ColumnLift>();

            if (!HasSplice)
            {
                // No splice - entire column is one lift
                var lift = new ColumnLift
                {
                    Name = $"{ParentMember.Name}_L1",
                    Spans = new List<IMemberSpan>(Spans),
                    StartNode = Spans.First().StartMemberNode,
                    EndNode = Spans.Last().EndMemberNode,
                    Length = Spans.Sum(s => s.Length.Value)
                };
                lifts.Add(lift);
                return lifts;
            }

            // Has splices - create lifts between splices
            var currentLiftSpans = new List<IMemberSpan>();
            var liftCount = 1;
            var spliceSpanIndices = SpanSpliceInfo
                .Where(si => si.Value.Item1)
                .Select(si => si.Key)
                .OrderBy(idx => idx)
                .ToList();

            foreach (var span in Spans.OrderBy(s => s.Index))
            {
                if (spliceSpanIndices.Contains(span.Index) && currentLiftSpans.Any())
                {
                    var lift = new ColumnLift
                    {
                        Name = $"{ParentMember.Name}_L{liftCount}",
                        Spans = new List<IMemberSpan>(currentLiftSpans),
                        StartNode = currentLiftSpans.First().StartMemberNode,
                        EndNode = currentLiftSpans.Last().EndMemberNode,
                        Length = currentLiftSpans.Sum(s => s.Length.Value)
                    };
                    lifts.Add(lift);
                    currentLiftSpans.Clear();
                    liftCount++;
                }
                currentLiftSpans.Add(span);
            }

            // Add final lift
            if (currentLiftSpans.Any())
            {
                var lift = new ColumnLift
                {
                    Name = $"{ParentMember.Name}_L{liftCount}",
                    Spans = new List<IMemberSpan>(currentLiftSpans),
                    StartNode = currentLiftSpans.First().StartMemberNode,
                    EndNode = currentLiftSpans.Last().EndMemberNode,
                    Length = currentLiftSpans.Sum(s => s.Length.Value)
                };
                lifts.Add(lift);
            }
            return lifts;
        }
    }

    /// <summary>
    /// Represents a lift of a column (a segment between splices or levels).
    /// </summary>
    public class ColumnLift
    {
        /// <summary>The name of the lift.</summary>
        public string Name { get; set; } = "";
        /// <summary>The spans contained in this lift.</summary>
        public List<IMemberSpan> Spans { get; set; } = new List<IMemberSpan>();
        /// <summary>The start node of the lift.</summary>
        public IMemberNode? StartNode { get; set; }
        /// <summary>The end node of the lift.</summary>
        public IMemberNode? EndNode { get; set; }
        /// <summary>Total length of the lift in mm.</summary>
        public double Length { get; set; } // Total length in mm
    }
}
