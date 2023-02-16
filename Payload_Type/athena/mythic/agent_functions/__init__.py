currentPath = Path(__file__)
searchPath = currentPath.parent / "agent_functions" / "test_bofs" / ".py"
modules = glob.glob(f"{searchPath}")
invalidate_caches()
for x in modules:
    if not x.endswith("__init__.py") and x[-3:] == ".py":
        module = import_module(f"{__name__}.agent_functions." + Path(x).stem)
        for el in dir(module):
            if "__" not in el:
                globals()[el] = getattr(module, el)