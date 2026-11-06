"""Build an immutable plugin archive and its release inventory from published output."""

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET
import zipfile

REPOSITORY = "https://github.com/XmmShp/Quantum.Zju.Plugin.Cas"
ROOT = Path(__file__).resolve().parents[1]


def digest(content):
    return hashlib.sha256(content).hexdigest()


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def create_archive(directory, platform):
    directory = directory.resolve()
    manifest = json.loads((directory / "plugin.json").read_text(encoding="utf-8-sig"))
    version = manifest["version"]
    if manifest["id"] != "quantum.zju.plugin.cas" or not re.fullmatch(r"\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?", version):
        raise ValueError("Unexpected plugin identity or version")
    if platform not in ("windows", "macos"):
        raise ValueError("Unsupported native platform")
    required = {"plugin.json", "Quantum.Zju.Plugin.Cas.dll", "Quantum.Zju.Plugin.Cas.Native.dll",
                "Quantum.Plugin.Abstraction.dll", "LICENSE", "NOTICE"}
    paths = sorted(path for path in directory.rglob("*") if path.is_file())
    names = {path.relative_to(directory).as_posix() for path in paths}
    if not required.issubset(names):
        raise ValueError("Missing required release files")
    for path in paths:
        if path.is_symlink() or not path.resolve().is_relative_to(directory):
            raise ValueError("Release files must stay inside the publish directory")
        if any(part.startswith(".") or part in {"src", "tests", "bin", "obj"} for part in path.relative_to(directory).parts):
            raise ValueError("Source, hidden or development files must not be published")
        if path.suffix.lower() in {".pdb", ".log", ".env"} or ".staticwebassets." in path.name:
            raise ValueError("Debug or local-only files must not be published")
    for name in ("Quantum.Zju.Plugin.Cas.dll", "Quantum.Zju.Plugin.Cas.Native.dll"):
        data = (directory / name).read_bytes()
        for encoding in ("utf-8", "utf-16-le"):
            if re.search(r"(?i)([a-z]:[\\/]Users[\\/]|/Users/|/home/[^/]+/|/tmp/)", data.decode(encoding, errors="ignore")):
                raise ValueError("Plugin assemblies contain a private workstation path")
    archive = directory.parent / f"{manifest['id']}-{version}-{platform}.zip"
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED) as package:
        for path in paths:
            info = zipfile.ZipInfo(path.relative_to(directory).as_posix(), (1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            package.writestr(info, path.read_bytes())
    return manifest, archive, paths


def release_inventory(directory, platform):
    manifest, archive, paths = create_archive(directory, platform)
    sha256 = digest(archive.read_bytes())
    sdk = next(item.attrib["Version"] for item in ET.parse(ROOT / "Directory.Packages.props").getroot().iter("PackageVersion")
               if item.attrib.get("Include") == "Quantum.Plugin.Abstraction")
    if not re.fullmatch(r"\[[^\[\],()]+\]", sdk):
        raise ValueError("Release builds require an exact SDK pin")
    sdk = sdk[1:-1]
    revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    dirty = bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT, text=True).strip())
    metadata = {"pluginId": manifest["id"], "version": manifest["version"], "platform": platform,
                "buildSdkVersion": sdk, "sdkVersionSupport": sdk, "sourceSha": revision, "sourceDirty": dirty,
                "archive": archive.name, "sha256": sha256}
    pipeline = f"{REPOSITORY}/actions/runs/{os.environ['GITHUB_RUN_ID']}" if os.environ.get("GITHUB_RUN_ID") else None
    metadata["pipeline"] = pipeline
    packages = {}
    for project in ("Quantum.Zju.Plugin.Cas", "Quantum.Zju.Plugin.Cas.Native"):
        assets = json.loads((ROOT / "src" / project / "obj/project.assets.json").read_text(encoding="utf-8"))
        packages.update({name: value for name, value in assets["libraries"].items() if value["type"] == "package"})
    package_entries = [{"SPDXID": "SPDXRef-Plugin", "name": manifest["id"], "versionInfo": manifest["version"],
                        "downloadLocation": "NOASSERTION", "filesAnalyzed": False,
                        "licenseConcluded": "NOASSERTION", "licenseDeclared": "Apache-2.0", "copyrightText": "NOASSERTION",
                        "checksums": [{"algorithm": "SHA256", "checksumValue": sha256}]}]
    relationships = [{"spdxElementId": "SPDXRef-DOCUMENT", "relationshipType": "DESCRIBES", "relatedSpdxElement": "SPDXRef-Plugin"}]
    for identity in sorted(packages):
        name, version = identity.split("/", 1)
        identifier = "SPDXRef-NuGet-" + digest(identity.encode())[:20]
        package_entries.append({"SPDXID": identifier, "name": name, "versionInfo": version, "filesAnalyzed": False,
                                "licenseConcluded": "NOASSERTION", "licenseDeclared": "NOASSERTION", "copyrightText": "NOASSERTION",
                                "downloadLocation": f"https://api.nuget.org/v3-flatcontainer/{name.lower()}/{version.lower()}/{name.lower()}.{version.lower()}.nupkg",
                                "externalRefs": [{"referenceCategory": "PACKAGE-MANAGER", "referenceType": "purl", "referenceLocator": f"pkg:nuget/{name}@{version}"}]})
        relationships.append({"spdxElementId": "SPDXRef-Plugin", "relationshipType": "DEPENDS_ON", "relatedSpdxElement": identifier})
    files = []
    for path in paths:
        name = path.relative_to(directory.resolve()).as_posix()
        identifier = "SPDXRef-File-" + digest(name.encode())[:20]
        files.append({"SPDXID": identifier, "fileName": "./" + name,
                      "checksums": [{"algorithm": "SHA256", "checksumValue": digest(path.read_bytes())}],
                      "licenseConcluded": "NOASSERTION", "licenseInfoInFiles": ["NOASSERTION"], "copyrightText": "NOASSERTION"})
        relationships.append({"spdxElementId": "SPDXRef-Plugin", "relationshipType": "CONTAINS", "relatedSpdxElement": identifier})
    sbom = {"spdxVersion": "SPDX-2.3", "dataLicense": "CC0-1.0", "SPDXID": "SPDXRef-DOCUMENT",
            "name": archive.name, "documentNamespace": f"{REPOSITORY}/sbom/{sha256}/{platform}",
            "creationInfo": {"creators": ["Tool: quantum-cas-release-inventory"], "created": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")},
            "comment": "Archive file inventory and NuGet restore inputs. The host supplies the shared MAUI runtime.",
            "packages": package_entries, "files": files, "relationships": relationships}
    provenance = {"_type": "https://in-toto.io/Statement/v1", "subject": [{"name": archive.name, "digest": {"sha256": sha256}}],
                  "predicateType": "https://slsa.dev/provenance/v1", "predicate": {
                      "buildDefinition": {"buildType": REPOSITORY + "/.ci/Build.ps1", "externalParameters": metadata,
                                          "resolvedDependencies": [{"uri": REPOSITORY + ".git", "digest": {"gitCommit": revision}}]},
                      "runDetails": {"builder": {"id": "https://github.com/actions/runner" if pipeline else "local-verification"},
                                     "metadata": {"invocationId": pipeline or "local"}}}}
    output = directory.parent
    write_json(output / f"release-metadata-{platform}.json", metadata)
    write_json(output / f"sbom-{platform}.spdx.json", sbom)
    write_json(output / f"provenance-{platform}.json", provenance)
    names = [archive.name, f"release-metadata-{platform}.json", f"sbom-{platform}.spdx.json", f"provenance-{platform}.json"]
    (output / f"SHA256SUMS-{platform}.txt").write_text("".join(f"{digest((output / name).read_bytes())}  {name}\n" for name in names), encoding="utf-8")
    print(f"Created {archive.name}; SHA-256 {sha256}; source dirty: {dirty}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("platform", choices=["windows", "macos"])
    args = parser.parse_args()
    release_inventory(args.directory.resolve(), args.platform)
