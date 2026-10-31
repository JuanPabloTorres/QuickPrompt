. (Join-Path $PSScriptRoot 'Companion.Startup.ps1')
#requires -Version 7.2
# Package/configuration rules only; credentials belong exclusively to the existing Companion.
Set-StrictMode -Version Latest

function Assert-CompanionPackage([string]$Root) {
    $manifestPath = Join-Path $Root 'package-manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'PACKAGE_MANIFEST_MISSING' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.schema -notin @(1,2) -or $manifest.model -cne 'qwen3:8b' -or $manifest.packageVersion -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw 'PACKAGE_MANIFEST_INVALID' }
    if ($manifest.schema -eq 1) {
        if ($manifest.provider -cne 'ollama') { throw 'PACKAGE_MANIFEST_INVALID' }
    } elseif (@($manifest.providers).Count -ne 2 -or $manifest.providers[0] -cne 'ollama' -or $manifest.providers[1] -cne 'docker-model-runner') {
        throw 'PACKAGE_MANIFEST_INVALID'
    }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $manifest.files) {
        $relative = [string]$entry.path
        if (-not $relative -or [IO.Path]::IsPathRooted($relative) -or $relative.Contains('\') -or $relative.Split('/') -contains '..' -or
            -not $seen.Add($relative) -or $entry.sha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'PACKAGE_MANIFEST_INVALID' }
        $path = Join-Path $Root $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).LinkTarget -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ine $entry.sha256) { throw 'PACKAGE_FILE_CHANGED' }
    }
    foreach ($required in @('app/QuickPrompt.Companion.dll','app/QuickPrompt.Companion.runtimeconfig.json','Companion.ps1',
        'Companion.Package.ps1','Resolve-NovaDotnet.ps1','Test-PythonRequirements.py','runtime/nova_worker.py',
        'runtime/sdk/adaptive_core/host.py','runtime/sdk/adaptive_core/inference.py','runtime/requirements-inference.lock.txt')) {
        if (-not $seen.Contains($required)) { throw 'PACKAGE_MANIFEST_INCOMPLETE' }
    }
    if ($manifest.schema -eq 2 -and (-not $seen.Contains('runtime/sdk/adaptive_core/dmr_framework.py') -or -not $seen.Contains('companion.dmr.example.json'))) {
        throw 'PACKAGE_MANIFEST_INCOMPLETE'
    }
    foreach ($file in (Get-ChildItem -LiteralPath $Root -Recurse -File)) {
        $relative = [IO.Path]::GetRelativePath($Root,$file.FullName).Replace('\','/')
        if ($relative -notin @('package-manifest.json','companion.json') -and -not $seen.Contains($relative)) { throw 'PACKAGE_UNLISTED_FILE' }
    }
}

function Read-CompanionConfiguration([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf) -or (Get-Item -LiteralPath $Path).Length -gt 16384) { throw 'CONFIGURATION_MISSING_OR_TOO_LARGE' }
    $config = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
    $keys = @('version','server','slot','name','capability','python','dotnet','endpoint','maximumJobs')
    if ($config -isnot [Collections.IDictionary]) { throw 'CONFIGURATION_FIELDS_INVALID' }
    if ($config.version -in @(2,3)) { $keys += @('provider','physicalModel','thinking') }
    if ($config.version -eq 3) { $keys = @($keys | Where-Object { $_ -ne 'maximumJobs' }) + @('sessionMode') }
    if ($config.Count -ne $keys.Count) { throw 'CONFIGURATION_FIELDS_INVALID' }
    foreach ($key in $config.Keys) { if ($key -cnotin $keys) { throw 'CONFIGURATION_FIELDS_INVALID' } }
    if ($config.version -isnot [long] -and $config.version -isnot [int]) { throw 'CONFIGURATION_VERSION_INVALID' }
    if ($config.version -notin @(1,2,3)) { throw 'CONFIGURATION_VERSION_INVALID' }
    foreach ($key in @('server','slot','name','capability','python','dotnet','endpoint')) {
        if ($config[$key] -isnot [string]) { throw 'CONFIGURATION_FIELD_TYPE_INVALID' }
    }
    $server = $null
    if (-not [uri]::TryCreate($config.server, [UriKind]::Absolute, [ref]$server) -or $server.Scheme -ne 'https' -or
        $server.AbsolutePath -ne '/' -or $server.UserInfo -or $server.Query -or $server.Fragment) { throw 'SERVER_ORIGIN_INVALID' }
    if ($config.slot -cnotmatch '^[A-Za-z0-9-]{1,40}$' -or $config.name.Length -notin 1..80 -or
        $config.capability -cnotin @('nova','prompts')) { throw 'PROFILE_CONFIGURATION_INVALID' }
    if ($config.version -eq 3) {
        if ($config.sessionMode -isnot [string] -or $config.sessionMode -cne 'connected') { throw 'SESSION_MODE_INVALID' }
    } elseif (($config.maximumJobs -isnot [int] -and $config.maximumJobs -isnot [long]) -or $config.maximumJobs -notin 1..50) { throw 'JOB_LIMIT_INVALID' }
    $endpoint = $null; $address = $null
    if (-not [uri]::TryCreate($config.endpoint, [UriKind]::Absolute, [ref]$endpoint) -or $endpoint.Scheme -ne 'http' -or
        $endpoint.AbsolutePath -ne '/' -or $endpoint.UserInfo -or $endpoint.Query -or $endpoint.Fragment -or
        -not [Net.IPAddress]::TryParse($endpoint.DnsSafeHost.Trim('[',']'), [ref]$address) -or
        -not [Net.IPAddress]::IsLoopback($address)) { throw 'OLLAMA_ENDPOINT_MUST_BE_LOOPBACK' }
    if ($config.version -in @(2,3)) {
        if ($config.provider -isnot [string] -or $config.provider -cne 'docker-model-runner' -or $config.physicalModel -isnot [string] -or
            $config.physicalModel.Length -gt 240 -or $config.physicalModel.Contains('://') -or $config.physicalModel -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._:/-]*@sha256:[a-f0-9]{64}$' -or
            $config.thinking -isnot [bool] -or $endpoint.Port -ne 12434) { throw 'DMR_CONFIGURATION_INVALID' }
    }
    return $config
}

