from importlib import import_module, invalidate_caches
from pathlib import Path
import glob

currentPath = Path(__file__)
searchPath = currentPath.parent / "trusted_sec_bofs" / "*.py"
modules = glob.glob(f"{searchPath}")
invalidate_caches()
for x in modules:
    if not x.endswith("__init__.py") and x[-3:] == ".py":
        module = import_module(f"{__name__}.trusted_sec_bofs." + Path(x).stem) 
        for el in dir(module):
            if "__" not in el:
                globals()[el] = getattr(module, el)

searchPath = currentPath.parent / "outflank_bofs" / "*.py"
modules = glob.glob(f"{searchPath}")
invalidate_caches()
for x in modules:
    if not x.endswith("__init__.py") and x[-3:] == ".py":
        module = import_module(f"{__name__}.outflank_bofs." + Path(x).stem) 
        for el in dir(module):
            if "__" not in el:
                globals()[el] = getattr(module, el)

searchPath = currentPath.parent / "trusted_sec_remote_bofs" / "*.py"
modules = glob.glob(f"{searchPath}")
invalidate_caches()
for x in modules:
    if not x.endswith("__init__.py") and x[-3:] == ".py":
        module = import_module(f"{__name__}.trusted_sec_remote_bofs." + Path(x).stem) 
        for el in dir(module):
            if "__" not in el:
                globals()[el] = getattr(module, el)

searchPath = currentPath.parent / "misc_bofs" / "*.py"
modules = glob.glob(f"{searchPath}")
invalidate_caches()
for x in modules:
    if not x.endswith("__init__.py") and x[-3:] == ".py":
        module = import_module(f"{__name__}.misc_bofs." + Path(x).stem) 
        for el in dir(module):
            if "__" not in el:
                globals()[el] = getattr(module, el)

searchPath = currentPath.parent / "nidhogg_commands" / "*.py"
modules = glob.glob(f"{searchPath}")
invalidate_caches()
for x in modules:
    if not x.endswith("__init__.py") and x[-3:] == ".py":
        module = import_module(f"{__name__}.nidhogg_commands." + Path(x).stem) 
        for el in dir(module):
            if "__" not in el:
                globals()[el] = getattr(module, el)

searchPath = currentPath.parent / "athena_messages" / "*.py"
modules = glob.glob(f"{searchPath}")
invalidate_caches()
for x in modules:
    if not x.endswith("__init__.py") and x[-3:] == ".py":
        module = import_module(f"{__name__}.athena_messages." + Path(x).stem) 
        for el in dir(module):
            if "__" not in el:
                globals()[el] = getattr(module, el)