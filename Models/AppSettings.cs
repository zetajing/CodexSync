namespace CodexSync.Models;

public sealed class AppSettings
{
    public string WebDavUrl { get; set; } = "http://192.168.1.2:5005/";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string RemoteRoot { get; set; } = "CodexSync";
    public string CodexHome { get; set; } = string.Empty;
}
