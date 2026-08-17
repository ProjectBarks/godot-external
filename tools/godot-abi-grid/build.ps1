<#
.SYNOPSIS
    Export the godot-abi-grid ground-truth project across the compat matrix.

.DESCRIPTION
    docs/analysis.md 8.9: "Godot ships official export templates for every
    version. A minimal test project ... exported across the axes gives ground
    truth we control completely."

    Axes:
        version    4.2 4.3 4.4 4.5 4.6
        template   release / debug     (the 4.6 variant flag IS this)
        precision  single / double     (real_t width changes every float offset)
        binding    gdscript / dotnet   (only the latter has the managed bridge)

    Output: out/<version>-<template>-<precision>-<binding>/grid.exe

    This script is expected to run on machines where most cells cannot be
    built. It therefore NEVER fails the whole run because one cell is missing:
    it detects what is installed, skips the rest with a reason, and prints
    exactly what to install. out/availability.json records all of that so
    REPORT.md can print honest "not built" rows instead of blanks.

.PARAMETER ListOnly
    Detect and report; build nothing.

.PARAMETER Versions
    Minor versions to attempt. Default: 4.2 4.3 4.4 4.5 4.6

.PARAMETER Only
    Substring filter on the cell name, e.g. -Only '4.5'.

.PARAMETER GodotBinDir
    Directory searched (recursively) for Godot editor executables.
    Default: $env:GODOT_BIN_DIR, else <harness>/bin.

.PARAMETER TemplatesDir
    Export-template root. Default: $env:APPDATA/Godot/export_templates.

.PARAMETER DoubleTemplateDir
    Root holding CUSTOM double-precision templates, laid out as
    <dir>/<version>/windows_{release,debug}_x86_64[.mono].exe
    There are no official double-precision templates; see README.md.

.EXAMPLE
    pwsh ./build.ps1 -ListOnly
    pwsh ./build.ps1 -Only 4.5
#>
[CmdletBinding()]
param(
    [switch] $ListOnly,
    [string[]] $Versions = @('4.2', '4.3', '4.4', '4.5', '4.6'),
    [ValidateSet('release', 'debug')] [string[]] $Templates = @('release', 'debug'),
    [ValidateSet('single', 'double')] [string[]] $Precisions = @('single', 'double'),
    [ValidateSet('gdscript', 'dotnet')] [string[]] $Bindings = @('gdscript', 'dotnet'),
    [string] $Only,
    [string] $GodotBinDir,
    [string] $TemplatesDir,
    [string] $DoubleTemplateDir,
    [string] $OutDir,
    [int] $ExportTimeoutSec = 900,
    [switch] $KeepStaging
)

$ErrorActionPreference = 'Stop'
$HarnessRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir  = Join-Path $HarnessRoot 'project'
$StagingRoot = Join-Path $HarnessRoot 'build-staging'

if (-not $OutDir)      { $OutDir = Join-Path $HarnessRoot 'out' }
if (-not $GodotBinDir) { if ($env:GODOT_BIN_DIR) { $GodotBinDir = $env:GODOT_BIN_DIR } else { $GodotBinDir = Join-Path $HarnessRoot 'bin' } }
if (-not $TemplatesDir) { $TemplatesDir = Join-Path $env:APPDATA 'Godot\export_templates' }
if (-not $DoubleTemplateDir -and $env:GODOT_DOUBLE_TEMPLATES) { $DoubleTemplateDir = $env:GODOT_DOUBLE_TEMPLATES }

# Godot.NET.Sdk and TargetFramework must track the engine version, or the
# export's `dotnet publish` step fails in a way that looks like an export bug.
#
# The TFM also decides which CLR gets bundled into data_<assembly>_windows_x86_64,
# and that is what the bridge.managed check reads. A net8.0 export ships a
# self-contained .NET 8 runtime whose coreclr.dll exports no readable
# DotNetRuntimeContractDescriptor, so the calibrator cannot attach to the managed
# side at all and the .NET half of 4.6's chain goes unmeasured on every cell.
# net9.0 is therefore the floor wherever the engine's SDK accepts it; a version
# that rejects it falls back to $VersionFallbackTfm and the cell says so.
$VersionMatrix = @{
    '4.2' = @{ Sdk = '4.2.2'; Tfm = 'net6.0' }
    '4.3' = @{ Sdk = '4.3.0'; Tfm = 'net9.0' }
    '4.4' = @{ Sdk = '4.4.1'; Tfm = 'net9.0' }
    '4.5' = @{ Sdk = '4.5.0'; Tfm = 'net9.0' }
    '4.6' = @{ Sdk = '4.6.0'; Tfm = 'net9.0' }
}

