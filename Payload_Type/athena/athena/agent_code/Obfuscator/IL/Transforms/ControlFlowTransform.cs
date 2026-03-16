using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Obfuscator.IL.Transforms;

/// <summary>
/// Applies control flow flattening to methods in a compiled
/// .NET assembly using Mono.Cecil IL manipulation.
/// Converts sequential method IL into a switch-based dispatcher
/// that hides the original execution order.
/// </summary>
public sealed class ControlFlowTransform
{
    private readonly int _seed;

    public ControlFlowTransform(int seed)
    {
        _seed = seed;
    }

    /// <summary>
    /// Transform an assembly in-memory. Returns modified bytes.
    /// </summary>
    public byte[] Transform(byte[] assemblyBytes)
    {
        using var input = new MemoryStream(assemblyBytes);
        var readerParams = new ReaderParameters
        {
            ReadingMode = ReadingMode.Immediate,
            ReadSymbols = false,
        };
        using var asm = AssemblyDefinition.ReadAssembly(
            input, readerParams);

        foreach (var type in asm.MainModule.Types)
            ProcessType(type);

        using var output = new MemoryStream();
        asm.Write(output);
        return output.ToArray();
    }

    private void ProcessType(TypeDefinition type)
    {
        foreach (var nested in type.NestedTypes)
            ProcessType(nested);

        foreach (var method in type.Methods)
        {
            if (ShouldSkip(method))
                continue;

            var blocks = BuildBasicBlocks(method.Body);
            if (blocks.Count < 4)
                continue;

            FlattenControlFlow(method, blocks);
        }
    }

    private static bool ShouldSkip(MethodDefinition method)
    {
        if (!method.HasBody)
            return true;
        if (method.IsConstructor)
            return true;
        if (method.IsGetter || method.IsSetter)
            return true;
        if (IsAsyncOrIteratorMoveNext(method))
            return true;
        return false;
    }

    private static bool IsAsyncOrIteratorMoveNext(
        MethodDefinition method)
    {
        if (method.Name != "MoveNext")
            return false;

        var declType = method.DeclaringType;
        if (declType == null)
            return false;

        foreach (var iface in declType.Interfaces)
        {
            var name = iface.InterfaceType.FullName;
            if (name.Contains("IAsyncStateMachine")
                || name.Contains("IEnumerator"))
                return true;
        }
        return false;
    }

    private static List<BasicBlock> BuildBasicBlocks(
        MethodBody body)
    {
        body.SimplifyMacros();

        var instructions = body.Instructions;
        if (instructions.Count == 0)
            return new List<BasicBlock>();

        var leaders = new HashSet<int> { 0 };

        for (int i = 0; i < instructions.Count; i++)
        {
            var instr = instructions[i];
            var fc = instr.OpCode.FlowControl;

            if (fc == FlowControl.Branch
                || fc == FlowControl.Cond_Branch)
            {
                if (instr.Operand is Instruction target)
                {
                    var idx = instructions.IndexOf(target);
                    if (idx >= 0)
                        leaders.Add(idx);
                }
                else if (instr.Operand is Instruction[] targets)
                {
                    foreach (var t in targets)
                    {
                        var idx = instructions.IndexOf(t);
                        if (idx >= 0)
                            leaders.Add(idx);
                    }
                }

                if (i + 1 < instructions.Count)
                    leaders.Add(i + 1);
            }

            if (fc == FlowControl.Return
                || fc == FlowControl.Throw)
            {
                if (i + 1 < instructions.Count)
                    leaders.Add(i + 1);
            }
        }

        var sortedLeaders = leaders.OrderBy(x => x).ToList();
        var blocks = new List<BasicBlock>();

        for (int i = 0; i < sortedLeaders.Count; i++)
        {
            int start = sortedLeaders[i];
            int end = i + 1 < sortedLeaders.Count
                ? sortedLeaders[i + 1]
                : instructions.Count;

            var block = new BasicBlock { Index = i };
            for (int j = start; j < end; j++)
                block.Instructions.Add(instructions[j]);

            blocks.Add(block);
        }

        return blocks;
    }

