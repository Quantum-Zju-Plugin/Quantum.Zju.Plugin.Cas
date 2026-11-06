$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    python .standards/.ci/check-adoption.py
    dotnet restore Quantum.Zju.Plugin.Oidc.slnx
    dotnet format Quantum.Zju.Plugin.Oidc.slnx --verify-no-changes --no-restore
    dotnet build Quantum.Zju.Plugin.Oidc.slnx -c Release --no-restore
    dotnet test Quantum.Zju.Plugin.Oidc.slnx -c Release --no-build
    $native = 'src/Quantum.Zju.Plugin.Oidc.Native/Quantum.Zju.Plugin.Oidc.Native.csproj'
    dotnet restore $native
    dotnet format $native --verify-no-changes --no-restore
    dotnet build $native -c Release --no-restore
    $target = if ($IsWindows) { 'net10.0-windows10.0.19041.0' } else { 'net10.0-maccatalyst' }
    $platform = if ($IsWindows) { 'windows' } else { 'macos' }
    $output = "artifacts/$platform/plugin"
    dotnet publish src/Quantum.Zju.Plugin.Oidc/Quantum.Zju.Plugin.Oidc.csproj -c Release --output $output -p:DebugType=None -p:DebugSymbols=false
    Copy-Item "src/Quantum.Zju.Plugin.Oidc.Native/bin/Release/$target/Quantum.Zju.Plugin.Oidc.Native.dll" $output
    Copy-Item LICENSE,NOTICE $output
    Get-ChildItem $output -Filter '*.staticwebassets.*.json' | Remove-Item
    Compress-Archive -Path "$output/*" -DestinationPath "artifacts/$platform/quantum.zju.plugin.oidc.zip" -Force
} finally {
    Pop-Location
}
