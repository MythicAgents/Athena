from mythic_payloadtype_container.PayloadBuilder import *
from mythic_payloadtype_container.MythicCommandBase import *
import asyncio
import os
from distutils.dir_util import copy_tree
import tempfile

# define your payload type class here, it must extend the PayloadType class though
class Athena(PayloadType):

    name = "Athena"  # name that would show up in the UI
    file_extension = "dll"  # default file extension to use when creating payloads
    author = "@checkymander"  # author of the payload type
    supported_os = [
            SupportedOS.Windows,
            SupportedOS.Linux,
            SupportedOS.MacOS
    ]  # supported OS and architecture combos
    wrapper = False  # does this payload type act as a wrapper for another payloads inside of it?
    wrapped_payloads = []  # if so, which payload types. If you are writing a wrapper, you will need to modify this variable (adding in your wrapper's name) in the builder.py of each payload that you want to utilize your wrapper.
    note = """A cross platform .NET 5.0 compatible agent."""
    supports_dynamic_loading = True  # setting this to True allows users to only select a subset of commands when generating a payload
    build_parameters = {
        #  these are all the build parameters that will be presented to the user when creating your payload
        "version": BuildParameter(
            name="version",
            parameter_type=BuildParameterType.ChooseOne,
            description="Choose a target .NET Framework",
            choices=["4.0", "3.5"],
        ),
        "self-contained": BuildParameter(
            name="self-contained",
            parameter_type=BuildParameterType.ChooseOne,
            description="Select whether the payload will be self-contained or not",
            choices=["True", "False"],
        ),
        "trimmed": BuildParameter(
            name="trimmed",
            parameter_type=BuildParameterType.ChooseOne,
            description="Select whether the payload will be trimmed or not",
            choices=["True", "False"],
        ),
    }
    #  the names of the c2 profiles that your agent supports
    c2_profiles = ["http"]
    # after your class has been instantiated by the mythic_service in this docker container and all required build parameters have values
    # then this function is called to actually build the payload
    async def build(self) -> BuildResponse:
        resp = BuildResponse(status=BuildStatus.Error)
        copy_tree(self.agent_code_path, agent_build_path.name)
        configFile = open("{}/Athena/Config.cs".format(agent_build_path.name), "r").read()
        configFlie = configFile.replace("%UUID%", self.uuid)
        
        
        if self.selected_os == "macOS":
            print("Mac")
        elif self.selected_os == "windows":
            print("Windows")
        elif self.selected_os == "linux":
            print("Linux")
        # this function gets called to create an instance of your payload
        #resp = BuildResponse(status=BuildStatus.Error)
        resp = BuildResponse(status=BuildStatus.Success)
        return resp