    private void FlattenControlFlow(
        MethodDefinition method,
        List<BasicBlock> blocks)
    {
        var body = method.Body;
        var il = body.GetILProcessor();
        var module = method.DeclaringType.Module;

        // Map original Instruction refs to block indices
        var blockLeader = new Dictionary<Instruction, int>(
            ReferenceEqualityComparer.Instance);
        for (int i = 0; i < blocks.Count; i++)
        {
            if (blocks[i].Instructions.Count > 0)
                blockLeader[blocks[i].Instructions[0]] = i;
        }

        // Assign shuffled state IDs
        var rng = new Random(_seed);
        var stateIds = Enumerable
            .Range(0, blocks.Count).ToList();
        Shuffle(stateIds, rng);

        var blockToState = new int[blocks.Count];
        for (int i = 0; i < blocks.Count; i++)
            blockToState[i] = stateIds[i];

        // Add state variable
        var stateVar = new VariableDefinition(
            module.TypeSystem.Int32);
        body.Variables.Add(stateVar);

        // For non-void methods, add a result variable so we
        // can store the return value and have a single exit
        var hasReturnValue =
            method.ReturnType.FullName != "System.Void";
        VariableDefinition? retVar = null;
        if (hasReturnValue)
        {
            retVar = new VariableDefinition(method.ReturnType);
            body.Variables.Add(retVar);
        }

        body.InitLocals = true;
        body.Instructions.Clear();
        if (body.HasExceptionHandlers)
            body.ExceptionHandlers.Clear();

        // Dispatcher layout:
        //   state = entryState
        //   loopStart:
        //     switch(state) { case N: goto caseN; ... }
        //     goto exitLabel
        //   case0: ...
        //   case1: ...
        //   exitLabel:
        //     [ldloc retVar]  // if non-void
        //     ret

        var loopStart = il.Create(OpCodes.Nop);
        var exitLabel = il.Create(OpCodes.Nop);

        // Init state
        il.Append(il.Create(
            OpCodes.Ldc_I4, blockToState[0]));
        il.Append(il.Create(OpCodes.Stloc, stateVar));

        // Loop header
        il.Append(loopStart);
        il.Append(il.Create(OpCodes.Ldloc, stateVar));

        // Build switch target array
        var caseNops = new Instruction[blocks.Count];
        for (int i = 0; i < blocks.Count; i++)
            caseNops[i] = il.Create(OpCodes.Nop);

        int maxState = stateIds.Max();
        var switchTargets = new Instruction[maxState + 1];
        for (int i = 0; i < switchTargets.Length; i++)
            switchTargets[i] = exitLabel;
        for (int i = 0; i < blocks.Count; i++)
            switchTargets[blockToState[i]] = caseNops[i];

        il.Append(il.Create(OpCodes.Switch, switchTargets));
        il.Append(il.Create(OpCodes.Br, exitLabel));

        // Emit each block
        for (int bi = 0; bi < blocks.Count; bi++)
        {
            il.Append(caseNops[bi]);

            var instrs = blocks[bi].Instructions;
            if (instrs.Count == 0)
            {
                EmitGotoNext(
                    il, stateVar, bi, blocks.Count,
                    blockToState, exitLabel, loopStart);
                continue;
            }

            var last = instrs[instrs.Count - 1];
            var fc = last.OpCode.FlowControl;

            if (fc == FlowControl.Return)
            {
                // Emit all instructions except the final ret.
                // Instead, store result and jump to exit.
                for (int j = 0; j < instrs.Count - 1; j++)
                    il.Append(CloneInstruction(il, instrs[j]));
                if (retVar != null)
                    il.Append(il.Create(OpCodes.Stloc, retVar));
                il.Append(il.Create(OpCodes.Br, exitLabel));
            }
            else if (fc == FlowControl.Throw)
            {
                foreach (var instr in instrs)
                    il.Append(CloneInstruction(il, instr));
            }
            else if (fc == FlowControl.Branch
                     && last.Operand is Instruction brTarget)
            {
                EmitAllExcept(il, instrs, last);
                EmitGotoBlock(
                    il, stateVar, brTarget, blockLeader,
                    blockToState, loopStart, exitLabel);
            }
            else if (fc == FlowControl.Cond_Branch
                     && last.Operand is Instruction cbTarget)
            {
                EmitAllExcept(il, instrs, last);
                EmitCondBranch(
                    il, stateVar, last.OpCode, cbTarget,
                    bi, blocks.Count, blockLeader,
                    blockToState, loopStart, exitLabel);
            }
            else
            {
                // Fall-through block
                foreach (var instr in instrs)
                    il.Append(CloneInstruction(il, instr));
                EmitGotoNext(
                    il, stateVar, bi, blocks.Count,
                    blockToState, exitLabel, loopStart);
            }
        }

        // Exit: load return value (if any) and ret
        il.Append(exitLabel);
        if (retVar != null)
            il.Append(il.Create(OpCodes.Ldloc, retVar));
        il.Append(il.Create(OpCodes.Ret));

        body.OptimizeMacros();
    }

