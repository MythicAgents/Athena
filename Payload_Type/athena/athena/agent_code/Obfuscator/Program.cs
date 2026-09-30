using System.CommandLine;
using Obfuscator.Config;
using Obfuscator.IL;
using Obfuscator.Source;

var rootCommand = new RootCommand("Athena obfuscation tool")
{
    BuildRewriteSourceCommand(),
    BuildRewriteIlCommand(),
    BuildRewriteIlBatchCommand(),
};

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();

static Option<int> CreateSeedOption() => new("--seed")
{
    Description = "Random seed for deterministic obfuscation",
    Required = true,
};

static Option<string?> CreateMapOption() => new("--map")
{
    Description = "Optional path to write the rename map JSON",
};

static Command BuildRewriteSourceCommand()
{
    var seedOption = CreateSeedOption();
    var uuidOption = new Option<string>("--uuid")
    {
        Description = "Agent UUID for payload identification",
        Required = true,
    };
    var inputOption = new Option<string>("--input")
    {
        Description = "Input path (source directory or assembly)",
        Required = true,
    };
    var outputOption = new Option<string>("--output")
    {
        Description = "Output path for rewritten sources",
        Required = true,
    };
    var mapOption = CreateMapOption();
    var broadSemanticRenameOption = new Option<bool>("--broad-semantic-rename")
    {
        Description = "Enable project-graph semantic renaming",
    };
    var projectRootOption = new Option<string?>("--project-root")
    {
        Description = "Root payload project, relative to the output workspace",
    };
    var configurationOption = new Option<string?>("--configuration")
    {
        Description = "Selected MSBuild Configuration",
    };
    var handlerOsOption = new Option<string?>("--handler-os")
    {
        Description = "Selected HandlerOS MSBuild property",
    };
    var cryptoProviderOption = new Option<string?>("--crypto-provider")
    {
        Description = "Selected CryptoProvider MSBuild property",
    };

    var command = new Command(
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
        cryptoProviderOption,
    };

    command.SetAction(parseResult =>
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

        new SourceRewriter().Rewrite(config);
    });

    return command;
}

static Command BuildRewriteIlCommand()
{
    var seedOption = CreateSeedOption();
    var inputOption = new Option<string>("--input")
    {
        Description = "Input assembly path",
        Required = true,
    };
    var mapOption = CreateMapOption();

    var command = new Command("rewrite-il", "Rewrite IL in a compiled assembly")
    {
        seedOption,
        inputOption,
        mapOption,
    };

    command.SetAction(parseResult =>
    {
        new ILRewriter().Rewrite(
            parseResult.GetValue(inputOption)!,
            parseResult.GetValue(seedOption),
            parseResult.GetValue(mapOption));
    });

    return command;
}

static Command BuildRewriteIlBatchCommand()
{
    var seedOption = CreateSeedOption();
    var dirOption = new Option<string>("--dir")
    {
        Description = "Directory containing DLLs to process",
        Required = true,
    };
    var mapOption = CreateMapOption();
    var skipFileRenameOption = new Option<bool>("--skip-file-rename")
    {
        Description = "Skip renaming output files after IL rewriting",
    };
    var skipAssemblyRenameOption = new Option<bool>("--skip-assembly-rename")
    {
        Description = "Skip assembly identity renaming (required for single-file bundles)",
    };
    var firstPartyAssemblyOption = new Option<string[]>("--first-party-assembly")
    {
        Description = "Managed assembly identity owned by the payload (repeatable)",
        Required = true,
        AllowMultipleArgumentsPerToken = true,
    };

    var command = new Command(
        "rewrite-il-batch",
        "Batch rewrite IL in all assemblies in a directory")
    {
        seedOption,
        dirOption,
        mapOption,
        firstPartyAssemblyOption,
        skipFileRenameOption,
        skipAssemblyRenameOption,
    };

    command.SetAction(parseResult =>
    {
        var firstPartyAssemblies = parseResult.GetValue(firstPartyAssemblyOption) ?? [];
        if (firstPartyAssemblies.Length == 0
            || firstPartyAssemblies.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "At least one non-empty --first-party-assembly is required.");
        }

        new ILRewriter().RewriteBatch(
            parseResult.GetValue(dirOption)!,
            parseResult.GetValue(seedOption),
            parseResult.GetValue(mapOption),
            firstPartyAssemblies,
            parseResult.GetValue(skipFileRenameOption),
            parseResult.GetValue(skipAssemblyRenameOption));
    });

    return command;
}
