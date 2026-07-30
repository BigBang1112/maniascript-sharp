using ManiaScriptSharp.Generator.Emission;
using Xunit;

namespace ManiaScriptSharp.Generator.Tests;

public class IndentedWriterTests
{
    [Fact]
    public void Line_WithText_AppendsWithNewline()
    {
        var w = new IndentedWriter();
        w.Line("hello");
        Assert.Equal("hello\r\n", w.ToString());
    }

    [Fact]
    public void Line_Empty_AppendsBlankLine()
    {
        var w = new IndentedWriter();
        w.Line();
        Assert.Equal("\r\n", w.ToString());
    }

    [Fact]
    public void Push_IncreasesIndent()
    {
        var w = new IndentedWriter();
        Assert.Equal(0, w.Indent);
        w.Push();
        Assert.Equal(1, w.Indent);
    }

    [Fact]
    public void Pop_DecreasesIndent()
    {
        var w = new IndentedWriter();
        w.Push();
        w.Push();
        w.Pop();
        Assert.Equal(1, w.Indent);
    }

    [Fact]
    public void Pop_AtZero_DoesNotGoNegative()
    {
        var w = new IndentedWriter();
        w.Pop();
        Assert.Equal(0, w.Indent);
    }

    [Fact]
    public void Line_WithIndent_PrependsTabs()
    {
        var w = new IndentedWriter(useSpaces: false, indentSize: 1);
        w.Push();
        w.Line("x");
        Assert.Equal("\tx\r\n", w.ToString());
    }

    [Fact]
    public void Line_WithSpacesIndent_PrependsSpaces()
    {
        var w = new IndentedWriter(useSpaces: true, indentSize: 4);
        w.Push();
        w.Line("y");
        Assert.Equal("    y\r\n", w.ToString());
    }

    [Fact]
    public void Line_TwoLevels_DoubleIndent()
    {
        var w = new IndentedWriter(useSpaces: false, indentSize: 1);
        w.Push();
        w.Push();
        w.Line("z");
        Assert.Equal("\t\tz\r\n", w.ToString());
    }

    [Fact]
    public void Line_EmptyText_NoIndentChars()
    {
        var w = new IndentedWriter();
        w.Push();
        w.Line();
        // Blank lines should have no leading whitespace
        Assert.Equal("\r\n", w.ToString());
    }

    [Fact]
    public void Raw_AppendsWithoutNewline()
    {
        var w = new IndentedWriter();
        w.Raw("abc");
        w.Raw("def");
        Assert.Equal("abcdef", w.ToString());
    }

    [Fact]
    public void IndentSize_GreaterThanOne_Multiplies()
    {
        var w = new IndentedWriter(useSpaces: false, indentSize: 2);
        w.Push();
        w.Line("a");
        Assert.Equal("\t\ta\r\n", w.ToString());
    }
}
