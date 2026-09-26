using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ManiaScriptSharp;

public partial class CMapEditorPlugin
{
    private readonly Dictionary<CBlock, string> _blockSkins = new();
    private readonly Dictionary<CMacroblockInstance, string> _macroblockSkins = new();
    private readonly Stack<EditorSnapshot> _undo = new();
    private readonly Stack<EditorSnapshot> _redo = new();
    private List<CBlock> _clipboard = new();
    private string _mapFileName = string.Empty;
    private string _mapType = string.Empty;
    private string _mapStyle = string.Empty;
    private ShadowsQuality _shadowsQuality = ShadowsQuality.NotComputed;
    private EValidationStatus _validationStatus = EValidationStatus.NotValidable;
    private bool _isTesting;
    private bool _isValidating;
    private bool _quitRequested;
    private bool _customSelectionVisible;
    private bool _createdWithPartyEditor;

    public CMapEditorPlugin()
    {
        Map = new CMap { MapName = "New Map" };
        Inventory = new CMapEditorInventory();
        Inventory.RootNodes = new List<CMapEditorInventoryNode>();
        Camera = new CMapEditorCamera();
        Cursor = new CMapEditorCursor();
        PendingEvents = new List<CMapEditorPluginEvent>();
        Blocks = new List<CBlock>();
        ClassicBlocks = new List<CBlock>();
        TerrainBlocks = new List<CBlock>();
        Items = new List<CItemAnchor>();
        AnchorData = new List<CAnchorData>();
        MacroblockInstances = new List<CMacroblockInstance>();
        BlockModels = new List<CBlockModel>();
        TerrainBlockModels = new List<CBlockModel>();
        MacroblockModels = new List<CMacroblockModel>();
        FrameClipLists = new List<CBlockClipList>();
        FixedClipLists = new List<CBlockClipList>();
        MacroblockInstanceClipLists = new List<CBlockClipList>();
        ConnectResults = new List<CMapEditorConnectResults>();
        MediatrackIngameClips = new List<string>();
        CustomSelectionCoords = new List<Int3>();
    }

    public string MapName => Map.MapName;
    public string MapFileName => _mapFileName;
    public bool IsEditorReadyForRequest => !_quitRequested && !_isTesting;
    public ShadowsQuality CurrentShadowsQuality => _shadowsQuality;
    public bool IsUltraShadowsQualityAvailable => true;
    public bool IsTesting => _isTesting;
    public bool IsValidating => _isValidating;
    public EValidationStatus ValidationStatus => _validationStatus;
    public float CollectionSquareSize => 32f;
    public float CollectionSquareHeight => 8f;
    public int CollectionGroundY => 0;

    private sealed class EditorSnapshot
    {
        public CBlock[] Blocks = [];
        public CBlock[] ClassicBlocks = [];
        public CBlock[] TerrainBlocks = [];
        public CMacroblockInstance[] Macroblocks = [];
        public CAnchorData[] Anchors = [];
        public CItemAnchor[] Items = [];
        public Dictionary<CBlock, string> BlockSkins = new();
        public Dictionary<CMacroblockInstance, string> MacroblockSkins = new();
    }

    private EditorSnapshot Snapshot() => new()
    {
        Blocks = Blocks.ToArray(),
        ClassicBlocks = ClassicBlocks.ToArray(),
        TerrainBlocks = TerrainBlocks.ToArray(),
        Macroblocks = MacroblockInstances.ToArray(),
        Anchors = AnchorData.ToArray(),
        Items = Items.ToArray(),
        BlockSkins = new Dictionary<CBlock, string>(_blockSkins),
        MacroblockSkins = new Dictionary<CMacroblockInstance, string>(_macroblockSkins),
    };

