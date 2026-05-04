using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Helpers;

[Injectable]
public class AssemblySelfReferenceHelper(ILogger<AssemblySelfReferenceHelper> logger)
{
    private ModuleDefinition? _module;

    private readonly HashSet<TypeReference> _visitedTypeRefs = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<TypeSignature> _visitedSigs = new(ReferenceEqualityComparer.Instance);

    private int _fixedCount;

    public void RemoveSelfAssemblyReferences(ModuleDefinition module)
    {
        var selfName = module.Assembly?.Name;
        if (selfName is null)
        {
            return;
        }

        var selfRefs = module.AssemblyReferences.Where(a => a.Name == selfName).ToList();

        if (selfRefs.Count == 0)
        {
            return;
        }

        logger.LogInformation(
            "Found {Count} self-referential assembly reference(s) to '{Name}', removing",
            selfRefs.Count,
            selfName.ToString()
        );

        _module ??= module;

        FixTypeReferencesWithSelfScope(module, selfName);

        foreach (var selfRef in selfRefs)
        {
            if (module.AssemblyReferences.Remove(selfRef))
            {
                logger.LogInformation("Removed self-referential assembly reference to '{Name}'", selfName.ToString());
            }
            else
            {
                logger.LogError(
                    "Could not remove self-referential assembly reference to '{Name}'",
                    selfName.ToString()
                );
            }
        }

        // reset
        _module = null;
        _visitedTypeRefs.Clear();
        _visitedSigs.Clear();
        _fixedCount = 0;
    }

    private void FixTypeReferencesWithSelfScope(ModuleDefinition module, Utf8String selfName)
    {
        // Original TypeRefs from the loaded PE metadata table
        foreach (var typeRef in module.GetImportedTypeReferences())
        {
            FixTypeRef(typeRef, selfName);
        }

        // Module + assembly-level custom attributes
        FixCustomAttributes(module, selfName);
        if (module.Assembly is { } assembly)
        {
            FixCustomAttributes(assembly, selfName);
        }

        foreach (var typeDef in module.GetAllTypes())
        {
            FixTypeRef(typeDef.BaseType, selfName);
            FixCustomAttributes(typeDef, selfName);

            foreach (var iface in typeDef.Interfaces)
            {
                FixTypeRef(iface.Interface, selfName);
                FixCustomAttributes(iface, selfName);
            }

            foreach (var gp in typeDef.GenericParameters)
            {
                FixCustomAttributes(gp, selfName);
                foreach (var c in gp.Constraints)
                {
                    FixTypeRef(c.Constraint, selfName);
                    FixCustomAttributes(c, selfName);
                }
            }

            foreach (var field in typeDef.Fields)
            {
                FixSig(field.Signature?.FieldType, selfName);
                FixCustomAttributes(field, selfName);
            }

            foreach (var prop in typeDef.Properties)
            {
                FixCustomAttributes(prop, selfName);
                FixMethodSig(prop.Signature, selfName);
            }

            foreach (var ev in typeDef.Events)
            {
                FixCustomAttributes(ev, selfName);
                FixTypeRef(ev.EventType, selfName);
            }

            foreach (var impl in typeDef.MethodImplementations)
            {
                if (impl.Body is { } body)
                {
                    FixTypeRef(body.DeclaringType, selfName);
                    if (body.Signature is MethodSignature bms)
                    {
                        FixMethodSig(bms, selfName);
                    }
                }

                if (impl.Declaration is { } decl)
                {
                    FixTypeRef(decl.DeclaringType, selfName);
                    if (decl.Signature is MethodSignature dms)
                    {
                        FixMethodSig(dms, selfName);
                    }
                }
            }

            foreach (var method in typeDef.Methods)
            {
                FixMethodSig(method.Signature, selfName);
                FixCustomAttributes(method, selfName);

                foreach (var param in method.ParameterDefinitions)
                {
                    FixCustomAttributes(param, selfName);
                }

                foreach (var gp in method.GenericParameters)
                {
                    FixCustomAttributes(gp, selfName);
                    foreach (var c in gp.Constraints)
                    {
                        FixTypeRef(c.Constraint, selfName);
                        FixCustomAttributes(c, selfName);
                    }
                }

                if (method.CilMethodBody is null)
                {
                    continue;
                }

                foreach (var local in method.CilMethodBody.LocalVariables)
                {
                    FixSig(local.VariableType, selfName);
                }

                foreach (var instr in method.CilMethodBody.Instructions)
                {
                    switch (instr.Operand)
                    {
                        case ITypeDefOrRef tdor:
                            FixTypeRef(tdor, selfName);
                            break;
                        case MemberReference mr:
                            FixTypeRef(mr.DeclaringType, selfName);
                            if (mr.Signature is MethodSignature mms)
                            {
                                FixMethodSig(mms, selfName);
                            }
                            else if (mr.Signature is FieldSignature fs)
                            {
                                FixSig(fs.FieldType, selfName);
                            }
                            break;
                        case MethodSpecification methodSpec:
                            FixTypeRef(methodSpec.DeclaringType, selfName);
                            if (methodSpec.Method is MemberReference smr)
                            {
                                FixTypeRef(smr.DeclaringType, selfName);
                                if (smr.Signature is MethodSignature smms)
                                {
                                    FixMethodSig(smms, selfName);
                                }
                            }
                            if (methodSpec.Signature is { } gsig)
                            {
                                foreach (var arg in gsig.TypeArguments)
                                {
                                    FixSig(arg, selfName);
                                }
                            }
                            break;
                        case StandAloneSignature sas when sas.Signature is MethodSignature stms:
                            FixMethodSig(stms, selfName);
                            break;
                    }
                }

                foreach (var handler in method.CilMethodBody.ExceptionHandlers)
                {
                    FixTypeRef(handler.ExceptionType, selfName);
                }
            }
        }

        if (_fixedCount > 0)
        {
            logger.LogInformation(
                "Fixed {Count} type reference(s) pointing to the self-referential assembly reference",
                _fixedCount
            );
        }
    }

