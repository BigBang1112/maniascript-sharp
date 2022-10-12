namespace ManiaScriptSharp;

public interface IContext
{
    bool LoopCondition => true;
    void Main();
    void Loop();
}
