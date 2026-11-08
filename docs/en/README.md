# ZJU CAS account plugin

[简体中文](../zh-CN/README.md) · [Glossary](glossary.md) · [Integration contract](integration.md) · [Releases](releases.md)

`quantum.zju.plugin.cas` provides a Zhejiang University [CAS](glossary.md#cas) account connection, contributes an account section to `quantum.settings/accounts`, and issues service-bound authentication callbacks for external integration plugins. It uses the published [SDK](glossary.md#sdk) 0.3.0-rc.3. The Desktop host must implement that settings extension point; `.ci/desktop-revision.txt` pins the public host used for integration tests.

## Authentication

Open **Settings → Accounts → ZJU CAS account** and select **Sign in**. The native [WebView](glossary.md#webview) opens the university CAS login page. The university handles passwords, redirects and additional verification. After the CAS callback is detected and verified, the window closes automatically and settings shows the verified account identifier. Reopening settings restores the current account display. The buttons use the host settings styles; no separate confirmation step is required.

If the official page reports that the login page has been idle too long and F5 cannot recover it, the browser automatically starts a fresh login once. It discards the expired flow URL and uses a new login URL while retaining CAS cookies. If the new page also expires, **Reopen** starts another fresh login without an automatic navigation loop. The detector reads only rendered error text and returns a boolean; it does not read form values.

Windows uses WebView2 and Mac Catalyst uses WKWebView. Native cookie managers capture only cookies applicable to the CAS origin, including [HttpOnly](glossary.md#httponly) cookies, plus the browser [User-Agent](glossary.md#user-agent). Before accepting the connection, the plugin obtains a service ticket for the CAS portal itself and validates it at the CAS `serviceValidate` endpoint to retrieve the account identifier. A failed verification keeps the window open with a retry action. No academic website participates in this verification. A failed or cancelled replacement retains the previous local connection. HTTP 200 or cookie presence alone does not prove sign-in.

Settings shows a single action: **Sign in** before login, or the account identifier and **Sign out** after login. To use another account, sign out and then sign in. Before navigating, the native cookie manager removes cookies applicable to the CAS origin, including legacy SSO state. The login uses the standard service flow without a forced renewal parameter. After a verified login, it removes those browser cookies again. Cookies belonging to unrelated hosts and paths are preserved.

After a successful sign-in, the plugin keeps a separate copy of the CAS cookies and browser User-Agent in the operating system's secure storage. The next time the account is read after a restart or reload, the plugin checks the saved session with CAS and restores the account if the school still accepts it. This opens no browser window and does not store a password, account identifier or service ticket. If the school session has expired, sign in again.

This plugin implements CAS, not [OIDC](glossary.md#oidc), and does not require an OIDC client registration. Third-party identity-provider navigation is not enabled; use the university account login or its on-domain verification flow.

## Responsibilities and credentials

The provider owns the native login, CAS session storage, current local connection and ticket acquisition. Its two [RPC](glossary.md#rpc) capabilities are `zju.cas.account` and `zju.cas.service-ticket`; see the [integration contract](integration.md).

External plugins choose their registered service URL, consume the one-use callback, establish and clear their own site cookies or tokens, and implement business requests and data parsing. The provider never follows the service callback, reads a business API, interprets academic records or exposes a general HTTP proxy. Website-specific sign-in chains remain the external integration's responsibility.

CAS cookies and passwords never appear in contributions or RPC results. The ticket callback is intentionally a short-lived credential for the requested service; consume it immediately, do not log or persist it, and never share it with unrelated code. Installed plugins are trusted local clients, not an anonymous public API. The school must recognize the service URL, and the provider additionally accepts only default-port HTTPS URLs on ZJU domains without user information, fragments or pre-existing ticket parameters.

One local account connection is supported per plugin runtime. Its opaque `sessionId` identifies a connection generation, not a university subject or verified student number. Account replacement or restoration after a reload creates a new identifier. The next ticket request can still fail if the university session has expired. **Sign out** clears the saved session as well as the current connection. The next interactive sign-in presents a fresh login. External integrations must discard their own site sessions separately; this does not globally log out CAS in other browsers.

## Consumer compatibility

Version 0.5 uses plugin ID `quantum.zju.plugin.cas` and the two CAS capabilities. The former `quantum.zju.plugin.oidc` / `quantum.plugin.zjuam` providers and `zjuam.read-resource` contract are not aliases. Existing consumers must adopt the new contract and own their business readers before upgrading; this package does not transparently supply their old capability. Replace the previous account provider rather than leaving duplicate account contributions installed.

## Build and maintenance

Use .NET 10, the matching MAUI workload and Python 3. From the repository root:

```powershell
git submodule update --init --recursive
pwsh .ci/Build.ps1
```

The maintained helper restores, verifies formatting, builds, tests and packages `quantum.zju.plugin.cas-<version>-<platform>.zip`. Windows and Mac use separate native assemblies; the payload reuses the host's public MAUI framework. Run `.ci/Verify-Host.ps1` to verify discovery, account contributions, capability dispatch and lifecycle against the pinned actual host. Tests use fictional data and do not contact the university. macOS requires its own build and native validation.

Follow the [development standards](../../.standards/docs/en/README.md). Live credentials, cookies, one-off test programs and acceptance records remain outside Git.

## Repository workflow

Owner: XmmShp. Main changes require a pull request, including administrator changes. Independent approvals, CI status checks and resolved conversations are not mandatory platform merge gates. Run relevant checks and disclose unavailable checks in the pull request. Windows and macOS CI remain configured.
