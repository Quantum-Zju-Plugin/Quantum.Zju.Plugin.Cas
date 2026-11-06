using NOF.Domain;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Quantum.OfficialPlugins.Zjuam;

public sealed class ZjuamSession(IZjuBrowser browser) : IDisposable
{
    private static readonly Uri TodosUrl = new("https://courses.zju.edu.cn/api/todos?no-intercept=true");
    private readonly SemaphoreSlim _gate = new(1);
    private readonly CancellationTokenSource _lifetime = new();
    private HttpClient? _client;
    private string? _accountKey;
    private string? _username;

    public async Task SignInAsync(string username, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        cancellationToken = linked.Token;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var cookies = await browser.SignInAsync(cancellationToken);
            var client = new HttpClient(new HttpClientHandler { CookieContainer = cookies });
            try
            {
                var request = new ReadZjuamResourceRequest { Resource = "zdbk-selected" };
                await ZdbkResourceReader.ReadAsync(client, username.Trim(), request, cancellationToken);
            }
            catch
            {
                client.Dispose();
                throw;
            }
            _client?.Dispose();
            _client = client;
            _username = username.Trim();
            _accountKey = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(_username)));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ZjuamResource> ReadAsync(ReadZjuamResourceRequest request, CancellationToken cancellationToken)
    {
        if (request.Resource is not ("xzzd-todos" or "zdbk-courses" or "zdbk-sections"
            or "zdbk-selected" or "zdbk-graduation" or "zdbk-introduction"))
        {
            throw new DomainException("zjuam_invalid_resource", "Unknown authenticated resource.");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var client = _client ?? throw new DomainException("zjuam_sign_in_required", "Sign in through ZJU Identity first.");
            if (request.Resource != "xzzd-todos")
            {
                var academicData = await ZdbkResourceReader.ReadAsync(client, _username!, request, cancellationToken);
                return new ZjuamResource { AccountKey = _accountKey!, Payload = academicData };
            }

            using var response = await client.GetAsync(TodosUrl, cancellationToken);
            if (!response.IsSuccessStatusCode || response.RequestMessage?.RequestUri != TodosUrl
                || response.Content.Headers.ContentType?.MediaType != "application/json")
            {
                throw new DomainException("zjuam_resource_unavailable", "The authenticated endpoint did not return JSON.");
            }

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            return new ZjuamResource { AccountKey = _accountKey!, Payload = payload };
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            throw new DomainException("zjuam_resource_unavailable", "The authenticated endpoint could not be read.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _client?.Dispose();
            _client = null;
            _accountKey = null;
            _username = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void CancelSignIn() => _lifetime.Cancel();

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        _client?.Dispose();
        _gate.Dispose();
    }
}
