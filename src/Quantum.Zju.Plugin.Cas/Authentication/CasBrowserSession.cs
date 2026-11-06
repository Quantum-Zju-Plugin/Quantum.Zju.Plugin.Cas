using System.Net;

namespace Quantum.Zju.Plugin.Cas;

public sealed record CasBrowserSession(CookieContainer Cookies, string UserAgent);
