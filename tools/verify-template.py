#!/usr/bin/env python3
"""Verifies the `dotnet new shopify-blazor` template the only way that means anything: by packing
it, installing it, generating a project and building that project.

Why this exists rather than trusting the template by inspection:

  * A template is not compiled by the solution build. Nothing in `dotnet build` reads
    templates/**/content, so a broken `.csproj`, an unreplaced token or a template directive left in
    the output would ship silently.
  * The template's four parameters are not independent. `--include-admin-api-example false` removes
    a service and a page; if a navigation entry or an `@using` still refers to them, the generated
    project does not compile. A choice that produces an untested combination is worse than no
    choice, so every combination is generated and built.

It needs no network beyond NuGet (the packages are restored from the cache when it is warm) and no
npm. Exit code 0 means every combination generated cleanly and built.

Usage:
    python3 tools/verify-template.py                 # every combination
    python3 tools/verify-template.py --keep          # leave the temp tree for inspection
    python3 tools/verify-template.py --quick         # the default combination only
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TEMPLATE_PROJECT = ROOT / "src" / "Shopify.Blazor.Templates" / "Shopify.Blazor.Templates.csproj"
TEMPLATE_JSON = (
    ROOT / "src" / "Shopify.Blazor.Templates" / "templates" / "shopify-blazor" / ".template.config" / "template.json"
)
ARTIFACTS = ROOT / "artifacts"

# The package ids whose locally packed versions the generated project must resolve.
LIBRARY_PROJECTS = [
    ROOT / "src" / "Polaris.Blazor" / "Polaris.Blazor.csproj",
    ROOT / "src" / "Shopify.Blazor" / "Shopify.Blazor.csproj",
]

# Every parameter combination the template offers. `None` means "leave the parameter out", which is
# what a developer who does not pass it gets — the default must be exercised too.
COMBINATIONS = [
    ("defaults", {}),
    ("no-sample-pages", {"include-sample-pages": "false"}),
    ("no-admin-api-example", {"include-admin-api-example": "false"}),
    ("shell-only", {"include-sample-pages": "false", "include-admin-api-example": "false"}),
]

QUICK_COMBINATIONS = [("defaults", {})]

# Tokens the template engine must have replaced in the generated output. A leftover token is the
# silent failure this script exists to catch: the project still looks plausible.
UNREPLACED_TOKENS = ["TOOLKIT_PACKAGE_VERSION", "SHOPIFY_BLAZOR_USER_SECRETS_ID", "Shopify.Blazor.SampleApp"]

# A template directive left in the output would be compiled by Razor or parsed by JSON. Razor
# conditionals are written as `@*#if (x)` … `##endif*@` — note the doubled hash on the closing
# directive — which is why the pattern allows more than one '#'.
TEMPLATE_DIRECTIVE = re.compile(r"^\s*(?:@\*|//|<!--)?\s*#+(?:if|elseif|else|endif)\b", re.MULTILINE)

TEXT_SUFFIXES = {".cs", ".razor", ".json", ".csproj", ".md", ".js", ".gitignore", ".config", ""}


def log(message: str) -> None:
    print(message, flush=True)


def run(command: list[str], *, cwd: Path | None = None, env: dict[str, str] | None = None) -> str:
    """Runs a command, echoing it, and returns its combined output.

    A failure raises with the tail of the output attached: an unbuilt template is the whole point of
    running this, so the reason must be visible in the terminal rather than swallowed.
    """
    log(f"  $ {' '.join(command)}")
    completed = subprocess.run(
        command,
        cwd=str(cwd) if cwd else None,
        env=env,
        capture_output=True,
        text=True,
    )
    output = (completed.stdout or "") + (completed.stderr or "")

    if completed.returncode != 0:
        raise RuntimeError(
            f"command failed with exit code {completed.returncode}: {' '.join(command)}\n"
            f"--- output (tail) ---\n{output[-4000:]}"
        )

    return output


def dotnet_env(template_home: Path) -> dict[str, str]:
    """Environment for every tool invocation here.

    DOTNET_CLI_HOME decides where the template engine stores installed templates, so it points at a
    throwaway directory: installing the template under test into the real home would leave it behind
    and make the next run test a stale copy. NUGET_PACKAGES stays on the repository cache, which is
    what makes a repeat run fast and independent of the network.
    """
    env = dict(os.environ)
    env["DOTNET_CLI_HOME"] = str(template_home)
    env["NUGET_PACKAGES"] = str(ROOT / ".nuget" / "packages")
    env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    env["DOTNET_NOLOGO"] = "1"
    env["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1"
    return env


def template_metadata() -> dict:
    """The template's own declaration, checked against the repository version.

    The package version in the generated project is a template parameter with a literal default, so
    it can drift from Directory.Build.props. Comparing them here catches that at the cheapest
    possible moment.
    """
    metadata = json.loads(TEMPLATE_JSON.read_text(encoding="utf-8"))
    symbols = metadata.get("symbols", {})

    if metadata.get("shortName") != "shopify-blazor":
        raise RuntimeError(f"unexpected shortName in {TEMPLATE_JSON}: {metadata.get('shortName')!r}")

    for required in ("include-sample-pages", "include-admin-api-example", "toolkitVersion"):
        if required not in symbols:
            raise RuntimeError(f"template.json is missing the {required!r} symbol")

    declared = symbols["toolkitVersion"].get("defaultValue")
    repository_version = repository_package_version()
    if declared != repository_version:
        raise RuntimeError(
            f"the template pins toolkitVersion={declared!r} but the repository builds {repository_version!r}. "
            f"Update the defaultValue in {TEMPLATE_JSON.relative_to(ROOT)}."
        )

    log(f"  template shortName=shopify-blazor, toolkitVersion={declared} (matches Directory.Build.props)")
    return metadata


def repository_package_version() -> str:
    """The version `dotnet pack` will stamp on the libraries.

    Directory.Build.props builds it from MSBuild properties (`$(VersionPrefix)-preview.1`), so the
    literal text is not the version. Expanding the one property it references is enough to compare
    against the template's pinned value; anything more elaborate would be reimplementing MSBuild.
    """
    props = (ROOT / "Directory.Build.props").read_text(encoding="utf-8")

    def property_value(name: str) -> str | None:
        match = re.search(rf"<{name}>([^<]+)</{name}>", props)
        return match.group(1).strip() if match else None

    version = property_value("PackageVersion")
    if not version:
        raise RuntimeError("Directory.Build.props has no <PackageVersion>, so the template's version cannot be checked")

    prefix = property_value("VersionPrefix")
    if "$(VersionPrefix)" in version:
        if not prefix:
            raise RuntimeError("PackageVersion references $(VersionPrefix) but no VersionPrefix is defined")
        version = version.replace("$(VersionPrefix)", prefix)

    return version


def pack(env: dict[str, str]) -> Path:
    """Packs the two libraries and the template into artifacts/.

    Every previous package is deleted first. `dotnet pack` overwrites, but MSBuild's incremental
    build can reuse a cached item list, and a stale package that still installs is exactly how a
    verification run tests last week's template and reports success.
    """
    ARTIFACTS.mkdir(exist_ok=True)

    for stale in list(ARTIFACTS.glob("Polaris.Blazor.*.nupkg")) + list(ARTIFACTS.glob("Shopify.Blazor.*.nupkg")):
        stale.unlink()

    for project in LIBRARY_PROJECTS:
        run(["dotnet", "pack", str(project), "-c", "Release", "-o", str(ARTIFACTS)], env=env)

    run(["dotnet", "pack", str(TEMPLATE_PROJECT), "-c", "Release", "-o", str(ARTIFACTS)], env=env)

    template_package = next(ARTIFACTS.glob("Shopify.Blazor.Templates.*.nupkg"), None)
    if template_package is None:
        raise RuntimeError(f"no template package was produced in {ARTIFACTS}")

    _assert_template_package_is_installable(template_package)
    return template_package


def _assert_template_package_is_installable(package: Path) -> None:
    """Checks the package's internal layout, which `dotnet new install` is unforgiving about.

    A template package whose template.json is not under content/ installs successfully and then
    offers no template, which reads as "dotnet new cannot find shopify-blazor" and sends you looking
    in the wrong place.
    """
    with zipfile.ZipFile(package) as archive:
        names = archive.namelist()

    manifests = [name for name in names if name.endswith(".template.config/template.json")]
    if not manifests:
        raise RuntimeError(f"{package.name} contains no .template.config/template.json: {names[:20]}")

    if not all(name.startswith("content/") for name in manifests):
        raise RuntimeError(f"template manifests are not under content/: {manifests}")

    # The directory holding .template.config IS the template root, and every file under it becomes
    # the generated project. A stray folder there (a leftover `content/` from an earlier layout,
    # which MSBuild can keep alive in obj/) does not fail anything — it just generates the project
    # one level too deep, which is why this is checked rather than discovered later.
    stray = [name for name in names if name.startswith("content/") and "/content/" in name[len("content/"):]]
    if stray:
        raise RuntimeError(
            f"{package.name} nests the template content inside a stray directory, so generated projects "
            f"would be one level too deep ({stray[0]}). This usually means a stale obj/ — delete "
            f"src/Shopify.Blazor.Templates/obj and pack again."
        )

    content_files = [name for name in names if name.startswith("content/") and not name.endswith("/")]
    if len(content_files) < 10:
        raise RuntimeError(f"{package.name} looks empty: only {len(content_files)} content files")

    log(f"  {package.name}: {len(content_files)} content files, manifest at {manifests[0]}")


def install_template(package: Path, env: dict[str, str]) -> None:
    run(["dotnet", "new", "install", str(package)], env=env)

    listing = run(["dotnet", "new", "list", "shopify-blazor"], env=env)
    if "shopify-blazor" not in listing:
        raise RuntimeError(f"the template did not install:\n{listing}")


def generate(work: Path, name: str, options: dict[str, str], env: dict[str, str]) -> Path:
    """Generates one project and returns its directory."""
    target = work / name
    command = ["dotnet", "new", "shopify-blazor", "-n", name, "-o", str(target)]

    for key, value in options.items():
        command += ["--" + key, value]

    run(command, env=env)
    return target


def write_local_feed(project: Path) -> None:
    """Points the generated project at the locally packed toolkit.

    The template references the packages by version, which is what a real consumer does. Nothing is
    on nuget.org yet, so the local artifacts folder stands in for it. This file belongs to the test
    harness, not to the template: the generated project must not ship a feed pointing at somebody
    else's build output.
    """
    (project / "nuget.config").write_text(
        f"""<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="{ARTIFACTS}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
