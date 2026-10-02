using AssemblyNameObfuscator;

if (args.Length == 2 && int.TryParse(args[1], out var legacySeed))
{
    Console.WriteLine(AssemblyIdentityRenamer.Rewrite(args[0], legacySeed));
    return 0;
}

if (args.Length == 3 && int.TryParse(args[2], out var seed))
{
    if (args[0] == "patch-bundle")
    {
        new BundlePatcher(seed).Patch(args[1]);
        return 0;
    }
    if (args[0] == "rewrite-dir")
    {
        RewriteDirectory(args[1], seed);
        return 0;
    }
}

Console.Error.WriteLine("Usage: AssemblyNameObfuscator <assembly-path> <seed> | patch-bundle|rewrite-dir <path> <seed>");
return 2;

static void RewriteDirectory(string directory, int seed)
{
    var runtimeConfig = Directory.GetFiles(directory, "*.runtimeconfig.json").SingleOrDefault();
    var entryAssembly = runtimeConfig is null
        ? null
        : Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(runtimeConfig));
    var renamed = new AssemblyIdentityRenamer(seed).RenameAll(
        directory,
        extraSkipNames: entryAssembly is null ? null : [entryAssembly]);
    foreach (var depsPath in Directory.GetFiles(directory, "*.deps.json"))
        PatchDepsJson(depsPath, renamed);
    Console.WriteLine($"Renamed {renamed.Count} assemblies.");
}

static void PatchDepsJson(string depsPath, IReadOnlyDictionary<string, string> renamed)
{
    var deps = File.ReadAllText(depsPath);
    foreach (var (original, replacement) in renamed)
    {
        deps = deps.Replace($"\"{original}/", $"\"{replacement}/");
        deps = deps.Replace($"\"{original}.dll\"", $"\"{replacement}.dll\"");
    }
    File.WriteAllText(depsPath, deps);
}
