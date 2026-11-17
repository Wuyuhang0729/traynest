namespace NestedTray;

internal static class EntryRegistration
{
    // A successful add alone is insufficient: callbacks require version 4.
    // Roll back a partial registration before another attempt using the same identity.
    internal static bool Attempt(Func<uint,bool> notify)
    {
        if(!notify(0))return false;
        if(notify(4))return true;
        notify(2);return false;
    }
}
