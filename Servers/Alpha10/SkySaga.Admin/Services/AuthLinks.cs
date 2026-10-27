namespace SkySaga.Admin.Services;

public sealed record AuthLinks(string PublicUrl)
{
    public string Reset(string code) => $"{PublicUrl}/reset?code={code}";
}
