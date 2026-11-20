param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference='Stop'
$file=(Resolve-Path -LiteralPath $Path).Path
$signTool=$env:QSL_SIGN_TOOL
if(!$signTool -or !$env:QSL_SIGN_TIMESTAMP -or $env:QSL_SIGN_THUMBPRINT -notmatch '^[0-9A-F]{40}$') {
    throw 'Signing settings are missing.'
}
$arguments=@('sign','/sha1',$env:QSL_SIGN_THUMBPRINT,'/s','My','/fd','SHA256','/tr',$env:QSL_SIGN_TIMESTAMP,'/td','SHA256')
if($env:QSL_SIGN_MACHINE -eq '1'){$arguments+='/sm'}
& $signTool @arguments $file
if($LASTEXITCODE){throw ('Authenticode signing failed for '+$file)}
& $signTool verify /pa /v /tw $file
if($LASTEXITCODE){throw ('Signature verification failed for '+$file)}
$signature=Get-AuthenticodeSignature -LiteralPath $file
if($signature.Status -ne 'Valid' -or !$signature.TimeStamperCertificate -or $signature.SignerCertificate.Thumbprint -ne $env:QSL_SIGN_THUMBPRINT) {
    throw ('The signature or timestamp is invalid for '+$file)
}
