namespace Obfuscator.Config;

public record ObfuscationConfig(
    int Seed,
    string? Uuid,
    string InputPath,
    string OutputPath,
    string? MapPath
)
{
    public Random CreateRandom() => new Random(Seed);
}
