namespace TrayNest.Installation;

public static class InstallationPaths
{
    public static bool Overlaps(string a,string b)=>a.Equals(b,StringComparison.OrdinalIgnoreCase)||a.StartsWith(b.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)||b.StartsWith(a.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase);
    public static void ValidateParents(string path)
    {for(DirectoryInfo? part=new DirectoryInfo(path);part is not null;part=part.Parent)if(part.Exists)Package.RejectReparse(part.FullName);}
}
