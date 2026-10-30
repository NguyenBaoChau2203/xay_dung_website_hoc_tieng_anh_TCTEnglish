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


def api(path, body=None, method=None):
    payload = None if body is None else json.dumps(body).encode()
    request = urllib.request.Request('https://api.github.com/repos/' + REPOSITORY + path, data=payload,
        headers={'Authorization': 'Bearer ' + os.environ['GH_TOKEN'], 'Accept': 'application/vnd.github+json', 'Content-Type': 'application/json'}, method=method)
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)


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


def main():
    artifact = Path(sys.argv[1]).resolve()
    sha = os.environ['GITHUB_SHA']
    if not re.fullmatch(r'[0-9a-f]{40}', sha) or os.environ['GITHUB_REPOSITORY'] != REPOSITORY:
        raise SystemExit('Unexpected release identity.')
    webhook = os.environ.get('PLESK_WEBHOOK_URL', '')
    if not webhook.startswith('https://peaceful-bartik.103-28-36-90.plesk.page:8443/modules/git/public/web-hook.php?'):
        raise SystemExit('Configure the verified HTTPS Plesk webhook before enabling deployment.')
    tag = 'deploy-' + os.environ['GITHUB_RUN_ID'] + '-' + sha[:12]
    subprocess.run(['gh', 'release', 'create', tag, str(artifact) + '#tctenglish-release.zip', '--repo', REPOSITORY,
        '--target', sha, '--title', 'TCT English ' + sha[:12], '--notes', 'Application and migration bundle from a successful CI run.'], check=True)
    manifest = {'enabled': True, 'repository': REPOSITORY, 'commit': sha,
        'runNumber': int(os.environ['GITHUB_RUN_NUMBER']), 'sha256': hashlib.file_digest(artifact.open('rb'), 'sha256').hexdigest(),
        'artifactUrl': 'https://github.com/' + REPOSITORY + '/releases/download/' + tag + '/tctenglish-release.zip'}
    update_branch({'release.json': json.dumps(manifest, indent=2), 'deploy-plesk.ps1': Path('scripts/deploy-plesk.ps1').read_text(encoding='utf-8')})
    try:
        with urllib.request.urlopen(webhook, timeout=30) as response:
            response.read(4096)
    except Exception:
        raise SystemExit('Plesk webhook failed; its private URL has not been logged.') from None
    for _ in range(48):
        try:
            with urllib.request.urlopen('https://tctenglish.site/tct-release.json', timeout=10) as response:
                deployed = json.load(response)
            with urllib.request.urlopen('https://tctenglish.site/health/ready', timeout=10) as response:
                ready = response.status == 200
            if deployed.get('commit') == sha and ready:
                print('Plesk deployed the expected commit and database readiness passed.')
                return
        except Exception:
            pass
        time.sleep(5)
    raise SystemExit('Plesk release did not reach readiness; inspect private release status and backup before retrying.')


if __name__ == '__main__':
    main()
