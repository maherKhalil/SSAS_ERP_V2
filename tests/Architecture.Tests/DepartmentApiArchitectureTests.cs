using System.Reflection;
using SSAS.HR.Application.Departments.Reads;

namespace SSAS.Architecture.Tests;

// THE DEPARTMENT TRANSPORT LAYER'S STRUCTURAL GUARANTEES (FP-007 Phase 4).
//
// The HTTP layer parses transport, dispatches, and maps the answer. Everything it must NOT do is asserted
// here by shape — a type it cannot reference, a member it does not have — rather than by a rule someone
// has to remember while reviewing a new route.
public sealed class DepartmentApiArchitectureTests
{
  private static readonly Assembly HrApiAssembly =
    typeof(SSAS.HR.API.Departments.DepartmentApiErrorMapper).Assembly;

  // ================================================================================================
  // THE API LAYER CANNOT READ DEPARTMENTS WITHOUT GOING THROUGH THE SCOPED HANDLER.
  // ================================================================================================
  //
  // `IDepartmentReadService` requires a `DepartmentReadScope` on every method, and that scope can only be
  // produced by the resolver. If transport held the read service directly it could still not fabricate a
  // scope — but it could pass one obtained for a DIFFERENT purpose, and more practically it would put
  // query composition in a layer that has no business deciding what a caller may see.
  //
  // So the API depends on the QUERY HANDLERS instead, which resolve their own scope per request. This
  // asserts the read service never appears in the transport assembly at all.
  [Fact]
  public void The_api_layer_never_holds_the_department_read_service()
  {
    // ⚠ FOUR TESTS HERE PASSED OVER AN EMPTY TYPE SET (T-258). The floor is on the assembly's types,
    // which is what both offender scans read.
    var hrApiTypes = HrApiAssembly.GetTypes();
    Assert.True(hrApiTypes.Length >= 10,
      $"only {hrApiTypes.Length} HR API types were scanned; the assembly reference is wrong or the " +
      "enumeration collapsed, and an empty offender list below would mean nothing.");

    var offenders = hrApiTypes
      .SelectMany(type => type.GetFields(
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
        .Select(field => (Type: type, Member: field.Name, field.FieldType))
        .Concat(type.GetProperties(
          BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
          .Select(property => (Type: type, Member: property.Name, FieldType: property.PropertyType))))
      .Where(member => member.FieldType == typeof(IDepartmentReadService))
      .Select(member => $"{member.Type.Name}.{member.Member}")
      .ToArray();

    Assert.Empty(offenders);
  }

  // ---- NOR THE SCOPE RESOLVER, NOR A SCOPE.
  //
  // Holding the resolver would let transport decide WHEN to resolve, and holding a scope would let it
  // carry one across operations. Both are the handlers' business.
  [Fact]
  public void The_api_layer_never_holds_a_department_scope_or_its_resolver()
  {
    var forbidden = new[] { typeof(IDepartmentScopeResolver), typeof(DepartmentReadScope) };

    var hrApiTypes = HrApiAssembly.GetTypes();
    Assert.True(hrApiTypes.Length >= 10,
      $"only {hrApiTypes.Length} HR API types were scanned; an empty offender list below would mean " +
      "nothing.");

    var offenders = hrApiTypes
      .SelectMany(type => type.GetMethods(
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static |
        BindingFlags.DeclaredOnly))
      .SelectMany(method => method.GetParameters().Select(parameter => (method, parameter.ParameterType)))
      .Where(entry => forbidden.Contains(entry.ParameterType))
      .Select(entry => $"{entry.method.DeclaringType?.Name}.{entry.method.Name}")
      .ToArray();

    Assert.Empty(offenders);
  }

  // ---- AND NO PERSISTENCE AT ALL.
  //
  // A DbContext or a repository in transport would let a route compose its own query, which is every scope
  // guarantee in this module undone in one line that would look perfectly ordinary in review.
  [Fact]
  public void The_api_layer_references_no_persistence_type()
  {
    Assert.DoesNotContain(
      HrApiAssembly.GetReferencedAssemblies(),
      reference => reference.Name is "Microsoft.EntityFrameworkCore" or "Microsoft.Data.SqlClient");

    Assert.DoesNotContain(
      HrApiAssembly.GetReferencedAssemblies(),
      reference => reference.Name?.StartsWith("SSAS.HR.Infrastructure", StringComparison.Ordinal) ?? false);
  }

  // ---- THE MODULE BOUNDARY (ADR-012).
  //
  // HR.API must not reference Platform's assemblies. This is the guard that caught a real attempt during
  // Phase 4: the department error mapper first translated to Platform's `Persistence.ConcurrencyConflict`,
  // and the compiler refused. HR's own error maps to the same problem code, so the wire answer is
  // unchanged and the boundary holds.
  [Fact]
  public void The_hr_api_references_no_platform_assembly()
  {
    Assert.DoesNotContain(
      HrApiAssembly.GetReferencedAssemblies(),
      reference => reference.Name?.StartsWith("SSAS.Platform", StringComparison.Ordinal) ?? false);
  }
}
