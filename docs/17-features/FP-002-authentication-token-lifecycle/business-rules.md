---
document_id: FP-002-BR
title: Authentication Business Rules
status: Draft
version: 0.1
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
