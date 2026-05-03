using AsmResolver.DotNet;
using AsmResolver.DotNet.Serialized;
using EFT.InventoryLogic;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Patching.ModulePatches.Fixes;

[Injectable]
public class InventoryEquipmentPaymentSlotPatch(MemberLookup.MemberLookup lookup) : IModulePatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var inventoryEquipmentType = lookup.Eft.Type<InventoryEquipment>();
        if (inventoryEquipmentType is null)
        {
            throw new FailedToFindTypeException("Could not find `Eft.InventoryEquipment` when patching");
        }

        var paymentSlotsField = lookup.Eft.Field<InventoryEquipment>(nameof(InventoryEquipment._paymentSlots));
        if (paymentSlotsField is null)
        {
            throw new FailedToFindTypeException("Could not find _paymentSlots field");
        }

        var throwingGrenadeSlotsField = new FieldDefinition(
            "_grenadeThrowingSlots",
            paymentSlotsField.Attributes,
            paymentSlotsField.Signature!
        );
        inventoryEquipmentType.Fields.Add(throwingGrenadeSlotsField);

        var grenadeGetter = lookup.Eft.Method<InventoryEquipment>("get_GrenadeThrowingSlots");
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
    }
}
