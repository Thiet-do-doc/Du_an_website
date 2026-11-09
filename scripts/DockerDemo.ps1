param([ValidateSet('start', 'status', 'stop', 'mail')][string]$Action = 'start')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $demoDir = Join-Path $root '.artifacts/docker-demo'
    $mailDir = Join-Path $demoDir 'mail'
    if ($Action -eq 'mail') { & "$PSScriptRoot/Read-DevMail.ps1" -MailDirectory $mailDir; return }
    $envFile = Join-Path $root '.env.demo'
    if (!(Test-Path -LiteralPath $envFile)) {
        if ($Action -ne 'start') { throw 'Run the start command first.' }
        function New-DemoSecret {
            $bytes = New-Object byte[] 32
            $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
            try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
            return [BitConverter]::ToString($bytes).Replace('-', '')
        }
        @('POSTGRES_DB=mylove_demo', 'POSTGRES_USER=mylove_demo', ('POSTGRES_PASSWORD=' + (New-DemoSecret)),
          ('JWT_KEY=' + (New-DemoSecret)), 'STAGING_PORT=8080', 'AUTH_FRONTEND_URL=http://127.0.0.1:8080',
          'EMAIL_FROM=noreply@example.test', 'SMTP_HOST=unused.invalid', 'SMTP_PORT=587',
          'SMTP_USERNAME=unused', 'SMTP_PASSWORD=unused') | Set-Content -LiteralPath $envFile -Encoding ascii
    }
    $docker = (Get-Command docker.exe -ErrorAction SilentlyContinue).Source
    if (!$docker) { $docker = Join-Path $env:ProgramFiles 'Docker/Docker/resources/bin/docker.exe' }
    $compose = @('compose', '-p', 'mylove-demo', '--env-file', $envFile, '-f', 'compose.staging.yml', '-f', 'compose.demo.yml')
    function Invoke-DemoCompose {
        & $docker @compose @args
        if ($LASTEXITCODE -ne 0) { throw ('Docker Compose failed: ' + $args[0]) }
    }
    if ($Action -eq 'status') { Invoke-DemoCompose ps -a; return }
    if ($Action -eq 'stop') { Invoke-DemoCompose stop; return }

    $null = New-Item -ItemType Directory -Force -Path $mailDir
    Invoke-DemoCompose config --quiet
    $schema = Join-Path $demoDir 'schema.sql'
    $previousConnection = $env:ConnectionStrings__DefaultConnection
    try {
        # EF only generates SQL. No host database is contacted or updated.
        $env:ConnectionStrings__DefaultConnection = 'Host=localhost;Database=design_only;Username=design_only'
        & dotnet ef migrations script --idempotent --project backend/DatingApp.Api --configuration Release --output $schema
        if ($LASTEXITCODE -ne 0) { throw 'Could not generate migrations. Check dotnet tool restore and the Release build.' }
    } finally { $env:ConnectionStrings__DefaultConnection = $previousConnection }
    Invoke-DemoCompose up -d --wait postgres
    Invoke-DemoCompose cp $schema 'postgres:/tmp/mylove-demo-schema.sql'
    Invoke-DemoCompose exec -T postgres sh -c 'exec psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f /tmp/mylove-demo-schema.sql'
    Invoke-DemoCompose up -d --no-build --wait
    Invoke-DemoCompose ps
} finally { Pop-Location }
