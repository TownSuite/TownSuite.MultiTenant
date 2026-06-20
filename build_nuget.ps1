#!/bin/pwsh
$ErrorActionPreference = "Stop"
$CURRENTPATH = $pwd.Path

dotnet pack TownSuite.MultiTenant.sln --no-build -c=Release -p:Platform="Any CPU" --output "$CURRENTPATH/build"
