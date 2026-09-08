import sys
import os
import platform
import subprocess
import xml.etree.ElementTree as ET
import time
import hashlib
import shutil

def create_obfuscar_xml(plugin_name, config, project_dir, rid):
    #assembly_search_path = os.path.join(project_dir.replace(plugin_name,""),"Agent.Models", "bin",config,"net10.0")
    assembly_search_path = os.path.abspath(os.path.join(project_dir, os.pardir, "Agent.Models", "bin", config, "net10.0"))
    models_assembly_path = os.path.join(assembly_search_path,"Agent.Models.dll")
    if(not os.path.exists(models_assembly_path)):
        print("!!!!!!!!!!!!! Building Agent.Models.dll !!!!!!!!!!!!!")
        try:
            build_model_dll(plugin_name, project_dir, config)
        except:
            wait_for_file(os.path.join(project_dir.replace(plugin_name,""),"Agent.Models", "bin",config,"net10.0","Agent.Models.dll"))

    in_path = get_interim_build_path(plugin_name, config, project_dir, rid)
    out_path = get_obfuscated_build_path(plugin_name, config, project_dir, rid)
    plugin_path = os.path.join(get_interim_build_path(plugin_name, config, project_dir, rid), plugin_name + ".dll")
    dotnet_directory = get_dotnet_directory()
    obfuscar_xml_content = f'''<?xml version='1.0'?>
<Obfuscator>
	<Var name="InPath" value="{in_path}" />
	<Var name="OutPath" value="{out_path}" />
	<Var name="KeepPublicApi" value="true" />
	<Var name="HidePrivateApi" value="true" />
	<Var name="RenameProperties" value="true" />
	<Var name="RenameEvents" value="true" />
	<Var name="RenameFields" value="true" />
	<Var name="UseUnicodeNames" value="true" />
	<Var name="HideStrings" value="true" />
	<Var name="OptimizeMethods" value="true" />
	<Var name="SuppressIldasm" value="true" />
	<Module file="{plugin_path}" />
	<AssemblySearchPath path="{assembly_search_path}" />
    <AssemblySearchPath path="{dotnet_directory}" />
</Obfuscator>'''
    obfuscar_path = get_obfuscar_xml_path(plugin_name, project_dir)
    # Write obfuscar.xml to the specified path if it doesn't exist
    if os.path.exists(obfuscar_path):
        os.remove(obfuscar_path)

    with open(obfuscar_path, 'w') as xml_file:
        xml_file.write(obfuscar_xml_content)
    print(f"Default obfuscar.xml created at {obfuscar_path}")
    wait_for_file(in_path)

def run_obfuscator(obfuscar_exe_path, obfuscar_config_path):
    command = [
        obfuscar_exe_path,
        os.path.join(obfuscar_config_path, "obfuscar.xml"),
    ]
    subprocess.run(command, check=True)


def derive_obfuscation_seed(payload_uuid):
    digest = hashlib.sha256(payload_uuid.encode()).hexdigest()
    return int(digest, 16) & 0x7FFFFFFF


def should_obfuscate_assembly_identity(requested_names, plugin_name):
    names = {name.strip() for name in requested_names.split(",") if name.strip()}
    return plugin_name in names


def get_identity_obfuscator_paths():
    project = os.path.join(
        os.path.dirname(__file__),
        "Tools", "AssemblyNameObfuscator", "AssemblyNameObfuscator.csproj",
    )
    binary = os.path.join(
        os.path.dirname(project),
        "bin", "Release", "net10.0", "AssemblyNameObfuscator.dll",
    )
    return project, binary


def obfuscate_assembly_identity(assembly_path, payload_uuid):
    if not payload_uuid:
        raise ValueError("PayloadUUID is required to obfuscate assembly identities")

    project, binary = get_identity_obfuscator_paths()
    if not os.path.isfile(binary):
        subprocess.run(
            ["dotnet", "build", project, "-c", "Release", "--nologo"],
            check=True,
        )

    seed = derive_obfuscation_seed(payload_uuid)
    subprocess.run(
        ["dotnet", binary, assembly_path, str(seed)],
        check=True,
    )

def get_obfuscar_xml_path(plugin_name, project_dir):
    return os.path.join(project_dir,"obfuscar.xml")

def get_interim_build_path(plugin_name, config, project_dir, rid):
    if rid is not None:
        return os.path.join(project_dir, "obj", config, "net10.0", rid)
    
    return os.path.join(project_dir,"obj",config,"net10.0")

