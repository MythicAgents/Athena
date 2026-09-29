import shutil
import xml.etree.ElementTree as ET
from pathlib import Path


def effective_assembly_name(project_path):
    """Return a project's literal AssemblyName, or its csproj stem."""
    project_path = Path(project_path)
    if not project_path.is_file():
        return project_path.stem
    root = ET.parse(project_path).getroot()
    for element in root.iter():
        if element.tag.rsplit("}", 1)[-1] != "AssemblyName":
            continue
        value = (element.text or "").strip()
        if value and "$(" not in value:
            return value
    return project_path.stem


def project_reference_includes(project_path):
    """Return normalized ProjectReference Include paths from a csproj file."""
    project_path = Path(project_path)
    if not project_path.is_file():
        return []
    root = ET.parse(project_path).getroot()
    return [
        element.attrib["Include"].replace("\\", "/")
        for element in root.iter()
        if element.tag.rsplit("}", 1)[-1] == "ProjectReference"
        and "Include" in element.attrib
    ]


def copy_project_dependencies(plugin_project, agent_code, temp_root):
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
            target_dir = temp_root / source.parent.name
            if not target_dir.exists():
                shutil.copytree(
                    source.parent, target_dir,
                    ignore=shutil.ignore_patterns("bin", "obj"),
                )
            target_csproj = target_dir / source.name
            if target_csproj not in dependencies:
                dependencies.append(target_csproj)
                pending.append(target_csproj)
    return dependencies

