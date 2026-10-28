#requires -Version 7.2
param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference = 'Stop'
$secretFile = [IO.FileInfo]::new([IO.Path]::GetFullPath($Path))
if (-not $secretFile.Exists) { throw 'No existe el archivo que se debe proteger.' }
if ($IsWindows) {
    # Access-only persistence avoids owner/SACL writes and SeSecurityPrivilege.
    $secretAcl = [IO.FileSystemAclExtensions]::GetAccessControl($secretFile, [Security.AccessControl.AccessControlSections]::Access)
    $secretAcl.SetAccessRuleProtection($true, $false)
    foreach ($oldRule in @($secretAcl.GetAccessRules($true, $false, [Security.Principal.SecurityIdentifier]))) {
        $secretAcl.RemoveAccessRuleSpecific($oldRule)
    }
    $allowedSids = @([Security.Principal.WindowsIdentity]::GetCurrent().User, [Security.Principal.SecurityIdentifier]'S-1-5-18')
    foreach ($allowedSid in $allowedSids) {
        $secretAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($allowedSid, 'FullControl', 'Allow'))
    }
    [IO.FileSystemAclExtensions]::SetAccessControl($secretFile, $secretAcl)
    $verifiedAcl = [IO.FileSystemAclExtensions]::GetAccessControl($secretFile, [Security.AccessControl.AccessControlSections]::Access)
    $verifiedRules = @($verifiedAcl.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier]))
    if (-not $verifiedAcl.AreAccessRulesProtected -or $verifiedRules.Count -ne 2 -or
        @($verifiedRules | Where-Object { $_.IdentityReference -notin $allowedSids -or $_.AccessControlType -ne 'Allow' }).Count -gt 0) {
        throw 'No se verificó la ACL privada del archivo.'
    }
} else {
    [IO.File]::SetUnixFileMode($secretFile.FullName, [IO.UnixFileMode]'UserRead,UserWrite')
    if ([IO.File]::GetUnixFileMode($secretFile.FullName) -ne [IO.UnixFileMode]'UserRead,UserWrite') {
        throw 'No se verificaron los permisos privados del archivo.'
    }
}
