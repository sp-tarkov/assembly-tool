// Claude Generated

using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssemblyLib.Patching.Tool;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Patching;

[Injectable]
public class MethodPatcher(ILogger<MethodPatcher> logger, DataProvider dataProvider, MethodBodyNuker nuker)
{
    /// <summary>
    ///     Applies a prefix or postfix patch from <paramref name="source"/> into <paramref name="target"/>.
    ///     The source method's instructions (and locals) are cloned and imported into the target module.
    ///     For <see cref="MethodPatchType.Prefix"/>, the source body (minus its final ret) is prepended.
    ///     For <see cref="MethodPatchType.Postfix"/>, every ret in the target is redirected through the source body.
    /// </summary>
    public void Patch(MethodDefinition target, MethodDefinition source, MethodPatchType methodPatchType)
    {
        if (target.CilMethodBody is null)
        {
            throw new InvalidOperationException($"Target method '{target.FullName}' has no CIL body.");
        }

        if (source.CilMethodBody is null)
        {
            throw new InvalidOperationException($"Source method '{source.FullName}' has no CIL body.");
        }

        if (IsConstructor(target) && source.Signature?.ReturnsValue == true)
        {
            throw new InvalidOperationException(
                $"Constructor patch '{source.FullName}' must return void when patching '{target.FullName}'."
            );
        }

        var targetBody = target.CilMethodBody;
        var module = dataProvider.LoadedModule!;

        var argSlotMap = methodPatchType != MethodPatchType.Replace ? BuildArgSlotMap(source, target) : null;
        var (cloned, _) = CloneBody(source.CilMethodBody, targetBody, module, source, target, argSlotMap);

        switch (methodPatchType)
        {
            case MethodPatchType.Prefix:
                ApplyPrefix(targetBody, cloned, source.Signature!.ReturnType);
                break;
            case MethodPatchType.Postfix:
                ApplyPostfix(targetBody, cloned, target.Signature!.ReturnType);
                break;
            case MethodPatchType.Replace:
                ApplyReplace(target, source);
                break;
            default:
                throw new NotImplementedException($"Method patch type '{methodPatchType}' is not implemented.");
        }

        targetBody.Instructions.CalculateOffsets();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Applied {PatchType} patch from {Source} → {Target}",
                methodPatchType,
                source.FullName,
                target.FullName
            );
        }
    }

    private static void ApplyPrefix(
        CilMethodBody targetBody,
        List<CilInstruction> prefix,
        TypeSignature sourceReturnType
    )
    {
        if (prefix.Count == 0)
            return;

        if (sourceReturnType.FullName == "System.Boolean")
        {
            ApplyBoolPrefix(targetBody, prefix);
        }
        else
        {
            ApplyVoidPrefix(targetBody, prefix);
        }
    }

    private static void ApplyBoolPrefix(CilMethodBody targetBody, List<CilInstruction> prefix)
    {
        var checkPoint = new CilInstruction(CilOpCodes.Nop);
        var finalRet = new CilInstruction(CilOpCodes.Ret);

        // All rets in the prefix leave a bool on the stack — redirect to checkPoint
        foreach (var instr in prefix)
        {
            if (instr.OpCode == CilOpCodes.Ret)
            {
                instr.OpCode = CilOpCodes.Br;
                instr.Operand = new CilInstructionLabel(checkPoint);
            }
        }

        // Capture original start BEFORE shifting the list
        var originalStart = targetBody.Instructions[0];

        for (var i = 0; i < prefix.Count; i++)
            targetBody.Instructions.Insert(i, prefix[i]);

        // Insert the dispatch block right after the prefix
        var brFalse = new CilInstruction(CilOpCodes.Brfalse, new CilInstructionLabel(originalStart));
        var brFinal = new CilInstruction(CilOpCodes.Br, new CilInstructionLabel(finalRet));

        targetBody.Instructions.Insert(prefix.Count, checkPoint);
        targetBody.Instructions.Insert(prefix.Count + 1, brFalse);
        targetBody.Instructions.Insert(prefix.Count + 2, brFinal);

        targetBody.Instructions.Add(finalRet);
    }

    private static void ApplyVoidPrefix(CilMethodBody targetBody, List<CilInstruction> prefix)
    {
        NopTrailingRet(prefix);

        for (var i = 0; i < prefix.Count; i++)
            targetBody.Instructions.Insert(i, prefix[i]);
    }

    private static void ApplyPostfix(CilMethodBody targetBody, List<CilInstruction> postfix, TypeSignature returnType)
    {
        NopTrailingRet(postfix);
        if (postfix.Count == 0)
        {
            return;
        }

        var isVoid = returnType.FullName == "System.Void";

        // We'll jump every existing ret to the first postfix instruction
        var firstPostfix = postfix[0];

        // For non-void methods we need a local to stash the return value
        CilLocalVariable? retLocal = null;
        if (!isVoid)
        {
            retLocal = new CilLocalVariable(returnType);
            targetBody.LocalVariables.Add(retLocal);
        }

        // Patch all existing rets
        var existingRets = targetBody.Instructions.Where(i => i.OpCode == CilOpCodes.Ret).ToList();

        foreach (var ret in existingRets)
        {
            if (!isVoid)
            {
                // stloc <retLocal> — replaces the ret, pops the return value
                ret.OpCode = CilOpCodes.Stloc;
                ret.Operand = retLocal!;

                // Insert a branch to postfix after the stloc
                var br = new CilInstruction(CilOpCodes.Br, new CilInstructionLabel(firstPostfix));
                var retIndex = targetBody.Instructions.IndexOf(ret);
                targetBody.Instructions.Insert(retIndex + 1, br);
            }
            else
            {
                ret.OpCode = CilOpCodes.Br;
                ret.Operand = new CilInstructionLabel(firstPostfix);
            }
        }

        // Append the cloned postfix block
        foreach (var instr in postfix)
        {
            targetBody.Instructions.Add(instr);
        }

        // Re-load the saved return value (if any) and add the single final ret
        if (!isVoid)
        {
            targetBody.Instructions.Add(new CilInstruction(CilOpCodes.Ldloc, retLocal!));
        }

        targetBody.Instructions.Add(CilOpCodes.Ret);
    }

    private void ApplyReplace(MethodDefinition target, MethodDefinition source)
    {
        if (target.CilMethodBody is null)
        {
            throw new InvalidOperationException($"Target method '{target.FullName}' has no CIL body.");
        }

        if (source.CilMethodBody is null)
        {
            throw new InvalidOperationException($"Source method '{source.FullName}' has no CIL body.");
        }

        var targetBody = target.CilMethodBody;
        var module = dataProvider.LoadedModule!;

        // Wipe the target body clean
        targetBody.Instructions.Clear();
        targetBody.ExceptionHandlers.Clear();
        targetBody.LocalVariables.Clear();

        // Clone the source body directly into the (now empty) target body
        var (cloned, _) = CloneBody(source.CilMethodBody, targetBody, module);
        foreach (var instr in cloned)
        {
            targetBody.Instructions.Add(instr);
        }

        targetBody.MaxStack = source.CilMethodBody.MaxStack;
        targetBody.InitializeLocals = source.CilMethodBody.InitializeLocals;

        targetBody.Instructions.CalculateOffsets();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Replaced body of {Target} with {Source}", target.FullName, source.FullName);
        }
    }

    // -------------------------------------------------------------------------
    // Body cloning
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Clones all instructions from <paramref name="source"/> into standalone
    ///     <see cref="CilInstruction"/> objects suitable for insertion into <paramref name="targetBody"/>.
    ///     Locals are added to <paramref name="targetBody"/> and branch targets / exception handlers are remapped.
    /// </summary>
    private (List<CilInstruction> instructions, Dictionary<CilLocalVariable, CilLocalVariable> localMap) CloneBody(
        CilMethodBody source,
        CilMethodBody targetBody,
        ModuleDefinition module,
        MethodDefinition? sourceMethod = null,
        MethodDefinition? targetMethod = null,
        Dictionary<int, int>? argSlotMap = null
    )
    {
        var importer = module.DefaultImporter;

        // Clone
        var localMap = new Dictionary<CilLocalVariable, CilLocalVariable>();
        foreach (var srcLocal in source.LocalVariables)
        {
            var importedSig = importer.ImportTypeSignature(srcLocal.VariableType);
            var dstLocal = new CilLocalVariable(importedSig);
            targetBody.LocalVariables.Add(dstLocal);
            localMap[srcLocal] = dstLocal;
        }

        // Create instructions
        var srcList = source.Instructions.ToList();
        var cloned = srcList.Select(i => new CilInstruction(i.OpCode)).ToList();

        var instrMap = srcList.Zip(cloned, (src, dst) => (src, dst)).ToDictionary(p => p.src, p => p.dst);

        // Import / remap operands
        for (var i = 0; i < srcList.Count; i++)
        {
            var srcInstr = srcList[i];

            // Expand short-form local opcodes FIRST — their index is baked into
            // the opcode with no operand, so RemapOperand never gets a chance to
            // remap them, causing index collisions with the target's existing locals
            var shortLocal = TryExpandShortLocal(srcInstr.OpCode, source.LocalVariables, localMap);
            if (shortLocal.HasValue)
            {
                cloned[i].OpCode = shortLocal.Value.opCode;
                cloned[i].Operand = shortLocal.Value.operand;
                continue;
            }

            if (argSlotMap is not null && sourceMethod is not null && targetMethod is not null)
            {
                var remappedArg = TryRemapArgInstruction(
                    srcInstr.OpCode,
                    srcInstr.Operand,
                    sourceMethod,
                    targetMethod,
                    argSlotMap
                );
                if (remappedArg.HasValue)
                {
                    cloned[i].OpCode = remappedArg.Value.opCode;
                    cloned[i].Operand = remappedArg.Value.operand;
                    continue;
                }
            }

            var (newOpCode, newOperand) = RemapInstruction(
                srcInstr.OpCode,
                srcInstr.Operand,
                instrMap,
                localMap,
                importer,
                module
            );
            cloned[i].OpCode = newOpCode;
            cloned[i].Operand = newOperand;
        }

        // Clone exception handlers
        foreach (var handler in source.ExceptionHandlers)
        {
            targetBody.ExceptionHandlers.Add(
                new CilExceptionHandler
                {
                    HandlerType = handler.HandlerType,
                    TryStart = RemapLabel(handler.TryStart, instrMap),
                    TryEnd = RemapLabel(handler.TryEnd, instrMap),
                    HandlerStart = RemapLabel(handler.HandlerStart, instrMap),
                    HandlerEnd = RemapLabel(handler.HandlerEnd, instrMap),
                    FilterStart = handler.FilterStart is null ? null : RemapLabel(handler.FilterStart, instrMap),
                    ExceptionType = handler.ExceptionType is null ? null : importer.ImportType(handler.ExceptionType),
                }
            );
        }

        return (cloned, localMap);
    }

    /// <summary>
    ///     Remaps both the opcode and operand of a single instruction.
    ///     Handles the common stub/dummy-DLL mismatch where a property getter/setter
    ///     in the source compiles to callvirt get_X / set_X, but the real target
    ///     assembly exposes the same member as a plain field.
    ///
    ///     get_X  →  ldfld / ldsfld
    ///     set_X  →  stfld / stsfld
    /// </summary>
    private (CilOpCode opCode, object? operand) RemapInstruction(
        CilOpCode opCode,
        object? operand,
        Dictionary<CilInstruction, CilInstruction> instrMap,
        Dictionary<CilLocalVariable, CilLocalVariable> localMap,
        ReferenceImporter importer,
        ModuleDefinition module
    )
    {
        // call/callvirt to get_X / set_X → remap to field opcode
        if (
            operand is IMethodDefOrRef { Name: { } accessorName } accessorMethod
            && (opCode == CilOpCodes.Call || opCode == CilOpCodes.Callvirt)
        )
        {
            var isGetter = accessorName.Value.StartsWith("get_", StringComparison.Ordinal);
            var isSetter = accessorName.Value.StartsWith("set_", StringComparison.Ordinal);

            if (isGetter || isSetter)
            {
                var field = FindFieldByAccessor(
                    accessorMethod.DeclaringType?.FullName,
                    accessorName.Value.Substring(4),
                    module
                );

                if (field is not null)
                {
                    var imported = importer.ImportField(field);
                    var fieldOp = isGetter
                        ? field.IsStatic
                            ? CilOpCodes.Ldsfld
                            : CilOpCodes.Ldfld
                        : field.IsStatic
                            ? CilOpCodes.Stsfld
                            : CilOpCodes.Stfld;

                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug(
                            "Rewrote accessor {A} → {Op} {F}",
                            accessorName.Value,
                            fieldOp.Mnemonic,
                            field.FullName
                        );
                    }

                    return (fieldOp, imported);
                }
            }
        }

        // field opcode whose operand is a method-like MemberReference
        // Stub generators sometimes emit fields as MemberReferences with method
        // signatures. MemberReference implements IMethodDefOrRef, so RemapOperand
        // would route it to ImportMethodSafe instead of ImportField.  Intercept here.
        if (operand is IMethodDefOrRef { Name: { } memberName } memberRef && IsFieldOpCode(opCode))
        {
            var field = FindFieldByAccessor(memberRef.DeclaringType?.FullName, memberName.Value, module);

            if (field is not null)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                        "Rewrote method-ref field operand {M} → {Op} {F}",
                        memberName.Value,
                        opCode.Mnemonic,
                        field.FullName
                    );
                }

                return (opCode, importer.ImportField(field));
            }
        }

        return (opCode, RemapOperand(operand, instrMap, localMap, importer));
    }

    private static bool IsFieldOpCode(CilOpCode op) =>
        op == CilOpCodes.Ldfld
        || op == CilOpCodes.Ldsfld
        || op == CilOpCodes.Stfld
        || op == CilOpCodes.Stsfld
        || op == CilOpCodes.Ldflda
        || op == CilOpCodes.Ldsflda;

    private FieldDefinition? FindFieldByAccessor(string? declaringFqn, string fieldName, ModuleDefinition module)
    {
        if (declaringFqn is null)
        {
            return null;
        }

        var targetType = module.GetAllTypes().FirstOrDefault(t => t.FullName == declaringFqn);

        return FindFieldInHierarchy(targetType, fieldName);
    }

    private FieldDefinition? FindFieldInHierarchy(TypeDefinition? type, string fieldName)
    {
        while (type is not null)
        {
            var field = type.Fields.FirstOrDefault(f => f.Name == fieldName);
            if (field is not null)
            {
                return field;
            }

            type = type.BaseType?.Resolve(dataProvider.Context);
        }
        return null;
    }

    private object? RemapOperand(
        object? operand,
        Dictionary<CilInstruction, CilInstruction> instrMap,
        Dictionary<CilLocalVariable, CilLocalVariable> localMap,
        ReferenceImporter importer
    )
    {
        return operand switch
        {
            null => null,

            // Locals
            CilLocalVariable local => localMap.TryGetValue(local, out var mapped)
                ? mapped
                : throw new InvalidOperationException($"Encountered unmapped local variable at index {local.Index}."),

            // Single branch target
            ICilLabel label => RemapLabel(label, instrMap),

            // Switch table
            IList<ICilLabel> labels => labels.Select(l => RemapLabel(l, instrMap)).ToList(),

            // Member references — import into target module
            IMethodDefOrRef method => ImportMethodSafe(method, importer, method.ContextModule),
            IFieldDescriptor field => importer.ImportField(field),
            ITypeDefOrRef type => importer.ImportType(type),
            TypeSignature sig => importer.ImportTypeSignature(sig),

            // Raw value operands — copy as-is
            string or int or long or float or double or sbyte or byte => operand,

            _ => operand,
        };
    }

    private static ICilLabel RemapLabel(ICilLabel label, Dictionary<CilInstruction, CilInstruction> instrMap)
    {
        if (label is CilInstructionLabel { Instruction: { } srcInstr })
        {
            if (instrMap.TryGetValue(srcInstr, out var dstInstr))
            {
                return new CilInstructionLabel(dstInstr);
            }

            // Label points outside the source body (forward ref we don't own) — keep offset
            return new CilOffsetLabel(srcInstr.Offset);
        }

        // Already an offset label — carry it forward unchanged
        return label;
    }

    private static void NopTrailingRet(List<CilInstruction> instructions)
    {
        if (instructions.Count > 0 && instructions[^1].OpCode == CilOpCodes.Ret)
        {
            instructions[^1].OpCode = CilOpCodes.Nop;
            instructions[^1].Operand = null;
        }
    }

    /// <summary>
    ///     Import-safe wrapper around <see cref="ReferenceImporter.ImportMethod"/>.
    ///     Handles three edge cases that the raw importer throws on
    /// </summary>
    private IMethodDefOrRef ImportMethodSafe(
        IMethodDefOrRef method,
        ReferenceImporter importer,
        ModuleDefinition module
    )
    {
        if (method is MethodDefinition methodDef && methodDef.DeclaringModule == module)
        {
            return method;
        }

        if (method.Signature is not null)
        {
            return importer.ImportMethod(method);
        }

        // null signature — try resolving, but Resolve() throws on invalid refs
        MethodDefinition? resolved = null;
        try
        {
            resolved = method.Resolve(dataProvider.Context);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Failed to resolve method '{Method}' during import: {Message}",
                method.FullName,
                ex.Message
            );
        }

        if (resolved?.Signature is not null)
        {
            return importer.ImportMethod(resolved);
        }

        // unresolvable — emit a stub ref so the assembly still serialises
        logger.LogWarning(
            "Method '{Method}' has no signature and could not be resolved — "
                + "importing as a bare MemberReference. The emitted IL may be invalid.",
            method.FullName
        );

        var declaringType = method.DeclaringType is not null
            ? importer.ImportType(method.DeclaringType)
            : module.CorLibTypeFactory.Object.Type;

        var isStatic = method is MethodDefinition { IsStatic: true };

        return new MemberReference(
            declaringType,
            method.Name,
            new MethodSignature(isStatic ? 0 : CallingConventionAttributes.HasThis, module.CorLibTypeFactory.Void, [])
        );
    }

    private static (CilOpCode opCode, CilLocalVariable operand)? TryExpandShortLocal(
        CilOpCode opCode,
        IList<CilLocalVariable> srcLocals,
        Dictionary<CilLocalVariable, CilLocalVariable> localMap
    )
    {
        // Inline-index forms — index baked into opcode, no operand
        (CilOpCode expanded, int index)? inlined = opCode.Code switch
        {
            CilCode.Ldloc_0 => (CilOpCodes.Ldloc, 0),
            CilCode.Ldloc_1 => (CilOpCodes.Ldloc, 1),
            CilCode.Ldloc_2 => (CilOpCodes.Ldloc, 2),
            CilCode.Ldloc_3 => (CilOpCodes.Ldloc, 3),
            CilCode.Stloc_0 => (CilOpCodes.Stloc, 0),
            CilCode.Stloc_1 => (CilOpCodes.Stloc, 1),
            CilCode.Stloc_2 => (CilOpCodes.Stloc, 2),
            CilCode.Stloc_3 => (CilOpCodes.Stloc, 3),
            _ => null,
        };

        if (inlined.HasValue)
        {
            var (expanded, index) = inlined.Value;
            if (index >= srcLocals.Count)
            {
                return null;
            }

            return (expanded, localMap[srcLocals[index]]);
        }

        // Short operand forms (ldloc.s / stloc.s / ldloca.s) — operand is the
        // source CilLocalVariable directly, remap it to the target local
        CilOpCode? longForm = opCode.Code switch
        {
            CilCode.Ldloc_S => CilOpCodes.Ldloc,
            CilCode.Stloc_S => CilOpCodes.Stloc,
            CilCode.Ldloca_S => CilOpCodes.Ldloca,
            _ => null,
        };

        return null; // not a short-form local — let normal remapping handle it
    }

    /// <summary>
    ///     Builds a mapping from source IL arg slot → target IL arg slot by matching parameter names.
    ///     Allows patch methods to declare only the parameters they need, in any order.
    /// </summary>
    private static Dictionary<int, int> BuildArgSlotMap(MethodDefinition source, MethodDefinition target)
    {
        var map = new Dictionary<int, int>();

        if (!source.IsStatic)
        {
            map[0] = 0; // this → this
        }

        for (var i = 0; i < source.Parameters.Count; i++)
        {
            var name = source.Parameters[i].Name;
            var srcSlot = source.IsStatic ? i : i + 1;
            var found = false;

            for (var j = 0; j < target.Parameters.Count; j++)
            {
                if (target.Parameters[j].Name != name)
                {
                    continue;
                }

                map[srcSlot] = target.IsStatic ? j : j + 1;
                found = true;
                break;
            }

            if (!found)
            {
                throw new InvalidOperationException(
                    $"Patch parameter '{name}' has no match in target method '{target.FullName}'."
                );
            }
        }

        return map;
    }

    /// <summary>
    ///     Remaps ldarg / starg / ldarga instructions (including inline ldarg.0-3 forms)
    ///     using the provided arg slot map so that patch parameters align with the target.
    /// </summary>
    private static (CilOpCode opCode, object? operand)? TryRemapArgInstruction(
        CilOpCode opCode,
        object? operand,
        MethodDefinition sourceMethod,
        MethodDefinition targetMethod,
        Dictionary<int, int> argSlotMap
    )
    {
        // Inline ldarg.0-3 — index baked into opcode, no operand
        int? inlineSlot = opCode.Code switch
        {
            CilCode.Ldarg_0 => 0,
            CilCode.Ldarg_1 => 1,
            CilCode.Ldarg_2 => 2,
            CilCode.Ldarg_3 => 3,
            _ => null,
        };

        if (inlineSlot.HasValue)
        {
            if (!argSlotMap.TryGetValue(inlineSlot.Value, out var dstSlot))
            {
                return null;
            }

            if (dstSlot == inlineSlot.Value)
            {
                return (opCode, null); // no remapping needed
            }

            return dstSlot switch
            {
                0 => (CilOpCodes.Ldarg_0, null),
                1 => (CilOpCodes.Ldarg_1, null),
                2 => (CilOpCodes.Ldarg_2, null),
                3 => (CilOpCodes.Ldarg_3, null),
                _ => (CilOpCodes.Ldarg, GetParamBySlot(targetMethod, dstSlot)),
            };
        }

        // Explicit ldarg / ldarg.s / starg / starg.s / ldarga / ldarga.s
        if (operand is not Parameter srcParam)
        {
            return null;
        }

        var isLoad = opCode.Code is CilCode.Ldarg or CilCode.Ldarg_S;
        var isStore = opCode.Code is CilCode.Starg or CilCode.Starg_S;
        var isAddr = opCode.Code is CilCode.Ldarga or CilCode.Ldarga_S;

        if (!isLoad && !isStore && !isAddr)
        {
            return null;
        }

        var srcSlot = GetArgSlot(sourceMethod, srcParam);
        if (!argSlotMap.TryGetValue(srcSlot, out var targetSlot))
        {
            return null;
        }

        var newOpCode =
            isLoad ? CilOpCodes.Ldarg
            : isStore ? CilOpCodes.Starg
            : CilOpCodes.Ldarga;

        return (newOpCode, GetParamBySlot(targetMethod, targetSlot));
    }

    private static int GetArgSlot(MethodDefinition method, Parameter param) =>
        method.IsStatic ? param.Index : param.Index + 1;

    private static bool IsConstructor(MethodDefinition method) =>
        method.Name?.ToString() is ".ctor" or ".cctor";

    private static Parameter? GetParamBySlot(MethodDefinition method, int slot)
    {
        var idx = method.IsStatic ? slot : slot - 1;
        return idx >= 0 && idx < method.Parameters.Count ? method.Parameters[idx] : null;
    }
}
