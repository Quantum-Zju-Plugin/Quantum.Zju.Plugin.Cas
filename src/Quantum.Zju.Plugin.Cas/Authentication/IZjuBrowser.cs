namespace Quantum.Zju.Plugin.Cas;

public interface IZjuBrowser
{
    /// <summary>
    /// Detects the CAS callback and awaits verification before closing the browser.
    /// A failed verification leaves the browser open for retry; closing it cancels the attempt.
    /// Clears CAS browser cookies before interactive navigation and after verified handoff.
    /// The captured session cookies are independent of the native browser's cookie store.
    /// </summary>
    Task<CasBrowserSession> SignInAsync(
        Func<CasBrowserSession, CancellationToken, Task> verifySession,
        CancellationToken cancellationToken);
}
