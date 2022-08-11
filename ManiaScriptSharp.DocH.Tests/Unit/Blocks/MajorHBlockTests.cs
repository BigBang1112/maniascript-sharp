using ManiaScriptSharp.DocH.Tests.Mocks;
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
}
