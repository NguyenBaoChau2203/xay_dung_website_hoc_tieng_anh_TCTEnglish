import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[2]


class SharedDataGitTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        spec = importlib.util.spec_from_file_location('shared_checker', ROOT/'scripts/check-shared-data.py')
        self.checker = importlib.util.module_from_spec(spec); spec.loader.exec_module(self.checker)
        self.folder = self.root/'data/shared'; self.folder.mkdir(parents=True)
        self.package = self.folder/'packages/team.demo/1/package.json'
        self.package.parent.mkdir(parents=True); self.package.write_text('{}')
        self.release = dict(id='team.demo',version=1,path='packages/team.demo/1/package.json',sha256='a'*64)
        self.catalog = self.folder/'catalog.json'
        self.catalog.write_text(json.dumps(dict(schemaVersion=1,releases=[self.release])))
        self.git('init','--quiet')
        self.git('add','.')
        self.git('-c','user.name=Test','-c','user.email=test@example.test','commit','-qm','baseline')

    def git(self,*args):
        return subprocess.check_output(['git',*args],cwd=self.root,stderr=subprocess.STDOUT)

    def check(self):
        with mock.patch.object(self.checker,'ROOT',self.root):self.checker.check('HEAD')

    def test_unchanged_published_package_passes(self):self.check()

    def test_rewritten_published_bytes_rejected_even_if_catalog_unchanged(self):
        self.package.write_text('{"changed":true}')
        with self.assertRaisesRegex(ValueError,'immutable'):self.check()

    def test_removed_release_rejected(self):
        self.catalog.write_text('{"schemaVersion":1,"releases":[]}')
        with self.assertRaisesRegex(ValueError,'changed/removed'):self.check()

    def test_unfinished_export_cannot_silently_ship(self):
        new=self.folder/'packages/team.demo/2/package.json';new.parent.mkdir();new.write_text('{}')
        with self.assertRaisesRegex(ValueError,'Unindexed'):self.check()

    def test_new_version_can_be_added_without_mutating_old(self):
        new=self.folder/'packages/team.demo/2/package.json';new.parent.mkdir();new.write_text('{}')
        release=dict(self.release,version=2,path='packages/team.demo/2/package.json')
        self.catalog.write_text(json.dumps(dict(schemaVersion=1,releases=[self.release,release])))
        self.check()
