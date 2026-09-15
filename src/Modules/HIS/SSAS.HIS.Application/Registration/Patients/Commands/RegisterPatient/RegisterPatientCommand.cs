using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Application.Registration.Patients.Commands.RegisterPatient;

public sealed record RegisterPatientCommand(
    string FirstName,
    string LastName,
    string PhoneNumber,
    DateTime DateOfBirth,
    string Gender
);
