import sys
import os
import platform
import subprocess
import xml.etree.ElementTree as ET
import time

def create_obfuscar_xml(plugin_name, config, project_dir, rid):
    assembly_search_path = os.path.join(project_dir.replace(plugin_name,""),"Agent.Models", "bin",config,"net7.0")

    if(not os.path.exists(assembly_search_path)):
        print("!!!!!!!!!!!!! Building Agent.Models.dll !!!!!!!!!!!!!")
        try:
            build_model_dll(plugin_name, project_dir, config)
        except:
            wait_for_file(os.path.join(project_dir.replace(plugin_name,""),"Agent.Models", "bin",config,"net7.0","Agent.Models.dll"))

    in_path = get_interim_build_path(plugin_name, config, project_dir, rid)
    out_path = get_obfuscated_build_path(plugin_name, config, project_dir, rid)
    plugin_path = os.path.join(get_interim_build_path(plugin_name, config, project_dir, rid), plugin_name + ".dll")

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
</Obfuscator>'''
    obfuscar_path = get_obfuscar_xml_path(plugin_name, project_dir)
    # Write obfuscar.xml to the specified path if it doesn't exist
    if os.path.exists(obfuscar_path):
        os.remove(obfuscar_path)

    with open(obfuscar_path, 'w') as xml_file:
        xml_file.write(obfuscar_xml_content)
    print(f"Default obfuscar.xml created at {obfuscar_path}")

def run_obfuscator(obfuscar_exe_path, obfuscar_config_path):
    # Execute the obfuscator command
    try:
        command = [obfuscar_exe_path, os.path.join(obfuscar_config_path, "obfuscar.xml")]

        # Start the process asynchronously
        process = subprocess.Popen(command)

        # Wait for the process to complete
        process.wait()

        process.communicate()
    except Exception as e:
        print(f"Error during obfuscation: {e}")

def get_obfuscar_xml_path(plugin_name, project_dir):
    return os.path.join(project_dir,"obfuscar.xml")

def get_interim_build_path(plugin_name, config, project_dir, rid):
    if rid is not None:
        return os.path.join(project_dir, "obj", config, "net7.0", rid)
    
    return os.path.join(project_dir,"obj",config,"net7.0")

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

def build_model_dll(plugin_name, project_dir, configuration):
    models_proj_path = os.path.join(project_dir.replace(plugin_name,""),"Agent.Models", "Agent.Models.csproj")

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


def main():
    # Check if the correct number of command-line arguments is provided
    if len(sys.argv) < 4:
        print("Usage: python script.py pluginName solutionDir configuration")
        sys.exit(1)

    # Get command-line arguments
    plugin_name = sys.argv[1].replace('\'','')
    project_dir = os.getcwd()
    #solution_dir = sys.argv[2]
    configuration = sys.argv[3]
    
    if len(sys.argv) == 5:
        rid = sys.argv[4]
    else:
        rid = None

    # Create default obfuscar.xml
    create_obfuscar_xml(plugin_name, configuration, project_dir, rid)

    # Run obfuscator
    run_obfuscator(get_obfuscar_exe_path(), project_dir)

if __name__ == "__main__":
    main()