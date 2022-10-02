namespace SampleFSharp

open ManiaScriptSharp
open type ManiaScriptSharp.ManiaScript

type Gamemode =
    inherit CTmMode
    interface IContext with
        member this.Main(): unit = 
            Log("bro");
        member this.Loop(): unit = 
            Log("bro");
