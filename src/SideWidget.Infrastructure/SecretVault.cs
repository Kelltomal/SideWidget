using System.Security.Cryptography;
using System.Text;
using System.Runtime.Versioning;

namespace SideWidget.Infrastructure;
[SupportedOSPlatform("windows")]
public sealed class SecretVault(string directory)
{
    public string Save(string value)
    {
        Directory.CreateDirectory(directory); var id=Guid.NewGuid().ToString("N");
        File.WriteAllBytes(Path.Combine(directory,id),ProtectedData.Protect(Encoding.UTF8.GetBytes(value),null,DataProtectionScope.CurrentUser)); return id;
    }
    public string Read(string id)
    {
        if(!Guid.TryParseExact(id,"N",out _)) throw new ArgumentException("Invalid secret reference");
        return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(Path.Combine(directory,id)),null,DataProtectionScope.CurrentUser));
    }
}
