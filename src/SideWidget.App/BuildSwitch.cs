using System.Diagnostics;
using System.Text.Json;
using SideWidget.Infrastructure;

namespace SideWidget.App;

static class BuildSwitch
{
    const string ExecutableName="SideWidget.App.exe";
    static readonly StringComparer Paths=StringComparer.OrdinalIgnoreCase;

    public static ProcessStartInfo? FindNewer(string current)
    {
        var currentFile=new FileInfo(Path.GetFullPath(current));
        var currentManifest=new FileInfo(Path.Combine(currentFile.DirectoryName!,DistributionArchive.ManifestName));
        var currentTime=currentManifest.Exists?currentManifest.LastWriteTimeUtc:currentFile.LastWriteTimeUtc;
        var parent=currentFile.Directory!.Parent;
        if(parent is null)return null;

        var directories=parent.Name.Equals("artifacts",StringComparison.OrdinalIgnoreCase)
            ? new[]{Path.Combine(parent.FullName,"release")}
            : SiblingBuilds(parent.FullName);
        if(Paths.Equals(parent.Name,"output")&&parent.Parent is {} outputProject)
            directories=directories.Append(Path.Combine(outputProject.FullName,"artifacts","release"));
        if(Paths.Equals(parent.Name,"artifacts")&&parent.Parent is {} artifactsProject)
            directories=directories.Concat(SiblingBuilds(Path.Combine(artifactsProject.FullName,"output")));

        var candidates=directories.Distinct(Paths)
            .Select(directory=>new FileInfo(Path.Combine(directory,DistributionArchive.ManifestName)))
            .Where(manifest=>manifest.Exists&&manifest.LastWriteTimeUtc>currentTime)
            .OrderByDescending(manifest=>manifest.LastWriteTimeUtc).ThenBy(manifest=>manifest.FullName,Paths);
        foreach(var manifest in candidates)
        {
            var executable=Path.Combine(manifest.DirectoryName!,ExecutableName);
            if(Paths.Equals(executable,currentFile.FullName))continue;
            try{return Validate(executable,currentFile.FullName);}
            catch(Exception error) when(error is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or KeyNotFoundException or InvalidOperationException)
            {
                // A partial or damaged publication must not hide a usable build.
            }
        }
        return null;
    }

    static IEnumerable<string> SiblingBuilds(string directory)=>Directory.Exists(directory)
        ? Directory.GetDirectories(directory,"*",new EnumerationOptions{IgnoreInaccessible=true,AttributesToSkip=FileAttributes.ReparsePoint})
        : [];

    public static ProcessStartInfo Validate(string selected, string current)
    {
        var path=Path.GetFullPath(selected);
        if(!Paths.Equals(Path.GetFileName(path),ExecutableName))
            throw new InvalidDataException("Выберите файл SideWidget.App.exe.");
        if(path.Equals(Path.GetFullPath(current),StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Эта сборка уже запущена. Выберите SideWidget.App.exe из другой папки.");
        var directory=Path.GetDirectoryName(path)!;
        DistributionArchive.Validate(directory);
        return new ProcessStartInfo(path){UseShellExecute=false,WorkingDirectory=directory};
    }
}
