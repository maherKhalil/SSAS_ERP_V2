namespace SSAS.HIS.Application.Registration.Patients.Queries.GetPatientById;

public sealed record PatientDto(
    Guid Id,
    string FirstName,
    string LastName,
    string PhoneNumber,
    DateTime DateOfBirth,
    string Gender
);
