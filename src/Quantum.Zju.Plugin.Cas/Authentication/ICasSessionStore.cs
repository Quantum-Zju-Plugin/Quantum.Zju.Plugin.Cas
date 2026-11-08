namespace Quantum.Zju.Plugin.Cas;

internal interface ICasSessionStore
{
    Task<string?> ReadAsync();
    Task WriteAsync(string value);
    Task ClearAsync();
}
