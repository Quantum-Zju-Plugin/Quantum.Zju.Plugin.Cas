# ZJU identity plugin
[简体中文](../zh-CN/README.md) · [Glossary](glossary.md)

The plugin `quantum.zju.plugin.oidc` contributes a ZJU account section to `quantum.settings/accounts` using the published [SDK](glossary.md#sdk) 0.3.0-rc.3. It requires a Desktop host implementing that extension point. The integration fixture pins the public host commit in `.ci/desktop-revision.txt`; source references to sibling checkouts are not required.

## Authentication

Open **Settings → Accounts → ZJU account**, enter the account identifier used by legacy academic resource calls, and select **Open ZJU sign-in**. A native [WebView](glossary.md#webview) opens the university's official academic login entry. The school handles password encryption, redirects and any additional verification. Complete sign-in there, then choose **Use this signed-in session**. Passwords are not collected by plugin forms. Third-party identity-provider navigation is not currently enabled; use the university account login or its on-domain verification flow.

The adapter copies university cookies, including [HttpOnly](glossary.md#httponly) cookies, through native APIs. Windows uses WebView2; Mac Catalyst uses WKWebView. Cookie retrieval includes the `/cas/`, `/jwglxt/` and `/api/` paths. The plugin validates an independent authenticated academic JSON read before committing the new in-memory session. A failed or cancelled replacement leaves the previous session intact. HTTP 200 alone does not prove authentication.

The current university route is [CAS](glossary.md#cas), despite the requested plugin identifier containing [OIDC](glossary.md#oidc). This implementation does not pretend that CAS tickets are OIDC tokens and does not add an unsupported OIDC client configuration.

The native adapter is a platform-specific assembly supplied next to the portable plugin entry assembly. It uses the host's public MAUI framework, without copying host code, inspecting host internals or altering the SDK. Build a separate plugin archive for each operating system. macOS requires its own build and native validation.

## Session boundaries

Cookies remain within local trusted .NET services, never in extension JSON, RPC results, logs or release artifacts. The university browser store may retain its own SSO session. **Disconnect locally** releases the plugin's HTTP session; it does not globally log out the university account or clear other host browser data. The account identifier is user-supplied routing metadata for the legacy API, not an independent identity assertion.

Only approved read operations are exposed through [RPC](glossary.md#rpc): `zjuam.read-resource` accepts `xzzd-todos`, `zdbk-courses`, `zdbk-sections`, `zdbk-selected`, `zdbk-graduation` or `zdbk-introduction`. Requests and response namespaces remain compatible with existing Zdbk/Xzzd consumers. Install one provider of this unique capability; replace the old `quantum.plugin.zjuam` plugin rather than enabling both.

## Build and maintenance

Use .NET 10, the matching MAUI workload and Python 3. Windows and Mac builds use the corresponding native target. From the repository root:

```powershell
git submodule update --init --recursive
pwsh .ci/Build.ps1
```

The maintained helper restores, verifies formatting, builds, tests and packages. It excludes framework assemblies already supplied by Desktop from the native adapter payload. Run `.ci/Verify-Host.ps1` for discovery, settings contribution and JSON dispatch against the pinned actual host. Tests use fictional data and do not contact the university. Live account files, cookies, personal records and one-off verification scripts/results must remain outside Git. See the [development standards](../../.standards/docs/en/README.md).