# --------------------------------------------------------------------------
# UTF-8 without BOM, always.
#
# Main.tscn carries "hello (non-ASCII) / Japanese / an astral codepoint" on
# purpose (analysis.md 4.6: scry truncates char32_t to a byte, silently).
# Get-Content/Set-Content in Windows PowerShell 5.1 default to the ANSI
# codepage and would quietly destroy exactly the characters the harness exists
# to test - producing a "calibrator is lossy" verdict caused by the BUILD
# SCRIPT. Every read/write below goes through these two helpers.
# --------------------------------------------------------------------------
$script:Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Read-TextFile([string] $Path) {
    return [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
}

function Write-TextFile([string] $Path, [string] $Content) {
    [System.IO.File]::WriteAllText($Path, $Content, $script:Utf8NoBom)
}

function Write-Head([string] $Text) { Write-Host "`n=== $Text ===" -ForegroundColor Cyan }
function Write-Ok([string] $Text)   { Write-Host "  [ok]   $Text" -ForegroundColor Green }
function Write-Skip([string] $Text) { Write-Host "  [skip] $Text" -ForegroundColor Yellow }
function Write-Bad([string] $Text)  { Write-Host "  [fail] $Text" -ForegroundColor Red }

# --------------------------------------------------------------------------
# Discovery
# --------------------------------------------------------------------------

function Get-GodotEditors {
    $found = @()

    # 1. godot-bin.json, if the user pinned exact paths.
    $mapPath = Join-Path $HarnessRoot 'godot-bin.json'
    if (Test-Path $mapPath) {
        $map = Read-TextFile $mapPath | ConvertFrom-Json
        foreach ($prop in $map.PSObject.Properties) {
            foreach ($bindingProp in $prop.Value.PSObject.Properties) {
                if (Test-Path $bindingProp.Value) {
                    $found += [pscustomobject]@{
                        Version = $prop.Name
                        Binding = $bindingProp.Name
                        Path    = (Resolve-Path $bindingProp.Value).Path
                        Source  = 'godot-bin.json'
                    }
                }
            }
        }
    }

    # 2. Recursive scan of the bin directory for official archive layouts.
    if (Test-Path $GodotBinDir) {
        $candidates = Get-ChildItem -Path $GodotBinDir -Recurse -File -Filter 'Godot_v*.exe' -ErrorAction SilentlyContinue
        foreach ($file in $candidates) {
            if ($file.Name -like '*console*') { continue }
            $m = [regex]::Match($file.Name, '^Godot_v(?<ver>\d+\.\d+(\.\d+)?)-(?<rel>[a-z0-9]+)(?<mono>_mono)?_win64\.exe$')
            if (-not $m.Success) { continue }
            $found += [pscustomobject]@{
                Version = $m.Groups['ver'].Value
                Binding = $(if ($m.Groups['mono'].Success) { 'dotnet' } else { 'gdscript' })
                Path    = $file.FullName
                Source  = 'scan'
            }
        }
    }

    return $found
}

function Resolve-Editor([string] $MinorVersion, [string] $Binding, $Editors) {
    # A .NET editor can export a GDScript-only project; the reverse is not true.
    $usable = $Editors | Where-Object {
        $_.Version -like "$MinorVersion.*" -or $_.Version -eq $MinorVersion
    }
    $exact = $usable | Where-Object { $_.Binding -eq $Binding }
    if ($exact) { return ($exact | Sort-Object Version -Descending | Select-Object -First 1) }
    if ($Binding -eq 'gdscript') {
        $mono = $usable | Where-Object { $_.Binding -eq 'dotnet' }
        if ($mono) { return ($mono | Sort-Object Version -Descending | Select-Object -First 1) }
    }
    return $null
}

function Get-TemplateDirs {
    if (-not (Test-Path $TemplatesDir)) { return @() }
    $dirs = Get-ChildItem -Path $TemplatesDir -Directory -ErrorAction SilentlyContinue
    $result = @()
    foreach ($dir in $dirs) {
        $m = [regex]::Match($dir.Name, '^(?<ver>\d+\.\d+(\.\d+)?)\.(?<rel>[a-z0-9]+)(?<mono>\.mono)?$')
        if (-not $m.Success) { continue }
        $result += [pscustomobject]@{
            Version = $m.Groups['ver'].Value
            Binding = $(if ($m.Groups['mono'].Success) { 'dotnet' } else { 'gdscript' })
            Path    = $dir.FullName
            HasRelease = (Test-Path (Join-Path $dir.FullName 'windows_release_x86_64.exe'))
            HasDebug   = (Test-Path (Join-Path $dir.FullName 'windows_debug_x86_64.exe'))
        }
    }
    return $result
}

function Resolve-Template([string] $MinorVersion, [string] $Binding, [string] $Template, $TemplateDirs) {
    $match = $TemplateDirs | Where-Object {
        ($_.Version -like "$MinorVersion.*" -or $_.Version -eq $MinorVersion) -and $_.Binding -eq $Binding
    } | Sort-Object Version -Descending | Select-Object -First 1
    if (-not $match) { return $null }
    if ($Template -eq 'release' -and -not $match.HasRelease) { return $null }
    if ($Template -eq 'debug'   -and -not $match.HasDebug)   { return $null }
    return $match
}

function Resolve-DoubleTemplate([string] $Version, [string] $Binding, [string] $Template) {
    if (-not $DoubleTemplateDir) { return $null }
    $dir = Join-Path $DoubleTemplateDir $Version
    if (-not (Test-Path $dir)) { return $null }
    $suffix = ''
    if ($Binding -eq 'dotnet') { $suffix = '.mono' }
    $exe = Join-Path $dir ("windows_{0}_x86_64{1}.exe" -f $Template, $suffix)
    if (Test-Path $exe) { return $exe }
    $exe = Join-Path $dir ("windows_{0}_x86_64.exe" -f $Template)
    if (Test-Path $exe) { return $exe }
    return $null
}

function Get-InstallHint([string] $Version, [string] $Binding, [string] $What) {
    $monoEd = ''
    $monoTp = ''
    if ($Binding -eq 'dotnet') { $monoEd = '_mono'; $monoTp = '_mono' }
    switch ($What) {
        'editor' {
            # NOTE the asset names differ between the two sources, and getting this wrong 404s:
            # the GitHub release asset for the GDScript editor is "..._win64.exe.zip", while the
            # mono editor is "..._mono_win64.zip". The download page links the same files under
            # friendlier labels. Both spellings are given so a script can use either source.
            $ghAsset = if ($Binding -eq 'dotnet') { 'mono_win64.zip' } else { 'win64.exe.zip' }
            return ("Download Godot_v{0}-stable{1}_win64.zip from https://godotengine.org/download/archive/{0}-stable/ (GitHub release asset name: Godot_v{0}-stable_{3}) and unzip it under {2}" -f $Version, $monoEd, $GodotBinDir, $ghAsset)
        }
        'template' {
            return ("Download Godot_v{0}-stable{1}_export_templates.tpz from https://godotengine.org/download/archive/{0}-stable/ then Editor > Manage Export Templates > Install from File (or unzip its 'templates' folder to {2}\{0}.stable{3})" -f $Version, $monoTp, $TemplatesDir, $(if ($Binding -eq 'dotnet') { '.mono' } else { '' }))
        }
        'double' {
            return ("No OFFICIAL double-precision export templates exist for any Godot version. Build them from source: scons platform=windows target=template_release (and target=template_debug) precision=double [module_mono_enabled=yes], then place the results at {0}\{1}\windows_<release|debug>_x86_64[.mono].exe and pass -DoubleTemplateDir {0}" -f $(if ($DoubleTemplateDir) { $DoubleTemplateDir } else { '<dir>' }), $Version)
        }
        'dotnet-sdk' {
            return 'Install the .NET SDK (dotnet --version must work); Godot runs `dotnet publish` during a .NET export.'
        }
    }
    return ''
}

# --------------------------------------------------------------------------
# Staging: copy the project and patch it for one cell
# --------------------------------------------------------------------------

# --------------------------------------------------------------------------
# The .NET export REQUIRES a solution file, and says so only in a warning.
#
# GodotTools' export plugin aborts the managed half of the export with
#   "This project contains C# files but no solution file was found"
# and the editor still exits 0, having written a perfectly good native
# grid.exe with NO data_<assembly>_windows_x86_64/ payload beside it. The
# exported game then dies with 0xC0000005 the moment it loads Probe.cs.
#
# `--build-solutions` does NOT create the .sln in headless mode (verified on
# 4.5-stable.mono: the step runs, reports DONE, and produces nothing), so the
# harness writes it. This is Godot's own solution layout, not `dotnet new sln`'s
# - the ExportDebug/ExportRelease solution configurations are the ones
# `dotnet publish -c ExportDebug|ExportRelease` is invoked with during export,
# and the debug half of the grid depends on them existing.
# --------------------------------------------------------------------------
function New-GodotSolution([string] $Name) {
    $guid = [guid]::NewGuid().ToString('B').ToUpper()
    $csGuid = '{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}'
    $configs = @('Debug', 'ExportDebug', 'ExportRelease')

    $sb = New-Object System.Text.StringBuilder
    [void] $sb.AppendLine('Microsoft Visual Studio Solution File, Format Version 12.00')
    [void] $sb.AppendLine(('Project("{0}") = "{1}", "{1}.csproj", "{2}"' -f $csGuid, $Name, $guid))
    [void] $sb.AppendLine('EndProject')
    [void] $sb.AppendLine('Global')
    [void] $sb.AppendLine("`tGlobalSection(SolutionConfigurationPlatforms) = preSolution")
    foreach ($c in $configs) { [void] $sb.AppendLine("`t`t$c|Any CPU = $c|Any CPU") }
    [void] $sb.AppendLine("`tEndGlobalSection")
    [void] $sb.AppendLine("`tGlobalSection(ProjectConfigurationPlatforms) = postSolution")
    foreach ($c in $configs) {
        [void] $sb.AppendLine("`t`t$guid.$c|Any CPU.ActiveCfg = $c|Any CPU")
        [void] $sb.AppendLine("`t`t$guid.$c|Any CPU.Build.0 = $c|Any CPU")
    }
    [void] $sb.AppendLine("`tEndGlobalSection")
    [void] $sb.AppendLine('EndGlobal')
    return $sb.ToString()
}

function New-Staging([string] $CellName, [string] $MinorVersion, [string] $Binding, [string] $Precision, [string] $Template, [string] $CustomTemplateExe, [string] $ExportExe) {
    $staging = Join-Path $StagingRoot $CellName
    if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
    New-Item -ItemType Directory -Force -Path $staging | Out-Null

    foreach ($name in @('project.godot', 'Main.tscn', 'export_presets.cfg', 'Probe.cs', 'Probe.gd', 'Marker.cs', 'Marker.gd', 'GodotAbiGrid.csproj', 'expected.json')) {
        $src = Join-Path $ProjectDir $name
        if (Test-Path $src) { Copy-Item $src (Join-Path $staging $name) }
    }

    # ---- project.godot ----
    $pg = Read-TextFile (Join-Path $staging 'project.godot')
    $features = if ($Binding -eq 'dotnet') { '"{0}", "C#", "GL Compatibility"' } else { '"{0}", "GL Compatibility"' }
    $pg = [regex]::Replace($pg, 'config/features=PackedStringArray\([^)]*\)', ('config/features=PackedStringArray(' + ($features -f $MinorVersion) + ')'))
    $pg = $pg -replace 'config/custom_user_dir_name="[^"]*"', ('config/custom_user_dir_name="godot-abi-grid/' + $CellName + '"')
    if ($Binding -ne 'dotnet') {
        # Strip the [dotnet] section; a GDScript-only engine warns about it.
        $pg = [regex]::Replace($pg, '(?ms)^\[dotnet\].*?(?=^\[|\z)', '')
    }
    Write-TextFile (Join-Path $staging 'project.godot') $pg

    # ---- Main.tscn ----
    $scene = Read-TextFile (Join-Path $staging 'Main.tscn')
    if ($Binding -ne 'dotnet') {
        $scene = $scene -replace 'res://Probe\.cs', 'res://Probe.gd'
        $scene = $scene -replace 'res://Marker\.cs', 'res://Marker.gd'
    }
    # Strip ';' comment lines. VariantParser handles them, but a comment is
    # never worth risking a scene-load failure across five engine versions,
    # and the source file keeps the commentary either way.
    $scene = ($scene -split "`n" | Where-Object { $_ -notmatch '^\s*;' }) -join "`n"
    Write-TextFile (Join-Path $staging 'Main.tscn') $scene

    if ($Binding -eq 'dotnet') {
        Remove-Item (Join-Path $staging 'Probe.gd') -Force -ErrorAction SilentlyContinue
        Remove-Item (Join-Path $staging 'Marker.gd') -Force -ErrorAction SilentlyContinue
        $csproj = Read-TextFile (Join-Path $staging 'GodotAbiGrid.csproj')
        $csproj = $csproj -replace 'Godot\.NET\.Sdk/[\d\.]+', ('Godot.NET.Sdk/' + $VersionMatrix[$MinorVersion].Sdk)
        $csproj = $csproj -replace '<TargetFramework>[^<]*</TargetFramework>', ('<TargetFramework>' + $VersionMatrix[$MinorVersion].Tfm + '</TargetFramework>')
        Write-TextFile (Join-Path $staging 'GodotAbiGrid.csproj') $csproj
        Write-TextFile (Join-Path $staging 'GodotAbiGrid.sln') (New-GodotSolution 'GodotAbiGrid')
    } else {
        Remove-Item (Join-Path $staging 'Probe.cs') -Force -ErrorAction SilentlyContinue
        Remove-Item (Join-Path $staging 'Marker.cs') -Force -ErrorAction SilentlyContinue
        Remove-Item (Join-Path $staging 'GodotAbiGrid.csproj') -Force -ErrorAction SilentlyContinue
    }

    # ---- export_presets.cfg ----
    $presets = Read-TextFile (Join-Path $staging 'export_presets.cfg')
    $presets = $presets -replace 'export_path="[^"]*"', ('export_path="' + ($ExportExe -replace '\\', '/') + '"')
    if ($CustomTemplateExe) {
        $ct = $CustomTemplateExe -replace '\\', '/'
        $presets = $presets -replace 'custom_template/release=""', ('custom_template/release="' + $ct + '"')
        $presets = $presets -replace 'custom_template/debug=""',   ('custom_template/debug="' + $ct + '"')
    }
    Write-TextFile (Join-Path $staging 'export_presets.cfg') $presets

    return $staging
}

function ConvertTo-ArgString([string[]] $Items) {
    # ProcessStartInfo.ArgumentList does not exist on .NET Framework, so
    # Windows PowerShell 5.1 needs a quoted argument STRING. "Windows Desktop"
    # (the preset name) and any path with a space both depend on this.
    $quoted = foreach ($item in $Items) {
        if ($item -match '[\s"]') { '"' + ($item -replace '"', '\"') + '"' } else { $item }
    }
    return ($quoted -join ' ')
}

function Invoke-Godot([string] $Exe, [string[]] $GodotArgs, [int] $TimeoutSec) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $Exe
    $psi.Arguments = ConvertTo-ArgString $GodotArgs
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $proc = [System.Diagnostics.Process]::Start($psi)
    $stdout = $proc.StandardOutput.ReadToEndAsync()
    $stderr = $proc.StandardError.ReadToEndAsync()
    if (-not $proc.WaitForExit($TimeoutSec * 1000)) {
        try { $proc.Kill() } catch { }
        return [pscustomobject]@{ ExitCode = -1; StdOut = ''; StdErr = "timed out after ${TimeoutSec}s" }
    }
    return [pscustomobject]@{ ExitCode = $proc.ExitCode; StdOut = $stdout.Result; StdErr = $stderr.Result }
}

