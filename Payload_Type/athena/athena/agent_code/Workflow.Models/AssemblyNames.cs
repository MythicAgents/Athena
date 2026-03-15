namespace Workflow.Contracts
{
    public static class AssemblyNames
    {
        private const string VersionSuffix =
            ", Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";

        public static string ForModule(string name) => $"{name}{VersionSuffix}";

        public static string ForChannel(string name) =>
            $"Workflow.Channels.{name}{VersionSuffix}";
    }
}
