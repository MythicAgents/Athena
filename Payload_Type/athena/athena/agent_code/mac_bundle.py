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

    return bundle_path

def main():
    parser = argparse.ArgumentParser(description="Create a macOS application bundle for a .NET Core executable.")
    parser.add_argument("app_name", help="Name of the application")
    parser.add_argument("executable_path", help="Path to the .NET Core executable")
    parser.add_argument("--output_dir", help="Output directory for the application bundle", default=".")

    args = parser.parse_args()

    app_bundle_path = create_app_bundle(args.app_name, args.executable_path, args.output_dir)
    print(f"Application bundle created at: {app_bundle_path}")

if __name__ == "__main__":
    main()