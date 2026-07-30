namespace SSAS.BuildingBlocks.Domain;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
  "Naming",
  "CA1716:Identifiers should not match keywords",
  Justification = "Error is an approved BuildingBlocks primitive in the Sprint-00 specification.")]
public sealed record Error(string Code, string Message)
{
  public static readonly Error None = new(string.Empty, string.Empty);
}
