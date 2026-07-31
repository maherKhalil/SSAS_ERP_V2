---
document_id: FP-002-BR
title: Authentication Business Rules
status: Approved for Implementation
version: 1.0
---

# Business Rules

- **BRULE-AUTH-0001:** Credential verification identifies a global Identity; tenant access starts only after membership resolution.
- **BRULE-AUTH-0002:** A tenant access token represents one Identity, one TenantUser, one tenant, and one session.
- **BRULE-AUTH-0003:** A pre-tenant transaction cannot access tenant business APIs.
- **BRULE-AUTH-0004:** Token issuance and refresh require active identity, membership, tenant, and session.
- **BRULE-AUTH-0005:** Roles and permissions in a token belong only to the selected tenant.
- **BRULE-AUTH-0006:** A refresh token succeeds at most once.
- **BRULE-AUTH-0007:** Reuse is treated as possible compromise.
- **BRULE-AUTH-0008:** Plain refresh, reset, and invitation tokens are never stored.
- **BRULE-AUTH-0009:** Revoking one session leaves unrelated sessions active unless an explicit wider action applies.
- **BRULE-AUTH-0010:** Password reset and approved password changes advance security state.
- **BRULE-AUTH-0011:** Invalid identifier, password, disabled state, and unavailable membership use indistinguishable public errors.
- **BRULE-AUTH-0012:** Invitation, reset, refresh, and tenant-selection tokens are purpose-bound.
- **BRULE-AUTH-0013:** An invitation activates only its intended identity and membership.
- **BRULE-AUTH-0014:** Administrators never create, view, or communicate passwords.
- **BRULE-AUTH-0015:** Private signing keys and production secrets never enter source control.

## Sprint-01 Milestone 2 interpretation

- A new password-based Identity receives a server-generated exact `local:{guid}` subject; login email is never the subject.
- Global `LoginEmail` and tenant-specific `TenantUser.Email` remain independent.
- Invitations create or target only a pending membership and never stage or assign roles.
- Invitation completion for an existing verified active account activates only the intended pending membership and does not change password or security version.
- `PendingSetup`, `Active`, and `Disabled` are the only authentication-account statuses. Temporary lockout is represented by failed-attempt state and `LockoutEndUtc`.
- Action-token lookup uses a public selector; the raw secret is verified against the exact purpose using a fixed-time hash comparison.
- Failed-login concurrency retries are bounded to three attempts and can never produce authentication success.
- Raw invitation and reset tokens may leave the issuing command only once through an explicitly sensitive internal result. They are not public API DTOs.
