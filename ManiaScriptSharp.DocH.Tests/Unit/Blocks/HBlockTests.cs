using System.Text;
using System.Text.RegularExpressions;

namespace ManiaScriptSharp.DocH.Tests.Unit.Blocks;

public class HBlockTests
{
    [Fact]
    public void TryRead_LineDoesNotStartWithStart_ReturnsFalse()
    {
        // Arrange
        var hBlockMock = new Mock<HBlock>(null);
        hBlockMock.SetupGet(x => x.Start).Returns("random");

        using var ms = new MemoryStream();
        using var reader = new StreamReader(ms);
        var builder = new StringBuilder();

        // Act
        var result = hBlockMock.Object.TryRead("test", reader, builder);

        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public void TryRead_LineMatchDoesNotMatch_ReturnsFalse()
    {
        // Arrange
        var hBlockMock = new Mock<HBlock>(null);
        hBlockMock.SetupGet(x => x.IdentifierRegex).Returns(new Regex("x"));

        using var ms = new MemoryStream();
        using var reader = new StreamReader(ms);
        var builder = new StringBuilder();

        // Act
        var result = hBlockMock.Object.TryRead("random test", reader, builder);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryRead_BeforeReadIsFalse_ReturnsFalse()
    {
        // Arrange
        using var ms = new MemoryStream();
        using var reader = new StreamReader(ms);
        var builder = new StringBuilder();
        
        var line = "random test";
        
        var hBlockMock = new Mock<HBlock>(null);
        hBlockMock.Setup(x => x.BeforeRead(line, default, builder)).Returns(false);

        // Act
        var result = hBlockMock.Object.TryRead(line, reader, builder);

        // Assert
        Assert.False(result);
    }
}
