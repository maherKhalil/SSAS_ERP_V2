using System.Globalization;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Application.Employees;
using SSAS.HR.Domain.Employees;
using SSAS.HR.Domain.ImportExport;

namespace SSAS.HR.Application.ImportExport;

// THE CAPS (DEC-DOC-0005).
//
// Configuration values, not architectural constants — which is why they are a record with defaults rather
// than `const int` on the handler. `DEC-DOC-0007`'s synchronous execution is what they are aligned with, so
// when they rise, execution is what gets revisited.
public sealed record EmployeeImportLimits(int MaximumRows = 5_000, int MaximumBytes = 10 * 1024 * 1024)
{
  public static readonly EmployeeImportLimits Default = new();
}

// The command. It carries the file's CONTENT rather than a stream, because the transport has already
// decoded it — `StrictCsvReader` owns the content-type gate and the UTF-8 refusal, and this handler owns
// everything that has an audit consequence.
//
// `ByteCount` is the size the transport measured, not `Content.Length`: a UTF-8 character is one to four
// bytes, so the cap the operator was promised is a byte cap and must be checked against bytes.
public sealed record ImportEmployeesCommand(
  string? Content,
  string ImportKey,
  string FileName,
  int ByteCount,
  bool ValidateOnly = false);

// One thing wrong with one row, as the report carries it. The domain `Error` travels rather than a wire
// code: mapping to `employee.number_conflict` is the API layer's job and it already has the mapper that
// does it, so doing it here would give one condition two mappings and let them drift.
public sealed record EmployeeImportReport(
  Guid ImportRunId,
  EmployeeImportOutcome Outcome,
  int RowCount,
  int AcceptedCount,
  int RejectedCount,
  IReadOnlyList<EmployeeImportRowError> Errors,
  bool WasReplayed = false);

