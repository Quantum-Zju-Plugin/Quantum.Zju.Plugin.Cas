using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NOF.Domain;

namespace Quantum.Zju.Plugin.Cas;

public sealed class ZjuCasSession : IDisposable
{
    private readonly IZjuBrowser _browser;
    private readonly Func<CasBrowserSession, HttpClient> _createClient;
    private readonly ILogger<ZjuCasSession> _logger;
    private readonly SemaphoreSlim _gate = new(1);
    private readonly CancellationTokenSource _lifetime = new();
    private HttpClient? _client;
    private string? _sessionId;
    private string? _userAgent;
    private string? _username;

    public ZjuCasSession(IZjuBrowser browser, ILogger<ZjuCasSession>? logger = null) : this(browser, snapshot =>
        new HttpClient(new HttpClientHandler { CookieContainer = snapshot.Cookies, AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(30)
        }, logger)
    {
    }

    internal ZjuCasSession(IZjuBrowser browser, Func<CasBrowserSession, HttpClient> createClient, ILogger<ZjuCasSession>? logger = null)
    {
        _browser = browser;
        _createClient = createClient;
        _logger = logger ?? NullLogger<ZjuCasSession>.Instance;
    }

    public async Task SignInAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        await _gate.WaitAsync(linked.Token);
        HttpClient? candidate = null;
        try
        {
            string? username = null;
            string? userAgent = null;
            await _browser.SignInAsync(async (snapshot, token) =>
            {
                if (string.IsNullOrWhiteSpace(snapshot.UserAgent) || snapshot.UserAgent.Length > 2048)
                {
                    throw new DomainException("zju_cas_invalid_browser", "The browser identity is unavailable.");
                }
                var client = _createClient(snapshot);
                try
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(snapshot.UserAgent);
                    // Validate a CAS-owned service ticket without involving a business system.
                    var verifiedUsername = await CasIdentityClient.GetUsernameAsync(client, token);
                    token.ThrowIfCancellationRequested();
                    candidate?.Dispose();
                    candidate = client;
                    username = verifiedUsername;
                    userAgent = snapshot.UserAgent;
                }
                catch (DomainException exception)
                {
                    _logger.LogWarning("CAS account verification failed with {ErrorCode}.", exception.ErrorCode);
                    client.Dispose();
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogDebug("CAS account verification ended with {FailureType}.", exception.GetType().Name);
                    client.Dispose();
                    throw;
                }
            }, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            if (candidate is null || username is null)
            {
                throw new DomainException("zju_cas_identity_unavailable", "CAS could not verify the signed-in account.");
            }
            _client?.Dispose();
            _client = candidate;
            candidate = null;
            _sessionId = Guid.NewGuid().ToString("N");
            _userAgent = userAgent;
            _username = username;
            _logger.LogInformation("CAS account connection established.");
        }
        finally
        {
            candidate?.Dispose();
            _gate.Release();
        }
    }

    public async Task<CasAccount> GetAccountAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return new CasAccount { IsSignedIn = _client is not null, SessionId = _sessionId, Username = _username };
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
        _client?.Dispose();
        _lifetime.Dispose();
        _gate.Dispose();
    }
}
