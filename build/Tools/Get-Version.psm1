function Get-Version {
  param(
    [System.IO.DirectoryInfo] $RepoRoot = (Join-Path $PSScriptRoot "../..")
  )
  $versionFile = Join-Path $RepoRoot.FullName "Version.props"
  if (Test-Path $versionFile) {
    $versionXml = [xml] (Get-Content $versionFile)
    $ns = new-object Xml.XmlNamespaceManager $versionXml.NameTable
    $ns.AddNamespace("msb", "http://schemas.microsoft.com/developer/msbuild/2003")
    return $versionXml.SelectSingleNode("//msb:Version", $ns).InnerText
  }
  else {
    return $null
  }
}
