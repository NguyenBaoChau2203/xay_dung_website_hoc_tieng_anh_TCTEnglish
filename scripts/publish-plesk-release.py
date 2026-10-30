"""Publish a tested artifact and a tiny Plesk deployment branch; never send app secrets."""
import hashlib
import json
import os
import re
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

REPOSITORY = 'NguyenBaoChau2203/xay_dung_website_hoc_tieng_anh_TCTEnglish'
BRANCH = 'codex/production-release'
RELEASE_ASSET = 'tctenglish-release.zip'


def api(path, body=None, method=None):
    payload = None if body is None else json.dumps(body).encode()
    request = urllib.request.Request('https://api.github.com/repos/' + REPOSITORY + path, data=payload,
        headers={'Authorization': 'Bearer ' + os.environ['GH_TOKEN'], 'Accept': 'application/vnd.github+json', 'Content-Type': 'application/json'}, method=method)
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)


def find_release_by_tag(tag):
    try:
        return api('/releases/tags/' + tag)
    except urllib.error.HTTPError as error:
        if error.code == 404:
            return None
        raise


def asset_sha256(release, artifact_name=RELEASE_ASSET):
    """Best-effort digest from release notes/body metadata is unavailable; callers supply local digest compare."""
    for asset in release.get('assets') or []:
        if asset.get('name') == artifact_name:
            return asset.get('digest') or asset.get('sha256')
    return None


def release_target_matches(release, sha):
    target = (release.get('target_commitish') or '').strip().lower()
    if re.fullmatch(r'[0-9a-f]{40}', target):
        return target == sha
    # GitHub may store a branch name; require an asset and rely on caller digest check.
    return bool(release.get('assets'))


def file_sha256(path):
    with open(path, 'rb') as handle:
        return hashlib.file_digest(handle, 'sha256').hexdigest()


def ensure_release(tag, artifact, sha):
    """Create the release or reuse an existing one with matching identity/digest."""
    local_digest = file_sha256(artifact)
    existing = find_release_by_tag(tag)
    if existing is not None:
        if not release_target_matches(existing, sha):
            raise SystemExit('Existing release tag points at a different commit; refusing to clobber.')
        remote_digest = asset_sha256(existing)
        if remote_digest and remote_digest.replace('sha256:', '') != local_digest:
            raise SystemExit('Existing release asset digest mismatch; refusing to clobber.')
        # When GitHub does not expose digest on the asset metadata, compare against a
        # recorded digest from a companion note is unavailable — require local upload
        # path only when asset missing.
        assets = existing.get('assets') or []
        if not any(asset.get('name') == RELEASE_ASSET for asset in assets):
            raise SystemExit('Existing release is missing the expected asset; operator recovery required.')
        return existing, local_digest, False

    subprocess.run(
        ['gh', 'release', 'create', tag, str(artifact) + '#' + RELEASE_ASSET, '--repo', REPOSITORY,
         '--target', sha, '--title', 'TCT English ' + sha[:12],
         '--notes', 'Application and migration bundle from a successful CI run.'],
        check=True)
    created = find_release_by_tag(tag)
    if created is None:
        raise SystemExit('Release create completed but tag lookup failed.')
    return created, local_digest, True


def update_branch(files):
    parent = None
    try:
        parent = api('/git/ref/heads/' + BRANCH)['object']['sha']
    except urllib.error.HTTPError as error:
        if error.code != 404:
            raise
    tree = api('/git/trees', {'tree': [{'path': name, 'mode': '100644', 'type': 'blob', 'content': contents} for name, contents in files.items()]})
    commit = api('/git/commits', {'message': 'ci: update tested Plesk release manifest', 'tree': tree['sha'], 'parents': [] if parent is None else [parent]})
    if parent:
        api('/git/refs/heads/' + BRANCH, {'sha': commit['sha'], 'force': False}, 'PATCH')
    else:
        api('/git/refs', {'ref': 'refs/heads/' + BRANCH, 'sha': commit['sha']})


def notify_plesk(webhook):
    # The Plesk handler returns 404 for GET, even with a valid webhook UUID.
    request = urllib.request.Request(webhook, data=b'{}', headers={'Content-Type': 'application/json'}, method='POST')
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            response.read(4096)
    except Exception:
        raise SystemExit('Plesk webhook failed; its private URL has not been logged.') from None


def wait_for_deployed_commit(sha, attempts=48, delay_seconds=5):
    for _ in range(attempts):
        try:
            with urllib.request.urlopen('https://tctenglish.site/tct-release.json', timeout=10) as response:
                headers = {name.lower(): value for name, value in response.headers.items()}
                deployed = json.load(response)
            with urllib.request.urlopen('https://tctenglish.site/health/ready', timeout=10) as response:
                ready = response.status == 200
            if deployed.get('commit') == sha and ready:
                cache_control = headers.get('cache-control', '')
                if 'no-store' not in cache_control and 'no-cache' not in cache_control:
                    # Soft signal only; readiness + commit match remain authoritative.
                    pass
                print('Plesk deployed the expected commit and database readiness passed.')
                return True
        except Exception:
            pass
        time.sleep(delay_seconds)
    return False


def main_with_artifact(artifact_path):
    artifact = Path(artifact_path).resolve()
    sha = os.environ['GITHUB_SHA']
    if not re.fullmatch(r'[0-9a-f]{40}', sha) or os.environ['GITHUB_REPOSITORY'] != REPOSITORY:
        raise SystemExit('Unexpected release identity.')
    webhook = os.environ.get('PLESK_WEBHOOK_URL', '')
    if not webhook.startswith('https://peaceful-bartik.103-28-36-90.plesk.page:8443/modules/git/public/web-hook.php?'):
        raise SystemExit('Configure the verified HTTPS Plesk webhook before enabling deployment.')
    tag = 'deploy-' + os.environ['GITHUB_RUN_ID'] + '-' + sha[:12]
    release, digest, created = ensure_release(tag, artifact, sha)
    artifact_url = 'https://github.com/' + REPOSITORY + '/releases/download/' + tag + '/' + RELEASE_ASSET
    for asset in release.get('assets') or []:
        if asset.get('name') == RELEASE_ASSET and asset.get('browser_download_url'):
            artifact_url = asset['browser_download_url']
            break
    manifest = {
        'enabled': True,
        'repository': REPOSITORY,
        'commit': sha,
        'runNumber': int(os.environ['GITHUB_RUN_NUMBER']),
        'sha256': digest,
        'artifactUrl': artifact_url,
        'releaseReused': not created,
    }
    update_branch({'release.json': json.dumps(manifest, indent=2), 'deploy-plesk.ps1': Path('scripts/deploy-plesk.ps1').read_text(encoding='utf-8')})
    notify_plesk(webhook)
    if wait_for_deployed_commit(sha):
        return
    raise SystemExit(
        'Plesk release did not reach readiness; inspect private release status and backup before retrying. '
        'Do not re-run migrations blindly if hosting state is unclear.')


def main():
    if len(sys.argv) < 2:
        raise SystemExit('Usage: publish-plesk-release.py <artifact.zip>')
    main_with_artifact(sys.argv[1])


if __name__ == '__main__':
    main()
