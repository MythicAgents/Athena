namespace Obfuscator.Config;

public record ObfuscationConfig(
    int Seed,
    string? Uuid,
    string InputPath,
    string OutputPath,
    string? MapPath,
    bool EnableBroadSemanticRename = false,
    string? ProjectRoot = null,
    string? Configuration = null,
    string? HandlerOS = null,
    string? CryptoProvider = null
)
{
    public Random CreateRandom() => new Random(Seed);
}
