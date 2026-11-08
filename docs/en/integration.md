# External integration contract

[简体中文](../zh-CN/integration.md) · [Documentation](README.md) · [Glossary](glossary.md)

## Account metadata

Discover `zju.cas.account` through the host [RPC](glossary.md#rpc) catalog and send `{}`. The response value contains:

```json
{"provider":"zju","protocol":"cas","isSignedIn":true,"sessionId":"opaque-local-connection-id","username":"fictional-student"}
```

The additive `username` field is the account identifier returned by CAS ticket validation, not an entered login alias. When disconnected, `isSignedIn` is false and both `sessionId` and `username` are null. The identifier changes on reconnect. It is not a student number, verified identity claim, browser cookie or access token. This call checks local state without network access. Only the account settings UI initiates interactive sign-in; background capability calls do not open a login window.

## Service authentication

Call `zju.cas.service-ticket` with the current `sessionId` and the exact [CAS](glossary.md#cas) service identifier advertised by the target website:

```json
{"sessionId":"opaque-local-connection-id","serviceUrl":"https://service.zju.edu.cn/callback"}
```

The example domain represents a hypothetical registered service, not a built-in website integration. The response value has four fields:

```json
{"sessionId":"opaque-local-connection-id","serviceUrl":"https://service.zju.edu.cn/callback","callbackUrl":"https://service.zju.edu.cn/callback?ticket=ST-placeholder","userAgent":"Browser/placeholder"}
```

`callbackUrl` carries a [service ticket](glossary.md#service-ticket). It is a credential, valid only for its service and one validation attempt. Do not log, persist, cache or reuse it. The provider verifies its scheme, host, path, existing query parameters and ticket structure before returning it. It never follows this callback and never returns its own [ticket-granting cookie](glossary.md#tgc). The university decides whether the requested service is registered and authorized. A service must use default-port HTTPS under `zju.edu.cn`, with no user information, fragment or pre-existing `ticket` parameter. Legacy HTTP-only service identifiers are currently unsupported.

The consumer must:

1. Keep its own site session, separate from the account provider's session.
2. Apply the supplied [User-Agent](glossary.md#user-agent) to its authentication requests. Build a client with its own framework cookie container, not an exported CAS cookie jar.
3. Consume the callback immediately using the target website's documented login flow. Some sites accept the callback directly; others exchange the ticket at a same-site login endpoint. Do not first call the CAS validation endpoint yourself and then reuse that consumed ticket.
4. Follow only the integration's approved secure destinations, and verify a protected response rather than HTTP 200 alone. Store any resulting site cookie or token inside that integration.
5. Implement business requests, schemas, identity/profile extraction and data parsing outside this repository. If an integration needs a university identifier, obtain it from its authenticated website profile; do not infer it from `sessionId`.
6. Drop its own site session on local disconnect/reconnect as appropriate, and reacquire the current account metadata when establishing a new session. A provider reload also invalidates the old connection identifier.

Both capabilities have one provider and single-provider ordered dispatch. External integrations declare the capabilities they use in their manifests. No general authenticated HTTP forwarding, arbitrary browser script execution or business-specific resource names are part of this contract.

## Errors and lifetime

| Error code | Meaning |
| --- | --- |
| `zju_cas_sign_in_required` | No local account connection; ask the user to sign in through account settings. |
| `zju_cas_session_changed` | The supplied connection identifier is stale; read account metadata again. |
| `zju_cas_invalid_service` | The service identifier violates the destination policy. |
| `zju_cas_ticket_unavailable` | No valid redirect ticket was issued; the site may not be authorized or sign-in may have expired. |
| `zju_cas_invalid_callback` | The redirect does not match the requested service or valid ticket structure. |
| `zju_cas_transport_failed` | The authentication service could not be reached. |

Requests can be cancelled. Do not automatically resubmit passwords, invent a school client registration or disable certificate validation. A failed service request does not erase an otherwise usable local account connection. The settings sign-in flow can replace a connection; a failed replacement preserves the previous one. Disconnecting locally does not revoke sessions already created by external integrations or perform university-wide logout.

The [CAS protocol specification](https://apereo.github.io/cas/development/protocol/CAS-Protocol-Specification.html) defines the service-bound, single-use ticket semantics. This plugin is a client of that authentication service, not a new identity server.
