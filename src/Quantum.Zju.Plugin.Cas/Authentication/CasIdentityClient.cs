using NOF.Domain;
using System.Xml;
using System.Xml.Linq;

namespace Quantum.Zju.Plugin.Cas;

internal static class CasIdentityClient
{
    internal static async Task<string> GetUsernameAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var service = ZjuLoginPolicy.CasOrigin.AbsoluteUri;
        var callback = await CasTicketClient.RequestCallbackAsync(client, service, timeout.Token);
        var ticket = CasTicketClient.ReadQuery(new Uri(callback)).Single(pair => pair.Key == "ticket").Value;
        var validation = $"{ZjuLoginPolicy.CasOrigin}serviceValidate?service={Uri.EscapeDataString(service)}&ticket={Uri.EscapeDataString(ticket)}";
        try
        {
            using var response = await client.GetAsync(validation, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw InvalidIdentity();
            }
            await response.Content.LoadIntoBufferAsync(65536, timeout.Token);
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 65536
            });
            var document = await XDocument.LoadAsync(reader, LoadOptions.None, timeout.Token);
            XNamespace cas = "http://www.yale.edu/tp/cas";
            var root = document.Root;
            var results = root?.Elements().ToArray() ?? [];
            var users = results.Length == 1 && results[0].Name == cas + "authenticationSuccess"
                ? results[0].Elements(cas + "user").ToArray() : [];
            if (root?.Name != cas + "serviceResponse" || users.Length != 1 || users[0].HasElements)
            {
                throw InvalidIdentity();
            }
            var username = users[0].Value.Trim();
            if (username.Length is 0 or > 256 || username.Any(char.IsControl))
            {
                throw InvalidIdentity();
            }
            return username;
        }
        catch (XmlException)
        {
            throw InvalidIdentity();
        }
        catch (HttpRequestException)
        {
            throw new DomainException("zju_cas_transport_failed", "The university authentication service could not be reached.");
        }
    }

    private static DomainException InvalidIdentity() => new("zju_cas_identity_unavailable", "CAS could not verify the signed-in account.");
}
