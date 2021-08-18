from mythic_payloadtype_container.PayloadBuilder import *
from mythic_payloadtype_container.MythicCommandBase import *
import asyncio
import os
import sys
from distutils.dir_util import copy_tree
import tempfile
import shutil
import traceback

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
            description="Indicate whether the payload will include the full .NET framework. Default: True",
            default_value = "True",
            choices=["True", "False"],
        ),
        "trimmed": BuildParameter(
            name="trimmed",
            parameter_type=BuildParameterType.ChooseOne,
            description="Indicate whether the payload will trim unecessary assemblies. Note: This will decrease the file size, while making reflection slightly more difficult. Default: False",
            default_value = "False",
            choices=["True", "False"],
        ),
        "single-file": BuildParameter(
            name="single-file",
            parameter_type=BuildParameterType.ChooseOne,
            description="Indicate whether the file returned will be published as a single-file executable or not. Default: True",
            default_value = "False",
            choices=["True", "False"],
        ),
        "arch": BuildParameter(
            name="arch", 
            parameter_type=BuildParameterType.ChooseOne,
            choices=["x64", "x86","amd64","AnyCPU"], 
            default_value="x64", 
            description="Target architecture"
        ),
        "obfuscate": BuildParameter(
            name="obfuscate",
            parameter_type=BuildParameterType.ChooseOne,
            description="Obfuscate the payload using ConfuserEx. Default: False",
            default_value = "False",
            choices=["True", "False"],
        ),
        
        "default_proxy": BuildParameter(
        name="default_proxy", 
        parameter_type=BuildParameterType.String,
        default_value="false", required=False,
        description="Use the default proxy on the system, either true or false"),
    }
    #  the names of the c2 profiles that your agent supports
    c2_profiles = ["http"]
    # after your class has been instantiated by the mythic_service in this docker container and all required build parameters have values
    # then this function is called to actually build the payload
    async def build(self) -> BuildResponse:
        #self.Get_Parameter returns the values specified in the build_parameters above.
        resp = BuildResponse(status=BuildStatus.Error)
        config_files_map = {
            "Config.cs": {
                "callback_interval": "",
                "callback_jitter": "",
                "callback_port": "",
                "callback_host": "",
                "domain_front": "",
                "encrypted_exchange_check": "",
                "%UUID%": self.uuid,
                "AESPSK": "",
            },
            "SMBServerProfile.cs": {
                "pipe_name": "",
                "%UUID%": self.uuid,
                "AESPSK": "",
            },
            "Agent.cs": {
                "%UUID%": self.uuid
            }
        }
        agent_build_path = tempfile.TemporaryDirectory(suffix=self.uuid)

        copy_tree(self.agent_code_path, agent_build_path.name)
        print("{}/Athena/Config.cs".format(agent_build_path.name))
        configFile = open("{}/Athena/Config.cs".format(agent_build_path.name), "r").read()
        configFlie = configFile.replace("%UUID%", self.uuid)
        configFile = configFile.replace("%UUID%", self.uuid)
        #configFile = configFile.replace('%CHUNK_SIZE%', self.get_parameter('chunk_size'))
        configFile = configFile.replace('%DEFAULT_PROXY%', self.get_parameter('default_proxy'))
        
        for c2 in self.c2info:
            profile = c2.get_c2profile()
            print(profile["name"])
            if profile["name"] == "http":
                for key, val in c2.get_parameters_dict().items():
                        if isinstance(val, dict):
                            config_files_map["Config.cs"][key] = val["enc_key"] if val["enc_key"] is not None else ""
                        elif isinstance(val, list):
                            for item in val:
                                # I should probably print out a list of keys to see what they look like when coming from mythic and update these values in the config.cs (or figure out how to append and prepend % to the values
                                print(key)
                                print(val)
                                #Check to see if we have a list of Dictionaries
                                if not isinstance(item, dict):
                                    raise Exception("Expected a list of dictionaries, but got {}".format(type(item)))
                                    
                                    
                                # Update Domain Fronting Header
                                if item["key"] == "Host":
                                    config_files_map["Config.cs"]["%HOSTHEADER%"] = item["value"]
                                # Update 
                                elif item["key"] == "User-Agent":
                                    config_files_map["Config.cs"]["%USERAGENT%"] = item["value"]
                                else:
                                    #Gonna have to figure out how to replace this
                                    config_files_map["Config.cs"][item["key"]] = item["value"]
                        elif isinstance(val, str):
                            config_files_map["Config.cs"][key] = val
                        else:
                            config_files_map["Config.cs"][key] = json.dumps(val)
            elif profile["name"] == "SMBServer":
                for key, val in c2.get_parameters_dict().items():
                    config_files_map["SMBServerProfile.cs"][key] = val
            elif profile["name"] == "SMBClient":
                pass
            else:
                raise Exception("Unsupported C2 profile type for Athena: {}".format(profile["name"]))
        try:
            # make a Temporary Directory for the payload files
            
            #Copy files into the temp directory
            copy_tree(self.agent_code_path, agent_build_path.name)
            
            #Rewrite the config.cs with the proper values assigned above.
            
            ##########
            #
            #   TODO
            #
            ##########
            
            #Apollo splits the cs files into 3 separate ones, grabs each one, and replaces the appropriate values from the json dump specified in the beginning.
            
            command = "nuget restore; dotnet publish"
            output_path = agent_build_path.name + "/Athena/bin/Release/net5.0/"
            
            
            #output path = 
            
            #Add command for creating a mac OS payload
            if self.selected_os == "macOS":
                print("Mac")
                if self.get_parameter("arch") == "x64":
                    output_path += "osx-x64/publish/"
                    command+= " -r osx-x64"
                elif self.get_parameter("arch") == "arm64":
                    output_path += "osx.11.0-arm64/publish/"
                    command+= " -r osx.11.0-arm64"
                else:
                    resp.payload = b""
                    resp.status = BuildStatus.Error
                    resp.build_message = "Architecture selected for MacOS not supported"
            
            
            #We're creating a windows payloads
            elif self.selected_os == "Windows":
                print("Windows")
                #C:\Users\checkymander\source\repos\Athena\Payload_Type\Athena\agent_code\Athena\bin\Release\net5.0\win-x64\Athena.dll
                if self.get_parameter("arch") == "x64":
                    output_path += "win-x64/publish/"
                    command+= " -r win-x64"
                elif self.get_parameter("arch") == "x86":
                    output_path += "win-x86/publish/"
                    command+= " -r win-x86"
                elif self.get_parameter("arch") == "arm64":
                    output_path += "win-arm64/publish/"
                    command+= " -r win-arm64"
                elif self.get_parameter("arch") == "arm":
                    output_path += "win-arm/publish/"
                    command+= " -r win-arm"
                else:
                    resp.payload = b""
                    resp.status = BuildStatus.Error
                    resp.build_message = "Architecture selected for Windows not supported"
            
            
            #We're creating a linux payload
            elif self.selected_os == "Linux":
                print("Nix")
                if self.get_parameter("arch") == "x64":
                    output_path += "linux-x64/publish/"
                    command+= " -r linux-x64"
                elif self.get_parameter("arch") == "arm":
                    output_path += "linux-arm/publish/"
                    command+= " -r linux-arm"
                elif self.get_parameter("arch") == "arm64":
                    output_path += "linux-arm64/publish/"
                    command+= " -r linux-arm64"
           
            command += " -c Release"
            
            
            if self.get_parameter("self-contained") == "True":
                command+= " --self-contained true"
            
            if self.get_parameter("single-file") == "True":
                command+= " /p:PublishSingleFile=true"
            
            # File size comes out to 28mb trimmed x.x
            # File size comes out to 60mb untrimmed
            if self.get_parameter("trummed") == "True":
                command+= " /p:PublishTrimmed=true"
            
            # Run the build command
            print("Running Build Command")
            proc = await asyncio.create_subprocess_shell(command, stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.PIPE, 
            cwd=agent_build_path.name)
            stdout, stderr = await proc.communicate()
            stdout_err = ""
            if stdout:
                stdout_err += f'[stdout]\n{stdout.decode()}\n'
            if stderr:
                stdout_err += f'[stderr]\n{stderr.decode()}' + "\n" + command
            # Check to see if the build worked
            
            if os.path.exists(output_path):
                #Build worked, return payload
                resp.status = BuildStatus.Success
                shutil.make_archive(f"{agent_build_path}/", "zip", "/build")
                resp.payload = open(output_path + ".zip", 'rb').read()
                resp.message = success_message
            else:
                #Build Failed, return error message
                resp.status = BuildStatus.Error
                resp.payload = b""
                resp.build_message = stdout_err
                resp.build_stderr = stdout_err
            sys.stdout.flush()    
        except:
            #An error occured, return the error
            resp.payload = b""
            resp.status = BuildStatus.Error
            resp.build_message = "Error building payload: " + str(traceback.format_exc())

        return resp

