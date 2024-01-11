import struct
import subprocess

class OfArg:
    def __init__(self, arg_data, arg_type):
        self.arg_data = arg_data
        self.arg_type = arg_type

def generateWString(arg):
    return OfArg(arg.encode('utf-16le') + b'\x00\x00', 0)

def generateString(arg):
    return OfArg(arg.encode('ascii') + b'\x00', 0)

def generate32bitInt(arg):
    return OfArg(struct.pack('<I', int(arg)), 1)

def generate16bitInt(arg):
    return OfArg(struct.pack('<H', int(arg)), 2)

def generateBinary(arg):
    return OfArg(arg, 0)

def SerializeArgs(OfArgs):
    output_bytes = b''
    for of_arg in OfArgs:
        output_bytes += struct.pack('<I', of_arg.arg_type)
        output_bytes += struct.pack('<I', len(of_arg.arg_data))
        output_bytes += of_arg.arg_data
    return output_bytes

async def compile_bof(bof_path):
    p = subprocess.Popen(["make"], cwd=bof_path)
    p.wait()
    streamdata = p.communicate()[0]
    rc = p.returncode
    if rc != 0:
        raise Exception("Error compiling BOF: " + str(streamdata))