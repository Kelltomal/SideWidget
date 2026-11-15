using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace SideWidget.Infrastructure;

public sealed record DistributionFile(string Path,long Size,string Sha256);
public sealed record DistributionManifest(int FormatVersion,DistributionFile[] Files);

public static class DistributionArchive
{
    public const string ManifestName="distribution-files.json";
    public static string CreateNextToApplication(string applicationDirectory)=>Create(applicationDirectory,applicationDirectory);
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){WriteIndented=true};
    public static IReadOnlyList<string> RequiredFiles {get;}=Array.AsReadOnly(new[]{
        "SideWidget.App.exe","SideWidget.App.dll","SideWidget.App.runtimeconfig.json","SideWidget.App.deps.json",
        "hostfxr.dll","hostpolicy.dll","coreclr.dll","System.Private.CoreLib.dll",
        "Microsoft.WindowsAppRuntime.dll","Microsoft.ui.xaml.dll","Microsoft.UI.Xaml.Controls.pri","WebView2Loader.dll",
        "e_sqlite3.dll","Everything64.dll","vcruntime140.dll","vcruntime140_1.dll","msvcp140.dll","concrt140.dll",
        "Web/index.html","Web/fonts/MedeoSansVariable.woff2","Web/fonts/MedeoMonoVariable.woff2",
        "Assets/Fonts/MedeoSans.ttf","Assets/Icons/sidemenu.ico",
        "WebView2Runtime/msedgewebview2.exe","WebView2Runtime/msedge.dll","WebView2Runtime/msedge_elf.dll",
        "WebView2Runtime/icudtl.dat","WebView2Runtime/resources.pak","WebView2Runtime/msedge_100_percent.pak",
        "WebView2Runtime/msedge_200_percent.pak","WebView2Runtime/v8_context_snapshot.bin",
        "WebView2Runtime/EBWebView/x64/EmbeddedBrowserWebView.dll"
    });

    // Only prepare freshly published output, never a user profile.
    public static void Prepare(string applicationDirectory)
    {
        var root=Root(applicationDirectory);
        var files=Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories)
            .Select(path=>Path.GetRelativePath(root,path).Replace('\\','/'))
            .Where(path=>path!=ManifestName&&IsDistributionFile(path))
            .Order(StringComparer.Ordinal).Select(path=>{
                var source=SafePath(root,path);using var stream=File.OpenRead(source);
                return new DistributionFile(path,stream.Length,Convert.ToHexStringLower(SHA256.HashData(stream)));
            }).ToArray();
        var manifest=new DistributionManifest(2,files);ValidateInventory(root,manifest);
        File.WriteAllText(Path.Combine(root,ManifestName),JsonSerializer.Serialize(manifest,Json));
    }
    public static void Validate(string applicationDirectory)
    {
        var root=Root(applicationDirectory);var manifest=ReadManifest(root);ValidateInventory(root,manifest);
        foreach(var file in manifest.Files){using var stream=File.OpenRead(SafePath(root,file.Path));CheckContent(file,stream.Length,SHA256.HashData(stream));}
    }
    public static string Create(string applicationDirectory,string outputDirectory)
    {
        var root=Root(applicationDirectory);var manifest=ReadManifest(root);ValidateInventory(root,manifest);
        var manifestBytes=JsonSerializer.SerializeToUtf8Bytes(manifest,Json);
        Directory.CreateDirectory(outputDirectory);
        var archive=Path.Combine(outputDirectory,$"SideWidget-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}-win-x64.zip");
        var temporary=archive+".partial";
        try
        {
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None))
            using(var zip=new ZipArchive(stream,ZipArchiveMode.Create))
            {
                foreach(var file in manifest.Files)
                {
                    // Check the same stream that is copied, not a previous snapshot.
                    using var source=new FileStream(SafePath(root,file.Path),FileMode.Open,FileAccess.Read,FileShare.Read);
                    using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    using var destination=zip.CreateEntry(file.Path,CompressionLevel.Fastest).Open();
                    var buffer=new byte[128*1024];long length=0;int count;
                    while((count=source.Read(buffer))>0){destination.Write(buffer,0,count);hash.AppendData(buffer,0,count);length+=count;}
                    CheckContent(file,length,hash.GetHashAndReset());
                }
                using var manifestEntry=zip.CreateEntry(ManifestName,CompressionLevel.Fastest).Open();manifestEntry.Write(manifestBytes);
            }
            VerifyArchive(temporary,manifest);
            File.Move(temporary,archive);
            return archive;
        }
        catch{if(File.Exists(temporary))File.Delete(temporary);throw;}
    }
    static DistributionManifest ReadManifest(string root)
    {
        var path=Path.Combine(root,ManifestName);
        if(!File.Exists(path))throw new InvalidDataException("В этой сборке отсутствует список файлов portable-поставки. Используйте полную новую публикацию SideWidget: файл distribution-files.json создаётся при публикации именно этой сборки.");
        using var document=JsonDocument.Parse(File.ReadAllText(path));
        if(document.RootElement.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Устаревшая сборка. Используйте новую полную portable-сборку SideWidget.");
        return document.RootElement.Deserialize<DistributionManifest>(Json)??throw new InvalidDataException("Состав сборки повреждён");
    }
    static void ValidateInventory(string root,DistributionManifest manifest)
    {
        if(manifest.FormatVersion!=2||manifest.Files is null||manifest.Files.Length==0)throw new InvalidDataException("Состав сборки повреждён");
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var file in manifest.Files)
        {
            if(file is null)throw new InvalidDataException("Состав сборки повреждён");
            SafePath(root,file.Path);
            if(!names.Add(file.Path)||!IsDistributionFile(file.Path)||file.Path==ManifestName||file.Size<=0||file.Sha256 is null||file.Sha256.Length!=64||!file.Sha256.All(Uri.IsHexDigit))throw new InvalidDataException("Некорректный состав сборки: "+file.Path);
        }
        void Require(string path){if(!names.Contains(path))throw new InvalidDataException("Неполная portable-сборка. Отсутствует: "+path);}
        foreach(var required in RequiredFiles)Require(required);
        if(!names.Any(path=>path.StartsWith("WebView2Runtime/Locales/",StringComparison.OrdinalIgnoreCase)&&path.EndsWith(".pak",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Отсутствуют языковые ресурсы WebView2");
        using var config=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"SideWidget.App.runtimeconfig.json")));
        var options=config.RootElement.GetProperty("runtimeOptions");
        if(options.TryGetProperty("framework",out _)||options.TryGetProperty("frameworks",out _)||!options.TryGetProperty("includedFrameworks",out var included)||!included.EnumerateArray().Any(item=>item.GetProperty("name").GetString()=="Microsoft.NETCore.App"))throw new InvalidDataException("Сборка требует установленный .NET. Нужна автономная публикация --self-contained true.");
        using var deps=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"SideWidget.App.deps.json")));
        var target=deps.RootElement.GetProperty("runtimeTarget").GetProperty("name").GetString()!;
        if(!target.EndsWith("/win-x64",StringComparison.Ordinal))throw new InvalidDataException("Нужна автономная сборка Windows x64");
        foreach(var library in deps.RootElement.GetProperty("targets").GetProperty(target).EnumerateObject())
            foreach(var group in new[]{"runtime","native","resources"})
                if(library.Value.TryGetProperty(group,out var assets))foreach(var asset in assets.EnumerateObject())
                {
                    var filename=asset.Name.Replace('\\','/').Split('/').Last();if(filename=="_._")continue;
                    var path=group=="resources"&&asset.Value.TryGetProperty("locale",out var locale)?locale.GetString()+"/"+filename:filename;
                    Require(path);
                }
    }
    static bool IsDistributionFile(string path)
    {
        var parts=path.Split('/');
        // Fixed runtime EBWebView contains REQUIRED binaries, not profile data.
        if(parts[0].Equals("WebView2Runtime",StringComparison.OrdinalIgnoreCase))return true;
        return !parts.Any(part=>new[]{"secrets","Shared","backups","EBWebView","TranslationWebView2","WebView2"}.Contains(part,StringComparer.OrdinalIgnoreCase)||part.StartsWith(".env",StringComparison.OrdinalIgnoreCase))
            &&!new[]{".pdb",".zip",".partial",".db",".db-wal",".db-shm",".log",".cs",".csproj",".sln",".slnx",".ts",".tsx",".jsx",".map",".py"}.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase);
    }
    static string Root(string directory)=>Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory))+Path.DirectorySeparatorChar;
    static string SafePath(string root,string file)
    {
        if(string.IsNullOrWhiteSpace(file)||Path.IsPathRooted(file)||file.Contains(':')||file.Contains('\\')||file.Split('/').Any(part=>part is ".." or "." or ""))throw new InvalidDataException("Некорректный путь в составе сборки");
        var path=Path.GetFullPath(Path.Combine(root,file));
        if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Некорректный путь в составе сборки");
        if(!File.Exists(path))throw new FileNotFoundException("Отсутствует файл сборки: "+file,path);
        for(var current=path;current.Length>=root.Length;current=Path.GetDirectoryName(current)!)
            if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Ссылки в составе сборки не поддерживаются");
        return path;
    }
    static void CheckContent(DistributionFile file,long length,byte[] hash)
    {if(length!=file.Size||!Convert.ToHexString(hash).Equals(file.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Файл сборки изменён или повреждён: "+file.Path+". Используйте полную новую сборку.");}
    static void VerifyArchive(string archive,DistributionManifest manifest)
    {
        using var zip=ZipFile.OpenRead(archive);
        if(zip.Entries.Count!=manifest.Files.Length+1)throw new InvalidDataException("ZIP содержит неполный состав сборки");
        foreach(var file in manifest.Files)
        {
            var entry=zip.GetEntry(file.Path)??throw new InvalidDataException("ZIP не содержит: "+file.Path);
            using var stream=entry.Open();CheckContent(file,entry.Length,SHA256.HashData(stream));
        }
    }
}
