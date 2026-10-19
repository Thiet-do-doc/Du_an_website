param([string]$RepoRoot = (Split-Path -Parent $PSScriptRoot))

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $RepoRoot).Path
$problems = [System.Collections.Generic.List[string]]::new()

# The executable projects live below backend/, frontend/ and tests/.
Get-ChildItem -LiteralPath $root -File | Where-Object {
    $_.Extension -in @('.cs', '.csproj', '.tsx', '.jsx')
} | ForEach-Object { $problems.Add("Unexpected root source/project: $($_.Name)") }
foreach ($name in @('Modules', 'Infrastructure', 'Controllers', 'Models', 'Data', 'Services', 'Contracts')) {
    if (Test-Path -LiteralPath (Join-Path $root $name)) {
        $problems.Add("Backend directory must be inside backend/DatingApp.Api: $name")
    }
}

$api = Join-Path $root 'backend/DatingApp.Api'
$snapshots = @(Get-ChildItem -LiteralPath $api -Recurse -Filter '*ModelSnapshot.cs' -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
$expected = [IO.Path]::GetFullPath((Join-Path $api 'Infrastructure/Migrations/AppDbContextModelSnapshot.cs'))
if ($snapshots.Count -ne 1 -or $snapshots[0].FullName -ne $expected) {
    $problems.Add('Keep exactly one AppDbContext snapshot in Infrastructure/Migrations; reconcile branch migrations before merging.')
}

if ($problems.Count -gt 0) {
    throw ("Repository layout check failed:`n- " + ($problems -join "`n- "))
}
Write-Output 'Repository layout OK: no duplicate root project; one canonical EF snapshot.'
