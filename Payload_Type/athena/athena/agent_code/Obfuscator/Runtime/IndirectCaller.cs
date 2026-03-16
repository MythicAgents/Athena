using System.Collections.Concurrent;
using System.Reflection;

namespace __OBFS_NS__
{
    internal static class __OBFS_CALLER_CLASS__
    {
        private static readonly ConcurrentDictionary<string, MethodInfo> _cache = new();

        internal static object? __OBFS_INVOKE_METHOD__(
            string typeName, string methodName, object?[] args)
        {
            var key = typeName + "." + methodName;
            var method = _cache.GetOrAdd(key, _ =>
            {
                var type = Type.GetType(typeName, throwOnError: true)!;
                var methods = type.GetMethods(
                    BindingFlags.Public | BindingFlags.Static |
                    BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (var m in methods)
                {
                    if (m.Name == methodName &&
                        m.GetParameters().Length == args.Length)
                        return m;
                }
                throw new MissingMethodException(typeName, methodName);
            });
            return method.Invoke(null, args);
        }
    }
}
