using ManiaScriptSharp;
using static ManiaScriptSharp.ManiaScript;

namespace MyManialink;

// Building this project emits ManiaScript/MyManialink.Script.txt next to it, merged with
// MyManialink.xml (auto-detected by matching file name) into the final manialink markup.
#if (UseCSmMode)
public class MyManialink : CSmMlScriptIngame, IContext
#else
public class MyManialink : CTmMlScriptIngame, IContext
#endif
{
    [ManialinkControl]
    public required CMlLabel LabelHello;

    public void Main()
    {
        LabelHello.Value = "Hello from MyManialink!";
    }

    public void Loop()
    {
    }
}
