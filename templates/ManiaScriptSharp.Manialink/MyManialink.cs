using ManiaScriptSharp;
using static ManiaScriptSharp.ManiaScript;

namespace MyManialink;

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
