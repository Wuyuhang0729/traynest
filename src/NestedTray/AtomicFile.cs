namespace NestedTray;

internal static class AtomicFile
{
    internal static void Commit(string temporary,string destination)
    {
        // File scanners may briefly hold a just-written destination without
        // delete sharing. Keep both files intact and retry only the rename.
        for(int attempt=0;;attempt++)
        {
            try{File.Move(temporary,destination,true);return;}
            catch(IOException error)when(attempt<10&&(error.HResult&0xffff) is 5 or 32 or 33){Thread.Sleep(50);}
            catch(UnauthorizedAccessException)when(attempt<10){Thread.Sleep(50);}
        }
    }
}
