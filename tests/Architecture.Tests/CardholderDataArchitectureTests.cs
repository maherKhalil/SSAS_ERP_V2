using Xunit.Abstractions;
using System.Reflection;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// `ADR-029` DECISION 4: NO SSAS TYPE MAY BE **CAPABLE** OF HOLDING CARDHOLDER DATA (T-037).
// ==================================================================================================
//
// > *No entity, value object, DTO, request body, response body, log statement or diagnostic in this
// > repository may declare a field capable of holding a primary account number, a card verification
// > value, a cardholder name or an expiry date.*
//
// **Capability, not content.** A nullable `CardNumber` that is never populated still violates the rule:
// the boundary is the ABSENCE OF A PLACE TO PUT THE DATA, not the discipline of not putting it there.
// A field that exists will eventually be filled.
//
// ---- WHY IT IS REPOSITORY-WIDE, AND WHY THAT IS NOT THE SAME AS THE EXISTING ONE.
//
// `SubscriptionResidencyArchitectureTests` asserts this over **seven commercial types** and says in its
// own comment that it is not a repository-wide guard. This is the rest, and the widening is the point:
// **a guard scoped to the commercial plane would not catch a `CardNumber` on a type called
// `PaymentRecord` in `Platform.Infrastructure`** — and it is precisely a type nobody thought of as
// commercial that would carry one.
//
// ---- THE TIMING, WHICH IS NOT INCIDENTAL.
//
// `ADR-029` deferred this to *"the first package to build the payment surface"*. Nothing builds it yet —
// and T-046 has just made `REQ-SUB-0026` unconditional, so **"the product performs payment capture" is a
// settled requirement rather than a hypothetical one.** The rule is cheapest to enforce before the code
// it constrains exists, and it is worth least once that code is written against nothing.
//
// ==================================================================================================
// WHAT THIS GUARD CAN SEE, AND WHAT IT CANNOT. READ BOTH BEFORE TRUSTING IT.
// ==================================================================================================
//
// **It sees a member DECLARED FOR cardholder data** — by its own name, or by the name of its declared
// type. That is the explicit case, and it is the one a guard can decide.
//
// **It cannot see capability in general, and no reflection-based guard can.** `string Notes` is capable
// of holding a primary account number. So is `string Description`, and so is every free-text field in
// the repository. A guard flagging them would flag several thousand members and be switched off within
// a week.
//
// **`ADR-029`'s own risks section says this is where the rule erodes**, and says it plainly enough to be
// quoted rather than paraphrased:
//
// > *"The rule erodes at the edges rather than at the centre. Nobody will propose a `Pan` column. The
// > realistic failure is a free-text `Notes`, `Description` or `Metadata` field on a payment type, into
// > which a support process pastes a card number. Decision 4's capability test is the mitigation and it
// > is weakest exactly here, because such a field is capable by accident."*
//
// **So this guard covers the case the ADR says nobody will commit, and not the case the ADR says will
// actually happen.** That is worth having — the explicit case is catastrophic and silent — but a reader
// who takes a green here as "no cardholder data can enter SSAS" has read more than it says. What closes
// the free-text edge is the no-logging clause, the provider-hosted capture shape, and review of any type
// that touches the payment surface; none of those is a test.
//
// It also cannot see: a `Dictionary<string, string>` or JSON blob (capable, unnameable); a value arriving
// through a parameter and never stored on a type; or a provider callback logged verbatim, which is
// `ADR-029`'s second named edge.
public sealed class CardholderDataArchitectureTests(ITestOutputHelper output)
{
  // ---- THE PRODUCTION SURFACE, TAKEN FROM DISK RATHER THAN FROM REFERENCES.
  //
  // `GetReferencedAssemblies()` reports what the compiler kept, not what the project references — an
  // assembly referenced and never touched by test code is pruned and would silently leave the scan. The
  // build copies every referenced project's output here, so the directory is the honest inventory.
  private static IReadOnlyList<Assembly> ProductionAssemblies()
  {
    var directory = Path.GetDirectoryName(typeof(CardholderDataArchitectureTests).Assembly.Location)!;

    return
    [
      .. Directory.EnumerateFiles(directory, "SSAS.*.dll")
        .Where(path => !Path.GetFileName(path).Contains(".Tests.", StringComparison.Ordinal))
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(Assembly.LoadFrom)
    ];
  }

