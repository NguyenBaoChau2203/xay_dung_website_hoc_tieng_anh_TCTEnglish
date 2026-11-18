"""Publish only a tested master artifact to the isolated Plesk staging target."""
import base64
import importlib.util
import json
import os
from pathlib import Path
import re
import sys
import time
import urllib.error
import urllib.request

spec = importlib.util.spec_from_file_location('plesk_release_common', Path(__file__).with_name('publish-plesk-release.py'))
common = importlib.util.module_from_spec(spec)
spec.loader.exec_module(common)
common.BRANCH = 'release/staging'
STAGING_ORIGIN = 'https://staging.tctenglish.site'
RECOVERY_PLAN = Path(__file__).resolve().parents[1] / 'data/releases/staging-recovery.json'
WEBHOOK_PREFIX = 'https://peaceful-bartik.103-28-36-90.plesk.page:8443/modules/git/public/web-hook.php?'


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise urllib.error.HTTPError(req.full_url if req else STAGING_ORIGIN, code, 'Redirect refused', headers, fp)


def portal_json(relative, timeout=10):
    # Never follow a redirect with the portal Authorization header.
    credentials = (os.environ['STAGING_PORTAL_USERNAME'] + ':' + os.environ['STAGING_PORTAL_PASSWORD']).encode()
    request = urllib.request.Request(STAGING_ORIGIN + relative, headers={
        'Authorization': 'Basic ' + base64.b64encode(credentials).decode('ascii'),
        'Cache-Control': 'no-cache',
    })
    with urllib.request.build_opener(NoRedirect()).open(request, timeout=timeout) as response:
        return response.status, json.load(response)


def marker_matches(marker, sha, run_id, digest):
    return marker.get('environment') == 'Staging' and common.marker_matches(marker, sha, run_id, digest)


def wait_for_deployed_release(sha, run_id, digest, timeout_seconds=1200, delay_seconds=5):
    # The observed 140 MB hosting download took ~9 minutes. Count HTTP time
    # in one wall-clock budget, allowing backup/migration/startup after download.
    deadline = time.monotonic() + timeout_seconds
    while time.monotonic() < deadline:
        try:
            status, marker = portal_json('/tct-release.json', timeout=min(10, deadline - time.monotonic()))
            if time.monotonic() >= deadline:
                break
            ready_status, ready = portal_json('/health/ready', timeout=min(10, deadline - time.monotonic()))
            if time.monotonic() <= deadline and status == ready_status == 200 and ready.get('status') == 'ready' and marker_matches(marker, sha, run_id, digest):
                print('Staging expected commit/run/digest and readiness verified.')
                return True
        except Exception:
            pass  # Never print exception URLs, response bodies or credentials.
        remaining = deadline - time.monotonic()
        if remaining > 0:
            time.sleep(min(delay_seconds, remaining))
    return False


def ensure_staging_release(tag, artifact, sha):
    local_digest = common.file_sha256(artifact)
    release = common.find_release_by_tag(tag)
    if release is None:
        common.subprocess.run(['gh', 'release', 'create', tag, str(artifact) + '#' + common.RELEASE_ASSET,
            '--repo', common.REPOSITORY, '--target', sha, '--prerelease', '--latest=false',
            '--title', 'Staging ' + sha[:12], '--notes', 'Tested master artifact; staging only.'], check=True)
        release = common.find_release_by_tag(tag)
    if release is None or common.resolve_tag_commit_sha(tag) != sha:
        raise SystemExit('Staging release tag does not match the source commit.')
    return common.resolve_asset_sha256(release, local_digest)


def reviewed_recovery(run_id):
    if not RECOVERY_PLAN.is_file():
        return None
    recovery = json.loads(RECOVERY_PLAN.read_text(encoding='utf-8'))
    if (not isinstance(recovery, dict) or set(recovery) != {'runNumber', 'commit', 'previousCommit'}
            or type(recovery['runNumber']) is not int
            or not 0 < recovery['runNumber'] < int(run_id)
            or not re.fullmatch(r'[1-9][0-9]{0,17}', str(recovery['runNumber']))
            or any(not isinstance(recovery[key], str) or not common.SHA_RE.fullmatch(recovery[key])
                   for key in ('commit', 'previousCommit'))
            or recovery['commit'] == recovery['previousCommit']):
        raise SystemExit('Reviewed staging recovery identity is invalid; no hosting webhook was sent.')
    return recovery


def main_with_artifact(artifact_path):
    sha = os.environ.get('GITHUB_SHA', '')
    run_id = os.environ.get('GITHUB_RUN_ID', '')
    webhook = os.environ.get('PLESK_STAGING_WEBHOOK_URL', '')
    if (not common.SHA_RE.fullmatch(sha) or not re.fullmatch(r'[1-9][0-9]{0,17}', run_id)
            or os.environ.get('GITHUB_REPOSITORY') != common.REPOSITORY
            or os.environ.get('GITHUB_REF') != 'refs/heads/master'):
        raise SystemExit('Only a tested master run in the expected repository may publish staging.')
    if not webhook.startswith(WEBHOOK_PREFIX) or not os.environ.get('GH_TOKEN'):
        raise SystemExit('Staging GitHub/Plesk credentials are not configured.')
    if not os.environ.get('STAGING_PORTAL_USERNAME') or not os.environ.get('STAGING_PORTAL_PASSWORD'):
        raise SystemExit('Staging portal credentials are required for release verification.')
    artifact = Path(artifact_path).resolve()
    if not artifact.is_file():
        raise SystemExit('Staging artifact is missing.')
    recovery = reviewed_recovery(run_id)
    tag = 'staging-' + run_id + '-' + sha[:12]
    digest = ensure_staging_release(tag, artifact, sha)
    manifest = {'enabled': True, 'environment': 'Staging', 'sharedDataSchema': 1, 'repository': common.REPOSITORY,
        'commit': sha, 'runNumber': int(run_id), 'sha256': digest,
        'artifactUrl': 'https://github.com/' + common.REPOSITORY + '/releases/download/' + tag + '/' + common.RELEASE_ASSET}
    if recovery is not None:
        manifest['recoverImport'] = recovery
    common.update_branch({'release.json': json.dumps(manifest, indent=2),
        'deploy-staging.ps1': Path(__file__).with_name('deploy-staging.ps1').read_text(encoding='utf-8')})
    result = common.notify_plesk(webhook)
    if not wait_for_deployed_release(sha, int(run_id), digest):
        raise SystemExit('Staging did not verify the expected release (' + result + '). Inspect private state/backup before retry; do not rerun migrations blindly.')


if __name__ == '__main__':
    try:
        main_with_artifact(sys.argv[1])
    except (OSError, ValueError, KeyError, IndexError, urllib.error.URLError):
        raise SystemExit('Staging publication failed. Check private configuration and hosting state; credentials are omitted.') from None
