using ManiaScriptSharp;
using static ManiaScriptSharp.ManiaScript;

namespace MyServerPlugin;

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
