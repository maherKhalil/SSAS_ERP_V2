---
document_id: FP-002-AC
title: Authentication Acceptance Criteria
status: Draft
version: 0.1
---

# Acceptance Criteria

- **AC-AUTH-0001:** Login failures do not disclose the exact reason.
- **AC-AUTH-0002:** One active membership is selected automatically.
- **AC-AUTH-0003:** Multiple memberships require a short-lived selection transaction.
- **AC-AUTH-0004:** A tenant without active membership cannot be selected.
- **AC-AUTH-0005:** Every tenant access token has exactly one tenant claim.
- **AC-AUTH-0006:** Roles and permissions belong only to the selected tenant.
- **AC-AUTH-0007:** Successful refresh invalidates the submitted refresh token.
- **AC-AUTH-0008:** Reuse revokes the approved scope and requires reauthentication.
- **AC-AUTH-0009:** No plaintext refresh, reset, or invitation token is stored.
- **AC-AUTH-0010:** Current-session logout does not revoke unrelated sessions.
- **AC-AUTH-0011:** Logout-all revokes every active session for the identity.
- **AC-AUTH-0012:** Password change advances security state and applies the approved revocation policy.
- **AC-AUTH-0013:** Reset request prevents account enumeration.
- **AC-AUTH-0014:** Reset tokens are single-use and expire.
- **AC-AUTH-0015:** Invitation tokens are single-use and membership-bound.
- **AC-AUTH-0016:** Failed attempts trigger approved throttling/lockout.
- **AC-AUTH-0017:** Disabled identity or membership cannot login or refresh.
- **AC-AUTH-0018:** Suspended tenants cannot receive normal tenant tokens.
- **AC-AUTH-0019:** Invalid JWT signature, issuer, audience, expiry, or claims is rejected.
- **AC-AUTH-0020:** Secrets never appear in logs or telemetry.
- **AC-AUTH-0021:** Concurrent refresh permits at most one successful rotation.
- **AC-AUTH-0022:** Signing-key overlap supports controlled rotation.
