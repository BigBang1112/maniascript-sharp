namespace ManiaScriptSharp;

public partial class CXmlRpc
{
    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void PendingEventHandler(CXmlRpcEvent e);

    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void CallbackEventHandler([MemberName("Param1")] string method,
        [MemberName("Param2")] string parameter);

    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void CallbackArrayEventHandler([MemberName("ParamArray1")] string method,
        [MemberName("ParamArray2")] string[] parameters);
}
