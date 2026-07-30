namespace ManiaScriptSharp;

/// <summary>
/// Marker interface for classes that represent a ManiaScript context.
/// A class implementing IContext gets a <c>main()</c> function generated,
/// with <c>Main()</c> running once and <c>Loop()</c> wrapped in <c>while(True) { yield; ... }</c>.
/// Apply <see cref="NoLoopAttribute"/> to suppress the <c>while(True)</c> wrapper entirely
/// (e.g. for one-off scripts) — <c>Loop()</c> is then never emitted.
/// </summary>
public interface IContext
{
    void Main();
    void Loop();
}
