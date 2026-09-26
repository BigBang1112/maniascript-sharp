using System.Collections.Generic;

namespace ManiaScriptSharp;

public partial class CMacroblockInstance
{
    public CMacroblockInstance() { }

    internal CMacroblockInstance(CMacroblockModel model, Int3 coord,
        CMapEditorPlugin.CardinalDirections direction, CBlockClipList? clipList, int order, int userData)
    {
        MacroblockModel = model;
        Coord = coord;
        Dir = direction;
        ClipList = clipList!;
        Order = order;
        UserData = userData;
        UnitCoords = new List<Int3> { coord };
    }
}
