namespace NestedTray;

internal enum AutoExpandReason { None, NativeOverflow, Hover }

// Consumes inexpensive window/cursor state. Never queries Explorer's UIA tree.
internal sealed class AutoExpand
{
    private bool observedNative,wasHovering,followHandled,hoverHandled;
    private long hoverSince;
    internal void NativeHidden(){observedNative=false;followHandled=false;}
    internal const int HoverDelayMs=450;
    internal AutoExpandReason Evaluate(long now,bool follow,bool hover,bool nativeVisible,bool panelVisible,bool overEntry,bool blocked)
    {
        if(!nativeVisible||!follow)followHandled=false;
        if(!overEntry||!hover){wasHovering=false;hoverHandled=false;}
        else if(!wasHovering){wasHovering=true;hoverSince=now;}
        if(nativeVisible&&!observedNative)followHandled=false;
        observedNative=nativeVisible;
        if(blocked)return AutoExpandReason.None;
        if(panelVisible)
        {
            if(nativeVisible)followHandled=true;
            if(overEntry)hoverHandled=true;
            return AutoExpandReason.None;
        }
        if(follow&&nativeVisible&&!followHandled)
        {followHandled=true;if(overEntry)hoverHandled=true;return AutoExpandReason.NativeOverflow;}
        if(hover&&overEntry&&!hoverHandled&&now-hoverSince>=HoverDelayMs)
        {hoverHandled=true;return AutoExpandReason.Hover;}
        return AutoExpandReason.None;
    }
    internal void SuppressCurrentCycle(bool nativeVisible)
    {if(nativeVisible)followHandled=true;hoverHandled=true;}
}
