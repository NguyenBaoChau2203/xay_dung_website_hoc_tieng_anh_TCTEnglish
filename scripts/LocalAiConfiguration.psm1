#requires -Version 7.0
Set-StrictMode -Version Latest

function Get-LocalUserSecretsPath {
    param([Parameter(Mandatory)][string]$ProjectPath)
    [xml]$project = Get-Content -Raw -LiteralPath $ProjectPath
    $ids = @($project.SelectNodes('//UserSecretsId') | ForEach-Object { $_.InnerText })
    if ($ids.Count -ne 1 -or $ids[0] -notmatch '^[A-Za-z0-9._-]+$') { throw 'Expected one safe UserSecretsId in the application project.' }
    $root = if ($IsWindows) { Join-Path $env:APPDATA 'Microsoft/UserSecrets' } else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.microsoft/usersecrets' }
    return Join-Path (Join-Path $root $ids[0]) 'secrets.json'
}

function Read-LocalSecretStore {
    param([Parameter(Mandatory)][string]$SecretPath)
    $values = [hashtable]::new([StringComparer]::OrdinalIgnoreCase)
    if (-not (Test-Path -LiteralPath $SecretPath)) { return ,$values }
    try { $raw = Get-Content -LiteralPath $SecretPath -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable }
    catch { throw 'Invalid User Secrets JSON. File preserved; repair it locally without sharing its contents.' }
    if ($raw -isnot [System.Collections.IDictionary]) { throw 'User Secrets must contain a JSON object.' }
    function Add-FlattenedValue($node, [string]$prefix) {
        if ($node -is [System.Collections.IDictionary]) {
            foreach ($key in $node.Keys) { Add-FlattenedValue $node[$key] $(if ($prefix) { "${prefix}:$key" } else { [string]$key }) }
        } elseif ($node -is [array]) {
            for ($i = 0; $i -lt $node.Count; $i++) { Add-FlattenedValue $node[$i] "${prefix}:$i" }
        } else {
            if ($values.ContainsKey($prefix)) { throw "Conflicting nested/flat User Secrets setting: $prefix. File preserved." }
            $values[$prefix] = $node
        }
    }
    Add-FlattenedValue $raw ''
    return ,$values
}

