# ZJU CAS account plugin

[简体中文](../zh-CN/README.md) · [Glossary](glossary.md) · [Integration contract](integration.md)

`quantum.zju.plugin.cas` provides a Zhejiang University [CAS](glossary.md#cas) account connection, contributes an account section to `quantum.settings/accounts`, and issues service-bound authentication callbacks for external integration plugins. It uses the published [SDK](glossary.md#sdk) 0.3.0-rc.3. The Desktop host must implement that settings extension point; `.ci/desktop-revision.txt` pins the public host used for integration tests.

## Authentication

Open **Settings → Accounts → ZJU CAS account** and select **Open ZJU sign-in**. The native [WebView](glossary.md#webview) opens the university CAS login page. The university handles passwords, redirects and additional verification. Complete sign-in there, then choose **Use this signed-in session**. No account identifier or password is collected by the plugin form.

Windows uses WebView2 and Mac Catalyst uses WKWebView. Native cookie managers capture only cookies applicable to the CAS origin, including [HttpOnly](glossary.md#httponly) cookies, plus the browser [User-Agent](glossary.md#user-agent). Before accepting the connection, the plugin obtains a service ticket for the CAS portal itself. No academic website participates in this verification. A failed or cancelled replacement retains the previous local connection. HTTP 200 or cookie presence alone does not prove sign-in.

This plugin implements CAS, not [OIDC](glossary.md#oidc), and does not require an OIDC client registration. Third-party identity-provider navigation is not enabled; use the university account login or its on-domain verification flow.

## Responsibilities and credentials

The provider owns the native login, in-memory CAS browser cookies, current local connection and ticket acquisition. Its two [RPC](glossary.md#rpc) capabilities are `zju.cas.account` and `zju.cas.service-ticket`; see the [integration contract](integration.md).

External plugins choose their registered service URL, consume the one-use callback, establish and clear their own site cookies or tokens, and implement business requests and data parsing. The provider never follows the service callback, reads a business API, interprets academic records or exposes a general HTTP proxy. Website-specific sign-in chains remain the external integration's responsibility.

CAS cookies and passwords never appear in contributions or RPC results. The ticket callback is intentionally a short-lived credential for the requested service; consume it immediately, do not log or persist it, and never share it with unrelated code. Installed plugins are trusted local clients, not an anonymous public API. The school must recognize the service URL, and the provider additionally accepts only default-port HTTPS URLs on ZJU domains without user information, fragments or pre-existing ticket parameters.

One local account connection is supported per plugin runtime. Its opaque `sessionId` identifies a connection generation, not a university subject or verified student number. It changes on reconnect and disappears on local disconnect or plugin reload. The account capability reports local state; the next ticket request can still fail if the university session has expired. The browser may retain [SSO](glossary.md#sso) state. **Disconnect locally** releases only the provider's HTTP session; external integrations must discard their own site sessions separately. It does not globally log out CAS or clear unrelated browser data.

## Consumer compatibility

Version 0.5 uses plugin ID `quantum.zju.plugin.cas` and the two CAS capabilities. The former `quantum.zju.plugin.oidc` / `quantum.plugin.zjuam` providers and `zjuam.read-resource` contract are not aliases. Existing consumers must adopt the new contract and own their business readers before upgrading; this package does not transparently supply their old capability. Replace the previous account provider rather than leaving duplicate account contributions installed.

## Build and maintenance

Use .NET 10, the matching MAUI workload and Python 3. From the repository root:

```powershell
git submodule update --init --recursive
pwsh .ci/Build.ps1
```

The maintained helper restores, verifies formatting, builds, tests and packages `quantum.zju.plugin.cas.zip`. Windows and Mac use separate native assemblies; the payload reuses the host's public MAUI framework. Run `.ci/Verify-Host.ps1` to verify discovery, account contributions, capability dispatch and lifecycle against the pinned actual host. Tests use fictional data and do not contact the university. macOS requires its own build and native validation.

Follow the [development standards](../../.standards/docs/en/README.md). Live credentials, cookies, one-off test programs and acceptance records remain outside Git.

## Repository workflow

Owner: XmmShp. Main changes require a pull request, including administrator changes. Independent approvals, CI status checks and resolved conversations are not mandatory platform merge gates. Run relevant checks and disclose unavailable checks in the pull request. Windows and macOS CI remain configured.
