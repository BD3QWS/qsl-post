param(
    [string]$Payload=(Join-Path $PSScriptRoot '..\build\release'),
    [string]$Output,
    [string]$Makensis='E:\Codex\2026-11-12\ui-win-11-win-11-windows\work\installer\nsis-3.13\makensis.exe',
    [string]$CertificateThumbprint,
    [string]$SignTool,
    [string]$TimestampServer='http://timestamp.digicert.com'
)
$ErrorActionPreference='Stop'
$payloadPath=(Resolve-Path -LiteralPath $Payload).Path
if(!$Output) {
    $Output=Join-Path $PSScriptRoot '..\outputs\1.4.3\QSLPost-Setup-1.4.3.exe'
}
$outputPath=[IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path (Split-Path -Parent $outputPath) -Force | Out-Null
$iconPath=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\src\QslPost.ico')).Path
$project=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$stagedExe=$outputPath
$environment=@{}
try {
    if($CertificateThumbprint) {
        $fingerprint=($CertificateThumbprint -replace '[\s\u200e\u200f]','').ToUpperInvariant()
        if($fingerprint -notmatch '^[0-9A-F]{40}$'){throw 'Enter the code-signing certificate thumbprint.'}
        $cert=Get-Item -LiteralPath ('Cert:\CurrentUser\My\'+$fingerprint) -ErrorAction SilentlyContinue
        $machine=$false
        if(!$cert){$cert=Get-Item -LiteralPath ('Cert:\LocalMachine\My\'+$fingerprint) -ErrorAction SilentlyContinue;$machine=$true}
        if(!$cert -or !$cert.HasPrivateKey -or $cert.Subject -eq $cert.Issuer -or
              $cert.EnhancedKeyUsageList.ObjectId -notcontains '1.3.6.1.5.5.7.3.3'){
            throw 'A CA-issued code-signing certificate with an accessible private key is required.'
        }
        $chain=[Security.Cryptography.X509Certificates.X509Chain]::new()
        try {
            [void]$chain.ChainPolicy.ApplicationPolicy.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3'))
            if(!$chain.Build($cert)){throw 'Windows could not validate the selected certificate chain.'}
        } finally {$chain.Dispose()}
        if(!$SignTool) {
            $command=Get-Command signtool.exe -ErrorAction SilentlyContinue
            if($command){$SignTool=$command.Source}
            else {
                $sdk=Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
                $SignTool=Get-ChildItem -LiteralPath $sdk -Filter 'signtool.exe' -Recurse -ErrorAction SilentlyContinue |
                    Where-Object {$_.Directory.Name -eq 'x64'} | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
            }
        }
        if(!$SignTool){throw 'Windows SDK SignTool is required for Authenticode signing.'}
        $signScript=Join-Path $PSScriptRoot 'SignFile.ps1'
        $signEnv=@{QSL_SIGN_TOOL=(Resolve-Path -LiteralPath $SignTool).Path;QSL_SIGN_THUMBPRINT=$fingerprint;
                   QSL_SIGN_MACHINE=$(if($machine){'1'}else{'0'});QSL_SIGN_TIMESTAMP=$TimestampServer;QSL_SIGN_SCRIPT=$signScript}
        foreach($key in $signEnv.Keys){
            $environment[$key]=[Environment]::GetEnvironmentVariable($key,'Process')
            [Environment]::SetEnvironmentVariable($key,$signEnv[$key],'Process')
        }
        $signedPayload=Join-Path $project ('work\signed-package-'+[guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $signedPayload | Out-Null
        Copy-Item -LiteralPath (Join-Path $payloadPath 'QslPost.exe'),(Join-Path $payloadPath 'QslPost.exe.config'),(Join-Path $payloadPath 'QslPost.DataCleanup.exe'),
            (Join-Path $payloadPath '使用说明.txt'),(Join-Path $payloadPath 'tracking') -Destination $signedPayload -Recurse
        $payloadPath=$signedPayload
        & $signScript -Path (Join-Path $payloadPath 'QslPost.exe')
        & $signScript -Path (Join-Path $payloadPath 'QslPost.DataCleanup.exe')
        & $signScript -Path (Join-Path $payloadPath 'tracking\QslPost.TrackingView.dll')
        $stagedExe=Join-Path $signedPayload 'setup.exe'
    }
    $defines=@("/DPAYLOAD=$payloadPath","/DOUTPUT=$stagedExe","/DICON=$iconPath")
    if($CertificateThumbprint){$defines+='/DSIGNED_BUILD'}
    & $Makensis /V3 /WX /INPUTCHARSET UTF8 @defines (Join-Path $PSScriptRoot 'QslPost.nsi')
    if($LASTEXITCODE -ne 0){throw 'Installer build failed.'}
    if($CertificateThumbprint) {
        & $signScript -Path $stagedExe
        Move-Item -LiteralPath $stagedExe -Destination $outputPath -Force
    }
    Get-Item -LiteralPath $outputPath | Select-Object FullName,Length
} finally {
    foreach($key in $environment.Keys) {[Environment]::SetEnvironmentVariable($key,$environment[$key],'Process')}
}