# --------------------------------------------------------------------------
# Main
# --------------------------------------------------------------------------

$editors = Get-GodotEditors
$templateDirs = Get-TemplateDirs
$hasDotnetSdk = [bool] (Get-Command dotnet -ErrorAction SilentlyContinue)
$hasNode = [bool] (Get-Command node -ErrorAction SilentlyContinue)

Write-Head 'Detected'
Write-Host "  harness      : $HarnessRoot"
Write-Host "  editors dir  : $GodotBinDir"
Write-Host "  templates dir: $TemplatesDir"
Write-Host "  double dir   : $(if ($DoubleTemplateDir) { $DoubleTemplateDir } else { '(none - see README)' })"
Write-Host "  dotnet SDK   : $(if ($hasDotnetSdk) { 'present' } else { 'MISSING - .NET cells cannot export' })"
if ($editors.Count -eq 0) {
    Write-Host '  editors      : none found' -ForegroundColor Yellow
} else {
    foreach ($e in $editors) { Write-Host ("  editor       : {0,-8} {1,-9} {2}" -f $e.Version, $e.Binding, $e.Path) }
}
if ($templateDirs.Count -eq 0) {
    Write-Host '  templates    : none found' -ForegroundColor Yellow
} else {
    foreach ($t in $templateDirs) { Write-Host ("  templates    : {0,-8} {1,-9} release={2} debug={3}" -f $t.Version, $t.Binding, $t.HasRelease, $t.HasDebug) }
}

