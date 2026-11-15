using SideWidget.App;

static class ExplorerRevealChecks
{
    public static Task Run()
    {
        var root=Path.Combine(Path.GetTempPath(),"SideWidget-reveal-"+Guid.NewGuid().ToString("N"));
        var directory=Path.Combine(root,"Папка с пробелами, и запятой");Directory.CreateDirectory(directory);
        var file=Path.Combine(directory,"Сборка SideWidget, готовая.zip");File.WriteAllText(file,"fixture");
        string? requested=null;
        if(!ExplorerReveal.TryReveal(file,path=>{requested=path;return 0;})||requested!=file)throw new Exception("Shell must receive the exact Unicode path, without CLI switches/quoting");
        if(ExplorerReveal.TryReveal(file,_=>unchecked((int)0x80004004)))throw new Exception("Cancelled Shell request must not claim selected file");
        if(ExplorerReveal.TryReveal(file,_=>unchecked((int)0x80070005)))throw new Exception("Denied Shell request must not claim selected file");
        if(ExplorerReveal.TryReveal(file,_=>1))throw new Exception("Only S_OK confirms selection request");
        if(ExplorerReveal.TryReveal(file,_=>throw new System.Runtime.InteropServices.COMException()))throw new Exception("Shell exception must preserve ZIP and return honest failure");
        var invoked=false;
        if(ExplorerReveal.TryReveal(Path.Combine(directory,"missing.zip"),_=>{invoked=true;return 0;})||invoked)throw new Exception("Missing file must not invoke Shell");
        if(ExplorerReveal.TryReveal(directory,_=>0))throw new Exception("Archive reveal must require a file");
        if(!File.Exists(file))throw new Exception("Reveal failure must not remove the archive");
        Console.WriteLine("PASS 8 Explorer reveal checks: exact Unicode path, missing file, HRESULT and failure reporting; no Explorer opened");
        return Task.CompletedTask;
    }
}
