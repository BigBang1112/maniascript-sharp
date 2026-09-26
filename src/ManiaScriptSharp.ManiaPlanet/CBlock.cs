namespace ManiaScriptSharp;

public partial class CBlock
{
    public CBlock() { }

    internal CBlock(CBlockModel model, Int3 coord, CMapEditorPlugin.CardinalDirections direction)
    {
        BlockModel = model;
        Coord = coord;
        Dir = direction;
        Direction = (CardinalDirections)direction;
    }
}
