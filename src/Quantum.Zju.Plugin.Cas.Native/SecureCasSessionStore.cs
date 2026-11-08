namespace Quantum.Zju.Plugin.Cas.Native;

internal sealed class SecureCasSessionStore : ICasSessionStore
{
    private const string Key = "quantum.zju.plugin.cas.session.v1";

    public Task<string?> ReadAsync() => SecureStorage.Default.GetAsync(Key);

    public Task WriteAsync(string value) => SecureStorage.Default.SetAsync(Key, value);

    public async Task ClearAsync()
    {
        var storage = SecureStorage.Default;
        if (!storage.Remove(Key) && await storage.GetAsync(Key) is not null)
        {
            throw new IOException("Could not clear the saved CAS session.");
        }
    }
}
