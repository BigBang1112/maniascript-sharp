using ManiaScriptSharp;
using static ManiaScriptSharp.ManiaScript;

namespace MyMapEditorPlugin;

public class MyMapEditorPlugin : CMapEditorPlugin, IContext
{
    public void Main()
    {
        Log("MyMapEditorPlugin starting...");
    }

    public void Loop()
    {
    }
}
