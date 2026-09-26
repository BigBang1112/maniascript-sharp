using Xunit;

namespace ManiaScriptSharp.ManiaPlanet.Tests;

public class CMapEditorPluginTests
{
    [Fact]
    public void BlocksCanBePlacedRemovedAndRestored()
    {
        var editor = new CMapEditorPlugin();
        var model = new CBlockModel();
        var coord = new Int3(2, 1, 3);

        Assert.True(editor.PlaceBlock(model, coord, CMapEditorPlugin.CardinalDirections.East));
        Assert.Same(model, editor.GetBlock(coord).BlockModel);
        Assert.Equal(coord, editor.GetBlock(coord).Coord);
        editor.GetConnectResults(editor.GetBlock(coord), model);
        Assert.Equal(4, editor.ConnectResults.Count);
        Assert.All(editor.ConnectResults, result => Assert.True(result.CanPlace));
        Assert.False(editor.PlaceBlock_NoDestruction(model, coord, CMapEditorPlugin.CardinalDirections.North));
        Assert.True(editor.RemoveBlock(coord));
        Assert.Null(editor.GetBlock(coord));
        Assert.True(editor.Undo());
        Assert.NotNull(editor.GetBlock(coord));
        Assert.True(editor.Redo());
        Assert.Null(editor.GetBlock(coord));
    }

    [Fact]
    public void TerrainPlacementAndRemovalUseOneUndoStep()
    {
        var editor = new CMapEditorPlugin();
        var model = new CBlockModel();

        Assert.True(editor.PlaceTerrainBlocks(model, new Int3(0, 2, 0), new Int3(1, 2, 1)));
        Assert.Equal(4, editor.TerrainBlocks.Count);
        Assert.Equal(2, editor.GetGroundHeight(1, 1));
        Assert.True(editor.Undo());
        Assert.Empty(editor.Blocks);
        Assert.True(editor.Redo());
        Assert.Equal(4, editor.Blocks.Count);
    }

    [Fact]
    public void MacroblocksAndMapStateAreAvailableThroughExistingMembers()
    {
        var editor = new CMapEditorPlugin();
        var model = new CMacroblockModel();
        var coord = new Int3(4, 0, 5);

        var instance = editor.CreateMacroblockInstance(model, coord, CMapEditorPlugin.CardinalDirections.South, 7);
        Assert.Same(instance, editor.GetMacroblockInstanceFromUnitCoord(coord));
        Assert.Equal(7, instance.UserData);
        Assert.True(editor.RemoveMacroblockInstancesByUserData(7));
        Assert.Empty(editor.MacroblockInstances);

        Assert.True(editor.SetMapType("Race"));
        editor.SetMapStyle("Day");
        editor.SaveMap("Example.Map.Gbx", "Maps");
        Assert.Equal("Race", editor.GetMapType());
        Assert.Equal("Day", editor.GetMapStyle());
        Assert.Equal("Example.Map", editor.MapName);
        Assert.Contains("Example.Map.Gbx", editor.MapFileName);
    }

    [Fact]
    public void SkinChangesAndClipListsHaveUsableMockState()
    {
        var editor = new CMapEditorPlugin();
        var model = new CBlockModel();
        var coord = new Int3(1, 0, 1);
        Assert.True(editor.PlaceBlock(model, coord, CMapEditorPlugin.CardinalDirections.North));
        var block = editor.GetBlock(coord);

        editor.SetBlockSkin(block, "Skins/Blue.zip");
        Assert.Equal("Skins/Blue.zip", editor.GetBlockSkin(block));
        Assert.Equal("Blue", editor.GetSkinDisplayName("Skins/Blue.zip"));
        Assert.True(editor.Undo());
        Assert.Equal(string.Empty, editor.GetBlockSkin(block));

        var clips = editor.CreateFrameClipList();
        Assert.NotNull(clips.Clips);
        Assert.Contains(clips, editor.FrameClipLists);
    }
}
