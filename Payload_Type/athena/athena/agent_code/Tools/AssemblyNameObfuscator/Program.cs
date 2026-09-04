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
        var runtimeConfig = Directory.GetFiles(args[1], "*.runtimeconfig.json").SingleOrDefault();
        var entryAssembly = runtimeConfig is null
            ? null
            : Path.GetFileNameWithoutExtension(
                Path.GetFileNameWithoutExtension(runtimeConfig));
        var renamed = new AssemblyIdentityRenamer(seed).RenameAll(
            args[1],
            extraSkipNames: entryAssembly is null ? null : [entryAssembly]);
        foreach (var depsPath in Directory.GetFiles(args[1], "*.deps.json"))
        {
            var deps = File.ReadAllText(depsPath);
            foreach (var (original, replacement) in renamed)
            {
                deps = deps.Replace($"\"{original}/", $"\"{replacement}/");
                deps = deps.Replace($"\"{original}.dll\"", $"\"{replacement}.dll\"");
            }
            File.WriteAllText(depsPath, deps);
        }
        Console.WriteLine($"Renamed {renamed.Count} assemblies.");
        return 0;
    }
}

Console.Error.WriteLine("Usage: AssemblyNameObfuscator <assembly-path> <seed> | patch-bundle|rewrite-dir <path> <seed>");
return 2;
