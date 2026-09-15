# T-098 Part 1 — the draft read is UNSPECIFIED, so I stopped

- **Branch:** `agent/T-098-gl-draft-read`, pushed on start from `ClaudeBranch` at `5989a82`
- **Nothing built.** Question 1 answered "not specified", and the instruction was to stop there.

---

## First — your correction is right and mine was the sloppier reading

I classified `GL.Drafts.View` as the one dead permission on the evidence that **no drafts read exists
anywhere**. You read the same fact the other way: the writers exist, so the missing reader is **an unbuilt
half, not a dead permission.**

**The general form is worth more than the instance, and it is yours:** the absence of a reader is evidence
about which half is missing, not about whether the capability is wanted — **and which it is depends on
whether the WRITERS exist.** A sweep that asks only "does anything require this" cannot tell those apart,
which is exactly the limit that made me recommend against building the guard, arriving at the right
conclusion through a wrong classification.

---

# Q1 — NOT SPECIFIED. AND IT IS WORSE THAN THE READ HALF.

**`GL.Drafts.View` appears in NO FP-011 document.** Twelve files swept, un-truncated:

```
Drafts.View          0 hits across the whole feature package
Drafts.Manage        1 hit — traceability-matrix.md:160, the separation-of-duties note
read/view/list a draft   0 hits in acceptance-criteria, requirements, api-contracts, authorization-model
```

**And the route table has no draft routes AT ALL** — not the read, and not the four that are built:

```
api-contracts.md:43-55   nineteen rows: journals, accounts, fiscal periods, reports
                          NO /api/gl/journal-drafts row of any verb
api-contracts.md:82-84   the only mention, in prose: OD-GL-0007 "ruled two aggregates, so a draft surface
                          DOES exist — a mutable /api/gl/journal-drafts family plus a posting route"
```

**So the whole drafts family was built from one prose bullet, not from a specified contract.** The four
write routes are as unspecified as the missing read; they were simply built.

**This is therefore a product decision, not the completion of a specification** — which by your own framing
makes it yours. I stopped.

**Where the permission came from:** `72cfe9b feat(gl): application layer, permissions, and the nineteen-route
HTTP surface` — the same commit that built the nineteen routes. The constant was declared with its
separation-of-duties comment and the route that would have used it was never among the nineteen.

---

# Q2 — WHAT IT WOULD NEED, IF YOU RULE IT IN

**Both a list and a fetch by id**, mirroring journals exactly — that is the only pair the existing surface
offers for a lines-bearing aggregate, and a draft has lines.

`IGlReadService`'s contract is unusually strict and any addition inherits it:

> *"EVERY METHOD TAKES A `GlReadScope`, AND NONE HAS AN OVERLOAD WITHOUT ONE. That is the enforcement, not a
> convention: the type cannot be constructed outside `GlScopeResolver`... The scope is the FIRST parameter
> everywhere, so a call site missing it fails to compile at the opening paren."*

**Following `SearchJournalsAsync` / `GetJournalAsync`:**

```
Task<IReadOnlyList<JournalDraftListItem>> SearchJournalDraftsAsync(
  GlReadScope scope, Guid? companyId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc,
  string? reference, CancellationToken)

Task<JournalDraftDetail?> GetJournalDraftAsync(
  GlReadScope scope, Guid journalDraftId, CancellationToken)
```

**The read models are the journal ones MINUS what a draft does not have.** `JournalDraft` carries
`CompanyId`, `EntryDateUtc`, `Description`, `Reference` and `Lines(LineNumber, AccountId, Debit, Credit,
Description)` — everything `JournalDetail` has except:

- **no `JournalNumber`** — assigned at posting, so a draft has none;
- **no `ReversesJournalEntryId` / `IsReversed`** — reversal is a posted-journal concept.

`JournalDraftLine` already matches `JournalLineDetail` field for field, so the line projection is the same
shape with the account code and name joined in.

---

# Q3 — THE GATE. REPORTED, NOT CHOSEN.

**Two readings, and the codebase's own precedent cuts against the convenient one.**

**Reading A — `GL.Drafts.View` gates it, and both roles need it granted.**
The preparer holds `GL.Drafts.Manage`; **today they cannot read the draft they are editing.** The reviewer
holds `GL.Journals.Post`; they cannot see what they are about to post. Under A, *both* also need
`GL.Drafts.View` — two grants for each of two jobs.

**Reading B — the posting permission implies the read.** Convenient, and **this codebase has refused that
shape repeatedly**: T-088 and T-089 both ruled that an administrative permission is NOT a superset of the
self one, on the ground that an implied permission makes the explicit one optional and its absence
unenforceable. `AC-SS-0005` is that rule.

**And the separation of duties is the thing at stake**, in the permission's own words: *"A user who may
prepare work for someone else to post is a real separation of duties."* **The reviewer who cannot see what
they are approving is that separation defeated** — the poster either approves blind or is handed the
content out of band.

**One more fact for the ruling:** the posting route is `POST /journal-drafts/{id}/posting` gated on
`GL.Journals.Post`. So the poster already names a draft by id. Whether naming it should also let them read
it is the question, and it is the same question T-088 answered "no" for payslips.

---

## For the architect

- **Blocked on you:** whether the draft read is built at all, and under which gate. Both are product
  decisions by your own test, because nothing in FP-011 specifies either.
- **If you rule it in**, Q2 is a complete build sketch and the shape follows journals rather than inventing
  one. I would add the route to `api-contracts.md`'s table in the same change — **the table's silence about
  the four EXISTING draft routes is its own small defect**, and fixing it while adding a fifth is cheaper
  than fixing it later.
- **Not mine to decide:** whether the four built write routes should have been specified first. Reported
  because it changes what "completing the specification" means here — there is no specification to complete.
