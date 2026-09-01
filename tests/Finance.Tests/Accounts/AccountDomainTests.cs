using SSAS.BuildingBlocks.Domain;
using SSAS.GL.Domain.Accounts;

namespace SSAS.Finance.Tests.Accounts;

// THE CHART OF ACCOUNTS (REQ-GL-0005..0007, BR-GL-0004, OD-GL-0003).
public sealed class AccountDomainTests
{
  [Fact]
  [Trait("Decision", "OD-GL-0003")]
  public void An_account_is_tenant_owned_and_deliberately_not_company_owned()
  {
    // ---- THIS IS THE RULING, ASSERTED AS AN INTERFACE RATHER THAN A COLUMN.
    //
    // ICompanyOwnedEntity is not decoration: it makes every save a company-scoped write running
    // AuthorizeCurrentCompanyAsync at the write boundary. A future convention that added it would silently
    // change what account maintenance requires, and this assertion is what makes that loud.
    Assert.Contains(typeof(ITenantOwnedEntity), typeof(Account).GetInterfaces());
    Assert.DoesNotContain(typeof(ICompanyOwnedEntity), typeof(Account).GetInterfaces());

    // And no CompanyId property at all — the absence is structural, not merely unmapped.
    Assert.Null(typeof(Account).GetProperty(nameof(SSAS.BuildingBlocks.Domain.ICompanyOwnedEntity.CompanyId)));
  }

  [Fact]
  public void A_new_account_is_active()
  {
    var account = Account.Create("4100", "Trade Receivables");

    Assert.True(account.IsSuccess);
    Assert.True(account.Value.IsActive);
    Assert.Equal("4100", account.Value.Code.Value);
    Assert.Equal("Trade Receivables", account.Value.Name.Value);
  }

  [Fact]
  [Trait("Decision", "REQ-GL-0006")]
  public void An_account_exposes_no_way_to_change_its_code()
  {
    // The code is immutable from creation, and the strict reading is enforced by ABSENCE: there is no
    // method, so no path — present or future — can reach it. The looser "immutable once used" rule would
    // have required the aggregate to know whether any journal line references it, which is a cross-aggregate
    // query at write time and the derived-state-that-drifts shape this codebase refuses.
    // Narrowed to methods `Account` DECLARES, and to authored ones. Both filters are load-bearing:
    // property accessors (`get_Code`) are generated rather than authored, and inherited members include
    // `object.GetHashCode` — which contains "Code" and has nothing to do with account codes. A substring
    // match without them reports a passing design as a failure.
    var mutators = typeof(Account).GetMethods()
      .Where(method => method.DeclaringType == typeof(Account))
      .Where(method => !method.IsSpecialName)
      .Select(method => method.Name)
      .Where(name => name.Contains("Code", StringComparison.Ordinal))
      .ToArray();

    Assert.Empty(mutators);

    // `CanWrite` is deliberately NOT the assertion: the property carries a PRIVATE setter so EF can
    // materialize it, and `CanWrite` is true for that. What must not exist is a setter a CALLER can reach.
    Assert.Null(typeof(Account).GetProperty(nameof(Account.Code))!.GetSetMethod(nonPublic: false));
  }

  [Fact]
  [Trait("Decision", "BR-GL-0004")]
  public void An_inactive_account_refuses_transactions_and_the_refusal_names_it()
  {
    var account = Account.Create("5200", "Office Supplies").Value;
    account.Deactivate();

    var receivable = account.EnsureCanReceiveTransactions();

    Assert.True(receivable.IsFailure);
    Assert.Equal("Gl.AccountInactive", receivable.Error.Code);

    // Naming the account is the difference between a user fixing something and a user filing a ticket —
    // lifecycle-model.md states the standard and this is where it is kept.
    Assert.Contains("5200", receivable.Error.Message, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "BR-GL-0004")]
  public void Deactivation_is_reversible_and_neither_direction_is_an_error_when_repeated()
  {
    var account = Account.Create("5200", "Office Supplies").Value;

    account.Deactivate();
    account.Deactivate();
    Assert.False(account.IsActive);

    account.Reactivate();
    account.Reactivate();
    Assert.True(account.IsActive);
    Assert.True(account.EnsureCanReceiveTransactions().IsSuccess);
  }

  [Fact]
  public void Renaming_keeps_the_search_shadow_in_step_with_the_display_name()
  {
    // The shadow exists because DEC-POS-0030 records that a value-converted property translates in a
    // PROJECTION but not in a PREDICATE. A rename that updated only the display value would leave the
    // account findable under its OLD name and invisible under its new one — a defect that no test of the
    // rename's return value would catch.
    var account = Account.Create("6000", "Old Name").Value;
    Assert.Equal("OLD NAME", account.NormalizedName);

    Assert.True(account.Rename("New Name").IsSuccess);

    Assert.Equal("New Name", account.Name.Value);
    Assert.Equal("NEW NAME", account.NormalizedName);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void A_code_is_required(string? code)
  {
    var account = Account.Create(code, "Name");

    Assert.True(account.IsFailure);
    Assert.Equal("Gl.AccountCodeInvalid", account.Error.Code);
  }

  [Fact]
  public void A_code_longer_than_the_column_is_refused()
  {
    var account = Account.Create(new string('9', AccountCode.MaximumLength + 1), "Name");

    Assert.True(account.IsFailure);
    Assert.Equal("Gl.AccountCodeInvalid", account.Error.Code);
  }

  [Fact]
  public void A_control_character_in_a_code_is_refused()
  {
    var account = Account.Create("41\t00", "Name");

    Assert.True(account.IsFailure);
    Assert.Equal("Gl.AccountCodeInvalid", account.Error.Code);
  }

  [Fact]
  public void Codes_are_compared_ordinally_after_upper_casing()
  {
    // Two codes differing only in case are the SAME code, and the binary-collated index is what makes the
    // database agree. Equality here is what the index enforces there.
    var lower = AccountCode.Create("ab-100").Value;
    var upper = AccountCode.Create("AB-100").Value;

    Assert.Equal(lower, upper);
    Assert.Equal("AB-100", lower.NormalizedValue);

    // Display casing is PRESERVED — normalization decides identity, not presentation.
    Assert.Equal("ab-100", lower.Value);
  }

  [Fact]
  public void Codes_that_differ_only_in_unicode_composition_are_different_codes()
  {
    // No NFC/NFD normalization is applied, deliberately: two visually identical values that differ in
    // composition are different codes, which is what makes the binary-collated index authoritative rather
    // than merely fast.
    var composed = AccountCode.Create("CAFÉ").Value;      // É as one code point
    var decomposed = AccountCode.Create("CAFÉ").Value;   // E + combining acute

    Assert.NotEqual(composed, decomposed);
  }
}
