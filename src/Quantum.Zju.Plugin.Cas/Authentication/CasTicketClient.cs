using NOF.Domain;
using System.Net;

namespace Quantum.Zju.Plugin.Cas;

internal static class CasTicketClient
{
    internal static async Task<string> RequestCallbackAsync(HttpClient client, string serviceUrl, CancellationToken cancellationToken)
    {
        if (!ZjuLoginPolicy.IsAllowedService(serviceUrl))
        {
            throw new DomainException("zju_cas_invalid_service", "Use an official HTTPS service URL without a fragment or ticket parameter.");
        }
        var login = $"{ZjuLoginPolicy.CasOrigin}login?service={Uri.EscapeDataString(serviceUrl)}";
        try
        {
            using var response = await client.GetAsync(login, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode is not (HttpStatusCode.Found or HttpStatusCode.SeeOther)
                || response.Headers.Location is null)
            {
                throw new DomainException("zju_cas_ticket_unavailable", "CAS did not issue a service ticket. Check the service or sign in again.");
            }
            var callback = new Uri(new Uri(login), response.Headers.Location);
            var service = new Uri(serviceUrl);
            var parameters = ReadQuery(callback);
            var tickets = parameters.Where(pair => pair.Key == "ticket").ToArray();
            if (!ZjuLoginPolicy.IsAllowedNavigation(callback.AbsoluteUri) || callback.Fragment.Length != 0
                || callback.GetLeftPart(UriPartial.Path) != service.GetLeftPart(UriPartial.Path)
                || tickets.Length != 1 || !tickets[0].Value.StartsWith("ST-", StringComparison.Ordinal)
                || tickets[0].Value.Length <= 3 || tickets[0].Value.Length > 8192 || tickets[0].Value.Any(char.IsWhiteSpace)
                || !parameters.Where(pair => pair.Key != "ticket").OrderBy(pair => pair.Key, StringComparer.Ordinal).ThenBy(pair => pair.Value, StringComparer.Ordinal)
                    .SequenceEqual(ReadQuery(service).OrderBy(pair => pair.Key, StringComparer.Ordinal).ThenBy(pair => pair.Value, StringComparer.Ordinal)))
            {
                throw new DomainException("zju_cas_invalid_callback", "CAS returned an unexpected service callback.");
            }
            return callback.AbsoluteUri;
        }
        catch (HttpRequestException)
        {
            throw new DomainException("zju_cas_transport_failed", "The university authentication service could not be reached.");
        }
    }

    internal static (string Key, string Value)[] ReadQuery(Uri uri) => uri.Query.TrimStart('?')
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.Split('=', 2))
        .Select(parts => (Uri.UnescapeDataString(parts[0].Replace('+', ' ')),
            Uri.UnescapeDataString((parts.Length == 2 ? parts[1] : "").Replace('+', ' '))))
        .ToArray();
}
