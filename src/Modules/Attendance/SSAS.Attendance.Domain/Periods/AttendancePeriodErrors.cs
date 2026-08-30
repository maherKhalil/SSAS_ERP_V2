using SSAS.BuildingBlocks.Domain;

namespace SSAS.Attendance.Domain.Periods;

public static class AttendancePeriodErrors
{
  public static readonly Error CompanyRequired = new(
    "Attendance.PeriodCompanyRequired",
    "An attendance period must belong to a company.",
    Field: "companyId");

  public static readonly Error InvalidName = new(
    "Attendance.PeriodNameInvalid",
    "An attendance period name is required and must be at most 200 characters.",
    Field: "name");

  public static readonly Error InvalidRange = new(
    "Attendance.PeriodRangeInvalid",
    "An attendance period cannot end before it starts.");

  public static readonly Error OverlapsExistingPeriod = new(
    "Attendance.PeriodOverlaps",
    "An attendance period overlapping these dates already exists in this company.");

  public static readonly Error NotFound = new(
    "Attendance.PeriodNotFound",
    "The attendance period does not exist.");

  public static readonly Error AlreadyClosed = new(
    "Attendance.PeriodAlreadyClosed",
    "The attendance period is already closed.");

  public static readonly Error AlreadyOpen = new(
    "Attendance.PeriodAlreadyOpen",
    "The attendance period is already open.");

  // The refusal a recorder meets when they try to write into a frozen period. The remedy is an adjustment
  // in the current open period (`OD-ATT-0012`), not a reopen — reopening exists for the case where the
  // close itself was premature.
  public static readonly Error PeriodClosed = new(
    "Attendance.PeriodClosed",
    "The attendance period is closed; record a correction as an adjustment in the current open period.");

  public static readonly Error NoOpenPeriod = new(
    "Attendance.NoOpenPeriod",
    "This company has no open attendance period covering that date.");
}
