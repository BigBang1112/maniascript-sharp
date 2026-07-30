namespace ManiaScriptSharp;

/// <summary>
/// Marker interface for classes that represent a ManiaScript context.
/// A class implementing IContext gets a <c>main()</c> function generated,
/// with <c>Main()</c> running once and <c>Loop()</c> wrapped in <c>while(True) { yield; ... }</c>.
/// </summary>
public interface IContext
{
    void Main();
    void Loop();
}
