using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NOF.Domain;

namespace Quantum.Zju.Plugin.Cas;

public sealed class ZjuCasSession : IDisposable
{
    private readonly IZjuBrowser _browser;
    private readonly ICasSessionStore? _store;
    private readonly Func<CasBrowserSession, HttpClient> _createClient;
    private readonly ILogger<ZjuCasSession> _logger;
    private readonly SemaphoreSlim _gate = new(1);
    private readonly CancellationTokenSource _lifetime = new();
    private HttpClient? _client;
    private string? _sessionId;
    private string? _userAgent;
    private string? _username;
    private CasBrowserSession? _activeSnapshot;
    private string? _savedValue;
    private DateTimeOffset _restoreAfter;

    public ZjuCasSession(IZjuBrowser browser, ILogger<ZjuCasSession>? logger = null) : this(browser, snapshot =>
        new HttpClient(new HttpClientHandler { CookieContainer = snapshot.Cookies, AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(30)
        }, logger, browser as ICasSessionStore)
    {
    }

    internal ZjuCasSession(IZjuBrowser browser, Func<CasBrowserSession, HttpClient> createClient,
        ILogger<ZjuCasSession>? logger = null, ICasSessionStore? store = null)
    {
        _browser = browser;
        _store = store;
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
            CasBrowserSession? verifiedSnapshot = null;
            await _browser.SignInAsync(async (snapshot, token) =>
            {
                try
                {
                    var verified = await VerifyAsync(snapshot, token);
                    candidate?.Dispose();
                    candidate = verified.Client;
                    username = verified.Username;
                    verifiedSnapshot = snapshot;
                }
                catch (DomainException exception)
                {
                    _logger.LogWarning("CAS account verification failed with {ErrorCode}.", exception.ErrorCode);
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogDebug("CAS account verification ended with {FailureType}.", exception.GetType().Name);
                    throw;
                }
            }, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            if (candidate is null || username is null || verifiedSnapshot is null)
            {
                throw new DomainException("zju_cas_identity_unavailable", "CAS could not verify the signed-in account.");
            }
            await SaveAsync(verifiedSnapshot, replacing: true);
            SetConnection(candidate, verifiedSnapshot, username);
            candidate = null;
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
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        await _gate.WaitAsync(linked.Token);
        try
        {
            if (_client is null && _store is not null && DateTimeOffset.UtcNow >= _restoreAfter)
            {
                await RestoreAsync(linked.Token);
            }
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
            if (_activeSnapshot is not null)
            {
                await SaveAsync(_activeSnapshot);
            }
            return new CasServiceTicket { SessionId = _sessionId!, ServiceUrl = request.ServiceUrl, CallbackUrl = callback, UserAgent = _userAgent! };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        await _gate.WaitAsync(linked.Token);
        try
        {
            if (_store is not null)
            {
                await _store.ClearAsync();
            }
            ClearConnection();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void CancelSignIn() => _lifetime.Cancel();

    internal async Task StopAsync(CancellationToken cancellationToken)
    {
        CancelSignIn();
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ClearConnection();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<(HttpClient Client, string Username)> VerifyAsync(CasBrowserSession snapshot, CancellationToken token)
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
            var username = await CasIdentityClient.GetUsernameAsync(client, token);
            token.ThrowIfCancellationRequested();
            return (client, username);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private async Task RestoreAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            _savedValue = await _store!.ReadAsync();
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = _savedValue is null ? null : SavedCasSession.Deserialize(_savedValue);
            if (snapshot is null)
            {
                _restoreAfter = DateTimeOffset.MaxValue;
                return;
            }
            var (client, username) = await VerifyAsync(snapshot, timeout.Token);
            SetConnection(client, snapshot, username);
            await SaveAsync(snapshot);
            _logger.LogInformation("CAS account connection restored.");
        }
        catch (Exception exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Do not erase a session merely because CAS or secure storage is temporarily unavailable.
            _restoreAfter = DateTimeOffset.UtcNow.AddSeconds(30);
            _logger.LogDebug("CAS account restoration ended with {FailureType}.", exception.GetType().Name);
        }
    }

    private async Task SaveAsync(CasBrowserSession snapshot, bool replacing = false)
    {
        if (_store is null)
        {
            return;
        }
        try
        {
            var value = SavedCasSession.Serialize(snapshot);
            if (replacing || value != _savedValue)
            {
                if (value is null)
                {
                    await _store.ClearAsync();
                }
                else
                {
                    await _store.WriteAsync(value);
                }
                _savedValue = value;
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning("CAS session could not be saved: {FailureType}.", exception.GetType().Name);
            if (replacing)
            {
                try
                {
                    await _store.ClearAsync();
                    _savedValue = null;
                }
                catch (Exception clearFailure)
                {
                    _logger.LogWarning("Previous saved CAS session could not be cleared: {FailureType}.", clearFailure.GetType().Name);
                }
            }
        }
    }

    private void SetConnection(HttpClient client, CasBrowserSession snapshot, string username)
    {
        _client?.Dispose();
        _client = client;
        _sessionId = Guid.NewGuid().ToString("N");
        _userAgent = snapshot.UserAgent;
        _username = username;
        _activeSnapshot = snapshot;
    }

    private void ClearConnection()
    {
        _client?.Dispose();
        _client = null;
        _sessionId = null;
        _userAgent = null;
        _username = null;
        _activeSnapshot = null;
        _savedValue = null;
        _restoreAfter = DateTimeOffset.MaxValue;
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        ClearConnection();
        _lifetime.Dispose();
        _gate.Dispose();
    }
}
