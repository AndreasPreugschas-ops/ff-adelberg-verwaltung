namespace Fevad.Domain.Gemeinsam;

/// <summary>
/// Der Wert dieser Eigenschaft wird nicht ins Änderungsprotokoll kopiert, z. B. weil er einer
/// eigenen Löschfrist unterliegt. Protokolliert wird nur, dass sich das Feld geändert hat.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NichtProtokollierenAttribute : Attribute
{
}