  private static IEnumerable<Type> TypesIn(Assembly assembly)
  {
    try
    {
      return assembly.GetTypes();
    }
    catch (ReflectionTypeLoadException failure)
    {
      // Partial results rather than none: a type that will not load must not blind the scan to the
      // ones beside it. The floor assertions below are what stop this degrading silently.
      return failure.Types.Where(type => type is not null)!;
    }
  }

  // ---- TOKENS, NOT SUBSTRINGS, AND THE REASON IS `Company`.
  //
  // `"Company".Contains("pan")` is TRUE — c-o-m-**pan**-y. A substring guard on `pan` flags every company
  // name, branch company reference and `CompanyId` in the repository, and the only way to keep it green
  // is to remove the term that catches the actual PCI primary account number.
  //
  // So identifiers are split into words on underscores, digits and camel-case boundaries, and the short
  // unambiguous terms are matched as WHOLE TOKENS.
  // ---- ACRONYMS ARE WHY THIS IS NOT A ONE-LINE SPLIT, AND `PAN` IS THE SPECIMEN.
  //
  // A naive "break before every capital" tokenizer turns `PAN` into `p`, `a`, `n` — so the guard
  // silently stops recognising the single most important term in the rule, while still passing its own
  // tests on `CardNumber`. **The red-direction cases below are what caught it**, which is the whole
  // argument for demonstrating a guard in both directions rather than watching it go green.
  //
  // So a boundary is: a capital following a lower-case letter (`cardNumber`), or a capital followed by a
  // lower-case letter at the end of a capital run (`PANDigits` -> `pan`, `digits`).
  private static string[] Tokens(string name)
  {
    var words = new List<string>();
    var current = new System.Text.StringBuilder();

    for (var index = 0; index < name.Length; index++)
    {
      var character = name[index];

      if (character is '_' or '<' or '>' or '.' or '+' or '`')
      {
        if (current.Length > 0) { words.Add(current.ToString()); current.Clear(); }
        continue;
      }

      if (char.IsUpper(character) && current.Length > 0)
      {
        var previousWasLower = char.IsLower(name[index - 1]);
        var endsAnAcronymRun = index + 1 < name.Length && char.IsLower(name[index + 1]);

        if (previousWasLower || endsAnAcronymRun)
        {
          words.Add(current.ToString());
          current.Clear();
        }
      }

      current.Append(char.ToLowerInvariant(character));
    }

    if (current.Length > 0)
    {
      words.Add(current.ToString());
    }

    return [.. words];
  }

  // Unambiguous on their own: none of these is a word that appears in this product for any other reason.
  private static readonly string[] StandaloneTokens =
    ["pan", "cvv", "cvv2", "cvc", "cvc2", "csc", "cav2", "magstripe", "track1", "track2"];

  // Ambiguous alone, decisive beside `card`. **`expiry` is the one this matters most for**: a CARD expiry
  // is cardholder data and a TOKEN expiry is not, and this repository is full of the second — refresh
  // tokens, sessions, action tokens, backup retention. A bare `expiry` term would fire on all of them and
  // teach everyone that this guard cries wolf.
  private static readonly string[] CardQualifiedTokens =
    ["number", "no", "num", "holder", "expiry", "expiration", "expires", "exp", "security", "cvv", "cvc",
     "pin", "track", "verification", "sec"];

  private static readonly string[] JoinedPhrases =
    ["cardnumber", "cardholder", "primaryaccountnumber", "cardexpiry", "cardexpiration",
     "cardsecuritycode", "cardverificationvalue", "cardverificationcode", "pinblock", "magstripe",
     "magneticstripe"];

