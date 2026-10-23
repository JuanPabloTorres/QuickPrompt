param([Parameter(Mandatory=$true)][string]$ExpectedFile,[Parameter(Mandatory=$true)][string]$ResultFile)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$owned=[IO.Path]::GetFullPath((Join-Path $repo '.playwright'))+[IO.Path]::DirectorySeparatorChar
foreach($path in @($ExpectedFile,$ResultFile)){
    if(-not [IO.Path]::GetFullPath($path).StartsWith($owned,[StringComparison]::OrdinalIgnoreCase)){throw 'Only owned synthetic QA paths are allowed'}
}
$fixture=Get-Content -LiteralPath $ExpectedFile -Raw | ConvertFrom-Json
if($fixture.syntheticOnly -ne $true -or $fixture.prefix -notmatch '^QP_NATIVE_[a-f0-9-]{36}$' -or
    -not $fixture.expected.StartsWith($fixture.prefix+"`n",[StringComparison]::Ordinal)){throw 'Explicit public fixture required'}
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class OwnedClipboardProbe {
 [DllImport("user32.dll",SetLastError=true)] public static extern bool OpenClipboard(IntPtr owner);
 [DllImport("user32.dll")] public static extern bool CloseClipboard();
 [DllImport("user32.dll")] public static extern IntPtr GetClipboardData(uint format);
 [DllImport("kernel32.dll")] public static extern IntPtr GlobalLock(IntPtr data);
 [DllImport("kernel32.dll")] public static extern bool GlobalUnlock(IntPtr data);
}
'@
$opened=$false
for($attempt=0;$attempt -lt 50;$attempt++){if([OwnedClipboardProbe]::OpenClipboard([IntPtr]::Zero)){$opened=$true;break};Start-Sleep -Milliseconds 20}
$result=@{scope='WIN32_CF_UNICODETEXT_RAW_EQUALITY';status='UNCONFIRMED';expected=$fixture.expected;observed=$null;code='CLIPBOARD_BUSY'}
if($opened){
    try{
        $handle=[OwnedClipboardProbe]::GetClipboardData(13)
        if($handle -ne [IntPtr]::Zero){
            $pointer=[OwnedClipboardProbe]::GlobalLock($handle)
            if($pointer -ne [IntPtr]::Zero){
                try{
                    $actual=[Runtime.InteropServices.Marshal]::PtrToStringUni($pointer)
                    $windows=$fixture.expected.Replace("`n","`r`n")
                    # Never persist arbitrary user clipboard data, even if another
                    # application changed it while the browser test was running.
                    if($actual -ceq $fixture.expected){$result.status='PASS';$result.code='EXACT';$result.observed=$fixture.expected}
                    elseif($actual -ceq $windows){$result.status='FAIL';$result.code='LF_CHANGED_TO_CRLF';$result.observed=$windows}
                    else{$result.code='UNEXPECTED_CONTENT_REDACTED'}
                }finally{[OwnedClipboardProbe]::GlobalUnlock($handle) | Out-Null}
            }
        }
    }finally{[OwnedClipboardProbe]::CloseClipboard() | Out-Null}
}
[IO.File]::WriteAllText($ResultFile,($result|ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
if($result.status -eq 'PASS'){exit 0};if($result.status -eq 'FAIL'){exit 1};exit 2
