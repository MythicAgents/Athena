using System.Collections.Concurrent;
using System.Reflection;

namespace __OBFS_NS__
{
    internal static class __OBFS_CALLER_CLASS__
    {
        private static readonly ConcurrentDictionary<string, MethodInfo> _cache = new();

        internal static object? __OBFS_INVOKE_METHOD__(
            string typeName,
            string methodName,
            object? instance,
            object?[] args)
        {
            // Include arg types in key to correctly cache overloads
            // that differ only by parameter type (e.g. Assembly.Load
            // has string, AssemblyName, and byte[] overloads).
            var argSig = string.Join(",", args.Select(
                a => a?.GetType().FullName ?? "null"));
            var key =
                (instance == null ? "s:" : "i:")
                + typeName + "." + methodName
                + "(" + argSig + ")";
            var method = _cache.GetOrAdd(key, _ =>
            {
                var type = Type.GetType(
                    typeName, throwOnError: true)!;
                var methods = type.GetMethods(
                    BindingFlags.Public
                    | BindingFlags.Static
                    | BindingFlags.Instance
                    | BindingFlags.NonPublic);
                foreach (var m in methods)
                {
                    if (m.Name != methodName)
                        continue;
                    var ps = m.GetParameters();
                    if (ps.Length != args.Length)
                        continue;
                    var match = true;
                    for (var i = 0; i < ps.Length; i++)
                    {
                        if (args[i] != null
                            && !ps[i].ParameterType
                                .IsAssignableFrom(
                                    args[i]!.GetType()))
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match) return m;
                }
                throw new MissingMethodException(
                    typeName, methodName);
            });
            return method.Invoke(instance, args);
        }
    }
}
