function Get-ProjectPath {
  param(
    [System.IO.DirectoryInfo] $RepoRoot = (Join-Path $PSScriptRoot "../.."),
    [Parameter(Mandatory = $true)] [string] $ProjectName
  )
  $project = Get-ChildItem $RepoRoot -Filter "$ProjectName.csproj" -Exclude "artifacts/*" -File -Recurse
  if ($null -eq $project) {
    throw
  }
  return $project.FullName
}
