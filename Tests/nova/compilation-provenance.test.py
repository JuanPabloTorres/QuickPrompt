"""Real SDK regression for restored source older than cached compiler outputs."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

spec=importlib.util.spec_from_file_location('stage',Path(__file__).with_name('certification-stage.py'))
stage=importlib.util.module_from_spec(spec);spec.loader.exec_module(stage)


class CompilationProvenanceTests(unittest.TestCase):
    def test_certification_build_compiles_restored_source_despite_older_timestamp(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            project=root/'Probe.csproj'
            project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>'
                '</PropertyGroup></Project>',encoding='utf-8')
            # No package sources or dependencies: use installed SDK reference packs.
            config=root/'NuGet.Config'
            config.write_text('<configuration><packageSources><clear /></packageSources></configuration>',encoding='utf-8')
            source=root/'Program.cs'
            source.write_text('System.Console.Write("OLD_ARTIFACT");',encoding='utf-8')
            original=source.stat()
            def run(command):
                result=subprocess.run([str(arg) for arg in command],cwd=root,capture_output=True,text=True,timeout=90)
                self.assertEqual(0,result.returncode,result.stdout+'\n'+result.stderr)
                return result.stdout
            run(['dotnet','restore',project,'--configfile',config,'-p:NuGetAudit=false'])
            run(['dotnet','build',project,'--no-restore'])
            assembly=root/'bin/Debug/net8.0/Probe.dll'
            self.assertEqual('OLD_ARTIFACT',run(['dotnet',assembly]))
            source.write_text('System.Console.Write("RESTORED_SOURCE");',encoding='utf-8')
            os.utime(source,ns=(original.st_atime_ns,original.st_mtime_ns))
            run(['dotnet','build',project,'--no-restore'])
            incremental=run(['dotnet',assembly])
            # Exercise the command used by the real certification stage.
            run(stage.backend_build_command(project))
            self.assertEqual('RESTORED_SOURCE',run(['dotnet',assembly]))
            print(json.dumps({'scope':'SYNTHETIC_DOTNET_COMPILATION_NO_MODEL',
                'incrementalResult':incremental,'certificationResult':'RESTORED_SOURCE'}))


if __name__=='__main__':unittest.main()
