namespace Sample;

public class Gamemode : CTmMode, IContext
{
    public struct Player
    {
        public string Text;
        public string Icon;
    }

    [Setting(As = "Chat time")]
    public const int ChatTime = 50;

    public int Ggsdg = 69;

    public void SpawnAllWaitingPlayers(int team, int raceStartTime)
    {
        foreach (var player in PlayersWaiting)
        {
            SpawnPlayer(player, team, raceStartTime);
        }
    }

    protected override void OnPendingEvent(CTmModeEvent e)
    {
        base.OnPendingEvent(e);
    }

    protected override void OnStartLine(CTmPlayer player)
    {
        base.OnStartLine(player);
    }

    protected override void OnStunt(CTmPlayer player, int raceTime, CTmModeEvent.EStuntFigure stuntFigure, int angle, int points, float factor, int combo,
        bool isStraight, bool isReverse, bool isPerfectLanding, bool isMasterJump, bool isMasterLanding,
        bool isEpicLanding)
    {
        base.OnStunt(player, raceTime, stuntFigure, angle, points, factor, combo, isStraight, isReverse, isPerfectLanding, isMasterJump, isMasterLanding, isEpicLanding);
    }

    public void Main()
    {
        Log("bro");
    }

    public void Loop()
    {
        
    }
}
