#requires -Version 7.2
Set-StrictMode -Version Latest

function New-TransferPrivateFile([string]$Path, [string]$Repository) {
    $physical = Assert-PublicPostgresPrivatePath $Path $Repository
    if ($physical.StartsWith('\\') -or [IO.DriveInfo]::new([IO.Path]::GetPathRoot($physical)).DriveType -ne [IO.DriveType]::Fixed) { throw 'Use a local fixed drive.' }
    $stream=[IO.File]::Open($physical,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    $stream.Dispose()
    & (Join-Path $Repository 'scripts/Protect-QuickPromptSecret.ps1') -Path $physical
    return $physical
}

function New-TransferPrivateDirectory([string]$Path, [string]$Repository) {
    $physical=Assert-PublicPostgresPrivatePath $Path $Repository
    if ($physical.StartsWith('\\') -or [IO.Directory]::Exists($physical) -or [IO.File]::Exists($physical)) { throw 'Use a new local directory.' }
    $directory=[IO.Directory]::CreateDirectory($physical)
    $acl=[Security.AccessControl.DirectorySecurity]::new(); $acl.SetAccessRuleProtection($true,$false)
    foreach($sid in @([Security.Principal.WindowsIdentity]::GetCurrent().User,[Security.Principal.SecurityIdentifier]'S-1-5-18')) {
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
    }
    [IO.FileSystemAclExtensions]::SetAccessControl($directory,$acl)
    $verified=[IO.FileSystemAclExtensions]::GetAccessControl($directory,[Security.AccessControl.AccessControlSections]::Access)
    $allowed=@([Security.Principal.WindowsIdentity]::GetCurrent().User,[Security.Principal.SecurityIdentifier]'S-1-5-18')
    $rules=@($verified.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier]))
    if(-not $verified.AreAccessRulesProtected -or $rules.Count -ne 2 -or
       @($rules|Where-Object{$_.IdentityReference -notin $allowed -or $_.AccessControlType -ne 'Allow' -or $_.FileSystemRights -ne 'FullControl'}).Count){
        throw 'Private directory ACL verification failed.'
    }
    return $physical
}

