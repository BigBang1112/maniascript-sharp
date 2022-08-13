﻿using ManiaScriptSharp.DocH.Blocks;
using ManiaScriptSharp.DocH.Tests.Mocks;
using System.Text;

namespace ManiaScriptSharp.DocH.Tests.Unit.Blocks;

public class NamespaceHBlockTests
{
    [Fact]
    public void AfterRead_AppendsEndCurlyBracket()
    {
        // Arrange
        var hBlock = new NamespaceHBlock();
        var builder = new StringBuilder();

        // Act
        hBlock.AfterRead(builder);

        // Assert
        Assert.Equal($"}}{Environment.NewLine}{Environment.NewLine}", builder.ToString());
    }

    [Fact]
    public void BeforeAttemptToEnd_TryReadComment()
    {
        // Arrange
        var hBlock = new NamespaceHBlock();

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
        var hBlock = new NamespaceHBlock();

        // Act
        var actual = hBlock.End;

        // Assert
        Assert.Equal("};", actual);
    }
}