  /// <summary>The rule that a name trips, or null when it trips none.</summary>
  private static string? CardholderCapability(string name)
  {
    var flattened = name.Replace("_", string.Empty).ToLowerInvariant();

    foreach (var phrase in JoinedPhrases)
    {
      if (flattened.Contains(phrase, StringComparison.Ordinal))
      {
        return $"joined phrase '{phrase}'";
      }
    }

    var tokens = Tokens(name);

    foreach (var token in tokens)
    {
      if (StandaloneTokens.Contains(token, StringComparer.Ordinal))
      {
        return $"standalone token '{token}'";
      }
    }

    if (tokens.Contains("card", StringComparer.Ordinal))
    {
      foreach (var token in tokens)
      {
        if (CardQualifiedTokens.Contains(token, StringComparer.Ordinal))
        {
          return $"'card' qualified by '{token}'";
        }
      }
    }

    return null;
  }

  private sealed record Offender(string Assembly, string Type, string Member, string Rule)
  {
    public override string ToString() => $"{Assembly}::{Type}.{Member} ({Rule})";
  }

  private static (List<Offender> Offenders, int Assemblies, int Types, int Members) Scan()
  {
    var offenders = new List<Offender>();
    var assemblies = ProductionAssemblies();
    var types = 0;
    var members = 0;

    foreach (var assembly in assemblies)
    {
      var assemblyName = assembly.GetName().Name ?? "?";

      foreach (var type in TypesIn(assembly))
      {
        types++;

        const BindingFlags Everything =
          BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
          BindingFlags.DeclaredOnly;

        // Properties AND fields. A record's positional parameters become properties, so they are
        // covered; private fields are covered because a type capable of holding the data privately is
        // exactly as capable as one holding it publicly.
        foreach (var property in type.GetProperties(Everything))
        {
          members++;
          var rule = CardholderCapability(property.Name)
            ?? CardholderCapability(property.PropertyType.Name);
          if (rule is not null)
          {
            offenders.Add(new Offender(assemblyName, type.FullName ?? type.Name, property.Name, rule));
          }
        }

        foreach (var field in type.GetFields(Everything))
        {
          // Auto-property backing fields duplicate a property already scanned; reporting both would
          // double every finding and make the message harder to act on.
          if (field.Name.Contains("k__BackingField", StringComparison.Ordinal))
          {
            continue;
          }

          members++;
          var rule = CardholderCapability(field.Name) ?? CardholderCapability(field.FieldType.Name);
          if (rule is not null)
          {
            offenders.Add(new Offender(assemblyName, type.FullName ?? type.Name, field.Name, rule));
          }
        }
      }
    }

    return (offenders, assemblies.Count, types, members);
  }

  // ==================================================================================================
  // THE GUARD.
  // ==================================================================================================
  [Fact]
  public void No_production_type_declares_a_member_capable_of_holding_cardholder_data()
  {
    var (offenders, _, _, _) = Scan();

    Assert.True(
      offenders.Count == 0,
      "ADR-029 decision 4: no SSAS type may DECLARE a member capable of holding a primary account " +
      "number, card verification value, cardholder name or card expiry date — capability, not content, " +
      "so a member that is never populated still violates it. " +
      $"Offenders ({offenders.Count}): {string.Join("; ", offenders.Select(o => o.ToString()))}. " +
      "If one of these is a false positive, correct the predicate and say why; if one is real, it is a " +
      "NEEDS-DECISION and not a property to delete for a green gate.");
  }

