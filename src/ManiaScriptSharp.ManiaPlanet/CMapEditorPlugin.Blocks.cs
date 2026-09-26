using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ManiaScriptSharp;

public partial class CMapEditorPlugin
{
    private bool CanPlaceBlockCore(CBlockModel model, Int3 coord, bool noDestruction) =>
        model is not null && coord.X >= 0 && coord.Y >= 0 && coord.Z >= 0
        && (!noDestruction || GetBlock(coord) is null);

    private bool PlaceBlockCore(CBlockModel model, Int3 coord, CardinalDirections dir, bool terrain, bool noDestruction)
    {
        if (!CanPlaceBlockCore(model, coord, noDestruction)) return false;
        Remember();
        var previous = GetBlock(coord);
        if (previous is not null) RemoveBlockCore(previous);
        var block = new CBlock(model, coord, dir);
        Blocks.Add(block);
        (terrain ? TerrainBlocks : ClassicBlocks).Add(block);
        return true;
    }

    private void RemoveBlockCore(CBlock block)
    {
        Blocks.Remove(block);
        ClassicBlocks.Remove(block);
        TerrainBlocks.Remove(block);
        _blockSkins.Remove(block);
    }

    public partial bool CanPlaceBlock(CBlockModel BlockModel, Int3 Coord, CardinalDirections Dir, bool OnGround, int VariantIndex) =>
        VariantIndex >= 0 && CanPlaceBlockCore(BlockModel, Coord, false);
    public partial bool PlaceBlock(CBlockModel BlockModel, Int3 Coord, CardinalDirections Dir) =>
        PlaceBlockCore(BlockModel, Coord, Dir, false, false);
    public partial bool CanPlaceBlock_NoDestruction(CBlockModel BlockModel, Int3 Coord, CardinalDirections Dir, bool OnGround, int VariantIndex) =>
        VariantIndex >= 0 && CanPlaceBlockCore(BlockModel, Coord, true);
    public partial bool PlaceBlock_NoDestruction(CBlockModel BlockModel, Int3 Coord, CardinalDirections Dir) =>
        PlaceBlockCore(BlockModel, Coord, Dir, false, true);

    public partial bool CanPlaceRoadBlocks(CBlockModel BlockModel, Int3 StartCoord, Int3 EndCoord) =>
        BlockModel is not null && Rectangle(StartCoord, EndCoord).Any();
    public partial bool PlaceRoadBlocks(CBlockModel BlockModel, Int3 StartCoord, Int3 EndCoord) =>
        PlaceBlocksInRectangle(BlockModel, StartCoord, EndCoord, false, false);
    public partial bool CanPlaceTerrainBlocks(CBlockModel BlockModel, Int3 StartCoord, Int3 EndCoord) =>
        BlockModel is not null && Rectangle(StartCoord, EndCoord).Any();
    public partial bool PlaceTerrainBlocks(CBlockModel BlockModel, Int3 StartCoord, Int3 EndCoord) =>
        PlaceBlocksInRectangle(BlockModel, StartCoord, EndCoord, true, false);
    public partial bool PlaceTerrainBlocks_NoDestruction(CBlockModel BlockModel, Int3 StartCoord, Int3 EndCoord) =>
        PlaceBlocksInRectangle(BlockModel, StartCoord, EndCoord, true, true);

    private bool PlaceBlocksInRectangle(CBlockModel model, Int3 start, Int3 end, bool terrain, bool noDestruction)
    {
        var coords = Rectangle(start, end).ToArray();
        if (coords.Length == 0 || model is null || coords.Any(c => !CanPlaceBlockCore(model, c, noDestruction))) return false;
        Remember();
        foreach (var coord in coords)
        {
            var previous = GetBlock(coord);
            if (previous is not null) RemoveBlockCore(previous);
            var block = new CBlock(model, coord, CardinalDirections.North);
            Blocks.Add(block);
            (terrain ? TerrainBlocks : ClassicBlocks).Add(block);
        }
        return true;
    }

    public partial CBlock GetBlock(Int3 Coord) => Blocks.FirstOrDefault(b => b.Coord == Coord)!;
    public partial CBlock GetBlock(CBlockModel BlockModel, Int3 Coord, CardinalDirections Dir) =>
        Blocks.FirstOrDefault(b => ReferenceEquals(b.BlockModel, BlockModel) && b.Coord == Coord && b.Dir == Dir)!;
    public partial bool RemoveBlock(Int3 Coord)
    {
        var block = GetBlock(Coord);
        if (block is null) return false;
        Remember();
        RemoveBlockCore(block);
        return true;
    }
    public partial bool RemoveBlock(CBlockModel BlockModel, Int3 Coord, CardinalDirections Dir)
    {
        var block = GetBlock(BlockModel, Coord, Dir);
        if (block is null) return false;
        Remember();
        RemoveBlockCore(block);
        return true;
    }
    public partial bool RemoveTerrainBlocks(Int3 StartCoord, Int3 EndCoord)
    {
        var coords = new HashSet<Int3>(Rectangle(StartCoord, EndCoord));
        var removed = TerrainBlocks.Where(b => coords.Contains(b.Coord)).ToArray();
        if (removed.Length == 0) return false;
        Remember();
        foreach (var block in removed) RemoveBlockCore(block);
        return true;
    }

