using SSAS.BuildingBlocks.Api.Transport;
using SSAS.Platform.API.Transport;

namespace SSAS.API.Tests.Transport;

// ⚠ THIS FILE CARRIES THE BEHAVIOURAL BODY OF `AC-LOC-0061` — *"Localization HTTP ACCEPTS ONLY canonical
// padded RFC 4648 Base64 rowversions of exact SQL-rowversion length, EMITS canonical Base64, REJECTS
// Base64Url/hex/blank/whitespace/malformed/wrong-length/noncanonical values…"* — even though nothing in it
// mentions localization.
//
// ⚠⚠ SO SAY WHAT JOINS THEM, BECAUSE `RowVersionCodec` IS A SHARED BUILDING BLOCK AND THE CRITERION IS
// SCOPED TO ONE MODULE. `LocalizationEndpointRouteBuilderExtensions` calls `RowVersionCodec.TryDecode` at
// its PUT, Undo and Restore-Default handlers and `RowVersionCodec.Encode` on the way out, and
// `LocalizationAuditReadinessApiTests.Malformed_transport_rowversion_is_400_before_concurrency_or_audit_
// processing` OBSERVES the join end to end: a real localization route, a Base64Url value, HTTP 400 and
// `localization.rowversion_invalid`. **Without that observed join this would be an ADJACENT-SCOPE
// citation — a shared codec answering for one module's sentence.**
//
// ⚠⚠⚠ AND THE REJECTION THEORY IS CITED BECAUSE ITS POPULATION MATCHES THE CRITERION'S LIST TERM FOR TERM,
// which is the only thing that makes a hand-enumerated `[InlineData]` set a claim about the sentence rather
// than about twelve strings someone happened to think of:
//
//   Base64Url     → `AQIDBAUGBwg_`        noncanonical  → `AQIDBAUGBwg` (missing padding)
//   hex           → `0102030405060708`    wrong-length  → `AQIDBAUGBw==` (6 bytes), `AQIDBAUGBwgJ` (9)
//   blank         → `null`, `""`          malformed     → `not-base64`
//   whitespace    → `" "`, leading, trailing, embedded
//
// ⚠ The remaining clauses of 0061 are elsewhere and NOT here: the 400 and the exact code are
// `LocalizationTransportContractTests.Error_mapper_exposes_invalid_rowversion_contract`, and *maps ONLY
// valid stale values to 409* is `Error_mapper_maps_internal_concurrency_to_http_contract` plus the *before
// concurrency processing* half of the end-to-end test above. **This file proves what is refused, never what
// the refusal turns into.**
public sealed class RowVersionCodecTests
{
  private static readonly byte[] CanonicalBytes = [1, 2, 3, 4, 5, 6, 7, 8];
  private const string CanonicalValue = "AQIDBAUGBwg=";

  [Fact]
  [Trait("Criterion", "AC-LOC-0061")]
  public void Encode_returns_canonical_padded_base64()
  {
    Assert.Equal(CanonicalValue, RowVersionCodec.Encode(CanonicalBytes));
  }

  [Fact]
  [Trait("Criterion", "AC-LOC-0061")]
  public void Decode_accepts_canonical_eight_byte_base64()
  {
    Assert.True(RowVersionCodec.TryDecode(CanonicalValue, out var decoded));
    Assert.Equal(CanonicalBytes, decoded);
  }

  [Theory]
  [InlineData(null)]              // null
  [InlineData("")]               // empty
  [InlineData(" ")]              // whitespace only
  [InlineData(" AQIDBAUGBwg=")]  // leading whitespace
  [InlineData("AQIDBAUGBwg= ")]  // trailing whitespace
  [InlineData("AQID BAUGBwg=")]  // embedded whitespace
  [InlineData("not-base64")]     // malformed base64
  [InlineData("AQIDBAUGBwg")]    // missing padding (non-canonical)
  [InlineData("AQIDBAUGBwg_")]   // Base64Url alphabet
  [InlineData("0102030405060708")] // hexadecimal
  [InlineData("AQIDBAUGBw==")]   // decodes to 6 bytes
  [InlineData("AQIDBAUGBwgJ")]   // decodes to 9 bytes
  [Trait("Criterion", "AC-LOC-0061")]
  public void Decode_rejects_noncanonical_or_wrong_length_values(string? value)
  {
    Assert.False(RowVersionCodec.TryDecode(value, out var decoded));
    Assert.Empty(decoded);
  }

  [Fact]
  [Trait("Criterion", "AC-LOC-0061")]
  public void Encode_rejects_non_sql_server_rowversion_length()
  {
    Assert.Throws<ArgumentException>(() => RowVersionCodec.Encode([1]));
  }
}
