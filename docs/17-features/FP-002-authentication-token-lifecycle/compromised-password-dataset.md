---
document_id: FP-002-COMPROMISED-PASSWORD-DATASET
title: Compromised Password Dataset Deployment
status: Approved for Implementation
version: 1.0
---

# Compromised Password Dataset Deployment

Milestone 2 does not commit or redistribute a production compromised-password corpus. Production operators must provide a reviewed, versioned offline dataset and must confirm that their selected dataset license permits the intended deployment and use.

## File format

The adapter accepts a UTF-8 text file containing one SHA-256 password hash per line as 64 hexadecimal characters. Empty lines and lines beginning with `#` are ignored. The file contains hashes only; raw passwords must not be added to it.

The in-memory adapter accepts at most 64 MiB and 1,000,000 distinct hashes. Deployment validation fails before use when either bound is exceeded. This keeps startup parsing and the singleton lookup set bounded; operators needing a larger corpus must provide a separately reviewed adapter in a later approved change.

## Required production configuration

Configure `Authentication:CompromisedPasswords` with:

- `Enabled: true`;
- `DatasetPath`: an absolute path or a path relative to the Host content root;
- `DatasetVersion`: the deployment-owned dataset version;
- `LicenseName`: the reviewed license name;
- `LicenseUrl`: the reviewed license or attribution location.

`LicenseUrl` must be an absolute HTTP(S) URL without embedded user credentials. Dataset paths and metadata must not contain deployment credentials.

Production startup fails when checking is disabled, when metadata is missing, when the file is missing or unreadable, or when any non-comment dataset line is not an exact SHA-256 hash. Development explicitly disables the production adapter and automated tests provide explicit checker implementations.

The Milestone 2 password policy accepts 12 through 128 characters. This exceeds the approved requirement to support at least 64 characters while retaining a bounded hashing input.

The repository does not select, endorse, or sublicense a third-party corpus. Deployment owners must record the source, version, license review, update cadence, and integrity-verification process in their release evidence.