    private static void Restore<T>(IList<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    private void Restore(EditorSnapshot snapshot)
    {
        Restore(Blocks, snapshot.Blocks);
        Restore(ClassicBlocks, snapshot.ClassicBlocks);
        Restore(TerrainBlocks, snapshot.TerrainBlocks);
        Restore(MacroblockInstances, snapshot.Macroblocks);
        Restore(AnchorData, snapshot.Anchors);
        Restore(Items, snapshot.Items);
        _blockSkins.Clear();
        foreach (var pair in snapshot.BlockSkins) _blockSkins.Add(pair.Key, pair.Value);
        _macroblockSkins.Clear();
        foreach (var pair in snapshot.MacroblockSkins) _macroblockSkins.Add(pair.Key, pair.Value);
        UnvalidatePlayfield();
    }

    private void Remember(bool unvalidate = true)
    {
        _undo.Push(Snapshot());
        _redo.Clear();
        if (unvalidate) UnvalidatePlayfield();
    }

    public partial bool Undo()
    {
        if (_undo.Count == 0) return false;
        _redo.Push(Snapshot());
        Restore(_undo.Pop());
        return true;
    }

    public partial bool Redo()
    {
        if (_redo.Count == 0) return false;
        _undo.Push(Snapshot());
        Restore(_redo.Pop());
        return true;
    }

    public partial void ComputeShadows() => ComputeShadows(ShadowsQuality.Default);
    public partial void ComputeShadows(ShadowsQuality ShadowsQuality) => _shadowsQuality = ShadowsQuality;
    public partial void DisplayDefaultSetObjectivesDialog() { }
    public partial void Help() { }
    public partial void Validate()
    {
        _isValidating = true;
        _validationStatus = Blocks.Count + MacroblockInstances.Count + AnchorData.Count > 0
            ? EValidationStatus.Validated : EValidationStatus.NotValidable;
        _isValidating = false;
    }
    public partial void AutoSave() => SaveMap(string.IsNullOrWhiteSpace(_mapFileName) ? GetAvailableMapName() : _mapFileName);
    public partial void Quit() { _quitRequested = true; _isTesting = false; }
    public partial void QuickQuit() => Quit();
    public partial void QuitAndSetResult(string Type, IList<string> Data) => Quit();
    public partial void QuickQuitAndSetResult(string Type, IList<string> Data) => Quit();
    public partial void TestMapFromStart() => _isTesting = true;
    public partial void TestMapFromCoord(Int3 Coord, CardinalDirections Dir) => _isTesting = true;
    public partial void TestMapWithMode(string RulesModeName) => TestMapWithMode(RulesModeName, string.Empty);
    public partial void TestMapWithMode(string RulesModeName, string SettingsXml) => _isTesting = true;
    public partial void TestMapWithMode_SplitScreen(string RulesModeName) => TestMapWithMode_SplitScreen(RulesModeName, 2);
    public partial void TestMapWithMode_SplitScreen(string RulesModeName, int ScreenCount) =>
        TestMapWithMode_SplitScreen(RulesModeName, ScreenCount, 0, string.Empty);
    public partial void TestMapWithMode_SplitScreen(string RulesModeName, int ScreenCount, int FakeCount, string SettingsXml) =>
        _isTesting = ScreenCount > 0 && FakeCount >= 0;

    public partial void SaveMap(string FileName) => SaveMap(FileName, string.Empty);
    public partial void SaveMap(string FileName, string Path)
    {
        if (string.IsNullOrWhiteSpace(FileName)) return;
        _mapFileName = string.IsNullOrWhiteSpace(Path) ? FileName : global::System.IO.Path.Combine(Path, FileName);
        Map.MapName = global::System.IO.Path.GetFileNameWithoutExtension(FileName);
    }

    public partial bool SetMapType(string MapType)
    {
        if (string.IsNullOrWhiteSpace(MapType)) return false;
        _mapType = MapType;
        UnvalidateMetadata();
        return true;
    }
    public partial string GetMapType() => _mapType;
    public partial void SetMapStyle(string MapStyle) { _mapStyle = MapStyle ?? string.Empty; UnvalidateMetadata(); }
    public partial string GetMapStyle() => _mapStyle;
    public partial void SetMapIsCreatedWithPartyEditor(bool IsCreatedWithPartyEditor) =>
        _createdWithPartyEditor = IsCreatedWithPartyEditor;
    public partial string GetAvailableMapName() => string.IsNullOrWhiteSpace(MapName) ? "New Map" : MapName;
    public partial Vec3 GetVec3FromCoord(Int3 Coord) => new(
        Coord.X * CollectionSquareSize,
        Coord.Y * CollectionSquareHeight,
        Coord.Z * CollectionSquareSize);
    public partial bool GetRaceCamera(Vec3 Position, float Yaw, float Pitch, float Roll, float FovY)
    {
        if (FovY <= 0 || FovY >= 180) return false;
        ThumbnailCameraPosition = Position;
        ThumbnailCameraHAngle = Yaw;
        ThumbnailCameraVAngle = Pitch;
        ThumbnailCameraRoll = Roll;
        ThumbnailCameraFovY = FovY;
        return true;
    }

    public partial void UnvalidateMetadata() => _validationStatus = EValidationStatus.Validable;
    public partial void UnvalidateGameplayInfo() => _validationStatus = EValidationStatus.Validable;
    public partial void UnvalidatePlayfield() => _validationStatus = EValidationStatus.Validable;

    public partial void ShowCustomSelection() => _customSelectionVisible = true;
    public partial void HideCustomSelection() => _customSelectionVisible = false;
    public partial void CopyPaste_Copy() => _clipboard = Blocks.Where(b => CustomSelectionCoords.Contains(b.Coord)).ToList();
    public partial void CopyPaste_Cut() { CopyPaste_Copy(); CopyPaste_Remove(); }
    public partial void CopyPaste_Remove()
    {
        var selected = Blocks.Where(b => CustomSelectionCoords.Contains(b.Coord)).ToArray();
        if (selected.Length == 0) return;
        Remember();
        foreach (var block in selected) RemoveBlockCore(block);
    }
    public partial void CopyPaste_SelectAll() => CustomSelectionCoords = Blocks.Select(b => b.Coord).Distinct().ToList();
    public partial void CopyPaste_ResetSelection() => CustomSelectionCoords.Clear();
    public partial void CopyPaste_AddOrSubSelection(Int3 StartCoord, Int3 EndCoord)
    {
        foreach (var coord in Rectangle(StartCoord, EndCoord))
            if (!CustomSelectionCoords.Remove(coord)) CustomSelectionCoords.Add(coord);
    }
    public partial bool CopyPaste_Symmetrize()
    {
        if (!_customSelectionVisible || CustomSelectionCoords.Count == 0) return false;
        CustomSelectionCoords = CustomSelectionCoords.Select(c => new Int3(-c.X, c.Y, c.Z)).ToList();
        return true;
    }
    public partial void OpenToolsMenu() { }
    public partial void EditMediatrackIngame() { }
    public partial void PreloadAllBlocks() { }
    public partial void PreloadAllItems() { }

    private static IEnumerable<Int3> Rectangle(Int3 start, Int3 end)
    {
        var minX = Math.Min(start.X, end.X);
        var maxX = Math.Max(start.X, end.X);
        var minZ = Math.Min(start.Z, end.Z);
        var maxZ = Math.Max(start.Z, end.Z);
        var width = (long)maxX - minX + 1;
        var depth = (long)maxZ - minZ + 1;
        if (width > 10_000 || depth > 10_000 || width * depth > 10_000) yield break;
        for (long x = minX; x <= maxX; x++)
            for (long z = minZ; z <= maxZ; z++)
                yield return new Int3((int)x, start.Y, (int)z);
    }
}
