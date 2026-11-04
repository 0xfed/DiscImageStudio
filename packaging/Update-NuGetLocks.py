"""Regenerate or validate the reviewed NuGet graphs for supported release targets.

Run from any directory with the exact SDK from global.json installed. Updating
is explicit; ordinary restores stay in locked mode. No package code is built.
"""
import argparse
from contextlib import ExitStack
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[1]
RIDS = ("linux-x64", "linux-arm64", "osx-x64", "osx-arm64")


def restore(workspace, project, windows, rid, check):
    command = ["dotnet", "restore", str(workspace / project),
               "-p:EnableWindowsTargeting=true",
               f"-p:DiscImageStudioWindowsTargeting={str(windows).lower()}"]
    if rid:
        command += [f"-p:RuntimeIdentifier={rid}", "-p:SelfContained=true"]
    command += (["--locked-mode"] if check else
                ["-p:RestoreLockedMode=false", "--force-evaluate"])
    print(" ".join(command), flush=True)
    environment = {**os.environ, "DOTNET_NUGET_SIGNATURE_VERIFICATION": "true"}
    subprocess.run(command, cwd=workspace, env=environment, check=True)


def copy_build_inputs(destination):
    files = [ROOT / name for name in ("global.json", "Directory.Build.props", "NuGet.Config", "DiscImageStudio.slnx")]
    for folder in ("src", "tests", "tools"):
        files += [path for path in (ROOT / folder).rglob("*.csproj")
                  if not {"bin", "obj"} & set(path.relative_to(ROOT).parts)]
    for source in files:
        target = destination / source.relative_to(ROOT)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
    shutil.copytree(ROOT / "eng/dependencies", destination / "eng/dependencies")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="validate locks without updating them")
    args = parser.parse_args()
    with ExitStack() as stack:
        workspace = ROOT
        if args.check:
            workspace = Path(stack.enter_context(tempfile.TemporaryDirectory(prefix="disc-image-studio-locks-")))
            copy_build_inputs(workspace)
        # Both target-framework layouts are explicit, independent of the host OS.
        for windows in (False, True):
            restore(workspace, "DiscImageStudio.slnx", windows, None, args.check)
        for rid in RIDS:
            restore(workspace, "src/DiscImageStudio.Cli/DiscImageStudio.Cli.csproj", False, rid, args.check)
        restore(workspace, "src/DiscImageStudio.App/DiscImageStudio.App.csproj", True, "win-x64", args.check)
        restore(workspace, "tools/FourLevelDither/FourLevelDither.csproj", True, "win-x64", args.check)
        if not args.check:
            # Leave normal local build assets in the native layout. Validation
            # runs in an isolated metadata copy so it cannot disrupt a build.
            restore(workspace, "DiscImageStudio.slnx", os.name == "nt", None, True)


if __name__ == "__main__":
    main()
