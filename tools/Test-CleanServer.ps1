param(
    [Parameter(Mandatory)][string]$CleanSptRoot,
    [string]$CompatibilityRoot,
    [string[]]$Scenarios = @('standalone','barterizer','hardcore','both','optional-rules')
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$source = [IO.Path]::GetFullPath((Join-Path $CleanSptRoot 'SPT_Runtime'))
$stage = Join-Path $repo 'dist/package/SPT_Runtime/user/mods/HardcoreProgression'
if (-not (Test-Path -LiteralPath "$stage/HardcoreProgression.dll")) { throw 'Build the package first.' }
$results = [Collections.Generic.List[object]]::new()
$httpHeaders = @{ requestcompressed='0'; responsecompressed='0' }
function Request([int]$port, [string]$route) {
    $response = Invoke-WebRequest -Uri "https://127.0.0.1:$port$route" -Method Post -Body '{}' -ContentType 'application/json' -Headers $httpHeaders -SkipCertificateCheck -TimeoutSec 10
    $result = $response.Content | ConvertFrom-Json -AsHashtable
    if ($null -ne $result.err -and $result.err -ne 0) { throw "API error: $route" }
    if ($null -ne $result.data) { return $result.data }
    return $result
}
$index=0
$runId=Get-Date -Format 'yyyyMMdd-HHmmss'
foreach ($scenario in $Scenarios) {
    if ($scenario -notin @('standalone','barterizer','hardcore','both','optional-rules')) { throw 'Unknown test scenario.' }
    $caseRoot = [IO.Path]::GetFullPath((Join-Path $repo "artifacts/server-$scenario-$runId"))
    $allowedParent = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts')) + [IO.Path]::DirectorySeparatorChar
    if (-not $caseRoot.StartsWith($allowedParent, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture path.' }
    if (Test-Path -LiteralPath $caseRoot) { throw "Fixture already exists: $caseRoot. Preserve its evidence or choose a fresh checkout." }
    $runtime = Join-Path $caseRoot 'SPT_Runtime'
    New-Item -ItemType Directory -Path $runtime -Force | Out-Null
    robocopy $source $runtime /E /XD user logs /XF '*.log' /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw 'Clean runtime copy failed.' }
    $global:LASTEXITCODE=0
    $mods = Join-Path $runtime 'user/mods'
    New-Item -ItemType Directory -Path $mods,"$runtime/user/profiles" -Force | Out-Null
    Copy-Item -LiteralPath $stage -Destination $mods -Recurse
    $configPath = Join-Path $mods 'HardcoreProgression/config.json'
    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    $config.WriteExchangeCatalog = $true
    if ($scenario -eq 'optional-rules') { $config.EnableMedicineLimits=$true; $config.EnableSecureContainerLimit=$true }
    $config | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $configPath -Encoding utf8NoBOM
    if ($scenario -in @('barterizer','both')) {
        if (-not $CompatibilityRoot) { throw 'CompatibilityRoot is required for optional-mod tests.' }
        $target = Join-Path $mods 'SPT-Barterizer'
        New-Item -ItemType Directory -Path $target | Out-Null
        foreach ($name in @('SPT.Barterizer.dll','config.json')) {
            Copy-Item -LiteralPath (Join-Path $CompatibilityRoot "SPT_Runtime/user/mods/SPT-Barterizer/$name") -Destination $target
        }
    }
    if ($scenario -in @('hardcore','both')) {
        if (-not $CompatibilityRoot) { throw 'CompatibilityRoot is required for optional-mod tests.' }
        $target = Join-Path $mods 'HardcoreRules'
        New-Item -ItemType Directory -Path $target | Out-Null
        foreach ($name in @('HardcoreRules-Server.dll','config.json','translations.json')) {
            Copy-Item -LiteralPath (Join-Path $CompatibilityRoot "SPT_Runtime/user/mods/HardcoreRules/$name") -Destination $target
        }
        $hardcoreClient = Join-Path $caseRoot 'BepInEx/plugins/HardcoreRules'
        New-Item -ItemType Directory -Path $hardcoreClient -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $CompatibilityRoot 'BepInEx/plugins/HardcoreRules/HardcoreRules-Client.dll') -Destination $hardcoreClient
    }
    $port=17879 + $index++
    if ([Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners().Port -contains $port) { throw "Port is already in use: $port" }
    $httpPath = Join-Path $runtime 'SPT_Data/configs/http.json'
    $http=Get-Content -LiteralPath $httpPath -Raw | ConvertFrom-Json
    $http.port=$port; $http.backendPort=$port
    $http | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $httpPath -Encoding utf8NoBOM
    $stdout=Join-Path $caseRoot 'stdout.log'; $stderr=Join-Path $caseRoot 'stderr.log'
    $process=$null
    try {
        $process=Start-Process -FilePath "$runtime/SPT.Server.exe" -WorkingDirectory $runtime -RedirectStandardOutput $stdout -RedirectStandardError $stderr -WindowStyle Hidden -PassThru
        Write-Host "Started owned test server: $scenario (PID $($process.Id), port $port)"
        $deadline=[DateTime]::UtcNow.AddSeconds(150)
        $progress=$null
        while ([DateTime]::UtcNow -lt $deadline) {
            $process.Refresh()
            if ($process.HasExited) { throw "Test server exited early. Inspect $stdout and $stderr" }
            try { $progress=Request $port '/hardcore-progression/quest-progress'; if ($progress.Locales.en.Raids) { break } } catch { }
            Start-Sleep -Milliseconds 400
        }
        if (-not $progress.Locales.en.Raids) { throw "Test server did not become ready: $scenario" }
        $catalog=@(Get-Content -LiteralPath "$mods/HardcoreProgression/exchange-catalog.json" -Raw | ConvertFrom-Json)
        if ($catalog.Count -ne 178) { throw "Unexpected catalog count: $($catalog.Count)" }
        $items=Request $port '/client/items'
        $gamma=$items.'5857a8bc2459772bad15db29'._props.Grids[0]._props
        if ($scenario -eq 'optional-rules') {
            if ($gamma.cellsH -ne 2 -or $gamma.cellsV -ne 2) { throw 'Optional secure-container limit was not applied.' }
        } elseif ($gamma.cellsH -eq 2 -and $gamma.cellsV -eq 2) { throw 'Default config unexpectedly shrank the Gamma.' }
        $log=Get-Content -LiteralPath $stdout -Raw
        if ($log -match '\[HardcoreProgression\].*(failed|Cannot|skipped; original template retained)') { throw "Mod error in $scenario" }
        if ($scenario -in @('hardcore','both') -and $log -notmatch 'Starter kit applied to BEAR/USEC') { throw 'Hardcore starter kit was not applied.' }
        if ($scenario -in @('barterizer','both') -and $log -notmatch '\[Barterizer\] Ready;') { throw 'Barterizer did not initialize.' }
        $results.Add([pscustomobject]@{Scenario=$scenario;Port=$port;Catalog=$catalog.Count;Gamma="$($gamma.cellsH)x$($gamma.cellsV)";ServerReady=$true;PID=$process.Id;InGameTested=$false})
        Write-Host "PASS clean server: $scenario; $($catalog.Count) quest barters; Gamma $($gamma.cellsH)x$($gamma.cellsV)"
    } finally {
        if ($null -ne $process) {
            $process.Refresh()
            if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force; $process.WaitForExit() }
            Write-Host "Stopped owned test server PID $($process.Id)"
            $process.Dispose()
        }
    }
}
$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$repo/artifacts/clean-server-results.json" -Encoding utf8NoBOM
$global:LASTEXITCODE=0