function Export-TransferTrustAnchor([string]$HostName, [string]$Output, [string]$Repository) {
    $tcp=[Net.Sockets.TcpClient]::new(); $tls=$null; $chain=$null
    try {
        if (-not $tcp.ConnectAsync($HostName,5432).Wait(15000)) { throw 'TLS endpoint connection timed out.' }
        $tcp.ReceiveTimeout=15000; $tcp.SendTimeout=15000
        $network=$tcp.GetStream(); $network.Write([byte[]]@(0,0,0,8,4,210,22,47))
        if ($network.ReadByte() -ne 83) { throw 'Endpoint did not accept PostgreSQL TLS.' }
        $tls=[Net.Security.SslStream]::new($network,$false)
        $options=[Net.Security.SslClientAuthenticationOptions]::new()
        $options.TargetHost=$HostName
        $options.EnabledSslProtocols=[Security.Authentication.SslProtocols]::Tls12 -bor [Security.Authentication.SslProtocols]::Tls13
        if (-not $tls.AuthenticateAsClientAsync($options).Wait(15000)) { throw 'TLS certificate validation timed out.' }
        $certificate=[Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadCertificate($tls.RemoteCertificate.GetRawCertData())
        try {
            $chain=[Security.Cryptography.X509Certificates.X509Chain]::new()
            if (-not $chain.Build($certificate)) { throw 'Operating-system certificate chain validation failed.' }
            $root=$chain.ChainElements[$chain.ChainElements.Count-1].Certificate
            $path=New-TransferPrivateFile $Output $Repository
            [IO.File]::WriteAllText($path,$root.ExportCertificatePem(),[Text.UTF8Encoding]::new($false))
            return @{path=$path;sha256=(Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant();subject=$root.Subject;tls=$tls.SslProtocol.ToString()}
        } finally { $certificate.Dispose() }
    } finally { if($chain){$chain.Dispose()};if($tls){$tls.Dispose()};$tcp.Dispose() }
}

function Get-TransferNativeRuntime([string]$Directory) {
    if (-not [IO.Path]::IsPathFullyQualified($Directory) -or -not [IO.Directory]::Exists($Directory)) { throw 'Existing native PostgreSQL client directory required.' }
    $files=@([IO.Directory]::EnumerateFiles($Directory) | Where-Object { [IO.Path]::GetExtension($_) -in @('.exe','.dll') } | ForEach-Object { [IO.Path]::GetFullPath($_) } | Sort-Object)
    foreach($name in @('pg_dump.exe','pg_restore.exe')) {
        $path=Join-Path $Directory $name
        if (-not [IO.File]::Exists($path) -or [Diagnostics.FileVersionInfo]::GetVersionInfo($path).FileVersion -cne '18.6') { throw 'PostgreSQL 18.6 clients required.' }
    }
    return @{directory=[IO.Path]::GetFullPath($Directory);files=@($files|ForEach-Object{@{path=$_;sha256=(Get-FileHash -LiteralPath $_).Hash.ToLowerInvariant()}})}
}

function New-TransferNativeStart([string]$Executable, [string[]]$Arguments, [string]$PassFile, [string]$CaFile) {
    $start=[Diagnostics.ProcessStartInfo]::new($Executable)
    $start.UseShellExecute=$false; $start.CreateNoWindow=$true
    $start.RedirectStandardInput=$true; $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    foreach($key in @($start.Environment.Keys)) { if($key.StartsWith('PG',[StringComparison]::OrdinalIgnoreCase)){$null=$start.Environment.Remove($key)} }
    $start.Environment['PGPASSFILE']=$PassFile; $start.Environment['PGSSLMODE']='verify-full'; $start.Environment['PGSSLROOTCERT']=$CaFile
    $start.Environment['PGGSSENCMODE']='disable'; $start.Environment['PGSSLCERTMODE']='disable'; $start.Environment['PGCONNECT_TIMEOUT']='15'
    $start.Environment['PGCLIENTENCODING']='UTF8'
    $start.Environment['PGOPTIONS']='-c timezone=UTC -c datestyle=ISO,YMD -c extra_float_digits=1'
    foreach($argument in $Arguments){$start.ArgumentList.Add($argument)}
    return $start
}

function Get-TransferPassLine($Target) {
    $values=@($Target.Host,[string]$Target.Port,$Target.Database,$Target.Username,$Target.Password)
    foreach($value in $values){if([string]::IsNullOrWhiteSpace($value) -or $value -match '[\r\n\x00]'){throw 'Invalid pgpass field.'}}
    (($values|ForEach-Object{([string]$_).Replace('\','\\').Replace(':','\:')}) -join ':')+"`n"
}

function Protect-TransferArchive([string]$Plaintext, [string]$Ciphertext, [string]$Purpose, [string]$Repository) {
    if ([IO.FileInfo]::new($Plaintext).Length -gt 67108864) { throw 'Migration archive exceeds 64 MiB.' }
    $clear=[IO.File]::ReadAllBytes($Plaintext)
    try {
        $entropy=[Text.Encoding]::UTF8.GetBytes($Purpose)
        $encrypted=[Security.Cryptography.ProtectedData]::Protect($clear,$entropy,[Security.Cryptography.DataProtectionScope]::CurrentUser)
        $path=New-TransferPrivateFile $Ciphertext $Repository; [IO.File]::WriteAllBytes($path,$encrypted)
        $roundtrip=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($path),$entropy,[Security.Cryptography.DataProtectionScope]::CurrentUser)
        try { if (-not [Security.Cryptography.CryptographicOperations]::FixedTimeEquals([Security.Cryptography.SHA256]::HashData($clear),[Security.Cryptography.SHA256]::HashData($roundtrip))) { throw 'Encrypted archive roundtrip mismatch.' } }
        finally { [Array]::Clear($roundtrip) }
        return @{path=$path;sha256=(Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant();protection='Windows DPAPI CurrentUser; not portable disaster recovery'}
    } finally { [Array]::Clear($clear) }
}

function Invoke-TransferNative($Start, [string]$Stdout, [string]$Stderr, [string]$Repository, [long]$OutputLimit=67108864,
    [Collections.Generic.HashSet[string]]$OwnedFiles=$null) {
    if (-not ('QuickPrompt.Transfer.NativeStreams' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
namespace QuickPrompt.Transfer {
 public static class NativeStreams {
  static async Task Copy(Stream source, Stream target, long limit, CancellationToken token) {
   byte[] buffer=new byte[16384]; long count=0;
   while(true) { int n=await source.ReadAsync(buffer,token); if(n==0)return;
    if(n>limit-count)throw new InvalidDataException("Native stream limit exceeded.");
    await target.WriteAsync(buffer.AsMemory(0,n),token); count+=n; }
  }
  public static void Run(ProcessStartInfo start, Stream output, Stream errors, long limit) {
   using var process=new Process { StartInfo=start }; using var timeout=new CancellationTokenSource(180000); bool started=false;
   try {
    process.Start(); started=true; process.StandardInput.Close();
    var stdout=Copy(process.StandardOutput.BaseStream,output,limit,timeout.Token);
    var stderr=Copy(process.StandardError.BaseStream,errors,1048576,timeout.Token);
    var exit=process.WaitForExitAsync(timeout.Token);
    while(!stdout.IsCompleted||!stderr.IsCompleted||!exit.IsCompleted) {
     if(stdout.IsFaulted)stdout.GetAwaiter().GetResult(); if(stderr.IsFaulted)stderr.GetAwaiter().GetResult();
     timeout.Token.ThrowIfCancellationRequested(); Thread.Sleep(10);
    }
    Task.WhenAll(stdout,stderr,exit).GetAwaiter().GetResult();
    if(process.ExitCode!=0)throw new InvalidOperationException("Native PostgreSQL command failed; private diagnostics retained.");
   } finally {
    if(started&&!process.HasExited) {process.Kill(true);if(!process.WaitForExit(5000))throw new InvalidOperationException("Native child exit unconfirmed.");}
   }
  }
 }
}
'@
    }
    $outPath=New-TransferPrivateFile $Stdout $Repository
    if($null -ne $OwnedFiles){$null=$OwnedFiles.Add($outPath)}
    $errorPath=New-TransferPrivateFile $Stderr $Repository
    if($null -ne $OwnedFiles){$null=$OwnedFiles.Add($errorPath)}
    $output=[IO.File]::OpenWrite($outPath); $errors=[IO.File]::OpenWrite($errorPath)
    try { [QuickPrompt.Transfer.NativeStreams]::Run($Start,$output,$errors,$OutputLimit) }
    finally { $output.Dispose();$errors.Dispose() }
}

Export-ModuleMember -Function New-TransferPrivateFile, New-TransferPrivateDirectory, Export-TransferTrustAnchor, Get-TransferNativeRuntime, New-TransferNativeStart, Get-TransferPassLine, Protect-TransferArchive, Invoke-TransferNative