def get_obfuscated_build_path(plugin_name, config, project_dir, rid):
    return os.path.join(get_interim_build_path(plugin_name, config, project_dir, rid), "Obfuscated")

def get_plugin_dir(plugin_name, solution_dir):
    return os.path.join(solution_dir,plugin_name)

def get_obfuscar_exe_path():
    is_windows = platform.system().lower() == 'windows'
    if is_windows:
        return os.path.join(os.path.expanduser("~"),".dotnet", "tools", "obfuscar.console.exe")
    else:
        return os.path.join(os.path.expanduser("~"),".dotnet", "tools", "obfuscar.console")

def get_dotnet_directory():
    output = subprocess.run(
        ["dotnet", "--list-runtimes"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout
    candidates = []
    for line in output.splitlines():
        if not line.startswith("Microsoft.NETCore.App "):
            continue
        version, _, bracketed_root = line.removeprefix(
            "Microsoft.NETCore.App "
        ).partition(" ")
        try:
            numeric_version = tuple(int(part) for part in version.split("."))
        except ValueError:
            continue
        runtime_root = bracketed_root.strip()
        if (
            numeric_version
            and numeric_version[0] == 10
            and runtime_root.startswith("[")
            and runtime_root.endswith("]")
        ):
            candidates.append((numeric_version, version, runtime_root[1:-1]))
    if not candidates:
        raise RuntimeError("Microsoft.NETCore.App 10 runtime was not found")
    _, version, runtime_root = max(candidates)
    return os.path.join(runtime_root, version)

def build_model_dll(plugin_name, project_dir, configuration):
    #models_proj_path = os.path.join(project_dir.replace(plugin_name,""),"Agent.Models", "Agent.Models.csproj")
    models_proj_path = os.path.abspath(os.path.join(project_dir, os.pardir, "Agent.Models", "Agent.Models.csproj"))

    try:
        command = ["dotnet", "build", models_proj_path, "-c", "Release"]

        # Start the process asynchronously
        process = subprocess.Popen(command)

        # Wait for the process to complete
        process.wait()

        process.communicate()
    except Exception as e:
        print(f"Error during build: {e}")

def wait_for_file(file_path, timeout_seconds=60):
    start_time = time.time()

    while not os.path.exists(file_path):
        if time.time() - start_time > timeout_seconds:
            print(f"Timeout waiting for {file_path} to exist.")
            return False

        # Adjust the sleep duration based on your requirements
        time.sleep(1)

    print(f"{file_path} found.")
    return True

def skip_plugin(plugin_name, config, project_dir, rid):
    out_path = get_obfuscated_build_path(plugin_name, config, project_dir, rid)
    plugin_path = os.path.join(get_interim_build_path(plugin_name, config, project_dir, rid), plugin_name + ".dll")
    shutil.copy(plugin_path,out_path)

def main():
    # Check if the correct number of command-line arguments is provided
    if len(sys.argv) < 3:
        print("Usage: python script.py pluginName configuration")
        sys.exit(1)

    # Get command-line arguments
    plugin_name = sys.argv[1].replace('\'','')
    project_dir = os.getcwd()
    #solution_dir = sys.argv[2]
    configuration = sys.argv[2]
    
    rid = sys.argv[3] if len(sys.argv) >= 4 and sys.argv[3] else None
    payload_uuid = sys.argv[4] if len(sys.argv) >= 5 else ""
    obfuscated_assembly_names = sys.argv[5] if len(sys.argv) >= 6 else ""

    if plugin_name == "Agent.Managers.Python":
        skip_plugin(plugin_name, configuration, project_dir, rid)
        return
    
    # Create default obfuscar.xml
    create_obfuscar_xml(plugin_name, configuration, project_dir, rid)

    # Run Obfuscar, then rewrite the PE assembly identity so hot-loaded
    # command names are not visible through Assembly.GetName().
    run_obfuscator(get_obfuscar_exe_path(), project_dir)
    obfuscated_path = os.path.join(
        get_obfuscated_build_path(
            plugin_name, configuration, project_dir, rid
        ),
        plugin_name + ".dll",
    )
    if should_obfuscate_assembly_identity(obfuscated_assembly_names, plugin_name):
        obfuscate_assembly_identity(obfuscated_path, payload_uuid)

if __name__ == "__main__":
    main()
