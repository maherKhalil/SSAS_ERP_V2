using SSAS.Platform.API.Localization;

namespace SSAS.API.Tests.Localization;

public sealed class LocalizationRowVersionCodecTests
{
  private static readonly byte[] CanonicalBytes = [1, 2, 3, 4, 5, 6, 7, 8];
  private const string CanonicalValue = "AQIDBAUGBwg=";

  [Fact]
  public void Encode_returns_canonical_padded_base64()
  {
    Assert.Equal(CanonicalValue, LocalizationRowVersionCodec.Encode(CanonicalBytes));
  }

  [Fact]
  public void Decode_accepts_canonical_eight_byte_base64()
  {
    Assert.True(LocalizationRowVersionCodec.TryDecode(CanonicalValue, out var decoded));
    Assert.Equal(CanonicalBytes, decoded);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(" ")]
  [InlineData(" AQIDBAUGBwg=")]
  [InlineData("AQIDBAUGBwg= ")]
  [InlineData("AQID BAUGBwg=")]
  [InlineData("not-base64")]
  [InlineData("AQIDBAUGBwg")]
  [InlineData("AQIDBAUGBwg_")]
  [InlineData("0102030405060708")]
  [InlineData("AQIDBAUGBw==")]
  [InlineData("AQIDBAUGBwgJ")]
  public void Decode_rejects_noncanonical_or_wrong_length_values(string? value)
  {
    Assert.False(LocalizationRowVersionCodec.TryDecode(value, out var decoded));
    Assert.Empty(decoded);
  }

  [Fact]
  public void Encode_rejects_non_sql_server_rowversion_length()
  {
    Assert.Throws<ArgumentException>(() => LocalizationRowVersionCodec.Encode([1]));
  }
}
