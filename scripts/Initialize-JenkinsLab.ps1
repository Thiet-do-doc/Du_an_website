$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$local = Join-Path $root '.artifacts/jenkins'
New-Item -ItemType Directory -Force -Path $local | Out-Null
function New-LabPassword {
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    return [Convert]::ToBase64String($bytes)
}
$adminFile = Join-Path $local 'admin-password.txt'
$envFile = Join-Path $local '.env'
if (-not (Test-Path -LiteralPath $adminFile)) {
    [IO.File]::WriteAllText($adminFile, (New-LabPassword))
}
if (-not (Test-Path -LiteralPath $envFile)) {
    [IO.File]::WriteAllText($envFile, "JENKINS_TEST_DB_PASSWORD=$(New-LabPassword)`n")
}
Write-Host 'Local credentials prepared (not committed).'
Write-Host 'Jenkins URL: http://localhost:8081 | Username: student'
Write-Host "Password file: $adminFile"
Write-Host 'Start: docker compose --env-file .artifacts/jenkins/.env -f compose.jenkins.yml up -d --build'
