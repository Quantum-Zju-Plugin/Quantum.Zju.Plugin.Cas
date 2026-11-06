using Microsoft.AspNetCore.Components;
using System.Globalization;
namespace Quantum.Zju.Plugin.Cas.UI.Pages;

public partial class ZjuAccount(ZjuCasSession session) : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
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
            await session.SignInAsync(_lifetime.Token);
            _status = Text("ZJU CAS account connected.", "已连接浙大 CAS 账号。");
        }
        catch (OperationCanceledException)
        {
            _status = Text("Sign-in cancelled.", "已取消登录。");
        }
        catch
        {
            _status = Text("Could not verify the CAS session. Complete official sign-in and check the network, then retry.", "未能验证 CAS 会话，请完成学校官方登录并检查网络后重试。");
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
