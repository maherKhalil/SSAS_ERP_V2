# AI Implementation Workflow

**Document ID:** DOC-AI-002

**Version:** 1.0

**Status:** Approved

**Audience:** AI Coding Assistants, Developers, Architects

---

# Purpose

This document defines the standard workflow that every AI coding assistant shall follow when contributing to SSAS ERP V2.

Unlike the Codex System Prompt, which defines permanent rules, this document defines the implementation process.

Every implementation session shall follow this workflow.

---

# Guiding Principles

The AI shall:

- Read before coding.
- Understand before implementing.
- Validate before modifying.
- Test before completing.
- Document before finishing.

Never generate code without first understanding the surrounding architecture.

---

# Phase 1 – Understand the Request

Identify:

- Sprint
- Feature Package
- Module
- Scope
- Requested outcome

If the scope is unclear:

STOP

Ask for clarification.

Do not guess.

---

# Phase 2 – Read Documentation

Read the following documents in order.

1.

Development Standards

```
docs/08-Development/Development-Standards.md
```

2.

Architecture

```
docs/03-Architecture/*
```

3.

Master Product Specification

```
docs/00-Master-Product-Specification/*
```

4.

Feature Package

```
docs/12-Feature-Packages/<Module>/<Feature>/
```

5.

Functional Specification

```
docs/02-Functional/
```

6.

Current Sprint

```
docs/13-Implementation/
```

---

# Phase 3 – Validate Documentation

Before coding verify:

✓ Requirements exist

✓ Business Rules exist

✓ Functional Specification exists

✓ Architecture supports the feature

✓ Development Standards apply

If anything is missing:

Stop implementation.

Report missing documentation.

---

# Phase 4 – Plan the Work

Break the work into small implementation tasks.

Example

- Create Entity
- Create DTO
- Create Command
- Create Validator
- Create Handler
- Create API
- Create Tests

Avoid large, unreviewable changes.

---

# Phase 5 – Implement

Follow:

- Clean Architecture
- CQRS
- Dependency Injection
- Repository Pattern (where approved)
- Development Standards

Keep commits focused.

---

# Phase 6 – Validate

Before completion verify:

Solution builds.

Tests compile.

Architecture rules are respected.

No circular dependencies.

No security violations.

No tenant isolation issues.

---

# Phase 7 – Testing

When applicable create:

Unit Tests

Integration Tests

API Tests

Architecture Tests

Performance Tests

Testing shall accompany implementation.

---

# Phase 8 – Documentation

Whenever implementation changes behavior:

Update:

- Functional Specification
- API Specification
- Database Specification
- Testing Specification

Documentation must remain synchronized with the code.

---

# Phase 9 – Review

Before marking the work complete verify:

✓ Feature requirements satisfied

✓ Coding standards followed

✓ Documentation updated

✓ Tests added

✓ Build successful

✓ No unrelated files modified

---

# Phase 10 – Completion Report

Every implementation session shall end with a report.

Include:

## Summary

Brief description of completed work.

## Files Created

List every new file.

## Files Modified

List every modified file.

## Tests

List tests created or updated.

## Build Status

Pass / Fail

## Risks

Outstanding issues.

## Next Recommended Step

The next logical implementation task.

---

# Handling Missing Information

If required information is missing:

Do not invent requirements.

Do not invent APIs.

Do not invent database tables.

Do not invent business rules.

Instead:

1. Stop.
2. Explain what is missing.
3. Request clarification.

---

# Handling Architecture Conflicts

If generated code conflicts with:

- Development Standards
- Architecture
- Solution Structure

Stop implementation.

Report the conflict.

Do not silently resolve it.

---

# Commit Guidelines

One logical change per commit.

Preferred commit sequence:

```
feat(platform): implement authentication

feat(platform): add tenant infrastructure

feat(hr): create employee domain model

feat(gl): implement chart of accounts
```

Avoid combining unrelated work.

---

# Quality Checklist

Before considering work complete:

- Solution builds successfully.
- Tests pass.
- No compiler warnings introduced without justification.
- Documentation updated.
- Security preserved.
- Tenant isolation maintained.
- Architecture preserved.
- Code is production-ready.

---

# Definition of Success

An implementation is successful only when:

- It satisfies the documented requirements.
- It follows the approved architecture.
- It passes quality checks.
- It is fully documented.
- It is ready for code review.