    private void FixTypeRef(ITypeDefOrRef? candidate, Utf8String selfName)
    {
        while (true)
        {
            switch (candidate)
            {
                case null:
                    return;
                case TypeSpecification spec:
                    FixSig(spec.Signature, selfName);
                    return;
                case TypeReference typeRef when _visitedTypeRefs.Add(typeRef):
                    if (typeRef.Scope is AssemblyReference asmRef && asmRef.Name == selfName)
                    {
                        typeRef.Scope = _module;
                        _fixedCount++;
                    }
                    else if (typeRef.Scope is TypeReference parentRef)
                    {
                        candidate = parentRef;
                        continue;
                    }

                    return;
            }

            break;
        }
    }

    private void FixSig(TypeSignature? sig, Utf8String selfName)
    {
        if (sig is null || !_visitedSigs.Add(sig))
        {
            return;
        }

        switch (sig)
        {
            case TypeDefOrRefSignature tdor:
                FixTypeRef(tdor.Type, selfName);
                break;
            case GenericInstanceTypeSignature gen:
                FixTypeRef(gen.GenericType, selfName);
                foreach (var arg in gen.TypeArguments)
                {
                    FixSig(arg, selfName);
                }
                break;
            case CustomModifierTypeSignature mod:
                FixTypeRef(mod.ModifierType, selfName);
                FixSig(mod.BaseType, selfName);
                break;
            case TypeSpecificationSignature spec:
                FixSig(spec.BaseType, selfName);
                break;
            case FunctionPointerTypeSignature fp when fp.Signature is { } fpSig:
                FixSig(fpSig.ReturnType, selfName);
                foreach (var p in fpSig.ParameterTypes)
                {
                    FixSig(p, selfName);
                }
                break;
        }
    }

    private void FixMethodSig(MethodSignatureBase? sig, Utf8String selfName)
    {
        if (sig is null)
        {
            return;
        }

        FixSig(sig.ReturnType, selfName);
        foreach (var p in sig.ParameterTypes)
        {
            FixSig(p, selfName);
        }

        if (sig is MethodSignature ms)
        {
            foreach (var p in ms.SentinelParameterTypes)
            {
                FixSig(p, selfName);
            }
        }
    }

    private void FixCustomAttributes(IHasCustomAttribute target, Utf8String selfName)
    {
        foreach (var attr in target.CustomAttributes)
        {
            if (attr.Constructor is { } ctor)
            {
                FixTypeRef(ctor.DeclaringType, selfName);
                if (ctor.Signature is { } ctorSig)
                {
                    FixMethodSig(ctorSig, selfName);
                }
            }

            if (attr.Signature is null)
            {
                continue;
            }

            foreach (var fixedArg in attr.Signature.FixedArguments)
            {
                FixSig(fixedArg.ArgumentType, selfName);
                foreach (var element in fixedArg.Elements)
                {
                    if (element is TypeSignature ts)
                    {
                        FixSig(ts, selfName);
                    }
                }
            }

            foreach (var namedArg in attr.Signature.NamedArguments)
            {
                FixSig(namedArg.ArgumentType, selfName);
                FixSig(namedArg.Argument.ArgumentType, selfName);
                foreach (var element in namedArg.Argument.Elements)
                {
                    if (element is TypeSignature ts)
                    {
                        FixSig(ts, selfName);
                    }
                }
            }
        }
    }
}
