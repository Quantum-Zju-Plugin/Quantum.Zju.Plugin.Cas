using NOF.Domain;

namespace Quantum.Zju.Plugin.Cas;

public sealed class ZjuCasSession : IDisposable
{
    private readonly IZjuBrowser _browser;
    private readonly Func<CasBrowserSession, HttpClient> _createClient;
    private readonly SemaphoreSlim _gate = new(1);
    private readonly CancellationTokenSource _lifetime = new();
    private HttpClient? _client;
    private string? _sessionId;
    private string? _userAgent;

    public ZjuCasSession(IZjuBrowser browser) : this(browser, snapshot =>
        new HttpClient(new HttpClientHandler { CookieContainer = snapshot.Cookies, AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(30)
        })
    {
    }

    internal ZjuCasSession(IZjuBrowser browser, Func<CasBrowserSession, HttpClient> createClient)
    {
        _browser = browser;
        _createClient = createClient;
    }

    public async Task SignInAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        await _gate.WaitAsync(linked.Token);
        try
        {
            var snapshot = await _browser.SignInAsync(linked.Token);
            var client = _createClient(snapshot);
            try
            {
                if (string.IsNullOrWhiteSpace(snapshot.UserAgent) || snapshot.UserAgent.Length > 2048)
                {
                    throw new DomainException("zju_cas_invalid_browser", "The browser identity is unavailable.");
                }
                client.DefaultRequestHeaders.UserAgent.ParseAdd(snapshot.UserAgent);
                // Verify CAS itself; no business system participates in accepting an account.
                await CasTicketClient.RequestCallbackAsync(client, ZjuLoginPolicy.CasOrigin.AbsoluteUri, linked.Token);
            }
            catch
            {
                client.Dispose();
                throw;
            }
            _client?.Dispose();
            _client = client;
            _sessionId = Guid.NewGuid().ToString("N");
            _userAgent = snapshot.UserAgent;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CasAccount> GetAccountAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return new CasAccount { IsSignedIn = _client is not null, SessionId = _sessionId };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CasServiceTicket> GetServiceTicketAsync(GetCasServiceTicketRequest request, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        await _gate.WaitAsync(linked.Token);
        try
        {
            var client = _client ?? throw new DomainException("zju_cas_sign_in_required", "Sign in to the ZJU account first.");
            if (request.SessionId != _sessionId)
            {
                throw new DomainException("zju_cas_session_changed", "The local account connection has changed. Read its current state again.");
            }
            var callback = await CasTicketClient.RequestCallbackAsync(client, request.ServiceUrl, linked.Token);
            return new CasServiceTicket { SessionId = _sessionId!, ServiceUrl = request.ServiceUrl, CallbackUrl = callback, UserAgent = _userAgent! };
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
            _sessionId = null;
            _userAgent = null;
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
        _client?.Dispose();
        _lifetime.Dispose();
        _gate.Dispose();
    }
}
