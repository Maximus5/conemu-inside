param([string]$build="")

# This file will update PortableApps and Chocolatey versions

$Script_File_path = split-path -parent $MyInvocation.MyCommand.Definition
$NuSpec = Join-Path $Script_File_path "..\ConEmuWinForms\Package.nuspec"
$Assembly = Join-Path $Script_File_path "..\ConEmuWinForms\AssemblyInfo.cs"

if ($build -eq "") {
  $build = $env:CurVerBuild
}

if (($build -eq $null) -Or (-Not ($build -match "\b\d\d\d\d\d\d(\w*)\b"))) {
  Write-Host -ForegroundColor Red "Build was not passed as argument!"
  $host.SetShouldExit(101)
  exit 
}

$build_dot4 = "1.$($build.SubString(0,2)).$($build.SubString(2,2).TrimStart('0')).$($build.SubString(4,2).TrimStart('0'))"

Write-Host -ForegroundColor Yellow "Updating files with build# $build"


#
# Helper to update NuSpec-s
#
function NuSpec-SetBuild([string]$XmlFile) {
  Write-Host -ForegroundColor Green $XmlFile
  $xml = Get-Content -Raw $XmlFile -Encoding UTF8
  $m = ([regex]'<version>\d+\.\d+\.\d+\.\d+<\/version>').Matches($xml)
  if (($m -eq $null) -Or ($m.Count -ne 1)) {
    Write-Host -ForegroundColor Red "Proper <version>...</version> tag was not found in:`r`n$XmlFile"
    $host.SetShouldExit(101)
    return $FALSE
  }
  $xml = $xml.Replace($m[0].Value, "<version>$build_dot4</version>").Trim()
  Set-Content $XmlFile $xml -Encoding UTF8
  return $TRUE
}

#
# Helper to update Assemblies
#
function Assembly-SetBuild([string]$AsmFile) {
  Write-Host -ForegroundColor Green $AsmFile
  $cs = Get-Content -Raw $AsmFile -Encoding UTF8
  $m = ([regex]'Version\("\d+\.\d+\.\d+\.\d+"\)').Matches($cs)
  if (($m -eq $null) -Or ($m.Count -ne 2)) {
    Write-Host -ForegroundColor Red "Proper ...Version(...) tag was not found in:`r`n$AsmFile"
    $host.SetShouldExit(101)
    return $FALSE
  }
  $cs = $cs.Replace($m[0].Value, "Version(`"$build_dot4`")").Trim()
  Set-Content $AsmFile $cs -Encoding UTF8
  return $TRUE
}



#
# Nuget.org : ConEmuWinForms\Package.nuspec
#
if (-Not (NuSpec-SetBuild $NuSpec)) {
  exit 
}

#
# Nuget.org : ConEmuWinForms\AssemblyInfo.cs
#
if (-Not (Assembly-SetBuild $Assembly)) {
  exit 
}
