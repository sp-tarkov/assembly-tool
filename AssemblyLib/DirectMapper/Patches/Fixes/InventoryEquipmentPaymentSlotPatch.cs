using AsmResolver.DotNet;
using AsmResolver.DotNet.Serialized;
using AssemblyLib.Exceptions;
using AssemblyLib.Shared;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Fixes;

[Injectable]
public class InventoryEquipmentPaymentSlotPatch(DataProvider dataProvider) : IPatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var inventoryEquipmentType = dataProvider
            .LoadedModule!.GetAllTypes()
            .FirstOrDefault(t => t.Namespace == "EFT.InventoryLogic" && t.Name == "InventoryEquipment");

        if (inventoryEquipmentType is null)
        {
            throw new FailedToFindTypeException("Could not find `Eft.InventoryEquipment` when patching");
        }

        var paymentSlotsField = inventoryEquipmentType.Fields.FirstOrDefault(f => f.Name == "_paymentSlots");
        if (paymentSlotsField is null)
        {
            throw new FailedToFindTypeException("Could not find _paymentSlots field");
        }

        var throwingGrenadeSlotsField = new FieldDefinition(
            "_throwingGrenadeSlots",
            paymentSlotsField.Attributes,
            paymentSlotsField.Signature!
        );
        inventoryEquipmentType.Fields.Add(throwingGrenadeSlotsField);

        var grenadeGetter = inventoryEquipmentType.Methods.FirstOrDefault(m => m.Name == "get_GrenadeThrowingSlots");
        if (grenadeGetter is null)
        {
            throw new FailedToFindTypeException("Could not find get_GrenadeThrowingSlots");
        }

        var instructions = grenadeGetter.CilMethodBody!.Instructions;

        foreach (var instruction in instructions)
        {
            if (instruction.Operand is SerializedFieldDefinition field && field.Name == "_paymentSlots")
            {
                instruction.Operand = throwingGrenadeSlotsField;
            }
        }

        instructions.CalculateOffsets();

        Log.Information("InventoryEquipmentPaymentSlotPatch Successful");
    }
}
