namespace SSAS.BuildingBlocks.Domain;

public interface IAuditableEntity
{
  DateTimeOffset CreatedUtc { get; set; }

  DateTimeOffset ModifiedUtc { get; set; }

  string? CreatedBy { get; set; }

  string? ModifiedBy { get; set; }
}
