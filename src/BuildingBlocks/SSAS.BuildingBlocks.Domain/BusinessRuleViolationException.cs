namespace SSAS.BuildingBlocks.Domain;

public sealed class BusinessRuleViolationException : Exception
{
  public BusinessRuleViolationException(string message)
    : base(message)
  {
  }
}
