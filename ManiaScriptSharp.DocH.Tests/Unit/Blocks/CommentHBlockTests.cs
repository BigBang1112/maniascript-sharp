using ManiaScriptSharp.DocH.Blocks;
using ManiaScriptSharp.DocH.Inlines;
using ManiaScriptSharp.DocH.Tests.Mocks;
using System.Collections.Immutable;
using System.Text;

namespace ManiaScriptSharp.DocH.Tests.Unit.Blocks;

public class CommentHBlockTests
{
    [Fact]
    public void Start_ReturnsCorrect()
    {
        // Arrange
        var hBlock = new CommentHBlock(depth: 0);

        // Act & Assert
        Assert.Equal(expected: "/*!", actual: hBlock.Start);
    }

    [Fact]
    public void End_ReturnsCorrect()
    {
        // Arrange
        var hBlock = new CommentHBlock(depth: 0);

        // Act & Assert
        Assert.Equal(expected: "*/", actual: hBlock.End);
    }

    [Fact]
    public void UseEmptyLines_IsTrue()
    {
        // Arrange
        var hBlock = new CommentHBlock(depth: 0);

        // Act & Assert
        Assert.True(hBlock.UseEmptyLines);
    }

    [Fact]
    public void ReadLine_AddsCommentCorretly_HandlesAsterisk()
    {
        // Arrange
        var hBlock = new CommentHBlock(depth: 0);

        var exampleString = @"* This is the base Manialink page interface.
*
* Supported declare modes :
* - Local
* - Persistent
*/";

        using var exampleStream = new MemoryStream(Encoding.UTF8.GetBytes(exampleString));
        using var reader = new StreamReader(exampleStream);
        var builder = new StringBuilder();

        // Act
        hBlock.ReadLine(reader.ReadLine()!, reader, builder);

        // Assert
        Assert.True(hBlock.Comments.Count == 1);
        Assert.Contains(expected: "This is the base Manialink page interface.", hBlock.Comments);
    }

    [Fact]
    public void ReadLine_AddsCommentCorretly_HandlesBrief()
    {
        // Arrange
        var hBlock = new CommentHBlock(depth: 0);

        var exampleString = @"* \brief This is the base Manialink page interface.
*
* Supported declare modes :
* - Local
* - Persistent
*/";
        
        using var exampleStream = new MemoryStream(Encoding.UTF8.GetBytes(exampleString));
        using var reader = new StreamReader(exampleStream);
        var builder = new StringBuilder();

        // Act
        hBlock.ReadLine(reader.ReadLine()!, reader, builder);

        // Assert
        Assert.True(hBlock.Comments.Count == 1);
        Assert.Contains(expected: "This is the base Manialink page interface.", hBlock.Comments);
    }

    [Fact]
    public void ReadLine_HandlesEmptyLine()
    {
        // Arrange
        var hBlock = new CommentHBlock(depth: 0);

        var exampleString = @"* 
*
* Supported declare modes :
* - Local
* - Persistent
*/";

        using var exampleStream = new MemoryStream(Encoding.UTF8.GetBytes(exampleString));
        using var reader = new StreamReader(exampleStream);
        var builder = new StringBuilder();

        // Act
        hBlock.ReadLine(reader.ReadLine()!.Trim(), reader, builder);

        // Assert
        Assert.True(hBlock.Comments.Count == 1);
        Assert.Contains(expected: "", hBlock.Comments);
    }

    [Fact]
    public void ReadLine_AtLeastOneCommentAlreadyExists()
    {
        // Arrange
        var hBlock = new CommentHBlock(depth: 0);
        hBlock.Comments.Add("This is the base Manialink page interface.");

        var exampleString = @"
*
* Supported declare modes :
* - Local
* - Persistent
*/";

        using var exampleStream = new MemoryStream(Encoding.UTF8.GetBytes(exampleString));
        using var reader = new StreamReader(exampleStream);
        var builder = new StringBuilder();

        // Act
        hBlock.ReadLine(reader.ReadLine()!, reader, builder);

        // Assert
        Assert.True(hBlock.Comments.Count == 2);
        Assert.True(hBlock.Comments[1] == "");
    }

    [Fact]
    public void AfterRead_CreatesSummaryFromComments()
    {
        // Arrange
        var hBlock = new CommentHBlock(depth: 1);
        hBlock.Comments.Add("This is the base Manialink page interface.");
        hBlock.Comments.Add("");
        hBlock.Comments.Add("Supported declare modes :");

        var expected = $"\t/// <summary>{Environment.NewLine}" +
            $"\t/// This is the base Manialink page interface.{Environment.NewLine}" +
            $"\t/// {Environment.NewLine}" +
            $"\t/// Supported declare modes :{Environment.NewLine}" +
            $"\t/// </summary>{Environment.NewLine}";

        var builder = new StringBuilder();

        // Act
        hBlock.AfterRead(builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }

    [Fact]
    public void AfterRead_NoComments()
    {
        // Arrange
        var hBlock = new CommentHBlock(depth: 1);

        var expected = $"";

        var builder = new StringBuilder();

        // Act
        hBlock.AfterRead(builder);

        // Assert
        Assert.Equal(expected, actual: builder.ToString());
    }
}
