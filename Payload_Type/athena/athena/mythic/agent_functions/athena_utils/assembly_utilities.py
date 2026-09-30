import hashlib
import shutil
import xml.etree.ElementTree as ET
from pathlib import Path

OBFUSCATOR_RELATIVE_BINARY = Path("Obfuscator/bin/Release/net10.0/obfuscator.dll")
OBFUSCATOR_RELATIVE_PROJECT = Path("Obfuscator/Obfuscator.csproj")


def _local_name(element) -> str:
    return element.tag.rsplit("}", 1)[-1]


def derive_obfuscation_seed(agent_uuid: str) -> int:
    digest = hashlib.sha256(agent_uuid.encode()).hexdigest()
    return int(digest, 16) & 0x7FFFFFFF


def effective_assembly_name(project_path) -> str:
    """Return a project's literal AssemblyName, or its csproj stem."""
    project_path = Path(project_path)
    if not project_path.is_file():
        return project_path.stem
    root = ET.parse(project_path).getroot()
    for element in root.iter():
        if _local_name(element) != "AssemblyName":
            continue
        value = (element.text or "").strip()
        if value and "$(" not in value:
            return value
    return project_path.stem


def project_reference_includes(project_path) -> list[str]:
    """Return normalized ProjectReference Include paths from a csproj file."""
    project_path = Path(project_path)
    if not project_path.is_file():
        return []
    root = ET.parse(project_path).getroot()
    return [
        element.attrib["Include"].replace("\\", "/")
        for element in root.iter()
        if _local_name(element) == "ProjectReference"
        and "Include" in element.attrib
    ]


def _copy_project_directory(source_csproj: Path, temp_root: Path) -> Path:
    target_dir = temp_root / source_csproj.parent.name
    if not target_dir.exists():
        shutil.copytree(
            source_csproj.parent,
            target_dir,
            ignore=shutil.ignore_patterns("bin", "obj"),
        )
    return target_dir / source_csproj.name


def copy_project_dependencies(plugin_project, agent_code, temp_root) -> list[Path]:
    """Copy transitive sibling ProjectReference directories into temp_root."""
    models_csproj = temp_root / "Agent.Models/Agent.Models.csproj"
    dependencies = [models_csproj] if models_csproj.is_file() else []
    pending = [Path(plugin_project)]
    while pending:
        current = pending.pop()
        for rel_ref in project_reference_includes(current):
            source = (agent_code / current.parent.name / rel_ref).resolve()
            if not source.is_relative_to(agent_code) or not source.is_file():
                continue
            target_csproj = _copy_project_directory(source, temp_root)
            if target_csproj not in dependencies:
                dependencies.append(target_csproj)
                pending.append(target_csproj)
    return dependencies


def obfuscator_binary_path(workspace) -> Path:
    return Path(workspace) / OBFUSCATOR_RELATIVE_BINARY


async def ensure_obfuscator_binary(workspace, runner) -> Path:
    workspace_path = Path(workspace)
    obfuscator = obfuscator_binary_path(workspace_path)
    if not obfuscator.is_file():
        await runner(
            [
                "dotnet",
                "build",
                str(workspace_path / OBFUSCATOR_RELATIVE_PROJECT),
                "-c",
                "Release",
                "--nologo",
            ],
            str(workspace),
        )
    if not obfuscator.is_file():
        raise FileNotFoundError(
            "Custom obfuscator build produced no binary: " + str(obfuscator)
        )
    return obfuscator


def build_rewrite_source_command(
    obfuscator,
    seed,
    uuid,
    workspace,
    project_root,
    *,
    configuration="Release",
    handler_os="windows",
    crypto_provider="Aes",
    map_path=None,
) -> list[str]:
    command = [
        "dotnet",
        str(obfuscator),
        "rewrite-source",
        "--seed",
        str(seed),
        "--uuid",
        uuid,
        "--input",
        str(workspace),
        "--output",
        str(workspace),
    ]
    if map_path is not None:
        command.extend(["--map", str(map_path)])
    command.extend(
        [
            "--broad-semantic-rename",
            "--project-root",
            project_root,
            "--configuration",
            str(configuration),
            "--handler-os",
            handler_os,
            "--crypto-provider",
            crypto_provider,
        ]
    )
    return command


def build_rewrite_il_batch_command(
    obfuscator,
    seed,
    directory,
    map_path,
    first_party_assemblies,
    *,
    skip_file_rename=False,
) -> list[str]:
    command = [
        "dotnet",
        str(obfuscator),
        "rewrite-il-batch",
        "--seed",
        str(seed),
        "--dir",
        str(directory),
        "--map",
        str(map_path),
    ]
    if skip_file_rename:
        command.append("--skip-file-rename")
    for assembly_name in sorted(first_party_assemblies, key=str.casefold):
        command.extend(["--first-party-assembly", assembly_name])
    return command



