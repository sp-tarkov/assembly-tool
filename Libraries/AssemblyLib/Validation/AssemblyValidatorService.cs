using SPTarkov.DI.Annotations;

namespace AssemblyLib.Validation;

[Injectable(InjectionType.Singleton)]
public class AssemblyValidatorService(
    ILogger<AssemblyValidatorService> logger,
    IEnumerable<IAssemblyValidator> validators
)
{
    /// <summary>
    ///     Returns true if validation passed (no errors). Warnings do not block.
    /// </summary>
    public bool Validate(ValidationStage stage)
    {
        var staged = validators.Where(v => v.Enabled && v.Stage == stage).OrderByDescending(v => v.Priority).ToList();

        if (staged.Count == 0)
            return true;

        logger.LogInformation("Running {count} {stage} validators", staged.Count, stage);

        var allIssues = new List<ValidationIssue>();

        foreach (var validator in staged)
        {
            IReadOnlyList<ValidationIssue> issues;

            try
            {
                issues = validator.Validate();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Validator '{name}' threw an unhandled exception", validator.Name);
                continue;
            }

            allIssues.AddRange(issues);

            var errors = issues.Count(i => i.Severity == ValidationSeverity.Error);
            var warnings = issues.Count(i => i.Severity == ValidationSeverity.Warning);

            if (errors > 0 || warnings > 0)
            {
                logger.LogWarning(
                    "[{name}] {errors} error(s), {warnings} warning(s)",
                    validator.Name,
                    errors,
                    warnings
                );
            }
            else
            {
                logger.LogInformation("[{name}] passed", validator.Name);
            }

            foreach (var issue in issues)
            {
                if (issue.Severity == ValidationSeverity.Error)
                {
                    logger.LogError("  [{validator}] {message}", issue.ValidatorName, issue.Message);
                }
                else
                {
                    logger.LogWarning("  [{validator}] {message}", issue.ValidatorName, issue.Message);
                }
            }
        }

        var totalErrors = allIssues.Count(i => i.Severity == ValidationSeverity.Error);
        var totalWarnings = allIssues.Count(i => i.Severity == ValidationSeverity.Warning);

        if (totalErrors > 0)
        {
            logger.LogError(
                "{stage} validation failed — {errors} error(s), {warnings} warning(s). Aborting.",
                stage,
                totalErrors,
                totalWarnings
            );
        }
        else
        {
            logger.LogInformation("{stage} validation complete — {warnings} warning(s)", stage, totalWarnings);
        }

        return totalErrors == 0;
    }
}
