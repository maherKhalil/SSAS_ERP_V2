using SSAS.BuildingBlocks.Domain;

namespace SSAS.GL.Domain.Accounts;

// THE CHART OF ACCOUNTS (REQ-GL-0005..0008, BR-GL-0004, OD-GL-0003).
//
// ================================================================================================
// TENANT-OWNED, AND DELIBERATELY NOT COMPANY-OWNED.
// ================================================================================================
//
// `OD-GL-0003` ruled the chart TENANT-level: one chart, shared by every company in the tenant. So this type
// implements `ITenantOwnedEntity` and **not** `ICompanyOwnedEntity`, and that is not a column decision — it
// decides what the write boundary does. An `ICompanyOwnedEntity` makes every save a company-scoped write
// running `AuthorizeCurrentCompanyAsync`; account maintenance is instead a tenant-level write authorized by
// permission alone. A reader who sees only the absent `CompanyId` will not see that difference, which is why
// it is stated here.
//
// **Balances are never stored above the company**, which is the other half of the ruling and the reason it
// does not disturb money. Every posted amount lives on a journal line beneath a company-owned journal, so
// every amount still has exactly one unambiguous currency and `ADR-027` decision 3 condition 2 — "an amount
// is stored above the Company level" — stays untriggered. The chart is shared; the money is not.
//
// ---- DEACTIVATION, NEVER DELETION.
//
// `BR-GL-0004` says an inactive account "cannot receive transactions" — note what it does not say. It does
// not say the account disappears, and it does not say its history becomes invalid. This is the same
// lifecycle posture `TenantDbContext.PreventCompanyDeletion` enforces for Company so "history stays
// reconstructable", and GL follows that precedent rather than inventing a second pattern.
//
// Reactivation is permitted without ceremony. `lifecycle-model.md` recorded that as a visible suggestion
// rather than a silent implementation: an account is master data, not a closed book.
public sealed class Account : AggregateRoot<Guid>, IAuditableEntity, ITenantOwnedEntity
{
  private string normalizedCode = string.Empty;
  private string normalizedName = string.Empty;

  private Account(Guid id, AccountCode code, AccountName name)
    : base(id)
  {
    Code = code;
    normalizedCode = code.NormalizedValue;
    Name = name;
    normalizedName = name.Value.ToUpperInvariant();
    IsActive = true;
  }

  // EF materialization only.
  private Account(Guid id)
    : base(id)
  {
    Code = null!;
    Name = null!;
  }

  public Guid TenantId { get; set; }

  // ---- THE CODE IS IMMUTABLE FROM CREATION.
  //
  // `REQ-GL-0006` raised whether it is immutable from creation or only once transactions reference it, and
  // recorded that no existing rule settles it. This takes the stricter reading, for a reason the looser one
  // cannot answer: "once used" means the aggregate must know whether any journal line points at it, which is
  // a query across another aggregate at write time. The strict rule needs no such knowledge and cannot drift
  // out of step with the data. Renaming an account is a correction; re-coding one silently re-labels history.
  public AccountCode Code { get; private set; }

  public AccountName Name { get; private set; }

  // ---- THE TWO NORMALIZED SHADOWS, AND WHY BOTH EXIST FOR DIFFERENT REASONS.
  //
  // `NormalizedCode` backs the ORDINAL UNIQUE INDEX. Comparison is binary-collated, so two codes differing
  // only in case are the same code and the database is what says so.
  //
  // `NormalizedName` backs SEARCH, and it is here because of a break that already shipped once.
  // `DEC-POS-0030` records it: a value-converted property translates in a PROJECTION but not in a PREDICATE,
  // so `Name.Value.Contains(text)` threw for every department search carrying a search term, and no test
  // covered it. GL would have reproduced that exactly — `REQ-GL-0008` searches the chart — so the column is
  // written now rather than after the same failure. It carries NO index: a leading-wildcard LIKE cannot seek.
  public string NormalizedCode => normalizedCode;

  public string NormalizedName => normalizedName;

  public bool IsActive { get; private set; }

  public DateTimeOffset CreatedUtc { get; set; }

  public DateTimeOffset ModifiedUtc { get; set; }

  public string? CreatedBy { get; set; }

  public string? ModifiedBy { get; set; }

  // Optimistic concurrency (`DEC-GL-0007`). Accounts are mutable, so they carry one; a posted journal does
  // not, because there is no concurrent update for it to detect.
  public byte[]? RowVersion { get; set; }

  public static Result<Account> Create(string? code, string? name)
  {
    var accountCode = AccountCode.Create(code);
    if (accountCode.IsFailure)
    {
      return Result.Failure<Account>(accountCode.Error);
    }

    var accountName = AccountName.Create(name);
    if (accountName.IsFailure)
    {
      return Result.Failure<Account>(accountName.Error);
    }

    return Result.Success(new Account(Guid.NewGuid(), accountCode.Value, accountName.Value));
  }

  public Result Rename(string? name)
  {
    var accountName = AccountName.Create(name);
    if (accountName.IsFailure)
    {
      return Result.Failure(accountName.Error);
    }

    Name = accountName.Value;
    normalizedName = accountName.Value.Value.ToUpperInvariant();
    return Result.Success();
  }

  // Idempotent on purpose: deactivating an inactive account is not an error, it is a no-op that leaves the
  // caller in the state they asked for. The same reasoning the platform applies to archive operations.
  public void Deactivate() => IsActive = false;

  public void Reactivate() => IsActive = true;

  // ---- THE POSTING GUARD, ASKED AT POST TIME AGAINST LIVE STATE (DEC-GL-0009).
  //
  // Not captured on the draft and re-used later: a draft prepared while the account was active must still be
  // refused if the account was deactivated before it posted. Every state check in this module reads live
  // state for the same reason, and the cost — a long-running client refused for a change it never saw — is
  // correct behaviour for a ledger.
  public Result EnsureCanReceiveTransactions()
  {
    return IsActive
      ? Result.Success()
      : Result.Failure(AccountErrors.Inactive(Code.Value));
  }
}
