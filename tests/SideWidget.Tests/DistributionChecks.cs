using System.IO.Compression;
using System.Text.Json;
using SideWidget.Infrastructure;

static class DistributionChecks
{
    public static int Run()
    {
        SideWidget.Core.Protocol.Parse(SideWidget.Core.Protocol.Origin,JsonSerializer.Serialize(new{protocolVersion=1,requestId=Guid.NewGuid().ToString(),actionId="app.share",@params=new{}}));
        var root=Path.Combine(Path.GetTempPath(),"sidewidget-share-"+Guid.NewGuid().ToString("N"));
        var app=Path.Combine(root,"app");var output=Path.Combine(root,"exports");
        foreach(var name in DistributionArchive.RequiredFiles.Concat(new[]{"WebView2Runtime/Locales/ru.pak","extra-dependency.dll"}))
        {var path=Path.Combine(app,name);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,"current-build");}
        var configPath=Path.Combine(app,"SideWidget.App.runtimeconfig.json");
        const string standalone="""{"runtimeOptions":{"includedFrameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.14"}]}}""";
        File.WriteAllText(configPath,standalone);
        File.WriteAllText(Path.Combine(app,"SideWidget.App.deps.json"),"""{"runtimeTarget":{"name":".NETCoreApp,Version=v10.0/win-x64"},"targets":{".NETCoreApp,Version=v10.0/win-x64":{"app/1.0":{"runtime":{"extra-dependency.dll":{}}}}}}""");
        File.WriteAllText(Path.Combine(app,"sidewidget.db"),"personal-data");
        File.WriteAllText(Path.Combine(app,"source.cs"),"private source code");
        File.WriteAllText(Path.Combine(app,"source.tsx"),"private frontend source");
        Directory.CreateDirectory(Path.Combine(app,"secrets"));File.WriteAllText(Path.Combine(app,"secrets","key"),"secret");
        DistributionArchive.Prepare(app);DistributionArchive.Validate(app);
        var manifestPath=Path.Combine(app,DistributionArchive.ManifestName);
        var valid=File.ReadAllText(manifestPath);
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var manifest=JsonSerializer.Deserialize<DistributionManifest>(valid,json)!;
        void Manifest(DistributionFile[] files)=>File.WriteAllText(manifestPath,JsonSerializer.Serialize(new DistributionManifest(2,files),json));
        void Reject(Action action,string message){try{action();}catch(Exception error)when(error is InvalidDataException or FileNotFoundException){return;}throw new Exception(message);}
        var first=DistributionArchive.Create(app,output);
        using(var zip=ZipFile.OpenRead(first))
        {
            if(zip.Entries.Count!=manifest.Files.Length+1||zip.GetEntry("sidewidget.db")!=null||zip.GetEntry("secrets/key")!=null||zip.GetEntry("source.cs")!=null||zip.GetEntry("source.tsx")!=null)throw new Exception("Share must contain only distribution files, never project sources");
            if(zip.GetEntry("WebView2Runtime/EBWebView/x64/EmbeddedBrowserWebView.dll")==null)throw new Exception("Runtime EBWebView libraries must not be filtered out as user data");
            using var reader=new StreamReader(zip.GetEntry("SideWidget.App.exe")!.Open());
            if(reader.ReadToEnd()!="current-build")throw new Exception("Share must package current executable");
        }
        var second=DistributionArchive.Create(app,output);
        if(first==second||!File.Exists(first))throw new Exception("Repeated share must preserve previous archive");
        Manifest([..manifest.Files,new("../outside.txt",1,new string('0',64))]);Reject(()=>DistributionArchive.Create(app,output),"Traversal accepted");
        File.WriteAllText(manifestPath,valid);
        File.WriteAllText(Path.Combine(app,"coreclr.dll"),"changed-build"); // same byte count: hashes, not only lengths
        Reject(()=>DistributionArchive.Create(app,output),"Changed runtime accepted");
        File.WriteAllText(Path.Combine(app,"coreclr.dll"),"current-build");
        foreach(var missing in new[]{"coreclr.dll","WebView2Runtime/EBWebView/x64/EmbeddedBrowserWebView.dll","WebView2Runtime/Locales/ru.pak","extra-dependency.dll"})
        {Manifest(manifest.Files.Where(file=>file.Path!=missing).ToArray());Reject(()=>DistributionArchive.Create(app,output),"Missing dependency accepted: "+missing);}
        File.WriteAllText(manifestPath,valid);File.WriteAllText(configPath,"""{"runtimeOptions":{"framework":{"name":"Microsoft.WindowsDesktop.App","version":"10.0.14"}}}""");
        Reject(()=>DistributionArchive.Prepare(app),"Framework-dependent build accepted");File.WriteAllText(configPath,standalone);
        File.WriteAllText(manifestPath,"[\"SideWidget.App.exe\"]");Reject(()=>DistributionArchive.Create(app,output),"Legacy weak manifest accepted");
        File.WriteAllText(manifestPath,valid);
        File.Delete(manifestPath);
        try{DistributionArchive.Create(app,output);throw new Exception("Missing manifest accepted");}
        catch(InvalidDataException error){if(!error.Message.Contains("список файлов portable-поставки"))throw;}
        File.WriteAllText(manifestPath,valid);
        var unpacked=Path.Combine(root,"unpacked");ZipFile.ExtractToDirectory(first,unpacked);DistributionArchive.Validate(unpacked);
        var reshared=DistributionArchive.Create(unpacked,Path.Combine(root,"reshared"));if(!File.Exists(reshared))throw new Exception("Recipient cannot share again");
        var adjacent=DistributionArchive.CreateNextToApplication(unpacked);
        if(Path.GetDirectoryName(adjacent)!=unpacked)throw new Exception("Share must save next to the running application");
        using(var zip=ZipFile.OpenRead(adjacent))if(zip.Entries.Count!=manifest.Files.Length+1||zip.Entries.Any(entry=>entry.FullName.EndsWith(".zip",StringComparison.OrdinalIgnoreCase)))throw new Exception("Adjacent ZIP must not contain previous archives");
        if(Directory.GetFiles(output).Length!=2)throw new Exception("Failed export left a partial archive");
        return 14;
    }
}
