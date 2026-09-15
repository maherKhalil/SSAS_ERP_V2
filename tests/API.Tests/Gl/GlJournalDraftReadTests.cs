using System.Net;
using SSAS.GL.Application.Permissions;
using SSAS.GL.Application.Reads;

namespace SSAS.API.Tests.Gl;

// ==================================================================================================
// READING A DRAFT (T-098) — AND THE TWO REFUSALS ARE THE SEPARATION OF DUTIES MADE MECHANICAL.
// ==================================================================================================
//
// FP-011 shipped four draft write routes — create, update, discard, post — **and nothing that could read
// one.** The create route returns a `Location` header and an id for a resource no route could fetch, so a
// preparer could not see what they were editing and a poster could not see what they were about to post.
//
// ---- THE TWO NEGATIVE TESTS CARRY THE RULING.
//
// `GL.Drafts.Manage` alone must not read, and `GL.Journals.Post` alone must not read. **Those two are the
// whole of Reading A**: nothing implies `GL.Drafts.View`, so both roles are granted it explicitly.
//
// An implied permission makes the explicit one optional and its absence unenforceable — `AC-SS-0005`, and
// the third time this codebase has refused the shape (T-088 for payslips, T-089 for attendance). **Naming
// a draft by id is not authority to read it**, which is exactly the payslip question answered no.
//
// **Without these two, a later "the poster obviously needs to see it" would go green**, and the separation
// the permission was declared for would be gone with nothing red.
[Collection(GlApiEndpointGroup.Name)]
public sealed class GlJournalDraftReadTests : IClassFixture<GlApiTestHost>
{
  private const string ListRoute = "/api/gl/journal-drafts";

  private static readonly Guid DraftId = Guid.Parse("77777777-7777-7777-7777-777777777777");

  private readonly GlApiTestHost host;

  public GlJournalDraftReadTests(GlApiTestHost host)
  {
    this.host = host;
    host.ResetToAuthorizedState();
  }

  // ---- THE CONTROL. Without it, both refusals below would also pass against a route that refuses
  // ---- everyone, and a route nobody can reach is not a route.
  [Fact]
  [Trait("Permission", "GL.Drafts.View")]
  public async Task The_view_permission_alone_reads_the_draft_list()
  {
    host.Reads.Drafts.Add(NewListItem());

    var response = await Get(ListRoute, GlPermissionNames.ViewDrafts);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    // The read was reached with a RESOLVED scope, not merely permitted at the door. A route that answered
    // 200 without consulting the read service would pass the status assertion alone.
    var scope = Assert.Single(host.Reads.ObservedScopes);
    Assert.Equal(GlApiTestHost.TenantId, scope.TenantId);
  }

  [Fact]
  [Trait("Permission", "GL.Drafts.View")]
  public async Task The_view_permission_alone_reads_one_draft_with_its_lines()
  {
    host.Reads.Draft = NewDetail();

    var response = await Get($"{ListRoute}/{DraftId}", GlPermissionNames.ViewDrafts);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var document = await GlApiTestHost.DocumentAsync(response);
    var root = document.RootElement;

    Assert.Equal(DraftId, root.GetProperty("journalDraftId").GetGuid());
    Assert.Equal(2, root.GetProperty("lines").GetArrayLength());

    // ---- THE SHAPE IS THE RULING TOO.
    //
    // A draft has NO journal number — one is assigned at posting — and reversal is a posted-journal
    // concept. Asserting their ABSENCE is what stops someone adding them back as nulls, which would invite
    // a client to render an empty number as though the draft had one.
    Assert.False(root.TryGetProperty("journalNumber", out _));
    Assert.False(root.TryGetProperty("isReversed", out _));
    Assert.False(root.TryGetProperty("reversesJournalEntryId", out _));
  }

  // ================================================================================================
  // READING A. NOTHING IMPLIES `GL.Drafts.View`.
  // ================================================================================================
  //
  // The preparer can create, update and discard a draft and **cannot read one** without the view grant.
  // That is not friction: the point of a separate `View` is that it can be held WITHOUT `Manage`, which is
  // what makes "prepare for someone else to post" expressible at all.
  [Fact]
  [Trait("Permission", "GL.Drafts.View")]
  public async Task The_manage_permission_alone_does_not_read_a_draft()
  {
    host.Reads.Drafts.Add(NewListItem());

    var response = await Get(ListRoute, GlPermissionNames.ManageDrafts);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

    // Refused at the door, so the read service was never reached. Without this a handler that ran and
    // returned an empty list would be indistinguishable from a refusal by status alone.
    Assert.Empty(host.Reads.ObservedScopes);
  }

  // ---- AND THE POSTER CANNOT EITHER, THOUGH THEY NAME THE DRAFT BY ID ON THE POSTING ROUTE.
  //
  // `POST /journal-drafts/{id}/posting` is gated on `GL.Journals.Post`, so a poster already names a draft.
  // **Naming a resource is not authority to read it** — T-088 answered exactly this for payslips.
  [Fact]
  [Trait("Permission", "GL.Drafts.View")]
  public async Task The_posting_permission_alone_does_not_read_a_draft()
  {
    host.Reads.Draft = NewDetail();

    var response = await Get($"{ListRoute}/{DraftId}", GlPermissionNames.PostJournals);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Empty(host.Reads.ObservedScopes);
  }

  // ---- AN UNKNOWN DRAFT IS 404, NOT AN EMPTY BODY.
  [Fact]
  public async Task An_unknown_draft_is_not_found()
  {
    var response = await Get($"{ListRoute}/{Guid.NewGuid()}", GlPermissionNames.ViewDrafts);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("gl.not_found", await GlApiTestHost.ProblemCodeAsync(response));
  }

  // ---- NO AUTHORIZED COMPANY IS A REFUSAL, NOT AN EMPTY PAGE.
  //
  // `AC-GL-0014`, asserted here as well as on journals because the scope resolution is per route and a new
  // route is a new place to forget it.
  [Fact]
  [Trait("Decision", "AC-GL-0014")]
  public async Task A_caller_with_no_authorized_company_is_refused_rather_than_served_an_empty_page()
  {
    host.CompanyAccess.Permitted = [];

    var response = await Get(ListRoute, GlPermissionNames.ViewDrafts);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal("company.scope_denied", await GlApiTestHost.ProblemCodeAsync(response));
  }

  private Task<HttpResponseMessage> Get(string path, params string[] permissions) =>
    host.Client.SendAsync(GlApiTestHost.Request(HttpMethod.Get, path, host.TokenWith(permissions)));

  private static JournalDraftListItem NewListItem() => new(
    DraftId, GlApiTestHost.CompanyA, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
    "Accrual", "REF-1", 1000m);

  private static JournalDraftDetail NewDetail() => new(
    DraftId, GlApiTestHost.CompanyA, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
    "Accrual", "REF-1",
    [
      new JournalLineDetail(1, Guid.NewGuid(), "1000", "Cash", 1000m, 0m, null),
      new JournalLineDetail(2, Guid.NewGuid(), "2000", "Accruals", 0m, 1000m, null)
    ]);
}
