using ManiaScriptSharp;
using static ManiaScriptSharp.ManiaScript;

namespace MyMapType;

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
