using ManiaScriptSharp;
using static ManiaScriptSharp.ManiaScript;

namespace MyMapEditorPlugin;

// Building this project emits ManiaScript/MyMapEditorPlugin.Script.txt next to it.
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
