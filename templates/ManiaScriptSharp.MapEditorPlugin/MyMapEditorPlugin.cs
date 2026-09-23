using ManiaScriptSharp;
using static ManiaScriptSharp.ManiaScript;

namespace MyMapEditorPlugin;

#if (IsManiaPlanet3)
public class MyMapEditorPlugin : CEditorPlugin, IContext
#else
public class MyMapEditorPlugin : CMapEditorPlugin, IContext
#endif
{
    public void Main()
    {
        Log("MyMapEditorPlugin starting...");
    }

    public void Loop()
    {
    }
}
