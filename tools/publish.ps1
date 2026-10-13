# リリースビルド: 自己完結の単一 exe を publish/ に作る。ファイル名に日時を付ける。
# 使い方: powershell -NoProfile -File tools\publish.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$work = Join-Path $root "publish\work_$stamp"

if (Get-Process -Name Parent_Mail -ErrorAction SilentlyContinue) {
    throw 'Parent_Mail is running. Close it first.'
}

dotnet publish (Join-Path $root 'src\ParentMail.App\ParentMail.App.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none `
    -o $work -v q --nologo
if ($LASTEXITCODE -ne 0) { throw 'publish failed' }

$dest = Join-Path $root "publish\Parent_Mail_$stamp.exe"
Move-Item (Join-Path $work 'Parent_Mail.exe') $dest
Remove-Item $work -Recurse -Force
Write-Output $dest
