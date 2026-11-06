$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    python .standards/.ci/check-adoption.py
    python -m unittest discover -s tests/packaging -p 'test_*.py'
    if ($LASTEXITCODE -ne 0) { throw 'Release packaging tests failed.' }
    dotnet restore Quantum.Zju.Plugin.Cas.slnx
    dotnet format Quantum.Zju.Plugin.Cas.slnx --verify-no-changes --no-restore
    dotnet build Quantum.Zju.Plugin.Cas.slnx -c Release --no-restore
    dotnet test Quantum.Zju.Plugin.Cas.slnx -c Release --no-build
    $native = 'src/Quantum.Zju.Plugin.Cas.Native/Quantum.Zju.Plugin.Cas.Native.csproj'
    dotnet restore $native
    dotnet format $native --verify-no-changes --no-restore
    dotnet build $native -c Release --no-restore
    $target = if ($IsWindows) { 'net10.0-windows10.0.19041.0' } else { 'net10.0-maccatalyst' }
    $platform = if ($IsWindows) { 'windows' } else { 'macos' }
    $output = "artifacts/$platform/plugin"
    $artifactRoot = [IO.Path]::GetFullPath((Join-Path (Get-Location).Path 'artifacts')) + [IO.Path]::DirectorySeparatorChar
    $outputPath = [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $output))
    if (-not $outputPath.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected package output path.' }
    if (Test-Path -LiteralPath $outputPath) { Remove-Item -LiteralPath $outputPath -Recurse -Force }
    dotnet publish src/Quantum.Zju.Plugin.Cas/Quantum.Zju.Plugin.Cas.csproj -c Release --output $output -p:DebugType=None -p:DebugSymbols=false
    Copy-Item "src/Quantum.Zju.Plugin.Cas.Native/bin/Release/$target/Quantum.Zju.Plugin.Cas.Native.dll" $output
    Copy-Item LICENSE,NOTICE $output
    Get-ChildItem $output -Filter '*.staticwebassets.*.json' | Remove-Item
    python .ci/package-plugin.py $output $platform
    if ($LASTEXITCODE -ne 0) { throw 'Release packaging failed.' }
} finally {
    Pop-Location
}
