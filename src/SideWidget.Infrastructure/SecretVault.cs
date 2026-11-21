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
        return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(PathFor(id)),null,DataProtectionScope.CurrentUser));
    }
    public void Replace(string id, string value)
    {
        var destination = PathFor(id);
        Directory.CreateDirectory(directory);
        var temporary = destination + "." + Guid.NewGuid().ToString("N");
        try
        {
            var protectedData = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(temporary, protectedData);
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public void Delete(string id)
    {
        var path = PathFor(id);
        if (Directory.Exists(directory)) File.Delete(path);
    }

    string PathFor(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Invalid secret reference");
        return Path.Combine(directory, id);
    }
}
