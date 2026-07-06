param(
  [string]$Module,
  [switch]$AllModules,
  [int]$MaxAgeDays = 180
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Set-Location $repoRoot

if($AllModules -and -not [string]::IsNullOrWhiteSpace($Module)){
  throw 'Use either -Module <module> or -AllModules, not both.'
}

if($MaxAgeDays -lt 1){
  throw 'MaxAgeDays must be greater than zero.'
}

if($AllModules){
  $moduleDirsForValidation = Get-ChildItem -Path 'docs/modules' -Directory |
    Sort-Object Name

  foreach($dir in $moduleDirsForValidation){
    & $PSCommandPath -Module $dir.Name -MaxAgeDays $MaxAgeDays
  }

  Write-Output 'Documentation validation passed for all modules.'
  exit 0
}

$requiredFrontmatter = @('module','domain','content_type','status','updated_on','owner','audience')
$validContentTypes = @('Landing','Reference','Tutorial','How-to','Troubleshooting','Conceptual')
$requiredModuleFiles = @('README.md','product.md','api.md','architecture.md','workflows.md','testing.md')
$allowedCompactDirs = @('references')
$disallowedModuleDirs = @('prd','contracts','implementation','testing','_archive','_generated')
$requiredReadmeSections = @('## Read order','## Source of truth','## For agents','## Validation')
$errors = New-Object System.Collections.Generic.List[string]
$today = (Get-Date).Date

function Add-DocError([string]$message){
  $script:errors.Add($message)
}

function Get-RelativePath([string]$path){
  return (Resolve-Path -LiteralPath $path -Relative).TrimStart('.\').Replace('\','/')
}

function Parse-Frontmatter([string]$content){
  if($content -notmatch '^(?s)---\r?\n(.*?)\r?\n---'){
    return $null
  }

  $metadata = @{}
  $block = $Matches[1]
  foreach($line in ($block -split "\r?\n")){
    if($line -match '^([a-z_]+):\s*(.+?)\s*$'){
      $metadata[$Matches[1]] = $Matches[2]
    }
  }
  return $metadata
}

function Resolve-DocLink($file, $target){
  $cleanTarget = $target.Trim()
  $cleanTarget = $cleanTarget -replace '^<(.+)>$', '$1'
  $cleanTarget = $cleanTarget -replace '\s+["''][^"'']+["'']$', ''
  $targetNoAnchor = $cleanTarget.Split('#')[0]
  if([string]::IsNullOrWhiteSpace($targetNoAnchor)){ return $null }
  if($targetNoAnchor.StartsWith('/')){
    return Join-Path $repoRoot $targetNoAnchor.TrimStart('/')
  }
  return Join-Path -Path $file.DirectoryName -ChildPath $targetNoAnchor
}

function Convert-DocGlobToRegex([string]$pattern){
  $normalized = $pattern -replace '\\','/'
  $escaped = [regex]::Escape($normalized)
  $escaped = $escaped -replace '\\\*\\\*', '.*'
  $escaped = $escaped -replace '\\\*', '[^/]*'
  return '^' + $escaped + '$'
}

function Test-RepoPathReference($file, [string]$rawPath, [int]$lineNumber, [string[]]$repoFiles){
  $candidate = $rawPath.Trim().TrimEnd('.', ',', ';', ':')

  if($candidate -notmatch '^(src|test|e2e|scripts|docs)/'){
    return
  }
  if($candidate -match '^docs/modules/<module>'){
    return
  }
  if($candidate -match '[{}]'){
    return
  }
  if($candidate -match '[\s<>|]'){
    return
  }

  $relativeFile = Get-RelativePath $file.FullName
  if($candidate.Contains('*')){
    $regex = Convert-DocGlobToRegex $candidate
    foreach($repoFile in $repoFiles){
      if($repoFile -match $regex){
        return
      }
    }
    Add-DocError "BROKEN_SOURCE_PATH :: ${relativeFile}:${lineNumber} -> $candidate"
    return
  }

  $resolved = Join-Path $repoRoot ($candidate -replace '/', [System.IO.Path]::DirectorySeparatorChar)
  if(-not (Test-Path -Path $resolved)){
    Add-DocError "BROKEN_SOURCE_PATH :: ${relativeFile}:${lineNumber} -> $candidate"
  }
}

function Test-Frontmatter($file, [string]$content, [string]$moduleName){
  $relativeFile = Get-RelativePath $file.FullName
  $metadata = Parse-Frontmatter $content

  if($null -eq $metadata){
    Add-DocError "MISSING_FRONTMATTER :: $relativeFile"
    return
  }

  foreach($key in $requiredFrontmatter){
    if(-not $metadata.ContainsKey($key)){
      Add-DocError "MISSING_FRONTMATTER :: $relativeFile -> $key"
    }
  }

  if($metadata.ContainsKey('module') -and $metadata['module'] -ne $moduleName){
    Add-DocError "BAD_FRONTMATTER :: $relativeFile -> module must be $moduleName"
  }

  if($metadata.ContainsKey('content_type') -and $validContentTypes -notcontains $metadata['content_type']){
    Add-DocError "BAD_FRONTMATTER :: $relativeFile -> invalid content_type"
  }

  if($metadata.ContainsKey('updated_on')){
    $updatedOn = [datetime]::MinValue
    if(-not [datetime]::TryParseExact($metadata['updated_on'], 'yyyy-MM-dd', $null, [Globalization.DateTimeStyles]::None, [ref]$updatedOn)){
      Add-DocError "BAD_FRONTMATTER :: $relativeFile -> updated_on must use yyyy-MM-dd"
    }
    elseif(($today - $updatedOn.Date).Days -gt $MaxAgeDays){
      Add-DocError "STALE_DOC :: $relativeFile -> updated_on older than $MaxAgeDays days"
    }
    elseif(($updatedOn.Date - $today).Days -gt 1){
      Add-DocError "FUTURE_DOC_DATE :: $relativeFile -> updated_on is more than one day ahead"
    }
  }
}

function Test-ReadmeHandoff($file, [string]$content){
  $relativeFile = Get-RelativePath $file.FullName
  if($relativeFile -notmatch '^docs/modules/[^/]+/README\.md$'){
    return
  }
  if($content -notmatch '(?m)^TL;DR:'){
    Add-DocError "MISSING_README_HANDOFF :: $relativeFile -> TL;DR"
  }
  foreach($section in $requiredReadmeSections){
    if($content -notmatch "(?m)^$([regex]::Escape($section))\s*$"){
      Add-DocError "MISSING_README_HANDOFF :: $relativeFile -> $section"
    }
  }
}

function Test-ForbiddenText($file, [string]$content){
  $relativeFile = Get-RelativePath $file.FullName
  $forbiddenPatterns = @(
    @{ Pattern = 'docs[\\/]prd[\\/]hr'; Label = 'OLD_HR_PRD_PATH' },
    @{ Pattern = 'docs[\\/]implementation[\\/]hr'; Label = 'OLD_HR_IMPLEMENTATION_PATH' },
    @{ Pattern = 'README \(2\)'; Label = 'DUPLICATE_README_NAME' },
    @{ Pattern = '_archive'; Label = 'ARCHIVE_DOC_REFERENCE' },
    @{ Pattern = '_generated'; Label = 'GENERATED_DOC_REFERENCE' },
    @{ Pattern = 'docs[\\/]meta'; Label = 'META_DOC_REFERENCE' },
    @{ Pattern = 'api-and-permissions-map\.md'; Label = 'OLD_FRONTEND_HR_DOC' },
    @{ Pattern = 'testing-and-acceptance\.md'; Label = 'OLD_FRONTEND_TEST_DOC' },
    @{ Pattern = 'workflows-and-scenarios\.md'; Label = 'OLD_FRONTEND_WORKFLOW_DOC' },
    @{ Pattern = 'technical-spec\.md'; Label = 'OLD_FRONTEND_TECH_DOC' },
    @{ Pattern = 'finance-charges-api-integration-plan\.md'; Label = 'OLD_FINANCE_PLAN_DOC' },
    @{ Pattern = '\?\?'; Label = 'DOUBLE_QUESTION_MARK' }
  )
  foreach($item in $forbiddenPatterns){
    if($content -match $item.Pattern){
      Add-DocError "$($item.Label) :: $relativeFile"
    }
  }
  if($content.Contains([string][char]0x00E2) -or $content.Contains([string][char]0xFFFD)){
    Add-DocError "MOJIBAKE :: $relativeFile"
  }
}

function Test-Links($file, [string]$content){
  $relativeFile = Get-RelativePath $file.FullName
  $matches = [regex]::Matches($content, '!\[[^\]]*\]\(([^)]+)\)|\[[^\]]*\]\(([^)]+)\)')
  foreach($match in $matches){
    $target = if($match.Groups[1].Success){ $match.Groups[1].Value } else { $match.Groups[2].Value }
    $target = $target.Trim()
    if($target -match '^(https?:|mailto:|#)'){ continue }
    $resolved = Resolve-DocLink $file $target
    if($null -eq $resolved){ continue }
    if(Test-Path $resolved){ continue }
    if(Test-Path (Join-Path $resolved 'README.md')){ continue }
    Add-DocError "BROKEN_LINK :: $relativeFile -> $target"
  }
}

$moduleRoot = $null
$moduleName = $null
if(-not [string]::IsNullOrWhiteSpace($Module)){
  $moduleName = $Module.Trim().ToLowerInvariant()
  if($moduleName -notmatch '^[a-z0-9-]+$'){
    throw "Invalid module name: $Module"
  }
  $moduleRoot = Join-Path 'docs/modules' $moduleName
  if(-not (Test-Path $moduleRoot)){
    throw "Module docs folder not found: $moduleRoot"
  }
}

$legacyDirs = Get-ChildItem -Path 'docs' -Directory -Recurse -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -in @('_archive','_generated','meta') }
foreach($dir in $legacyDirs){
  Add-DocError "LEGACY_DOC_DIR :: $(Get-RelativePath $dir.FullName)"
}

if($null -eq $moduleRoot){
  $allowedRootDirs = @('architecture', 'common', 'modules')
  $rootDirs = Get-ChildItem 'docs' -Directory | Select-Object -ExpandProperty Name
  foreach($dir in $rootDirs){
    if($allowedRootDirs -notcontains $dir){
      Add-DocError "FORBIDDEN_ROOT_DIR :: docs/$dir"
    }
  }
}
else{
  foreach($fileName in $requiredModuleFiles){
    $path = Join-Path $moduleRoot $fileName
    if(-not (Test-Path $path)){
      Add-DocError "MISSING_MODULE_FILE :: $moduleRoot/$fileName"
    }
  }

  foreach($dirName in $disallowedModuleDirs){
    $path = Join-Path $moduleRoot $dirName
    if(Test-Path $path){
      Add-DocError "LEGACY_ACTIVE_DIR :: $moduleRoot/$dirName"
    }
  }

  $moduleDirs = Get-ChildItem -Path $moduleRoot -Directory | Select-Object -ExpandProperty Name
  foreach($dirName in $moduleDirs){
    if($allowedCompactDirs -notcontains $dirName){
      Add-DocError "UNEXPECTED_MODULE_DIR :: $moduleRoot/$dirName"
    }
  }
}

$docsRoot = if($null -eq $moduleRoot){ 'docs' } else { $moduleRoot }
$mdFiles = Get-ChildItem -Path $docsRoot -Recurse -File -Filter *.md
$repoFiles = @(rg --files | ForEach-Object { $_ -replace '\\','/' })

foreach($file in $mdFiles){
  $content = Get-Content -Path $file.FullName -Raw
  $relativeFile = Get-RelativePath $file.FullName
  $isModuleDoc = $relativeFile -match '^docs/modules/([^/]+)/'

  if($isModuleDoc){
    $docModuleName = $Matches[1]
    Test-Frontmatter $file $content $docModuleName
    Test-ReadmeHandoff $file $content
  }

  Test-ForbiddenText $file $content
  Test-Links $file $content

  $lineNumber = 0
  foreach($line in ($content -split "\r?\n")){
    $lineNumber++
    foreach($match in [regex]::Matches($line, '`([^`]+)`')){
      Test-RepoPathReference $file $match.Groups[1].Value $lineNumber $repoFiles
    }
  }
}

if($errors.Count -gt 0){
  $errors | Sort-Object -Unique | ForEach-Object { Write-Output $_ }
  Write-Error "Documentation validation failed with $($errors.Count) issue(s)."
}

if($null -eq $moduleRoot){
  Write-Output 'Documentation validation passed.'
}
else{
  Write-Output "Documentation validation passed for module '$moduleName'."
}
