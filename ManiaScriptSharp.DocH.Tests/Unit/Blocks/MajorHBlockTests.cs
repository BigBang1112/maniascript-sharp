﻿using ManiaScriptSharp.DocH.Blocks;
using ManiaScriptSharp.DocH.Inlines;
using ManiaScriptSharp.DocH.Tests.Mocks;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;

namespace ManiaScriptSharp.DocH.Tests.Unit.Blocks;

public class MajorHBlockTests
{
    [Fact]
    public void AfterRead_AppendsEndCurlyBracket()
    {
        // Arrange
        var hBlock = new MockMajorHBlock();
        var builder = new StringBuilder();

        // Act
        hBlock.AfterRead(builder);

        // Assert
        Assert.Equal($"}}{Environment.NewLine}{Environment.NewLine}", builder.ToString());
    }
    
    [Fact]
    public void ReadLine_CanProcessBlock()
    {
        // Arrange
        var hBlock = new MockMajorHBlock(ImmutableArray.Create<Func<SymbolContext?, HGeneral>>(context => new EnumHBlock(context)));

        var expected = $"\tpublic enum TestEnum{Environment.NewLine}" +
            $"\t{{{Environment.NewLine}" +
            $"\t\tOne,{Environment.NewLine}" +
            $"\t\tTwo,{Environment.NewLine}" +
            $"\t\tThree{Environment.NewLine}" +
            $"\t}}{Environment.NewLine}{Environment.NewLine}";

        var exampleString = @"enum TestEnum {
    One,
    Two,
    Three
};";
        using var exampleStream = new MemoryStream(Encoding.UTF8.GetBytes(exampleString));
        using var reader = new StreamReader(exampleStream);
        var builder = new StringBuilder();

        // Act
        var result = hBlock.ReadLine(reader.ReadLine()!, reader, builder);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void ReadLine_CanProcessInline()
    {
        // Arrange
        var hBlock = new MockMajorHBlock(ImmutableArray.Create<Func<SymbolContext?, HGeneral>>(context => new PropertyHInline(context)));

        var expected = $"\tpublic bool IsPartUnderground {{ get; }}{Environment.NewLine}";

        var exampleString = @"	const	Boolean IsPartUnderground;";
        using var exampleStream = new MemoryStream(Encoding.UTF8.GetBytes(exampleString));
        using var reader = new StreamReader(exampleStream);
        var builder = new StringBuilder();

        // Act
        var result = hBlock.ReadLine(reader.ReadLine()!, reader, builder);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void ReadLine_SymbolIncludesMembersInHContext()
    {
        // Arrange
        var memberSymbolMock = new Mock<IPropertySymbol>();
        memberSymbolMock.SetupGet(x => x.Name).Returns("IsPartUnderground");

        var manualSymbolMock = new Mock<INamedTypeSymbol>();
        manualSymbolMock.Setup(x => x.GetMembers()).Returns(ImmutableArray.Create<ISymbol>(memberSymbolMock.Object));

        var hBlock = new MockMajorHBlock(ImmutableArray.Create<Func<SymbolContext?, HGeneral>>(context => new PropertyHInline(context)))
        {
            ManualSymbol = manualSymbolMock.Object
        };

        var expected = $"\tpublic bool IsPartUnderground {{ get; }}{Environment.NewLine}";

        var exampleString = @"	const	Boolean IsPartUnderground;";
        using var exampleStream = new MemoryStream(Encoding.UTF8.GetBytes(exampleString));
        using var reader = new StreamReader(exampleStream);
        var builder = new StringBuilder();

        // Act
        var result = hBlock.ReadLine(reader.ReadLine()!, reader, builder);

        // Assert
        Assert.True(result);
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void BeforeAttemptToEnd_TryReadComment()
    {
        // Arrange
        var hBlock = new MockMajorHBlock();

        var expected = $"\t/// <summary>{Environment.NewLine}" +
            $"\t/// Save a matchsettings file.{Environment.NewLine}" +
            $"\t/// </summary>{Environment.NewLine}";

        var exampleString = @"/*!
Save a matchsettings file.
*/";
        using var exampleStream = new MemoryStream(Encoding.UTF8.GetBytes(exampleString));
        using var reader = new StreamReader(exampleStream);
        var builder = new StringBuilder();

        // Act
        hBlock.BeforeAttemptToEnd(reader.ReadLine()!, reader, builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void End_ReturnsCorrect()
    {
        // Arrange
        var hBlock = new MockMajorHBlock();

        // Act
        var actual = hBlock.End;

        // Assert
        Assert.Equal("};", actual);
    }
}
