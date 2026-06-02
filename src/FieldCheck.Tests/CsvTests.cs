using FieldCheck.Core.Utilities;
using Xunit;

namespace FieldCheck.Tests;

public class CsvTests
{
    [Fact]
    public void Parse_SimpleRows()
    {
        var rows = Csv.Parse("a,b,c\r\n1,2,3");
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "a", "b", "c" }, rows[0]);
        Assert.Equal(new[] { "1", "2", "3" }, rows[1]);
    }

    [Fact]
    public void Parse_HandlesQuotedCommas()
    {
        var rows = Csv.Parse("item_text\r\n\"Verify A, B, and C\"");
        Assert.Equal("Verify A, B, and C", rows[1][0]);
    }

    [Fact]
    public void Parse_HandlesEscapedQuotes()
    {
        var rows = Csv.Parse("col\r\n\"He said \"\"go\"\"\"");
        Assert.Equal("He said \"go\"", rows[1][0]);
    }

    [Fact]
    public void Parse_HandlesEmbeddedNewlinesInsideQuotes()
    {
        var rows = Csv.Parse("col\r\n\"line1\nline2\"");
        Assert.Equal(2, rows.Count);
        Assert.Equal("line1\nline2", rows[1][0]);
    }

    [Fact]
    public void Parse_AcceptsLfOnly()
    {
        var rows = Csv.Parse("a,b\n1,2\n");
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "1", "2" }, rows[1]);
    }

    [Fact]
    public void Parse_TrailingNewlineDoesNotCreateExtraRow()
    {
        var rows = Csv.Parse("a,b\r\n1,2\r\n");
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void Parse_BlankLineBecomesBlankRow()
    {
        var rows = Csv.Parse("a\r\n\r\nb");
        Assert.Equal(3, rows.Count);
        Assert.True(rows[1].All(string.IsNullOrEmpty));
    }

    [Fact]
    public void Parse_StripsLeadingBom()
    {
        var rows = Csv.Parse("﻿item_text\r\nVerify");
        Assert.Equal("item_text", rows[0][0]);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("has,comma", "\"has,comma\"")]
    [InlineData("has\"quote", "\"has\"\"quote\"")]
    [InlineData("has\nnewline", "\"has\nnewline\"")]
    public void Escape_QuotesOnlyWhenNeeded(string input, string expected)
    {
        Assert.Equal(expected, Csv.Escape(input));
    }

    [Fact]
    public void BuildAndParse_RoundTripsTrickyValues()
    {
        var original = new List<IEnumerable<string?>>
        {
            new[] { "a", "b", "c" },
            new[] { "comma, here", "quote \" here", "line\nbreak" }
        };

        var text = Csv.Build(original);
        var parsed = Csv.Parse(text);

        Assert.Equal(2, parsed.Count);
        Assert.Equal("comma, here", parsed[1][0]);
        Assert.Equal("quote \" here", parsed[1][1]);
        Assert.Equal("line\nbreak", parsed[1][2]);
    }
}
