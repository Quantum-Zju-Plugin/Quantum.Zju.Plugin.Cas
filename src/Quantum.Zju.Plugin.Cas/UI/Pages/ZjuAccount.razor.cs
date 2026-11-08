using Microsoft.AspNetCore.Components;
using System.Globalization;
namespace Quantum.Zju.Plugin.Cas.UI.Pages;

public partial class ZjuAccount(ZjuCasSession session) : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private CasAccount _account = new();
    private string? _error;
    private bool _busy;
    private static string Text(string en, string zh) => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? zh : en;

    protected override async Task OnInitializedAsync()
    {
        _busy = true;
        try
        {
            _account = await session.GetAccountAsync(_lifetime.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task SignInAsync()
    {
        if (_busy)
        {
            return;
        }
        _busy = true;
        _error = null;
        try
        {
            await session.SignInAsync(_lifetime.Token);
            _account = await session.GetAccountAsync(_lifetime.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            _error = Text("Sign-in failed. Please try again.", "登录失败，请重试。");
        }
        finally
        {
            _busy = false;
        }
    }
    private async Task SignOutAsync()
    {
        if (_busy)
        {
            return;
        }
        _busy = true;
        _error = null;
        try
        {
            await session.SignOutAsync(_lifetime.Token);
            _account = new();
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            _error = Text("Sign-out failed. Please try again.", "退出失败，请重试。");
        }
        finally
        {
            _busy = false;
        }
    }
    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