    public partial void RemoveAllBlocks()
    {
        if (ClassicBlocks.Count == 0) return;
        Remember();
        foreach (var block in ClassicBlocks.ToArray()) RemoveBlockCore(block);
    }
    public partial void RemoveAllTerrain()
    {
        if (TerrainBlocks.Count == 0) return;
        Remember();
        foreach (var block in TerrainBlocks.ToArray()) RemoveBlockCore(block);
    }
    public partial void RemoveAllOffZone() { }
    public partial void RemoveAllObjects()
    {
        if (Items.Count + AnchorData.Count + MacroblockInstances.Count == 0) return;
        Remember();
        Items.Clear();
        AnchorData.Clear();
        MacroblockInstances.Clear();
    }
    public partial void RemoveAllBlocksAndTerrain()
    {
        if (Blocks.Count == 0) return;
        Remember();
        Blocks.Clear();
        ClassicBlocks.Clear();
        TerrainBlocks.Clear();
        _blockSkins.Clear();
    }
    public partial void RemoveAll()
    {
        if (Blocks.Count + Items.Count + AnchorData.Count + MacroblockInstances.Count == 0) return;
        Remember();
        Blocks.Clear();
        ClassicBlocks.Clear();
        TerrainBlocks.Clear();
        Items.Clear();
        AnchorData.Clear();
        MacroblockInstances.Clear();
        _blockSkins.Clear();
        _macroblockSkins.Clear();
    }

    public partial bool IsBlockModelSkinnable(CBlockModel BlockModel) => BlockModel is not null;
    public partial int GetNbBlockModelSkins(CBlockModel BlockModel) =>
        BlockModel is null ? 0 : _blockSkins.Where(p => ReferenceEquals(p.Key.BlockModel, BlockModel))
            .Select(p => p.Value).Distinct(StringComparer.Ordinal).Count();
    public partial string GetBlockModelSkin(CBlockModel BlockModel, int SkinIndex) =>
        BlockModel is null || SkinIndex < 0 ? string.Empty : _blockSkins
            .Where(p => ReferenceEquals(p.Key.BlockModel, BlockModel)).Select(p => p.Value)
            .Distinct(StringComparer.Ordinal).Skip(SkinIndex).FirstOrDefault() ?? string.Empty;
    public partial string GetSkinDisplayName(string SkinFileName) =>
        string.IsNullOrWhiteSpace(SkinFileName) ? string.Empty : Path.GetFileNameWithoutExtension(SkinFileName);
    public partial string GetBlockSkin(CBlock Block) =>
        Block is not null && _blockSkins.TryGetValue(Block, out var skin) ? skin : string.Empty;
    public partial void SetBlockSkin(CBlock Block, string SkinFileName)
    {
        if (Block is null || !Blocks.Contains(Block)) return;
        Remember();
        _blockSkins[Block] = SkinFileName ?? string.Empty;
    }
    public partial bool IsMacroblockModelSkinnable(CMacroblockModel BlockModel) => BlockModel is not null;
    public partial bool SetMacroblockSkin(CMacroblockInstance Macroblock, string SkinFileName)
    {
        if (Macroblock is null || !MacroblockInstances.Contains(Macroblock)) return false;
        Remember();
        _macroblockSkins[Macroblock] = SkinFileName ?? string.Empty;
        return true;
    }
    public partial bool OpenBlockSkinDialog(CBlock Block) => Block is not null && Blocks.Contains(Block);

    public partial int GetBlockGroundHeight(CBlockModel BlockModel, int CoordX, int CoordZ, CardinalDirections Dir) =>
        GetGroundHeight(CoordX, CoordZ);
    public partial int GetGroundHeight(int CoordX, int CoordZ) => TerrainBlocks
        .Where(b => b.Coord.X == CoordX && b.Coord.Z == CoordZ)
        .Select(b => b.Coord.Y).DefaultIfEmpty(CollectionGroundY).Max();
    public partial Int3 GetMouseCoordOnGround() => new(Cursor.Coord.X, GetGroundHeight(Cursor.Coord.X, Cursor.Coord.Z), Cursor.Coord.Z);
    public partial Int3 GetMouseCoordAtHeight(int CoordY) => new(Cursor.Coord.X, CoordY, Cursor.Coord.Z);
    public partial CBlock GetStartLineBlock() => Blocks.FirstOrDefault(b =>
        b.BlockModel?.Name?.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0)!;
    public partial int GetStartBlockCount(bool IncludeMultilaps) => CountWaypoints("Start", IncludeMultilaps);
    public partial int GetFinishBlockCount(bool IncludeMultilaps) => CountWaypoints("Finish", IncludeMultilaps);
    public partial int GetMultilapBlockCount() => CountWaypoints("Multilap", true);
    public partial int GetCheckpointBlockCount() => CountWaypoints("Checkpoint", true);
    private int CountWaypoints(string name, bool includeMultilaps) => Blocks.Count(b =>
        b.BlockModel?.Name?.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0
        || (includeMultilaps && name != "Multilap" && b.BlockModel?.Name?.IndexOf("Multilap", StringComparison.OrdinalIgnoreCase) >= 0));

    public partial bool RemoveItem(CAnchorData Item)
    {
        if (Item is null || !AnchorData.Contains(Item)) return false;
        Remember();
        AnchorData.Remove(Item);
        return true;
    }
    public partial CBlockModel GetTerrainBlockModelFromName(string TerrainBlockModelName) =>
        TerrainBlockModels.FirstOrDefault(m => string.Equals(m.Name, TerrainBlockModelName, StringComparison.OrdinalIgnoreCase))!;
    public partial CBlockModel GetBlockModelFromName(string BlockModelName) =>
        BlockModels.FirstOrDefault(m => string.Equals(m.Name, BlockModelName, StringComparison.OrdinalIgnoreCase))!;

    public partial CBlockClipList CreateFrameClipList()
    {
        var list = new CBlockClipList();
        FrameClipLists.Add(list);
        return list;
    }
    public partial CBlockClipList CreateFixedClipList()
    {
        var list = new CBlockClipList();
        FixedClipLists.Add(list);
        return list;
    }
}
