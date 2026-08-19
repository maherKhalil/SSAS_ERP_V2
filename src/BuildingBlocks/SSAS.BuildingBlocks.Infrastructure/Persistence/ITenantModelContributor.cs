using Microsoft.EntityFrameworkCore;

namespace SSAS.BuildingBlocks.Infrastructure.Persistence;

// HOW A BUSINESS MODULE PUTS ITS ENTITIES INTO THE TENANT ERP MODEL (FP-006C3-pre, ADR-012, ADR-017).
//
// ---- THE PROBLEM THIS SOLVES.
//
// Tenant business data lives in ONE context and ONE migration stream (ADR-017): a second context would mean
// a second history table, a second connection to route, and no shared transaction between them. But the
// context is owned by Platform, and ADR-012 forbids Platform from referencing HR or GL to map their
// entities — and forbids them from referencing Platform to register themselves.
//
// So the module contributes its mapping through this abstraction, which neither side owns: Platform calls
// it without knowing who implements it, and a module implements it without referencing Platform.
//
// ---- IT IS EXPLICIT REGISTRATION, NOT DISCOVERY.
//
// ADR-012 rejects reflection-based runtime module discovery for V1. Contributors are registered by the Host
// — the composition root, and the one place permitted to see every module's Infrastructure — so the set of
// modules in a deployment is stated in code rather than inferred from what happens to be on disk.
//
// ---- IT SHAPES THE MODEL, SO IT SHAPES THE MODEL CACHE KEY.
//
// EF caches one model per context type by default, which would let a model built with one contributor set
// serve a context expecting another. TenantDbContext therefore folds its contributor set into the cache key
// (see TenantModelCacheKeyFactory). Contributors must be deterministic for that to hold: the same set must
// always produce the same model, so an implementation must not vary its mapping by tenant, request, or any
// ambient state (ADR-017 "the model is tenant-invariant").
public interface ITenantModelContributor
{
  // Called during OnModelCreating, BEFORE the shared conventions run, so entities added here receive the
  // global tenant query filter and the restricted delete behaviour exactly as Platform's own do.
  void Configure(ModelBuilder modelBuilder);
}
