using System.Net;
namespace Quantum.Zju.Plugin.Oidc.Native;

using Quantum.OfficialPlugins.Zjuam;
public sealed class EmbeddedZjuBrowser : IZjuBrowser
{
    public Task<CookieContainer> SignInAsync(CancellationToken cancellationToken) => MainThread.InvokeOnMainThreadAsync(() => OpenAsync(cancellationToken));
    private static async Task<CookieContainer> OpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var app = Application.Current ?? throw new InvalidOperationException("A native host is required.");
        var completion = new TaskCompletionSource<CookieContainer>(TaskCreationOptions.RunContinuationsAsynchronously);
        var web = new WebView { Source = ZjuLoginPolicy.AcademicLogin.AbsoluteUri };
        web.Navigating += (_, e) => e.Cancel = !ZjuLoginPolicy.IsAllowedNavigation(e.Url);
        var accept = new Button { Text = "Use this signed-in session / 使用此登录会话" };
        var cancel = new Button { Text = "Cancel / 取消" };
        var status = new Label { Text = "Complete sign-in on the university page, then continue. / 在学校页面完成登录后继续。" };
        var layout = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) } };
        layout.Add(status, 0, 0);
        layout.Add(web, 0, 1);
        layout.Add(accept, 0, 2);
        layout.Add(cancel, 0, 3);
        var window = new Window(new ContentPage { Content = layout }) { Title = "ZJU sign-in", Width = 1000, Height = 760 };
        window.Destroying += (_, _) => completion.TrySetCanceled();
        cancel.Clicked += (_, _) => completion.TrySetCanceled();
        accept.Clicked += async (_, _) =>
        {
            accept.IsEnabled = false;
            try
            {
                completion.TrySetResult(await ReadCookiesAsync(web));
            }
            catch
            {
                status.Text = "The browser session could not be read. Retry or cancel. / 无法读取浏览器会话，请重试或取消。";
                accept.IsEnabled = true;
            }
        };
        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        app.OpenWindow(window);
        try
        {
            return await completion.Task;
        }
        finally
        {
            web.Handler?.DisconnectHandler();
            if (app.Windows.Contains(window))
            {
                app.CloseWindow(window);
            }
        }
    }
    private static async Task<CookieContainer> ReadCookiesAsync(WebView web)
    {
        var result = new CookieContainer();
#if WINDOWS
        var native = (Microsoft.UI.Xaml.Controls.WebView2?)web.Handler?.PlatformView;
        var manager = native?.CoreWebView2?.CookieManager ?? throw new InvalidOperationException("Browser is not ready.");
        foreach (var origin in ZjuLoginPolicy.CookieOrigins)
        {
            foreach (var item in await manager.GetCookiesAsync(origin.AbsoluteUri))
            {
                var cookie = new Cookie(item.Name, item.Value, item.Path, item.Domain) { Secure = item.IsSecure, HttpOnly = item.IsHttpOnly };
                if (!item.IsSession)
                {
                    cookie.Expires = DateTime.UnixEpoch.AddSeconds(item.Expires);
                }
                result.Add(cookie);
            }
        }
#elif MACCATALYST
        var native = (WebKit.WKWebView?)web.Handler?.PlatformView ?? throw new InvalidOperationException("Browser is not ready.");
        foreach (var item in await native.Configuration.WebsiteDataStore.HttpCookieStore.GetAllCookiesAsync())
        {
            var cookie = new Cookie(item.Name, item.Value, item.Path, item.Domain) { Secure = item.IsSecure, HttpOnly = item.IsHttpOnly };
            if (cookie.Domain.TrimStart('.').Equals("zju.edu.cn", StringComparison.OrdinalIgnoreCase) || ZjuLoginPolicy.CookieOrigins.Any(origin => cookie.Domain.TrimStart('.').Equals(origin.Host, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(cookie);
            }
        }
#endif
        return result;
    }
}
