param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [string] $Output = "$PSScriptRoot\..\artifacts\publish\WindowsDictation"
)

$ErrorActionPreference = "Stop"

dotnet publish "$PSScriptRoot\..\src\WindowsDictation.App\WindowsDictation.App.csproj" `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -o $Output

Write-Host "Published Windows Dictation to $Output"