# Configuration is validated before this pure projection. Only run can select
# lifetime policy; status/pair/disconnect never receive ignored session budgets.
function Get-CompanionSessionArguments([Collections.IDictionary]$Configuration, [string]$Command) {
    if ($Command -ne 'run') { return @() }
    if ($Configuration.version -eq 3) { return @('--session-mode','connected') }
    return @('--max-jobs',[string]$Configuration.maximumJobs)
}

# Only an explicit local v2/v3 profile can enable remote proposal thinking.
# This permission is not projected to pairing, status or disconnection commands.
function Get-CompanionThinkingArguments([Collections.IDictionary]$Configuration, [string]$Command) {
    if ($Command -ne 'run' -or $Configuration.version -eq 1) { return @() }
    return @('--thinking',$Configuration.thinking.ToString().ToLowerInvariant())
}

function Resolve-CompanionExecutable([string]$Configured, [string]$Name, [string]$ConfigDirectory) {
    if ($Configured) {
        $path = if ([IO.Path]::IsPathRooted($Configured)) { $Configured } else { Join-Path $ConfigDirectory $Configured }
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw 'CONFIGURED_EXECUTABLE_MISSING' }
        return [IO.Path]::GetFullPath($path)
    }
    $found = Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $found) { throw 'PREREQUISITE_EXECUTABLE_MISSING' }
    if ($found.Source -match '[\\/]Microsoft[\\/]WindowsApps[\\/]python(3)?\.exe$') { throw 'PYTHON_STORE_ALIAS_IS_NOT_AN_INSTALLED_RUNTIME' }
    return $found.Source
}

function Test-CompanionOllama([uri]$Endpoint) {
    $handler = [Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false; $handler.UseProxy = $false; $handler.UseCookies = $false
    $client = [Net.Http.HttpClient]::new($handler); $client.Timeout = [TimeSpan]::FromSeconds(3)
    $deadline = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(3))
    try {
        $response = $client.GetAsync([uri]::new($Endpoint, 'api/tags'), [Net.Http.HttpCompletionOption]::ResponseHeadersRead, $deadline.Token).GetAwaiter().GetResult()
        try {
            if (-not $response.IsSuccessStatusCode) { return 'OLLAMA_API_REJECTED' }
            $stream = $response.Content.ReadAsStream(); $buffer = [byte[]]::new(131073); $total = 0
            while ($total -lt $buffer.Length) {
                $read = $stream.ReadAsync([Memory[byte]]::new($buffer, $total, $buffer.Length - $total), $deadline.Token).GetAwaiter().GetResult()
                if ($read -eq 0) { break }; $total += $read
            }
            if ($total -gt 131072) { return 'OLLAMA_REPLY_TOO_LARGE' }
            $json = [Text.Encoding]::UTF8.GetString($buffer, 0, $total) | ConvertFrom-Json
            if (@($json.models | Where-Object { $_.name -ceq 'qwen3:8b' }).Count -eq 0) { return 'QWEN3_8B_NOT_LISTED' }
            return 'MODEL_LISTED_NOT_INFERENCE_VERIFIED'
        } finally { $response.Dispose() }
    } catch { return 'OLLAMA_API_UNAVAILABLE' }
    finally { $deadline.Dispose(); $client.Dispose(); $handler.Dispose() }
}

