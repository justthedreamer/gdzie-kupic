<#
.SYNOPSIS
Runs a command in the .NET SDK container against a copy of the service sources.

.DESCRIPTION
Useful when the host blocks unsigned .NET assemblies (e.g. Windows Smart App Control), which breaks
`dotnet ef` and the integration tests. Sources are copied (without bin/obj) into the container, so
host build output is untouched. Paths listed in -SyncBack (relative to src/) are copied back to the
host afterwards, e.g. generated EF migrations.

.EXAMPLE
./scripts/dotnet-docker.ps1 "dotnet test Gdzie.Kupic.Tests.Integration"

.EXAMPLE
./scripts/dotnet-docker.ps1 "dotnet tool install -g dotnet-ef >/dev/null && export PATH=`$PATH:/root/.dotnet/tools && dotnet ef migrations add Name --project Gdzie.Kupic.Storage --startup-project Gdzie.Kupic.API" -SyncBack Gdzie.Kupic.Storage/Migrations
#>
param(
    [Parameter(Mandatory, Position = 0)][string]$Command,
    [string[]]$SyncBack = @()
)

$root = Split-Path $PSScriptRoot -Parent
$sync = ($SyncBack | ForEach-Object { "mkdir -p /out/$_ && cp -r /work/src/$_/. /out/$_/" }) -join ' && '
if (-not $sync) { $sync = 'true' }

$script = "set -e; mkdir /work; cd /src; tar --exclude=bin --exclude=obj -cf - . | tar -xf - -C /work; cd /work/src; dotnet restore GdzieKupicService.sln -v q >/dev/null; ($Command); $sync"

docker run --rm `
    -v "${root}:/src:ro" `
    -v "${root}/src:/out" `
    -v nuget-cache:/root/.nuget/packages `
    mcr.microsoft.com/dotnet/sdk:10.0 bash -c $script