using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ManiaScriptSharp;

public partial class CMapEditorPlugin
{
    private bool CanPlaceMacroblockCore(CMacroblockModel model, Int3 coord, bool noDestruction) =>
        model is not null && coord.X >= 0 && coord.Y >= 0 && coord.Z >= 0
        && (!noDestruction || GetMacroblockInstanceFromUnitCoord(coord) is null);

    private bool PlaceMacroblockCore(CMacroblockModel model, Int3 coord, CardinalDirections dir,
        bool noDestruction, bool unvalidate)
    {
        if (!CanPlaceMacroblockCore(model, coord, noDestruction)) return false;
        Remember(unvalidate);
        var previous = GetMacroblockInstanceFromUnitCoord(coord);
        if (previous is not null) MacroblockInstances.Remove(previous);
        AddMacroblock(model, coord, dir, null, 0);
        return true;
    }

    private CMacroblockInstance AddMacroblock(CMacroblockModel model, Int3 coord, CardinalDirections dir,
        CBlockClipList? clipList, int userData)
    {
        var instance = new CMacroblockInstance(model, coord, dir, clipList, GetMaxOrder() + 1, userData);
        MacroblockInstances.Add(instance);
        return instance;
    }

    public partial bool CanPlaceMacroblock(CMacroblockModel MacroblockModel, Int3 Coord, CardinalDirections Dir) =>
        CanPlaceMacroblockCore(MacroblockModel, Coord, false);
    public partial bool PlaceMacroblock(CMacroblockModel MacroblockModel, Int3 Coord, CardinalDirections Dir) =>
        PlaceMacroblockCore(MacroblockModel, Coord, Dir, false, true);
    public partial bool CanPlaceMacroblock_NoDestruction(CMacroblockModel MacroblockModel, Int3 Coord, CardinalDirections Dir) =>
        CanPlaceMacroblockCore(MacroblockModel, Coord, true);
    public partial bool PlaceMacroblock_NoDestruction(CMacroblockModel MacroblockModel, Int3 Coord, CardinalDirections Dir) =>
        PlaceMacroblockCore(MacroblockModel, Coord, Dir, true, true);
    public partial bool CanPlaceMacroblock_NoTerrain(CMacroblockModel MacroblockModel, Int3 Coord, CardinalDirections Dir) =>
        CanPlaceMacroblockCore(MacroblockModel, Coord, false);
    public partial bool PlaceMacroblock_NoTerrain(CMacroblockModel MacroblockModel, Int3 Coord, CardinalDirections Dir) =>
        PlaceMacroblockCore(MacroblockModel, Coord, Dir, false, true);
    public partial bool PlaceMacroblock_NoTerrain_NoUnvalidate(CMacroblockModel MacroblockModel, Int3 Coord, CardinalDirections Dir) =>
        PlaceMacroblockCore(MacroblockModel, Coord, Dir, false, false);

    private bool RemoveMacroblockCore(CMacroblockModel model, Int3 coord, CardinalDirections dir, bool unvalidate)
    {
        var instance = MacroblockInstances.FirstOrDefault(m => ReferenceEquals(m.MacroblockModel, model)
            && m.Coord == coord && m.Dir == dir);
        if (instance is null) return false;
        Remember(unvalidate);
        MacroblockInstances.Remove(instance);
        _macroblockSkins.Remove(instance);
        return true;
    }

    public partial bool RemoveMacroblock(CMacroblockModel MacroblockModel, Int3 Coord, CardinalDirections Dir) =>
        RemoveMacroblockCore(MacroblockModel, Coord, Dir, true);
    public partial bool RemoveMacroblock_NoTerrain(CMacroblockModel MacroblockModel, Int3 Coord, CardinalDirections Dir) =>
        RemoveMacroblockCore(MacroblockModel, Coord, Dir, true);
    public partial bool RemoveMacroblock_NoTerrain_NoUnvalidate(CMacroblockModel MacroblockModel, Int3 Coord, CardinalDirections Dir) =>
        RemoveMacroblockCore(MacroblockModel, Coord, Dir, false);

