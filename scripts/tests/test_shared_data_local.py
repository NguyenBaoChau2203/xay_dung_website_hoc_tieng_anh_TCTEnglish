"""Execute the real sync/pull wrappers with isolated files and fake native I/O."""
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from pwsh_util import find_pwsh

ROOT=Path(__file__).resolve().parents[2]


class SharedDataLocalTests(unittest.TestCase):
    def invoke(self, fail=False, pull=False, build=False):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory); (root/'scripts').mkdir(); (root/'.appdata/local').mkdir(parents=True)
            for name in ('shared-data.ps1','pull-local.ps1'):
                shutil.copyfile(ROOT/'scripts'/name,root/'scripts'/name)
            (root/'.appdata/local/database.env').write_text('MARIADB_DATABASE=tctenglish_dev\nMARIADB_USER=tct_dev\nMARIADB_PASSWORD=fixture-db-marker\n')
            (root/'data/shared').mkdir(parents=True); (root/'data/shared/catalog.json').write_text('{"schemaVersion":1,"releases":[]}')
            for relative in ('TCTEnglish/bin/Release/net9.0','.appdata/local/shared-data/build/Release'):
                output=root/relative;output.mkdir(parents=True);(output/'TCTEnglish.dll').write_text('fixture')
            script=root/'driver.ps1'
            script.write_text(r'''param([switch]$Fail,[switch]$Pull)
$ErrorActionPreference='Stop'
$env:AI__ApiKey='fixture-ai-marker';$env:ConnectionStrings__DefaultConnection='parent-db-marker'
$names=@('AI__ApiKey','ConnectionStrings__DefaultConnection','DOTNET_ENVIRONMENT','ASPNETCORE_ENVIRONMENT','TEMP','TMP','TCT_SHARED_SYNC_IN_PROGRESS','Database__ServerVersion','Database__ApplyMigrationsOnStartup','Database__SeedOnStartup')
$before=@{};foreach($name in $names){$before[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
$global:Trace=[Collections.Generic.List[string]]::new()
function global:dotnet {
    if(($args -join ' ') -match 'fixture-(ai|db)-marker'){throw 'Key/password in argv'}
    $operation=if($args -contains '--shared-data=sync'){'sync'}elseif($args -contains '--shared-data=migrate'){'migrate'}elseif($args -contains '--local-bootstrap=true'){'bootstrap'}else{[string]$args[0]}
    $global:Trace.Add($operation)
    if($args[0] -eq 'build' -and $args -notcontains '--output'){$global:LASTEXITCODE=1;return}
    if($args -contains '--shared-data=sync'){
        if($env:ConnectionStrings__DefaultConnection -notmatch 'Database=tctenglish_dev;' -or $env:AI__ApiKey -cne 'fixture-ai-marker'){throw 'Wrong child environment'}
        $global:LASTEXITCODE=if($Fail){2}else{0};return
    }
    $global:LASTEXITCODE=0
}
function global:git {
    if(($args -join ' ') -cne 'pull --ff-only origin master'){throw 'Unsafe pull args'}
    $global:Trace.Add('pull');$global:LASTEXITCODE=0
}
function global:pwsh {
    if($args -contains '-DatabaseOnly'){$global:Trace.Add('database');$global:LASTEXITCODE=0;return}
    if($args -contains 'Sync'){$global:Trace.Add('sync-after-pull');$global:LASTEXITCODE=if($Fail){2}else{0};return}
    throw 'Unexpected child process'
}
$failed=$false
try { if($Pull){& "$PSScriptRoot/scripts/pull-local.ps1"}else{& "$PSScriptRoot/scripts/shared-data.ps1" -Action Sync -SkipBuild:(!$env:TCT_FIXTURE_BUILD)} }catch{$failed=$true}
$restored=$true;foreach($name in $names){if($before[$name] -cne [Environment]::GetEnvironmentVariable($name,'Process')){$restored=$false}}
@{Failed=$failed;Restored=$restored;Trace=$global:Trace.ToArray();HasReceipt=(Test-Path "$PSScriptRoot/.appdata/local/shared-data/last-sync.json")}|ConvertTo-Json -Compress
''',encoding='utf-8')
            args=[find_pwsh(),'-NoProfile','-File',str(script)]
            if fail:args.append('-Fail')
            if pull:args.append('-Pull')
            import os
            env=dict(os.environ)
            if build:env['TCT_FIXTURE_BUILD']='1'
            p=subprocess.run(args,env=env,capture_output=True,text=True,timeout=25)
            self.assertEqual(p.returncode,0,p.stderr)
            self.assertNotIn('fixture-ai-marker',p.stdout+p.stderr)
            self.assertNotIn('fixture-db-marker',p.stdout+p.stderr)
            result=json.loads(p.stdout.strip().splitlines()[-1]);self.assertTrue(result['Restored'],result)
            return result

    def test_sync_does_not_put_credentials_in_argv_and_restores_environment(self):
        result=self.invoke();self.assertFalse(result['Failed'],result);self.assertTrue(result['HasReceipt'],result)
        self.assertEqual(result['Trace'],['database','migrate','bootstrap','sync'])

    def test_failed_sync_does_not_mark_complete_and_restores_environment(self):
        result=self.invoke(fail=True);self.assertTrue(result['Failed'],result);self.assertFalse(result['HasReceipt'])

    def test_pull_runs_sync_even_when_git_reports_no_new_commits(self):
        result=self.invoke(pull=True);self.assertFalse(result['Failed'],result)
        self.assertEqual(result['Trace'],['pull','sync-after-pull'])

    def test_pull_does_not_hide_sync_failure(self):
        self.assertTrue(self.invoke(fail=True,pull=True)['Failed'])

    def test_sync_build_avoids_the_running_web_output(self):
        self.assertFalse(self.invoke(build=True)['Failed'])
