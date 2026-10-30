"""Remove development configuration and reject credentials before artifact upload."""
import json
import sys
from pathlib import Path

root = Path(sys.argv[1]).resolve()
if not root.is_dir():
    raise SystemExit('Publish directory is missing.')
if not (root / 'TCTEnglish.dll').is_file() or not (root / 'TCTEnglish.runtimeconfig.json').is_file():
    raise SystemExit('Expected a dotnet publish output, not a source directory.')
for candidate in root.glob('appsettings.*.json'):
    candidate.unlink()
config_path = root / 'appsettings.json'
config = json.loads(config_path.read_text(encoding='utf-8-sig'))
sensitive_keys = {'clientid', 'clientsecret', 'appid', 'appsecret', 'password', 'apikey', 'hashsecret', 'defaultconnection', 'senderemail'}

def inspect(value):
    if isinstance(value, dict):
        for key, item in value.items():
            if key.lower() in sensitive_keys and item:
                raise SystemExit('Publish config contains a credential value; refusing to create an artifact.')
            inspect(item)
    elif isinstance(value, list):
        for item in value:
            inspect(item)

inspect(config)
for relative in ('wwwroot/uploads', 'wwwroot/images/avatars', 'wwwroot/images/cards', 'wwwroot/images/classes', '.appdata', '.git'):
    candidate = root / relative
    if candidate.exists() and any(path.is_file() and path.name != '.gitkeep' for path in candidate.rglob('*')):
        raise SystemExit('Publish output contains runtime data; remove it before artifact upload.')
print('Publish configuration checked; Development/User Secrets/runtime data are excluded.')
