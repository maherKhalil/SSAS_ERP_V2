# item 200 — the rationale is back, at both sites

**Comment only. Zero non-comment lines changed**, verified by diffing the change against a
comment-and-blank filter. Builds clean.

## What was put back, and where it came from

Both `SqlDbType.Char, 1` sites — `SqlServerBackupEvidence.cs`, the two `@type` parameters — now carry the
rationale recovered from `codex/sqlserver-unicode-enforcement` (`1abde84`, 2026-08-15): the parameter
compares against `msdb.dbo.backupset.type`, **a Microsoft-owned system column that IS `char(1)`**, and
matching it avoids an implicit conversion of the **column** side of the predicate.

**Provenance is stated in the comment itself**, as ruled — a rationale recovered from an unmerged branch
should say so, because the next reader cannot otherwise tell it apart from something invented.

## ⚠ AND IT ADDRESSES THE SWEEP THAT WOULD BREAK IT

The comment names `Every_persisted_string_in_the_tenant_model_is_unicode` (and its platform twin) and says
why these parameters sit **outside** that guard rather than escaping it:

> the guard requires every string column **this system persists** to be Unicode, because a non-Unicode
> column substitutes `'?'` silently. **These parameters persist nothing** — they are ADO parameters in a
> read against a **system database** whose schema Microsoft owns, and the guard walks the EF model, which
> none of this is in.

**So a reader cannot conclude the ban was evaded**, which was the failure mode: two bare `Char` parameters
beside `NVarChar` neighbours read as an oversight, and "fixing" them would cost the index.

⚠ **Both guard names were verified to exist** (`UnicodeStringPersistenceArchitectureTests.cs:62` and `:77`)
before being cited. **A comment naming a test that does not exist is worse than no comment** — it invites a
reader to go looking, fail, and distrust the rest.

## Both sites, deliberately, not one and a cross-reference

The first carries the full note; the second states the load-bearing claim in full and points at the first
for detail. ⚠ **Whoever "tidies" the second line will be reading the SECOND line**, so it has to defend
itself. This is repetition that the situation requires, not duplication to factor out.

## ⚠⚠ THE GATE CAUGHT MY OWN COMMENT, ON THE EXACT FAILURE THIS ITEM IS ABOUT

**`CommentCitationGuardTests.Every_cited_identifier_resolves_or_is_recorded` went RED on the first run** --
two entries, `SqlServerBackupEvidence.cs:96` and `:182`, both `msdb.dbo.backupset.type`.

**A repository guard exists for comments that cite things which do not resolve** -- the very hazard I had
just written into this file as *"a comment naming a test that does not exist is worse than no comment"* --
**and it fired on mine.** I had verified the two test names by hand and never considered that the SQL
column would be read as a citation at all.

⚠ **And my guess about the cause was wrong.** I expected the ellipsis in the second site's shortened
guard name; **the instrument named the real one**: `FileLike` classifies a lowercase-dotted token as a
FILE, so `database.schema.table.column` is indistinguishable from a filename.

**Recorded in `KnownUnresolvable` rather than worked around**, which is the guard's designed path and has an
exact precedent three lines above -- *"a permission NAME in lowercase-dotted form, which `FileLike` cannot
tell from a filename"*. The file's own reasoning says why recording beats rephrasing: **"a false positive
reported is a floor measurement; a false positive silently dropped is a guard learning to lie."**

⚠ **The entry is proved necessary rather than assumed**: `Nothing_is_recorded_that_no_longer_needs_to_be`
fails on any entry that is not actually unresolvable, and it passes -- **so the list did not become a place
to hide something.** Gate re-run: `[GATE GREEN -- TASK scope]`, 0 warnings.

## Scope
- **Comment only** — the two `SqlDbType.Char` parameters are unchanged, as is every behaviour.
- The claim that matching the column type avoids an implicit conversion is **quoted from the branch, not
  re-measured**. No execution plan was captured; it is a documented rationale restored, not a new finding.
