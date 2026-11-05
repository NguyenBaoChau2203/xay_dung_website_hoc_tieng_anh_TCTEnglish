#requires -Version 7.0
[CmdletBinding()]
param([switch]$EnableDeployment)
$ErrorActionPreference = 'Stop'
$repository = 'NguyenBaoChau2203/xay_dung_website_hoc_tieng_anh_TCTEnglish'
$privateDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) '.appdata/local/staging'
if ((& gh variable get PLESK_DEPLOYMENT_ENABLED --repo $repository) -cne 'false' -or $LASTEXITCODE -ne 0) { throw 'Verify the production gate is false before configuring staging.' }
try {
    $portal = Get-Content -Raw -LiteralPath (Join-Path $privateDirectory 'portal-access.json') | ConvertFrom-Json
    $hook = Get-Content -Raw -LiteralPath (Join-Path $privateDirectory 'webhook.json') | ConvertFrom-Json
} catch { throw 'Staging handoff could not be read; credential details omitted.' }
if ($hook.environment -cne 'Staging' -or -not ([string]$hook.url).StartsWith('https://peaceful-bartik.103-28-36-90.plesk.page:8443/modules/git/public/web-hook.php?') -or $portal.url -notmatch '^https://staging\.tctenglish\.site/?$') { throw 'Staging settings identity did not match.' }
$payloadPath = Join-Path $privateDirectory ('github-environment-' + [Guid]::NewGuid().ToString('N') + '.json')
try {
    @{ deployment_branch_policy=@{protected_branches=$false;custom_branch_policies=$true} } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $payloadPath -Encoding utf8NoBOM
    & gh api --method PUT "repos/$repository/environments/staging" --input $payloadPath > $null
    if ($LASTEXITCODE -ne 0) { throw 'Could not configure the staging environment.' }
    $policies = & gh api "repos/$repository/environments/staging/deployment-branch-policies" | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect staging branch policies.' }
    if (@($policies.branch_policies | Where-Object { $_.name -ne 'master' -or $_.type -ne 'branch' }).Count) { throw 'Unexpected existing staging policies; inspect before broadening access.' }
    if (-not @($policies.branch_policies | Where-Object { $_.name -eq 'master' -and $_.type -eq 'branch' }).Count) {
        @{name='master';type='branch'} | ConvertTo-Json | Set-Content -LiteralPath $payloadPath -Encoding utf8NoBOM
        & gh api --method POST "repos/$repository/environments/staging/deployment-branch-policies" --input $payloadPath > $null
        if ($LASTEXITCODE -ne 0) { throw 'Could not restrict staging to master.' }
    }
    foreach ($item in @(@('PLESK_STAGING_WEBHOOK_URL',[string]$hook.url),@('STAGING_PORTAL_USERNAME',[string]$portal.username),@('STAGING_PORTAL_PASSWORD',[string]$portal.password))) {
        # Credentials go through stdin, never process arguments or console output.
        $item[1] | & gh secret set $item[0] --env staging --repo $repository
        if ($LASTEXITCODE -ne 0) { throw 'Could not save a staging environment secret.' }
    }
    $gate = if ($EnableDeployment) { 'true' } else { 'false' }
    & gh variable set PLESK_STAGING_DEPLOYMENT_ENABLED --repo $repository --body $gate
    if ($LASTEXITCODE -ne 0) { throw 'Could not configure the staging gate.' }
    if ((& gh variable get PLESK_DEPLOYMENT_ENABLED --repo $repository) -cne 'false' -or $LASTEXITCODE -ne 0) { throw 'Production gate changed outside this operation; inspect before proceeding.' }
    Write-Host "Staging environment/secrets configured; staging gate=$gate. Production gate remains false."
} finally {
    if (Test-Path -LiteralPath $payloadPath) { Remove-Item -LiteralPath $payloadPath }
}
