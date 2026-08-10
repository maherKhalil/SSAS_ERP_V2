using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.ValueObjects;

public sealed class BaseCurrencyCode : ValueObject
{
  public const int RequiredLength = 3;

  // Static, deterministic ISO-4217 active alphabetic currency code set. Platform owns the base/default
  // currency configuration value only; no exchange-rate or accounting behaviour lives here.
  private static readonly HashSet<string> Iso4217AlphabeticCodes = new(StringComparer.Ordinal)
  {
    "AED", "AFN", "ALL", "AMD", "ANG", "AOA", "ARS", "AUD", "AWG", "AZN",
    "BAM", "BBD", "BDT", "BGN", "BHD", "BIF", "BMD", "BND", "BOB", "BOV", "BRL", "BSD", "BTN", "BWP", "BYN", "BZD",
    "CAD", "CDF", "CHE", "CHF", "CHW", "CLF", "CLP", "CNY", "COP", "COU", "CRC", "CUC", "CUP", "CVE", "CZK",
    "DJF", "DKK", "DOP", "DZD",
    "EGP", "ERN", "ETB", "EUR",
    "FJD", "FKP",
    "GBP", "GEL", "GHS", "GIP", "GMD", "GNF", "GTQ", "GYD",
    "HKD", "HNL", "HRK", "HTG", "HUF",
    "IDR", "ILS", "INR", "IQD", "IRR", "ISK",
    "JMD", "JOD", "JPY",
    "KES", "KGS", "KHR", "KMF", "KPW", "KRW", "KWD", "KYD", "KZT",
    "LAK", "LBP", "LKR", "LRD", "LSL", "LYD",
    "MAD", "MDL", "MGA", "MKD", "MMK", "MNT", "MOP", "MRU", "MUR", "MVR", "MWK", "MXN", "MXV", "MYR", "MZN",
    "NAD", "NGN", "NIO", "NOK", "NPR", "NZD",
    "OMR",
    "PAB", "PEN", "PGK", "PHP", "PKR", "PLN", "PYG",
    "QAR",
    "RON", "RSD", "RUB", "RWF",
    "SAR", "SBD", "SCR", "SDG", "SEK", "SGD", "SHP", "SLE", "SLL", "SOS", "SRD", "SSP", "STN", "SVC", "SYP", "SZL",
    "THB", "TJS", "TMT", "TND", "TOP", "TRY", "TTD", "TWD", "TZS",
    "UAH", "UGX", "USD", "USN", "UYI", "UYU", "UYW", "UZS",
    "VED", "VES", "VND", "VUV",
    "WST",
    "XAF", "XCD", "XOF", "XPF", "XSU", "XUA",
    "YER",
    "ZAR", "ZMW", "ZWL"
  };

  private BaseCurrencyCode(string value)
  {
    Value = value;
  }

  public string Value { get; }

  public static Result<BaseCurrencyCode> Create(string? value)
  {
    var trimmed = value?.Trim();
    if (string.IsNullOrEmpty(trimmed) || trimmed.Length != RequiredLength)
    {
      return Result.Failure<BaseCurrencyCode>(CompanyErrors.InvalidBaseCurrency);
    }

    var canonical = trimmed.ToUpperInvariant();
    if (!IsAsciiLetters(canonical) || !Iso4217AlphabeticCodes.Contains(canonical))
    {
      return Result.Failure<BaseCurrencyCode>(CompanyErrors.InvalidBaseCurrency);
    }

    return Result.Success(new BaseCurrencyCode(canonical));
  }

  public override string ToString() => Value;

  protected override IEnumerable<object?> GetEqualityComponents()
  {
    yield return Value;
  }

  private static bool IsAsciiLetters(string value)
  {
    foreach (var character in value)
    {
      if (character is < 'A' or > 'Z')
      {
        return false;
      }
    }

    return true;
  }
}
