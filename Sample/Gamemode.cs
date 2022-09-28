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

    public void Execute()
    {
        Log("bro");
    }
}
