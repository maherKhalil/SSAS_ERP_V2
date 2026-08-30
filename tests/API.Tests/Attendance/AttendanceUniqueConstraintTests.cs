using System.Net;
using SSAS.Attendance.Application.Permissions;
using SSAS.Platform.Domain;

namespace SSAS.API.Tests.Attendance;

// ==================================================================================================
// A LOST UNIQUENESS RACE IS A CONFLICT, NOT A SERVER FAULT (T-244).
// ==================================================================================================
//
// ---- WHAT THIS RETURNED BEFORE, MEASURED RATHER THAN INFERRED.
//
// `POST /calendars/{id}/holidays` with the save refused on a unique index answered **500
// `request.failed`**, with `resourceKey` `attendance.errors.request_rejected`. That was not a theoretical
// path: `AttendanceConfigurations` puts a unique index on `(WorkingCalendarId, HolidayDate)` and its own
// comment says *"the index is what makes a race lose rather than duplicate"*. **Two clients adding the same
// holiday — the loser got a 500.**
//
// ---- WHY THE STATUS IS THE WHOLE POINT AND NOT A COSMETIC PREFERENCE.
//
// A 500 tells a caller *the server broke, report a bug*. A 409 tells them *you lost a race, look at your
// input*. **Different conditions, different client actions.** It also poisons operational signal in the
// direction that costs most: a 500 is alarm-worthy and a conflict is not, so this class inflated exactly
// the metric an operator would page on.
//
// ---- ⚠ THE ASSERTION IS ON THE CODE, NOT ONLY THE STATUS.
//
// Asserting 409 alone would pass if someone mapped this onto the module's generic `attendance.conflict`,
// which is the outcome to avoid: the generic arm is the floor for paths NOBODY CLASSIFIED, and it needs its
// own code so an operator can see how often an unclassified one fires. A distinct code is what turns this
// from a silent fallback into a signal that some handler still needs a translation naming its constraint.
public sealed class AttendanceUniqueConstraintTests(AttendanceApiTestHost host)
  : IClassFixture<AttendanceApiTestHost>
{
  [Fact]
  public async Task A_unique_constraint_refusal_is_a_conflict_rather_than_a_server_fault()
  {
    host.ResetToAuthorizedState();
    host.UnitOfWork.Failure = IdentityAccessErrors.UniqueConstraintViolation;

    var response = await AddHoliday("2026-05-01");
    var body = await response.Content.ReadAsStringAsync();

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Contains("attendance.unique_conflict", body, StringComparison.Ordinal);
  }

  // The control, and it is what stops the assertion above from being satisfied by a route that refuses
  // everything: with no persistence failure the same request must not answer 409.
  [Fact]
  public async Task The_same_request_without_a_persistence_failure_is_not_a_conflict()
  {
    host.ResetToAuthorizedState();
    host.UnitOfWork.Failure = null;

    var response = await AddHoliday("2026-07-23");

    Assert.NotEqual(HttpStatusCode.Conflict, response.StatusCode);
  }

  // ⚠ A DISTINCT DATE PER TEST, AND THIS IS NOT TIDINESS. The stub calendar is shared across the class, so
  // a holiday added by one test is still there for the next — and the DOMAIN refuses a duplicate date
  // before the save is ever attempted. Sharing a date made this class assert the domain's duplicate rule
  // while believing it was asserting the persistence one, and it answered 409 for the wrong reason.
  private async Task<HttpResponseMessage> AddHoliday(string holidayDate)
  {
    var request = AttendanceApiTestHost.Request(
      HttpMethod.Post,
      $"/api/attendance/calendars/{Guid.NewGuid()}/holidays",
      host.TokenWith(AttendancePermissionNames.ManageCalendars));
    request.Content = new StringContent(
      $$"""{"holidayDate":"{{holidayDate}}","name":"Public Holiday"}""",
      System.Text.Encoding.UTF8, "application/json");

    return await host.Client.SendAsync(request);
  }
}
