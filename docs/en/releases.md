# Releases

[简体中文](../zh-CN/releases.md) · [Documentation](README.md)

The version source is `src/Quantum.Zju.Plugin.Cas/plugin.json`. Before 1.0, compatible fixes use patch versions, compatible features use minor versions, and incompatible contracts require an explicitly documented minor-version migration. The public integration surface is the plugin ID and its RPC capabilities, payloads and lifecycle behavior.

## 0.6.0 — unreleased

- Use the host settings button styles. Before login show only **Sign in**; after login show the account identifier and **Sign out**.
- Detect the CAS callback, verify the account and close the native login window automatically.
- Add the optional `username` account field returned by CAS ticket validation. Existing account and service-ticket requests remain compatible with 0.5.0.
- Restart an expired login page once with a new flow URL; repeated expiration offers **Reopen** without a navigation loop.
- Clear CAS-scoped native browser cookies before login and after verified session handoff. Sign-out releases the independent HTTP session, so a subsequent login can use another account. The university login uses its standard service flow without `renew=true`.
- Record diagnostic phase names, error codes and failure types without recording account identifiers, passwords, cookies, tickets or response bodies.
- Produce separate, reproducible Windows and macOS ZIP files with SDK metadata, SHA-256 checksums, a software bill of materials and build provenance. CI retains the candidate only after packaging and pinned-host checks pass.

Compatibility: build and install with SDK `0.3.0-rc.3` and a Desktop host implementing `quantum.settings/accounts`. The archive contains a platform-specific native adapter. Select the archive for the host operating system; there is no universal native ZIP. No database or configuration migration is required. Reopen account settings and sign in after upgrading.

Recovery: if login, account display or service-ticket acquisition fails, stop promotion and reinstall the immutable 0.5.0 Windows package, or publish a forward fix with a new version. Version 0.5.0 has the older manual confirmation and local-disconnect limitations. External integrations keep responsibility for their own sessions. Do not replace an existing release archive or move a published tag.

### Publication gates

Merge through a pull request under the repository profile. Verify the exact protected-history commit on Windows and macOS, retain both CI artifacts and create a signed annotated `v0.6.0` tag at that verified commit. Promote those exact archives without rebuilding. Verify each published download against its checksum, source revision and SDK metadata before submitting the Windows archive to the plugin marketplace. Native credential handling may require marketplace manual review; a submission or pending review is not publication.

Run `.ci/Build.ps1` and `.ci/Verify-Host.ps1`. Packaging emits `artifacts/<platform>/quantum.zju.plugin.cas-0.6.0-<platform>.zip` plus platform-specific release metadata, checksum, SPDX inventory and provenance files. Local builds explicitly record whether the source tree is dirty; only clean, successful CI inputs may be promoted. Keep source/check/asset links and actual acceptance results in the release and pull request records, outside source control.

## 0.5.0 — 2026-11-06

Windows marketplace release providing `zju.cas.account` and `zju.cas.service-ticket` with SDK `0.3.0-rc.3`. Business integrations own their callbacks, website sessions and data readers. Source: [`711bb453`](https://github.com/XmmShp/Quantum.Zju.Plugin.Cas/commit/711bb453a8ff48b1dc967cf7918cf11bb2bdf6f8). macOS was not included or validated in this release.