// IMPORT EMPLOYEES FROM A CSV FILE (FR-DOC-0101, FR-DOC-0102).
//
// ================================================================================================
// THIS HANDLER INVENTS NO SECOND CREATE PATH (BRULE-DOC-0603).
// ================================================================================================
//
// Every employee is created by `CreateEmployeeCommandHandler`, the same one a single `POST` uses. An import
// that assembled `Employee.Create` itself would be a second place an employee can come into existence, and
// the two would diverge the first time a rule changed — the initial branch, department and position
// assignments, the uniqueness probes, the inactive-department rule, all of it duplicated and none of it
// obviously duplicated.
//
// ---- ALL OR NOTHING (OD-DOC-003), WHICH IS WHY VALIDATION FINISHES BEFORE ANY WRITE BEGINS.
//
// Every row is validated and every error is reported; a single bad row refuses the file and nothing is
// written. Validating up front rather than creating-until-something-breaks is not an optimisation: it is
// what makes the report complete. A first-failure import would cost the operator one round trip per bad row
// to discover the rest.
//
// ---- THE KEY IS CONSUMED BY EVERY OUTCOME, INCLUDING REFUSAL (DEC-DOC-0004).
//
// A refused run still writes its record, so the key it used is gone. Releasing it would let the submission
// the key exists to make unrepeatable be replayed under it. That is why the refusal paths below write a run
// record rather than simply returning.
public sealed class ImportEmployeesCommandHandler(
  IEmployeeRepository employees,
  IEmployeeImportRunRepository runs,
  CreateEmployeeCommandHandler createEmployee,
  ITenantUnitOfWork unitOfWork,
  ICurrentTenant currentTenant,
  ICurrentCompany currentCompany,
  ICurrentUser currentUser,
  IDateTimeProvider clock,
  EmployeeImportLimits? limits = null)
{
  private readonly EmployeeImportLimits limits = limits ?? EmployeeImportLimits.Default;

  public async Task<Result<EmployeeImportReport>> HandleAsync(
    ImportEmployeesCommand command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (currentTenant.TenantId is not { } tenantId ||
      currentCompany.CompanyId is not { } companyId ||
      string.IsNullOrWhiteSpace(currentUser.UserId))
    {
      return Result.Failure<EmployeeImportReport>(EmployeeErrors.InvalidActor);
    }

    var importKey = ImportKey.Create(command.ImportKey);
    if (importKey.IsFailure)
    {
      return Result.Failure<EmployeeImportReport>(importKey.Error);
    }

    // ---- THE REPLAY, ANSWERED BEFORE ANYTHING ELSE IS EVEN PARSED.
    //
    // The question this answers is "did my import happen?", asked by an operator whose connection dropped
    // and who cannot tell whether five thousand employees exist. Answering it requires no file, so the file
    // is not consulted — and re-validating one here would let a replay of the same key report a DIFFERENT
    // outcome than the run it is replaying, which is the one thing a replay may not do.
    //
    // The errors list is empty on a replay, and that is a consequence of `DEC-DOC-0003` rather than a gap:
    // rejected rows are reported and counted, never persisted, because persisting them would mean storing
    // rejected PII indefinitely with no rule saying for how long.
    var existing = await runs.FindByKeyAsync(companyId, importKey.Value.NormalizedValue, cancellationToken);
    if (existing is not null)
    {
      return Result.Success(new EmployeeImportReport(
        existing.Id, existing.Outcome, existing.RowCount, existing.AcceptedCount, existing.RejectedCount,
        [], WasReplayed: true));
    }

    // ---- THE BYTE CAP, RE-CHECKED HERE (DEC-DOC-0005, R2).
    //
    // The transport floor stops the bytes; this owns the contract's error shape. A body that never reached
    // the floor's limit still reaches this check, so the number the operator is told is the number this
    // layer measured rather than one inferred from a connection reset.
    if (command.ByteCount > limits.MaximumBytes)
    {
      return await RefuseAsync(
        tenantId, companyId, importKey.Value, command, rowCount: 0,
        [new EmployeeImportRowError(0, null, EmployeeImportErrors.ByteLimitExceeded)], cancellationToken);
    }

    var parsed = EmployeeImportCsvParser.Parse(command.Content);
    if (parsed.IsFailure)
    {
      // A HEADER FAILURE IS A REFUSED RUN, not merely a 400. It consumed the key like any other attempt, and
      // the audit trail records that somebody tried to import a file this company would not accept.
      return await RefuseAsync(
        tenantId, companyId, importKey.Value, command, rowCount: 0,
        [new EmployeeImportRowError(1, null, parsed.Error)], cancellationToken);
    }

    var file = parsed.Value;
    var rowCount = file.Rows.Count + file.Errors.Count;

    if (rowCount > limits.MaximumRows)
    {
      return await RefuseAsync(
        tenantId, companyId, importKey.Value, command, rowCount,
        [new EmployeeImportRowError(0, null, EmployeeImportErrors.RowLimitExceeded)], cancellationToken);
    }

    var validated = await ValidateAsync(companyId, file, cancellationToken);

    if (validated.Errors.Count > 0)
    {
      return await RefuseAsync(
        tenantId, companyId, importKey.Value, command, rowCount, validated.Errors, cancellationToken);
    }

    // ---- THE DRY RUN WRITES ITS RECORD AND NOTHING ELSE (FR-DOC-0101).
    //
    // `Validated` is reachable only from here. It means the file was checked and nothing was written, which
    // is a fact worth recording precisely because it looks from the outside like nothing happened.
    if (command.ValidateOnly)
    {
      return await RecordAsync(
        EmployeeImportRun.Validated(
          tenantId, companyId, importKey.Value, command.FileName, command.ByteCount, rowCount,
          clock.UtcNow, currentUser.UserId!),
        [], cancellationToken);
    }

    return await ApplyAsync(
      tenantId, companyId, importKey.Value, command, rowCount, validated.Prepared, cancellationToken);
  }

  // ================================================================================================
  // VALIDATION — EVERY ROW, EVERY ERROR (DEC-DOC-0003)
  // ================================================================================================
  private async Task<ValidationOutcome> ValidateAsync(
    Guid companyId, EmployeeImportFile file, CancellationToken cancellationToken)
  {
    // Structural failures found by the parser are already errors and keep their row numbers.
    var errors = new List<EmployeeImportRowError>(file.Errors);
    var prepared = new List<PreparedRow>(file.Rows.Count);

    // ---- THE TWO SETS THE DATABASE CANNOT SEE.
    //
    // Two rows of one file claiming the same employee number both pass `EmployeeNumberExistsAsync` — neither
    // exists yet — and then collide on the unique index partway through the apply, in a save the
    // all-or-nothing rule rolls back with nothing to tell the operator. Detected here so the report names
    // the second row rather than the file failing for a reason nobody can see.
    var numbersInFile = new HashSet<string>(StringComparer.Ordinal);
    var nationalIdsInFile = new HashSet<string>(StringComparer.Ordinal);

    // Resolved codes are cached per file: a thousand employees in one department must not be a thousand
    // identical lookups, and the answer cannot change inside one import because nothing in this handler
    // creates organizational structure (`BRULE-DOC-0601`).
    var departments = new Dictionary<string, DepartmentAssignmentTarget?>(StringComparer.OrdinalIgnoreCase);
    var positions = new Dictionary<string, PositionAssignmentTarget?>(StringComparer.OrdinalIgnoreCase);

    foreach (var row in file.Rows)
    {
      var rowErrors = new List<EmployeeImportRowError>();

      var employeeNumber = await ValidateEmployeeNumberAsync(
        companyId, row, numbersInFile, rowErrors, cancellationToken);

      var fullName = Validate(
        row, EmployeeImportColumns.FullName, EmployeeFullName.Create, rowErrors);

      var employmentDate = ValidateEmploymentDate(row, rowErrors);

      var nationalId = await ValidateNationalIdAsync(
        companyId, row, nationalIdsInFile, rowErrors, cancellationToken);

      ValidateStatus(row, rowErrors);

      var department = await ResolveDepartmentAsync(
        companyId, row, departments, rowErrors, cancellationToken);

      var position = await ResolvePositionAsync(
        companyId, row, positions, rowErrors, cancellationToken);

      if (rowErrors.Count > 0)
      {
        errors.AddRange(rowErrors);
        continue;
      }

      prepared.Add(new PreparedRow(
        row.RowNumber,
        employeeNumber!,
        fullName!,
        employmentDate!.Value,
        row.Value(EmployeeImportColumns.NationalId),
        department!.DepartmentId,
        position!.PositionId));

      _ = nationalId;
    }

    return new ValidationOutcome(
      [.. errors.OrderBy(error => error.RowNumber)], prepared);
  }

  private async Task<EmployeeNumber?> ValidateEmployeeNumberAsync(
    Guid companyId,
    EmployeeImportRow row,
    HashSet<string> seen,
    List<EmployeeImportRowError> rowErrors,
    CancellationToken cancellationToken)
  {
    var parsed = EmployeeNumber.Create(row.Value(EmployeeImportColumns.EmployeeNumber));
    if (parsed.IsFailure)
    {
      rowErrors.Add(new EmployeeImportRowError(
        row.RowNumber, EmployeeImportColumns.EmployeeNumber, parsed.Error));
      return null;
    }

    if (!seen.Add(parsed.Value.NormalizedValue))
    {
      rowErrors.Add(new EmployeeImportRowError(
        row.RowNumber, EmployeeImportColumns.EmployeeNumber, EmployeeImportErrors.DuplicateWithinFile));
      return null;
    }

    // CREATE-ONLY (`OD-DOC-002`): an existing employee number is a row error, never an update. An import
    // that quietly updated would let a spreadsheet rewrite employment records with no history of the edit.
    if (await employees.EmployeeNumberExistsAsync(
      companyId, parsed.Value.NormalizedValue, cancellationToken))
    {
      rowErrors.Add(new EmployeeImportRowError(
        row.RowNumber, EmployeeImportColumns.EmployeeNumber, EmployeeErrors.NumberConflict));
      return null;
    }

    return parsed.Value;
  }

  private async Task<NationalId?> ValidateNationalIdAsync(
    Guid companyId,
    EmployeeImportRow row,
    HashSet<string> seen,
    List<EmployeeImportRowError> rowErrors,
    CancellationToken cancellationToken)
  {
    var raw = row.Value(EmployeeImportColumns.NationalId);
    if (raw is null)
    {
      return null;
    }

    var parsed = NationalId.Create(raw);
    if (parsed.IsFailure)
    {
      rowErrors.Add(new EmployeeImportRowError(
        row.RowNumber, EmployeeImportColumns.NationalId, parsed.Error));
      return null;
    }

    if (!seen.Add(parsed.Value.NormalizedValue))
    {
      rowErrors.Add(new EmployeeImportRowError(
        row.RowNumber, EmployeeImportColumns.NationalId, EmployeeImportErrors.DuplicateWithinFile));
      return null;
    }

    if (await employees.NationalIdExistsAsync(
      companyId, parsed.Value.NormalizedValue, cancellationToken))
    {
      rowErrors.Add(new EmployeeImportRowError(
        row.RowNumber, EmployeeImportColumns.NationalId, EmployeeErrors.NationalIdConflict));
      return null;
    }

    return parsed.Value;
  }

  // ---- ONE DATE FORMAT, AND NOT "WHATEVER PARSES" (DEC-DOC-0008).
  //
  // `yyyy-MM-dd` exactly, invariant culture, no time component. A permissive parse would read `03/04/2026`
  // as either 3 April or 4 March depending on the machine's culture and silently record the wrong
  // employment date — a value nobody would ever look at again. The export writes this same format, so the
  // round-trip property holds against a real referent rather than an assumed one.
  private static DateTimeOffset? ValidateEmploymentDate(
    EmployeeImportRow row, List<EmployeeImportRowError> rowErrors)
  {
    var raw = row.Value(EmployeeImportColumns.EmploymentDate);

    if (raw is not null && DateOnly.TryParseExact(
      raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
    {
      return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }

    rowErrors.Add(new EmployeeImportRowError(
      row.RowNumber, EmployeeImportColumns.EmploymentDate, EmployeeImportErrors.EmploymentDateInvalid));

    return null;
  }

  // ---- `status` IS READ AND CHECKED, AND SETS NOTHING (OD-DOC-010).
  //
  // Nothing downstream consumes the value: `CreateEmployeeCommand` has no status parameter, and creation
  // produces `Active`. This exists so the file can SAY what it means and be told when the system cannot
  // honour it — which is the difference between a recognized column and an ignored one.
  //
  // A `status=Terminated` row is refused rather than created-then-terminated. Create-only cannot recreate a
  // terminated person's employment history — the dates, the reason, the branch they left from — and an
  // import that resurrected them as new Active hires would be a worse outcome than the refusal, because it
  // would look like it worked.
  private static void ValidateStatus(EmployeeImportRow row, List<EmployeeImportRowError> rowErrors)
  {
    var raw = row.Value(EmployeeImportColumns.Status);

    if (raw is null ||
      string.Equals(raw, EmployeeImportColumns.CreatableStatus, StringComparison.OrdinalIgnoreCase))
    {
      return;
    }

    rowErrors.Add(new EmployeeImportRowError(
      row.RowNumber, EmployeeImportColumns.Status, EmployeeImportErrors.StatusNotCreatable));
  }

  private static T? Validate<T>(
    EmployeeImportRow row,
    string column,
    Func<string?, Result<T>> create,
    List<EmployeeImportRowError> rowErrors) where T : class
  {
    var parsed = create(row.Value(column));
    if (parsed.IsSuccess)
    {
      return parsed.Value;
    }

    rowErrors.Add(new EmployeeImportRowError(row.RowNumber, column, parsed.Error));

    return null;
  }

  // ================================================================================================
  // CLASSIFICATION RESOLUTION BY CODE, UNDER THE IMPORTER'S OWN AUTHORITY (OD-DOC-004)
  // ================================================================================================
  //
  // Resolved WITHIN THE TRUSTED COMPANY and nowhere else, which is the same predicate the single-create path
  // applies to a department identifier. That is what preserves the property `OD-DOC-004` names: a code in a
  // company the caller cannot see is reported ABSENT, identically to a code that exists nowhere, so a file
  // cannot discover which department codes exist elsewhere one rejection message at a time.
  //
  // AN IMPORT NEVER CREATES ORGANIZATIONAL STRUCTURE (`BRULE-DOC-0601`). There is no code path here that
  // could — not a flag, not a fallback, not an "unassigned" default. A typo becomes a rejected row, which is
  // recoverable; a typo becoming a permanent org unit is not.
  private async Task<DepartmentAssignmentTarget?> ResolveDepartmentAsync(
    Guid companyId,
    EmployeeImportRow row,
    Dictionary<string, DepartmentAssignmentTarget?> cache,
    List<EmployeeImportRowError> rowErrors,
    CancellationToken cancellationToken)
  {
    var code = row.Value(EmployeeImportColumns.DepartmentCode);
    if (code is null)
    {
      rowErrors.Add(new EmployeeImportRowError(
        row.RowNumber, EmployeeImportColumns.DepartmentCode, EmployeeErrors.DepartmentRequired));
      return null;
    }

    if (!cache.TryGetValue(code, out var target))
    {
      target = await employees.FindAssignableDepartmentByCodeAsync(
        companyId, code.ToUpperInvariant(), cancellationToken);
      cache[code] = target;
    }

    if (target is null)
    {
      rowErrors.Add(new EmployeeImportRowError(
        row.RowNumber, EmployeeImportColumns.DepartmentCode, EmployeeErrors.DepartmentNotFound));
      return null;
    }

    // An INACTIVE department keeps the employees it has and accepts no new ones, exactly as the single
    // create path rules. Named plainly, because the department is one the caller can see.
    if (!target.IsActive)
    {
      rowErrors.Add(new EmployeeImportRowError(
        row.RowNumber, EmployeeImportColumns.DepartmentCode, EmployeeErrors.DepartmentInactive));
      return null;
    }

    return target;
  }

  private async Task<PositionAssignmentTarget?> ResolvePositionAsync(
    Guid companyId,
    EmployeeImportRow row,
    Dictionary<string, PositionAssignmentTarget?> cache,
    List<EmployeeImportRowError> rowErrors,
    CancellationToken cancellationToken)
  {
    var code = row.Value(EmployeeImportColumns.PositionCode);
    if (code is null)
    {
      rowErrors.Add(new EmployeeImportRowError(
        row.RowNumber, EmployeeImportColumns.PositionCode, EmployeeErrors.PositionRequired));
      return null;
    }

    if (!cache.TryGetValue(code, out var target))
    {
      target = await employees.FindAssignablePositionByCodeAsync(
        companyId, code.ToUpperInvariant(), cancellationToken);
      cache[code] = target;
    }

    if (target is null)
    {
      rowErrors.Add(new EmployeeImportRowError(
        row.RowNumber, EmployeeImportColumns.PositionCode, EmployeeErrors.PositionNotFound));
      return null;
    }

    if (!target.IsActive)
    {
      rowErrors.Add(new EmployeeImportRowError(
        row.RowNumber, EmployeeImportColumns.PositionCode, EmployeeErrors.PositionInactive));
      return null;
    }

    return target;
  }

  // ================================================================================================
  // APPLY — THE EMPLOYEES AND THE RUN RECORD COMMIT TOGETHER, OR NEITHER DOES
  // ================================================================================================
  // ================================================================================================
  // APPLY — AND THE REFUSAL IS RECORDED **OUTSIDE** THE TRANSACTION IT DISCARDS
  // ================================================================================================
  //
  // ---- WHY THIS IS TWO METHODS AND NOT ONE, STATED ACCURATELY.
  //
  // The first version wrote the refusal record inside the `await using` scope, immediately after rolling
  // back, and that was SUSPECTED of being a defect: `await using var transaction` runs to the end of the
  // METHOD, so the rolled-back transaction looked like it would still be the context's current transaction
  // when the refusal's `SaveChangesAsync` executed.
  //
  // **IT WAS NOT A DEFECT, and the check is worth recording because the reasoning looked sound.**
  // `EfUnitOfWork.RollbackAsync` DISPOSES the transaction in its `finally` and nulls its own field, which
  // clears it from the `DbContext` — so the following save opens its own transaction and commits normally.
  // Reintroducing the original shape and running `I15` against real SQL confirmed it: the test passed
  // either way.
  //
  // The split is kept anyway, for one honest reason and not the one first claimed: it makes the transaction
  // UNREACHABLE from the method that decides what to record, so the question does not have to be re-derived
  // by the next reader. It is clarity, not a fix.
  private async Task<Result<EmployeeImportReport>> ApplyAsync(
    Guid tenantId,
    Guid companyId,
    ImportKey importKey,
    ImportEmployeesCommand command,
    int rowCount,
    IReadOnlyList<PreparedRow> prepared,
    CancellationToken cancellationToken)
  {
    var outcome = await CommitEmployeesAsync(
      tenantId, companyId, importKey, command, rowCount, prepared, cancellationToken);

    // ---- THE RACE. Validation passed and the write still failed.
    //
    // The per-company unique indexes are authoritative and the validation probes are an optimisation of the
    // error message, not the rule — so a concurrent create can take a number between the probe and the
    // insert. Everything written is already rolled back and the transaction is disposed; the key must still
    // be consumed, so the refusal is recorded now, on its own.
    if (outcome.Raced is { } raced)
    {
      return await RefuseAsync(
        tenantId, companyId, importKey, command, rowCount, [raced], cancellationToken);
    }

    if (outcome.Run is not { } run)
    {
      return Result.Failure<EmployeeImportReport>(outcome.Fatal ?? EmployeeErrors.InvalidActor);
    }

    return Result.Success(new EmployeeImportReport(
      run.Id, run.Outcome, run.RowCount, run.AcceptedCount, run.RejectedCount, []));
  }

  // What the committed attempt produced: the run on success, the offending row on a race, or an error the
  // caller cannot recover from. Exactly one is ever set.
  private sealed record ApplyOutcome(
    EmployeeImportRun? Run, EmployeeImportRowError? Raced, Error? Fatal);

  // ---- THE TRANSACTION LIVES AND DIES HERE, and nothing that writes a run record is in scope.
  private async Task<ApplyOutcome> CommitEmployeesAsync(
    Guid tenantId,
    Guid companyId,
    ImportKey importKey,
    ImportEmployeesCommand command,
    int rowCount,
    IReadOnlyList<PreparedRow> prepared,
    CancellationToken cancellationToken)
  {
    await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

    foreach (var row in prepared)
    {
      var created = await createEmployee.HandleAsync(
        new CreateEmployeeCommand(
          row.EmployeeNumber.Value,
          row.FullName.Value,
          row.EmploymentDate,
          row.NationalId,
          row.DepartmentId,
          row.PositionId),
        cancellationToken);

      if (created.IsFailure)
      {
        await transaction.RollbackAsync(cancellationToken);

        return new ApplyOutcome(
          null, new EmployeeImportRowError(row.RowNumber, null, created.Error), null);
      }
    }

    var run = EmployeeImportRun.Applied(
      tenantId, companyId, importKey, command.FileName, command.ByteCount, rowCount,
      clock.UtcNow, currentUser.UserId!);
    if (run.IsFailure)
    {
      await transaction.RollbackAsync(cancellationToken);
      return new ApplyOutcome(null, null, run.Error);
    }

    // THE RUN RECORD AND THE EMPLOYEES COMMIT TOGETHER. An applied run whose record did not land would be
    // employees nobody can account for; a record without its employees would describe something that did
    // not happen.
    await runs.AddAsync(run.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saved.IsFailure)
    {
      await transaction.RollbackAsync(cancellationToken);
      return new ApplyOutcome(null, null, saved.Error);
    }

    await transaction.CommitAsync(cancellationToken);

    return new ApplyOutcome(run.Value, null, null);
  }

  private async Task<Result<EmployeeImportReport>> RefuseAsync(
    Guid tenantId,
    Guid companyId,
    ImportKey importKey,
    ImportEmployeesCommand command,
    int rowCount,
    IReadOnlyList<EmployeeImportRowError> errors,
    CancellationToken cancellationToken)
  {
    // Rows rejected, not errors reported: several errors may belong to one row, and the count the run
    // records is how many ROWS did not make it — which is the number that can be compared with `RowCount`.
    var rejected = errors.Select(error => error.RowNumber).Distinct().Count(number => number > 0);

    return await RecordAsync(
      EmployeeImportRun.Refused(
        tenantId, companyId, importKey, command.FileName, command.ByteCount, rowCount,
        Math.Min(rejected, rowCount), clock.UtcNow, currentUser.UserId!),
      errors,
      cancellationToken);
  }

  private async Task<Result<EmployeeImportReport>> RecordAsync(
    Result<EmployeeImportRun> run,
    IReadOnlyList<EmployeeImportRowError> errors,
    CancellationToken cancellationToken)
  {
    if (run.IsFailure)
    {
      return Result.Failure<EmployeeImportReport>(run.Error);
    }

    await runs.AddAsync(run.Value, cancellationToken);

    var saved = await unitOfWork.SaveChangesAsync(cancellationToken);

    return saved.IsFailure
      ? Result.Failure<EmployeeImportReport>(saved.Error)
      : Result.Success(new EmployeeImportReport(
        run.Value.Id, run.Value.Outcome, run.Value.RowCount, run.Value.AcceptedCount,
        run.Value.RejectedCount, errors));
  }

  private sealed record ValidationOutcome(
    IReadOnlyList<EmployeeImportRowError> Errors, IReadOnlyList<PreparedRow> Prepared);

  // A row that passed every check, holding the PARSED values rather than the raw text. Re-parsing them in
  // the apply loop would be a second place the rules live, and the second place is where they drift.
  private sealed record PreparedRow(
    int RowNumber,
    EmployeeNumber EmployeeNumber,
    EmployeeFullName FullName,
    DateTimeOffset EmploymentDate,
    string? NationalId,
    Guid DepartmentId,
    Guid PositionId);
}
