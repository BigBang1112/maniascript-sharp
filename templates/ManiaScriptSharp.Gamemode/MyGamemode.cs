using ManiaScriptSharp;
using static ManiaScriptSharp.ManiaScript;

namespace MyGamemode;

#if (UseCSmMode)
public class MyGamemode : CSmMode, IContext
#else
public class MyGamemode : CTmMode, IContext
#endif
{
    [Setting(As = "Round time (s)")]
    public const int RoundTime = 180;

    private int _score;

    public void Main()
    {
        Log("MyGamemode starting...");
    }

    public void Loop()
    {
        if (_score >= 100)
        {
            Log("Round over");
        }
    }
}
