using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace SSAS.BuildingBlocks.Api.Transport;

// STRICT CSV BODY READING, THE SIBLING OF `StrictRequestReader` (FP-009, DEC-DOC-0008).
//
// ================================================================================================
// A SIBLING, NOT A WIDENING OF THE JSON READER.
// ================================================================================================
//
// `StrictRequestReader.ReadStrictJsonAsync` opens with `HasJsonContentType()`, and that line is its
// contract rather than a precondition: everything the method promises about strict binding is only true of
// a JSON body it recognised. Teaching it a second content type would make that first line a branch and its
// guarantees conditional. This type opens with its own gate on `text/csv`, in the same register — an
// unrecognised content type is a REFUSAL, never a guess about what the caller meant.
//
// ---- WHY THE IMPORT TAKES A RAW BODY AND NOT `multipart/form-data`.
//
// A CSV file IS a body. Multipart is browser-form machinery serving no API-first need, and it drags in
// form-parsing limits and a test-harness apparatus for nothing. What multipart would have bought — a strict
// declared field set with an unrecognised field refused — the module already has for query parameters, so
// the strictness survives the change of transport.
//
// ---- WHAT THIS DOES *NOT* DO, DELIBERATELY.
//
// It does not know a column contract, does not count rows and does not enforce a size cap. Those belong to
// the handler, because the handler is what writes the import run record: a bad header and an exceeded cap
// are REFUSED runs (`DEC-DOC-0006`) that consume the import key, and a refusal that never reached the
// handler could not have recorded one. Splitting the file's structure away from its transport is what lets
// every outcome — including the ones that reject before the first data row — leave the same audit trail.
//
// The transport floor for size is `IHttpMaxRequestBodySizeFeature` on the import route, which stops the
// bytes; the handler re-checks and produces the honest `400` naming the limit and the actual size.
public static class StrictCsvReader
{
  public const string CsvContentType = "text/csv";

  // ---- UTF-8, AND NOTHING ELSE (DEC-DOC-0008).
  //
  // `throwOnInvalidBytes` is the whole point: the permissive default silently substitutes U+FFFD for every
  // byte it cannot decode, so a file in the wrong encoding would import as employees whose names contain
  // replacement characters. That is worse than a refusal, because it succeeds.
  private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

  // Returns the decoded body, or NULL — which the caller maps to `400 request.invalid`, exactly as a null
  // from `ReadStrictJsonAsync` is mapped. Null means one of: not `text/csv`, a charset other than UTF-8, or
  // bytes that are not valid UTF-8.
  public static async Task<string?> ReadStrictCsvAsync(
    HttpContext context, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(context);

    if (!HasCsvContentType(context.Request))
    {
      return null;
    }

    try
    {
      using var buffer = new MemoryStream();
      await context.Request.Body.CopyToAsync(buffer, cancellationToken);

      var bytes = buffer.ToArray();

      // A UTF-8 BOM is stripped rather than refused. `DEC-DOC-0008` has exports emit one — Excel opens
      // UTF-8 without a BOM as mojibake — and the round-trip property requires that an exported file
      // re-imports, so the byte order mark an export writes must be one an import accepts.
      var start = HasUtf8ByteOrderMark(bytes) ? 3 : 0;

      return StrictUtf8.GetString(bytes, start, bytes.Length - start);
    }
    catch (DecoderFallbackException)
    {
      return null;
    }
    catch (BadHttpRequestException)
    {
      // The transport floor rejecting an oversized body arrives here. It is a refusal like any other; the
      // handler's honest "10 MB, yours was N" message is for bodies that get past the floor.
      return null;
    }
  }

  // Media type only, parameters ignored except `charset` — `text/csv; charset=utf-8` is the same contract as
  // `text/csv`, and a caller declaring a DIFFERENT charset is refused rather than silently decoded as UTF-8.
  public static bool HasCsvContentType(HttpRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var parsed))
    {
      return false;
    }

    if (!parsed.MatchesMediaType(CsvContentType))
    {
      return false;
    }

    var charset = parsed.Charset;

    return !charset.HasValue ||
      charset.Value.Equals("utf-8", StringComparison.OrdinalIgnoreCase) ||
      charset.Value.Equals("utf8", StringComparison.OrdinalIgnoreCase);
  }

  private static bool HasUtf8ByteOrderMark(byte[] bytes) =>
    bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
}
