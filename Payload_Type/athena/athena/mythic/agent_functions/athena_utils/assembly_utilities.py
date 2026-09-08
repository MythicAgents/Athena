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