    public partial CMacroblockInstance CreateMacroblockInstance(CMacroblockModel MacroblockModel, Int3 Coord, CardinalDirections Dir) =>
        CreateMacroblockInstance(MacroblockModel, Coord, Dir, null!, 0);
    public partial CMacroblockInstance CreateMacroblockInstance(CMacroblockModel MacroblockModel, Int3 Coord, CardinalDirections Dir, int UserData) =>
        CreateMacroblockInstance(MacroblockModel, Coord, Dir, null!, UserData);
    public partial CMacroblockInstance CreateMacroblockInstance(CMacroblockModel MacroblockModel, Int3 Coord, CardinalDirections Dir, CBlockClipList DefaultClipList) =>
        CreateMacroblockInstance(MacroblockModel, Coord, Dir, DefaultClipList, 0);
    public partial CMacroblockInstance CreateMacroblockInstance(CMacroblockModel MacroblockModel, Int3 Coord, CardinalDirections Dir, CBlockClipList DefaultClipList, int UserData)
    {
        if (!CanPlaceMacroblockCore(MacroblockModel, Coord, true)) return null!;
        Remember();
        return AddMacroblock(MacroblockModel, Coord, Dir, DefaultClipList, UserData);
    }
    public partial CMacroblockInstance GetMacroblockInstanceFromOrder(int Order) =>
        MacroblockInstances.FirstOrDefault(m => m.Order == Order)!;
    public partial CMacroblockInstance GetMacroblockInstanceFromUnitCoord(Int3 Coord) =>
        MacroblockInstances.FirstOrDefault(m => m.UnitCoords.Contains(Coord))!;
    public partial CMacroblockInstance GetLatestMacroblockInstance() => GetLatestMacroblockInstance(0);
    public partial CMacroblockInstance GetLatestMacroblockInstance(int Offset) =>
        Offset < 0 ? null! : MacroblockInstances.OrderByDescending(m => m.Order).Skip(Offset).FirstOrDefault()!;
    public partial CMacroblockInstance GetMacroblockInstanceConnectedToClip(CBlockClip Clip) =>
        MacroblockInstances.FirstOrDefault(m => m.ClipList?.Clips?.Contains(Clip) == true)!;
    public partial bool RemoveMacroblockInstance(CMacroblockInstance MacroblockInstance)
    {
        if (MacroblockInstance is null || !MacroblockInstances.Contains(MacroblockInstance)) return false;
        Remember();
        MacroblockInstances.Remove(MacroblockInstance);
        _macroblockSkins.Remove(MacroblockInstance);
        return true;
    }
    public partial bool RemoveMacroblockInstanceFromOrder(int Order) =>
        RemoveMacroblockInstance(GetMacroblockInstanceFromOrder(Order));
    public partial bool RemoveMacroblockInstanceFromUnitCoord(int Order) =>
        RemoveMacroblockInstanceFromOrder(Order);
    public partial bool RemoveMacroblockInstancesByUserData(int UserData)
    {
        var matches = MacroblockInstances.Where(m => m.UserData == UserData).ToArray();
        if (matches.Length == 0) return false;
        Remember();
        foreach (var instance in matches)
        {
            MacroblockInstances.Remove(instance);
            _macroblockSkins.Remove(instance);
        }
        return true;
    }
    public partial void ResetAllMacroblockInstances()
    {
        if (MacroblockInstances.Count == 0) return;
        Remember();
        MacroblockInstances.Clear();
        _macroblockSkins.Clear();
    }
    public partial int GetMaxOrder() => MacroblockInstances.Select(m => m.Order).DefaultIfEmpty(-1).Max();
    public partial void SaveMacroblock(CMacroblockModel MacroblockModel)
    {
        if (MacroblockModel is not null && !MacroblockModels.Contains(MacroblockModel))
            MacroblockModels.Add(MacroblockModel);
    }
    public partial CMacroblockModel GetMacroblockModelFromFilePath(string MacroblockModelFilePath)
    {
        var name = Path.GetFileNameWithoutExtension(MacroblockModelFilePath);
        return MacroblockModels.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))!;
    }

    private void AddConnectResults(Int3 origin, Func<Int3, CardinalDirections, bool> canPlace)
    {
        ConnectResults.Clear();
        foreach (var (coord, direction) in new[]
        {
            (new Int3(origin.X, origin.Y, origin.Z - 1), CardinalDirections.North),
            (new Int3(origin.X + 1, origin.Y, origin.Z), CardinalDirections.East),
            (new Int3(origin.X, origin.Y, origin.Z + 1), CardinalDirections.South),
            (new Int3(origin.X - 1, origin.Y, origin.Z), CardinalDirections.West),
        })
            ConnectResults.Add(new CMapEditorConnectResults
            {
                Coord = coord,
                Dir = direction,
                CanPlace = canPlace(coord, direction),
            });
    }
    public partial void GetConnectResults(CBlock ExistingBlock, CBlockModel NewBlock)
    {
        if (ExistingBlock is null) { ConnectResults.Clear(); return; }
        AddConnectResults(ExistingBlock.Coord, (coord, dir) =>
            CanPlaceBlock_NoDestruction(NewBlock, coord, dir, true, 0));
    }
    public partial void GetConnectResults(CBlock ExistingBlock, CMacroblockModel NewBlock)
    {
        if (ExistingBlock is null) { ConnectResults.Clear(); return; }
        AddConnectResults(ExistingBlock.Coord, (coord, dir) =>
            CanPlaceMacroblock_NoDestruction(NewBlock, coord, dir));
    }
    public partial void GetConnectResults(CMacroblockInstance ExistingBlock, CBlockModel NewBlock)
    {
        if (ExistingBlock is null) { ConnectResults.Clear(); return; }
        AddConnectResults(ExistingBlock.Coord, (coord, dir) =>
            CanPlaceBlock_NoDestruction(NewBlock, coord, dir, true, 0));
    }
    public partial void GetConnectResults(CMacroblockInstance ExistingBlock, CMacroblockModel NewBlock)
    {
        if (ExistingBlock is null) { ConnectResults.Clear(); return; }
        AddConnectResults(ExistingBlock.Coord, (coord, dir) =>
            CanPlaceMacroblock_NoDestruction(NewBlock, coord, dir));
    }
}