# expected.json must match Main.tscn, or every downstream result is a lie.
Write-Head 'Ground truth'
if (-not $hasNode) {
    Write-Skip 'node not found - cannot verify expected.json against Main.tscn (calibrate.mjs re-checks it anyway)'
} else {
    $null = & node (Join-Path $HarnessRoot 'gen-expected.mjs') --check
    if ($LASTEXITCODE -ne 0) {
        Write-Bad 'expected.json is STALE relative to Main.tscn'
        Write-Host '  Refusing to build against ground truth that has drifted from the scene.' -ForegroundColor Red
        Write-Host '  Fix: node gen-expected.mjs' -ForegroundColor Red
        exit 1
    }
    Write-Ok 'expected.json matches Main.tscn'
}

$built = @()
$missing = @()
$errors = @()

# Reference cell first (analysis.md 8.9: 4.5.1 release single dotnet is already
# known good, so it validates the harness before the harness judges anything).
$orderedVersions = @($Versions | Where-Object { $_ -eq '4.5' }) + @($Versions | Where-Object { $_ -ne '4.5' })

foreach ($version in $orderedVersions) {
    foreach ($template in $Templates) {
        foreach ($precision in $Precisions) {
            foreach ($binding in $Bindings) {

                $editor = Resolve-Editor $version $binding $editors
                $resolvedVersion = $version
                if ($editor) { $resolvedVersion = $editor.Version }
                $cellName = "$resolvedVersion-$template-$precision-$binding"
                if ($Only -and ($cellName -notlike "*$Only*")) { continue }

                $reasons = @()
                $installs = @()
                $customTemplate = $null

                if (-not $editor) {
                    $reasons += "no Godot $version editor ($binding)"
                    $installs += (Get-InstallHint $version $binding 'editor')
                }
                if ($binding -eq 'dotnet' -and -not $hasDotnetSdk) {
                    $reasons += 'no .NET SDK'
                    $installs += (Get-InstallHint $version $binding 'dotnet-sdk')
                }

                if ($precision -eq 'double') {
                    $customTemplate = Resolve-DoubleTemplate $resolvedVersion $binding $template
                    if (-not $customTemplate) {
                        $reasons += 'no double-precision export template'
                        $installs += (Get-InstallHint $resolvedVersion $binding 'double')
                    }
                } else {
                    $tpl = Resolve-Template $version $binding $template $templateDirs
                    if (-not $tpl) {
                        $reasons += "no $version $binding $template export template"
                        $installs += (Get-InstallHint $version $binding 'template')
                    }
                }

                if ($reasons.Count -gt 0) {
                    Write-Skip ("{0,-34} {1}" -f $cellName, ($reasons -join '; '))
                    $missing += [pscustomobject]@{
                        cell    = $cellName
                        axes    = @{ version = $version; template = $template; precision = $precision; binding = $binding }
                        missing = ($reasons -join '; ')
                        install = ($installs -join ' | ')
                    }
                    continue
                }

                if ($ListOnly) {
                    Write-Ok ("{0,-34} buildable" -f $cellName)
                    $built += [pscustomobject]@{ cell = $cellName; executable = 'grid.exe'; status = 'buildable (not built: -ListOnly)' }
                    continue
                }

                # ---- build ----
                Write-Head "Building $cellName"
                $cellOut = Join-Path $OutDir $cellName
                if (Test-Path $cellOut) { Remove-Item -Recurse -Force $cellOut }
                New-Item -ItemType Directory -Force -Path $cellOut | Out-Null
                $exportExe = Join-Path $cellOut 'grid.exe'

                $staging = New-Staging $cellName $version $binding $precision $template $customTemplate $exportExe

                try {
                    $import = Invoke-Godot $editor.Path @('--headless', '--path', $staging, '--import') $ExportTimeoutSec
                    if ($import.ExitCode -ne 0) {
                        # 4.2 predates a reliable --import exit code; --editor --quit is the fallback.
                        $import = Invoke-Godot $editor.Path @('--headless', '--path', $staging, '--editor', '--quit') $ExportTimeoutSec
                    }

                    if ($binding -eq 'dotnet') {
                        # Generates the .sln/.csproj glue and warms the build. Failure here
                        # is not fatal: --export-release builds the solution too.
                        $null = Invoke-Godot $editor.Path @('--headless', '--path', $staging, '--build-solutions', '--quit') $ExportTimeoutSec
                    }

                    $exportFlag = if ($template -eq 'release') { '--export-release' } else { '--export-debug' }
                    $export = Invoke-Godot $editor.Path @('--headless', '--path', $staging, $exportFlag, 'Windows Desktop', $exportExe) $ExportTimeoutSec

                    if (-not (Test-Path $exportExe)) {
                        Write-Bad ("{0}: export produced no executable (exit {1})" -f $cellName, $export.ExitCode)
                        Write-Host ($export.StdErr.Trim() -split "`n" | Select-Object -First 12 | ForEach-Object { "         $_" }) -ForegroundColor DarkGray
                        $errors += [pscustomobject]@{ cell = $cellName; error = "export failed (exit $($export.ExitCode))"; stderr = $export.StdErr }
                        continue
                    }

                    # ---- the exe existing is NOT the export succeeding ----
                    #
                    # Godot exits 0 after an export that its own plugins aborted:
                    # a .NET export with no .sln logs ERROR, writes the native
                    # binary anyway, and omits the managed payload entirely. The
                    # first grid run lost a cell to exactly that. So: fail on any
                    # ERROR the exporter logged, and for .NET cells require the
                    # assembly the game will actually try to load.
                    $exportLog = ($export.StdOut + "`n" + $export.StdErr)
                    $exportErrors = @($exportLog -split "`r?`n" | Where-Object { $_ -match '^\s*ERROR:' } | Select-Object -First 8)
                    $payloadProblem = $null

                    if ($binding -eq 'dotnet') {
                        $payloadDir = Get-ChildItem $cellOut -Directory -ErrorAction SilentlyContinue |
                            Where-Object { $_.Name -like 'data_*' } | Select-Object -First 1
                        if (-not $payloadDir) {
                            $payloadProblem = "no data_*/ managed payload beside grid.exe - the .NET half of the export did not run"
                        } elseif (-not (Test-Path (Join-Path $payloadDir.FullName 'GodotAbiGrid.dll'))) {
                            $payloadProblem = "managed payload $($payloadDir.Name) has no GodotAbiGrid.dll"
                        }
                    }

                    if ($exportErrors.Count -gt 0 -or $payloadProblem) {
                        $why = @()
                        if ($payloadProblem) { $why += $payloadProblem }
                        if ($exportErrors.Count -gt 0) { $why += "exporter logged $($exportErrors.Count) ERROR line(s)" }
                        Write-Bad ("{0}: export incomplete - {1}" -f $cellName, ($why -join '; '))
                        foreach ($line in $exportErrors) { Write-Host "         $line" -ForegroundColor DarkGray }
                        Remove-Item -Recurse -Force $cellOut -ErrorAction SilentlyContinue
                        $errors += [pscustomobject]@{
                            cell = $cellName
                            error = ($why -join '; ')
                            exportErrors = $exportErrors
                        }
                        continue
                    }

                    $manifest = [ordered]@{
                        cell        = $cellName
                        contract    = 'godot-abi-grid/cell.v1'
                        version     = $resolvedVersion
                        template    = $template
                        precision   = $precision
                        binding     = $binding
                        executable  = 'grid.exe'
                        editor      = $editor.Path
                        customTemplate = $customTemplate
                        exportedAt  = (Get-Date).ToString('o')
                        exportExit  = $export.ExitCode
                        sceneSha256 = (Get-FileHash (Join-Path $ProjectDir 'Main.tscn') -Algorithm SHA256).Hash.ToLower()
                    }
                    Write-TextFile (Join-Path $cellOut 'cell.json') ($manifest | ConvertTo-Json -Depth 5)
                    Copy-Item (Join-Path $ProjectDir 'expected.json') (Join-Path $cellOut 'expected.json') -Force

                    Write-Ok ("{0} -> {1}" -f $cellName, $exportExe)
                    $built += [pscustomobject]@{ cell = $cellName; executable = 'grid.exe'; status = 'built' }
                } catch {
                    Write-Bad ("{0}: {1}" -f $cellName, $_.Exception.Message)
                    $errors += [pscustomobject]@{ cell = $cellName; error = $_.Exception.Message }
                } finally {
                    if (-not $KeepStaging -and (Test-Path $staging)) {
                        Remove-Item -Recurse -Force $staging -ErrorAction SilentlyContinue
                    }
                }
            }
        }
    }
}

