using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// A UNIQUE INDEX OVER A NULLABLE COLUMN MUST CARRY A FILTER (item 177).
// ==================================================================================================
//
// SQL Server treats NULLs as EQUAL in a unique index. An optional column under a unique index therefore
// admits exactly one row with no value, and every later one is refused at insert -- a data defect that
// looks like nothing until a second record omits the field.
//
// `EmployeeConfiguration` states the intent for national ID:
//
//     .IsUnique().HasFilter("[NormalizedNationalId] IS NOT NULL")
//
// ⚠ **AND THAT FILTER IS A RAW T-SQL STRING, SO NOTHING TYPE-CHECKS IT.** Item 176 measured the
// consequence: no TASK-gate suite ever materialises a schema, so deleting that line compiles, keeps every
// model-shape assertion green, and merges. **This is the assertion that makes the worst known TASK-gate
// blind spot visible without a database.**
//
// ---- WHAT IS READ, AND FROM WHERE.
//
// The EF **model**, not the CLR type and not the source text. A `string` may be `IsRequired()` and a
// `Guid?` may be too, so `IProperty.IsNullable` is the only source that answers what will actually be
// created. `index.GetFilter()` is likewise the model's filter, which includes any the SQL Server provider
// adds by convention as well as those declared by hand.
//
// ---- ⚠ WHY THE CONTROLS ARE NOT OPTIONAL.
//
// This guard asserts an ABSENCE, and it has three separate ways to pass while measuring nothing: finding
// no unique indexes, running against a tenant model that contains none of the ERP, or reading a
// nullability flag that is never true. **The first version of this probe hit the second one** -- building
// `TenantDbContext` directly yields TWO entity types, none of the modules, which is the failure T-133
// recorded and `TenantModelEntityCountArchitectureTests` documents. Each is controlled below.
public sealed class UniqueIndexFilterArchitectureTests
{
  [Fact]
  public void Every_unique_index_over_a_nullable_column_carries_a_filter()
  {
    var offenders = UniqueIndexes()
      .Where(entry => entry.Index.GetFilter() is null &&
        entry.Index.Properties.Any(property => property.IsNullable))
      .Select(entry =>
        $"{entry.Model}.{entry.Index.DeclaringEntityType.ShortName()}" +
        $"[{string.Join(", ", entry.Index.Properties.Select(property => property.Name))}]")
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Empty(offenders);
  }

  // ---- ⚠ CONTROL 1: THERE ARE UNIQUE INDEXES TO JUDGE.
  [Fact]
  public void Both_models_contribute_unique_indexes()
  {
    var byModel = UniqueIndexes()
      .GroupBy(entry => entry.Model, StringComparer.Ordinal)
      .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    Assert.True(byModel.TryGetValue("Platform", out var platform) && platform >= 20, $"Platform unique indexes: {platform}");
    Assert.True(byModel.TryGetValue("Tenant", out var tenant) && tenant >= 20, $"Tenant unique indexes: {tenant}");
  }

  // ---- ⚠ CONTROL 2: THE TENANT MODEL IS THE COMPOSED ONE, NOT THE TWO-ENTITY STUB.
  // Checked by module-owned entity NAME rather than by count, so a failure says which module is missing.
  [Theory]
  [InlineData("Employee")]
  [InlineData("Account")]
  [InlineData("PayrollRun")]
  [InlineData("AttendanceRecord")]
  public void The_tenant_model_carries_every_module(string entityName) =>
    Assert.Contains(
      TenantModel().GetEntityTypes(),
      entity => string.Equals(entity.ShortName(), entityName, StringComparison.Ordinal));

  // ---- ⚠ CONTROL 3: BOTH SIDES OF THE PREDICATE ARE LIVE.
  // A `GetFilter()` that never returns a filter, or an `IsNullable` that is never true, would make the
  // guard green over anything at all.
  [Fact]
  public void The_predicate_reads_real_filters_and_real_nullability()
  {
    var indexes = UniqueIndexes().ToArray();

    Assert.Contains(indexes, entry => entry.Index.GetFilter() is not null);
    Assert.Contains(indexes, entry => entry.Index.Properties.Any(property => property.IsNullable));
  }

  private static IEnumerable<(string Model, IIndex Index)> UniqueIndexes() =>
    Models().SelectMany(pair => pair.Model
      .GetEntityTypes()
      .SelectMany(entity => entity.GetIndexes())
      .Where(index => index.IsUnique)
      .Select(index => (pair.Name, index)));

  private static IModel TenantModel() => Models().Single(pair => pair.Name == "Tenant").Model;

  private static IEnumerable<(string Name, IModel Model)> Models()
  {
    yield return ("Platform", PlatformModel());
    yield return ("Tenant", ComposedTenantModel());
  }

  private static IModel PlatformModel()
  {
    using var context = new PlatformDbContext(
      new DbContextOptionsBuilder<PlatformDbContext>().UseSqlServer("Server=model;Database=model").Options,
      new ModelUser(), new ModelTenant(), new ModelClock());

    return context.Model;
  }

  // Composed exactly as `TenantModelEntityCountArchitectureTests` composes it: contributors are read from
  // the OUTPUT DIRECTORY rather than `AppDomain.CurrentDomain.GetAssemblies()`, because .NET loads
  // assemblies lazily and nothing here touches a module type before discovery runs.
  private static IModel ComposedTenantModel()
  {
    var contributors = Directory
      .EnumerateFiles(AppContext.BaseDirectory, "SSAS.*.dll")
      .Select(LoadOrNull)
      .Where(assembly => assembly is not null)
      .Select(assembly => assembly!)
      .SelectMany(SafeTypes)
      .Where(type => typeof(ITenantModelContributor).IsAssignableFrom(type) &&
        type is { IsAbstract: false, IsInterface: false })
      .OrderBy(type => type.FullName, StringComparer.Ordinal)
      .Select(type => (ITenantModelContributor)Activator.CreateInstance(type)!)
      .ToArray();

    Assert.NotEmpty(contributors);

    using var context = new TenantDbContext(
      new DbContextOptionsBuilder<TenantDbContext>().UseSqlServer("Server=model;Database=model").Options,
      new ModelUser(), new ModelTenant(), new ModelClock(), modelContributors: contributors);

    return context.Model;
  }

  private static Assembly? LoadOrNull(string path)
  {
    try
    {
      return Assembly.LoadFrom(path);
    }
    catch (BadImageFormatException)
    {
      return null;
    }
    catch (FileLoadException)
    {
      return null;
    }
  }

  private static IEnumerable<Type> SafeTypes(Assembly assembly)
  {
    try
    {
      return assembly.GetTypes();
    }
    catch (ReflectionTypeLoadException exception)
    {
      return exception.Types.Where(type => type is not null).Select(type => type!);
    }
  }

  private sealed class ModelUser : ICurrentUser
  {
    public string? UserId => "unique-index-guard";
    public string? UserName => null;
    public string? Email => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class ModelTenant : ICurrentTenant
  {
    public Guid? TenantId => Guid.Empty;
  }

  private sealed class ModelClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
  }
}
