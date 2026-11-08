using System.Globalization;
using System.Net;

namespace Quantum.Zju.Plugin.Cas.Native;

public sealed class EmbeddedZjuBrowser(Action<string, string> report) : IZjuBrowser
{
    public Task<CasBrowserSession> SignInAsync(
        Func<CasBrowserSession, CancellationToken, Task> verifySession,
        CancellationToken cancellationToken) => MainThread.InvokeOnMainThreadAsync(() => OpenAsync(verifySession, report, cancellationToken));

    private static async Task<CasBrowserSession> OpenAsync(
        Func<CasBrowserSession, CancellationToken, Task> verifySession,
        Action<string, string> report,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var app = Application.Current ?? throw new InvalidOperationException("A native host is required.");
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var completion = new TaskCompletionSource<CasBrowserSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var web = new WebView();
        var recovery = new CasLoginRecovery();
        var currentUrl = "";
        var navigating = true;
        var expired = false;
        var retry = new Button { Text = Text("Retry", "重试"), CornerRadius = 8 };
        var status = new Label
        {
            Text = Text("Could not verify sign-in. Please retry.", "登录验证失败，请重试。"),
            VerticalOptions = LayoutOptions.Center
        };
        var error = new Grid
        {
            IsVisible = false,
            Padding = new Thickness(16, 8),
            ColumnSpacing = 12,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        error.Add(status, 0, 0);
        error.Add(retry, 1, 0);
        var layout = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
        layout.Add(web, 0, 0);
        layout.Add(error, 0, 1);
        var window = new Window(new ContentPage { Content = layout })
        {
            Title = Text("ZJU sign-in", "浙大登录"),
            Width = 1000,
            Height = 760
        };
        Task? verification = null;
        Task? initialization = null;
        void ReopenLogin(Uri url)
        {
            error.IsVisible = false;
            expired = false;
            navigating = true;
            web.Source = url.AbsoluteUri;
        }
        void BeginVerification()
        {
            if (!completion.Task.IsCompleted && (verification is null || verification.IsCompleted))
            {
                verification = VerifyAsync();
            }
        }
        async Task InitializeAsync()
        {
            try
            {
                await InitializeBrowserAsync(web).WaitAsync(TimeSpan.FromSeconds(30), lifetime.Token);
                // Clear legacy SSO state before the first request can silently select the previous account.
                await ClearCasCookiesAsync(web, lifetime.Token);
                report("initialization", "cookies-cleared");
                lifetime.Token.ThrowIfCancellationRequested();
                ReopenLogin(ZjuLoginPolicy.CreateFreshSignIn());
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                completion.TrySetCanceled(lifetime.Token);
            }
            catch (Exception exception)
            {
                report("initialization", exception.GetType().Name);
                completion.TrySetException(exception);
            }
        }
        async Task VerifyAsync()
        {
            error.IsVisible = false;
            expired = false;
            try
            {
                report("verification", "reading-session");
                var snapshot = await ReadSessionAsync(web);
                report("verification", "session-read");
                await verifySession(snapshot, lifetime.Token);
                report("verification", "account-verified");
                // The HTTP session owns its own cookie snapshot. Do not leave SSO credentials in the WebView.
                await ClearCasCookiesAsync(web, lifetime.Token);
                lifetime.Token.ThrowIfCancellationRequested();
                completion.TrySetResult(snapshot);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                completion.TrySetCanceled(lifetime.Token);
            }
            catch (Exception exception)
            {
                report("verification", exception.GetType().Name);
                // Keep the official page open; neither a redirect nor cookies alone prove sign-in.
                status.Text = Text("Could not verify sign-in. Please retry.", "登录验证失败，请重试。");
                retry.Text = Text("Retry", "重试");
                error.IsVisible = true;
            }
        }
        async Task MonitorExpirationAsync()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            try
            {
                while (await timer.WaitForNextTickAsync(lifetime.Token))
                {
                    if (navigating || expired || !ZjuLoginPolicy.IsCasPage(currentUrl)
                        || verification is { IsCompleted: false })
                    {
                        continue;
                    }
                    var observedUrl = currentUrl;
                    string result;
                    try
                    {
                        result = await web.EvaluateJavaScriptAsync(CasLoginRecovery.ExpirationProbe)
                            .WaitAsync(TimeSpan.FromSeconds(5), lifetime.Token);
                    }
                    catch (Exception) when (!lifetime.IsCancellationRequested)
                    {
                        // The native document may be between navigations. Check the next completed page.
                        continue;
                    }
                    if (navigating || currentUrl != observedUrl || !CasLoginRecovery.IsExpired(observedUrl, result))
                    {
                        continue;
                    }
                    var freshLogin = recovery.GetAutomaticRestart(observedUrl, result);
                    if (freshLogin is not null)
                    {
                        report("expiration", "reopening");
                        ReopenLogin(freshLogin);
                        continue;
                    }
                    expired = true;
                    status.Text = Text("Sign-in page expired.", "登录页已过期。");
                    retry.Text = Text("Reopen", "重新打开");
                    error.IsVisible = true;
                }
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
            }
        }
        web.Navigating += (_, e) =>
        {
            e.Cancel = !ZjuLoginPolicy.IsAllowedNavigation(e.Url);
            report("navigation-start", NavigationCategory(e.Url));
            if (ZjuLoginPolicy.IsSignInCallback(e.Url))
            {
                // Stop before the portal consumes the browser's callback. Verification uses a fresh CAS ticket.
                e.Cancel = true;
                BeginVerification();
            }
            else if (!e.Cancel)
            {
                navigating = true;
                expired = false;
                error.IsVisible = false;
            }
        };
        web.Navigated += (_, e) =>
        {
            report("navigation-end", e.Result.ToString());
            currentUrl = e.Url;
            navigating = false;
        };
        retry.Clicked += (_, _) =>
        {
            if (expired)
            {
                ReopenLogin(ZjuLoginPolicy.CreateFreshSignIn());
            }
            else
            {
                BeginVerification();
            }
        };
        web.Loaded += (_, _) => initialization ??= InitializeAsync();
        window.Destroying += (_, _) => lifetime.Cancel();
        using var registration = lifetime.Token.Register(() => completion.TrySetCanceled(lifetime.Token));
        app.OpenWindow(window);
        var expirationMonitor = MonitorExpirationAsync();
        try
        {
            return await completion.Task;
        }
        finally
        {
            await lifetime.CancelAsync();
            if (initialization is not null)
            {
                await initialization;
            }
            await expirationMonitor;
            if (verification is not null)
            {
                await verification;
            }
            web.Handler?.DisconnectHandler();
            if (app.Windows.Contains(window))
            {
                app.CloseWindow(window);
            }
        }
    }

