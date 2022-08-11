using ManiaScriptSharp.DocH.Blocks;
using System.Text;

namespace ManiaScriptSharp.DocH.Tests.Unit.Blocks;

public class ClassOrStructHBlockTests
{
    [Fact]
    public void AfterRead_AppendsEndCurlyBracket()
    {
        // Arrange
        var hBlock = new ClassOrStructHBlock();
        var builder = new StringBuilder();

        // Act
        hBlock.AfterRead(builder);

        // Assert
        Assert.Equal($"}}{Environment.NewLine}{Environment.NewLine}", builder.ToString());
    }
}
