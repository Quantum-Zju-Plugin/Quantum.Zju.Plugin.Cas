using Microsoft.AspNetCore.Components;
using System.Globalization;
namespace Quantum.OfficialPlugins.Zjuam.UI.Pages;

public partial class ZjuAccount(ZjuamSession session) : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private string _username = "";
    private string _status = "";
    private bool _busy;
    private static string Text(string en, string zh) => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? zh : en;
    private async Task SignInAsync()
    {
        if (_busy)
        {
            return;
        }
        _busy = true;
        try
        {
            await session.SignInAsync(_username, _lifetime.Token);
            _status = Text("Academic access verified.", "教务访问验证通过。");
        }
        catch (OperationCanceledException)
        {
            _status = Text("Sign-in cancelled.", "已取消登录。");
        }
        catch
        {
            _status = Text("Could not verify academic access. Check the account and network, then retry.", "未能验证教务访问，请检查账号和网络后重试。");
        }
        finally
        {
            _busy = false;
        }
    }
    private async Task SignOutAsync()
    {
        await session.SignOutAsync(_lifetime.Token);
        _status = Text("Disconnected locally. The university browser session may remain signed in.", "已断开本地连接，学校浏览器会话可能仍保持登录。");
    }
    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
