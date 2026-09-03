using System;
using NUnit.Framework;

namespace ChoSiren.Tests
{
    public sealed class MemberRosterPaginationTests
    {
        [Test]
        public void FiftyMembersFitThreeTwentyCardPagesWithoutDuplicates()
        {
            bool[] seen = new bool[50];

            for (int requestedPage = 0; requestedPage < 3; requestedPage++)
            {
                MemberRosterPage page = MemberRosterPagination.Build(50, requestedPage);

                Assert.That(page.PageCount, Is.EqualTo(3));
                Assert.That(page.VisibleCount, Is.EqualTo(requestedPage == 2 ? 10 : 20));
                for (int slot = 0; slot < page.VisibleCount; slot++)
                {
                    int sourceIndex = page.SourceIndexAt(slot);
                    Assert.That(seen[sourceIndex], Is.False, $"成员 {sourceIndex} 被重复分页");
                    seen[sourceIndex] = true;
                }
            }

            Assert.That(seen, Has.All.True, "50 名成员必须全部且仅出现一次");
        }

        [Test]
        public void FiftyMembersAlsoSupportTwelveCardPages()
        {
            MemberRosterPage lastPage = MemberRosterPagination.Build(50, 4, pageSize: 12);

            Assert.That(lastPage.PageCount, Is.EqualTo(5));
            Assert.That(lastPage.SourceIndices, Is.EqualTo(new[] { 48, 49 }));
            Assert.That(lastPage.HasPrevious, Is.True);
            Assert.That(lastPage.HasNext, Is.False);
        }

        [Test]
        public void FilteringHappensBeforePaginationAndKeepsSourceIndexes()
        {
            MemberRosterPage page = MemberRosterPagination.Build(60, 2, index => index % 2 == 0, pageSize: 9);

            Assert.That(page.TotalMatches, Is.EqualTo(30));
            Assert.That(page.PageCount, Is.EqualTo(4));
            Assert.That(page.SourceIndices, Is.EqualTo(new[] { 36, 38, 40, 42, 44, 46, 48, 50, 52 }));
        }

        [TestCase(-100, 0)]
        [TestCase(0, 0)]
        [TestCase(2, 2)]
        [TestCase(99, 2)]
        public void RequestedPageIsClampedToCatalogBounds(int requestedPage, int expectedPage)
        {
            MemberRosterPage page = MemberRosterPagination.Build(50, requestedPage);

            Assert.That(page.PageIndex, Is.EqualTo(expectedPage));
        }

        [Test]
        public void EmptyFilterProducesStableEmptyPage()
        {
            MemberRosterPage page = MemberRosterPagination.Build(50, 99, _ => false);

            Assert.That(page.IsEmpty, Is.True);
            Assert.That(page.TotalMatches, Is.Zero);
            Assert.That(page.PageCount, Is.Zero);
            Assert.That(page.PageIndex, Is.Zero);
            Assert.That(page.SourceIndices, Is.Empty);
            Assert.That(page.HasPrevious, Is.False);
            Assert.That(page.HasNext, Is.False);
        }

        [Test]
        public void FiveColumnGridPlacesTwentySlotsInFourRows()
        {
            MemberRosterCell first = MemberRosterPagination.CellFor(0);
            MemberRosterCell center = MemberRosterPagination.CellFor(9);
            MemberRosterCell last = MemberRosterPagination.CellFor(19);

            Assert.That((first.Row, first.Column), Is.EqualTo((0, 0)));
            Assert.That((center.Row, center.Column), Is.EqualTo((1, 4)));
            Assert.That((last.Row, last.Column), Is.EqualTo((3, 4)));
        }

        [TestCase(1070f, 3)]
        [TestCase(1290f, 4)]
        [TestCase(1510f, 5)]
        [TestCase(1730f, 6)]
        [TestCase(2200f, 7)]
        public void RosterRowsGrowWithAvailablePortraitHeight(float contentHeight, int expectedRows)
        {
            Assert.That(MemberRosterPagination.RowsForContentHeight(contentHeight), Is.EqualTo(expectedRows));
        }

        [Test]
        public void MovingPagesClampsAndCannotOverflow()
        {
            Assert.That(MemberRosterPagination.MovePage(0, -1, 6), Is.Zero);
            Assert.That(MemberRosterPagination.MovePage(2, 1, 6), Is.EqualTo(3));
            Assert.That(MemberRosterPagination.MovePage(5, 1, 6), Is.EqualTo(5));
            Assert.That(MemberRosterPagination.MovePage(int.MaxValue, int.MaxValue, 6), Is.EqualTo(5));
        }

        [Test]
        public void InvalidArgumentsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MemberRosterPagination.Build(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => MemberRosterPagination.Build(1, 0, pageSize: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => MemberRosterPagination.ClampPageIndex(0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => MemberRosterPagination.CellFor(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => MemberRosterPagination.CellFor(0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => MemberRosterPagination.RowsForContentHeight(-1f));
        }
    }
}
