$ErrorActionPreference = "Stop"

Write-Host "Cleaning up..."
$publishDir = ".\temp_publish"
$zipFile = ".\payload.zip"
$finalOut = ".\final_output_v16"

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (Test-Path $zipFile) { Remove-Item $zipFile -Force }
if (Test-Path $finalOut) { Remove-Item $finalOut -Recurse -Force }
New-Item -ItemType Directory $finalOut | Out-Null
New-Item -ItemType Directory $publishDir | Out-Null

Write-Host "Publishing Apps (Framework-dependent)..."
$tempApp = ".\temp_app"
$tempSvc = ".\temp_svc"
if (Test-Path $tempApp) { Remove-Item $tempApp -Recurse -Force }
if (Test-Path $tempSvc) { Remove-Item $tempSvc -Recurse -Force }

dotnet publish ShutdownGuard\ShutdownGuard.csproj -c Release -r win-x64 --self-contained false -o "$tempApp"
dotnet publish ShutdownGuardService\ShutdownGuardService.csproj -c Release -r win-x64 --self-contained false -o "$tempSvc"

Copy-Item -Path "$tempApp\*" -Destination "$publishDir\" -Recurse -Force
Copy-Item -Path "$tempSvc\*" -Destination "$publishDir\" -Recurse -Force

Write-Host "Zipping payload..."
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipFile -Force

Write-Host "Publishing Installer (Single file)..."
# The installer csproj needs to be updated to embed payload.zip instead of the old EXEs
$csproj = "ShutdownGuardInstaller\ShutdownGuardInstaller.csproj"
$xml = [xml](Get-Content $csproj)
# Remove old embedded resources
$xml.Project.ItemGroup | Where-Object { $_.EmbeddedResource } | ForEach-Object {
    [void]$xml.Project.RemoveChild($_)
}
# Add new ItemGroup with payload.zip
$itemGroup = $xml.CreateElement("ItemGroup")
$embed = $xml.CreateElement("EmbeddedResource")
$embed.SetAttribute("Include", "..\payload.zip")
$embed.SetAttribute("LogicalName", "payload.zip")
$itemGroup.AppendChild($embed)
$xml.Project.AppendChild($itemGroup)
$xml.Save((Convert-Path $csproj))

dotnet publish ShutdownGuardInstaller\ShutdownGuardInstaller.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "$finalOut"

Write-Host "Cleanup temp files..."
Remove-Item $publishDir -Recurse -Force
Remove-Item $zipFile -Force

Write-Host "Done! Final Installer:"
dir "$finalOut\ShutdownGuardInstaller.exe"
