param([Parameter(Mandatory)][string]$SptRoot, [string]$CompatibilityRoot, [switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME = Join-Path $repo '.dotnet-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$version = ([xml](Get-Content -LiteralPath "$repo/src/HardcoreProgression.csproj" -Raw)).Project.PropertyGroup.Version
Push-Location $repo
try {
    dotnet build src/HardcoreProgression.csproj -c Release "-p:SptRoot=$SptRoot"
    if ($LASTEXITCODE -ne 0) { throw 'Server build failed.' }
    & "$repo/client/Build-Client.ps1" -GameRoot $SptRoot
    if (-not $SkipTests) {
        $testArgs = @($SptRoot)
        if ($CompatibilityRoot) { $testArgs += $CompatibilityRoot }
        dotnet run --project tests -c Release "-p:SptRoot=$SptRoot" -- @testArgs
        if ($LASTEXITCODE -ne 0) { throw 'Server verification failed.' }
        dotnet run --project tests/client-bindings -c Release -- $SptRoot "$repo/client/output"
        if ($LASTEXITCODE -ne 0) { throw 'Client binding verification failed.' }
    }
    foreach ($dll in @("$repo/src/bin/Release/net10.0/HardcoreProgression.dll", "$repo/client/output/HardcoreProgression.Client.dll")) {
        $fileVersion = [Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString()
        if ($fileVersion -ne "$version.0") { throw "Version mismatch: $dll ($fileVersion)" }
    }
    $stage = [IO.Path]::GetFullPath((Join-Path $repo 'dist/package'))
    $safeParent = [IO.Path]::GetFullPath((Join-Path $repo 'dist')) + [IO.Path]::DirectorySeparatorChar
    if (-not $stage.StartsWith($safeParent, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe staging path.' }
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
    $server = Join-Path $stage 'SPT_Runtime/user/mods/HardcoreProgression'
    $client = Join-Path $stage 'BepInEx/plugins/HardcoreProgression'
    New-Item -ItemType Directory -Path $server,$client -Force | Out-Null
    Copy-Item -LiteralPath "$repo/src/bin/Release/net10.0/HardcoreProgression.dll" -Destination $server
    Copy-Item -LiteralPath "$repo/client/output/HardcoreProgression.Client.dll" -Destination $client
    foreach ($name in @('config.json','offers.json','starter-kit.json','README.md','README.ru.md','LICENSE','THIRD-PARTY-NOTICES.md')) {
        Copy-Item -LiteralPath (Join-Path $repo $name) -Destination $server
    }
    Copy-Item -LiteralPath "$repo/locales" -Destination $server -Recurse
    Copy-Item -LiteralPath "$repo/licenses" -Destination $server -Recurse
    $zip = Join-Path $repo "dist/HardcoreProgression-$version.zip"
    Compress-Archive -LiteralPath (Join-Path $stage 'SPT_Runtime'),(Join-Path $stage 'BepInEx') -DestinationPath $zip -Force
    Get-ChildItem -LiteralPath $stage -Recurse -Filter '*.dll' | ForEach-Object {
        $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
        [pscustomobject]@{File=$_.Name;SHA256=$hash.Hash.ToLowerInvariant();VirusTotal="https://www.virustotal.com/gui/file/$($hash.Hash.ToLowerInvariant())/detection"}
    } | ConvertTo-Json | Set-Content -LiteralPath "$repo/dist/binary-manifest.json" -Encoding utf8NoBOM
    $zipHash = Get-FileHash -LiteralPath $zip -Algorithm SHA256
    "$($zipHash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($zip))" | Set-Content -LiteralPath "$repo/dist/SHA256SUMS.txt" -Encoding utf8NoBOM
    Write-Host "Packaged without deployment: $zip"
} finally { Pop-Location }
