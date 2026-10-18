#!/usr/bin/env python3
"""Structure validator for the Lane Weaver Unity skeleton (issue #1).

Runs on Linux (Docker) in CI. Static file checks only — no Unity editor,
so it never claims a project opens or a player builds. Exit 0 = structure OK.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ERRORS: list[str] = []


def err(msg: str) -> None:
    ERRORS.append(msg)


def check_exists(rel: str) -> Path | None:
    p = ROOT / rel
    if not p.is_file():
        err(f"missing file: {rel}")
        return None
    return p


def main() -> int:
    toolchain = {}
    tc = check_exists("toolchain.json")
    if tc:
        toolchain = json.loads(tc.read_text())

    # --- pinned editor version -------------------------------------------------
    pv = check_exists("ProjectSettings/ProjectVersion.txt")
    version_line = ""
    if pv:
        m = re.search(r"^m_EditorVersion:\s*(\S+)\s*$", pv.read_text(), re.M)
        if not m:
            err("ProjectVersion.txt: no m_EditorVersion line")
        else:
            version_line = m.group(1)
            line = toolchain.get("unity_version_line", "6000.3")
            if not version_line.startswith(line + "."):
                err(f"ProjectVersion.txt pins {version_line}, expected {line}.x")

    # --- packages ---------------------------------------------------------------
    mf = check_exists("Packages/manifest.json")
    if mf:
        deps = json.loads(mf.read_text()).get("dependencies", {})
        for pkg in ("com.unity.render-pipelines.universal", "com.unity.inputsystem"):
            if pkg not in deps:
                err(f"manifest.json missing dependency {pkg}")
        ui_ok = any(
            k in deps
            for k in ("com.unity.modules.uielements", "com.unity.modules.ui", "com.unity.ugui")
        )
        if not ui_ok:
            err("manifest.json has no UI Toolkit module (com.unity.modules.uielements)")

    # --- identifiers ------------------------------------------------------------
    bundle = toolchain.get("bundle_identifier", "com.infinityball.laneweaver")
    ps = check_exists("ProjectSettings/ProjectSettings.asset")
    if ps:
        text = ps.read_text()
        for target in ("Standalone", "Android", "iPhone"):
            pat = rf"^\s+{target}: {re.escape(bundle)}\s*$"
            if not re.search(pat, text, re.M):
                err(f"ProjectSettings.asset: applicationIdentifier {target} != {bundle}")

    # --- assembly definitions ---------------------------------------------------
    assemblies = [
        "Weaver.Core",
        "Weaver.Physics",
        "Weaver.Routes",
        "Weaver.Input",
        "Weaver.Presentation",
        "Weaver.UI",
        "Weaver.Persistence",
    ]
    found: dict[str, dict] = {}
    for asmdef in sorted(ROOT.glob("Assets/**/*.asmdef")):
        try:
            data = json.loads(asmdef.read_text())
        except json.JSONDecodeError as e:
            err(f"{asmdef.relative_to(ROOT)}: invalid JSON ({e})")
            continue
        name = data.get("name", "")
        found[name] = data

    for asm in assemblies:
        if asm not in found:
            err(f"missing assembly definition: {asm}")

    core = found.get("Weaver.Core")
    if core is not None and core.get("noEngineReferences") is not True:
        err("Weaver.Core asmdef must set noEngineReferences: true")

    # Weaver.Core sources must not reference UnityEngine
    for src in (ROOT / "Assets/Weaver/Core").rglob("*.cs"):
        body = src.read_text(errors="replace")
        if re.search(r"^\s*using\s+UnityEngine", body, re.M):
            err(f"{src.relative_to(ROOT)} references UnityEngine but Weaver.Core must stay pure C#")

    # --- test project ------------------------------------------------------------
    tp = ROOT / "tests" / "Weaver.Core.Tests.csproj"
    if not tp.is_file():
        err("missing thin test project tests/Weaver.Core.Tests.csproj")
    sln_check = (ROOT / "tests").glob("*.cs")
    if not any(True for _ in sln_check):
        err("tests/ contains no C# test sources")

    # --- gitignore / lfs ----------------------------------------------------------
    gi = check_exists(".gitignore")
    if gi:
        gitext = gi.read_text()
        for needle in ("library/", "temp/", "logs/", "builds/"):
            if needle not in gitext.lower().replace("[ll]", "l").replace("[tt]", "t").replace("[bb]", "b"):
                err(f".gitignore does not exclude {needle}")
    check_exists(".gitattributes")
    ga = (ROOT / ".gitattributes").read_text() if (ROOT / ".gitattributes").exists() else ""
    if "filter=lfs" not in ga:
        err(".gitattributes has no LFS filter rules")

    # --- committed build output ----------------------------------------------------
    for banned in ("Library", "Temp", "Logs", "Builds"):
        if (ROOT / banned).exists():
            err(f"build/generated directory committed or present: {banned}/")

    # --- workflows -----------------------------------------------------------------
    for wf in ("ci.yml", "release.yml"):
        p = ROOT / ".github" / "workflows" / wf
        if not p.is_file():
            err(f"missing workflow .github/workflows/{wf}")
    ci = ROOT / ".github" / "workflows" / "ci.yml"
    if ci.is_file():
        text = ci.read_text()
        if "windows-latest" not in text:
            err("ci.yml has no windows-latest job")
        for secret in ("UNITY_LICENSE", "UNITY_EMAIL", "UNITY_PASSWORD"):
            if secret not in text:
                err(f"ci.yml does not reference secret name {secret}")

    if ERRORS:
        print("STRUCTURE VALIDATION FAILED:")
        for e in ERRORS:
            print(f"  - {e}")
        return 1

    print(f"Structure OK: Unity {version_line}, {len(found)} assemblies, bundle {bundle}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
