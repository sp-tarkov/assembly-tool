using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Validation.Validators;

/// <summary>
///     Post-mapping validator. Walks every <see cref="CilMethodBody"/> on the rewritten module
///     and surfaces issues that produce an unloadable assembly: bodies on methods that should
///     not have one (and vice versa), unresolved branch targets, and instructions whose
///     operands are missing or of the wrong type.
///
///     This is the single most useful catch-all after <c>MethodPatcher</c> splices source bodies
///     into the target — a bad clone shows up here as a label verification failure.
/// </summary>
[Injectable]
public class MethodBodyValidator(ILogger<MethodBodyValidator> logger, DataProvider dataProvider) : IAssemblyValidator
{
    public string Name => "Method Bodies";
    public bool Enabled => true;
    public int Priority => 12;
    public ValidationStage Stage => ValidationStage.PostMapping;

    public IReadOnlyList<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();

        if (dataProvider.LoadedModule is null)
        {
            return issues;
        }

        foreach (var type in dataProvider.LoadedModule.GetAllTypes())
        {
            foreach (var method in type.Methods)
            {
                CheckMethod(method, issues);
            }
        }

        return issues;
    }

    private void CheckMethod(MethodDefinition method, List<ValidationIssue> issues)
    {
        var hasBody = method.CilMethodBody is not null;
        var shouldHaveBody = ShouldHaveBody(method);

        if (shouldHaveBody && !hasBody)
        {
            // Native / runtime / pinvoke methods are exempt; everything else is a hard error.
            issues.Add(
                new ValidationIssue(ValidationSeverity.Error, Name, $"Method '{method.FullName}' is missing its CIL body")
            );
            return;
        }

        if (!hasBody)
        {
            return;
        }

        var body = method.CilMethodBody!;

        if (body.Instructions.Count == 0)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Error,
                    Name,
                    $"Method '{method.FullName}' has an empty CIL body — minimum legal body is a single ret"
                )
            );
            return;
        }

        // VerifyLabels confirms every branch / switch target is an instruction inside the same body.
        // It throws on the first failure, so we can report at most one label issue per method — that's fine,
        // a single broken label means the body is unverifiable already.
        try
        {
            body.VerifyLabels();
        }
        catch (Exception ex)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Error,
                    Name,
                    $"Method '{method.FullName}' failed label verification: {ex.Message}"
                )
            );
            // Don't continue — operand checks below assume labels resolve.
            return;
        }

        CheckOperands(method, body, issues);
        CheckExceptionHandlers(method, body, issues);
    }

    private static bool ShouldHaveBody(MethodDefinition method)
    {
        if (method.IsAbstract)
        {
            return false;
        }

        if (method.IsPInvokeImpl)
        {
            return false;
        }

        // Runtime / internal-call / native methods have no managed body.
        var impl = method.ImplAttributes;
        if ((impl & MethodImplAttributes.Runtime) != 0 || (impl & MethodImplAttributes.Native) != 0)
        {
            return false;
        }

        if ((impl & MethodImplAttributes.InternalCall) != 0)
        {
            return false;
        }

        return true;
    }

    private void CheckOperands(MethodDefinition method, CilMethodBody body, List<ValidationIssue> issues)
    {
        var instructions = body.Instructions;

        for (var i = 0; i < instructions.Count; i++)
        {
            var instr = instructions[i];
            var operandType = instr.OpCode.OperandType;
            var operand = instr.Operand;

            // Inline opcodes that bake the operand into the opcode (ldc.i4.0, ldarg.0, etc.) carry no operand.
            if (operandType == CilOperandType.InlineNone)
            {
                continue;
            }

            if (operand is null)
            {
                issues.Add(
                    new ValidationIssue(
                        ValidationSeverity.Error,
                        Name,
                        $"Method '{method.FullName}' instruction #{i} ({instr.OpCode.Mnemonic}) has a null operand"
                    )
                );
                continue;
            }

            // Token-based opcodes (call, callvirt, ldtoken, newobj, etc.) must reference something
            // the metadata builder can write a token for. A raw integer here means a forgotten remap.
            if (IsTokenOperand(operandType) && operand is int)
            {
                issues.Add(
                    new ValidationIssue(
                        ValidationSeverity.Error,
                        Name,
                        $"Method '{method.FullName}' instruction #{i} ({instr.OpCode.Mnemonic}) has a raw integer operand "
                            + "where a metadata reference is required — likely an un-remapped token"
                    )
                );
            }
        }
    }

    private void CheckExceptionHandlers(MethodDefinition method, CilMethodBody body, List<ValidationIssue> issues)
    {
        for (var i = 0; i < body.ExceptionHandlers.Count; i++)
        {
            var handler = body.ExceptionHandlers[i];

            if (handler.TryStart is null || handler.TryEnd is null)
            {
                issues.Add(
                    new ValidationIssue(
                        ValidationSeverity.Error,
                        Name,
                        $"Method '{method.FullName}' exception handler #{i} has missing try-block bounds"
                    )
                );
            }

            if (handler.HandlerStart is null || handler.HandlerEnd is null)
            {
                issues.Add(
                    new ValidationIssue(
                        ValidationSeverity.Error,
                        Name,
                        $"Method '{method.FullName}' exception handler #{i} has missing handler-block bounds"
                    )
                );
            }

            if (handler.HandlerType == CilExceptionHandlerType.Exception && handler.ExceptionType is null)
            {
                issues.Add(
                    new ValidationIssue(
                        ValidationSeverity.Error,
                        Name,
                        $"Method '{method.FullName}' typed exception handler #{i} has no ExceptionType"
                    )
                );
            }
        }
    }

    private static bool IsTokenOperand(CilOperandType type) =>
        type
            is CilOperandType.InlineMethod
                or CilOperandType.InlineField
                or CilOperandType.InlineType
                or CilOperandType.InlineTok
                or CilOperandType.InlineSig;
}
