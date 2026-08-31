using Microsoft.Extensions.DependencyInjection;
using SSAS.Attendance.API;
using SSAS.Attendance.Infrastructure;
using SSAS.GL.Infrastructure;
using SSAS.HR.Infrastructure;
using SSAS.Payroll.Infrastructure;
using SSAS.GL.API;
using SSAS.HR.API;
using SSAS.Payroll.API;

namespace SSAS.Architecture.Tests;

// ================================================================================================
// EVERY MODULE'S OWN REGISTRATION BINDS ITS READ SERVICES TO A CONCRETE TYPE (item 235).
// ================================================================================================
//
// ---- ⚠⚠ WHY THIS EXISTS, AND IT IS NOT A HYPOTHETICAL.
//
// `PayrollReadService`, `GlReadService` and `AttendanceReadService` were constructed by NO test in any
// suite. Item 233 exercised them for the first time and **`GlReadService` turned out to carry two live
// defects — queries that could not execute at all, one of them the trial balance.**
//
// **The cause was one difference, not three omissions: two of six API test hosts called
// `Add<Module>Module()` and never `Add<Module>Infrastructure()`**, so the concrete class was never
// registered and their stub was never challenged. ⚠ **A seventh module wired from one of those hosts as
// the template inherits the hole**, and no per-module test can see that coming.
//
// ---- ⚠ WHAT THIS GUARDS, AND WHAT IT EXPLICITLY DOES NOT.
//
// **This guards the REGISTRATION.** It asserts each module's own extensions bind every `I…ReadService`
// they register to a concrete implementation FROM THAT MODULE'S INFRASTRUCTURE ASSEMBLY.
//
// ⚠⚠ **It does NOT guard that the service is ever EXERCISED, and Attendance is the proof: its host DOES
// call `AddAttendanceInfrastructure` and then registers an explicit stub, last-in-wins.** **This test
// passes for Attendance today and passed for it before item 233 was written.** **Item 233 guards the
// behaviour; this guards the wiring; neither substitutes for the other.**
//
// ---- THE FORM: DESCRIPTORS, AND THE DISTINCTION IS DELIBERATE.
//
// The REAL extensions are called on a real `ServiceCollection`; what is inspected is what they
// REGISTERED. ⚠ **That is not the weaker form — the weaker form would assert descriptors INSTEAD of
// calling the extension.** Resolving would additionally require a tenant context accessor, which is
// fixture wiring this claim does not need: the failure mode is *the module's registration does not bind
// the concrete type*, and a descriptor answers exactly that.
public sealed class ModuleReadServiceRegistrationTests
{
  [Fact]
  public void Every_module_registration_binds_its_read_services_to_its_own_infrastructure()
  {
    var modules = new (string Name, Action<IServiceCollection> Register, string InfrastructureAssembly)[]
    {
      ("HR", services => services.AddHrModule().AddHrInfrastructure(), "SSAS.HR.Infrastructure"),
      ("GL", services => services.AddGlModule().AddGlInfrastructure(), "SSAS.GL.Infrastructure"),
      ("Payroll", services => services.AddPayrollModule().AddPayrollInfrastructure(),
        "SSAS.Payroll.Infrastructure"),
      ("Attendance", services => services.AddAttendanceModule().AddAttendanceInfrastructure(),
        "SSAS.Attendance.Infrastructure"),
    };

    // ⚠ THE MODULE COUNT IS NAMED AND FLOORED. A module added later is not covered by this test until
    // somebody adds it here, and the assertion is what makes that a FAILURE rather than a silent gap —
    // the whole defect being guarded is a new module inheriting an old template.
    Assert.Equal(
      ["Attendance", "GL", "HR", "Payroll"],
      modules.Select(module => module.Name).OrderBy(name => name, StringComparer.Ordinal));

    var offenders = new List<string>();
    var checkedServices = 0;

    foreach (var module in modules)
    {
      var services = new ServiceCollection();
      module.Register(services);

      var readServices = services
        .Where(descriptor => descriptor.ServiceType.IsInterface)
        .Where(descriptor => descriptor.ServiceType.Name.EndsWith("ReadService", StringComparison.Ordinal))
        .ToArray();

      // Each module must register at least one. A module that registered none would otherwise contribute
      // nothing to the offender list and read as compliant.
      if (readServices.Length == 0)
      {
        offenders.Add($"{module.Name}: registers no read service at all");
        continue;
      }

      foreach (var descriptor in readServices)
      {
        checkedServices++;

        // ⚠ A FACTORY OR AN INSTANCE LEAVES `ImplementationType` NULL. That is how a stub is registered,
        // and it is the shape this test exists to distinguish from a real binding.
        if (descriptor.ImplementationType is not { } implementation)
        {
          offenders.Add($"{module.Name}.{descriptor.ServiceType.Name}: bound to a factory or instance, " +
            "not to a concrete type");
          continue;
        }

        var assembly = implementation.Assembly.GetName().Name;
        if (!string.Equals(assembly, module.InfrastructureAssembly, StringComparison.Ordinal))
        {
          offenders.Add(
            $"{module.Name}.{descriptor.ServiceType.Name}: bound to {implementation.Name} in {assembly}, " +
            $"expected {module.InfrastructureAssembly}");
        }
      }
    }

    // ⚠ ANTI-VACUITY: the four-link selection above (interface, name suffix, per module) is what produces
    // the offender list, and an empty list is equally consistent with everything passing and with nothing
    // being examined.
    Assert.True(checkedServices >= 4,
      $"only {checkedServices} read-service registrations were found across {modules.Length} modules; " +
      "the descriptor selection has stopped matching and the check below would judge nothing.");

    Assert.True(
      offenders.Count == 0,
      "These module registrations do not bind a read service to a concrete type in the module's own " +
      "infrastructure assembly. A module whose Add<Module>Infrastructure() is not called — or whose read " +
      "service is bound to a factory — leaves the concrete class unconstructed, which is how two live " +
      "defects survived in GlReadService: " + string.Join("; ", offenders));
  }
}
