using AsmResolver.DotNet;
using AssemblyLib.DirectMapper.AttributeFactory.Builders;
using AssemblyLib.Shared;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.AttributeFactory;

[Injectable(InjectionType.Singleton)]
public class AttributeFactoryService(
    ILogger<AttributeFactoryService> logger,
    IEnumerable<IAttributeBuilder> builders,
    DataProvider dataProvider
)
{
    /// <summary>
    ///     Force-initializes all custom attribute signatures before any renaming occurs.
    /// AsmResolver deserializes blobs lazily; if a type is renamed first, the blob
    /// parser can no longer resolve the old type name and throws.
    /// </summary>
    public void PreInitializeAllAttributeSignatures()
    {
        foreach (var type in dataProvider.LoadedModule!.GetAllTypes())
        {
            ForceInitAttributes(type);

            foreach (var method in type.Methods)
            {
                ForceInitAttributes(method);
            }

            foreach (var property in type.Properties)
            {
                ForceInitAttributes(property);
            }

            foreach (var field in type.Fields)
            {
                ForceInitAttributes(field);
            }
        }
    }

    public void UpdateAsyncAttributes()
    {
        var asyncBuilder = builders.FirstOrDefault(b => b is AsyncAttributeBuilder);
        if (asyncBuilder is null)
        {
            logger.LogError("Could not find AsyncAttributeBuilder");
            return;
        }

        asyncBuilder.Build();
    }

    public void UpdateConverterAttributes()
    {
        var converterBuilders = builders.Where(b =>
            b is JsonConverterAttributeBuilder or TypeConverterAttributeBuilder
        );

        foreach (var converterBuilder in converterBuilders)
        {
            converterBuilder.Build();
        }
    }

    private void ForceInitAttributes(IHasCustomAttribute target)
    {
        foreach (var attr in target.CustomAttributes)
        {
            try
            {
                // Accessing FixedArguments triggers lazy blob deserialization.
                _ = attr.Signature?.FixedArguments.Count;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    "Could not pre-initialize attribute {Attr}: {Message}",
                    attr.Constructor?.DeclaringType?.FullName,
                    ex.Message
                );
            }
        }
    }
}
