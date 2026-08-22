using System.Text;
using Microsoft.AspNetCore.Http;
using SSAS.BuildingBlocks.Api.Transport;

namespace SSAS.API.Tests.Transport;

// THE CSV BODY READER (FP-009, R1, DEC-DOC-0008).
//
// Its sibling `StrictRequestReaderTests` proves the JSON reader refuses rather than guesses. These prove the
// same register for CSV, and one thing that reader has no equivalent of: an ENCODING refusal, because JSON
// arrives as text the framework already decoded while a CSV body arrives as bytes this reader decodes itself.
public sealed class StrictCsvReaderTests
{
  // ================================================================================================
  // THE CONTENT-TYPE GATE IS THE FIRST LINE, AND IT IS THE CONTRACT
  // ================================================================================================
  //
  // `StrictRequestReader.ReadStrictJsonAsync` opens with `HasJsonContentType()`; this opens with its own
  // gate on `text/csv`. Neither is a precondition to be relaxed later — everything either method promises is
  // only true of a body it recognised, so teaching one a second content type would make its guarantees
  // conditional. That is why this is a SIBLING and not a widening.
  [Theory]
  [InlineData("text/csv")]
  [InlineData("text/csv; charset=utf-8")]
  [InlineData("text/csv; charset=UTF-8")]
  [InlineData("TEXT/CSV")]
  public async Task A_declared_csv_body_is_read(string contentType)
  {
    var context = ContextWith(contentType, "employeeNumber\nE-1");

    Assert.Equal("employeeNumber\nE-1", await StrictCsvReader.ReadStrictCsvAsync(context, default));
  }

  [Theory]
  [InlineData("application/json")]
  [InlineData("text/plain")]
  [InlineData("multipart/form-data; boundary=x")]
  [InlineData("application/vnd.ms-excel")]
  [InlineData("")]
  public async Task Anything_that_is_not_csv_is_refused_rather_than_guessed(string contentType)
  {
    var context = ContextWith(contentType, "employeeNumber\nE-1");

    Assert.Null(await StrictCsvReader.ReadStrictCsvAsync(context, default));
  }

  // ---- MULTIPART IS REFUSED LIKE ANY OTHER UNRECOGNISED TYPE, and that is the ruling made visible.
  //
  // A CSV file IS a body. Multipart is browser-form machinery serving no API-first need, and it drags in
  // form-parsing limits and a test-harness apparatus for nothing. This assertion is the one that would go
  // red if somebody re-added it as a convenience.
  [Fact]
  public async Task A_body_with_no_content_type_at_all_is_refused()
  {
    var context = new DefaultHttpContext();
    context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("employeeNumber\nE-1"));

    Assert.Null(await StrictCsvReader.ReadStrictCsvAsync(context, default));
  }

  // ---- A DIFFERENT DECLARED CHARSET IS A REFUSAL, NOT A SILENT UTF-8 DECODE.
  //
  // `DEC-DOC-0008` says UTF-8. A caller who declares `windows-1256` and is decoded as UTF-8 anyway would get
  // employees whose Arabic names are mojibake — a success that produced wrong data, which is worse than a
  // refusal because nobody investigates it.
  [Theory]
  [InlineData("text/csv; charset=windows-1256")]
  [InlineData("text/csv; charset=iso-8859-1")]
  [InlineData("text/csv; charset=utf-16")]
  public async Task A_body_declaring_another_charset_is_refused(string contentType)
  {
    Assert.Null(await StrictCsvReader.ReadStrictCsvAsync(
      ContextWith(contentType, "employeeNumber\nE-1"), default));
  }

  // ---- BYTES THAT ARE NOT VALID UTF-8 ARE REFUSED, NOT SUBSTITUTED.
  //
  // The permissive default replaces every undecodable byte with U+FFFD, so a file in the wrong encoding
  // imports as employees whose names contain replacement characters. `throwOnInvalidBytes` is what turns
  // that silent corruption into this refusal, and this test is the only thing that proves the flag is set.
  [Fact]
  public async Task A_body_that_is_not_valid_utf8_is_refused_rather_than_substituted()
  {
    var context = new DefaultHttpContext();
    context.Request.ContentType = "text/csv";

    // 0xC3 starts a two-byte sequence; 0x28 cannot continue it.
    context.Request.Body = new MemoryStream([0xC3, 0x28, 0x41]);

    Assert.Null(await StrictCsvReader.ReadStrictCsvAsync(context, default));
  }

  // ---- A UTF-8 BOM IS STRIPPED, WHICH IS WHAT MAKES THE ROUND TRIP REAL.
  //
  // `DEC-DOC-0008` has exports emit a BOM because Excel opens UTF-8 without one as mojibake, and requires
  // that an exported file re-imports. A reader that refused — or worse, kept — the byte order mark would
  // break that property on the very files the product tells operators to edit and resubmit.
  [Fact]
  public async Task A_utf8_byte_order_mark_is_stripped_rather_than_read_as_content()
  {
    var context = new DefaultHttpContext();
    context.Request.ContentType = "text/csv";
    context.Request.Body = new MemoryStream(
      [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("employeeNumber\nE-1")]);

    var content = await StrictCsvReader.ReadStrictCsvAsync(context, default);

    Assert.Equal("employeeNumber\nE-1", content);
    Assert.DoesNotContain('﻿', content!);
  }

  // ---- NON-ASCII CONTENT SURVIVES INTACT, so the UTF-8 claim is a distinction rather than a default.
  [Fact]
  public async Task Non_ascii_content_is_decoded_verbatim()
  {
    const string content = "fullName\nليلى حداد\nJosé Álvarez";

    Assert.Equal(content, await StrictCsvReader.ReadStrictCsvAsync(
      ContextWith("text/csv", content), default));
  }

  // An empty body is not an encoding failure. It is an empty file, which the handler refuses for having no
  // header — a different refusal, reported with a different reason, and one that writes a run record.
  [Fact]
  public async Task An_empty_csv_body_reads_as_empty_rather_than_refused()
  {
    Assert.Equal(string.Empty, await StrictCsvReader.ReadStrictCsvAsync(
      ContextWith("text/csv", string.Empty), default));
  }

  private static DefaultHttpContext ContextWith(string contentType, string content)
  {
    var context = new DefaultHttpContext();

    if (!string.IsNullOrEmpty(contentType))
    {
      context.Request.ContentType = contentType;
    }

    context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(content));

    return context;
  }
}