    private static string Text(string en, string zh) => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? zh : en;

    private static string NavigationCategory(string url) => ZjuLoginPolicy.IsSignInCallback(url) ? "cas-callback"
        : ZjuLoginPolicy.IsCasPage(url) ? "cas-page"
        : ZjuLoginPolicy.IsAllowedNavigation(url) ? "university-page" : "blocked";

    private static async Task InitializeBrowserAsync(WebView web)
    {
#if WINDOWS
        var native = (Microsoft.UI.Xaml.Controls.WebView2?)web.Handler?.PlatformView
            ?? throw new InvalidOperationException("Browser is not ready.");
        await native.EnsureCoreWebView2Async();
#elif MACCATALYST
        _ = (WebKit.WKWebView?)web.Handler?.PlatformView
            ?? throw new InvalidOperationException("Browser is not ready.");
        await Task.CompletedTask;
#else
        throw new PlatformNotSupportedException();
#endif
    }

    private static async Task ClearCasCookiesAsync(WebView web, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
#if WINDOWS
        var native = (Microsoft.UI.Xaml.Controls.WebView2?)web.Handler?.PlatformView;
        var manager = native?.CoreWebView2?.CookieManager ?? throw new InvalidOperationException("Browser is not ready.");
        foreach (var cookie in await manager.GetCookiesAsync(ZjuLoginPolicy.CasOrigin.AbsoluteUri))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ZjuLoginPolicy.IsCasCookie(cookie.Domain, cookie.Path))
            {
                manager.DeleteCookie(cookie);
            }
        }
#elif MACCATALYST
        var native = (WebKit.WKWebView?)web.Handler?.PlatformView ?? throw new InvalidOperationException("Browser is not ready.");
        var store = native.Configuration.WebsiteDataStore.HttpCookieStore;
        foreach (var cookie in await store.GetAllCookiesAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ZjuLoginPolicy.IsCasCookie(cookie.Domain, cookie.Path))
            {
                await store.DeleteCookieAsync(cookie);
            }
        }
#else
        throw new PlatformNotSupportedException();
#endif
    }

    private static async Task<CasBrowserSession> ReadSessionAsync(WebView web)
    {
        var result = new CookieContainer();
#if WINDOWS
        var native = (Microsoft.UI.Xaml.Controls.WebView2?)web.Handler?.PlatformView;
        var manager = native?.CoreWebView2?.CookieManager ?? throw new InvalidOperationException("Browser is not ready.");
        foreach (var item in await manager.GetCookiesAsync(ZjuLoginPolicy.CasOrigin.AbsoluteUri))
        {
            var cookie = new Cookie(item.Name, item.Value, item.Path, item.Domain) { Secure = item.IsSecure, HttpOnly = item.IsHttpOnly };
            if (!item.IsSession)
            {
                cookie.Expires = DateTime.UnixEpoch.AddSeconds(item.Expires);
            }
            result.Add(cookie);
        }
#elif MACCATALYST
        var native = (WebKit.WKWebView?)web.Handler?.PlatformView ?? throw new InvalidOperationException("Browser is not ready.");
        foreach (var item in await native.Configuration.WebsiteDataStore.HttpCookieStore.GetAllCookiesAsync())
        {
            var cookie = new Cookie(item.Name, item.Value, item.Path, item.Domain) { Secure = item.IsSecure, HttpOnly = item.IsHttpOnly };
            if (item.ExpiresDate is { } expires)
            {
                cookie.Expires = (DateTime)expires;
            }
            if (ZjuLoginPolicy.IsCasCookie(cookie.Domain, cookie.Path))
            {
                result.Add(cookie);
            }
        }
#endif
#if WINDOWS
        var userAgent = native!.CoreWebView2.Settings.UserAgent;
#elif MACCATALYST
        var userAgent = (await native.EvaluateJavaScriptAsync("navigator.userAgent"))?.ToString()
            ?? throw new InvalidOperationException("Browser identity is unavailable.");
#else
        throw new PlatformNotSupportedException();
#endif
        var applicable = new CookieContainer();
        foreach (Cookie cookie in result.GetCookies(ZjuLoginPolicy.CasOrigin))
        {
            applicable.Add(cookie);
        }
        return new CasBrowserSession(applicable, userAgent);
    }
}
