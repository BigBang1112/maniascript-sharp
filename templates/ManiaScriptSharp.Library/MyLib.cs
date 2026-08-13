using ManiaScriptSharp;

namespace MyLib;

// Building this project emits ManiaScript/MyLib.Script.txt next to it.
// A library has no Main()/Loop() of its own — attach it to a context class with a
// `public required MyLib Field;` member, then call its methods from that context.
public class MyLib : ILib<CNod>
{
    public required CNod Context { get; init; }

    public string Greet()
    {
        return "Hello from MyLib!";
    }
}
