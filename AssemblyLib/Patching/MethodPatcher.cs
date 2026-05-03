// Claude Generated

using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Patching;

[Injectable]
public class MethodPatcher(ILogger<MethodPatcher> logger, DataProvider dataProvider)
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

        var targetBody = target.CilMethodBody;
        var module = dataProvider.LoadedModule!;

        var (cloned, _) = CloneBody(source.CilMethodBody, targetBody, module);

        switch (methodPatchType)
        {
            case MethodPatchType.Prefix:
                ApplyPrefix(targetBody, cloned);
                break;
            case MethodPatchType.Postfix:
                ApplyPostfix(targetBody, cloned, target.Signature!.ReturnType);
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

    private static void ApplyPrefix(CilMethodBody targetBody, List<CilInstruction> prefix)
    {
        // Drop the trailing ret from the patch body — fall through into original code
        NopTrailingRet(prefix);

        for (var i = 0; i < prefix.Count; i++)
        {
            targetBody.Instructions.Insert(i, prefix[i]);
        }
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
        ModuleDefinition module
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
            var (newOpCode, newOperand) = RemapInstruction(
                srcList[i].OpCode,
                srcList[i].Operand,
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
                    CilOpCode fieldOp = isGetter
                        ? (field.IsStatic ? CilOpCodes.Ldsfld : CilOpCodes.Ldfld)
                        : (field.IsStatic ? CilOpCodes.Stsfld : CilOpCodes.Stfld);

                    logger.LogDebug(
                        "Rewrote accessor {A} → {Op} {F}",
                        accessorName.Value,
                        fieldOp.Mnemonic,
                        field.FullName
                    );

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
                logger.LogDebug(
                    "Rewrote method-ref field operand {M} → {Op} {F}",
                    memberName.Value,
                    opCode.Mnemonic,
                    field.FullName
                );
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
}
