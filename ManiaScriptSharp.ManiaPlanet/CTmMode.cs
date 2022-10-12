namespace ManiaScriptSharp;

public partial class CTmMode
{
    /// <summary>
    /// Any pending event handler.
    /// </summary>
    /// <param name="e">Event.</param>
    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void PendingEventHandler(CTmModeEvent e);
    
    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void StartLineEventHandler(CTmPlayer player);
    
    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void GiveUpEventHandler(CTmPlayer player);
    
    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void WayPointEventHandler(CTmPlayer player, int raceTime, int checkpointInRace, int checkpointInLap,
        bool isEndLap, bool isEndRace, int lapTime, int nbRespawns, Ident blockId, float speed, float distance);
    
    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void RespawnEventHandler(CTmPlayer player, int raceTime, int checkpointInRace, int checkpointInLap,
        int lapTime, int nbRespawns, Ident blockId, float distance);
    
    [ManiaScriptEvent(nameof(PendingEvents))]
    public delegate void StuntEventHandler(CTmPlayer player, int raceTime, CTmModeEvent.EStuntFigure stuntFigure,
        int angle, int points, float factor, int combo, bool isStraight, bool isReverse, bool isPerfectLanding,
        bool isMasterJump, bool isMasterLanding, bool isEpicLanding);
    
    [ManiaScriptEvent(nameof(PendingEvents)), ActualEventName("OnPlayerAdded")]
    public delegate void PlayerAddedEventHandler(CTmPlayer player);
    
    [ManiaScriptEvent(nameof(PendingEvents)), ActualEventName("OnPlayerRemoved")]
    public delegate void PlayerRemovedEventHandler(CTmPlayer player);
}