# Pure metadata contract, also used by synthetic tests. This does not establish
# loaded weights, GPU availability, inference quality or an authorized account.
function Test-CompanionDmrMetadata([object]$Metadata, [string]$PhysicalModel, [bool]$Thinking = $false) {
    if ($Metadata -isnot [array] -or $Metadata.Count -ne 1 -or $Metadata[0] -isnot [Collections.IDictionary]) { return 'DMR_CONFIGURATION_NOT_READY' }
    $entry=$Metadata[0]; $digest=$PhysicalModel.Substring($PhysicalModel.LastIndexOf('@')+1)
    if ($entry['ModelID'] -isnot [string] -or $entry['Backend'] -isnot [string] -or $entry['Mode'] -isnot [string] -or
        $entry['ModelID'] -cne $digest -or $entry['Backend'] -cne 'llama.cpp' -or $entry['Mode'] -cne 'completion' -or
        $entry['Config'] -isnot [Collections.IDictionary]) { return 'DMR_CONFIGURATION_NOT_READY' }
    $config=$entry['Config']
    if (($config['context-size'] -isnot [int] -and $config['context-size'] -isnot [long]) -or $config['context-size'] -ne 12288 -or
        $config['llamacpp'] -isnot [Collections.IDictionary] -or
        ($config['llamacpp']['reasoning-budget'] -isnot [int] -and $config['llamacpp']['reasoning-budget'] -isnot [long]) -or
        ($Thinking -and $config['llamacpp']['reasoning-budget'] -ne -1) -or
        (-not $Thinking -and $config['llamacpp']['reasoning-budget'] -notin @(0,-1)) -or
        $config['runtime-flags'] -isnot [array] -or @($config['runtime-flags']).Count -ne 2 -or
        $config['runtime-flags'][0] -isnot [string] -or $config['runtime-flags'][1] -isnot [string] -or
        $config['runtime-flags'][0] -cne '--parallel' -or $config['runtime-flags'][1] -cne '1') { return 'DMR_CONFIGURATION_NOT_READY' }
    return 'DMR_MODEL_CONFIGURED_NOT_INFERENCE_VERIFIED'
}

function Test-CompanionDmr([uri]$Endpoint, [string]$PhysicalModel, [bool]$Thinking = $false) {
    $address=$null
    if ($Endpoint.Scheme -ne 'http' -or $Endpoint.Port -ne 12434 -or $Endpoint.AbsolutePath -ne '/' -or
        $Endpoint.UserInfo -or $Endpoint.Query -or $Endpoint.Fragment -or
        -not [Net.IPAddress]::TryParse($Endpoint.DnsSafeHost.Trim('[',']'),[ref]$address) -or -not [Net.IPAddress]::IsLoopback($address) -or
        $PhysicalModel.Length -gt 240 -or $PhysicalModel.Contains('://') -or $PhysicalModel -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._:/-]*@sha256:[a-f0-9]{64}$') { return 'DMR_CONFIGURATION_INVALID' }
    $handler=[Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect=$false; $handler.UseProxy=$false; $handler.UseCookies=$false
    $client=[Net.Http.HttpClient]::new($handler); $client.Timeout=[TimeSpan]::FromSeconds(3)
    $deadline=[Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(3))
    try {
        # Read the same native configuration identity as Adaptive Core. There is
        # no POST, model pull, daemon start, worker or account request in doctor.
        $url=[uri]::new($Endpoint,'engines/_configure?model='+[uri]::EscapeDataString($PhysicalModel))
        $response=$client.GetAsync($url,[Net.Http.HttpCompletionOption]::ResponseHeadersRead,$deadline.Token).GetAwaiter().GetResult()
        try {
            if (-not $response.IsSuccessStatusCode) { return 'DMR_API_REJECTED' }
            $stream=$response.Content.ReadAsStream(); $buffer=[byte[]]::new(131073); $total=0
            while ($total -lt $buffer.Length) {
                $read=$stream.ReadAsync([Memory[byte]]::new($buffer,$total,$buffer.Length-$total),$deadline.Token).GetAwaiter().GetResult()
                if ($read -eq 0) { break }; $total+=$read
            }
            if ($total -gt 131072) { return 'DMR_REPLY_TOO_LARGE' }
            $metadata=ConvertFrom-Json -InputObject ([Text.Encoding]::UTF8.GetString($buffer,0,$total)) -AsHashtable -NoEnumerate
            return Test-CompanionDmrMetadata $metadata $PhysicalModel $Thinking
        } finally { $response.Dispose() }
    } catch { return 'DMR_API_UNAVAILABLE' }
    finally { $deadline.Dispose(); $client.Dispose(); $handler.Dispose() }
}
