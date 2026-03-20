using System.CommandLine;
using Obfuscator.Config;
using Obfuscator.IL;
using Obfuscator.Source;

var seedOption = new Option<int>("--seed")
{
    Description = "Random seed for deterministic obfuscation",
    Required = true
};

var uuidOption = new Option<string>("--uuid")
{
    Description = "Agent UUID for payload identification",
    Required = true
};

var inputOption = new Option<string>("--input")
{
    Description = "Input path (source directory or assembly)",
    Required = true
};

var outputOption = new Option<string>("--output")
{
    Description = "Output path for rewritten sources",
    Required = true
};

var mapOption = new Option<string?>("--map")
{
    Description = "Optional path to write the rename map JSON"
};

var rewriteSourceCommand = new Command(
    "rewrite-source",
    "Rewrite C# source files with obfuscation transforms")
{
    seedOption,
    uuidOption,
    inputOption,
    outputOption,
    mapOption
};

rewriteSourceCommand.SetAction((parseResult) =>
{
    var config = new ObfuscationConfig(
        Seed: parseResult.GetValue(seedOption),
        Uuid: parseResult.GetValue(uuidOption),
        InputPath: parseResult.GetValue(inputOption)!,
        OutputPath: parseResult.GetValue(outputOption)!,
        MapPath: parseResult.GetValue(mapOption));

    var rewriter = new SourceRewriter();
    rewriter.Rewrite(config);
});

var ilSeedOption = new Option<int>("--seed")
{
    Description = "Random seed for deterministic obfuscation",
    Required = true
};

var ilInputOption = new Option<string>("--input")
{
    Description = "Input assembly path",
    Required = true
};

var ilMapOption = new Option<string?>("--map")
{
    Description = "Optional path to write the rename map JSON"
};

var rewriteIlCommand = new Command(
    "rewrite-il",
    "Rewrite IL in a compiled assembly")
{
    ilSeedOption,
    ilInputOption,
    ilMapOption
};

rewriteIlCommand.SetAction((parseResult) =>
{
    var seed = parseResult.GetValue(ilSeedOption);
    var input = parseResult.GetValue(ilInputOption)!;
    var map = parseResult.GetValue(ilMapOption);

    var rewriter = new ILRewriter();
    rewriter.Rewrite(input, seed, map);
});

var batchSeedOption = new Option<int>("--seed")
{
    Description =
        "Random seed for deterministic obfuscation",
    Required = true
};

var batchDirOption = new Option<string>("--dir")
{
    Description =
        "Directory containing DLLs to process",
    Required = true
};

var batchMapOption = new Option<string?>("--map")
{
    Description =
        "Optional path to write the rename map JSON"
};

var batchSkipFileRenameOption =
    new Option<bool>("--skip-file-rename")
    {
        Description = "Skip renaming output files after IL rewriting"
    };

var batchSkipAssemblyRenameOption =
    new Option<bool>("--skip-assembly-rename")
    {
        Description = "Skip assembly identity renaming (required for single-file bundles)"
    };

var rewriteIlBatchCommand = new Command(
    "rewrite-il-batch",
    "Batch rewrite IL in all assemblies in a directory")
{
    batchSeedOption,
    batchDirOption,
    batchMapOption,
    batchSkipFileRenameOption,
    batchSkipAssemblyRenameOption
};

rewriteIlBatchCommand.SetAction((parseResult) =>
{
    var seed = parseResult.GetValue(batchSeedOption);
    var dir = parseResult.GetValue(batchDirOption)!;
    var map = parseResult.GetValue(batchMapOption);
    var skipFileRename =
        parseResult.GetValue(batchSkipFileRenameOption);
    var skipAssemblyRename =
        parseResult.GetValue(batchSkipAssemblyRenameOption);

    var rewriter = new ILRewriter();
    rewriter.RewriteBatch(dir, seed, map, skipFileRename, skipAssemblyRename);
});

var patchBundleSeedOption = new Option<int>("--seed")
{
    Description =
        "RNG seed (same seed used for the rest of the build)",
    Required = true
};

var patchBundleInputOption = new Option<string>("--input")
{
    Description =
        "Path to single-file bundle exe (modified in place)",
    Required = true
};

var patchBundleMapOption = new Option<string?>("--map")
{
    Description =
        "Optional path to write/merge assembly rename entries"
};

var patchBundleCommand = new Command(
    "patch-bundle",
    "Rename embedded assembly names in a single-file bundle exe")
{
    patchBundleSeedOption,
    patchBundleInputOption,
    patchBundleMapOption
};

patchBundleCommand.SetAction((parseResult) =>
{
    var seed  = parseResult.GetValue(patchBundleSeedOption);
    var input = parseResult.GetValue(patchBundleInputOption)!;
    var map   = parseResult.GetValue(patchBundleMapOption);

    new BundlePatcher(seed).Patch(input, map);
});

var rootCommand = new RootCommand("Athena obfuscation tool")
{
    rewriteSourceCommand,
    rewriteIlCommand,
    rewriteIlBatchCommand,
    patchBundleCommand
};

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();
