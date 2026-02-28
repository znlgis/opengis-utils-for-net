using FluentAssertions;
using OpenGIS.Utils.Utils;

namespace OpenGIS.Utils.Tests;

public class SortUtilTests
{
    [Fact]
    public void CompareString_BothNull_ReturnsZero()
    {
        SortUtil.CompareString(null!, null!).Should().Be(0);
    }

    [Fact]
    public void CompareString_FirstNull_ReturnsNegative()
    {
        SortUtil.CompareString(null!, "abc").Should().BeNegative();
    }

    [Fact]
    public void CompareString_SecondNull_ReturnsPositive()
    {
        SortUtil.CompareString("abc", null!).Should().BePositive();
    }

    [Theory]
    [InlineData("file1", "file2", -1)]
    [InlineData("file2", "file10", -1)]
    [InlineData("file10", "file1", 1)]
    [InlineData("file1", "file1", 0)]
    public void CompareString_NumericOrdering(string a, string b, int expectedSign)
    {
        var result = SortUtil.CompareString(a, b);

        if (expectedSign < 0) result.Should().BeNegative();
        else if (expectedSign > 0) result.Should().BePositive();
        else result.Should().Be(0);
    }

    [Fact]
    public void CompareString_TextOrdering()
    {
        SortUtil.CompareString("apple", "banana").Should().BeNegative();
        SortUtil.CompareString("banana", "apple").Should().BePositive();
        SortUtil.CompareString("same", "same").Should().Be(0);
    }

    [Fact]
    public void NaturalSort_SortsCollectionCorrectly()
    {
        var items = new[] { "file10.txt", "file2.txt", "file1.txt", "file20.txt" };

        var sorted = SortUtil.NaturalSort(items, x => x).ToList();

        sorted.Should().ContainInOrder("file1.txt", "file2.txt", "file10.txt", "file20.txt");
    }

    [Fact]
    public void NaturalSort_ThrowsOnNullSource()
    {
        var act = () => SortUtil.NaturalSort<string>(null!, x => x);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NaturalSort_ThrowsOnNullKeySelector()
    {
        var items = new[] { "a", "b" };

        var act = () => SortUtil.NaturalSort<string>(items, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NaturalSort_WithCustomKeySelector()
    {
        var items = new[]
        {
            new { Id = 1, Name = "item10" },
            new { Id = 2, Name = "item2" },
            new { Id = 3, Name = "item1" }
        };

        var sorted = SortUtil.NaturalSort(items, x => x.Name).ToList();

        sorted[0].Name.Should().Be("item1");
        sorted[1].Name.Should().Be("item2");
        sorted[2].Name.Should().Be("item10");
    }
}
