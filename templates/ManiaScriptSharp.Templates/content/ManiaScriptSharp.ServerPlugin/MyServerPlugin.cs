using ManiaScriptSharp;
using static ManiaScriptSharp.ManiaScript;

namespace MyServerPlugin;

// Building this project emits ManiaScript/MyServerPlugin.Script.txt next to it.
public class MyServerPlugin : CServerPlugin, IContext
{
    public void Main()
    {
        Log("MyServerPlugin starting...");
    }

    public void Loop()
    {
    }
}
