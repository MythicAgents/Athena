using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Obfuscator.IL.Transforms;

public sealed record AssemblyRenameFile(string OldPath, string NewPath, byte[] Bytes);

public sealed class AssemblyRenamePlan
{
    public AssemblyRenamePlan(
        Dictionary<string, string> renameMap,
        IReadOnlyList<AssemblyRenameFile> files)
    {
        RenameMap = renameMap;
        Files = files;
    }

    public Dictionary<string, string> RenameMap { get; }
    public IReadOnlyList<AssemblyRenameFile> Files { get; }
}

public sealed class AssemblyRenameTransform
{
    private const string AlphaNumChars =
        "abcdefghijklmnopqrstuvwxyz0123456789"
        + "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private readonly int _seed;
    private readonly Func<AssemblyDefinition, byte[]> _emit;

    public AssemblyRenameTransform(int seed) : this(seed, Emit)
    {
    }

    internal AssemblyRenameTransform(
        int seed,
        Func<AssemblyDefinition, byte[]> emit)
    {
        _seed = seed;
        _emit = emit;
    }

    public Dictionary<string, string> RenameAll(
        string directory,
        IReadOnlyCollection<string> allowedAssemblyNames,
        IReadOnlyCollection<string> excludedRenameNames,
        bool skipFileRename = false)
    {
        var plan = Prepare(
            directory, allowedAssemblyNames, excludedRenameNames,
            skipFileRename);
        FileRewriteTransaction.Commit(plan.Files.Select(file =>
            new FileRewrite(file.OldPath, file.NewPath, file.Bytes)));
        return plan.RenameMap;
    }

