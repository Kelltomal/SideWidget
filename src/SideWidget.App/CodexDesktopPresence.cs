using System.Diagnostics;

namespace SideWidget.App;

sealed class CodexDesktopPresence
{
    int running,polling;
    long nextPoll;
    public bool IsRunning
    {
        get
        {
            if(Stopwatch.GetTimestamp()>=nextPoll&&Interlocked.CompareExchange(ref polling,1,0)==0)
            {
                nextPoll=Stopwatch.GetTimestamp()+Stopwatch.Frequency*5;
                _=Task.Run(()=>{
                    try{Volatile.Write(ref running,Detect()?1:0);}
                    finally{Volatile.Write(ref polling,0);}
                });
            }
            return Volatile.Read(ref running)!=0;
        }
    }
    static bool Detect()
    {
        foreach(var name in new[]{"ChatGPT","Codex"})
        {
            var found=false;
            foreach(var process in Process.GetProcessesByName(name))
            {
                using(process)
                {
                    try
                    {
                        var path=process.MainModule?.FileName??"";
                        if(name=="ChatGPT"?path.Contains("OpenAI.Codex",StringComparison.OrdinalIgnoreCase):
                            !path.Contains("\\resources\\",StringComparison.OrdinalIgnoreCase)&&process.MainWindowHandle!=0)found=true;
                    }
                    catch(Exception error) when(error is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException){}
                }
            }
            if(found)return true;
        }
        return false;
    }
}