function Update-LocalSecretStore {
    param([Parameter(Mandatory)][string]$SecretPath, [Parameter(Mandatory)][scriptblock]$Update)
    $directory = Split-Path -Parent ([IO.Path]::GetFullPath($SecretPath))
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $lock = $null
    $deadline = [Diagnostics.Stopwatch]::StartNew()
    while ($null -eq $lock) {
        try { $lock = [IO.File]::Open("$SecretPath.lock", 'OpenOrCreate', 'ReadWrite', 'None') }
        catch [IO.IOException] {
            if ($deadline.Elapsed.TotalSeconds -ge 5) { throw 'User Secrets is being updated by another process. Retry locally.' }
            Start-Sleep -Milliseconds 100
        }
    }
    $temporary = Join-Path $directory ('.tct-secrets-' + [guid]::NewGuid().ToString('N') + '.tmp')
    try {
        $values = Read-LocalSecretStore -SecretPath $SecretPath
        & $Update $values
        $json = ConvertTo-Json -InputObject $values -Depth 32
        [IO.File]::WriteAllText($temporary, $json, [Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temporary, $SecretPath, $true)
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
        $lock.Dispose()
    }
}

function Convert-LegacyAiSettings {
    param([Parameter(Mandatory)][hashtable]$Values)
    if ($Values.ContainsKey('AI:BaseUrl')) {
        $url = [string]$Values['AI:BaseUrl']
        if ($url.TrimEnd('/') -ceq 'https://generativelanguage.googleapis.com/v1beta') {
            $Values['AI:BaseUrl'] = 'https://api.openai.com/v1'
            Write-Host 'Normalized legacy AI:BaseUrl to the shared OpenAI default.'
        } elseif ($url) {
            $uri = $null
            if (-not [uri]::TryCreate($url, [UriKind]::Absolute, [ref]$uri) -or
                ($uri.Scheme -ne 'https' -and -not ($uri.Scheme -eq 'http' -and $uri.IsLoopback)) -or
                $uri.Host -eq 'generativelanguage.googleapis.com' -or $uri.Query -or $uri.Fragment -or $uri.UserInfo) {
                throw 'Unsupported AI:BaseUrl override. Remove or correct it locally; no credentials were printed.'
            }
            if ($uri.Host -ne 'api.openai.com') { Write-Host 'Preserving custom AI:BaseUrl; verify that this endpoint is trusted and supports OpenAI Responses.' }
        } else { $Values.Remove('AI:BaseUrl') }
    }
    if ($Values.ContainsKey('AI:Model')) {
        if ($Values['AI:Model'] -ceq 'gemini-2.5-flash-lite') {
            $Values['AI:Model'] = 'gpt-6-luna'
            Write-Host 'Normalized legacy AI:Model to gpt-6-luna.'
        } elseif (-not $Values['AI:Model']) { $Values.Remove('AI:Model') }
        elseif ($Values['AI:Model'] -cne 'gpt-6-luna') { throw 'AI:Model override must be gpt-6-luna. Store preserved.' }
    }
    if ($Values.ContainsKey('AI:ReasoningEffort') -and $Values['AI:ReasoningEffort'] -cne 'low') {
        throw 'AI:ReasoningEffort override must be low. Store preserved.'
    }
    $Values['AI:ReasoningEffort'] = 'low'
}

function Set-LocalAiKey {
    param([Parameter(Mandatory)][string]$SecretPath, [Parameter(Mandatory)][Security.SecureString]$SecureKey)
    $pointer = [IntPtr]::Zero
    try {
        $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureKey)
        $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
        if ([string]::IsNullOrWhiteSpace($plain) -or $plain -match '\s' -or $plain.StartsWith('AIza')) {
            throw 'Enter a nonempty OpenAI API key without whitespace. No key was written.'
        }
        Update-LocalSecretStore -SecretPath $SecretPath -Update {
            param($values)
            Convert-LegacyAiSettings $values
            $values['AI:ApiKey'] = $plain
        }
        Write-Host 'Saved AI:ApiKey in local .NET User Secrets. Account/model access and quota still require live verification.'
    } finally {
        if ($pointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
        $plain = $null
    }
}

function Assert-LocalAiEnvironment {
    # Environment wins over User Secrets in ASP.NET Core. Never silently use a different model/effort.
    $values = @{}
    foreach ($name in @('BaseUrl', 'Model', 'ReasoningEffort')) {
        $value = [Environment]::GetEnvironmentVariable("AI__$name")
        if ($null -ne $value) { $values["AI:$name"] = $value }
    }
    Convert-LegacyAiSettings $values
    $values.Remove('AI:ReasoningEffort') # Compare only explicitly supplied env values below.
    foreach ($name in @('BaseUrl', 'Model')) {
        $value = [Environment]::GetEnvironmentVariable("AI__$name")
        if ($null -ne $value -and $values["AI:$name"] -cne $value) {
            throw "Legacy AI__$name environment override detected. Remove it locally; source defaults already use OpenAI."
        }
    }
    if ($env:AI__ApiKey -and $env:AI__ApiKey.StartsWith('AIza')) { throw 'AI__ApiKey contains a legacy Google credential. Set an OpenAI key locally.' }
}

function Initialize-LocalAiEnvironment {
    param([Parameter(Mandatory)][string]$SecretPath)
    Assert-LocalAiEnvironment
    $values = Read-LocalSecretStore -SecretPath $SecretPath
    $key = [Environment]::GetEnvironmentVariable('AI__ApiKey', 'Process')
    $source = 'AI__ApiKey'
    if ($null -eq $key) {
        $key = [string]$values['AI:ApiKey']
        $source = 'User Secrets'
        if ([string]::IsNullOrWhiteSpace($key)) {
            $key = [Environment]::GetEnvironmentVariable('OPENAI_API_KEY', 'Process')
            $source = 'OPENAI_API_KEY'
        }
    }
    if ([string]::IsNullOrWhiteSpace($key)) {
        return [pscustomobject]@{ Configured = $false; Source = 'missing' }
    }
    if ($key -match '\s' -or $key.StartsWith('AIza', [StringComparison]::Ordinal)) {
        throw 'Local AI key is invalid (legacy Google credential or whitespace). Correct the selected local key; no credential was printed or persisted.'
    }
    if ($source -eq 'OPENAI_API_KEY') {
        # The owner explicitly opted into this source. Map only for this launcher and its children.
        [Environment]::SetEnvironmentVariable('AI__ApiKey', $key, 'Process')
    }
    return [pscustomobject]@{ Configured = $true; Source = $source }
}

Export-ModuleMember -Function Get-LocalUserSecretsPath, Read-LocalSecretStore, Update-LocalSecretStore, Convert-LegacyAiSettings, Set-LocalAiKey, Assert-LocalAiEnvironment, Initialize-LocalAiEnvironment
