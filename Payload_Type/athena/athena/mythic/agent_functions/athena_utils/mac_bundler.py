import os
import shutil
from plistlib import dump
import argparse

def create_app_bundle(app_name, executable_path, output_dir="."):
    # Create the directory structure
    bundle_path = os.path.join(output_dir, f"{app_name}.app")
    contents_path = os.path.join(bundle_path, "Contents")
    macos_path = os.path.join(contents_path, "MacOS")

    os.makedirs(macos_path, exist_ok=True)

    # Copy the .NET Core executable to the bundle
    shutil.copy(executable_path, os.path.join(macos_path, app_name))

    # Create the Info.plist file
    info_plist_content = {
        "CFBundleExecutable": app_name,
        "CFBundleInfoDictionaryVersion": "6.0",
        "CFBundlePackageType": "APPL",
        "CFBundleSignature": "????",
        "CFBundleVersion": "1.0",
    }

    info_plist_path = os.path.join(contents_path, "Info.plist")
    with open(info_plist_path, "wb") as plist_file:
        dump(info_plist_content, plist_file)

    print(f"Application bundle created at: {bundle_path}")
    return bundle_path