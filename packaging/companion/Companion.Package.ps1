#requires -Version 7.2
# Package/configuration rules only; credentials belong exclusively to the existing Companion.
Set-StrictMode -Version Latest

function Assert-CompanionPackage([string]$Root) {
    $manifestPath = Join-Path $Root 'package-manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'PACKAGE_MANIFEST_MISSING' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.schema -ne 1 -or $manifest.model -cne 'qwen3:8b' -or $manifest.provider -cne 'ollama' -or $manifest.packageVersion -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw 'PACKAGE_MANIFEST_INVALID' }
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
    foreach ($file in (Get-ChildItem -LiteralPath $Root -Recurse -File)) {
        $relative = [IO.Path]::GetRelativePath($Root,$file.FullName).Replace('\','/')
        if ($relative -notin @('package-manifest.json','companion.json') -and -not $seen.Contains($relative)) { throw 'PACKAGE_UNLISTED_FILE' }
    }
}

function Read-CompanionConfiguration([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf) -or (Get-Item -LiteralPath $Path).Length -gt 16384) { throw 'CONFIGURATION_MISSING_OR_TOO_LARGE' }
    $config = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
    $keys = @('version','server','slot','name','capability','python','dotnet','endpoint','maximumJobs')
    if ($config -isnot [Collections.IDictionary] -or $config.Count -ne $keys.Count) { throw 'CONFIGURATION_FIELDS_INVALID' }
    foreach ($key in $config.Keys) { if ($key -cnotin $keys) { throw 'CONFIGURATION_FIELDS_INVALID' } }
    if ($config.version -isnot [long] -and $config.version -isnot [int]) { throw 'CONFIGURATION_VERSION_INVALID' }
    if ($config.version -ne 1) { throw 'CONFIGURATION_VERSION_INVALID' }
    foreach ($key in @('server','slot','name','capability','python','dotnet','endpoint')) {
        if ($config[$key] -isnot [string]) { throw 'CONFIGURATION_FIELD_TYPE_INVALID' }
    }
    $server = $null
    if (-not [uri]::TryCreate($config.server, [UriKind]::Absolute, [ref]$server) -or $server.Scheme -ne 'https' -or
        $server.AbsolutePath -ne '/' -or $server.UserInfo -or $server.Query -or $server.Fragment) { throw 'SERVER_ORIGIN_INVALID' }
    if ($config.slot -cnotmatch '^[A-Za-z0-9-]{1,40}$' -or $config.name.Length -notin 1..80 -or
        $config.capability -cnotin @('nova','prompts')) { throw 'PROFILE_CONFIGURATION_INVALID' }
    if (($config.maximumJobs -isnot [int] -and $config.maximumJobs -isnot [long]) -or $config.maximumJobs -notin 1..50) { throw 'JOB_LIMIT_INVALID' }
    $endpoint = $null; $address = $null
    if (-not [uri]::TryCreate($config.endpoint, [UriKind]::Absolute, [ref]$endpoint) -or $endpoint.Scheme -ne 'http' -or
        $endpoint.AbsolutePath -ne '/' -or $endpoint.UserInfo -or $endpoint.Query -or $endpoint.Fragment -or
        -not [Net.IPAddress]::TryParse($endpoint.DnsSafeHost.Trim('[',']'), [ref]$address) -or
        -not [Net.IPAddress]::IsLoopback($address)) { throw 'OLLAMA_ENDPOINT_MUST_BE_LOOPBACK' }
    return $config
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
