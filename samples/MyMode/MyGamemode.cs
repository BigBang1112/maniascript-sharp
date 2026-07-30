using ManiaScriptSharp;
using static ManiaScriptSharp.ManiaScript;

namespace MyMode;

/// <summary>
/// Demonstrates the ManiaScriptSharp pipeline. When you save this file the
/// generator emits <c>out/MyMode/MyGamemode.Script.txt</c> in real time.
/// </summary>
public class MyGamemode : CTmMode, IContext
{
    public required TextLib TL;

    public required MyLib MyLib;

    // ----- #Const -----
    public const int MaxPlayers = 16;

    // ----- #Setting -----
    [Setting(As = "Round time (s)")]
    public const int RoundTime = 180;

    [Setting(As = "Allow respawn")]
    public const bool AllowRespawn = true;

    // ----- declare -----
    private int _score;
    private string _state = "Idle";

    public void Main()
    {
        MyLib.Nice();

        Log("Mode starting...");
        _state = "Running";

        for (int i = 0; i < MaxPlayers; i++)
        {
            Log($"Slot {i} reserved");
            Log($"""
                Mega string
                {i} reserved
                """);
        }
    }

    public void Loop()
    {
        if (_score >= 100)
        {
            _state = "Finished";
            Log("Round over");
        }
    }

    private static int ComputeBonus(int basePoints)
    {
        if (basePoints is > 0 and < 50) return basePoints * 2;
        if (basePoints is >= 50) return basePoints + 10;
        return 0;
    }
}
