param()

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Set-Location $repoRoot

$requiredFrontmatter = @('module','domain','status','source','updated_on','owner')
$requiredSections = @(
  '## Purpose',
  '## Endpoint(s)',
  '## Auth & permissions',
  '## Request headers',
  '## Path/query/body contract',
  '## Response contract (success + errors)',
  '## Business rules / invariants',
  '## Idempotency / concurrency notes (if relevant)',
  '## Examples (minimal, copy-ready)',
  '## Change log'
)

$errors = New-Object System.Collections.Generic.List[string]

$allowedRootDirs = @('architecture', 'common', 'meta', 'modules')
$rootDirs = Get-ChildItem 'docs' -Directory | Select-Object -ExpandProperty Name
foreach($dir in $rootDirs){
  if($allowedRootDirs -notcontains $dir){
    $errors.Add("FORBIDDEN_ROOT_DIR :: docs/$dir")
  }
}

# Broken links in active docs (exclude historical/meta and raw downloaded contracts)
$mdFiles = Get-ChildItem -Path 'docs' -Recurse -File -Filter *.md |
  Where-Object {
    $_.FullName -notmatch 'docs\\meta\\' -and
    $_.FullName -notmatch 'contracts\\raw\\'
  }

foreach($file in $mdFiles){
  $content = Get-Content -Path $file.FullName -Raw
  $matches = [regex]::Matches($content, '\[[^\]]*\]\(([^)]+)\)')
  foreach($m in $matches){
    $target = $m.Groups[1].Value.Trim()
    if($target -match '^(https?:|mailto:|#)'){ continue }
    $targetNoAnchor = $target.Split('#')[0]
    if([string]::IsNullOrWhiteSpace($targetNoAnchor)){ continue }
    $resolved = Join-Path -Path $file.DirectoryName -ChildPath $targetNoAnchor
    if(Test-Path $resolved){ continue }
    if(Test-Path (Join-Path $resolved 'README.md')){ continue }
    $errors.Add("BROKEN_LINK :: $($file.FullName) -> $target")
  }
}

$curatedDirs = Get-ChildItem -Path 'docs/modules' -Directory -Recurse |
  Where-Object { $_.FullName -match 'contracts\\curated$' }

foreach($dir in $curatedDirs){
  $curatedFiles = Get-ChildItem -Path $dir.FullName -File -Filter *.md
  foreach($f in $curatedFiles){
    if($f.Name -eq 'README.md'){ continue }

    if($f.Name -notmatch '^[a-z0-9]+(-[a-z0-9]+)*-api\.md$'){
      $errors.Add("BAD_NAME :: $($f.FullName)")
    }

    $text = Get-Content -Path $f.FullName -Raw

    foreach($k in $requiredFrontmatter){
      if($text -notmatch "(?m)^${k}:\s+"){
        $errors.Add("MISSING_FRONTMATTER :: $($f.FullName) -> $k")
      }
    }

    $lastPos = -1
    foreach($section in $requiredSections){
      $pos = $text.IndexOf($section)
      if($pos -lt 0){
        $errors.Add("MISSING_SECTION :: $($f.FullName) -> $section")
        continue
      }
      if($pos -lt $lastPos){
        $errors.Add("OUT_OF_ORDER_SECTION :: $($f.FullName) -> $section")
      }
      $lastPos = $pos
    }
  }
}

if($errors.Count -gt 0){
  $errors | ForEach-Object { Write-Output $_ }
  Write-Error "Documentation validation failed with $($errors.Count) issue(s)."
}

Write-Output 'Documentation validation passed.'
