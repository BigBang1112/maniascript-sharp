using ManiaScriptSharp;
using ManiaScriptSharp.Scripts.Libs.Nadeo;

namespace MyMode;

public class MyLib : ILib<CMap>
{
    public required TextLib TL;

    public required Layers2 LibLayers;

    public required CMap Context { get; init; }

    public string Nice()
    {
        return TL.StripFormatting(Context.AuthorNickName);
    }
}
