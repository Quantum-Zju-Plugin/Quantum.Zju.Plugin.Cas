# ZJU identity plugin instructions
- Follow the [English standards](.standards/docs/en/README.md) and [Git branching rules](.standards/docs/en/02-development/git-branching.md). Inspect the working tree, use a topic branch and merge through a pull request using this repository profile; never directly edit or push protected branches.
- Read [English documentation](docs/en/README.md). Maintain paired, mutually linked docs/en and docs/zh-CN files. Keep AGENTS.md English only.
- This repository is public under Apache-2.0. Audit source and Git history for secrets, private paths and one-off evidence before publication.
- Keep one-off scripts, acceptance results and credentials out of version control under [MNT-09](.standards/docs/en/05-maintenance/maintenance-standards.md#keep-acceptance-records-outside-source-repositories).
- Use the published SDK package; do not reference sibling source or Desktop internals. Preserve zjuam.read-resource transport compatibility.
- Never log passwords, cookies, authorization tickets, response bodies or personal academic data. Only the official HTTPS login page accepts credentials.
- Keep UI, session management and native browser adapters separate. Tests mirror source feature folders. Reuse framework cookie managers.
- Run all dotnet commands elevated on this Windows machine. Restore, format, build and test affected projects. Native macOS verification requires macOS.

- Repository profile: main requires a pull request, including administrator changes. Separate approving reviews, required CI status checks and resolved-conversation gates are not configured. Run relevant checks, report failures honestly and request review when useful. See the [repository workflow](docs/en/README.md#repository-workflow).
