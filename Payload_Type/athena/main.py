import mythic_container
from athena.mythic import *
import subprocess
import os

def prepare_agent_obfuscation():
    agent_code_dir = "/Mythic/athena/agent_code/"

def find_csproj_files(directory) -> list[str]:
    """
    Find all .csproj files in the given directory and its subdirectories.
    
    :param directory: The root directory to search
    :return: A list of file paths ending with .csproj
    """
    csproj_files = []
    for root, dirs, files in os.walk(directory):
        for file in files:
            if file.endswith(".csproj"):
                csproj_files.append(os.path.join(root, file))
    return csproj_files

def read_replacement_text(file_path) -> str:
    """
    Read the text from the given file which will be used to replace the placeholder.
    
    :param file_path: Path to the file containing the replacement text
    :return: Text content of the file
    """
    with open(file_path, 'r') as f:
        return f.read()

def replace_placeholder_in_file(file_path, placeholder, replacement_text):
    """
    Replace the placeholder in the given .csproj file with the replacement text.
    
    :param file_path: The .csproj file path
    :param placeholder: The placeholder string to be replaced
    :param replacement_text: The text that will replace the placeholder
    """
    with open(file_path, 'r') as f:
        content = f.read()
    
    new_content = content.replace(placeholder, replacement_text)

    with open(file_path, 'w') as f:
        f.write(new_content)

def process_csproj_files(directory, placeholder):
    """
    Find all .csproj files in a directory, read replacement text from a file,
    and replace placeholders in the .csproj files.
    
    :param directory: The directory to search for .csproj files
    :param placeholder: The placeholder text to search and replace
    :param replacement_file: The file containing the replacement text
    """
    csproj_files = find_csproj_files(directory)
    replacement_text = read_replacement_text("common.obfs")
    agent_replacement_text = read_replacement_text("agent.obfs")
    
    for csproj_file in csproj_files:
        if csproj_file.lower().endswith("AthenaCore.csproj"):
            replace_placeholder_in_file(csproj_file, placeholder, agent_replacement_text)
        else:
            replace_placeholder_in_file(csproj_file, placeholder, replacement_text)

directory = "/Mythic/athena/agent_code/"

placeholder = "<!-- Obfuscation Replacement Placeholder Do Not Remove -->"

process_csproj_files(directory, placeholder)

mythic_container.mythic_service.start_and_run_forever()