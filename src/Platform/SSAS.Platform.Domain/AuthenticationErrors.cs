using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain;

public static class AuthenticationErrors
{
  public static readonly Error InvalidLoginEmail = new("Authentication.InvalidLoginEmail", "The login email is invalid.");
  public static readonly Error InvalidPassword = new("Authentication.InvalidPassword", "The password does not satisfy the approved policy.");
  public static readonly Error CompromisedPassword = new("Authentication.CompromisedPassword", "The password is not permitted.");
  public static readonly Error CompromisedPasswordCheckUnavailable = new("Authentication.PasswordCheckUnavailable", "Password validation is unavailable.");
  public static readonly Error GenericCredentialFailure = new("Authentication.Failed", "Authentication failed.");
  public static readonly Error InvalidAccountTransition = new("AuthenticationAccount.InvalidTransition", "The authentication account transition is invalid.");
  public static readonly Error PasswordRequired = new("AuthenticationAccount.PasswordRequired", "Initial password setup is required.");
  public static readonly Error PasswordNotAllowed = new("AuthenticationAccount.PasswordNotAllowed", "A password must not be supplied for this account.");
  public static readonly Error InvalidActionToken = new("AccountActionToken.Invalid", "The action token is invalid or unavailable.");
  public static readonly Error InvalidActionTokenHash = new("AccountActionToken.InvalidHash", "The action token hash is invalid.");
  public static readonly Error SensitiveTokenConsumed = new("AccountActionToken.SensitiveValueConsumed", "The sensitive token value is no longer available.");
  public static readonly Error ActiveMembershipCannotBeInvited = new("Invitation.ActiveMembership", "The membership cannot be invited.");
  public static readonly Error DeactivatedMembershipRequiresReactivation = new("Invitation.DeactivatedMembership", "The membership requires the approved reactivation workflow.");
  public static readonly Error InvalidAuthenticationSession = new("AuthenticationSession.Invalid", "The authentication session operation is invalid.");
  public static readonly Error GenericRefreshFailure = new("AuthenticationSession.RefreshFailed", "Refresh failed.");
  public static readonly Error GenericTenantSelectionFailure = new("Authentication.TenantSelectionFailed", "Tenant selection failed.");
  public static readonly Error InvalidRefreshToken = GenericRefreshFailure;
  public static readonly Error InvalidTenantSelection = GenericTenantSelectionFailure;
  public static readonly Error NoEligibleMembership = new("Authentication.NoEligibleMembership", "No eligible tenant membership is available.");
  public static readonly Error InvalidClientId = new("Authentication.InvalidClientId", "The authentication client is invalid.");
}
