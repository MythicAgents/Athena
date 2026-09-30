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

var broadSemanticRenameOption = new Option<bool>("--broad-semantic-rename")
{
    Description = "Enable project-graph semantic renaming"
};

var projectRootOption = new Option<string?>("--project-root")
{
    Description = "Root payload project, relative to the output workspace"
};

var configurationOption = new Option<string?>("--configuration")
{
    Description = "Selected MSBuild Configuration"
};

var handlerOsOption = new Option<string?>("--handler-os")
{
    Description = "Selected HandlerOS MSBuild property"
};

var cryptoProviderOption = new Option<string?>("--crypto-provider")
{
    Description = "Selected CryptoProvider MSBuild property"
};

var rewriteSourceCommand = new Command(
    "rewrite-source",
    "Rewrite C# source files with obfuscation transforms")
{
    seedOption,
    uuidOption,
    inputOption,
    outputOption,
    mapOption,
    broadSemanticRenameOption,
    projectRootOption,
    configurationOption,
    handlerOsOption,
    cryptoProviderOption
};

rewriteSourceCommand.SetAction((parseResult) =>
{
    var uuid = parseResult.GetValue(uuidOption);
    if (!UuidRenameMap.TryNormalizeUuid(uuid, out var normalizedUuid))
        throw new ArgumentException("--uuid must be a valid UUID.");

    var enableBroadSemanticRename = parseResult.GetValue(broadSemanticRenameOption);
    var projectRoot = parseResult.GetValue(projectRootOption);
    var configuration = parseResult.GetValue(configurationOption);
    var handlerOs = parseResult.GetValue(handlerOsOption);
    var cryptoProvider = parseResult.GetValue(cryptoProviderOption);
    if (enableBroadSemanticRename)
    {
        var missing = new[]
        {
            ("--project-root", projectRoot),
            ("--configuration", configuration),
            ("--handler-os", handlerOs),
            ("--crypto-provider", cryptoProvider),
        }.Where(option => string.IsNullOrWhiteSpace(option.Item2))
            .Select(option => option.Item1)
            .ToArray();
        if (missing.Length != 0)
            throw new ArgumentException(
                "--broad-semantic-rename requires " + string.Join(", ", missing) + ".");
    }

    var config = new ObfuscationConfig(
        Seed: parseResult.GetValue(seedOption),
        Uuid: normalizedUuid,
        InputPath: parseResult.GetValue(inputOption)!,
        OutputPath: parseResult.GetValue(outputOption)!,
        MapPath: parseResult.GetValue(mapOption),
        EnableBroadSemanticRename: enableBroadSemanticRename,
        ProjectRoot: projectRoot,
        Configuration: configuration,
        HandlerOS: handlerOs,
        CryptoProvider: cryptoProvider);

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

var batchFirstPartyAssemblyOption =
    new Option<string[]>("--first-party-assembly")
    {
        Description = "Managed assembly identity owned by the payload (repeatable)",
        Required = true,
        AllowMultipleArgumentsPerToken = true,
    };

var rewriteIlBatchCommand = new Command(
    "rewrite-il-batch",
    "Batch rewrite IL in all assemblies in a directory")
{
    batchSeedOption,
    batchDirOption,
    batchMapOption,
    batchFirstPartyAssemblyOption,
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
    var firstPartyAssemblies =
        parseResult.GetValue(batchFirstPartyAssemblyOption) ?? [];
    if (firstPartyAssemblies.Length == 0
        || firstPartyAssemblies.Any(string.IsNullOrWhiteSpace))
        throw new ArgumentException(
            "At least one non-empty --first-party-assembly is required.");

    var rewriter = new ILRewriter();
    rewriter.RewriteBatch(
        dir, seed, map, firstPartyAssemblies,
        skipFileRename, skipAssemblyRename);
});

var rootCommand = new RootCommand("Athena obfuscation tool")
{
    rewriteSourceCommand,
    rewriteIlCommand,
    rewriteIlBatchCommand
};

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();
