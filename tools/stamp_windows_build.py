"""Run before each Windows export to stamp a unique compiled team-build identity."""
from pathlib import Path
from datetime import datetime, timezone
import json
root=Path(__file__).resolve().parent.parent
state=root/'artifacts/release-config/build-identities.json'
state.parent.mkdir(parents=True,exist_ok=True)
ledger=json.loads(state.read_text()) if state.exists() else {}
date=datetime.now().astimezone().strftime('%Y-%m-%d')
serial=int(ledger.get(date,0))+1
ledger[date]=serial
state.write_text(json.dumps(ledger,indent=2))
build_id=f'BITE-{date}-{serial:02d}'
built_at=datetime.now(timezone.utc).isoformat(timespec='seconds')
source='using Godot;\nnamespace Gamejam2.Startup;\n/// <summary>Generated once per Windows export by tools/stamp_windows_build.py.</summary>\npublic static class BuildIdentity\n{\n    public const string Id = "{BUILD_ID}";\n    public const string BuiltAtUtc = "{BUILT_AT}";\n    public const bool ShowTeamTestId = true;\n    private static bool _logged;\n    public static void LogBoot()\n    {\n        if(_logged)return;_logged=true;\n        GD.Print("[BUILD] "+Id);\n        string executable=OS.GetExecutablePath().Replace(\'\\\\\',\'/\').Split(\'/\')[^1];\n        GD.Print("[BUILD] BuiltAtUtc = "+BuiltAtUtc);\n        GD.Print("[BUILD] Godot = "+Engine.GetVersionInfo()["string"].AsString());\n        GD.Print("[BUILD] Configuration = "+(OS.IsDebugBuild()?"debug":"release"));\n        GD.Print("[BUILD] Executable = "+executable);\n    }\n}\n'
source=source.replace('{BUILD_ID}',build_id).replace('{BUILT_AT}',built_at)
(root/'game/startup/BuildIdentity.cs').write_text(source)
folder=f"BITE_TEST_{date.replace('-','')}_{serial:02d}"
print(json.dumps({'BuildId':build_id,'Folder':folder,'BuiltAtUtc':built_at}))
