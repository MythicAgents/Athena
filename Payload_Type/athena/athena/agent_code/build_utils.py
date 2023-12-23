import sys
import os
import platform
import xml.etree.ElementTree as ET

def create_obfuscar_xml(plugin_name, config, solution_dir):
    assembly_search_path = os.path.join(solution_dir,"Agent.Models","bin",config,"net7.0")
    in_path = get_interim_build_path(plugin_name, config, solution_dir)
    out_path = get_obfuscated_build_path(plugin_name, config, solution_dir)
    plugin_path = os.path.join(get_interim_build_path(plugin_name, config, solution_dir), plugin_name + ".dll")

    obfuscar_xml_content = f'''
<?xml version='1.0'?>
<Obfuscator>
	<Var name="InPath" value="{in_path}" />
	<Var name="OutPath" value="{out_path}" />
	<Var name="KeepPublicApi" value="false" />
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
</Obfuscator>
    '''
    obfuscar_path = get_obfuscar_xml_path(plugin_name, solution_dir)
    # Write obfuscar.xml to the specified path if it doesn't exist
    if not os.path.exists(obfuscar_path):
        with open(obfuscar_path, 'w') as xml_file:
            xml_file.write(obfuscar_xml_content)
        print(f"Default obfuscar.xml created at {obfuscar_path}")

def run_obfuscator(obfuscar_exe_path, obfuscar_config_path):
    # Determine the platform (Windows or Linux)
    is_windows = platform.system().lower() == 'windows'

    # Construct the obfuscator command based on the platform
    obfuscator_command = f'{obfuscar_exe_path} {os.path.join(obfuscar_config_path, "obfuscar.xml")}'

    # Execute the obfuscator command
    try:
        os.system(obfuscator_command)
        print("Obfuscation completed successfully.")
    except Exception as e:
        print(f"Error during obfuscation: {e}")

def get_obfuscar_xml_path(plugin_name, solution_dir):
    return os.path.join(solution_dir,plugin_name,"obfuscar.xml")

def get_interim_build_path(plugin_name, config, solution_dir):
    return os.path.join(solution_dir,plugin_name,"obj",config,"net7.0")

def get_obfuscated_build_path(plugin_name, config, solution_dir):
    return os.path.join(get_interim_build_path(plugin_name, config, solution_dir), "Obfuscated")

def get_plugin_dir(plugin_name, solution_dir):
    return os.path.join(solution_dir,plugin_name)

def main():
    # Check if the correct number of command-line arguments is provided
    if len(sys.argv) != 4:
        print("Usage: python script.py pluginName solutionDir configuration")
        sys.exit(1)

    # Get command-line arguments
    plugin_name = sys.argv[1]
    solution_dir = sys.argv[2]
    configuration = sys.argv[3]

    # Create default obfuscar.xml
    create_obfuscar_xml(plugin_name, configuration, solution_dir)

    # Run obfuscator
    run_obfuscator(plugin_name, solution_dir)

if __name__ == "__main__":
    main()