<#
.SYNOPSIS
    Publishes the SAP driver server, the SAP GUI Inspector, and the SAP GUI simulator as self-contained, single-file
    executables.

.DESCRIPTION
    Each application is published with the .NET runtime embedded, so the target machine needs neither a .NET
    installation nor any SAP registration. The driver and the inspector find SAP Logon through the Windows running
    object table and talk to 32-bit or 64-bit SAP GUI alike. The driver keeps appsettings.json next to its executable
    so the port and budgets stay editable. The simulator is a 32-bit test double for SAP Logon, published for x86 like
    SAP GUI, so the 64-bit driver and inspector are tested across the bitness boundary.

.PARAMETER Configuration
    The build configuration. Defaults to Release.

.PARAMETER OutputPath
    The folder that receives one subfolder per application. Defaults to the repository's artifacts folder.

.EXAMPLE
    .\scripts\publish.ps1
    Publishes the applications to .\artifacts\sap-driver-server, .\artifacts\sap-inspector, and
    .\artifacts\sap-gui-simulator.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

# Resolve the defaults here: Windows PowerShell 5.1 leaves $PSScriptRoot empty inside param defaults.
$repositoryRoot = Split-Path -Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path -Path $repositoryRoot -ChildPath 'artifacts'
}

# Lists the applications to publish, the folder each one lands in, and its runtime.
$applications = @(
    @{ Folder = 'sap-driver-server'; Project = 'src\Sap.DriverServer\Sap.DriverServer.csproj'; Runtime = 'win-x64' }
    @{ Folder = 'sap-inspector'; Project = 'src\Sap.DriverServer.Inspector\Sap.DriverServer.Inspector.csproj'; Runtime = 'win-x64' }
    @{ Folder = 'sap-gui-simulator'; Project = 'src\Sap.DriverServer.Simulator\Sap.DriverServer.Simulator.csproj'; Runtime = 'win-x86' }
)

foreach ($application in $applications) {
    # Publish one self-contained single file; native libraries are bundled so the folder holds one executable.
    $projectPath = Join-Path -Path $repositoryRoot -ChildPath $application.Project
    $targetPath = Join-Path -Path $OutputPath -ChildPath $application.Folder
    Write-Host "Publishing $($application.Project) to $targetPath"

    dotnet publish $projectPath `
        --configuration $Configuration `
        --runtime $application.Runtime `
        --self-contained true `
        --output $targetPath `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $($application.Project) with exit code $LASTEXITCODE."
    }
}

Write-Host "Published to $(Resolve-Path -Path $OutputPath)"
