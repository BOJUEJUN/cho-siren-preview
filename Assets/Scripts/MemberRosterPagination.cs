using System;

namespace ChoSiren
{
    /// <summary>
    /// Immutable description of one visible card slot in a paged member grid.
    /// </summary>
    public readonly struct MemberRosterCell
    {
        public int SlotIndex { get; }
        public int Row { get; }
        public int Column { get; }

        internal MemberRosterCell(int slotIndex, int row, int column)
        {
            SlotIndex = slotIndex;
            Row = row;
            Column = column;
        }
    }

    /// <summary>
    /// One page of source indexes. Keeping only the visible indexes allows the UI
    /// to create a small, fixed number of cards even when the catalog grows large.
    /// </summary>
    public sealed class MemberRosterPage
    {
        private readonly int[] sourceIndices;

        public int TotalMatches { get; }
        public int PageSize { get; }
        public int PageIndex { get; }
        public int PageCount { get; }
        public int VisibleCount => sourceIndices.Length;
        public bool IsEmpty => TotalMatches == 0;
        public bool HasPrevious => PageCount > 0 && PageIndex > 0;
        public bool HasNext => PageCount > 0 && PageIndex + 1 < PageCount;
        public int[] SourceIndices => (int[])sourceIndices.Clone();

        internal MemberRosterPage(
            int totalMatches,
            int pageSize,
            int pageIndex,
            int pageCount,
            int[] sourceIndices)
        {
            TotalMatches = totalMatches;
            PageSize = pageSize;
            PageIndex = pageIndex;
            PageCount = pageCount;
            this.sourceIndices = sourceIndices ?? Array.Empty<int>();
        }

        public int SourceIndexAt(int visibleIndex)
        {
            if (visibleIndex < 0 || visibleIndex >= sourceIndices.Length)
                throw new ArgumentOutOfRangeException(nameof(visibleIndex));

            return sourceIndices[visibleIndex];
        }
    }

    /// <summary>
    /// Pure pagination and grid math for the member roster. It deliberately does
    /// not depend on GameModel or Unity objects, so it can be tested cheaply and
    /// reused by member filters, search, and rarity/role tabs.
    /// </summary>
    public static class MemberRosterPagination
    {
        public const int DefaultColumns = 5;
        public const int DefaultRows = 4;
        public const int DefaultPageSize = DefaultColumns * DefaultRows;
        public const int MinimumRows = 3;
        public const int MaximumRows = 7;

        /// <summary>
        /// Calculates how many complete 210 px cards fit between the fixed filter
        /// header and the paginator anchored immediately above bottom navigation.
        /// Taller portrait windows therefore show more members without stretching
        /// the card art, while short windows keep the paginator reachable.
        /// </summary>
        public static int RowsForContentHeight(float contentHeight)
        {
            if (float.IsNaN(contentHeight) || float.IsInfinity(contentHeight) || contentHeight < 0f)
                throw new ArgumentOutOfRangeException(nameof(contentHeight));

            int rows = (int)Math.Floor((contentHeight - 278f) / 220f);
            return Math.Max(MinimumRows, Math.Min(MaximumRows, rows));
        }

        /// <summary>
        /// Filters source indexes first, then selects the requested page. An
        /// out-of-range page is clamped to the nearest valid page.
        /// </summary>
        public static MemberRosterPage Build(
            int sourceCount,
            int requestedPageIndex,
            Predicate<int> include = null,
            int pageSize = DefaultPageSize)
        {
            if (sourceCount < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceCount));
            if (pageSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(pageSize));

            int totalMatches = 0;
            for (int sourceIndex = 0; sourceIndex < sourceCount; sourceIndex++)
            {
                if (include == null || include(sourceIndex)) totalMatches++;
            }

            int pageCount = totalMatches == 0 ? 0 : (totalMatches + pageSize - 1) / pageSize;
            int pageIndex = ClampPageIndex(requestedPageIndex, pageCount);
            int pageStart = pageIndex * pageSize;
            int visibleCount = Math.Min(pageSize, Math.Max(0, totalMatches - pageStart));
            int[] sourceIndices = new int[visibleCount];

            int matchIndex = 0;
            int visibleIndex = 0;
            for (int sourceIndex = 0; sourceIndex < sourceCount && visibleIndex < visibleCount; sourceIndex++)
            {
                if (include != null && !include(sourceIndex)) continue;

                if (matchIndex >= pageStart)
                    sourceIndices[visibleIndex++] = sourceIndex;

                matchIndex++;
            }

            return new MemberRosterPage(totalMatches, pageSize, pageIndex, pageCount, sourceIndices);
        }

        public static int ClampPageIndex(int requestedPageIndex, int pageCount)
        {
            if (pageCount < 0)
                throw new ArgumentOutOfRangeException(nameof(pageCount));
            if (pageCount == 0 || requestedPageIndex < 0) return 0;
            return requestedPageIndex >= pageCount ? pageCount - 1 : requestedPageIndex;
        }

        public static int MovePage(int currentPageIndex, int delta, int pageCount)
        {
            if (pageCount < 0)
                throw new ArgumentOutOfRangeException(nameof(pageCount));

            long requested = (long)currentPageIndex + delta;
            if (requested < int.MinValue) requested = int.MinValue;
            if (requested > int.MaxValue) requested = int.MaxValue;
            return ClampPageIndex((int)requested, pageCount);
        }

        public static MemberRosterCell CellFor(int visibleIndex, int columns = DefaultColumns)
        {
            if (visibleIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(visibleIndex));
            if (columns <= 0)
                throw new ArgumentOutOfRangeException(nameof(columns));

            return new MemberRosterCell(visibleIndex, visibleIndex / columns, visibleIndex % columns);
        }
    }
}
