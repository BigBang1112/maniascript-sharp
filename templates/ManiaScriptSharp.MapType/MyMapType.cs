using ManiaScriptSharp;
using static ManiaScriptSharp.ManiaScript;

namespace MyMapType;

// Building this project emits ManiaScript/MyMapType.Script.txt next to it.
#if (UseCSmMapType)
public class MyMapType : CSmMapType, IContext
#else
public class MyMapType : CTmMapType, IContext
#endif
{
    public void Main()
    {
        Log("MyMapType starting...");
    }

    public void Loop()
    {
    }
}
