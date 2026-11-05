"""Git immutability and public-data boundary check; no database/credentials needed."""
import argparse
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

def check(base=None):
    root = ROOT / 'data/shared'
    catalog = json.loads((root/'catalog.json').read_text(encoding='utf-8'))
    current = {(r['id'],r['version']):r for r in catalog['releases']}
    if len(current) != len(catalog['releases']):raise ValueError('Duplicate shared release')
    if base:
        old = subprocess.run(['git','show',base+':data/shared/catalog.json'],cwd=ROOT,capture_output=True)
        if old.returncode == 0:
            for release in json.loads(old.stdout)['releases']:
                if current.get((release['id'],release['version'])) != release:raise ValueError('Published release metadata was changed/removed; add a new version')
                prefix='data/shared/'+str(Path(release['path']).parent).replace('\\','/')+'/'
                files=subprocess.check_output(['git','ls-tree','-r','--name-only',base,'--',prefix],cwd=ROOT,text=True).splitlines()
                for relative in files:
                    before=subprocess.check_output(['git','show',base+':'+relative],cwd=ROOT)
                    p=ROOT/relative
                    if not p.is_file() or p.read_bytes()!=before:raise ValueError('Published package/media is immutable: '+relative)
    tracked={str(p.relative_to(root)).replace('\\','/') for p in root.glob('packages/**/package.json')}
    indexed={r['path'] for r in catalog['releases']}
    if tracked != indexed:raise ValueError('Unindexed or missing package; finish export before handoff')
    print('Shared-data Git boundary and immutable history: PASS')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--base');args=p.parse_args()
    check(args.base)