    public AssemblyRenamePlan Prepare(
        string directory,
        IReadOnlyCollection<string> allowedAssemblyNames,
        IReadOnlyCollection<string> excludedRenameNames,
        bool skipFileRename = false,
        IReadOnlyDictionary<string, byte[]>? preparedBytes = null)
    {
        ArgumentNullException.ThrowIfNull(allowedAssemblyNames);
        ArgumentNullException.ThrowIfNull(excludedRenameNames);

        directory = Path.GetFullPath(directory);
        var allowed = new HashSet<string>(
            allowedAssemblyNames, StringComparer.OrdinalIgnoreCase);
        var excluded = new HashSet<string>(
            excludedRenameNames, StringComparer.OrdinalIgnoreCase);
        var renameMap = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        var assemblies = new Dictionary<string, AssemblyInput>(
            StringComparer.OrdinalIgnoreCase);

        var dllFiles = Directory.GetFiles(directory, "*.dll");
        Array.Sort(dllFiles, StringComparer.Ordinal);
        foreach (var dllPath in dllFiles)
        {
            var fullPath = Path.GetFullPath(dllPath);
            var bytes = preparedBytes is not null
                && preparedBytes.TryGetValue(fullPath, out var supplied)
                    ? supplied : File.ReadAllBytes(fullPath);
            if (PeFileClassifier.Classify(bytes, fullPath) == PeFileKind.Native)
                continue;

            try
            {
                using var stream = new MemoryStream(bytes);
                using var asm = AssemblyDefinition.ReadAssembly(stream);
                var identity = asm.Name.Name;
                if (!assemblies.TryAdd(identity, new AssemblyInput(fullPath, bytes)))
                    throw new InvalidDataException(
                        $"Duplicate managed assembly identity '{identity}' was found in "
                        + $"'{Path.GetFileName(assemblies[identity].Path)}' and "
                        + $"'{Path.GetFileName(fullPath)}'.");
                if (allowed.Contains(identity) && !excluded.Contains(identity)
                    && !HasSelfAssemblyQualifiedTypeName(asm, identity))
                    renameMap.Add(identity, GenerateName(identity));
            }
            catch (BadImageFormatException ex)
            {
                throw PeFileClassifier.InvalidImage(fullPath, ex);
            }
            catch (AssemblyResolutionException ex)
            {
                throw ResolutionFailure(fullPath, ex);
            }
        }

        // Direct Prepare/RenameAll callers must receive the same fail-closed guarantee
        // before any Cecil object is changed or emitted.
        foreach (var identity in allowed.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            if (assemblies.TryGetValue(identity, out var input))
                CliSignatureSafety.Validate(input.Bytes, input.Path);

        var generatedCollision = renameMap
            .GroupBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (generatedCollision is not null)
            throw new InvalidDataException(
                $"Multiple assemblies would be renamed to '{generatedCollision.Key}'.");

        var paths = new HashSet<string>(
            Directory.GetFiles(directory).Select(Path.GetFullPath),
            StringComparer.OrdinalIgnoreCase);
        var sourcePaths = new HashSet<string>(
            assemblies.Values.Select(input => input.Path),
            StringComparer.OrdinalIgnoreCase);
        var physicalMoves = new List<(string Source, string Destination)>();
        if (!skipFileRename)
        {
            foreach (var (identity, newName) in renameMap)
            {
                var source = assemblies[identity].Path;
                var destination = Path.Combine(directory, newName + ".dll");
                if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (sourcePaths.Contains(destination))
                    throw new InvalidDataException(
                        $"Assembly rename cycle targets existing source '{Path.GetFileName(destination)}'.");
                if (paths.Contains(destination))
                    throw new IOException(
                        $"Assembly rename destination already exists: '{Path.GetFileName(destination)}'.");
                physicalMoves.Add((source, destination));
            }

            var duplicatePath = physicalMoves
                .GroupBy(move => move.Destination, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicatePath is not null)
                throw new InvalidDataException(
                    $"Multiple assemblies would target '{Path.GetFileName(duplicatePath.Key)}'.");
        }

        var files = new List<AssemblyRenameFile>();
        foreach (var identity in allowed.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            if (!assemblies.TryGetValue(identity, out var input))
                continue;
            try
            {
                using var stream = new MemoryStream(input.Bytes);
                using var asm = AssemblyDefinition.ReadAssembly(
                    stream,
                    new ReaderParameters
                    {
                        ReadingMode = ReadingMode.Immediate,
                        ReadSymbols = false,
                    });

                var changed = false;
                if (renameMap.TryGetValue(asm.Name.Name, out var newIdentity))
                {
                    asm.Name.Name = newIdentity;
                    asm.MainModule.Name = newIdentity + ".dll";
                    changed = true;
                }
                foreach (var asmRef in asm.MainModule.AssemblyReferences)
                {
                    if (!renameMap.TryGetValue(asmRef.Name, out var newRefName))
                        continue;
                    asmRef.Name = newRefName;
                    changed = true;
                }
                if (!changed)
                    continue;

                var destination = skipFileRename
                    || !renameMap.TryGetValue(identity, out var fileName)
                        ? input.Path : Path.Combine(directory, fileName + ".dll");
                files.Add(new AssemblyRenameFile(input.Path, destination, _emit(asm)));
            }
            catch (AssemblyResolutionException ex)
            {
                throw ResolutionFailure(input.Path, ex);
            }
        }

        return new AssemblyRenamePlan(renameMap, files);
    }

    private static InvalidOperationException ResolutionFailure(
        string path, AssemblyResolutionException inner) =>
        new($"Failed to prepare assembly '{Path.GetFileName(path)}': {inner.Message}", inner);

    private static byte[] Emit(AssemblyDefinition assembly)
    {
        using var output = new MemoryStream();
        assembly.Write(output);
        return output.ToArray();
    }

    private static bool HasSelfAssemblyQualifiedTypeName(
        AssemblyDefinition assembly,
        string identity)
    {
        var localTypeNames = EnumerateTypes(assembly.MainModule.Types)
            .Select(ReflectionFullName)
            .ToArray();
        foreach (var method in EnumerateTypes(assembly.MainModule.Types)
            .SelectMany(type => type.Methods)
            .Where(method => method.HasBody))
        {
            var instructions = method.Body.Instructions;
            for (var index = 0; index < instructions.Count; index++)
            {
                if (instructions[index].Operand is not MethodReference called
                    || !IsAssemblyNameBearingReflectionApi(called))
                    continue;
                var nameParameter = called.Parameters
                    .Select((parameter, ordinal) => (parameter, ordinal))
                    .FirstOrDefault(item => item.parameter.ParameterType.MetadataType
                        == MetadataType.String);
                if (nameParameter.parameter is null)
                    continue;
                var depth = called.Parameters.Count - 1 - nameParameter.ordinal;
                var value = ResolveStringArgument(method, index, depth);
                if (value is null)
                    continue;
                if (value.IsUnknown
                    || value.Literals.Any(literal => ReferencesLocalAssemblyType(
                        literal, identity, localTypeNames)))
                    return true;
            }
        }
        return false;
    }

    private static bool IsAssemblyNameBearingReflectionApi(MethodReference method)
        => !method.HasThis && method.DeclaringType.FullName == "System.Type"
            && method.Name == "GetType"
            || method.DeclaringType.FullName == "System.Reflection.Assembly"
            && method.Name == "ReflectionOnlyGetType";

    private static bool ReferencesLocalAssemblyType(
        string value,
        string identity,
        IReadOnlyCollection<string> localTypeNames)
    {
        foreach (var typeName in localTypeNames)
        {
            for (var start = 0; start < value.Length; start++)
            {
                if (start != 0 && value[start - 1] != '[')
                    continue;
                while (start < value.Length && value[start] == '[')
                    start++;
                if (start + typeName.Length > value.Length
                    || !value.AsSpan(start, typeName.Length).Equals(
                        typeName, StringComparison.Ordinal))
                    continue;

                var end = start + typeName.Length;
                var bracketDepth = 0;
                while (end < value.Length)
                {
                    if (value[end] == '[')
                        bracketDepth++;
                    else if (value[end] == ']')
                    {
                        if (bracketDepth == 0)
                            break;
                        bracketDepth--;
                    }
                    else if (value[end] == ',' && bracketDepth == 0)
                        break;
                    else if (bracketDepth == 0
                        && value[end] is not '*' and not '&'
                        && !char.IsWhiteSpace(value[end]))
                        break;
                    end++;
                }
                if (end >= value.Length || value[end] != ',')
                    continue;
                end++;
                while (end < value.Length && char.IsWhiteSpace(value[end]))
                    end++;
                if (value.AsSpan(end).StartsWith(
                        identity, StringComparison.OrdinalIgnoreCase)
                    && (end + identity.Length == value.Length
                        || value[end + identity.Length] is ',' or ']'))
                    return true;
            }
        }
        return false;
    }

    private static string ReflectionFullName(TypeDefinition type)
        => type.DeclaringType is null
            ? string.IsNullOrEmpty(type.Namespace)
                ? type.Name : type.Namespace + "." + type.Name
            : ReflectionFullName(type.DeclaringType) + "+" + type.Name;

    private static StringValue? ResolveStringArgument(
        MethodDefinition method,
        int callIndex,
        int depthFromTop)
    {
        var instructions = method.Body.Instructions;
        if (instructions.Count == 0)
            return StringValue.Unknown;

        var addressTaken = new HashSet<VariableDefinition>(
            instructions
                .Where(instruction => instruction.OpCode.Code
                    is Code.Ldloca or Code.Ldloca_S)
                .Select(instruction => instruction.Operand)
                .OfType<VariableDefinition>());
        var states = new Dictionary<int, FlowState>();
        var pending = new Queue<int>();

        AddState(0, FlowState.Create(method.Body.Variables.Count), states, pending);
        foreach (var handler in method.Body.ExceptionHandlers)
        {
            if (handler.FilterStart is not null)
                AddHandlerState(handler.FilterStart, hasException: true);
            AddHandlerState(
                handler.HandlerStart,
                handler.HandlerType is ExceptionHandlerType.Catch
                    or ExceptionHandlerType.Filter);
        }

        while (pending.Count > 0)
        {
            var index = pending.Dequeue();
            var state = states[index].Clone();
            var instruction = instructions[index];
            ApplyInstruction(method, instruction, state, addressTaken);

            foreach (var successor in GetSuccessors(instructions, index, instruction))
            {
                var outgoing = state.Clone();
                if (instruction.OpCode.Code is Code.Leave or Code.Leave_S)
                    outgoing.Stack.Clear();
                AddState(successor, outgoing, states, pending);
            }
        }

        if (!states.TryGetValue(callIndex, out var callState))
            return null;
        if (callState.StackUnknown
            || depthFromTop < 0 || depthFromTop >= callState.Stack.Count)
            return StringValue.Unknown;
        return callState.Stack[callState.Stack.Count - 1 - depthFromTop];

        void AddHandlerState(Instruction start, bool hasException)
        {
            var handlerState = FlowState.Create(method.Body.Variables.Count);
            if (hasException)
                handlerState.Stack.Add(StringValue.Unknown);
            AddState(instructions.IndexOf(start), handlerState, states, pending);
        }
    }

    private static void ApplyInstruction(
        MethodDefinition method,
        Instruction instruction,
        FlowState state,
        IReadOnlySet<VariableDefinition> addressTaken)
    {
        if (instruction.OpCode == OpCodes.Ldstr
            && instruction.Operand is string literal)
        {
            state.Stack.Add(StringValue.Known(literal));
            return;
        }
        if (TryGetLoadedLocal(method, instruction, out var loaded)
            && loaded is not null)
        {
            state.Stack.Add(addressTaken.Contains(loaded)
                ? StringValue.Unknown : state.Locals[loaded.Index]);
            return;
        }
        if (TryGetStoredLocal(method, instruction, out var stored)
            && stored is not null)
        {
            var value = Pop(state);
            state.Locals[stored.Index] = addressTaken.Contains(stored)
                ? StringValue.Unknown : value;
            return;
        }
        if (instruction.OpCode == OpCodes.Dup)
        {
            state.Stack.Add(state.Stack.Count == 0
                ? StringValue.Unknown : state.Stack[^1]);
            return;
        }

        if ((instruction.OpCode.StackBehaviourPop == StackBehaviour.Varpop
                || instruction.OpCode.StackBehaviourPush == StackBehaviour.Varpush)
            && instruction.Operand is not MethodReference
            && instruction.OpCode != OpCodes.Ret)
            state.StackUnknown = true;

        var popCount = GetPopCount(instruction);
        for (var count = 0; count < popCount; count++)
            Pop(state);
        var pushCount = GetPushCount(instruction);
        for (var count = 0; count < pushCount; count++)
            state.Stack.Add(StringValue.Unknown);
    }

    private static StringValue Pop(FlowState state)
    {
        if (state.Stack.Count == 0)
        {
            state.StackUnknown = true;
            return StringValue.Unknown;
        }
        var index = state.Stack.Count - 1;
        var value = state.Stack[index];
        state.Stack.RemoveAt(index);
        return value;
    }

    private static IEnumerable<int> GetSuccessors(
        IList<Instruction> instructions,
        int index,
        Instruction instruction)
    {
        if (instruction.OpCode.FlowControl == FlowControl.Branch)
        {
            if (instruction.Operand is Instruction target)
                yield return instructions.IndexOf(target);
            yield break;
        }
        if (instruction.OpCode.FlowControl == FlowControl.Cond_Branch)
        {
            if (instruction.Operand is Instruction target)
                yield return instructions.IndexOf(target);
            else if (instruction.Operand is Instruction[] targets)
                foreach (var switchTarget in targets)
                    yield return instructions.IndexOf(switchTarget);
        }
        else if (instruction.OpCode.FlowControl is FlowControl.Return
                 or FlowControl.Throw)
            yield break;

        if (index + 1 < instructions.Count)
            yield return index + 1;
    }

    private static void AddState(
        int index,
        FlowState incoming,
        IDictionary<int, FlowState> states,
        Queue<int> pending)
    {
        if (index < 0)
            return;
        if (!states.TryGetValue(index, out var current))
        {
            states[index] = incoming.Clone();
            pending.Enqueue(index);
            return;
        }
        if (current.MergeFrom(incoming))
            pending.Enqueue(index);
    }

    private static bool TryGetLoadedLocal(
        MethodDefinition method,
        Instruction instruction,
        out VariableDefinition? local)
    {
        local = instruction.OpCode.Code switch
        {
            Code.Ldloc_0 => method.Body.Variables.ElementAtOrDefault(0),
            Code.Ldloc_1 => method.Body.Variables.ElementAtOrDefault(1),
            Code.Ldloc_2 => method.Body.Variables.ElementAtOrDefault(2),
            Code.Ldloc_3 => method.Body.Variables.ElementAtOrDefault(3),
            Code.Ldloc or Code.Ldloc_S => instruction.Operand as VariableDefinition,
            _ => null,
        };
        return local is not null;
    }

    private static bool TryGetStoredLocal(
        MethodDefinition method,
        Instruction instruction,
        out VariableDefinition? local)
    {
        local = instruction.OpCode.Code switch
        {
            Code.Stloc_0 => method.Body.Variables.ElementAtOrDefault(0),
            Code.Stloc_1 => method.Body.Variables.ElementAtOrDefault(1),
            Code.Stloc_2 => method.Body.Variables.ElementAtOrDefault(2),
            Code.Stloc_3 => method.Body.Variables.ElementAtOrDefault(3),
            Code.Stloc or Code.Stloc_S => instruction.Operand as VariableDefinition,
            _ => null,
        };
        return local is not null;
    }

    private static int GetPushCount(Instruction instruction)
    {
        if (instruction.OpCode.StackBehaviourPush == StackBehaviour.Varpush)
        {
            if (instruction.OpCode == OpCodes.Newobj)
                return 1;
            return instruction.Operand is MethodReference method
                && method.ReturnType.MetadataType != MetadataType.Void ? 1 : 0;
        }
        return instruction.OpCode.StackBehaviourPush switch
        {
            StackBehaviour.Push0 => 0,
            StackBehaviour.Push1_push1 => 2,
            _ => 1,
        };
    }

    private static int GetPopCount(Instruction instruction)
    {
        if (instruction.OpCode.StackBehaviourPop == StackBehaviour.Varpop)
        {
            if (instruction.Operand is not MethodReference method)
                return 0;
            return method.Parameters.Count
                + (instruction.OpCode != OpCodes.Newobj && method.HasThis ? 1 : 0);
        }
        return instruction.OpCode.StackBehaviourPop switch
        {
            StackBehaviour.Pop0 => 0,
            StackBehaviour.Pop1 or StackBehaviour.Popi
                or StackBehaviour.Popref => 1,
            StackBehaviour.Pop1_pop1 or StackBehaviour.Popi_pop1
                or StackBehaviour.Popi_popi or StackBehaviour.Popi_popi8
                or StackBehaviour.Popi_popr4 or StackBehaviour.Popi_popr8
                or StackBehaviour.Popref_pop1 or StackBehaviour.Popref_popi => 2,
            _ => 3,
        };
    }

    private static IEnumerable<TypeDefinition> EnumerateTypes(
        IEnumerable<TypeDefinition> types)
    {
        foreach (var type in types)
        {
            yield return type;
            foreach (var nested in EnumerateTypes(type.NestedTypes))
                yield return nested;
        }
    }

    private string GenerateName(string logicalName)
    {
        var digest = SHA256.HashData(
            Encoding.UTF8.GetBytes($"{_seed}:{logicalName}"));
        var result = new StringBuilder("_");
        for (var i = 0; i < 5; i++)
            result.Append(AlphaNumChars[digest[i] % AlphaNumChars.Length]);
        return result.ToString();
    }

    private sealed class StringValue
    {
        private StringValue(bool isUnknown, HashSet<string> literals)
        {
            IsUnknown = isUnknown;
            Literals = literals;
        }

        public static StringValue Unknown { get; } =
            new(true, new HashSet<string>(StringComparer.Ordinal));

        public bool IsUnknown { get; }
        public IReadOnlySet<string> Literals { get; }

        public static StringValue Known(string literal) =>
            new(false, new HashSet<string>([literal], StringComparer.Ordinal));

        public static StringValue Merge(StringValue left, StringValue right)
        {
            var literals = new HashSet<string>(left.Literals, StringComparer.Ordinal);
            literals.UnionWith(right.Literals);
            return new StringValue(left.IsUnknown || right.IsUnknown, literals);
        }

        public bool SameAs(StringValue other) =>
            IsUnknown == other.IsUnknown && Literals.SetEquals(other.Literals);
    }

    private sealed class FlowState
    {
        private FlowState(
            StringValue[] locals,
            List<StringValue> stack,
            bool stackUnknown = false)
        {
            Locals = locals;
            Stack = stack;
            StackUnknown = stackUnknown;
        }

        public StringValue[] Locals { get; }
        public List<StringValue> Stack { get; }
        public bool StackUnknown { get; set; }

        public static FlowState Create(int localCount) =>
            new(Enumerable.Repeat(StringValue.Unknown, localCount).ToArray(), []);

        public FlowState Clone() =>
            new((StringValue[])Locals.Clone(), [.. Stack], StackUnknown);

        public bool MergeFrom(FlowState incoming)
        {
            var changed = false;
            if (incoming.StackUnknown && !StackUnknown)
            {
                StackUnknown = true;
                changed = true;
            }
            for (var index = 0; index < Locals.Length; index++)
                changed |= MergeSlot(Locals, index, incoming.Locals[index]);

            if (Stack.Count != incoming.Stack.Count)
            {
                var count = Math.Max(Stack.Count, incoming.Stack.Count);
                StackUnknown = true;
                changed = true;
                while (Stack.Count < count)
                    Stack.Insert(0, StringValue.Unknown);
                for (var index = 0; index < Stack.Count; index++)
                    changed |= MergeSlot(Stack, index, StringValue.Unknown);
                return changed;
            }

            for (var index = 0; index < Stack.Count; index++)
                changed |= MergeSlot(Stack, index, incoming.Stack[index]);
            return changed;
        }

        private static bool MergeSlot(
            IList<StringValue> values,
            int index,
            StringValue incoming)
        {
            var merged = StringValue.Merge(values[index], incoming);
            if (values[index].SameAs(merged))
                return false;
            values[index] = merged;
            return true;
        }
    }

    private sealed record AssemblyInput(string Path, byte[] Bytes);
}
