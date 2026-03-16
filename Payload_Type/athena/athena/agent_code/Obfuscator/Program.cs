using System.CommandLine;
using Obfuscator.Config;
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

    Console.WriteLine($"rewrite-il: seed={seed}");
    Console.WriteLine($"  input={input}");
    Console.WriteLine($"  map={map ?? "(none)"}");
});

var rootCommand = new RootCommand("Athena obfuscation tool")
{
    rewriteSourceCommand,
    rewriteIlCommand
};

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();
