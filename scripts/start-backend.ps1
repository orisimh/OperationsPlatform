$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet restore .\OperationsPlatform.sln
dotnet build .\OperationsPlatform.sln
dotnet run --project .\Backend\OperationsPlatform.Api\OperationsPlatform.Api.csproj