    private static void EmitAllExcept(
        ILProcessor il,
        List<Instruction> instrs,
        Instruction exclude)
    {
        foreach (var instr in instrs)
        {
            if (ReferenceEquals(instr, exclude))
                continue;
            il.Append(CloneInstruction(il, instr));
        }
    }

    private static void EmitGotoBlock(
        ILProcessor il,
        VariableDefinition stateVar,
        Instruction branchTarget,
        Dictionary<Instruction, int> blockLeader,
        int[] blockToState,
        Instruction loopStart,
        Instruction exitLabel)
    {
        if (!blockLeader.TryGetValue(
                branchTarget, out int targetBlock))
        {
            il.Append(il.Create(OpCodes.Br, exitLabel));
            return;
        }

        il.Append(il.Create(
            OpCodes.Ldc_I4, blockToState[targetBlock]));
        il.Append(il.Create(OpCodes.Stloc, stateVar));
        il.Append(il.Create(OpCodes.Br, loopStart));
    }

    private static void EmitCondBranch(
        ILProcessor il,
        VariableDefinition stateVar,
        OpCode condOp,
        Instruction cbTarget,
        int curBlock,
        int totalBlocks,
        Dictionary<Instruction, int> blockLeader,
        int[] blockToState,
        Instruction loopStart,
        Instruction exitLabel)
    {
        int falseBlock = curBlock + 1;
        blockLeader.TryGetValue(
            cbTarget, out int trueBlock);

        var trueNop = il.Create(OpCodes.Nop);
        var afterNop = il.Create(OpCodes.Nop);

        // Emit the conditional branch to true path
        il.Append(il.Create(condOp, trueNop));

        // False path: fall-through block
        if (falseBlock < totalBlocks)
        {
            il.Append(il.Create(
                OpCodes.Ldc_I4, blockToState[falseBlock]));
        }
        else
        {
            il.Append(il.Create(OpCodes.Ldc_I4, -1));
        }
        il.Append(il.Create(OpCodes.Stloc, stateVar));
        il.Append(il.Create(OpCodes.Br, afterNop));

        // True path
        il.Append(trueNop);
        if (blockLeader.ContainsKey(cbTarget))
        {
            il.Append(il.Create(
                OpCodes.Ldc_I4, blockToState[trueBlock]));
        }
        else
        {
            il.Append(il.Create(OpCodes.Ldc_I4, -1));
        }
        il.Append(il.Create(OpCodes.Stloc, stateVar));

        il.Append(afterNop);
        il.Append(il.Create(OpCodes.Br, loopStart));
    }

    private static void EmitGotoNext(
        ILProcessor il,
        VariableDefinition stateVar,
        int curBlock,
        int totalBlocks,
        int[] blockToState,
        Instruction exitLabel,
        Instruction loopStart)
    {
        int next = curBlock + 1;
        if (next >= totalBlocks)
        {
            il.Append(il.Create(OpCodes.Br, exitLabel));
            return;
        }
        il.Append(il.Create(
            OpCodes.Ldc_I4, blockToState[next]));
        il.Append(il.Create(OpCodes.Stloc, stateVar));
        il.Append(il.Create(OpCodes.Br, loopStart));
    }

    private static Instruction CloneInstruction(
        ILProcessor il,
        Instruction original)
    {
        if (original.Operand == null)
            return il.Create(original.OpCode);

        return original.Operand switch
        {
            byte v => il.Create(original.OpCode, v),
            sbyte v => il.Create(original.OpCode, v),
            int v => il.Create(original.OpCode, v),
            long v => il.Create(original.OpCode, v),
            float v => il.Create(original.OpCode, v),
            double v => il.Create(original.OpCode, v),
            string v => il.Create(original.OpCode, v),
            TypeReference v => il.Create(original.OpCode, v),
            MethodReference v => il.Create(original.OpCode, v),
            FieldReference v => il.Create(original.OpCode, v),
            VariableDefinition v =>
                il.Create(original.OpCode, v),
            ParameterDefinition v =>
                il.Create(original.OpCode, v),
            CallSite v => il.Create(original.OpCode, v),
            Instruction v => il.Create(original.OpCode, v),
            Instruction[] v => il.Create(original.OpCode, v),
            _ => il.Create(original.OpCode),
        };
    }

    private static void Shuffle<T>(List<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private sealed class BasicBlock
    {
        public int Index { get; set; }
        public List<Instruction> Instructions { get; } = new();
    }
}
