#requires -Version 7.0
<#
.SYNOPSIS
  Local whisper.cpp lifecycle for TCT English development (Setup/Start/Status/Stop/Benchmark).

.NOTES
  Artifacts stay under .appdata/local/speech/ (gitignored). Metadata pin: tooling/whisper/artifacts.lock.json.
  Stop only kills processes that match ownership markers for this checkout. Never taskkill by image name.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Setup', 'Start', 'Status', 'Stop', 'Benchmark', 'Verify', 'ValidateProbeBody')]
    [string]$Action,

    [string]$ProbeBodyFile = '',

    [string]$RepoRoot = '',
    [string]$SpeechRoot = '',
    [string]$LockPath = '',
    [int]$Port = 0,
    [int]$Threads = 0,
    [string]$ExecutableOverride = '',
    [string]$ModelOverride = '',
    [string]$RuntimeFileName = '',
    [string[]]$RuntimePrefixArgs = @(),
    [switch]$Offline,
    [switch]$SkipLegacyCache,
    [int]$BenchmarkSeconds = 5,
    [string]$BenchmarkWav = '',
    [int]$ReadinessDeadlineSeconds = 0
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-RepoRoot {
    param([string]$Explicit)
    if ($Explicit) { return (Resolve-Path -LiteralPath $Explicit).Path }
    return (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
}

function Get-Sha256Lower {
    param([string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Read-ArtifactLock {
    param([string]$Root)
    $path = Join-Path $Root 'tooling/whisper/artifacts.lock.json'
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing artifact lock: $path"
    }
    return Get-Content -LiteralPath $path -Raw -Encoding utf8 | ConvertFrom-Json
}

function New-SpeechPaths {
    param([string]$Root, [string]$SpeechRootOverride)
    $speechRoot = if ($SpeechRootOverride) {
        $SpeechRootOverride
    } else {
        Join-Path $Root '.appdata/local/speech'
    }
    $artifacts = Join-Path $speechRoot 'artifacts'
    $binDir = Join-Path $artifacts 'bin'
    $logs = Join-Path $speechRoot 'logs'
    $probes = Join-Path $speechRoot 'probes'
    [pscustomobject]@{
        SpeechRoot   = $speechRoot
        Artifacts    = $artifacts
        BinDir       = $binDir
        Logs         = $logs
        Probes       = $probes
        StatePath    = Join-Path $speechRoot 'state.json'
        LockFilePath = Join-Path $speechRoot 'speech.lock'
        ZipPath      = Join-Path $artifacts 'whisper-bin-x64.zip'
        ModelPath    = Join-Path $artifacts 'ggml-tiny.en.bin'
        SilenceWav   = Join-Path $probes 'silence-1s.wav'
        OutLog       = Join-Path $logs 'whisper-server.out.log'
        ErrLog       = Join-Path $logs 'whisper-server.err.log'
    }
}

function Ensure-SpeechDirectories {
    param($Paths)
    foreach ($dir in @($Paths.SpeechRoot, $Paths.Artifacts, $Paths.BinDir, $Paths.Logs, $Paths.Probes)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

function Enter-SpeechLock {
    param([string]$LockFilePath, [int]$TimeoutSeconds = 120)
    $dir = Split-Path -Parent $LockFilePath
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ($true) {
        try {
            $stream = [System.IO.File]::Open(
                $LockFilePath,
                [System.IO.FileMode]::OpenOrCreate,
                [System.IO.FileAccess]::ReadWrite,
                [System.IO.FileShare]::None)
            $writer = New-Object System.IO.StreamWriter($stream)
            $writer.WriteLine((@{
                    pid       = $PID
                    acquired  = [DateTime]::UtcNow.ToString('o')
                    machine   = $env:COMPUTERNAME
                } | ConvertTo-Json -Compress))
            $writer.Flush()
            return [pscustomobject]@{ Stream = $stream; Writer = $writer; Path = $LockFilePath }
        } catch {
            if ([DateTime]::UtcNow -ge $deadline) {
                throw "Timed out waiting for speech lock at $LockFilePath"
            }
            Start-Sleep -Milliseconds 200
        }
    }
}

function Exit-SpeechLock {
    param($LockHandle)
    if ($null -eq $LockHandle) { return }
    try { $LockHandle.Writer.Dispose() } catch { }
    try { $LockHandle.Stream.Dispose() } catch { }
}

function Test-PathInsideRoot {
    param([string]$Root, [string]$Candidate)
    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $candFull = [System.IO.Path]::GetFullPath($Candidate)
    return $candFull.StartsWith($rootFull, [System.StringComparison]::OrdinalIgnoreCase) -or
        $candFull.Equals([System.IO.Path]::GetFullPath($Root), [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-FreeDiskBytes {
    param([string]$Path, [long]$RequiredBytes)
    $full = [System.IO.Path]::GetFullPath($Path)
    $root = [System.IO.Path]::GetPathRoot($full)
    $drive = Get-PSDrive -Name $root.Substring(0, 1) -ErrorAction Stop
    $free = [int64]$drive.Free
    if ($free -lt $RequiredBytes) {
        throw ("Insufficient disk space near {0}: need ~{1:N0} bytes, free {2:N0}. Free space manually; refusing to delete other caches." -f $root, $RequiredBytes, $free)
    }
}

function Copy-IfHashMatches {
    param([string]$Source, [string]$Destination, [string]$ExpectedSha)
    if (-not (Test-Path -LiteralPath $Source)) { return $false }
    $actual = Get-Sha256Lower $Source
    if ($actual -ne $ExpectedSha.ToLowerInvariant()) { return $false }
    $destDir = Split-Path -Parent $Destination
    New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    $tmp = "$Destination.partial"
    Copy-Item -LiteralPath $Source -Destination $tmp -Force
    $copied = Get-Sha256Lower $tmp
    if ($copied -ne $ExpectedSha.ToLowerInvariant()) {
        Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
        throw "Copied artifact hash mismatch for $Destination"
    }
    Move-Item -LiteralPath $tmp -Destination $Destination -Force
    return $true
}

function Invoke-DownloadPinned {
    param(
        [string]$Url,
        [string]$Destination,
        [string]$ExpectedSha,
        [switch]$OfflineMode
    )
    if (Test-Path -LiteralPath $Destination) {
        $existing = Get-Sha256Lower $Destination
        if ($existing -eq $ExpectedSha.ToLowerInvariant()) { return }
        Remove-Item -LiteralPath $Destination -Force
    }
    if ($OfflineMode) {
        throw "Offline mode: missing or mismatched artifact at $Destination. Run Setup while online or place a hash-matching file."
    }
    $tmp = "$Destination.partial"
    Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
    Write-Host "Downloading $Url ..."
    Invoke-WebRequest -Uri $Url -OutFile $tmp -UseBasicParsing
    $actual = Get-Sha256Lower $tmp
    if ($actual -ne $ExpectedSha.ToLowerInvariant()) {
        Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
        throw "Download hash mismatch for $Destination. expected=$ExpectedSha actual=$actual"
    }
    Move-Item -LiteralPath $tmp -Destination $Destination -Force
}

function Expand-PinnedZipSafe {
    param([string]$ZipPath, [string]$DestinationDir, [string]$ExpectedExeRelative)
    if (Test-Path -LiteralPath $DestinationDir) {
        Remove-Item -LiteralPath $DestinationDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $DestinationDir -Force | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        foreach ($entry in $archive.Entries) {
            if ([string]::IsNullOrWhiteSpace($entry.FullName)) { continue }
            $normalized = $entry.FullName -replace '/', '\'
            if ($normalized.Contains('..')) {
                throw "Refusing zip entry with path traversal: $($entry.FullName)"
            }
            $target = Join-Path $DestinationDir $normalized
            if (-not (Test-PathInsideRoot -Root $DestinationDir -Candidate $target)) {
                throw "Refusing zip entry outside destination: $($entry.FullName)"
            }
            if ($normalized.EndsWith('\')) {
                New-Item -ItemType Directory -Path $target -Force | Out-Null
                continue
            }
            $parent = Split-Path -Parent $target
            if ($parent) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        }
    } finally {
        $archive.Dispose()
    }
    $exe = Join-Path $DestinationDir $ExpectedExeRelative
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "Extracted zip missing expected executable: $ExpectedExeRelative"
    }
}

function Assert-InstalledArtifacts {
    param($Paths, $Lock, [string]$ExecutableOverride, [string]$ModelOverride)
    $exePath = if ($ExecutableOverride) { $ExecutableOverride } else { Join-Path $Paths.BinDir $Lock.executable.relativePath }
    $modelPath = if ($ModelOverride) { $ModelOverride } else { $Paths.ModelPath }

    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "Whisper executable missing. Run: pwsh -NoProfile -File scripts/local-speech.ps1 -Action Setup"
    }
    if (-not (Test-Path -LiteralPath $modelPath)) {
        throw "Whisper model missing. Run: pwsh -NoProfile -File scripts/local-speech.ps1 -Action Setup"
    }

    if (-not $ExecutableOverride) {
        $exeHash = Get-Sha256Lower $exePath
        if ($exeHash -ne $Lock.executable.sha256.ToLowerInvariant()) {
            throw "Executable hash mismatch; refusing to start. expected=$($Lock.executable.sha256) actual=$exeHash"
        }
        $binRoot = Split-Path -Parent $exePath
        foreach ($dll in $Lock.requiredDlls) {
            $dllPath = Join-Path $binRoot $dll.fileName
            if (-not (Test-Path -LiteralPath $dllPath)) {
                throw "Required DLL missing: $($dll.fileName)"
            }
            $dllHash = Get-Sha256Lower $dllPath
            if ($dllHash -ne $dll.sha256.ToLowerInvariant()) {
                throw "DLL hash mismatch for $($dll.fileName)"
            }
        }
        $pinnedCpu = @($Lock.cpuBackendDlls)
        if ($pinnedCpu.Count -lt 1) {
            throw 'Artifact lock missing cpuBackendDlls pins.'
        }
        $allowedNames = @{}
        foreach ($dll in $pinnedCpu) {
            $allowedNames[$dll.fileName.ToLowerInvariant()] = $dll.sha256.ToLowerInvariant()
            $dllPath = Join-Path $binRoot $dll.fileName
            if (-not (Test-Path -LiteralPath $dllPath)) {
                throw "Required CPU backend DLL missing: $($dll.fileName)"
            }
            $dllHash = Get-Sha256Lower $dllPath
            if ($dllHash -ne $dll.sha256.ToLowerInvariant()) {
                throw "CPU backend DLL hash mismatch for $($dll.fileName)"
            }
        }
        $presentCpu = @(Get-ChildItem -LiteralPath $binRoot -Filter $Lock.cpuBackendDllGlob -File -ErrorAction SilentlyContinue)
        foreach ($extra in $presentCpu) {
            $key = $extra.Name.ToLowerInvariant()
            if (-not $allowedNames.ContainsKey($key)) {
                throw "Unexpected CPU backend DLL not in lock allowlist: $($extra.Name)"
            }
        }
    }

    if (-not $ModelOverride) {
        $modelHash = Get-Sha256Lower $modelPath
        if ($modelHash -ne $Lock.model.sha256.ToLowerInvariant()) {
            throw "Model hash mismatch; refusing to start. expected=$($Lock.model.sha256) actual=$modelHash"
        }
    }

    return [pscustomobject]@{
        ExecutablePath = (Resolve-Path -LiteralPath $exePath).Path
        ModelPath      = (Resolve-Path -LiteralPath $modelPath).Path
        ExecutableHash = Get-Sha256Lower $exePath
        ModelHash      = Get-Sha256Lower $modelPath
    }
}

function New-SilenceWav {
    param([string]$Path, [int]$Seconds = 1)
    $sampleRate = 16000
    $dataBytes = $sampleRate * 2 * $Seconds
    $stream = [System.IO.File]::Create($Path)
    try {
        $bw = New-Object System.IO.BinaryWriter($stream)
        $bw.Write([Text.Encoding]::ASCII.GetBytes('RIFF'))
        $bw.Write([int](36 + $dataBytes))
        $bw.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt '))
        $bw.Write([int]16)
        $bw.Write([int16]1)
        $bw.Write([int16]1)
        $bw.Write([int]$sampleRate)
        $bw.Write([int]($sampleRate * 2))
        $bw.Write([int16]2)
        $bw.Write([int16]16)
        $bw.Write([Text.Encoding]::ASCII.GetBytes('data'))
        $bw.Write([int]$dataBytes)
        $bw.Write([byte[]]::new($dataBytes))
        $bw.Flush()
    } finally {
        $stream.Dispose()
    }
}

function Test-TcpPortOpen {
    param([string]$HostName, [int]$PortNumber, [int]$TimeoutMs = 500)
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $async = $client.BeginConnect($HostName, $PortNumber, $null, $null)
        if (-not $async.AsyncWaitHandle.WaitOne($TimeoutMs)) {
            return $false
        }
        $client.EndConnect($async)
        return $true
    } catch {
        return $false
    } finally {
        $client.Close()
    }
}

function Get-ListenerOwner {
    param([string]$HostName, [int]$PortNumber)
    # Intentionally avoid Get-NetTCPConnection — it can stall for minutes on some Windows hosts.
    # Return -1 when the TCP port accepts connections (owner PID unknown), else $null.
    if (Test-TcpPortOpen -HostName $HostName -PortNumber $PortNumber) {
        return -1
    }
    return $null
}

function Get-OwnedProcessIdentity {
    param([int]$ProcessId)
    try {
        $proc = Get-Process -Id $ProcessId -ErrorAction Stop
    } catch {
        return $null
    }
    $path = $null
    try { $path = $proc.Path } catch { $path = $null }
    # Do not call Get-CimInstance/Win32_Process here — it can stall for minutes on
    # some Windows hosts and blocks the Start CLI from returning under automation.
    return [pscustomobject]@{
        ProcessId   = $ProcessId
        ProcessName = $proc.ProcessName
        StartTime   = $proc.StartTime
        Path        = $path
        CommandLine = $null
        WorkingSet  = $proc.WorkingSet64
    }
}

function Resolve-ExpectedRuntimePath {
    param([string]$InstalledExecutablePath, [string]$RuntimeFileName)
    if ($RuntimeFileName) {
        return [System.IO.Path]::GetFullPath($RuntimeFileName)
    }
    return [System.IO.Path]::GetFullPath($InstalledExecutablePath)
}

function Test-OwnedSpeechProcess {
    param(
        $State,
        [string]$ExpectedExePath,
        [string]$ExpectedRuntimePath,
        [string]$ExpectedExeHash,
        [string]$ExpectedModelHash,
        [int]$ExpectedPort,
        [string]$CheckoutRoot
    )
    if ($null -eq $State) { return $false }
    if ($State.checkoutRoot -ne $CheckoutRoot) { return $false }
    if ([int]$State.port -ne $ExpectedPort) { return $false }
    if ($State.executableHash -ne $ExpectedExeHash) { return $false }
    if ($State.modelHash -ne $ExpectedModelHash) { return $false }
    $identity = Get-OwnedProcessIdentity -ProcessId ([int]$State.pid)
    if ($null -eq $identity) { return $false }
    $stateStart = [DateTime]::Parse($State.startTimeLocal, $null, [System.Globalization.DateTimeStyles]::RoundtripKind)
    # Allow 2s skew between recorded and OS start times.
    if ([Math]::Abs(($identity.StartTime - $stateStart).TotalSeconds) -gt 2) { return $false }

    # Fail closed: OS path is authoritative. State.commandLine / state.runtimeFileName
    # must never override a missing or mismatched executable path (WH01).
    if ([string]::IsNullOrWhiteSpace($identity.Path)) { return $false }
    $runtimeExpected = if ($ExpectedRuntimePath) {
        [System.IO.Path]::GetFullPath($ExpectedRuntimePath)
    } else {
        [System.IO.Path]::GetFullPath($ExpectedExePath)
    }
    $resolvedActual = [System.IO.Path]::GetFullPath($identity.Path)
    if (-not $resolvedActual.Equals($runtimeExpected, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }

    # Optional consistency checks against recorded command line (never used to accept a wrong path).
    $commandLine = [string]$State.commandLine
    if ($commandLine) {
        if ($commandLine -notmatch [regex]::Escape("--port $ExpectedPort") -and
            $commandLine -notmatch [regex]::Escape("--port=$ExpectedPort")) {
            if ($commandLine -notmatch "\s$ExpectedPort(\s|$)") { return $false }
        }
        if ($State.modelPath) {
            $modelName = [IO.Path]::GetFileName([string]$State.modelPath)
            if ($modelName -and ($commandLine -notlike "*${modelName}*")) {
                return $false
            }
        }
    }
    return $true
}

function Stop-ProcessIfStillOwned {
    param(
        [int]$ProcessId,
        [datetime]$ExpectedStartTime,
        [string]$ExpectedRuntimePath
    )
    $identity = Get-OwnedProcessIdentity -ProcessId $ProcessId
    if ($null -eq $identity) { return $false }
    if ([Math]::Abs(($identity.StartTime - $ExpectedStartTime).TotalSeconds) -gt 2) { return $false }
    if ([string]::IsNullOrWhiteSpace($identity.Path)) { return $false }
    $expected = [System.IO.Path]::GetFullPath($ExpectedRuntimePath)
    $actual = [System.IO.Path]::GetFullPath($identity.Path)
    if (-not $actual.Equals($expected, [System.StringComparison]::OrdinalIgnoreCase)) { return $false }
    $proc = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if (-not $proc) { return $false }
    try { $proc.Kill($true) } catch { Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue }
    return $true
}

function Read-SpeechState {
    param([string]$StatePath)
    if (-not (Test-Path -LiteralPath $StatePath)) { return $null }
    try {
        return Get-Content -LiteralPath $StatePath -Raw -Encoding utf8 | ConvertFrom-Json
    } catch {
        return $null
    }
}

function Write-SpeechState {
    param([string]$StatePath, $StateObject)
    $json = $StateObject | ConvertTo-Json -Depth 8
    $tmp = "$StatePath.partial"
    Set-Content -LiteralPath $tmp -Value $json -Encoding utf8NoBOM
    Move-Item -LiteralPath $tmp -Destination $StatePath -Force
}

function Clear-SpeechState {
    param([string]$StatePath)
    if (Test-Path -LiteralPath $StatePath) {
        Remove-Item -LiteralPath $StatePath -Force
    }
}

function Test-InferenceSegmentElement {
    param([System.Text.Json.JsonElement]$Item)
    # Mirror WhisperHttpTranscriber.TryReadSegment — false means invalid_schema for the body.
    if ($Item.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) {
        return [pscustomobject]@{ Ok = $false; Reason = 'segment-not-object' }
    }

    $textNode = [System.Text.Json.JsonElement]::new()
    if ($Item.TryGetProperty('text', [ref]$textNode)) {
        if ($textNode.ValueKind -eq [System.Text.Json.JsonValueKind]::String) {
            # ok
        } elseif ($textNode.ValueKind -eq [System.Text.Json.JsonValueKind]::Null) {
            # treated as empty string
        } else {
            return [pscustomobject]@{ Ok = $false; Reason = 'segment-text-type' }
        }
    }

    $start = 0.0
    $startNode = [System.Text.Json.JsonElement]::new()
    if ($Item.TryGetProperty('start', [ref]$startNode)) {
        if ($startNode.ValueKind -ne [System.Text.Json.JsonValueKind]::Number) {
            return [pscustomobject]@{ Ok = $false; Reason = 'segment-start-type' }
        }
        $tmp = 0.0
        if (-not $startNode.TryGetDouble([ref]$tmp) -or [double]::IsNaN($tmp) -or [double]::IsInfinity($tmp)) {
            return [pscustomobject]@{ Ok = $false; Reason = 'segment-start-finite' }
        }
        $start = $tmp
    }

    $end = $start
    $endNode = [System.Text.Json.JsonElement]::new()
    if ($Item.TryGetProperty('end', [ref]$endNode)) {
        if ($endNode.ValueKind -ne [System.Text.Json.JsonValueKind]::Number) {
            return [pscustomobject]@{ Ok = $false; Reason = 'segment-end-type' }
        }
        $tmp = 0.0
        if (-not $endNode.TryGetDouble([ref]$tmp) -or [double]::IsNaN($tmp) -or [double]::IsInfinity($tmp)) {
            return [pscustomobject]@{ Ok = $false; Reason = 'segment-end-finite' }
        }
        $end = $tmp
    }
    if ($end -lt $start) {
        return [pscustomobject]@{ Ok = $false; Reason = 'segment-end-before-start' }
    }
    return [pscustomobject]@{ Ok = $true; Reason = 'ok' }
}

function Test-InferenceResponseBody {
    param([string]$Body)
    # Mirror WhisperHttpTranscriber.MapVerboseJson using JsonDocument (preserve root kind).
    if ([string]::IsNullOrWhiteSpace($Body)) {
        return [pscustomobject]@{ Ok = $false; Reason = 'empty-body' }
    }
    $doc = $null
    try {
        $doc = [System.Text.Json.JsonDocument]::Parse($Body)
    } catch {
        return [pscustomobject]@{ Ok = $false; Reason = 'invalid-json' }
    }
    try {
        $root = $doc.RootElement
        if ($root.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) {
            return [pscustomobject]@{ Ok = $false; Reason = 'not-object' }
        }

        $errorNode = [System.Text.Json.JsonElement]::new()
        if ($root.TryGetProperty('error', [ref]$errorNode)) {
            return [pscustomobject]@{ Ok = $false; Reason = 'error-property' }
        }

        $textNode = [System.Text.Json.JsonElement]::new()
        $segmentsNode = [System.Text.Json.JsonElement]::new()
        $hasText = $root.TryGetProperty('text', [ref]$textNode)
        $hasSegments = $root.TryGetProperty('segments', [ref]$segmentsNode)
        if (-not $hasText -and -not $hasSegments) {
            return [pscustomobject]@{ Ok = $false; Reason = 'missing-text-and-segments' }
        }

        # Present text must be a JSON string (null/number/object => invalid_schema).
        $textIsNull = $true
        if ($hasText) {
            if ($textNode.ValueKind -ne [System.Text.Json.JsonValueKind]::String) {
                return [pscustomobject]@{ Ok = $false; Reason = 'text-not-string' }
            }
            $textIsNull = $false
        }

        $segmentCount = 0
        if ($hasSegments) {
            if ($segmentsNode.ValueKind -ne [System.Text.Json.JsonValueKind]::Array) {
                return [pscustomobject]@{ Ok = $false; Reason = 'segments-not-array' }
            }
            foreach ($item in $segmentsNode.EnumerateArray()) {
                if ($item.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) {
                    return [pscustomobject]@{ Ok = $false; Reason = 'segment-not-object' }
                }
                $seg = Test-InferenceSegmentElement -Item $item
                if (-not $seg.Ok) {
                    return $seg
                }
                $segmentCount++
            }
        }

        if ($textIsNull -and $segmentCount -eq 0) {
            return [pscustomobject]@{ Ok = $false; Reason = 'silence-requires-explicit-text' }
        }
        return [pscustomobject]@{ Ok = $true; Reason = 'ok' }
    } finally {
        if ($null -ne $doc) { $doc.Dispose() }
    }
}

function Invoke-InferenceProbe {
    param([string]$BaseUrl, [string]$WavPath, [double]$TimeoutSeconds)
    $uri = ($BaseUrl.TrimEnd('/') + '/inference')
    $timeout = [Math]::Max(0.01, $TimeoutSeconds)
    $statusCode = 0
    $body = ''
    $curl = Get-Command curl.exe -ErrorAction SilentlyContinue
    if ($curl) {
        $bodyFile = Join-Path ([System.IO.Path]::GetTempPath()) ("tct-asr-probe-" + [guid]::NewGuid().ToString('N') + ".txt")
        try {
            $args = @(
                '-sS', '-m', $timeout.ToString('0.###', [Globalization.CultureInfo]::InvariantCulture),
                '-o', $bodyFile, '-w', '%{http_code}',
                '-F', "file=@$WavPath;type=audio/wav",
                '-F', 'response_format=verbose_json',
                '-F', 'language=en',
                $uri
            )
            $code = & curl.exe @args 2>$null
            $exit = $LASTEXITCODE
            $body = if (Test-Path -LiteralPath $bodyFile) { Get-Content -LiteralPath $bodyFile -Raw -ErrorAction SilentlyContinue } else { '' }
            [void][int]::TryParse([string]$code, [ref]$statusCode)
            if ($exit -ne 0 -or $statusCode -lt 200 -or $statusCode -ge 300) {
                return [pscustomobject]@{
                    Ok         = $false
                    StatusCode = $statusCode
                    Body       = $body
                    Reason     = "http-$statusCode"
                }
            }
        } finally {
            Remove-Item -LiteralPath $bodyFile -Force -ErrorAction SilentlyContinue
        }
    } else {
        $handler = New-Object System.Net.Http.HttpClientHandler
        $client = [System.Net.Http.HttpClient]::new($handler)
        try {
            $client.Timeout = [TimeSpan]::FromSeconds($timeout)
            $content = [System.Net.Http.MultipartFormDataContent]::new()
            $bytes = [System.IO.File]::ReadAllBytes($WavPath)
            $byteContent = [System.Net.Http.ByteArrayContent]::new($bytes)
            $byteContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('audio/wav')
            $content.Add($byteContent, 'file', 'speech.wav')
            $content.Add([System.Net.Http.StringContent]::new('verbose_json'), 'response_format')
            $content.Add([System.Net.Http.StringContent]::new('en'), 'language')
            $response = $client.PostAsync($uri, $content).GetAwaiter().GetResult()
            $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $statusCode = [int]$response.StatusCode
            if (-not $response.IsSuccessStatusCode) {
                return [pscustomobject]@{
                    Ok         = $false
                    StatusCode = $statusCode
                    Body       = $body
                    Reason     = "http-$statusCode"
                }
            }
        } finally {
            $client.Dispose()
        }
    }

    $schema = Test-InferenceResponseBody -Body $body
    return [pscustomobject]@{
        Ok         = [bool]$schema.Ok
        StatusCode = $statusCode
        Body       = $body
        Reason     = [string]$schema.Reason
    }
}

function Wait-SpeechReady {
    param(
        [int]$ProcessId,
        [string]$BaseUrl,
        [string]$WavPath,
        [int]$DeadlineSeconds,
        [int]$ProbeTimeoutSeconds
    )
    $readinessClock = [Diagnostics.Stopwatch]::StartNew()
    $lastError = 'not-started'
    while ($readinessClock.Elapsed.TotalSeconds -lt $DeadlineSeconds) {
        $alive = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
        if (-not $alive) {
            throw "Whisper process $ProcessId exited before readiness. Inspect logs under .appdata/local/speech/logs/."
        }
        $remainingSeconds = $DeadlineSeconds - $readinessClock.Elapsed.TotalSeconds
        if ($remainingSeconds -le 0.01) { break }
        try {
            $probeBudget = [Math]::Min($ProbeTimeoutSeconds, $remainingSeconds)
            $probe = Invoke-InferenceProbe -BaseUrl $BaseUrl -WavPath $WavPath -TimeoutSeconds $probeBudget
            if ($probe.Ok -and $readinessClock.Elapsed.TotalSeconds -lt $DeadlineSeconds) { return $probe }
            $lastError = "HTTP $($probe.StatusCode) reason=$($probe.Reason)"
        } catch {
            $lastError = $_.Exception.Message
        }
        $remainingMs = [Math]::Floor(($DeadlineSeconds - $readinessClock.Elapsed.TotalSeconds) * 1000)
        if ($remainingMs -gt 0) {
            Start-Sleep -Milliseconds ([int][Math]::Min(500, $remainingMs))
        }
    }
    throw "Whisper readiness timed out after ${DeadlineSeconds}s (last=$lastError). Not marking ready."
}

function Invoke-SpeechSetup {
    param($Root, $Paths, $Lock, [switch]$OfflineMode, [switch]$SkipLegacy)
    Ensure-SpeechDirectories -Paths $Paths
    Assert-FreeDiskBytes -Path $Paths.SpeechRoot -RequiredBytes (220L * 1024L * 1024L)

    $zipSha = $Lock.serverZip.sha256.ToLowerInvariant()
    $modelSha = $Lock.model.sha256.ToLowerInvariant()
    $legacyCandidates = if ($SkipLegacy) { @() } else { @($Lock.legacyCacheCandidates) }

    $haveZip = $false
    if (Test-Path -LiteralPath $Paths.ZipPath) {
        if ((Get-Sha256Lower $Paths.ZipPath) -eq $zipSha) { $haveZip = $true }
    }
    if (-not $haveZip) {
        foreach ($candidate in $legacyCandidates) {
            $full = Join-Path $Root $candidate
            if ($candidate -like '*.zip') {
                if (Copy-IfHashMatches -Source $full -Destination $Paths.ZipPath -ExpectedSha $zipSha) {
                    Write-Host "Reused cache zip: $candidate"
                    $haveZip = $true
                    break
                }
            }
        }
    }
    if (-not $haveZip) {
        Invoke-DownloadPinned -Url $Lock.serverZip.url -Destination $Paths.ZipPath -ExpectedSha $zipSha -OfflineMode:$OfflineMode
    }

    $haveModel = $false
    if (Test-Path -LiteralPath $Paths.ModelPath) {
        if ((Get-Sha256Lower $Paths.ModelPath) -eq $modelSha) { $haveModel = $true }
    }
    if (-not $haveModel) {
        foreach ($candidate in $legacyCandidates) {
            $full = Join-Path $Root $candidate
            if ($candidate -like '*.bin') {
                if (Copy-IfHashMatches -Source $full -Destination $Paths.ModelPath -ExpectedSha $modelSha) {
                    Write-Host "Reused cache model: $candidate"
                    $haveModel = $true
                    break
                }
            }
        }
    }
    if (-not $haveModel) {
        Invoke-DownloadPinned -Url $Lock.model.url -Destination $Paths.ModelPath -ExpectedSha $modelSha -OfflineMode:$OfflineMode
    }

    $exePath = Join-Path $Paths.BinDir $Lock.executable.relativePath
    $needExtract = $true
    if (Test-Path -LiteralPath $exePath) {
        try {
            Assert-InstalledArtifacts -Paths $Paths -Lock $Lock -ExecutableOverride '' -ModelOverride '' | Out-Null
            $needExtract = $false
            Write-Host 'Installed whisper binaries already match lock; extract skipped.'
        } catch {
            $needExtract = $true
        }
    }

    if ($needExtract) {
        # Prefer copying a verified legacy Release folder when hashes match, else extract zip.
        $copiedLegacy = $false
        foreach ($candidate in $legacyCandidates) {
            if ($candidate -notlike '*Release') { continue }
            $legacyExe = Join-Path (Join-Path $Root $candidate) 'whisper-server.exe'
            if (-not (Test-Path -LiteralPath $legacyExe)) { continue }
            if ((Get-Sha256Lower $legacyExe) -ne $Lock.executable.sha256.ToLowerInvariant()) { continue }
            if (Test-Path -LiteralPath $Paths.BinDir) {
                Remove-Item -LiteralPath $Paths.BinDir -Recurse -Force
            }
            New-Item -ItemType Directory -Path (Join-Path $Paths.BinDir 'Release') -Force | Out-Null
            $legacyDir = Join-Path $Root $candidate
            Copy-Item -Path (Join-Path $legacyDir '*') -Destination (Join-Path $Paths.BinDir 'Release') -Recurse -Force
            try {
                Assert-InstalledArtifacts -Paths $Paths -Lock $Lock -ExecutableOverride '' -ModelOverride '' | Out-Null
                Write-Host "Reused verified legacy bin folder: $candidate"
                $copiedLegacy = $true
                break
            } catch {
                Remove-Item -LiteralPath $Paths.BinDir -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        if (-not $copiedLegacy) {
            Expand-PinnedZipSafe -ZipPath $Paths.ZipPath -DestinationDir $Paths.BinDir -ExpectedExeRelative $Lock.executable.relativePath
            Assert-InstalledArtifacts -Paths $Paths -Lock $Lock -ExecutableOverride '' -ModelOverride '' | Out-Null
            Write-Host 'Extracted and verified whisper-bin-x64.zip'
        }
    }

    if (-not (Test-Path -LiteralPath $Paths.SilenceWav)) {
        New-SilenceWav -Path $Paths.SilenceWav -Seconds 1
    }

    Write-Host "Speech setup ready under $($Paths.SpeechRoot)"
}

function Start-SpeechServer {
    param(
        $Root,
        $Paths,
        $Lock,
        [int]$PortNumber,
        [int]$ThreadCount,
        [string]$ExecutableOverride,
        [string]$ModelOverride,
        [string]$RuntimeFileName,
        [string[]]$RuntimePrefixArgs,
        [int]$DeadlineSeconds
    )
    Ensure-SpeechDirectories -Paths $Paths
    $installed = Assert-InstalledArtifacts -Paths $Paths -Lock $Lock -ExecutableOverride $ExecutableOverride -ModelOverride $ModelOverride
    if (-not (Test-Path -LiteralPath $Paths.SilenceWav)) {
        New-SilenceWav -Path $Paths.SilenceWav -Seconds 1
    }

    $hostName = $Lock.runtimeDefaults.host
    $baseUrl = "http://${hostName}:$PortNumber"
    $modelIdentity = $Lock.runtimeDefaults.modelIdentity
    $probeTimeout = [int]$Lock.runtimeDefaults.inferenceProbeTimeoutSeconds
    if ($DeadlineSeconds -le 0) { $DeadlineSeconds = [int]$Lock.runtimeDefaults.readinessDeadlineSeconds }
    $expectedRuntime = Resolve-ExpectedRuntimePath -InstalledExecutablePath $installed.ExecutablePath -RuntimeFileName $RuntimeFileName

    $state = Read-SpeechState -StatePath $Paths.StatePath
    $ownedNow = Test-OwnedSpeechProcess -State $state -ExpectedExePath $installed.ExecutablePath -ExpectedRuntimePath $expectedRuntime -ExpectedExeHash $installed.ExecutableHash -ExpectedModelHash $installed.ModelHash -ExpectedPort $PortNumber -CheckoutRoot $Root
    if ($ownedNow) {
        $probe = Invoke-InferenceProbe -BaseUrl $baseUrl -WavPath $Paths.SilenceWav -TimeoutSeconds $probeTimeout
        if ($probe.Ok) {
            Write-Host "Reusing healthy owned Whisper PID $($state.pid) on $baseUrl"
            return [pscustomobject]@{
                Status         = 'ready'
                Reused         = $true
                Pid            = [int]$state.pid
                BaseUrl        = $baseUrl
                ModelIdentity  = $modelIdentity
                Port           = $PortNumber
                WorkingSetMiB  = [math]::Round(((Get-OwnedProcessIdentity -ProcessId ([int]$state.pid)).WorkingSet / 1MB), 1)
            }
        }
        throw "Owned Whisper PID $($state.pid) failed inference readiness (reason=$($probe.Reason)). Stop it explicitly before starting another; refusing to spawn a duplicate on the same ownership/port."
    }

    $listenerPid = Get-ListenerOwner -HostName $hostName -PortNumber $PortNumber
    if ($null -ne $listenerPid) {
        throw "Port $PortNumber is already in use by a process that is not an owned Whisper for this checkout. Refusing to kill or reuse it. Choose another port or stop the owner explicitly."
    }

    if ($state) {
        Write-Host 'Clearing stale speech state (PID/identity no longer owned).'
        Clear-SpeechState -StatePath $Paths.StatePath
    }

    $runId = [guid]::NewGuid().ToString('N')
    $argList = @(
        '--host', $hostName,
        '--port', "$PortNumber",
        '-m', $installed.ModelPath,
        '-l', $Lock.runtimeDefaults.language,
        '-t', "$ThreadCount",
        '-ng'
    )

    $runtimePath = if ($RuntimeFileName) { $RuntimeFileName } else { $installed.ExecutablePath }
    # Build argv with List[string] — never use string + array (PowerShell glues tokens).
    $launchArgList = New-Object System.Collections.Generic.List[string]
    foreach ($prefix in @($RuntimePrefixArgs)) {
        if (-not [string]::IsNullOrWhiteSpace([string]$prefix)) {
            [void]$launchArgList.Add([string]$prefix)
        }
    }
    foreach ($arg in $argList) {
        [void]$launchArgList.Add([string]$arg)
    }
    $workDir = if ($RuntimeFileName) {
        Split-Path -Parent $RuntimeFileName
    } else {
        Split-Path -Parent $installed.ExecutablePath
    }
    if (-not $workDir) { $workDir = $Paths.SpeechRoot }

    # Truncate logs for this run (no learner audio stored by default).
    Set-Content -LiteralPath $Paths.OutLog -Value "runId=$runId" -Encoding utf8NoBOM
    Set-Content -LiteralPath $Paths.ErrLog -Value "runId=$runId" -Encoding utf8NoBOM

    # Hidden loopback process; must remain alive after this CLI returns.
    # ProcessStartInfo.ArgumentList keeps each argv distinct (unlike Start-Process
    # -ArgumentList string/array quirks that can glue script.py--host).
    # Launch via Win32_Process.Create so the child does NOT inherit the caller's
    # redirected stdout/stderr pipes. Process.Start with bInheritHandles=true lets
    # a grandchild keep Python subprocess pipes open forever (CLI never "exits").
    $quotedArgs = foreach ($arg in $launchArgList) {
        $token = [string]$arg
        if ($token -match '[\s"]') {
            '"' + ($token.Replace('"', '""')) + '"'
        } else {
            $token
        }
    }
    $commandLine = ('"{0}" {1}' -f $runtimePath, ($quotedArgs -join ' ')).Trim()
    $create = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{
        CommandLine      = $commandLine
        CurrentDirectory = $workDir
    }
    if ($null -eq $create -or [int]$create.ReturnValue -ne 0 -or [int]$create.ProcessId -le 0) {
        throw "Failed to start whisper-server via WMI (ReturnValue=$($create.ReturnValue))."
    }
    $pidValue = [int]$create.ProcessId
    Start-Sleep -Milliseconds 300
    $startedProc = Get-Process -Id $pidValue -ErrorAction SilentlyContinue
    if (-not $startedProc) {
        throw "Whisper process $pidValue exited immediately after WMI Create."
    }
    $startedLocal = $startedProc.StartTime
    Add-Content -LiteralPath $Paths.OutLog -Value "startedPid=$pidValue runtime=$runtimePath cmdline=$commandLine" -Encoding utf8

    $pendingState = [ordered]@{
        status          = 'starting'
        runId           = $runId
        pid             = $pidValue
        startTimeLocal  = $startedLocal.ToString('o')
        startTimeUtc    = $startedLocal.ToUniversalTime().ToString('o')
        checkoutRoot    = $Root
        host            = $hostName
        port            = $PortNumber
        baseUrl         = $baseUrl
        executablePath  = $installed.ExecutablePath
        executableHash  = $installed.ExecutableHash
        runtimeFileName = $runtimePath
        modelPath       = $installed.ModelPath
        modelHash       = $installed.ModelHash
        modelIdentity   = $modelIdentity
        threads         = $ThreadCount
        commandLine     = ("{0} {1}" -f $runtimePath, ($launchArgList -join ' '))
        ownershipMarker = "tct-local-speech:$runId"
    }
    Write-SpeechState -StatePath $Paths.StatePath -StateObject $pendingState

    try {
        $null = Wait-SpeechReady -ProcessId $pidValue -BaseUrl $baseUrl -WavPath $Paths.SilenceWav -DeadlineSeconds $DeadlineSeconds -ProbeTimeoutSeconds $probeTimeout
    } catch {
        # Bounded cleanup only if the PID still matches the runtime we just started.
        $null = Stop-ProcessIfStillOwned -ProcessId $pidValue -ExpectedStartTime $startedLocal -ExpectedRuntimePath $expectedRuntime
        Clear-SpeechState -StatePath $Paths.StatePath
        throw
    }

    $pendingState.status = 'ready'
    Write-SpeechState -StatePath $Paths.StatePath -StateObject $pendingState
    $identity = Get-OwnedProcessIdentity -ProcessId $pidValue
    Write-Host "Whisper ready PID $pidValue at $baseUrl (WS≈$([math]::Round($identity.WorkingSet/1MB,1)) MiB)"
    return [pscustomobject]@{
        Status        = 'ready'
        Reused        = $false
        Pid           = $pidValue
        BaseUrl       = $baseUrl
        ModelIdentity = $modelIdentity
        Port          = $PortNumber
        WorkingSetMiB = [math]::Round(($identity.WorkingSet / 1MB), 1)
    }
}

function Get-SpeechStatus {
    param($Root, $Paths, $Lock, [int]$PortNumber, [string]$ExecutableOverride, [string]$ModelOverride, [string]$RuntimeFileName)
    $installed = $null
    try {
        $installed = Assert-InstalledArtifacts -Paths $Paths -Lock $Lock -ExecutableOverride $ExecutableOverride -ModelOverride $ModelOverride
    } catch {
        return [pscustomobject]@{
            Status  = 'absent'
            Detail  = $_.Exception.Message
            BaseUrl = $null
            Pid     = $null
        }
    }

    $expectedRuntime = Resolve-ExpectedRuntimePath -InstalledExecutablePath $installed.ExecutablePath -RuntimeFileName $RuntimeFileName
    $state = Read-SpeechState -StatePath $Paths.StatePath
    $owned = Test-OwnedSpeechProcess -State $state -ExpectedExePath $installed.ExecutablePath -ExpectedRuntimePath $expectedRuntime -ExpectedExeHash $installed.ExecutableHash -ExpectedModelHash $installed.ModelHash -ExpectedPort $PortNumber -CheckoutRoot $Root
    $baseUrl = "http://$($Lock.runtimeDefaults.host):$PortNumber"
    if (-not $owned) {
        $listenerPid = Get-ListenerOwner -HostName $Lock.runtimeDefaults.host -PortNumber $PortNumber
        return [pscustomobject]@{
            Status      = if ($listenerPid) { 'foreign-listener' } else { 'stopped' }
            Detail      = if ($listenerPid) { "Port $PortNumber held by PID $listenerPid without ownership match." } else { 'No owned Whisper process.' }
            BaseUrl     = $baseUrl
            Pid         = $listenerPid
            ModelIdentity = $Lock.runtimeDefaults.modelIdentity
        }
    }

    $healthy = $false
    $reason = 'unprobed'
    try {
        if (-not (Test-Path -LiteralPath $Paths.SilenceWav)) { New-SilenceWav -Path $Paths.SilenceWav -Seconds 1 }
        $probe = Invoke-InferenceProbe -BaseUrl $baseUrl -WavPath $Paths.SilenceWav -TimeoutSeconds ([int]$Lock.runtimeDefaults.inferenceProbeTimeoutSeconds)
        $healthy = [bool]$probe.Ok
        $reason = [string]$probe.Reason
    } catch {
        $healthy = $false
        $reason = $_.Exception.Message
    }

    $identity = Get-OwnedProcessIdentity -ProcessId ([int]$state.pid)
    return [pscustomobject]@{
        Status          = if ($healthy) { 'ready' } else { 'failed' }
        Detail          = if ($healthy) { 'Owned process passed inference probe.' } else { "Owned process failed inference probe ($reason)." }
        BaseUrl         = $baseUrl
        Pid             = [int]$state.pid
        ModelIdentity   = $state.modelIdentity
        WorkingSetMiB   = if ($identity) { [math]::Round(($identity.WorkingSet / 1MB), 1) } else { $null }
        RunId           = $state.runId
    }
}

function Stop-SpeechServer {
    param($Root, $Paths, $Lock, [int]$PortNumber, [string]$ExecutableOverride, [string]$ModelOverride, [string]$RuntimeFileName)
    $installed = Assert-InstalledArtifacts -Paths $Paths -Lock $Lock -ExecutableOverride $ExecutableOverride -ModelOverride $ModelOverride
    $expectedRuntime = Resolve-ExpectedRuntimePath -InstalledExecutablePath $installed.ExecutablePath -RuntimeFileName $RuntimeFileName
    $state = Read-SpeechState -StatePath $Paths.StatePath
    if ($null -eq $state) {
        Write-Host 'No speech state file; nothing to stop.'
        return [pscustomobject]@{ Status = 'stopped'; Stopped = $false }
    }
    if (-not (Test-OwnedSpeechProcess -State $state -ExpectedExePath $installed.ExecutablePath -ExpectedRuntimePath $expectedRuntime -ExpectedExeHash $installed.ExecutableHash -ExpectedModelHash $installed.ModelHash -ExpectedPort $PortNumber -CheckoutRoot $Root)) {
        throw 'Refusing Stop: current process identity does not match ownership markers for this checkout (PID/start time/path/hash/port).'
    }
    $expectedStart = [DateTime]::Parse($state.startTimeLocal, $null, [System.Globalization.DateTimeStyles]::RoundtripKind)
    $targetPid = [int]$state.pid
    # Re-check identity immediately before kill (PID reuse race).
    if (-not (Stop-ProcessIfStillOwned -ProcessId $targetPid -ExpectedStartTime $expectedStart -ExpectedRuntimePath $expectedRuntime)) {
        $alive = Get-Process -Id $targetPid -ErrorAction SilentlyContinue
        Clear-SpeechState -StatePath $Paths.StatePath
        if (-not $alive) {
            Write-Host "Owned Whisper PID $targetPid already exited; cleared state."
            return [pscustomobject]@{ Status = 'stopped'; Stopped = $false; Pid = $targetPid }
        }
        throw 'Refusing Stop: process identity changed after ownership check (possible PID reuse).'
    }
    Start-Sleep -Milliseconds 300
    Clear-SpeechState -StatePath $Paths.StatePath
    Write-Host "Stopped owned Whisper PID $targetPid"
    return [pscustomobject]@{ Status = 'stopped'; Stopped = $true; Pid = $targetPid }
}

function Invoke-SpeechBenchmark {
    param($Root, $Paths, $Lock, [int]$PortNumber, [int]$Seconds, [string]$WavPath, [string]$ExecutableOverride, [string]$ModelOverride, [string]$RuntimeFileName)
    $status = Get-SpeechStatus -Root $Root -Paths $Paths -Lock $Lock -PortNumber $PortNumber -ExecutableOverride $ExecutableOverride -ModelOverride $ModelOverride -RuntimeFileName $RuntimeFileName
    if ($status.Status -ne 'ready') {
        throw "Benchmark requires ready Whisper; status=$($status.Status) detail=$($status.Detail)"
    }
    if (-not $WavPath) {
        $WavPath = Join-Path $Paths.Probes ("bench-{0}s.wav" -f $Seconds)
        New-SilenceWav -Path $WavPath -Seconds $Seconds
        Write-Host "WARNING: silence-only WAV used for $Seconds s; this does not prove speech accuracy."
    }
    if (-not (Test-Path -LiteralPath $WavPath)) {
        throw "Benchmark WAV missing: $WavPath"
    }

    $samples = @()
    $baseUrl = $status.BaseUrl
    $peakWs = 0L
    foreach ($i in 1..3) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $probe = Invoke-InferenceProbe -BaseUrl $baseUrl -WavPath $WavPath -TimeoutSeconds 60
        $sw.Stop()
        if (-not $probe.Ok) { throw "Benchmark probe failed HTTP $($probe.StatusCode) reason=$($probe.Reason)" }
        $samples += [math]::Round($sw.Elapsed.TotalSeconds, 3)
        $identity = Get-OwnedProcessIdentity -ProcessId ([int]$status.Pid)
        if ($identity) {
            $proc = Get-Process -Id ([int]$status.Pid) -ErrorAction SilentlyContinue
            $samplePeak = if ($proc) { [int64]$proc.PeakWorkingSet64 } else { [int64]$identity.WorkingSet }
            if ($samplePeak -gt $peakWs) { $peakWs = $samplePeak }
        }
    }
    $ram = Get-CimInstance Win32_OperatingSystem
    $sorted = @($samples | Sort-Object)
    $p95Index = [math]::Min($sorted.Count - 1, [math]::Ceiling($sorted.Count * 0.95) - 1)
    if ($p95Index -lt 0) { $p95Index = 0 }
    return [pscustomobject]@{
        ClipSeconds          = $Seconds
        SamplesSeconds       = $samples
        P50Seconds           = $sorted[[math]::Floor(($sorted.Count - 1) * 0.5)]
        P95Seconds           = $sorted[$p95Index]
        PeakWorkingSetMiB    = [math]::Round(($peakWs / 1MB), 1)
        WorkingSetSampleMiB  = [math]::Round(((Get-OwnedProcessIdentity -ProcessId ([int]$status.Pid)).WorkingSet / 1MB), 1)
        FreeRamGiB           = [math]::Round(($ram.FreePhysicalMemory / 1MB), 1)
        Note                 = if ($WavPath -like '*bench-*s.wav') { 'silence-only; not speech accuracy proof' } else { 'custom wav' }
        LogNote              = 'WMI Create does not capture engine stderr; OutLog holds launcher markers (runId/pid/cmdline) only.'
    }
}

# --- main ---
$resolvedRoot = Get-RepoRoot -Explicit $RepoRoot
$lockMeta = Read-ArtifactLock -Root $resolvedRoot
$paths = New-SpeechPaths -Root $resolvedRoot -SpeechRootOverride $SpeechRoot
if ($LockPath) { $paths.LockFilePath = $LockPath }

$portNumber = if ($Port -gt 0) { $Port } else { [int]$lockMeta.runtimeDefaults.port }
$threadCount = if ($Threads -gt 0) { $Threads } else { [int]$lockMeta.runtimeDefaults.threads }
$deadline = if ($ReadinessDeadlineSeconds -gt 0) { $ReadinessDeadlineSeconds } else { [int]$lockMeta.runtimeDefaults.readinessDeadlineSeconds }

$lockHandle = $null
try {
    if ($Action -in @('Setup', 'Start', 'Stop', 'Verify')) {
        $lockHandle = Enter-SpeechLock -LockFilePath $paths.LockFilePath
    }

    switch ($Action) {
        'Setup' {
            Invoke-SpeechSetup -Root $resolvedRoot -Paths $paths -Lock $lockMeta -OfflineMode:$Offline -SkipLegacy:$SkipLegacyCache
            [pscustomobject]@{ Status = 'setup-ready'; SpeechRoot = $paths.SpeechRoot } | ConvertTo-Json -Compress
        }
        'Verify' {
            $installed = Assert-InstalledArtifacts -Paths $paths -Lock $lockMeta -ExecutableOverride $ExecutableOverride -ModelOverride $ModelOverride
            [pscustomobject]@{
                Status           = 'verified'
                ExecutablePath   = $installed.ExecutablePath
                ModelPath        = $installed.ModelPath
                ExecutableHash   = $installed.ExecutableHash
                ModelHash        = $installed.ModelHash
            } | ConvertTo-Json -Compress
        }
        'Start' {
            if (-not $ExecutableOverride) {
                # Ensure artifacts exist (reuse/download) before start.
                Invoke-SpeechSetup -Root $resolvedRoot -Paths $paths -Lock $lockMeta -OfflineMode:$Offline -SkipLegacy:$SkipLegacyCache
            }
            $result = Start-SpeechServer -Root $resolvedRoot -Paths $paths -Lock $lockMeta -PortNumber $portNumber -ThreadCount $threadCount -ExecutableOverride $ExecutableOverride -ModelOverride $ModelOverride -RuntimeFileName $RuntimeFileName -RuntimePrefixArgs $RuntimePrefixArgs -DeadlineSeconds $deadline
            $result | ConvertTo-Json -Compress
        }
        'Status' {
            $result = Get-SpeechStatus -Root $resolvedRoot -Paths $paths -Lock $lockMeta -PortNumber $portNumber -ExecutableOverride $ExecutableOverride -ModelOverride $ModelOverride -RuntimeFileName $RuntimeFileName
            $result | ConvertTo-Json -Compress
        }
        'Stop' {
            $result = Stop-SpeechServer -Root $resolvedRoot -Paths $paths -Lock $lockMeta -PortNumber $portNumber -ExecutableOverride $ExecutableOverride -ModelOverride $ModelOverride -RuntimeFileName $RuntimeFileName
            $result | ConvertTo-Json -Compress
        }
        'Benchmark' {
            $result = Invoke-SpeechBenchmark -Root $resolvedRoot -Paths $paths -Lock $lockMeta -PortNumber $portNumber -Seconds $BenchmarkSeconds -WavPath $BenchmarkWav -ExecutableOverride $ExecutableOverride -ModelOverride $ModelOverride -RuntimeFileName $RuntimeFileName
            $result | ConvertTo-Json -Compress
        }
        'ValidateProbeBody' {
            if (-not $ProbeBodyFile) {
                throw 'ValidateProbeBody requires -ProbeBodyFile'
            }
            $body = Get-Content -LiteralPath $ProbeBodyFile -Raw -Encoding utf8
            Test-InferenceResponseBody -Body $body | ConvertTo-Json -Compress
        }
    }
} finally {
    Exit-SpeechLock -LockHandle $lockHandle
}
