using SSAS.Platform.API.Transport;

namespace SSAS.API.Tests.Transport;

public sealed class RowVersionCodecTests
{
  private static readonly byte[] CanonicalBytes = [1, 2, 3, 4, 5, 6, 7, 8];
  private const string CanonicalValue = "AQIDBAUGBwg=";

  [Fact]
  public void Encode_returns_canonical_padded_base64()
  {
    Assert.Equal(CanonicalValue, RowVersionCodec.Encode(CanonicalBytes));
  }

  [Fact]
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
  public void Decode_rejects_noncanonical_or_wrong_length_values(string? value)
  {
    Assert.False(RowVersionCodec.TryDecode(value, out var decoded));
    Assert.Empty(decoded);
  }

  [Fact]
  public void Encode_rejects_non_sql_server_rowversion_length()
  {
    Assert.Throws<ArgumentException>(() => RowVersionCodec.Encode([1]));
  }
}
