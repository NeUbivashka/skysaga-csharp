namespace SkySaga.Auth.Services;

public sealed record LinkSettings(string PublicUrl)
{
    public string Terms => $"{PublicUrl}/";

    public string Reset(string code) => $"{PublicUrl}/reset?code={code}";
}
