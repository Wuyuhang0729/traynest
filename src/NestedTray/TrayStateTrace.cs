namespace NestedTray;

// Correlates one real Shell_NotifyIcon state write with packets observed by our
// receiver. Absence of a packet is evidence of routing, not an Explorer reply.
internal sealed class TrayStateTrace(IconRecord source,uint state,nint receiver,nint explorer)
{
    private readonly nint sourceWindow=source.Window;
    private readonly uint sourceId=source.Id;
    private readonly Guid sourceGuid=source.Guid;
    private readonly int sourceProcess=source.ProcessId;
    private readonly long started=Environment.TickCount64;
    private readonly nint candidate=Native.FindWindowW("Shell_TrayWnd",null);
    private int received,replies,zeroReplies,timeouts,suppressed;

    internal bool Matches(TrayPacket packet)=>packet.Operation==1&&packet.Window==sourceWindow&&packet.Id==sourceId
        &&packet.Guid==sourceGuid&&(packet.Flags&8)!=0;
    internal void Received()=>received++;
    internal void Forwarded(bool success,nuint reply)
    {if(!success)timeouts++;else{replies++;if(reply==0)zeroReplies++;}}
    internal void Suppressed()=>suppressed++;
    internal Dictionary<string,string> Failure(bool accepted,bool pending,bool retired)=>new()
    {
        ["Operation"]=retired?"StateWriteSupersededByDelete":"StateNativeFailure",["Process"]=Environment.ProcessId.ToString(),
        ["Accepted"]=accepted.ToString(),["DurationMs"]=(Environment.TickCount64-started).ToString(),
        ["Source"]=Endpoint(sourceWindow),["ExpectedSourcePid"]=sourceProcess.ToString(),
        ["IconId"]=sourceId.ToString(),["RecordRetired"]=retired.ToString(),["State"]=state.ToString(),
        ["Receiver"]=Endpoint(receiver),["Explorer"]=Endpoint(explorer),
        ["CandidateBefore"]=Endpoint(candidate),["CandidateAfter"]=Endpoint(Native.FindWindowW("Shell_TrayWnd",null)),
        ["ReceivedPackets"]=received.ToString(),
        ["Forwarding"]=$"replies={replies},zero={zeroReplies},timeouts={timeouts},suppressed={suppressed}",
        ["Pending"]=pending.ToString()
    };
    internal static string Endpoint(nint window)
    {
        Native.GetWindowThreadProcessId(window,out uint owner);
        return $"hwnd={window},pid={owner},alive={Native.IsWindow(window)}";
    }
}