# --------------------------------------------------------------------------
# Availability report
# --------------------------------------------------------------------------

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$availability = [ordered]@{
    contract    = 'godot-abi-grid/availability.v1'
    generatedAt = (Get-Date).ToString('o')
    listOnly    = [bool] $ListOnly
    machine     = $env:COMPUTERNAME
    godotBinDir = $GodotBinDir
    templatesDir = $TemplatesDir
    doubleTemplateDir = $DoubleTemplateDir
    dotnetSdk   = $hasDotnetSdk
    editors     = @($editors)
    templates   = @($templateDirs)
    built       = @($built)
    missing     = @($missing)
    errors      = @($errors)
}
Write-TextFile (Join-Path $OutDir 'availability.json') ($availability | ConvertTo-Json -Depth 6)

Write-Head 'Summary'
Write-Host ("  built    : {0}" -f $built.Count)
Write-Host ("  skipped  : {0}" -f $missing.Count)
Write-Host ("  errors   : {0}" -f $errors.Count)
Write-Host ("  written  : {0}" -f (Join-Path $OutDir 'availability.json'))

if ($missing.Count -gt 0) {
    Write-Head 'To fill the empty cells, install'
    $hints = $missing | ForEach-Object { $_.install -split ' \| ' } | Where-Object { $_ } | Sort-Object -Unique
    foreach ($h in $hints) { Write-Host "  * $h" }
    Write-Host ''
    Write-Host '  Then re-run this script. Cells that stay unbuildable are reported as' -ForegroundColor DarkGray
    Write-Host '  "not built" in REPORT.md rather than being silently dropped.' -ForegroundColor DarkGray
}

# A missing cell is the expected state, not a build failure. Only a genuine
# export error is worth a non-zero exit.
if ($errors.Count -gt 0) { exit 1 }
exit 0
