param(
    [ValidateSet("Full", "ClipboardOnly", "FileJumpOnly")]
    [string]$Product = "Full",
    [switch]$Console,
    [switch]$Release,
    [switch]$Debug,
    [switch]$StopOnly,
    [switch]$BuildOnly,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

if ($Release -and $Debug) {
    throw "Use either -Release or -Debug, not both."
}

if ($StopOnly -and $BuildOnly) {
    throw "Use either -StopOnly or -BuildOnly, not both."
}

$ScriptPath = $MyInvocation.MyCommand.Definition
$ToolsDir = Split-Path -Parent $ScriptPath
$ProjectRoot = Split-Path -Parent $ToolsDir
$ProjectFile = Join-Path $ProjectRoot "ClipboardManager.csproj"
$Configuration = if ($Release) { "Release" } else { "Debug" }

function Resolve-AssemblyName {
    param(
        [string]$Flavor
    )

    switch ($Flavor) {
        "Full" { return "ClipboardX" }
        "ClipboardOnly" { return "ClipboardX-clipboard" }
        "FileJumpOnly" { return "ClipboardX-filejump" }
        default { throw "Unsupported product flavor: $Flavor" }
    }
}

function Resolve-TargetFramework {
    param(
        [string]$ProjectPath,
        [string]$BuildConfiguration,
        [string]$Flavor
    )

    $output = & dotnet msbuild $ProjectPath `
        -nologo `
        -getProperty:TargetFramework `
        "-p:Configuration=$BuildConfiguration" `
        "-p:ClipboardXProduct=$Flavor"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to resolve TargetFramework from project: $ProjectPath"
    }

    $values = @($output | ForEach-Object { "$_".Trim() } | Where-Object { $_ })
    if ($values.Count -ne 1) {
        throw "Unexpected TargetFramework result from project: $($values -join ' | ')"
    }

    return $values[0]
}

$AssemblyName = Resolve-AssemblyName -Flavor $Product

Set-Location $ProjectRoot

function Stop-LauncherHosts {
    if (-not (Get-Command Get-CimInstance -ErrorAction SilentlyContinue)) {
        return
    }

    Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object {
            ($_.Name -eq "powershell.exe" -or $_.Name -eq "pwsh.exe") -and
            $_.CommandLine -like "*$ScriptPath*" -and
            $_.ProcessId -ne $PID
        } |
        ForEach-Object {
            Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
        }
}

function Stop-ClipboardXProcesses {
    $names = @("ClipboardX", "ClipboardX-clipboard", "ClipboardX-filejump")
    foreach ($name in $names) {
        Get-Process $name -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    }
}

function Get-DotnetBuildArgs {
    $args = @(
        "build",
        $ProjectFile,
        "-c", $Configuration,
        "-p:ClipboardXProduct=$Product"
    )
    return $args
}

Stop-ClipboardXProcesses
Stop-LauncherHosts

if ($StopOnly) {
    exit 0
}

$TargetFramework = Resolve-TargetFramework `
    -ProjectPath $ProjectFile `
    -BuildConfiguration $Configuration `
    -Flavor $Product
$ExePath = Join-Path $ProjectRoot "bin\$Configuration\$TargetFramework\$AssemblyName.exe"

if (-not $NoBuild) {
    & dotnet @(Get-DotnetBuildArgs)
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if ($BuildOnly) {
    exit 0
}

if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "Built executable not found: $ExePath"
}

if ($Console) {
    Push-Location $ProjectRoot
    try {
        & $ExePath
        exit $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
} else {
    Start-Process -FilePath $ExePath -WorkingDirectory $ProjectRoot -WindowStyle Hidden
}
