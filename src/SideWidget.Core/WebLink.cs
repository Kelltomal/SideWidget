namespace SideWidget.Core;

public static class WebLink
{
    public static Uri Validate(string value)
    {
        if (value.Length > 2048 || value.Any(char.IsControl) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("https" or "http") || string.IsNullOrEmpty(uri.Host) || uri.UserInfo.Length != 0)
            throw new CommandFault("link", "Можно открыть только ссылку HTTP или HTTPS без данных входа");
        return uri;
    }
}
