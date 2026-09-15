# item 158 — the password hasher's actual algorithm and cost

**Measurement only. Nothing built, nothing edited.** Closes the gap stated in
`item-157-anonymous-door.md`.

## The figures

| | |
|---|---|
| assembly | `Microsoft.Extensions.Identity.Core` **8.0.30** (`8.0.0.0`), ASP.NET Core shared framework |
| format | **Identity V3** (`CompatibilityMode.IdentityV3`, marker byte `0x01`) |
| algorithm | **PBKDF2 with HMAC-SHA512** |
| **iteration count** | **100,000** |
| salt | **128-bit** (16 bytes), per hash |
| subkey | **256-bit** (32 bytes) |

## Where these were read

**Not from documentation or recall.** A probe built against the same framework reference hashed a password
with this repository's configured options and **decoded the header of the hash it produced** — the V3
format encodes the PRF, the iteration count and the salt length in bytes 1–12. The PRF byte read `2`,
which is `HMACSHA512`. The assembly version was read from the loaded assembly.

## ⚠ It is NOT the framework default — it is configured AND floored

item 157 said this "delegates to ASP.NET Core Identity's default, not a custom scheme." **The first half
was wrong.** `PlatformPersistenceServiceCollectionExtensions` binds and validates it:

```
services.AddOptions<PasswordHasherOptions>()
  .Bind(configuration.GetSection("Authentication:PasswordHasher"))
  .Validate(options => options.IterationCount >= 100_000,
    "Password hashing iteration count must be at least 100000.")
  .ValidateOnStart();
```

`appsettings.json` sets `Authentication:PasswordHasher:IterationCount` to **100000**.

⚠ **The configured value happens to equal the .NET 8 default, so today the setting changes no behaviour —
but the VALIDATOR is not a no-op.** It pins a **floor that survives a framework upgrade lowering the
default, a configuration edit, and an environment override**, and it fails at start-up rather than
silently hashing weaker. **A number that cannot be configured below a floor is a different kind of fact
from one that merely happens to be adequate today** — the same distinction as item 157's 15-minute
access-token cap.

## Weaker legacy hashes are accepted once and upgraded

`Aspnet_hasher_preserves_three_state_verification_and_requests_upgrade` (`SEC-AUTH-0201`, `TS-AUTH-0011`)
pins three-state verification: a hash produced at **10,000** iterations verifies under the production
hasher as **`SuccessRehashNeeded`**, a wrong password as **`Failed`**, and a current-cost hash as
**`Success`**. So a credential stored under an older cost still authenticates and is **re-hashed at the
current cost**, rather than being locked out or silently left weak.

## Adjacent, found while reading

Compromised-password checking is registered and enabled — `ICompromisedPasswordChecker` /
`OfflineCompromisedPasswordChecker`, with `Authentication:CompromisedPasswords:Enabled = true`. **Offline**,
so no credential material leaves the process. Not measured further here.

## Scope

The probe used a `FrameworkReference` to `Microsoft.AspNetCore.App`, resolving the same shared framework
the application runs on **on this machine**. A deployment on a different patch level could load a different
`Identity.Core`; the **floor validator holds regardless**, but the default it sits above is version-bound.

**MFA remains unexamined**, and is a product decision, not a measurement.
