internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.Write("before:");
        await Task.Delay(500);
        Console.WriteLine(args.Single());
        return 7;
    }
}
