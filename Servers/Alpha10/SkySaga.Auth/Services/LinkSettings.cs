namespace SkySaga.Auth.Services;

public sealed record LinkSettings(string PublicUrl)
{
    public string Terms(HttpRequest request) => $"{For(request)}/";

    public string Reset(string code) => $"{PublicUrl}/reset?code={code}";

    private string For(HttpRequest request)
    {
        var forwarded = request.Headers["X-Forwarded-Host"].ToString();
        var host = new HostString(forwarded.Length > 0 ? forwarded : request.Host.Value ?? "").Host;

        return host.Length == 0 ? PublicUrl : new UriBuilder(PublicUrl) { Host = host }.Uri.ToString().TrimEnd('/');
    }
}
