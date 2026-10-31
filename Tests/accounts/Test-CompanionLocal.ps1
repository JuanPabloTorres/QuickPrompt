#requires -Version 7.2
param([ValidateRange(1024,65535)][int]$Port = 5301,
    [Parameter(Mandatory)][string]$CorePython,
    [Parameter(Mandatory)][string]$CoreSdkRoot,
    [ValidateSet('prompts','nova')][string]$Capability = 'prompts',
    [ValidateSet('A','B')][string]$FixtureActor = 'A',
    [switch]$SharedFromA,
    [switch]$AuthorizationOnly,
    [string]$EvidenceDirectory = '.playwright/saas-delivery-20261026/companion-real')
$ErrorActionPreference = 'Stop'
if ($SharedFromA -and ($FixtureActor -ne 'B' -or $Capability -ne 'prompts' -or $AuthorizationOnly)) { throw 'El caso compartido requiere B, perfil prompts e inferencia real.' }
$testRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runtime = Get-Content -LiteralPath (Join-Path $testRoot ".playwright/accounts-runtime/runtime-$Port.json") -Raw | ConvertFrom-Json
if ($runtime.database -ne 'quickprompt_development' -or $runtime.origin -ne "https://127.0.0.1:$Port") { throw 'La prueba sólo admite el runtime sintético HTTPS declarado.' }
$listener = Get-NetTCPConnection -LocalPort $Port -State Listen
$process = Get-CimInstance Win32_Process -Filter "ProcessId=$($runtime.processId)"
if ($listener.OwningProcess -ne $runtime.processId -or -not $process.CommandLine.Contains([string]$runtime.artifact)) { throw 'No se verificó el artefacto QA propio.' }
$evidence = [IO.Path]::GetFullPath((Join-Path $testRoot $EvidenceDirectory))
if (-not $evidence.StartsWith((Join-Path $testRoot '.playwright') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'La evidencia debe quedar dentro de .playwright.' }
[IO.Directory]::CreateDirectory($evidence) | Out-Null
$sdk = & (Join-Path $testRoot 'scripts/Resolve-NovaDotnet.ps1') -ForBuild
$dll = Join-Path $testRoot 'backend/QuickPrompt.Companion/bin/Release/net10.0-windows/QuickPrompt.Companion.dll'
$slot = 'qa-' + [Guid]::NewGuid().ToString('N')
$deviceName = 'QA componente local ' + $slot
$fixtures = Get-Content (Join-Path $testRoot '.playwright/saas-delivery-20261026/memory-ui-fixtures.json') -Raw | ConvertFrom-Json
$fixture = $fixtures.$FixtureActor
if ($fixture.database -ne 'quickprompt_development' -or $fixture.email -notlike 'qa-*@example.invalid') { throw 'Fixture ajeno al entorno sintético.' }
$handler = [Net.Http.HttpClientHandler]::new(); $handler.UseProxy = $false; $handler.AllowAutoRedirect = $false
$http = [Net.Http.HttpClient]::new($handler); $http.BaseAddress = [uri]$runtime.origin; $http.Timeout = [TimeSpan]::FromSeconds(15)
$phase = 'INITIALIZE'; $children = [Collections.Generic.List[Diagnostics.Process]]::new(); $nativePath = $null
function Read-Json([string]$Path, [Net.Http.HttpClient]$Client = $http) {
    $response = $Client.GetAsync($Path).GetAwaiter().GetResult()
    try { if (-not $response.IsSuccessStatusCode) { throw 'GET rechazado.' }; return ($response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json) }
    finally { $response.Dispose() }
}
function Write-Request([string]$Path, [hashtable]$Body, [switch]$Form, [Net.Http.HttpClient]$Client = $http, [int]$ExpectedStatus = 0) {
    $csrf = Read-Json '/account/csrf' $Client
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Post, $Path)
    $request.Headers.Add('X-QP-CSRF', [string]$csrf.token)
    if ($Form) {
        $fields = [Collections.Generic.Dictionary[string,string]]::new()
        foreach ($key in $Body.Keys) { $fields.Add([string]$key,[string]$Body[$key]) }
        $request.Content = [Net.Http.FormUrlEncodedContent]::new($fields)
    } else { $request.Content = [Net.Http.StringContent]::new(($Body | ConvertTo-Json -Depth 8 -Compress), [Text.Encoding]::UTF8, 'application/json') }
    $response = $Client.SendAsync($request).GetAwaiter().GetResult()
    $script:lastHttpStatus = [int]$response.StatusCode
    try {
        if ($ExpectedStatus) { if ([int]$response.StatusCode -ne $ExpectedStatus) { throw 'El estado del control negativo no coincide.' }; return }
        if (-not $response.IsSuccessStatusCode) { throw 'POST rechazado.' }
        if ($response.Content.Headers.ContentType.MediaType -eq 'application/json') { return ($response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json) }
    } finally { $response.Dispose(); $request.Dispose() }
}
function Assert-Hidden([Net.Http.HttpClient]$Client, [string]$Path) {
    $response=$Client.GetAsync($Path).GetAwaiter().GetResult()
    try { if ([int]$response.StatusCode -ne 404) { throw 'Se expuso un recurso fuera del permiso del receptor.' } }
    finally { $response.Dispose() }
}
function Import-FromSyntheticOwner([string]$Destination, [string]$Template) {
    if ($fixtures.A.database -ne 'quickprompt_development' -or $fixtures.A.email -notlike 'qa-*@example.invalid' -or $fixtures.A.email -eq $fixture.email) { throw 'Propietario A sintético no verificado.' }
    $ownerHandler=[Net.Http.HttpClientHandler]::new(); $ownerHandler.UseProxy=$false; $ownerHandler.AllowAutoRedirect=$false
    $owner=[Net.Http.HttpClient]::new($ownerHandler); $owner.BaseAddress=$http.BaseAddress; $owner.Timeout=$http.Timeout
    $publication=$null
    try {
        Write-Request '/account/login' @{email=[string]$fixtures.A.email;password='Synthetic-Account!79'} -Client $owner | Out-Null
        $sourceLibrary=Write-Request '/api/accounts/v1/libraries/' @{name='QA · Origen compartido ' + $slot} -Client $owner
        $sourcePath="/api/accounts/v1/libraries/$($sourceLibrary.id)/prompts"
        $source=Write-Request $sourcePath @{revision=0;title='Conceptualización QA';template=$Template} -Client $owner
        $publication=Write-Request '/api/accounts/v1/sharing/publications' @{libraryId=$sourceLibrary.id;promptId=$source.id;revision=1} -Client $owner
        $sharedPath="/api/accounts/v1/sharing/publications/$($publication.id)"
        Assert-Hidden $http "$sourcePath/$($source.id)"; Assert-Hidden $http $sharedPath
        Write-Request "$sharedPath/invite" @{email=[string]$fixture.email} -Client $owner | Out-Null
        $invitation=@(Read-Json '/api/accounts/v1/sharing/invitations' | Where-Object publicationId -EQ $publication.id)
        if ($invitation.Count -ne 1) { throw 'No se seleccionó una sola invitación propia.' }
        Write-Request "/api/accounts/v1/sharing/invitations/$($invitation[0].id)/decision" @{state=1} | Out-Null
        $snapshot=Read-Json $sharedPath
        if ($snapshot.template -cne $Template) { throw 'El snapshot no coincide con la versión publicada.' }
        $operation=[Guid]::NewGuid().ToString()
        $copy=Write-Request "$sharedPath/import" @{libraryId=$Destination;operationId=$operation}
        if ($copy.libraryId -ne $Destination -or $copy.id -eq $source.id -or $copy.template -cne $Template) { throw 'La importación no creó una copia exacta propia.' }
        Write-Request "$sharedPath/revoke" @{} -Client $owner | Out-Null
        Assert-Hidden $http $sharedPath
        Write-Request "$sharedPath/import" @{libraryId=$Destination;operationId=$operation} -ExpectedStatus 404 | Out-Null
        Assert-Hidden $http "$sourcePath/$($source.id)"
        $retained=Read-Json "/api/accounts/v1/libraries/$Destination/prompts/$($copy.id)"
        $unchanged=Read-Json "$sourcePath/$($source.id)" $owner
        if ($retained.template -cne $Template -or $unchanged.template -cne $Template -or $unchanged.revision -ne 1) { throw 'La revocación alteró la copia o el origen.' }
        @{scope='REAL_HTTPS_SHARED_SOURCE_A_TO_B';sourceLibraryId=$sourceLibrary.id;sourcePromptId=$source.id;destinationLibraryId=$Destination;copyId=$copy.id;publicationId=$publication.id;rejectedBeforeAcceptance=$true;acceptedSnapshotExact=$true;revokedReadsAndDuplicateImport=$true;sourceUnchanged=$true;ownCopyRetained=$true} | ConvertTo-Json | Set-Content (Join-Path $evidence 'sharing.json')
        return $copy
    } finally {
        if ($publication) { try { Write-Request "/api/accounts/v1/sharing/publications/$($publication.id)/revoke" @{} -Client $owner | Out-Null } catch { } }
        $owner.Dispose()
    }
}
function Start-Client([string]$Command) {
    $start = [Diagnostics.ProcessStartInfo]::new($sdk)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $start.WorkingDirectory = $testRoot
    $start.Environment['DOTNET_ROOT'] = Split-Path -Parent $sdk
    $start.StandardOutputEncoding = [Text.Encoding]::UTF8; $start.StandardErrorEncoding = [Text.Encoding]::UTF8
    foreach ($argument in @($dll,$Command,'--server',[string]$runtime.origin,'--slot',$slot,'--name',$deviceName,'--capability',$Capability)) { $start.ArgumentList.Add($argument) }
    if ($Command -eq 'run') {
        $maximumJobs = if ($Capability -eq 'nova') { '12' } else { '1' }
        foreach ($argument in @('--python',$CorePython,'--sdk-root',$CoreSdkRoot,'--worker',(Join-Path $testRoot 'agents/nova-core/nova_worker.py'),'--max-jobs',$maximumJobs)) { $start.ArgumentList.Add($argument) }
    }
    $started = [Diagnostics.Process]::Start($start); $children.Add($started); return $started
}
function Invoke-NovaCreation([string]$LibraryId, [string]$ConnectionId) {
    # HTTP fixture receipts deliberately simulate the browser. Model calls and the
    # Windows OAuth/DPAPI client remain real; this is never human/DOM acceptance.
    $message = 'Crea un prompt reutilizable para conceptualizar ideas de software web, apps, IA y Spatial Web Apps 3D. Debe describir el propósito detallado, estructura de carpetas y archivos, principios SOLID, patrón Factory y otros patrones apropiados según https://refactoring.guru/design-patterns, habilidades, agentes, reglas, librerías y tecnologías. Si incluye IA debe analizar patrones RAG y Agentic. El resultado debe estar en español, formato Markdown. Las variables de entrada son nombre del proyecto, propósito y tipo de sistema.'
    $libraryPath = "/api/accounts/v1/libraries/$LibraryId"
    $runId = [Guid]::NewGuid().ToString(); $script:nativePath = "$libraryPath/nova/live/$runId"
    $http.DefaultRequestHeaders.Add('X-Nova-Surface', [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N'))
    Write-Request "$libraryPath/nova/live" @{connectionId=$ConnectionId;request=@{runId=$runId;sessionId=[Guid]::NewGuid().ToString();message=$message;command=@{operation='CREATE_DRAFT';content=$message;inputNames=@('nombre_proyecto','proposito','tipo')}}} | Out-Null
    $runner = Start-Client 'run'; $stdout=$runner.StandardOutput.ReadToEndAsync(); $stderr=$runner.StandardError.ReadToEndAsync()
    $began=[DateTimeOffset]::UtcNow; $deadline=$began.AddMinutes(8)
    $state=@{mode='discover';candidates=@();promptId=$null;revision=$null;title=$null;templateText=$null;values=@{};variableNames=@();draftChanged=$false;compiledReady=$false;compiledText=$null;response=$null;responseComplete=$false;providerOpen=$false}
    $acknowledged=[Collections.Generic.HashSet[string]]::new(); $approved=$false
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $view=Read-Json $script:nativePath
        if ($view.run.status -in @('COMPLETED','FAILED','CANCELLED','BLOCKED') -and -not $view.run.browserOpen) { break }
        if ($view.run.status -in @('FAILED','CANCELLED','BLOCKED')) { throw 'NOVA terminó sin completar el caso.' }
        if ($view.question) { throw 'NOVA requiere aclaración: el caso no se considera completado.' }
        if ($runner.HasExited -and $view.run.status -notin @('COMPLETED','FAILED','CANCELLED','BLOCKED')) { throw 'El componente terminó antes de cerrar la tarea.' }
        if (-not $approved -and $view.run.status -eq 'AWAITING_APPROVAL' -and $view.proposal) {
            if ($view.run.effectCommitted) { throw 'Se detectó un efecto anterior a la aprobación del fixture.' }
            $review=$view.proposal
            # The oracle comes from the requested contract, not the model's plan.
            $inspection=Write-Request "$libraryPath/memory/editor/inspect" @{templateText=$review.draft.templateText;values=@{}}
            if ((@($inspection.variableNames | Sort-Object) -join '|') -ne 'nombre_proyecto|proposito|tipo') { throw 'La propuesta no contiene exactamente las tres variables solicitadas.' }
            foreach($requirement in @('https://refactoring.guru/design-patterns','SOLID','Factory','RAG','Agentic')) {
                if (-not $review.draft.templateText.Contains($requirement,[StringComparison]::OrdinalIgnoreCase)) { throw 'La propuesta omite un requisito explícito.' }
            }
            if (-not $review.contentReview -or $review.contentReview.status -ne 'REQUIRES_HUMAN_CONTENT_REVIEW' -or
                [string]::IsNullOrWhiteSpace($review.contentReview.contractHash)) {
                throw 'Falta la revisión de contenido ligada al contrato de autoría.'
            }
            # This approval belongs to the synthetic harness. Its receipt does
            # not establish human acceptance or semantic correctness.
            Write-Request "$($script:nativePath)/approve" @{
                approvalId=$review.id;hash=$review.hash;revision=$review.revision;confirmed=$true
                contentReviewed=$true;contentReviewHash=$review.contentReview.contractHash
                approvedLiteralChangeIds=@($review.contentReview.literalChanges.entries | ForEach-Object { $_.id })
            } | Out-Null
            $approved=$true
        }
        $poll=Read-Json "$($script:nativePath)/actions?visible=true"
        if ($poll.action -and $acknowledged.Add([string]$poll.action.actionId)) {
            $action=$poll.action
            switch ($action.tool) {
                {$_ -in @('start','ui_read_visible_state','set_working_context','ui_verify_prompt_state','prepare_review')} { break }
                'ui_open_create_prompt' { $state.mode='create'; $state.title=''; $state.templateText=''; break }
                'ui_prepare_creation' {
                    $inspection=Write-Request "$libraryPath/memory/editor/inspect" @{templateText=$action.arguments.templateText;values=@{}}
                    $state.title=$action.arguments.title; $state.templateText=$action.arguments.templateText; $state.variableNames=@($inspection.variableNames)
                    $state.values=@{}; foreach($name in $state.variableNames){$state.values[$name]=''}; $state.draftChanged=$true; break
                }
                'commit_approved' {
                    if (-not $approved -or -not $review) { throw 'Falta la aprobación ligada al artefacto.' }
                    $saved=Write-Request "$($script:nativePath)/commit" @{title=$review.draft.title;templateText=$review.draft.templateText;description=$review.draft.description;categoryId=$review.draft.categoryId}
                    $state.mode='focus';$state.promptId=$saved.id;$state.revision=$saved.revision;$state.title=$saved.title;$state.templateText=$saved.templateText;$state.variableNames=@($saved.variableNames);$state.draftChanged=$false; break
                }
                default { throw 'Herramienta fuera del contrato de creación de esta prueba.' }
            }
            Write-Request "$($script:nativePath)/actions/receipt" @{actionId=$action.actionId;sequence=$action.sequence;executionStatus='EXECUTED';verificationStatus='VERIFIED';observation=$state} | Out-Null
        }
        Start-Sleep -Milliseconds 250
    }
    if ($view.run.status -ne 'COMPLETED' -or $view.run.browserOpen -or -not $view.run.effectCommitted -or $view.run.approval -ne 'CONSUMED') { throw 'El motor no alcanzó el cierre aprobado en el plazo del caso.' }
    $stored=Read-Json "$libraryPath/prompts/$($view.run.targetId)"
    if ($stored.revision -ne 1 -or $stored.template -cne $review.draft.templateText) { throw 'La lectura posterior no coincide con el artefacto aprobado.' }
    [IO.File]::WriteAllText((Join-Path $evidence 'approved-template.md'), [string]$stored.template)
    # This connection belongs exclusively to this case. A closed run has already
    # consumed all its model replies; stop only our now-idle polling child.
    $stoppedIdle = -not $runner.HasExited
    if ($stoppedIdle) { $runner.Kill($true); $runner.WaitForExit() }
    $output=$stdout.GetAwaiter().GetResult(); $stderr.GetAwaiter().GetResult() | Out-Null
    $calls=@([regex]::Matches($output,'Ejecutando la instrucción en Qwen3:8b local')).Count
    if ($calls -lt 1 -or $calls -gt 12) { throw 'No se confirmó inferencia dentro del límite de etapas.' }
    $http.DefaultRequestHeaders.Remove('X-Nova-Surface') | Out-Null
    return @{
        scope='REAL_WINDOWS_COMPANION_NATIVE_ENGINE_SIMULATED_UI';status='passed'
        technicalAcceptance='PASSED';semanticAcceptance='UNCONFIRMED';humanAcceptance='NOT_TESTED';browserAcceptance='NOT_TESTED'
        jobStatus=$view.run.status;runId=$runId;libraryId=$LibraryId;promptId=$stored.id;revision=$stored.revision
        pairAndRefresh=$true;model='qwen3:8b';provider='Adaptive Core / PydanticAI / Ollama';calls=$calls
        approvedFixture=$approved;effectCommitted=$view.run.effectCommitted;approval=$view.run.approval
        stoppedOwnedIdleClient=$stoppedIdle;seconds=([DateTimeOffset]::UtcNow-$began).TotalSeconds
        pending=@('Semantic usefulness is not established by field and substring checks',
            'Real browser interaction and human acceptance not tested by HTTP fixture receipts')
    }
}
try {
    $phase = 'LOGIN'; Write-Request '/account/login' @{email=[string]$fixture.email;password='Synthetic-Account!79'} | Out-Null
    $phase = 'PAIR'; $pair = Start-Client 'pair'; $pairError = $pair.StandardError.ReadToEndAsync()
    $code = $null
    for ($lineIndex=0; $lineIndex -lt 6; $lineIndex++) {
        $lineRead = $pair.StandardOutput.ReadLineAsync()
        if (-not $lineRead.Wait(20000)) { throw 'No llegó el código de vinculación.' }
        $line = $lineRead.Result
        if ($null -eq $line) { break }
        if ($line.StartsWith('Código de este equipo: ')) { $code = $line.Substring('Código de este equipo: '.Length); break }
    }
    if (-not $code) { throw 'No se pudo comenzar la vinculación.' }
    Write-Request '/connect/verify' @{user_code=$code;action='review'} -Form | Out-Null
    Write-Request '/connect/verify' @{user_code=$code;action='approve'} -Form | Out-Null
    $code = $null; $line = $null
    if (-not $pair.WaitForExit(30000) -or $pair.ExitCode -ne 0) { throw 'No se completó el cliente OAuth.' }
    $pairError.GetAwaiter().GetResult() | Out-Null
    $phase = 'STATUS'; $status = Start-Client 'status'; $statusOutput = $status.StandardOutput.ReadToEndAsync(); $statusError=$status.StandardError.ReadToEndAsync()
    if (-not $status.WaitForExit(30000) -or $status.ExitCode -ne 0) { throw 'No se recuperó la credencial protegida.' }
    $statusOutput.GetAwaiter().GetResult() | Out-Null; $statusError.GetAwaiter().GetResult() | Out-Null
    $connection = @(Read-Json '/account/connections/' | Where-Object { $_.label -eq $deviceName -and $_.active })[0]
    if (-not $connection) { throw 'Falta la conexión QA autorizada.' }
    $receipt = @{scope='REAL_COMPANION_AUTHORIZATION';status='partial';jobStatus='NOT_RUN';pairAndRefresh=$true;capability=$Capability;modelInvoked=$false;pending=@('Inference and user outcome not tested by authorization-only mode')}
    if (-not $AuthorizationOnly) {
        $phase = 'SUBMIT'; $library = Write-Request '/api/accounts/v1/libraries/' @{name='QA · Companion ' + [DateTimeOffset]::UtcNow.ToString('HHmmss')}
        if ($Capability -eq 'nova') {
            $phase='NATIVE_NOVA'; $receipt=Invoke-NovaCreation ([string]$library.id) ([string]$connection.id)
            $receipt | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $evidence 'result.json')
        } else {
        $template = 'Conceptualiza el proyecto <nombre_proyecto>, de tipo <tipo>, cuyo propósito es <proposito>. Responde en español y Markdown con secciones Datos, Estructura de carpetas, SOLID, Factory, Tecnologías y Reglas. Conserva explícitamente los tres datos suministrados. Explica cómo se aplican SOLID y Factory. No inventes ejecuciones ni archivos creados.'
        $prompt = if ($SharedFromA) { Import-FromSyntheticOwner ([string]$library.id) $template } else { Write-Request "/api/accounts/v1/libraries/$($library.id)/prompts" @{revision=0;title='Conceptualización QA';template=$template} }
        $job = Write-Request "/api/accounts/v1/libraries/$($library.id)/prompts/$($prompt.id)/run-local" @{connectionId=[string]$connection.id;operationId=[Guid]::NewGuid().ToString();revision=1;values=@{nombre_proyecto='Faro QA';tipo='Web';proposito='Organizar sesiones de estudio para estudiantes'}}
        $phase = 'RUN'; $began=[DateTimeOffset]::UtcNow; $run = Start-Client 'run'; $runOutput = $run.StandardOutput.ReadToEndAsync(); $runError=$run.StandardError.ReadToEndAsync()
        if (-not $run.WaitForExit(210000)) { throw 'El componente superó el tiempo de la prueba.' }
        $runOutput.GetAwaiter().GetResult() | Out-Null; $runError.GetAwaiter().GetResult() | Out-Null
        $current = Read-Json "/api/accounts/v1/libraries/$($library.id)/executions/$($job.id)"
        $receipt = @{scope='REAL_COMPANION_OAUTH_DPAPI_CORE';status='partial';jobStatus=$current.status;errorCode=$current.errorCode;libraryId=$library.id;executionId=$job.id;clientExit=$run.ExitCode;seconds=([DateTimeOffset]::UtcNow-$began).TotalSeconds;pairAndRefresh=$true;model='qwen3:8b';provider='Adaptive Core / PydanticAI / Ollama';pending=@('Real user outcome not yet confirmed')}
        if ($current.status -eq 'Completed') {
            $phase='RESULT'; $result=Read-Json "/api/accounts/v1/libraries/$($library.id)/executions/$($job.id)/result"
            if ($result.text -notmatch 'Faro QA' -or $result.text -notmatch 'Organizar sesiones de estudio' -or $result.model -ne 'qwen3:8b' -or -not $result.response) { throw 'La respuesta no conserva el contrato de origen.' }
            [IO.File]::WriteAllText((Join-Path $evidence 'response.md'), [string]$result.response)
            $receipt.status='awaiting-semantic-review'; $receipt.pending=@('Inspect response.md against the requested software concept')
        }
        $receipt | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $evidence 'result.json')
        }
    }
    $phase='DISCONNECT'; $disconnect=Start-Client 'disconnect'; $disconnectOut=$disconnect.StandardOutput.ReadToEndAsync(); $disconnectErr=$disconnect.StandardError.ReadToEndAsync()
    if (-not $disconnect.WaitForExit(30000) -or $disconnect.ExitCode -ne 0) { throw 'No se confirmó la desconexión QA.' }
    $disconnectOut.GetAwaiter().GetResult() | Out-Null; $disconnectErr.GetAwaiter().GetResult() | Out-Null
    $receipt.disconnected=$true; $receipt | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $evidence 'result.json')
    Write-Output ('Companion real: '+$receipt.jobStatus+'; emparejamiento, refresh DPAPI y desconexión comprobados. No se imprimieron códigos, tokens ni respuestas.')
} catch {
    $protocolCode='unconfirmed'; $protocolReference='unconfirmed'
    if ($pairError -and $pairError.IsCompletedSuccessfully) {
        $safeError=$pairError.Result
        $match=[regex]::Match($safeError,'(?m)^Código de diagnóstico OAuth: ([a-z_]{1,40})\r?$')
        if($match.Success){$protocolCode=$match.Groups[1].Value}
        $match=[regex]::Match($safeError,'(?m)^Referencia del protocolo: (ID[0-9]{4})\r?$')
        if($match.Success){$protocolReference=$match.Groups[1].Value}
    }
    @{status='failed';phase=$phase;lastHttpStatus=$script:lastHttpStatus;scriptFrames=$_.ScriptStackTrace;protocolCode=$protocolCode;protocolReference=$protocolReference;scope='REAL_COMPANION_QA_ONLY';observedAt=[DateTimeOffset]::UtcNow.ToString('O')} | ConvertTo-Json | Set-Content (Join-Path $evidence 'failure.json')
    throw "La prueba del companion no se completó en fase $phase. Se conserva evidencia segura; no se imprimieron cuerpos ni credenciales."
} finally {
    if ($nativePath) { try {
        $view=Read-Json $nativePath
        @{run=$view.run;question=$view.question;proposal=$view.proposal.draft;scope='Synthetic HTTP fixture; no DOM observation'} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $evidence 'native-state.json')
        if ($view.run.browserOpen) { Write-Request "$nativePath/cancel" @{} | Out-Null }
    } catch { } }
    foreach($child in $children){ if(-not $child.HasExited){$child.Kill($true);$child.WaitForExit()};$child.Dispose() }
    try { foreach($own in @(Read-Json '/account/connections/' | Where-Object { $_.label -eq $deviceName -and -not $_.revoked })) { Write-Request "/account/connections/$($own.id)/revoke" @{} | Out-Null } } catch { }
    $http.Dispose()
}
