using System.Collections.Concurrent;
using System.Reflection;

namespace __OBFS_NS__
{
    internal static class __OBFS_CALLER_CLASS__
    {
        private static readonly ConcurrentDictionary<string, Type> _typeCache = new();
        private static readonly ConcurrentDictionary<string, MethodInfo> _methodCache = new();

        internal static object? __OBFS_INVOKE_METHOD__(
            string typeName,
            string methodName,
            object? instance,
            Type[] parameterTypes,
            object?[] args)
        {
            return __OBFS_INVOKE_METHOD__(
                typeName,
                methodName,
                instance,
                parameterTypes,
                args,
                Enumerable.Range(0, args.Length).ToArray(),
                new bool[parameterTypes.Length]);
        }

        internal static object? __OBFS_INVOKE_METHOD__(
            string typeName,
            string methodName,
            object? instance,
            Type[] parameterTypes,
            object?[] args,
            int[] argumentOrdinals,
            bool[] parameterIsParams)
        {
            return __OBFS_INVOKE_METHOD__(
                typeName,
                methodName,
                instance,
                parameterTypes,
                args,
                argumentOrdinals,
                parameterIsParams,
                new bool[args.Length]);
        }

        internal static object? __OBFS_INVOKE_METHOD__(
            string typeName,
            string methodName,
            object? instance,
            Type[] parameterTypes,
            object?[] args,
            int[] argumentOrdinals,
            bool[] parameterIsParams,
            bool[] argumentIsExpandedParams)
        {
            // Runtime values cannot identify typed nulls. Preserve the
            // compiler-selected declared parameter types in both lookup
            // and cache identity instead.
            var argSig = string.Join(",", parameterTypes.Select(
                type => type.AssemblyQualifiedName));
            var key =
                (instance == null ? "s:" : "i:")
                + typeName + "." + methodName
                + "(" + argSig + ")";
            var method = _methodCache.GetOrAdd(key, _ =>
            {
                var type = _typeCache.GetOrAdd(typeName, ResolveLoadedType);
                return type.GetMethod(
                    methodName,
                    BindingFlags.Public
                    | BindingFlags.Static
                    | BindingFlags.Instance
                    | BindingFlags.NonPublic,
                    binder: null,
                    types: parameterTypes,
                    modifiers: null)
                    ?? throw new MissingMethodException(typeName, methodName);
            });
            if (args.Length != argumentOrdinals.Length)
                throw new ArgumentException(
                    "Each argument must have a bound parameter ordinal.",
                    nameof(argumentOrdinals));
            if (args.Length != argumentIsExpandedParams.Length)
                throw new ArgumentException(
                    "Each argument must have expanded-params metadata.",
                    nameof(argumentIsExpandedParams));
            if (parameterTypes.Length != parameterIsParams.Length)
                throw new ArgumentException(
                    "Each parameter must have params metadata.",
                    nameof(parameterIsParams));

            var boundArgs = Enumerable.Repeat<object?>(
                Type.Missing, parameterTypes.Length).ToArray();
            for (var parameterOrdinal = 0;
                 parameterOrdinal < parameterTypes.Length;
                 parameterOrdinal++)
            {
                var sourceIndexes = argumentOrdinals
                    .Select((ordinal, sourceIndex) => (ordinal, sourceIndex))
                    .Where(entry => entry.ordinal == parameterOrdinal)
                    .Select(entry => entry.sourceIndex)
                    .ToArray();
                if (!parameterIsParams[parameterOrdinal])
                {
                    if (sourceIndexes.Length == 1)
                        boundArgs[parameterOrdinal] = args[sourceIndexes[0]];
                    continue;
                }

                var parameterType = parameterTypes[parameterOrdinal];
                if (sourceIndexes.Length == 1
                    && !argumentIsExpandedParams[sourceIndexes[0]]
                    && (args[sourceIndexes[0]] is null
                        || parameterType.IsInstanceOfType(args[sourceIndexes[0]])))
                {
                    boundArgs[parameterOrdinal] = args[sourceIndexes[0]];
                    continue;
                }

                var elementType = parameterType.GetElementType()
                    ?? throw new InvalidOperationException(
                        "A params parameter must be an array type.");
                var array = Array.CreateInstance(elementType, sourceIndexes.Length);
                for (var index = 0; index < sourceIndexes.Length; index++)
                    array.SetValue(args[sourceIndexes[index]], index);
                boundArgs[parameterOrdinal] = array;
            }

            return method.Invoke(instance, boundArgs);
        }

        private static Type ResolveLoadedType(string typeName)
        {
            Type? match = null;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            Array.Sort(assemblies, (left, right) => string.CompareOrdinal(
                left.FullName, right.FullName));

            foreach (var assembly in assemblies)
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types
                        .Where(type => type is not null)
                        .Cast<Type>()
                        .ToArray();
                }

                foreach (var type in types)
                {
                    if (!string.Equals(
                            type.FullName, typeName,
                            StringComparison.Ordinal))
                        continue;
                    if (match is not null && match != type)
                        throw new AmbiguousMatchException(
                            "Multiple loaded types match '" + typeName + "'.");
                    match = type;
                }
            }

            return match ?? throw new TypeLoadException(
                "Could not resolve loaded type '" + typeName + "'.");
        }
    }
}