""",
        encoding="utf-8",
    )


def assert_fully_rendered(project: Path) -> None:
    """Every template token and directive must be gone from the output."""
    offenders: list[str] = []

    for path in sorted(project.rglob("*")):
        if not path.is_file() or "nuget.config" in path.name:
            continue
        if path.suffix not in TEXT_SUFFIXES:
            continue

        text = path.read_text(encoding="utf-8", errors="replace")
        relative = path.relative_to(project)

        for token in UNREPLACED_TOKENS:
            if token in text:
                offenders.append(f"{relative}: unreplaced token {token}")

        if TEMPLATE_DIRECTIVE.search(text):
            offenders.append(f"{relative}: template directive left in the output")

    if offenders:
        raise RuntimeError("generated project still contains template machinery:\n  " + "\n  ".join(offenders))


def assert_combination(project: Path, name: str, options: dict[str, str]) -> None:
    """Checks that the parameters actually changed the output.

    Generating a project that ignores its parameters is the quiet failure: `dotnet new` succeeds and
    the developer gets a project that does not match the flags they passed.
    """
    sample_pages = options.get("include-sample-pages", "true") == "true"
    admin_api = options.get("include-admin-api-example", "true") == "true"

    expected_present = [project / "Program.cs", project / "Components" / "Pages" / "Dashboard.razor"]
    expected_absent: list[Path] = []

    if sample_pages:
        expected_present.append(project / "Components" / "Pages" / "Components.razor")
    else:
        expected_absent.append(project / "Components" / "Pages" / "Components.razor")

    if sample_pages and admin_api:
        expected_present.append(project / "Components" / "Pages" / "Products.razor")
    else:
        expected_absent.append(project / "Components" / "Pages" / "Products.razor")

    if admin_api:
        expected_present += [project / "Services" / "ProductsService.cs", project / "wwwroot" / "js" / "shopify-fetch.js"]
    else:
        expected_absent += [project / "Services" / "ProductsService.cs", project / "wwwroot" / "js" / "shopify-fetch.js"]

    for path in expected_present:
        if not path.exists():
            raise RuntimeError(f"[{name}] expected {path.relative_to(project)} to exist")

    for path in expected_absent:
        if path.exists():
            raise RuntimeError(f"[{name}] expected {path.relative_to(project)} to be excluded")

    # A project with no reference to the toolkit builds into a page full of undefined components.
    csproj = next(project.glob("*.csproj"))
    csproj_text = csproj.read_text(encoding="utf-8")
    for package in ("Polaris.Blazor", "Shopify.Blazor"):
        if f'Include="{package}"' not in csproj_text:
            raise RuntimeError(f"[{name}] {csproj.name} does not reference {package}")

    # appsettings must stay parseable JSON: a stray template directive in a .json file is invisible
    # until the app starts and configuration silently ends up empty.
    json.loads((project / "appsettings.json").read_text(encoding="utf-8"))

    # The render mode is the difference between an interactive app and a page that renders perfectly
    # and does nothing.
    routes = (project / "Components" / "Routes.razor").read_text(encoding="utf-8")
    if "@rendermode InteractiveServer" not in routes:
        raise RuntimeError(f"[{name}] Components/Routes.razor lost its @rendermode InteractiveServer")

    # The generated project must be the developer's to edit: no scaffolding markers anywhere.
    if "SampleApp" in csproj.name:
        raise RuntimeError(f"[{name}] the project file was not renamed: {csproj.name}")

    log(f"  [{name}] layout, references, JSON and render mode checked")


def build(project: Path, env: dict[str, str]) -> None:
    write_local_feed(project)
    run(["dotnet", "build", "-c", "Debug"], cwd=project, env=env)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--keep", action="store_true", help="keep the temporary tree for inspection")
    parser.add_argument("--quick", action="store_true", help="verify the default combination only")
    parser.add_argument("--combinations", nargs="*", help="verify only these named combinations")
    parser.add_argument(
        "--metadata-only",
        action="store_true",
        help="check the template's declared version against Directory.Build.props and exit, without packing. "
             "Used by tools/check-drift.py, where the question is whether the two files agree, not whether "
             "the template builds.",
    )
    args = parser.parse_args()

    if args.metadata_only:
        # Pure file reads: this path deliberately needs no SDK, so the drift check can run anywhere.
        try:
            template_metadata()
            return 0
        except RuntimeError as error:
            log(f"FAILED: {error}")
            return 1

    combinations = QUICK_COMBINATIONS if args.quick else COMBINATIONS
    if args.combinations:
        wanted = set(args.combinations)
        combinations = [entry for entry in COMBINATIONS if entry[0] in wanted]
        if not combinations:
            log(f"no combination matched {sorted(wanted)}; known: {[name for name, _ in COMBINATIONS]}")
            return 2

    work = Path(tempfile.mkdtemp(prefix="shopify-blazor-template-"))
    template_home = work / ".template-home"
    template_home.mkdir(parents=True, exist_ok=True)
    env = dotnet_env(template_home)

    log(f"verifying the template in {work}")

    try:
        log("1. template metadata")
        template_metadata()

        log("2. packing the toolkit and the template")
        package = pack(env)

        log("3. installing the template into a throwaway template engine home")
        install_template(package, env)

        for name, options in combinations:
            log(f"4. generating and building [{name}] {options or 'default parameters'}")
            project = generate(work, f"App{name.title().replace('-', '')}", options, env)
            assert_fully_rendered(project)
            assert_combination(project, name, options)
            build(project, env)
            log(f"  [{name}] built")

        log(f"\ntemplate verified: {len(combinations)} combination(s) generated and built")
        return 0

    except RuntimeError as error:
        log(f"\nFAILED: {error}")
        return 1

    finally:
        if args.keep:
            log(f"kept {work}")
        else:
            shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())
