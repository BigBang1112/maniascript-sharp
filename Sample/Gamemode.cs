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

    public void Main()
    {
        Log("bro");
    }

    public void SpawnAllWaitingPlayers(int team, int raceStartTime)
    {
        foreach (var player in PlayersWaiting)
        {
            SpawnPlayer(player, team, raceStartTime);
        }
    }
}
