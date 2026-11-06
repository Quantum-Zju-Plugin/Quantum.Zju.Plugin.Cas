$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $revision = (Get-Content .ci/desktop-revision.txt -Raw).Trim()
    if ($revision -notmatch '^[0-9a-f]{40}$') { throw 'Expected a fixed host revision.' }
    $target = '.ci-dependencies/Quantum.Desktop'
    if (-not (Test-Path "$target/.git")) {
        git init $target
        git -C $target fetch --depth 1 https://github.com/io7-cn/Quantum.Desktop.git $revision
        git -C $target checkout --detach $revision
    }
    if ((git -C $target rev-parse HEAD) -ne $revision) { throw 'Unexpected host revision; preserve existing checkout for inspection.' }
    dotnet restore tests/Quantum.HostIntegration.Tests/Quantum.HostIntegration.Tests.csproj -p:QuantumCoreOnly=true
    dotnet test tests/Quantum.HostIntegration.Tests/Quantum.HostIntegration.Tests.csproj -c Release -p:QuantumCoreOnly=true
} finally {
    Pop-Location
}