  // ==================================================================================================
  // THE TRIPWIRE — `DEC-L-016`, AND `DEC-L-043` FOR A GUARD THAT PASSES FIRST TIME.
  // ==================================================================================================
  //
  // **A capability guard that matches nothing anywhere is indistinguishable from one that works.** The
  // specific way this one could be hollow: `ProductionAssemblies()` reads a DIRECTORY, so a build layout
  // change, a renamed output folder or a filter that excluded too much would leave it scanning nothing —
  // and it would pass, permanently and silently, exactly as it does today.
  //
  // So the floors are asserted rather than assumed. They are set below the measured values with room for
  // ordinary growth; the point is not the precise number but that ZERO CANNOT PASS.
  [Fact]
  public void The_scan_actually_covers_the_production_surface()
  {
    var (_, assemblies, types, members) = Scan();

    // Emitted rather than merely asserted, for the reason `DEC-L-043` exists: a floor tells you the scan
    // was not empty, and the numbers tell you WHAT it covered. A reader auditing this guard should not
    // have to make it fail to find out.
    output.WriteLine(
      $"ADR-029 capability scan: {assemblies} production assemblies, {types} types, {members} members.");
    output.WriteLine("Assemblies: " + string.Join(", ",
      ProductionAssemblies().Select(assembly => assembly.GetName().Name)));

    Assert.True(assemblies >= 25,
      $"only {assemblies} production assemblies were scanned — the repository ships more than that, so " +
      "the guard above is passing on a surface it cannot see. Check the output-directory layout.");
    Assert.True(types >= 1000, $"only {types} types were scanned.");
    Assert.True(members >= 5000, $"only {members} members were scanned.");
  }

  // ==================================================================================================
  // BOTH DIRECTIONS — `DEC-L-031`. THE GUARD FIRES, AND IT DOES NOT FIRE ON EVERYTHING.
  // ==================================================================================================
  //
  // The offending shapes are declared HERE rather than in production code, because demonstrating a guard
  // must not require committing the thing it forbids even briefly. The predicate under test is the same
  // one the scan uses.
  [Theory]
  [InlineData("CardNumber")]
  [InlineData("cardNumber")]
  [InlineData("card_number")]
  [InlineData("Pan")]
  [InlineData("PAN")]
  [InlineData("primaryAccountNumber")]
  [InlineData("Cvv")]
  [InlineData("Cvc")]
  [InlineData("CardholderName")]
  [InlineData("CardExpiryDate")]
  [InlineData("CardExpirationMonth")]
  [InlineData("CardSecurityCode")]
  [InlineData("Track2")]
  [InlineData("PinBlock")]
  public void The_guard_fires_on_a_member_declared_for_cardholder_data(string name) =>
    Assert.NotNull(CardholderCapability(name));

  // ---- AND THE HALF THAT MATTERS MORE, BECAUSE A GUARD THAT FLAGS EVERYTHING IS TURNED OFF.
  //
  // Every one of these exists in this repository or plausibly could. **`CompanyId` is the specimen**: a
  // substring guard on `pan` flags it, because "Com-PAN-y" contains the term. `ExpiresUtc` is the second:
  // a bare `expiry` term flags every refresh token, session and action token in the product.
  [Theory]
  [InlineData("CompanyId")]
  [InlineData("CompanyName")]
  [InlineData("NormalizedCompanyCode")]
  [InlineData("ExpiresUtc")]
  [InlineData("RefreshTokenExpiresUtc")]
  [InlineData("TokenExpiry")]
  [InlineData("TermEndUtc")]
  [InlineData("ExpandedScope")]
  [InlineData("PanelLayout")]
  [InlineData("BillingCurrencyCode")]
  [InlineData("AccountNumber")]
  [InlineData("SecurityVersion")]
  [InlineData("ProviderReference")]
  public void The_guard_does_not_fire_on_an_innocent_member(string name) =>
    Assert.Null(CardholderCapability(name));

  // ---- THE TYPE-NAME HALF, WHICH THE MEMBER-NAME HALF WOULD MISS ENTIRELY.
  //
  // `public CardNumber Value { get; }` carries an innocent member name and a type that is not. A guard
  // reading only member names passes it, and a value object is exactly how a careful author would model
  // a card number.
  [Fact]
  public void The_guard_reads_the_declared_type_as_well_as_the_member_name()
  {
    Assert.Null(CardholderCapability("Value"));
    Assert.NotNull(CardholderCapability(nameof(CardNumber)));
    Assert.NotNull(CardholderCapability(typeof(CardNumber).Name));
  }

  // Declared in the test assembly, which the scan excludes by design — the guard covers production, and a
  // fixture proving it fires must not become a finding against the repository it protects.
  private sealed record CardNumber(string Digits);
}
