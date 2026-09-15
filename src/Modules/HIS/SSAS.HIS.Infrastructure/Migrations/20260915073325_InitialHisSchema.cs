using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAS.HIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialHisSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ABGss_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PatientID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DoctorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PH = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PCO2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PO2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SA02 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HCT = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Hb = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BEecf = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Beb = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SBC = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Hco3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tco2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    A = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AaDo2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    aA = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RT = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    O2cap = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    O2CT = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FO2Hb = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KPlus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NAPlus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CI = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GLU = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LAC = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BASE = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ABGss_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccommodationTypes_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HospitalCase = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ServiceID = table.Column<int>(type: "int", nullable: false),
                    AccommodationTypeID = table.Column<int>(type: "int", nullable: false),
                    Companion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    IsEmergency = table.Column<bool>(type: "bit", nullable: false),
                    NameKa = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccommodationTypes_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccommodationTypess_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ServiceID = table.Column<int>(type: "int", nullable: false),
                    Companion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccommodationTypesID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccommodationTypess_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Additivess_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GenericId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TemplateId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Additivess_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdministrationSites_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministrationSites_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminstrationMasters_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminstrationMasters_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdmissionCategorys_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HospitalCase = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionCategorys_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdmissionPurposes_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionPurposes_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdmissionRequests_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    OPnumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    AdmissiontypeID = table.Column<int>(type: "int", nullable: false),
                    AdmissionWardID = table.Column<int>(type: "int", nullable: false),
                    AdmissionPurposeID = table.Column<int>(type: "int", nullable: false),
                    AdmissionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdmissionTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Days = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SurgeryTypeID = table.Column<int>(type: "int", nullable: false),
                    DeliveryTypeID = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RoomTypeID = table.Column<int>(type: "int", nullable: false),
                    BedNO = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InsuranceAuthorizationCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Instructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MedicalCondition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MLCTypeID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    AccomodationTypeID = table.Column<int>(type: "int", nullable: false),
                    ExpectedDischargeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmergencyUnitID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmissionRequests_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdmitPatientss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDayAdmission = table.Column<bool>(type: "bit", nullable: false),
                    InsuranceAuthorizationCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdmissionRequestID = table.Column<int>(type: "int", nullable: false),
                    AdmissionWardID = table.Column<int>(type: "int", nullable: false),
                    Days = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MLCTypeID = table.Column<int>(type: "int", nullable: false),
                    AdmissionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdmissionTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BedNO = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RoomTypeID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DischargeStatusId = table.Column<int>(type: "int", nullable: false),
                    ExpectedDateOfDischarge = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedTimeOfDischarge = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    EmergencyUnitID = table.Column<int>(type: "int", nullable: false),
                    AccomodationTypeId = table.Column<int>(type: "int", nullable: false),
                    PatLimitID = table.Column<int>(type: "int", nullable: false),
                    AdmitType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdmitNurseID = table.Column<int>(type: "int", nullable: false),
                    NeedEscort = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EscortBedID = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdmitDoctorID = table.Column<int>(type: "int", nullable: false),
                    AdmitUrgency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsIsolation = table.Column<bool>(type: "bit", nullable: false),
                    AdmitionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsReferral = table.Column<bool>(type: "bit", nullable: false),
                    RefDoctorName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RefClinicName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SponsorID = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ToOPD = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EncounterType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EncounterAdmit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Encounterstatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    serviceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    careteamrole = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmitPatientss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdmitPatientToNewMedUnits_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    PatAccom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatWard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatRoom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatBed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsCompanion = table.Column<bool>(type: "bit", nullable: false),
                    CompAccom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompWard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompRoom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompBed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    PatDiet = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompSex = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompDiet = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Discharge = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DischargeType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DischargeDoctor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DischargeDiagnosis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeathDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeathTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelAdmit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DischargeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmitPatientToNewMedUnits_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdmittingPatients_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    Approx = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorsID = table.Column<int>(type: "int", nullable: false),
                    AdmitDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdmitTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MLCID = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WardId = table.Column<int>(type: "int", nullable: false),
                    BedNoID = table.Column<int>(type: "int", nullable: false),
                    ISDaycase = table.Column<bool>(type: "bit", nullable: false),
                    ISEmergency = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdmittingPatients_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AllergyDetailss_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AllergyMasterID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllergyDetailss_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "AllergyMasters_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllergyMasters_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "AllowUsersToShowReportss_Laboratory",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeID = table.Column<int>(type: "int", nullable: false),
                    ResultEntryHeaderD = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllowUsersToShowReportss_Laboratory", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Antibioticss_Laboratory",
                columns: table => new
                {
                    AntibioticsID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AntibioticName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Antibioticss_Laboratory", x => x.AntibioticsID);
                });

            migrationBuilder.CreateTable(
                name: "ArrivalTypes_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ArrivalTypeDescArabic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ArrivalTypeDescEnglish = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArrivalTypes_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Batchess_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BatchNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GRNDetailsId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiryDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LPODetailsId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Batchess_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BedLockPurposes_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Default = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BedLockPurposes_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BedRenewals_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ward = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Room = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Bed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Infexted_Renewed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BedRenewals_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Beds_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BedNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BedStatusId = table.Column<int>(type: "int", nullable: false),
                    ChildBed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    BedTypeId = table.Column<int>(type: "int", nullable: false),
                    BedSequence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CountableBed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Active = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DescriptionAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssetID = table.Column<int>(type: "int", nullable: false),
                    IsBed = table.Column<bool>(type: "bit", nullable: false),
                    BedTypeIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Beds_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BedStatusChangeTrackers_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BedLockPurposeId = table.Column<int>(type: "int", nullable: false),
                    BedId = table.Column<int>(type: "int", nullable: false),
                    BedStatusChangeProcessEnumValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BedStatusChangeTrackers_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BedStatuss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusImage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BedStatuss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BedSwaps_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BedFrom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BedTo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientFrom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientTo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateSwap = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BedSwapStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RejectionReson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientFromReadyToSwap = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientToReadyToSwap = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientFromSwap = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientToSwap = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SwapReson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApproveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequesterNurseID = table.Column<int>(type: "int", nullable: false),
                    ApprovalNurseID = table.Column<int>(type: "int", nullable: false),
                    PatientFromSwapNurseID = table.Column<int>(type: "int", nullable: false),
                    PatientToSwapNurseID = table.Column<int>(type: "int", nullable: false),
                    PatientFromReadyToSwapNurseID = table.Column<int>(type: "int", nullable: false),
                    PatientToReadyToSwapNurseID = table.Column<int>(type: "int", nullable: false),
                    UCreateby = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientFromSwapDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PatientToSwapDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PatientFromReadyToSwapDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PatientToReadyToSwapDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PatientFrom_EscortBedFrom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientFrom_EscortBedTo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientTo_EscortBedFrom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientTo_EscortBedTo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BedSwaps_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "BedTrackers_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OPIP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    BedId = table.Column<int>(type: "int", nullable: false),
                    FromDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BedTrackers_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BedTypes_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TypeImage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameKa = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BedTypes_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BedWardArrangements_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WardId = table.Column<int>(type: "int", nullable: false),
                    RowsNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BedCount = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BedWardArrangements_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Blacklists_InPatient",
                columns: table => new
                {
                    BlacklistId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NationalIdType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdentificationNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blacklists_InPatient", x => x.BlacklistId);
                });

            migrationBuilder.CreateTable(
                name: "BloodBankInventoryBloodGroups_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BloodBankInventoryId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BloodGroupId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodBankInventoryBloodGroups_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodBankInventorys_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BagNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Volume = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BloodProductID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpireDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonationInfoID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NoUnits = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BloodGroupID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodBankInventorys_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodBankLPODetails_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LPOHeaderId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BloodGroupId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderedQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BonusQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcceptQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrevAcceptQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodBankLPODetails_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodBankLPOHeaders_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    InventoryId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LPOCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PurchaseOrderNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LPODate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsEmergency = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodBankLPOHeaders_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodBankSettingss_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Expiry = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonerPeriodBlock = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonerPeriodWorning = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DPrice = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonationAccount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonationPaidAccount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Minbloodbags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodBankSettingss_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodBankss_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fax = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PersonOnCharge = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodBankss_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodGroupCompatables_BloodBank",
                columns: table => new
                {
                    BloodGroupCompatableID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BloodGroupID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    given = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Taken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodGroupCompatables_BloodBank", x => x.BloodGroupCompatableID);
                });

            migrationBuilder.CreateTable(
                name: "BloodGroups_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BloodGroupCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GroupNameLatin = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GroupNameLocal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodGroups_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodProducts_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BloodProductCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductNameLatin = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductNameLocal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BloodGroupId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Active = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasExpireDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpireDateValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpireDateType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDonation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameKa = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodProducts_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodServicePrices_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BloodGroupID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ServiceID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BloodProductID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonationServiceID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodServicePrices_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodStreamDetailss_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BloodStreamId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dailyreview = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Performhandhygiene = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Maintainaseptictechnique = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Thedisinfectionofcatheterhub = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UseaCentralVenousCatheter = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Usesteriletransparent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Gauzedressings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Transparentdressings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IVadministrationsystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Date = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Time = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodStreamDetailss_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodStreams_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModificationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NurseID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DoctorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PatientId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cathetertype = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PerformhandBloodStream = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Usebetadine = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Maximalsterile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodStreams_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodTestingResultDetails_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BagID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TestID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Result = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodTestingResultDetails_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodTestingResultMasters_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BagID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    firstResult = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecondResult = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodTestingResultMasters_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodTestings_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TestType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LabSection = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ServiceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodTestings_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodTransferRequests_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TransferRequestCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PatientID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BloodProuductID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodTransferRequests_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodTransfusions_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PatientId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DoctorId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NurseId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReactionDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReactionTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reasonfortransfusion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Component = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VolumeGiven = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonationNumberinUnits = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsChills = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsUrticaria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsTachycadia = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsChestPain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsNausea = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDyspnoea = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsLumbarPain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsBurningaroundveinhypotension = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsHaemoglobinuria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsExcessiveBleeding = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsJaundice = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsShock = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    otherSymptoms = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    treatmentgiven = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    result = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    previoustransfusion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reactions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Pregnancies = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KnownAntiBodies = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransfusionDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransfusionTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    signture = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BloodBankrequestrecivedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BloodBankrequestrecivedTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LabellingError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    pretransfusion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    preAntibody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    preDAT = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Posttransfusion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PostAntibody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PostDAT = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvestDtlID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ISPregnancies = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    otherDoctor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeOfInformDR = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransfusionStartTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransfusionEndTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsTransfusionAction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    dispensedBag = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodTransfusions_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BloodTransfustionActiontakens_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BloodTransfusionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeofAction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetailsofAction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodTransfustionActiontakens_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Borrowings_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NameEN = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameAR = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BranchId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Borrowings_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Brandss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BrandName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GenericNamesId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brandss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Buildingss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuildingCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BuildingStatusId = table.Column<int>(type: "int", nullable: false),
                    BuildingSequence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buildingss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CafeteriaChargess_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    LastIPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChargeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ServiceID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Charge = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsGuest = table.Column<bool>(type: "bit", nullable: false),
                    IsNoCharge = table.Column<bool>(type: "bit", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CafeteriaChargess_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Campaigns_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CampDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NoDoctors = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NoNurses = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NoTechnicians = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NoBags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NoDoners = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CancelAdmissions_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    DateCancelation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdmitPatientsID = table.Column<int>(type: "int", nullable: false),
                    ReasonID = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CancelAdmissions_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CancelAdmitPatientInAnotherMedicalUnits_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    DischargeReasonID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CancelAdmitPatientInAnotherMedicalUnits_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CancelDischargePatientInAnotherMedicalUnits_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    DischargeReasonID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CancelDischargePatientInAnotherMedicalUnits_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CancelDischargeReasons_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnglishName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArabicName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Active = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CancelDischargeReasons_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CancelDischarges_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    DischargeReasonID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CancelDischarges_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CancelIntialDischarges_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    InitialDischID = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CancelIntialDischarges_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CancellationOfPatientRequests_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderID = table.Column<int>(type: "int", nullable: false),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    ReasonCancellation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CancellationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CancellationOfPatientRequests_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CancelTypesMasters_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnglishDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArabicDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CancelType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CancelTypesMasters_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Cardiac_Electrphysiologys_InPatient",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatintID = table.Column<int>(type: "int", nullable: false),
                    ReportDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    History = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Medications = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ECG = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CardiacProcedure = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ventricular_Pacing = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Atrial_Pacing = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RadioFreq_Ablation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ablation_Target = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RFCurrent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ECG_DuringRF = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Diagnosis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Recommendation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cardiac_Electrphysiologys_InPatient", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "CardiacCatheterizations_InPatient",
                columns: table => new
                {
                    CardiacId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RiskFactors = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Operators = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Procedures = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HemodyNamic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Angiogrephic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeftMainCoronaryArtery = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeftAnteriorDescending = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeftCircumflexArtery = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RightCoronary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeftVentriculography = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Recommendations = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardiacCatheterizations_InPatient", x => x.CardiacId);
                });

            migrationBuilder.CreateTable(
                name: "CasePrioritys_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CasePrioritys_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChangePatientDoctors_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    OldDoctorId = table.Column<int>(type: "int", nullable: false),
                    NewDoctorId = table.Column<int>(type: "int", nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangePatientDoctors_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "clabsibundleDetailss_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    clabsibundletId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Keepcatheterproperlysecuredtopreventmovementurethraltraction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    bagbelow = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    bagonce = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    urineflow = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Maintainclosed = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Performhand = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Obtainurinesampleasepticall = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    techniquedisconnection = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Perinealhygiene = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reassess = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Date = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Time = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clabsibundleDetailss_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClabsiBundles_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModificationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NurseID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DoctorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PatientId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Appropriateindicationforindwellingurinarycatheterplacement = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RiskforUTIevaluatedpriortopatientcatheterization = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Alternativestoindwellingcatheterplacementdiscussedpriortocatheterization = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnymajorpreexistingconditionsuchasDMmalnutritionorrenalinsufficiency = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Performhandhygieneimmediatelybeforeandafterinsertionofthecatheterdeviceorsite = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Securecathetertubingtopreventurethralirritationandmovementofurethraltraction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Usesterile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Positionthedrainage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Maintainstrict = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Checksystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Strictprolongedimmobilization = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Bladderoutletobstruction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Improvecomfortforendoflife = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Assist = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AppropriateindicationforindwellingurinarycatheterplacementOther = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClabsiBundles_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClinicalServicess_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Service = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    doctorId = table.Column<int>(type: "int", nullable: false),
                    ServiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalServicess_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClinicalSnomeds_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalSnomeds_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ClinicLocations_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicLocations_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ClinicProceduress_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcedureID = table.Column<int>(type: "int", nullable: false),
                    ServiceID = table.Column<int>(type: "int", nullable: false),
                    ProcedureSlot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FollowUpNum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FollowUpDuration = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItFollowUp = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClinicID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Creationdate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FollowUpPeriod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicProceduress_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ClinicSchedules_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayID = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubSpecialityID = table.Column<int>(type: "int", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClinicID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Creationdate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicSchedules_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ClinicSetups_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeSlot = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OverBooking = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DepartmentID = table.Column<int>(type: "int", nullable: false),
                    SpeciaityGroupID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MedicalRecordLocationID = table.Column<int>(type: "int", nullable: false),
                    ClinicLocationID = table.Column<int>(type: "int", nullable: false),
                    PharmcyID = table.Column<int>(type: "int", nullable: false),
                    ClinicTypeID = table.Column<int>(type: "int", nullable: false),
                    StotreID = table.Column<int>(type: "int", nullable: false),
                    PaidDealingID = table.Column<int>(type: "int", nullable: false),
                    SessionID = table.Column<int>(type: "int", nullable: false),
                    WalkInPatientOnly = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FutureBooking = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EndOfDay = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Creationdate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NameRu = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicSetups_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ClinicTypes_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicTypes_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ClottingTimeDetailss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MasterId = table.Column<int>(type: "int", nullable: false),
                    TestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TestTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClottingTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Dose = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdjustedDose = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClottingTimeDetailss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClottingTimeMasters_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    PatientIPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdmitDoctorID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClottingTimeMasters_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComplainSetups_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ComplainDescArabic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComplainDescEnglish = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplainSetups_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsultaionEnums_InPatient",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentID = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConsType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultaionEnums_InPatient", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Consultation_Requests_InPatient",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    Urgency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Request_type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FromDr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ToDr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Request_Response = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Response_comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IPOPNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    RequestTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OtherType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransferBedNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MainReqType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FromNurseID_Ready = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ToNurseID_Ready = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FromNurseID_Left = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ToNurseID_Arrived = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FromReady_Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToReady_Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FromLeft_Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToArrive_Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NewEscortBedNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HandoverStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CancelRequest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consultation_Requests_InPatient", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ConsultationSettings_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SpecilaityGroupMasterId = table.Column<int>(type: "int", nullable: false),
                    IsClinicPrice = table.Column<bool>(type: "bit", nullable: false),
                    IsSpecilaityPrice = table.Column<bool>(type: "bit", nullable: false),
                    ClinicId = table.Column<int>(type: "int", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    SpecialtyPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultationSettings_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContainerTypes_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContainerTypes_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContraDrugss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContraDrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContraDrugss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoronaryInterventions_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RiskFactors = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Operators = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Procedures = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LAD = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RCA = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Equipments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoronaryInterventions_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CRTPImplantationReports_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateOfImplantation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Operator = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ys = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    IndicationForPermanent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ECGBeforePacemaker = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreMedication = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IVAntibiotic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocalAnaesthesia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VenousAccess = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PocketSite = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeadInsertionSite = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AtrialLead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RightVentricualLead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeftVentricualLead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BatteryInsertionSite = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BatteryFixation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WoundClosure = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Subcutaneous = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Skin = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocalAntibiotic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PWave = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RWave = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RightVentricularPacingThreshold = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeftVentricularPacingThreshold = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImpedanceAtrialLead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImpedanceRightVentricularPacingThreshold = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImpedanceLeftVentricularPacingThreshold = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RVLeadManufacturer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LVLeadManufacturer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AtrialLeadManufacturer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BatteryDataManufacturer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LVLeadModel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RVLeadModel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AtrialLeadModel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BatteryDataModel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LVLeadSerialNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RVLeadSerialNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AtrialLeadSerialNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BatteryDataSerialNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastProgrammedData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ECGAfterImplantation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Complications = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Antibiotics = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AppointmentsForPacemaker = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CRTPImplantationReports_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CurrentMedications_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    PrescriptionNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DrugID = table.Column<int>(type: "int", nullable: false),
                    Dosage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DosageUnitID = table.Column<int>(type: "int", nullable: false),
                    FrequencyID = table.Column<int>(type: "int", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PeriodTypeID = table.Column<int>(type: "int", nullable: false),
                    DoseQTY = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    AssesmentID = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContinueDrugDuringAdmission = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NurseAdmissionAssessmentId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrentMedications_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyCloseDates_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CloseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCloseDates_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "DBloodBagss_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DonorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonationType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    donationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiryDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BloodGroup_ID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Size = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BagType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CampaignID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutSourceID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirstResult = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecondResult = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FinalResult = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BagDeal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VoucherID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScrapReson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScrapDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Serial = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Refrigerator = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Volume = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TubeNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BladderType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuestionnaireNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonationReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonationPeriod = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Receipt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PurchasePrice = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PatientId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NumberTo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntryCodes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdjustmentEntryID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdjustmentEntryState = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DBloodBagss_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DCAFs_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Patient = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtEligiberNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtDurationOfIllness = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtSignificatSigns = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtDiagnoses = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtPrimary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtSecondary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtOtherConditions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtOther = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtHow = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtWhen = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtWhere = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtCompleted_Coded_By = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtSignature = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtline_of_managment_When_applicable = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtEsstimated = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtAdmissionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    txtPhysician = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    txtRelationship = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtRelationshipSignature = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtRelationsDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChxPlanType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChxNewVisit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChxFollowUp = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChxRegularDentalTreatment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChxDentalCleaning = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChxRTA = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChxWorkRelated = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChxmanagmentY = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    OPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DCAFs_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryDetailss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    IdentificationName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeliveryModeID = table.Column<int>(type: "int", nullable: false),
                    SexId = table.Column<int>(type: "int", nullable: false),
                    DateofBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TimeofBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeliveryTerm = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sterilization = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BabyWardID = table.Column<int>(type: "int", nullable: false),
                    BedID = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveryId = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryDetailss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Deliverys_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    ObstetricianID = table.Column<int>(type: "int", nullable: false),
                    PediatricianID = table.Column<int>(type: "int", nullable: false),
                    DeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MidWifeID = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OEHID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliverys_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryTerms_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DeliveryTermName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryTerms_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryTypes_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryTypes_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DespatchingDetailss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TransferRequesEntrytId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubstoreBatchId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecievedQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcceptedQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DespatchingDetailss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DestinationOfPatients_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DestinationName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DestinationOfPatients_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DestroyingExpiryItemsDetailss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DestroyingExpiryItemsHeaderId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiryDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubStoreBatchId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DestroyQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitPrice = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SetEntryCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DestroyingExpiryItemsDetailss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DestroyingExpiryItemsHeaders_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SubStoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DestroyNO = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DestroyingExpiryItemsHeaders_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DevicesDefinations_Radiology",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Serial = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeviceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Emergency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ISActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    CommSetting = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CommPort = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FormName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IPAddres = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    investigationgroup = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevicesDefinations_Radiology", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "DeviceServicess_Radiology",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Active = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HostCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeviceValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceServicess_Radiology", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceTechnicians_Radiology",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TechnicianID = table.Column<int>(type: "int", nullable: false),
                    Device = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTechnicians_Radiology", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "DevicsSchedules_Radiology",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceID = table.Column<int>(type: "int", nullable: false),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    HolidayID = table.Column<int>(type: "int", nullable: false),
                    WorkFromTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WorkToTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WorkFromDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WorkToDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OffFromTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OffToTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OffFromDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OffToDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StatusEnum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevicsSchedules_Radiology", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosisAnswerss_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiagnosisGroupID = table.Column<int>(type: "int", nullable: false),
                    DiagnosisAnswerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DiagnosisQuestionID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosisAnswerss_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosisGroupss_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiagnosisGroupName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CostCenterID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosisGroupss_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosisMains_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEng = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameArab = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosisMains_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosisQuestions_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CostCenterID = table.Column<int>(type: "int", nullable: false),
                    DiagnosisGroupID = table.Column<int>(type: "int", nullable: false),
                    DiagnosisQuestionName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrderQuest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosisQuestions_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiagnosisSubs_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEng = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameArab = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MainId = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosisSubs_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DirectAddationDetailss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    HeaderID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitConversionFactorId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Condition = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectAddationDetailss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DirectAddationHeaders_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OperationNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OperationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubStockID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BorrowingID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectAddationHeaders_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DirectRad_Diagnosiss_Radiology",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvestegationReqID = table.Column<int>(type: "int", nullable: false),
                    ICDCodeID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectRad_Diagnosiss_Radiology", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "DirectSubstractDetailss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    HeaderID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubStockBatchID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitConversionFactorId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectSubstractDetailss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DirectSubstractHeaders_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OperationNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OperationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubStockID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BorrowingID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectSubstractHeaders_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Discharge_Orders_InPatient",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdmitPatientID = table.Column<int>(type: "int", nullable: false),
                    OrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OrderTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReasonID = table.Column<int>(type: "int", nullable: false),
                    FileURL = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HomeMedicine = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PFE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Discharge_Doc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    ReAdmit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Discharge_Orders_InPatient", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "DischargeReasons_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnglishName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArabicName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Active = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DischargeReasons_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "DischargeSummaryICDs_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    DischargeSummaryID = table.Column<int>(type: "int", nullable: false),
                    ICDCodeID = table.Column<int>(type: "int", nullable: false),
                    IsFinalDiagnosis = table.Column<bool>(type: "bit", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DischargeSummaryICDs_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DischargeSummarys_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    DestinationID = table.Column<int>(type: "int", nullable: false),
                    DischargeTypeID = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DischargeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DischargeTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Approvedate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApproveTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApproveDoctorID = table.Column<int>(type: "int", nullable: false),
                    PresentingComplaint = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    DestinationText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    CoMorbidConditions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SignificantPhysicalandOtherFindings = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IntrahospitalCourse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    INVRDId = table.Column<int>(type: "int", nullable: false),
                    DiagnosticandTherapeuticProceduresPerformed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SignificantMedicationsandOtherTreatments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PresDId = table.Column<int>(type: "int", nullable: false),
                    FollowupInstructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NextbookedOPDVisitDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    whentoseekEmergencyMedicalCare = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DischargeSummarys_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DischargeTypes_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DischargeTypeDescArabic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DischargeTypeDescEnglish = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DischargeTypes_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DischargeTypes_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HospitalCase = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeath = table.Column<bool>(type: "bit", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DischargeTypes_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Disease_InfectionTypess_OutPatient",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiseaseID = table.Column<int>(type: "int", nullable: false),
                    InfectionTypeID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Disease_InfectionTypess_OutPatient", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "DiseaseCategorys_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiseaseCategoryName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiseaseCategorys_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiseaseMasters_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiseaseCategoryID = table.Column<int>(type: "int", nullable: false),
                    DiseaseName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    IsChronic = table.Column<bool>(type: "bit", nullable: false),
                    IsCommunicable = table.Column<bool>(type: "bit", nullable: false),
                    ISHIbB = table.Column<bool>(type: "bit", nullable: false),
                    DiseaseNameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiseaseMasters_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DispenseBloodBagss_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PatientID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BloodGroup = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BagID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BagSource = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChequeDonor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChequeLocation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChequeExpiryDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DispenseDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BagCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OP_IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DoctorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ServiceID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReqBloodGroup = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReqProduct = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceiverName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DepartmentAndWard = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvestDtlID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    transform = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispenseBloodBagss_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DispenseDrugsDetailss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PatientID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IPnumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OPnumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceiptNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceiptDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiryDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BatchNO = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Instructions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispenseDrugsDetailss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DispenseDrugsHeaders_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PatientID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IPnumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OPnumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrescriptionNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceiptNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceiptDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DoctorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SponserID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConvertToselF = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrescriptionDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentTypeID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispenseDrugsHeaders_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Dobutamine_Stress_Echocardiography_Commentss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Dobutamine_Stress_EchocardiographyID = table.Column<int>(type: "int", nullable: false),
                    Dose = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HR = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dobutamine_Stress_Echocardiography_Commentss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Dobutamine_Stress_Echocardiography_MGMs_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Dobutamine_Stress_EchocardiographyID = table.Column<int>(type: "int", nullable: false),
                    MGM = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Basal = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mid = table.Column<int>(type: "int", nullable: false),
                    Apical = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dobutamine_Stress_Echocardiography_MGMs_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Dobutamine_Stress_Echocardiographys_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PID = table.Column<int>(type: "int", nullable: false),
                    DateofExam = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InterPretations = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    conclustion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TimeofExam = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dobutamine_Stress_Echocardiographys_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DoctorGeneralSchedules_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayID = table.Column<int>(type: "int", nullable: false),
                    SubSpecialityID = table.Column<int>(type: "int", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorGeneralSchedules_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "DoctorInstructionClassifications_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DescriptionAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorInstructionClassifications_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "DoctorNotes_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MedicalObservationID = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NoteDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorNotes_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DoctorRemarksAndCommentss_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    OPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    DoctorRemark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPatientComment = table.Column<bool>(type: "bit", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorRemarksAndCommentss_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "DoctorSchedules_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeFrom = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeTo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorSchedules_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DoctorsPersonalLists_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    doctorId = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorsPersonalLists_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DoctorTransfers_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    FromDepartmentID = table.Column<int>(type: "int", nullable: false),
                    ToDepartmentID = table.Column<int>(type: "int", nullable: false),
                    FromDoctorID = table.Column<int>(type: "int", nullable: false),
                    ToDoctorID = table.Column<int>(type: "int", nullable: false),
                    TransferDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Transfertime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    transferReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorTransfers_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DonationInfos_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DonationTypeCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonationTypeName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonationTypeNameEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonationInfos_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DonationInvestgationMasters_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    InvestgationID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonationInvestgationMasters_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DonationRestrictions_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    QuestionnaireNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NumberTo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonationReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BladderTypes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpirationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonationPeriod = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BloodProductID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PateietCount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RestrictionCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DBloodBank = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonationRestrictions_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DonorQuestionnaireDetailss_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    QuestionnaireNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuestionnaireDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuestionnaireResult = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuestionsURL = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Pulse_BPH = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Urine = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Extremity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Glucose = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Temp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TempMode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RespRate_MIN = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Positions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Bowel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MEWs = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PainScore = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Systole_MM_Hg = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LevelOfConsciouseness = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OxygenSaturation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Diastole_MM_Hg = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    O2Amount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FallRisk = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Height = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Wight = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BloodPressure_SYSTOLIC = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BloodPressure_DIASTOLIC = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CVP = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonorRegistrationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonorQuestionnaireDetailss_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DonorRegistrations_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FirstNameLatin = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecoendNameLatin = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ThirdNameLatin = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastNameLatin = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirstNameLocal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecoendNameLocal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ThirdNameLocal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastNameLocal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaritalStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Birthdate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Age = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NationalID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Telephone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BloodGroupID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ISactive = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClincalHistory = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RelativeTelephone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateFrom = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateTo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Occupation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastDonationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonorRegistrations_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DonorVitalSignss_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DonorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Temp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Pulse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    diaslotic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    systolic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Weight = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Height = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Unfit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonorVitalSignss_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DosageFrequencyLinks_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DosageId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FrequencyId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DosageFrequencyLinks_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DosageSessions_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DosageSessionName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DosageSessionTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DosageSessions_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DosageUniteForms_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DosageUniteForms_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Drug_manufacturerss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drug_manufacturerss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrugAdminModes_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DrugId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdminModeId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugAdminModes_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrugAlternatives_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MainDrug = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReplacedDrug = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugAlternatives_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrugCharts_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    IPNO = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DrugID = table.Column<int>(type: "int", nullable: false),
                    Dosage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DrugChartDateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DosageUnitID = table.Column<int>(type: "int", nullable: false),
                    IsChecked = table.Column<bool>(type: "bit", nullable: false),
                    PrescrirtionDetailsId = table.Column<int>(type: "int", nullable: false),
                    WardPharmacyDetailsId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugCharts_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrugClassDets_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DrugClassID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugClassDets_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrugClassificationPeroids_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DrugClassificationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Peroid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugClassificationPeroids_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrugClassifications_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugClassifications_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrugClasss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClassName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClassNameAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Serial = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugClasss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrugFormss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FormName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FormNameArabic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugFormss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrugGroupMasters_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugGroupMasters_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrugPreparationTemplates_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PreparationDrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubDrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugPreparationTemplates_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Drugss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DrugCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BrandID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugFormID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugTypeID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BaseUnitID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DosageUnitID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugStrength = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StorageConditions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Instractions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClassificationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OPSellingPrice = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IPSellingPrice = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsCombination = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdminModeId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitTemplateID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BarCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Batch = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CostPrice = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocalBarCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ServiceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SFDA = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsProhibited = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    manufacturer_ID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GenericName_ID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugNameAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HighRisk = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NphiesCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Istaxable = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BrandType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AllergyTestNeeded = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drugss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrugsWithInfusionRateDetails_StatusHistorys_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DrugsWithInfusionRateDetailsID = table.Column<int>(type: "int", nullable: false),
                    StatusID = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugsWithInfusionRateDetails_StatusHistorys_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrugsWithInfusionRateDetailsDetailss_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DrugsWithInfusionRateDetailsID = table.Column<int>(type: "int", nullable: false),
                    DrugID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GenericID = table.Column<int>(type: "int", nullable: false),
                    TotalDose = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Volume = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TemplateId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugsWithInfusionRateDetailsDetailss_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrugsWithInfusionRateDetailss_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DrugsWithInfusionRateID = table.Column<int>(type: "int", nullable: false),
                    PrescriptionNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    DrugID = table.Column<int>(type: "int", nullable: false),
                    DosageUnitID = table.Column<int>(type: "int", nullable: false),
                    Dosage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FrequencyID = table.Column<int>(type: "int", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoseQty = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TypeId = table.Column<int>(type: "int", nullable: false),
                    AdminModeId = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContinueDrugDuringAdmission = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    GenericID = table.Column<int>(type: "int", nullable: false),
                    SpecialInstruction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PRN_Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PRN_MaxFreq = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IFNeeded = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Strength = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClinicalPharmacy_Accept = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cannula = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TemplateId = table.Column<int>(type: "int", nullable: false),
                    MainSolution = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Volume = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DosageBaseUnit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegularContinous = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InfusionRate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InfusionRatePeriod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdministrationType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugsWithInfusionRateDetailss_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrugsWithInfusionRateHeaders_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    PrescriptionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PrescriptionNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    OPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AlertName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comorbidities = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugsWithInfusionRateHeaders_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrugTemplates_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DrugId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GenericId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dosage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DosageBaseUnitId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DosageQnt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RouteId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FrequId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Duration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DurationTypeId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdminSiteId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugStengthId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugFormId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugTemplates_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrugTypess_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TypeNameAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TypeName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugTypess_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ECGReports_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ECGDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ECGTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    PatientIPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorId = table.Column<int>(type: "int", nullable: false),
                    Rhythm = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HeartRate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Axis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    pwaveAmplitude = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    pwaveDuration = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PRInterval = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QRSComplexMorphology = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QRSComplexDuration = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    STSegment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TWave = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Interpretion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    userid = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ECGReports_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyDetailss_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PatientID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VisiteDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DischargeDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DischargeStatuse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BranchId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmergencyUnitId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    wardId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BedId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccommodationType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyDetailss_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyOrganizations_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DescriptionArabic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescriptionEnglish = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MainOrganization = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyOrganizations_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmergencySchedules_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DayID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EndTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubSpecialityID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DoctorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Creationdate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModificationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SessionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencySchedules_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyUnit_Historys_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EmergencyUnitID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangeDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DoctorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ERCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItemID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    other = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BranchID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyUnit_Historys_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyUnits_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ArrivalTypeID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComplainID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RiskID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PatientStatusID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DoctorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentTypeID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferralDoctorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferralUnitID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CasePriortyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PatientID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Date = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ERcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SeenDateTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComplainDuration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SpecialityID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Categoryitem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItemID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CategoryitemOthers = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BranchID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EncounterType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyUnits_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmergencyVisits_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PatientID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VistNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OPCase = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModeOfArrival = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccompaniedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RelativesNotifiedID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PriorityID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateOfArrival = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeOfArrival = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DepartmentID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferralDepartmentID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DoctorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferralDoctorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferralClinic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CheifComplaint = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OPNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WordToAdmintID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BedNoID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MedicoLegalDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InsuranceAuthorizationCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnteredBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyVisits_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EscortDetailss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EscortId = table.Column<int>(type: "int", nullable: false),
                    IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EscortName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdentityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EscortNationalId = table.Column<int>(type: "int", nullable: false),
                    GenderTypeId = table.Column<int>(type: "int", nullable: false),
                    EscortAge = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RelativeId = table.Column<int>(type: "int", nullable: false),
                    EscortNeedBed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BedId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Payment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovalOnTerms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovalOnTermsFileUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscortDetailss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EscortEnterances_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EscortId = table.Column<int>(type: "int", nullable: false),
                    EnteranceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnteranceTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscortEnterances_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Escorts_InPatient",
                columns: table => new
                {
                    EscortId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Escorts_InPatient", x => x.EscortId);
                });

            migrationBuilder.CreateTable(
                name: "EstimatedmissionDetailss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EstimatedmissionID = table.Column<int>(type: "int", nullable: false),
                    ServiceID = table.Column<int>(type: "int", nullable: false),
                    Units = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPrecentage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstimatedmissionDetailss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Estimatedmissions_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SurgeryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdmissionRequestId = table.Column<int>(type: "int", nullable: false),
                    EstimateNO = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CostEstimateStatus = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    IsLinked = table.Column<bool>(type: "bit", nullable: false),
                    OPnumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    AdmissionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Days = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RoomTypeID = table.Column<int>(type: "int", nullable: false),
                    EstimatedmissionStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Estimatedmissions_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExamDeliverys_Radiology",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccessionNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    ISReport = table.Column<bool>(type: "bit", nullable: false),
                    ISCD = table.Column<bool>(type: "bit", nullable: false),
                    ISFilm = table.Column<bool>(type: "bit", nullable: false),
                    DeliveryTo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamDeliverys_Radiology", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ExamRequests_Radiology",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccessionNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExamDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeviceID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExamID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FlagsID = table.Column<int>(type: "int", nullable: false),
                    ReservedDateFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReservedDateTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReservedTimeFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReservedTimeTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TimeStartExam = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TimeEndExam = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReportNotes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamRequests_Radiology", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Examss_Radiology",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LatinName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocalName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ISActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeviceID = table.Column<int>(type: "int", nullable: false),
                    Minutes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InvestegationGroupEnum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ISpregnant = table.Column<bool>(type: "bit", nullable: false),
                    ISDiabetic = table.Column<bool>(type: "bit", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Examss_Radiology", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseMasters_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExpenseName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccountID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseMasters_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalAgenciess_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HospitalId = table.Column<int>(type: "int", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    NationalityId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalAgenciess_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalFacilityDetailss_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExternalFacilityID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BloodProductID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiryDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BagNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    a1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalFacilityDetailss_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalFacilityHeaders_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FacilityID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActionDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReciptNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Actiontype = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PatientID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExternalFacilityCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalFacilityHeaders_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EyesightMeasurements_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MedicalObservationID = table.Column<int>(type: "int", nullable: false),
                    RSPHDiest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RSPHNear = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RCYLDiest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RCYLNear = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RAXDiest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RAXNear = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LSPHDiest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LSPHNear = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LCYLDiest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LCYLNear = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LAXDiest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LAXNear = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RReading = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LReading = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    prisimbase = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Prisimamount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EyesightMeasurements_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FacilityMasters_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FacilityLatinName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FacilityLocalName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FacilityCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EndDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ISActive = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityMasters_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Farmss_Laboratory",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FarmTypeId = table.Column<int>(type: "int", nullable: false),
                    ServiceID = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Farmss_Laboratory", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "FarmTypes_Laboratory",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Active = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmTypes_Laboratory", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "FastEmergancys_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ComeFrom = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    patientID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DoctorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentMethodID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ERCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FastEmergancys_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FindingFlags_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FindingFlags_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Findings_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FindingFlagId = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Findings_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FlagSettingss_Radiology",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LatinName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocalName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ToolTip = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FlagIcon = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlagSettingss_Radiology", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Floorss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FloorCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BuildingID = table.Column<int>(type: "int", nullable: false),
                    FloorStatusId = table.Column<int>(type: "int", nullable: false),
                    FloorSequence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Floorss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Frequenciess_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Frequenciess_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FrequencyMasters_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnglishNameDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArabicNameDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FrequencyType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrequencyMasters_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "GeneralNursingCarePlan_TagsValuess_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Value1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneralHeaderId = table.Column<int>(type: "int", nullable: false),
                    PageTagsId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneralNursingCarePlan_TagsValuess_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GeneralNursingCarePlanHeaders_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NurseId = table.Column<int>(type: "int", nullable: false),
                    CareModeId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneralNursingCarePlanHeaders_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GeneralSetups_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NumOfDays = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NumOfFollowUp = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneralSetups_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "GenericNamesReplacements_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MainGeneric = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReplacedGeneric = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenericNamesReplacements_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GenericNamess_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GenericNamesAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GenericNamesValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenericNamess_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GitImagess_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MedicalObservation_GitID = table.Column<int>(type: "int", nullable: false),
                    TapID = table.Column<int>(type: "int", nullable: false),
                    ImageName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitImagess_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceivedNoteDetailss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GRNId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcceptQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderedQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BonusQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PriceLC = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AmountLC = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscountAmount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscountPrecint = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PriceFC = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitConversionFactorId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AmountFC = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SellPrice = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LPODetID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceivedNoteDetailss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceivedNoteExpensess_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExpensesID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GRNId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceivedNoteExpensess_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GoodsReceivedNoteHeaders_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LPOId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GRNNO = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GRNDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentTermsID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryNoteNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryNoteDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvoiceNO = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvoiceDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalExpensesAmount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotallandedAmount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalGRNValueFC = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalGRNValueLC = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GRNPreparedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ISOpeneingStock = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TaxValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransType_ID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Direct = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OpeningBalance = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    issueToDepartment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StockID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SetEntryCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Holdingtax = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VatValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsReceivedNoteHeaders_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GRN_LPOss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GRNID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LPOID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GRN_LPOss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GRNDetailsBatchesTransactionss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GRNDetailsID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubStoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceivedQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcceptedQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExcessQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Bonus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvailableQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssueBatchID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BatchId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GRNDetailsBatchesTransactionss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HeadUpTiltDiagnosiss_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HeadUpTiltID = table.Column<int>(type: "int", nullable: false),
                    ICDCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeadUpTiltDiagnosiss_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HeadUpTilts_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    History = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ECG = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Echocardiography = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StressECG = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcedureStage1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcedureStage2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Results = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DuringStage1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DuringStage2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Recomendation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeadUpTilts_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "HeadUpTiltTableICDCodess_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HeadUpTiltTableId = table.Column<int>(type: "int", nullable: false),
                    ICDCodeId = table.Column<int>(type: "int", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeadUpTiltTableICDCodess_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HeadUpTiltTables_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReferredBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TiltDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    History = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Investigations = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Medications = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcedureStage1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcedureStage2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcedureOther = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RestingHR = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RestingBPSyst = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RestingBPDiast = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stage1HR = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stage1BPSyst = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stage1BPDiast = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stage1ECG = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stage1Symptoms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stage2HR = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stage2BPSyst = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stage2BPDiast = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stage2ECG = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stage2Symptoms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Diagnosis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Recommendation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeadUpTiltTables_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HebaTests_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Birthdate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HebaTests_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Holters_InPatient",
                columns: table => new
                {
                    HolterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecorderSerial = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Duration = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    HolterOrder = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OverreadingPhysician = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReferringPhysician = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrderingPhysician = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HookUpTechnician = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AnalyzingTechnician = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IndicationDiagnosis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Medications = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QRSComplexes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VentricularBeats = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupraventricularBeats = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JunctionalBeats = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalTimeClassified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MinHeartRate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinHeartRateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Average = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaxHeartRate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaxHeartRateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BeatsInTachycardia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SecondsMaxRR = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeatsInBradycardia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SecondsMaxRRDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Isolated = table.Column<bool>(type: "bit", nullable: false),
                    Couplets = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BigeminalCycles = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RunsTotaling = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Beats = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SuprIsolated = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SuprCouplets = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SuprBigeminalCycles = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SuprRunTotaling = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SuprBeats = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeatsLongest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BLRunBpm = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BLRunBpmDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BeatsFastest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BFRunBpm = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BFRunBpmDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Interpretation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holters_InPatient", x => x.HolterId);
                });

            migrationBuilder.CreateTable(
                name: "ICDCodes_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    ISInPatient = table.Column<bool>(type: "bit", nullable: false),
                    ISOutPatient = table.Column<bool>(type: "bit", nullable: false),
                    OPNO = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OPDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ICDCodeValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ICDDesc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ICDCodes_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InfectiousDiseaseScreenings_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    Q1_TravelOutsideUS = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Q1_TravelLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Q1_HouseholdTravel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Q1_HouseholdTravelLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Q2_CloseContact = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Q3_Fever = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Q4_CoughShortnessBreathSoreThroat = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Q5_VomitingDiarrhea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Q6_Rash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Q7_Fever = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Q8_SevereHeadache = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Q9_DiarrheaVomitingAbdominalPain = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Q10_RespiratoryIllness = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Q11_NewWorseningCough = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Q12_SoreThroat = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Q13_ShortnessOfBreath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Q14_LossOfSmell = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Q15_LossOfTaste = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Q16_UnexplainedHemorrhage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Q17_FatigueMuscleSkinChanges = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttachmentName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttachmentPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Identify_PutMaskGloves = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Identify_GivePatientMask = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Identify_ContactSupervisor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Isolate_SingleRoom = table.Column<bool>(type: "bit", nullable: false),
                    Isolate_SeparatePatient6Feet = table.Column<bool>(type: "bit", nullable: false),
                    Isolate_PPEEscort = table.Column<bool>(type: "bit", nullable: false),
                    Isolate_UrinalBedpan = table.Column<bool>(type: "bit", nullable: false),
                    Isolate_ProviderReview = table.Column<bool>(type: "bit", nullable: false),
                    Inform_ContactInfectionPrevention = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Inform_RiskAssessment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Inform_DoNotMovePatient = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InfectiousDiseaseScreenings_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InfusionRate_Orders_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    IPNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InfusionRate_Orders_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "InitialDischarges_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    DischargeTypeID = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InitiateDischargeReasonID = table.Column<int>(type: "int", nullable: false),
                    InitiateDischargeTypeID = table.Column<int>(type: "int", nullable: false),
                    Discharged = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InitialDischarges_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "InitiateDischargeTypess_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnglishName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArabicName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InitiateDischargeTypess_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "InpatientSettings_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdmitServiceID = table.Column<int>(type: "int", nullable: false),
                    AdmitPaymentWaitingHours = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    ReDepositBalance = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InitialDeposit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    OperationTimeslots = table.Column<DateTime>(type: "datetime2", nullable: false),
                    paybeforetakenaservice = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayDeposit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Deposit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Min = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Durationindays = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NumberofFlowUp = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DepositPercentage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConsultationServiceID = table.Column<int>(type: "int", nullable: false),
                    ShowSurgicalProceduralConsentForm = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    refrealServiceID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InpatientSettings_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "InternalConsumptionEntrys_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    InternalConsumptionID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubStoreBatchId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Date = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QtyConsumed = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubStoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalConsumptionEntrys_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InternalConsumptions_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    InternalConsumptionNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Date = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubStoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalConsumptions_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryAdjustmentEntrys_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PhysiacalAdjID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BloodGroupId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QTYinHand = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QTYAdj = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QtYDifference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryAdjustmentEntrys_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryAdjustments_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StockID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReasonAdjusyId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Date = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhysiacalNO = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryAdjustments_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvestigationRequestDetailss_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvestigationRequestId = table.Column<int>(type: "int", nullable: false),
                    QTY = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    ApprovalId = table.Column<int>(type: "int", nullable: false),
                    Isoverride = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    PatientOrderDetailsID = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartNurseID = table.Column<int>(type: "int", nullable: false),
                    EndNurseID = table.Column<int>(type: "int", nullable: false),
                    Done = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cancelled = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RayLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsNoDeposit = table.Column<bool>(type: "bit", nullable: false),
                    PaymentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CancelledBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CancelledReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    insuranceId = table.Column<int>(type: "int", nullable: false),
                    prescDtlID = table.Column<int>(type: "int", nullable: false),
                    toothNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    opticaltype = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestigationRequestDetailss_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvestigationRequestHeaders_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OP_IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WardID = table.Column<int>(type: "int", nullable: false),
                    IsPregnant = table.Column<bool>(type: "bit", nullable: false),
                    PregnantWeeks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClinicalDetails = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReqDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReqStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SurgeryID = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    PatientOrderMasterPriority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    selfmotivated = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    referraltype = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    organizationdoctor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    referredDoctor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestigationRequestHeaders_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IssueRequestdetailss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IssueRequestId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitConversionID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModificationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueRequestdetailss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IssueRequests_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RequestNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MainSubstoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubSubstoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModificationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmpRequestID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueRequests_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IssuetoDepartmentEntrys_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IssuetoDepartmentId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StockBatchId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Date = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitConversionID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssueQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalReturnQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuetoDepartmentEntrys_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IssuetoDepartmentReturnEntrys_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IssuetoDepartmentReturnID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssueToDepartementEntryID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssueReturnQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuetoDepartmentReturnEntrys_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IssuetoDepartmentReturns_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StockID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssueReturnNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssueReturnDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssueNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssueDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SetEntryCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuetoDepartmentReturns_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IssuetoDepartments_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MainStockID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssueNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssueDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StockID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssueRequestID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SetEntryCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuetoDepartments_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IssuetoInventoryEntrys_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IssuetoInventorytId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DBloodBagsId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuetoInventoryEntrys_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IssuetoInventorys_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MainStockID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssueNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssueDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StockID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssueRequestID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SetEntryCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuetoInventorys_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabAnalyzerss_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnalyzerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Isactive = table.Column<bool>(type: "bit", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabAnalyzerss_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LaboratorySettings_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpID = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaboratorySettings_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabsDevicess_Laboratory",
                columns: table => new
                {
                    DeviceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LabCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabsDevicess_Laboratory", x => x.DeviceId);
                });

            migrationBuilder.CreateTable(
                name: "LabServiceItemLinkMasters_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    UnitUsed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Wastage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WastageCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabServiceItemLinkMasters_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabServicesPeriods_Laboratory",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceID = table.Column<int>(type: "int", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabServicesPeriods_Laboratory", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Labss_Laboratory",
                columns: table => new
                {
                    LabAdminId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LabCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LabName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    labType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    LabNameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Labss_Laboratory", x => x.LabAdminId);
                });

            migrationBuilder.CreateTable(
                name: "LabsStoress_Laboratory",
                columns: table => new
                {
                    StoreId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LabCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabsStoress_Laboratory", x => x.StoreId);
                });

            migrationBuilder.CreateTable(
                name: "LabsTechnicanss_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LabCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TechnicanId = table.Column<int>(type: "int", nullable: false),
                    DayId = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabsTechnicanss_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabTestItems_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    ResultValueId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabTestItems_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabUnits_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabUnits_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegalStatusMasters_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalStatusMasters_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LinkCostEstimations_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    IsLinked = table.Column<bool>(type: "bit", nullable: false),
                    EstimationNO = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstimationAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EstimationDays = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkCostEstimations_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocalPurchaseCancelations_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CancelDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CancelNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Narration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalPurchaseCancelations_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocalPurchaseOrderDetailss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LPOID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderedQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BonusQuantity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscountAmount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscountPrecint = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitConversionFactorId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcceptQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BonusQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AmountFC = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PriceFC = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SellPrice = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrevAcceptQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GenericID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalPurchaseOrderDetailss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocalPurchaseOrderExpensess_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExpensesID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LPOID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalPurchaseOrderExpensess_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocalPurchaseOrderHeaders_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SubStoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PODate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrencyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PONumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShippingModeID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShippingTermsID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryTimeType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentModeID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentTermsID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalExpenses = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalAmount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NetAmount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscountAmount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscountPrecint = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsEmergency = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocalPurchaseCancelationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnifiedPurchCommission = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TaxValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Direct = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    openingBalance = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    issueToDepartment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PurchRequestID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalPurchaseOrderHeaders_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocationDefinations_Radiology",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LatinName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocalName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ISActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NumberOFPatient = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationDefinations_Radiology", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "LPOApprovingAuthorityHeaders_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LevelName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LevelAmount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LPOApprovingAuthorityHeaders_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LPOApprovingAuthoritys_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LevelID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LPOApprovingAuthoritys_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MainSolutions_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GenericId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TemplateId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MainSolutions_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceTypess_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HospitalCase = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceTypess_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ManualGroupTransfers_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FromClinicID = table.Column<int>(type: "int", nullable: false),
                    FromDocID = table.Column<int>(type: "int", nullable: false),
                    FromSchedualeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    FromTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToClinicID = table.Column<int>(type: "int", nullable: false),
                    ToDocID = table.Column<int>(type: "int", nullable: false),
                    ToSchedualeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualGroupTransfers_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MedicalConsumablesDetailss_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    MedicalConsumablesID = table.Column<int>(type: "int", nullable: false),
                    ItemID = table.Column<int>(type: "int", nullable: false),
                    UniteID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ISIncluded = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubstoreID = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalConsumablesDetailss_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicalConsumablesHeaders_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    PatientType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestNO = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalConsumablesHeaders_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicalObservation_CCU_MICUs_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MedicalObservationID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    PrimaryConsultant = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ICUConsultant = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ICUSection = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HospitalAdmissionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FromDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ICUAdmissionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TypeOfAdmission = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProvisionalDiagnosis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalObservation_CCU_MICUs_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicalObservation_Gits_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MedicalObservationID = table.Column<int>(type: "int", nullable: false),
                    Esophagus1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stomach1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Colon1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Conclusion1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Recommendet1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Esophagus2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stomach2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Colon2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Conclusion2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Recommendet2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Enteroscopy3 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Radiology3 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Conclusion3 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Recommendet3 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Enteroscopy4 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sonosrcphagc4 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Conclusion4 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Recommendet4 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Esophagus5 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stomach5 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Duodenum5 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Conclusion5 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Recommendet5 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Indication1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Indication2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Indication3 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Indication4 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Indication5 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreMedication1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreMedication2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreMedication3 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreMedication4 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreMedication5 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    P_R1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Report1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stomach5_Fundus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stomach5_Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stomach5_Pylorous = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RFR_Indecation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RFR_Premedication = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RFR_siteofablation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RFR_Frequency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RFR_Report = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RFR_ReportFile = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LbR_Indecation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LbR_Premedication = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LbR_txtsiteofbiobsy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LbR_Typeofneedle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LbR_Report = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LbR_ReportFile = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    smallinteitie = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    duodenoscopy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    endooscopy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalObservation_Gits_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MedicalObservation_MedicationHistorys_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MedicalObservationID = table.Column<int>(type: "int", nullable: false),
                    DrugId = table.Column<int>(type: "int", nullable: false),
                    Dosage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FerquencyID = table.Column<int>(type: "int", nullable: false),
                    ContinueDrugDuringAdmission = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalObservation_MedicationHistorys_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicalObservation_Obs_Gyns_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MedicalObservationID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    GH_LMP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GH_PMP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GH_MenstrualCycle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GH_TypeofFlow = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GH_Contraception = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GH_OtherContraception = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GH_PapSmearStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GH_PapSmearDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GH_PapSmearReport = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GH_HPVVaccinationStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GH_HPVVaccinationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GH_HPVVaccinationReport = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GH_Other = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GH_USGReport = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OH_LMP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OH_EDD = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OH_EDDBYUSG = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OH_Gravida = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OH_Para = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OH_Abortion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OH_LiveBirth = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OH_FoetalDeath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OH_Cause = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OH_Other = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SP_GestationalDiabetes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SP_DiabetesMellitus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SP_Hypertension = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SP_Oligohydramnios = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SP_PlacentaPraevia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SP_MedicalHistory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SP_Asthma = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SP_SurgicalHistory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SP_InfertilityTreatment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SP_Ifyes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InjectionTetanus1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InjectionTetanus2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InjectionTetanus3 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    booster = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    others = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsGynecologicalSelected = table.Column<bool>(type: "bit", nullable: false),
                    IsObstetricHistorySelected = table.Column<bool>(type: "bit", nullable: false),
                    IsPastObstetricSelected = table.Column<bool>(type: "bit", nullable: false),
                    IsSignificantHistorySelected = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalObservation_Obs_Gyns_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicalObservation_Paediatricss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MedicalObservationID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalObservation_Paediatricss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicalObservation_PastHistorys_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    POA = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    No = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sex = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Weight = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Age = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MedicalObservationobsid = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalObservation_PastHistorys_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicalObservation_Plastics_InPatient",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MedicalObservationID = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalObservation_Plastics_InPatient", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "MedicalObservation_TagsValuess_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Value1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value3 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value4 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MedicalObservationId = table.Column<int>(type: "int", nullable: false),
                    PageTagsId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalObservation_TagsValuess_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicalObservationDentals_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MedicalObservationID = table.Column<int>(type: "int", nullable: false),
                    ToothNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ServiceID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    insuranceId = table.Column<int>(type: "int", nullable: false),
                    MissingToothReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalObservationDentals_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicalObservationDepartmentDetailss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BgImgUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SerializedDataObject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MedicalObservationID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalObservationDepartmentDetailss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicalObservationICDCodess_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MedicalObservationID = table.Column<int>(type: "int", nullable: false),
                    ICDCodeID = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalObservationICDCodess_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicalObservationOpticals_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MedicalObservationId = table.Column<int>(type: "int", nullable: false),
                    ServiceID = table.Column<int>(type: "int", nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LensTypeID = table.Column<int>(type: "int", nullable: false),
                    LensTypeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    insuranceId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalObservationOpticals_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicalObservations_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    PateintType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MedicalObservationStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IP_OP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    MedicalObservationNO = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Complaints = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HistoryOFPresentIllness = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HospitalizationHistory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsInsignificant_HospitalizationHistory = table.Column<bool>(type: "bit", nullable: false),
                    IsInsignificant_PastMedicalHistoryDiseases = table.Column<bool>(type: "bit", nullable: false),
                    IsInsignificant_PastMedicalHistorySpecialConsiderations = table.Column<bool>(type: "bit", nullable: false),
                    IsInsignificant_FamilyHistoryDiseases = table.Column<bool>(type: "bit", nullable: false),
                    IsInsignificant_FamilyHistorySpecialConsiderations = table.Column<bool>(type: "bit", nullable: false),
                    IsInsignificant_FamilyHistoryPersonalHistory = table.Column<bool>(type: "bit", nullable: false),
                    IsInsignificant_DrugReactions = table.Column<bool>(type: "bit", nullable: false),
                    IsInsignificant_CurrMedicationHistory = table.Column<bool>(type: "bit", nullable: false),
                    FamilyHistoryNotes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pulse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsRegularPulse = table.Column<bool>(type: "bit", nullable: false),
                    BP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Height = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Weight = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TempF = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TempC = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BMI = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpO2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GCS = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    toothNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsInsignificant_NAD = table.Column<bool>(type: "bit", nullable: false),
                    IsInsignificant_SystemReview = table.Column<bool>(type: "bit", nullable: false),
                    SystemReview = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsInsignificant_LocalExamination = table.Column<bool>(type: "bit", nullable: false),
                    LocalExamination = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsInsignificant_Functional = table.Column<bool>(type: "bit", nullable: false),
                    IsInsignificant_Nutritional = table.Column<bool>(type: "bit", nullable: false),
                    IsInsignificant_Psychological = table.Column<bool>(type: "bit", nullable: false),
                    IsInsignificant_SocioEconomic = table.Column<bool>(type: "bit", nullable: false),
                    IsInsignificant_PainScore = table.Column<bool>(type: "bit", nullable: false),
                    IsPainManagementDone = table.Column<bool>(type: "bit", nullable: false),
                    IsInsignificant_AdditionalAssessmente = table.Column<bool>(type: "bit", nullable: false),
                    AdditionalAssessmente = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpectedLengthOfStay = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpectedLengthOfStayUnit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MainLinesOfTreatment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MeasurableGoalOfImprovement = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    SpecialityID = table.Column<int>(type: "int", nullable: false),
                    IsPregnant = table.Column<bool>(type: "bit", nullable: false),
                    IsLactation = table.Column<bool>(type: "bit", nullable: false),
                    OperationID = table.Column<int>(type: "int", nullable: false),
                    AnesthesiaDoctorId = table.Column<int>(type: "int", nullable: false),
                    OperationSetupMasterId = table.Column<int>(type: "int", nullable: false),
                    IsOperation = table.Column<bool>(type: "bit", nullable: false),
                    ActionTaken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionTakenDoctorID = table.Column<int>(type: "int", nullable: false),
                    contor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    dilated = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    hernial = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LiverPalp = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LiverRtLobe = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    speen = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    spleenLTLobe = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpleenLTLobeComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    liverRTLobeComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RTKidney = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RTKidneyComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LTKidney = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LTKidneyComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    colon = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    colonComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ascitls = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AscitlsLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AscitlsComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Intestlsound = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    lepaticrub = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    splenizrub = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LiverLTLobe = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    allert = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Oriented = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Memory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Conscious = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LNPathy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FlappiesTremor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    clubbing = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    cynofis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    jaurdice = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    pallor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Complaints_Psycho = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrenatalHistory_MaternalInfection = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrenatalHistory_ExposureToRadiation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrenatalHistory_Other = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrenatalHistory_OtherComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_TypesofDelivery = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_Complications = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_ComplicationsComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_BirthTruma = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_BirthTrumaComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_Milestones = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_MilestonesComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_Language = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_Behaviorchildhood_Temper = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_Behaviorchildhood_FeedingHabits = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_Behaviorchildhood_Pica = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_Behaviorchildhood_Tics = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_illnesschildhood_CNS = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_illnesschildhood_Infection = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_illnesschildhood_Epilepsy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_illnesschildhood_NE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_illnesschildhood_Encoporesis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_illnesschildhood_Separationanxiety = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_illnesschildhood_Neuroticdisorders = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_illnesschildhood_EatingProblems = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_Education = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_Adolescence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_OccupationalHistory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_SexualHistory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_MilitaryHistory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_MaritalHistory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Treatment_Hospitalization = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Treatment_Psychopharmacology = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Treatment_Psychopharmacology_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Treatment_Psychotheropy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Treatment_Psychotheropy_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Treatment_ECT = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Treatment_ECT_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Treatment_TMS = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Treatment_TMS_Commrnt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_Cooperative = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_Seductive = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_Defensive = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_Hostile = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_Guarded = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_Gait = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_Mannerism = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_Tics = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_Retard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_Hyperactive = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_Agitated = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_Posture = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_Grooming = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_Healthy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_Frightenend = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_Anxicty = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_older = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_Younger = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_PooreyeContact = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_FaireyeContact = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance_NeglectHyg = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance__FairHyg = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Appearance__Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Speech_Rapid = table.Column<int>(type: "int", nullable: false),
                    MentalStatus_Speech_Slow = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Speech_Pressured = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Speech_Hesitant = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Speech_Monotonous = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Speech_Loud = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Speech_Whispered = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Speech_Mumbled = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Speech_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Mood_Depressed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Mood_anxious = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Mood_angry = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Mood_guilty = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Mood_anhedonia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Mood_alexithymic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Mood_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Affect_Restricted = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Affect_Blunted = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Affect_Flat = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Affect_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_a_paucity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_a_flight = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_a_Rapid = table.Column<int>(type: "int", nullable: false),
                    MentalStatus_Thinking_a_Slow = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_a_taneouslt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_a_Question = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_a_Directed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_a_Tangentaial = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_a_Circumstantial = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_b_Delusion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_b_Obsession = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_b_Compulsion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_b_Phobias = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_b_Suicide = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_b_Homicide = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_b_Comments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_a_Comments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_a_Block = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_a_distractable = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_a_Loose = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_a_incohorent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_a_wordsalad = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_a_neologism = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_c_reading = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_c_insertion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_c_withdrawal = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_c_casting = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus_Thinking_c_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Perception_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Perception_Illusion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Perception_Depersonalization = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Perception_Derealization = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Perception_Hallucination = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_a_Conscious = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_a_Fluctuation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_a_Stupor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_a_Lethargy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_a_FugueState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_a_Coma = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_a_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_b_Time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Sensorium_b_place = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_b_Person = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_b_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_c_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_c_Subtract = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_d_RemoteMemory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_d_RecentMemory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_d_Immediate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_d_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_e_Similarities = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_e_Concrete = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_e_Abstract = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_e_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_f_Completedenial = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_f_partial = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_f_insight = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_f_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_g_judgment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sensorium_g_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NatalHistory_Behaviorchildhood_HighTerror = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Itching = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Itching_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Itching_Location = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Itching_Duration = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Itching_Freq = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Itching_Character = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Itching_PainScore = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Itching_Radiation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Itching_WhatModifiesPain = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Itching_OtherComplains = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SimilarCondition1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PastG6PD = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PastAnomaly = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PastSickle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FamilyG6PD = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FamilyAnomaly = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FamilySickle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Family_Historysimilar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PastPeptic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pastthrombosis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PastPulmonary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PastThyroid = table.Column<int>(type: "int", nullable: false),
                    PastTuberculosis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PastHereditary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FamilyCardiacDeath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FamilyCancer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FamilyHereditary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pulse_Rate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pulse_regular = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pulse_Rhysim = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pulse_Volume = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pulse_Equelity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pulse_Charachter = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pulse_PeriphPulses = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pulse_PeripheralPerfusion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pulse_JVPVolume = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pulse_JVPWave = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HernialSite = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    contourComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    sound1Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    sound2Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    sound3Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsSmoking = table.Column<bool>(type: "bit", nullable: false),
                    IsSmokingComment = table.Column<bool>(type: "bit", nullable: false),
                    IsAlcohol = table.Column<bool>(type: "bit", nullable: false),
                    IsAlcoholComment = table.Column<bool>(type: "bit", nullable: false),
                    IsDrugs = table.Column<bool>(type: "bit", nullable: false),
                    IsDrugsComment = table.Column<bool>(type: "bit", nullable: false),
                    IsDiet = table.Column<bool>(type: "bit", nullable: false),
                    IsBowels = table.Column<bool>(type: "bit", nullable: false),
                    IsDietComment = table.Column<bool>(type: "bit", nullable: false),
                    IsBowelsComment = table.Column<bool>(type: "bit", nullable: false),
                    IsHandedness = table.Column<bool>(type: "bit", nullable: false),
                    Menstrual_History_Reg = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastMenstrualPeriod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AvarageDuration = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BleedingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PresentIllness_Onset = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PresentIllness_OnsetComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Course = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Duration_Period = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Duration_PeriodType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Stomatitis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_StomatitisComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Halitosis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_HalitosisComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Dyspepsia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_DyspepsiaComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Dysphagia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_DysphagiaComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Odynophagia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_OdynophagiaComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Vomiting = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_VomitingComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Vomiting_Nausea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Vomiting_NoAttacks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Vomiting_Freq = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Vomiting_Color = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Vomiting_Association = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Diarrhoea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_DiarrhoeaComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Diarrhoea_Freq = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Diarrhoea_Color = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Diarrhoea_Blood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Diarrhoea_BloodComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Diarrhoea_Tenesmus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Diarrhoea_TenesmusComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Diarrhoea_Association = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Constipation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_ConstipationComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Constipation_Freq = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Constipation_Consistancy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Constipation_Dyschezia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Constipation_DyscheziaComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Constipation_Flatulence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Constipation_FlatulenceComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Constipation_Hema = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Constipation_HemaComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Abdomina = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_AbdominaComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Abdomina_Site = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Abdomina_Character = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Abdomina_increase = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Abdomina_Decrease = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Abdomina_Referral = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Abdomina_Association = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Haematemesis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_HaematemesisComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Haematemesis_Color = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Haematemesis_Freq = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    History_Haematemesis_Association = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsContraceptive_Method = table.Column<bool>(type: "bit", nullable: false),
                    Contraceptive_Method = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Jaundice_Yellowish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Jaundice_Yellowish_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Jaundice_DarkBrown = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Jaundice_DarkBrown_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Jaundice_SoftClay = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Jaundice_SoftClay_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Jaundice_itching = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Jaundice_itching_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Jaundice_Fever = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Jaundice_Fever_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Jaundice_AbdominalPain = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Jaundice_AbdominalPain_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BleedingTendency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BleedingTendency_Gums = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BleedingTendency_Gums_Comments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BleedingTendency_Epitaxis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BleedingTendency_Epitaxis_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BleedingTendency_Ecchymosis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BleedingTendency_Ecchymosis_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnlargedAbdomen = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnlargedAbdomen_Site = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnlargedAbdomen_Site_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnlargedAbdomen_Association = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnlargedAbdomen_Association_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Encephalopathy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Encephalopathy_FlappingTremors = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Encephalopathy_FlappingTremors_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Encephalopathy_Rhythm = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Encephalopathy_Rhythm_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Encephalopathy_conscious = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Encephalopathy_conscious_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Encephalopathy_Admitted = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Encephalopathy_Admitted_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Encephalopathy_Precipitating = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Encephalopathy_Precipitating_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Toxic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Toxic_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Toxic_Appetit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Toxic_Appetit_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Toxic_weight = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Toxic_weight_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Toxic_Fever = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Toxic_Fever_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Toxic_Sweat = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Toxic_Sweat_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Contour = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Contour2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Umbiticus_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Umbiticus_Site = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Umbiticus_Shape = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Umbiticus_Secretion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Umbiticus_Vein = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Umbiticus_Hernia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DilatedVeins = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DilatedVeins_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DilatedVeins_Site = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DilatedVeins_Direction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DilatedVeins_Burrowi = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Hernia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Hernia_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Hernia2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DivaricationRecti = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DivaricationRecti_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Skin_Hair = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Skin_Hair_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Skin_Pigmentation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Skin_Pigmentation_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Skin_Scratch = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Skin_Scratch_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Skin_Scar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Skin_Scar_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Skin_Site = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Skin_Lenght = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Skin_Healing = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Skin_Hernia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Epigastrium = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Epigastrium_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Epigastrium_Direction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pedal_Oedema = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pedal_Oedema_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Venous_Hum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RenalArteryStenosis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Venous_HumComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RenalArteryStenosis_Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HepaticRubComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SplenicRubComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RTConsistancy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RTEdge = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RTPulsation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RTSize = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RTSurface = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RTTenderness = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LTConsistancy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LTEdge = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LTPulsation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LTSize = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LTSurface = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LTTenderness = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpleenConsistancy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpleenEdge = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpleenPulsation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpleenSize = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpleenSurface = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpleenTenderness = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PastMedicalComment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MissingToothReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    snomedTxt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    snomedCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalObservations_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicationDispensingPeriod_GenericNamess_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MedicationPeriod_Id = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GenericName_Id = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicationDispensingPeriod_GenericNamess_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicationDispensingPeriods_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirstTimeWarning = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecondTimeWarning = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TradeName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicationDispensingPeriods_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MergeSampless_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SampleNumberId = table.Column<int>(type: "int", nullable: false),
                    MergedWithSampleId = table.Column<int>(type: "int", nullable: false),
                    NewSampleNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MergeSampless_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MostCommenInvestigationss_Radiology",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvestigationGroupEnum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InvestigationID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MostCommenInvestigationss_Radiology", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MovingPatients_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WardId = table.Column<int>(type: "int", nullable: false),
                    ToCostCenterId = table.Column<int>(type: "int", nullable: false),
                    IsOut = table.Column<bool>(type: "bit", nullable: false),
                    PateintId = table.Column<int>(type: "int", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MoveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovingPatients_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MyocardialPerfusionImagings_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IMPRESSION = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyocardialPerfusionImagings_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NationalVacations_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NationalVacations_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NewBorns_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MRN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BabyNameEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BabyNameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MotherID = table.Column<int>(type: "int", nullable: false),
                    FatherID = table.Column<int>(type: "int", nullable: false),
                    FatherFullNameArabic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FatherFullNameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FatherReligionID = table.Column<int>(type: "int", nullable: false),
                    FatherNationalNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FatherIDTypeID = table.Column<int>(type: "int", nullable: false),
                    FatherNationalityID = table.Column<int>(type: "int", nullable: false),
                    FatherJobID = table.Column<int>(type: "int", nullable: false),
                    FatherAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssueDate = table.Column<bool>(type: "bit", nullable: false),
                    IssueAddress = table.Column<bool>(type: "bit", nullable: false),
                    TimeOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BabyGenderID = table.Column<int>(type: "int", nullable: false),
                    BloodGroupID = table.Column<int>(type: "int", nullable: false),
                    BloodGroupTypeID = table.Column<int>(type: "int", nullable: false),
                    AttendingDoctorID = table.Column<int>(type: "int", nullable: false),
                    BabyDoctorID = table.Column<int>(type: "int", nullable: false),
                    BabyHeight = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BabyHeightUnitID = table.Column<int>(type: "int", nullable: false),
                    BabyWeight = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BabyWeightUnitID = table.Column<int>(type: "int", nullable: false),
                    BabyAlive = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Twins = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NumberOfTwins = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BabyBirthDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SquenceNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BabyAccommTypeID = table.Column<int>(type: "int", nullable: false),
                    BabyWardID = table.Column<int>(type: "int", nullable: false),
                    BabyRoomID = table.Column<int>(type: "int", nullable: false),
                    BabyBedID = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    FatherJob = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewBorns_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "NormalEmergancys_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    patientID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CameFrom = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentMethodID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DoctorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ERCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NormalEmergancys_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NurseAssessment_TagsValuess_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Value1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneralHeaderId = table.Column<int>(type: "int", nullable: false),
                    PageTagsId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NurseAssessment_TagsValuess_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NurseAssessmentHeaders_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NurseId = table.Column<int>(type: "int", nullable: false),
                    CareModeId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NurseAssessmentHeaders_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NurseStation_Drugs_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    drugID = table.Column<int>(type: "int", nullable: false),
                    ReceivedQty = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DispensedQty = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OPIPNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    SourceOrderDetailID = table.Column<int>(type: "int", nullable: false),
                    DestinationOrderDetailID = table.Column<int>(type: "int", nullable: false),
                    pharmacist = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DispensedPharmacy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExecutionBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NurseStation_Drugs_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NursingAdmissionAssessments_InPatient",
                columns: table => new
                {
                    NursingAdmissionAssessmentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChiefComplaint = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UrgentNeeds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrientedTo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Valuables = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Clothing = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OwnMedication = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EyeGlasses = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Dentures = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OtherAids = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BriefHistoryOfChiefComplaintAndReasonForAdmission = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreviousMajorRelated_IlnessOrSurgery = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Allergiesto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    PainScreening = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NumericPainScale = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PainAssessment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneralAppearance = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneralAppearanceDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MentalStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Anxious_relatedto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Useeyeglassesfor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Head_Ent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Respiratory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RespiratoryReferralInitiated = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Neuro_Muscular_Skeletal = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RespiratoryRemark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LevelofConsciousnessis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cardiovascular = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Edemaof = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ivfluid = table.Column<int>(type: "int", nullable: false),
                    CardiovascularSite = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CardiovascularRate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CardiovascularReferralInitiated = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CardiovascularRemarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Neuro_Muscular_SkeletalReferralInitiated = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Neuro_Muscular_Skeletal_Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SkinAndHair = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SkinAndHairReferralInitiated = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SkinAndHair_Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NutritionalAssessment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Special_Diet = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Vitamin_Or_Mineral_Supplement = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Genitourinary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Last_menstrual_period = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Gravida = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Para = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Abortion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Gestational_diabetes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GenitourinaryReferralInitiated = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GenitourinaryRemark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Genitourinary_Frequency_Tiems = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Genitourinary_Frequency_hr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Genitourinary_Frequency_day = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SPECIAL_ACTIVITIES_OF_DAILY_LIVING_ASSISTANCE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SPECIAL_ACTIVITIES_ReferralInitiated = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SPECIAL_ACTIVITIES_Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SPECIAL_ACTIVITIES_OtherSpecify = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SocialAssessment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SocialAssessment_ReferralInitiated = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SocialAssessment_Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OTHER_OBSERVATIONS_FINDINGS_ACTION_TAKEN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Signature = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NAME_OF_ADMITTING_NURSE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Time_Notified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Mode_Of_Admission = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreviousMajorRelated_IlnessOrSurgery_OtherDisease = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Emergency_Admission = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Direct_Admission = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ACCOMPANIED_BY = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Primary_Language = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    English = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Temp = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pulse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Resp = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Bp = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ht = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Wt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    HearingDeficit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VisionDefect = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NursingAdmissionAssessments_InPatient", x => x.NursingAdmissionAssessmentID);
                });

            migrationBuilder.CreateTable(
                name: "NursingAssessments_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PatientID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssessmentID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Date = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Time = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DoneBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OPNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PainIntensily = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TypeOfPainID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FrequencyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Duration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MentalStatusID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SpeechID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RespirationID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SkinColorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SkinTemperatureID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SkinMoistureID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NursingAssessments_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NursingNotes_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnteredBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NursingNotes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntryTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NursingNotes_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OCAFs_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Patient = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlanType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Bifocal = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Vertex = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Bifocal1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Glass = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Plastic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    None = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Multi_coated = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Medium = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Coating = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Varilux = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Lenticular = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Photosensitive = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Light = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Vision = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    hIndex = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Aspheric = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Dark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Colored = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    chkBifocal = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Thickness = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Anti_Scratch = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Permanent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Disposable = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Frames = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpecifyOfPairs = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtLensesSR = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtFrameSR = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtPhysicianSignature = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    txtNameRelationship = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtSignature = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    IPOP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OCAFs_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OnCallDoctorss_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DoctorId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SpicialityID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CallingDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModificationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnCallDoctorss_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OPDInternalTransfers_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    FromDoctorID = table.Column<int>(type: "int", nullable: false),
                    ToDoctorID = table.Column<int>(type: "int", nullable: false),
                    OldPrepareVisitSlipID = table.Column<int>(type: "int", nullable: false),
                    NewPrepareVisitSlipID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OPDInternalTransfers_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpeningPharmacyDetailss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OpeningPharmacyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BatchID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiryDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BatchNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhysicQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PharmacyInHand = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DifferenceQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OpenBalance = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningPharmacyDetailss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpeningPharmacyHeaders_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PharmacyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Year = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Date = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CloseYear = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningPharmacyHeaders_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpeningStockDetailss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OpeningStockID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BatchID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiryDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BatchNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhysicQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StockInHand = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DifferenceQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningStockDetailss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpeningStockHeaders_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SubStoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Year = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Date = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningStockHeaders_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationRequests_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiagnosisId = table.Column<int>(type: "int", nullable: false),
                    ProcedureId = table.Column<int>(type: "int", nullable: false),
                    OperationWardId = table.Column<int>(type: "int", nullable: false),
                    OperationRoomId = table.Column<int>(type: "int", nullable: false),
                    OperationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DepId = table.Column<int>(type: "int", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    EmergencyUnitID = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationRequests_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationRooms_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WardID = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationRooms_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "OperationTheatreDailyDutys_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationTheatreId = table.Column<int>(type: "int", nullable: false),
                    CostCenterId = table.Column<int>(type: "int", nullable: false),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    DutyDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FromTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationTheatreDailyDutys_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationTheatres_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationTheatres_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationWards_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FloorID = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationWards_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "OrderCategoryDetails_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderCategory_Id = table.Column<int>(type: "int", nullable: false),
                    Service_Id = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderCategoryDetails_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderCategoryMasters_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderTypeID = table.Column<int>(type: "int", nullable: false),
                    ArabicNameDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnglishNameDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderCategoryMasters_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "OrderMemberShips_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    OrderTypeId = table.Column<int>(type: "int", nullable: false),
                    VW_Order_vertified = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VW_Order_Unvertified = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VW_Order_Completed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VW_Order_Resulted = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VW_Order_Confirmed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Exc_Order_vertified = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Exc_Order_Unvertified = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Exc_Order_Completed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Exc_Order_Resulted = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Exc_Order_Confirmed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cncl_Order_vertified = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cncl_Order_Unvertified = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cncl_Order_Completed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cncl_Order_Resulted = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cncl_Order_Confirmed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderMemberShips_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderParameters_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderParameters_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "OrdersEnterys_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    OrderTypeID = table.Column<int>(type: "int", nullable: false),
                    OrderCategoryID = table.Column<int>(type: "int", nullable: false),
                    OrderItemID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FrequencyMasterID = table.Column<int>(type: "int", nullable: false),
                    Eurgent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DurationNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DurationType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdersEnterys_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "OrderTypes_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArabicNameDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EnglishNameDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TypeClass = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrderSheetType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InventoryID = table.Column<int>(type: "int", nullable: false),
                    ExpirationDuration = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderTypes_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "OrganismAntibioticSensitivitys_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    TestId = table.Column<int>(type: "int", nullable: false),
                    OrganismId = table.Column<int>(type: "int", nullable: false),
                    AntibioticId = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganismAntibioticSensitivitys_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organismss_Laboratory",
                columns: table => new
                {
                    OrganismsID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganismsName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Frequent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LessFrequent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organismss_Laboratory", x => x.OrganismsID);
                });

            migrationBuilder.CreateTable(
                name: "OtherHospitalss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    HospitalName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    POBox = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Zip = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WebSite = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtherHospitalss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutRersourceDispenseBagss_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OutResourceID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BagID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DispenseDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BagCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ServiceID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Paid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntryCodes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutRersourceDispenseBagss_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutResourcess_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutResourcesEnglishName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutResourcesArabicName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Administrator = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Active = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Account = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReturnPeriodNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReturnPeriodType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WarningPeriodNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WarningPeriodType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsLab = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Labresponsibleperson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LabPhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LabApi = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutResourcess_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutResourceTypes_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HospitalCase = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutResourceTypes_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PackagesRequestDetailss_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvestigationRequestID = table.Column<int>(type: "int", nullable: false),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    packagesID = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackagesRequestDetailss_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PackageUnitMasters_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageUnitMasters_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PageTagss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PageTagsGroupEnumValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TagId = table.Column<int>(type: "int", nullable: false),
                    PageEnumValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageTagss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Patient_Familys_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PF_Id = table.Column<int>(type: "int", nullable: false),
                    confirmeddiagnosis_understood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    confirmeddiagnosis_report = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    safeeffective_understood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    safeeffective_report = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    druginteraction_understood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    druginteraction_report = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    drugfood_understood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    drugfood_report = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nutrition_understood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nutrition_report = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nutrition_explained = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    safeequipment_understood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    safeequipment_report = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    safeequipment_explained = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Painmanagement_understood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Painmanagement_report = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    rehabilitationtechnique_understood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    rehabilitationtechnique_report = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    rehabilitationtechnique_explained = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    dischargeplanhome_understood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    dischargeplanhome_report = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    dischargeplanfollowup_understood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    dischargeplanfollowup_report = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    preventivemeasureinfection_understood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    preventivemeasureinfection_report = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    preventivemeasurepersonal_understood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    preventivemeasurepersonal_report = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    othersImplans_understood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    othersImplans_report = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    othersConsent_understood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    othersConsent_report = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    othersFinancial_understood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    othersFinancial_report = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    othersCommunity_understood = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    othersCommunity_report = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    date_time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patient_Familys_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Patient_Farmss_Laboratory",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    TestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FarmID = table.Column<int>(type: "int", nullable: false),
                    PatientType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patient_Farmss_Laboratory", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "PatientAllergyNews_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AllergyType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    genericID = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DrugID = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientAllergyNews_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatientAllergys_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    AllergyDetailID = table.Column<int>(type: "int", nullable: false),
                    TypeId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    ReactionId = table.Column<int>(type: "int", nullable: false),
                    ReactionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SeverityId = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Createby = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientAllergys_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PatientAllergyUpdatess_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsAllergy = table.Column<bool>(type: "bit", nullable: false),
                    AllergyDesc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    CreatorName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AllergyDetailsID = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientAllergyUpdatess_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatientDietManagements_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientDietId = table.Column<int>(type: "int", nullable: false),
                    BreakfastTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LunchTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DinnerTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientDietManagements_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatientDiets_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    DietId = table.Column<int>(type: "int", nullable: false),
                    PNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientDiets_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatientDischarges_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    DisChDoctorID = table.Column<int>(type: "int", nullable: false),
                    DisChargeTypeId = table.Column<int>(type: "int", nullable: false),
                    DisChDiagnosisId = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeathDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeathTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmergencyUnitID = table.Column<int>(type: "int", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DischargeReasonID = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DischargDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DischargeOrderID = table.Column<int>(type: "int", nullable: false),
                    HomeMed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BedClear = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Possisions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IPNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SecurityDischargeTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientDischarges_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PatientFamilyEducations_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    DateofAssessment = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Dept = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Educationgivento = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Relationship = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LiteracyLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Willingness = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrimaryLanguage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnderstoodLanguage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    A_None = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    A_Anxiety_Fear = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    A_LanguageBarrier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    A_Denial = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    A_Sensory_de_cit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    A_BeliefsandValues = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    A_Literacy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    A_CulturalPractice = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    A_PhysicalImpairment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    A_Pain_Discomfort = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    A_Emotional = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    A_Cognitive_impairment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    A_Lackofcon_dence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    A_Financial_Problems = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    A_Others = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    A_Others_text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    I_None = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    I_Obtaintranslator = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    I_TeachFamily = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    I_Respectvalues = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    I_Review_Repeat = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    I_Reassurance = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    I_RespectCultural = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    I_Appropritatesubstitution = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    I_Others = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    I_Others_text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    B_Diagnosis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    B_Treatment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    B_Pain = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    B_Self_care = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    B_regimen = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    B_Discharge = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    B_hand = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    B_stoma = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    B_Injection = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    B_Dietary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    B_Tube = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    B_Rehabilitation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    B_Antenatal = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    B_urinary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    B_SafeEffective = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    B_tracheotomy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    B_Implants = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    B_Coping = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TM_lecture = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TM_Demonstration = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TM_Discussion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TM_Audio = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TM_Model = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TM_Verbal = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OPIP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientFamilyEducations_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatientFarm_Detailss_Laboratory",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientFarmID = table.Column<int>(type: "int", nullable: false),
                    AntibioticID = table.Column<int>(type: "int", nullable: false),
                    TestResult = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AntibioticTestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientFarm_Detailss_Laboratory", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "PatientOrderDetails_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientOrderMasterId = table.Column<int>(type: "int", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Qty = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DrugID = table.Column<int>(type: "int", nullable: false),
                    GenericID = table.Column<int>(type: "int", nullable: false),
                    DoseUnitID = table.Column<int>(type: "int", nullable: false),
                    DrugForm = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Strength = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrescriptionsDetailsID = table.Column<int>(type: "int", nullable: false),
                    OrderTypeId = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FrequencyID = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerificationFromUserId = table.Column<int>(type: "int", nullable: false),
                    VerificationToNurseId = table.Column<int>(type: "int", nullable: false),
                    VerificationDateFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerificationDateTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    isVerified = table.Column<bool>(type: "bit", nullable: false),
                    ExecutionBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPaused = table.Column<bool>(type: "bit", nullable: false),
                    IsSkipped = table.Column<bool>(type: "bit", nullable: false),
                    SkipInstructionID = table.Column<int>(type: "int", nullable: false),
                    SkipReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SkipDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InsuranceId = table.Column<int>(type: "int", nullable: false),
                    RequestSuppliesDetailID = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ServiceGroupName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientOrderDetails_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PatientOrderMasters_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    DoctorId = table.Column<int>(type: "int", nullable: false),
                    OrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PeriodType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RepetTypeId = table.Column<int>(type: "int", nullable: false),
                    OrderCategId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    OrderTarget = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OPIPNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ISMedicine = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEditable = table.Column<bool>(type: "bit", nullable: false),
                    OrderStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FrequencyID = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransferJustification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransferUserId = table.Column<int>(type: "int", nullable: false),
                    IsOperation = table.Column<bool>(type: "bit", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientOrderMasters_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PatientRadReceptions_Radiology",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TechnicianID = table.Column<int>(type: "int", nullable: false),
                    DeviceID = table.Column<int>(type: "int", nullable: false),
                    ReceptionID = table.Column<int>(type: "int", nullable: false),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    PatientOPIP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RadStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    InvestigationRequestDetailsID = table.Column<int>(type: "int", nullable: false),
                    Approve = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientRadReceptions_Radiology", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PatientStatuss_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PatientStatusTypeDescArabic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PatientStatusTypeDescEnglish = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientStatuss_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatientTypes_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HospitalCase = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientTypes_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatientVitalss_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OP_IP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReadingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeroidType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MonitoredDays = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VitalParametersDeptWiseId = table.Column<int>(type: "int", nullable: false),
                    PateintId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientVitalss_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentGroups_Pharmacy",
                columns: table => new
                {
                    PaymentGroupID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PaymentGroupName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentGroups_Pharmacy", x => x.PaymentGroupID);
                });

            migrationBuilder.CreateTable(
                name: "PaymentSuppliers_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SupplierID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrencyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ForiegnValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConversionValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocalValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Sub_SupplierAccount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Main_SupplierAccount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Sub_BoxAccount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Main_BoxAccount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Checkno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntryCodes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentSuppliers_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTermsMasters_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PaymentTermCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentTermDesc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameKa = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTermsMasters_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTermsSchedules_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PaymentTermsID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScheduleDay = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SchedulePrecentage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeFrom = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTermsSchedules_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayPolicys_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayPolicys_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PermanentPacemakerImplantationReports_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateOfImplantation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Ys = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    IndicationForPermanent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ECGBeforePacemaker = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreMedication = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IVAntibiotic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocalAnaesthesia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VenousAccess = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PocketSite = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeadInsertionSite = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AtrialLead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VentricualLead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BatteryInsertionSite = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BatteryFixation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WoundClosure = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Subcutaneous = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Skin = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocalAntibiotic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PWave = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RWave = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VentricularPacingThreshold = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImpedanceAtrialLead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImpedanceVentricularLead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VLeadManufacturer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VLeadModel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AtrialLeadManufacturer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BatteryDataManufacturer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Complications = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Antibiotics = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AppointmentsForPacemaker = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArtialPacingThreshold = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AtrialLeadModel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BatteryDataModel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VLeadSerialNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AtrialLeadSerialNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BatteryDataSerialNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VLeadType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AtrialLeadType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BatteryDataType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermanentPacemakerImplantationReports_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PermittedStaffs_Laboratory",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvestigationGroup = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReportEntry = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReportValidation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermittedStaffs_Laboratory", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "PharmacyPayments_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Date = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    chequeNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    chequeStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DueDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MainAccount_Supplier = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubAccount_Supplier = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MainAccount_Bank = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubAccount_Bank = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MainAccount_PaymentPaper = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubAccount_PaymentPaper = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Modificationdate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ForiegnValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConversionValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrencyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SetEntryCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentMode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierDuesId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacyPayments_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PharmacySettingss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MinQtyStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MinUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaxQtyStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaxUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExchangePolicy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnifiedPurchaseSupplierID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PharmacyMgr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PharmacySubDepartmentID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Vat = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DispenseQuantity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhysicalStockAdjustment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiaryPeriodDays = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecessionPeriodInDays = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReorderLvel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    slowMove = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacySettingss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PharmacySupplierInvoicePayments_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GRNHeaderID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrencyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ForiegnValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConversionValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocalValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CashHeaderID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PharmacyHeaderID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntryCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacySupplierInvoicePayments_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PharmInstallationDoctorDegrees_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DoctorDegreeId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmInstallationDoctorDegrees_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PharmInstallations_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BarCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JEwithBatch = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Alert0Qty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Order0Qty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IBarCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DispenseOPIP = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    storeIntegration = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReturnToDeposit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatePrice = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubStoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsUpdateStock = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsAddition = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDispense = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    isReturn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsIToD = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsIToPharmacy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IstraferStock = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiaryPeriodDays = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmInstallations_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalStockAdjustmentEntrys_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PhysiacalAdjID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BatchID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QTYinHand = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QTYAdj = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QtYDifference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalStockAdjustmentEntrys_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalStockAdjustments_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SubStoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReasonAdjusyId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Date = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhysiacalNO = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalStockAdjustments_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostOPNursingCarePlan_TagsValuess_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Value1 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneralHeaderId = table.Column<int>(type: "int", nullable: false),
                    PageTagsId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostOPNursingCarePlan_TagsValuess_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostOPNursingCarePlanHeaders_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NurseId = table.Column<int>(type: "int", nullable: false),
                    CareModeId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    IPOP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostOPNursingCarePlanHeaders_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostRoomChargess_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdmitPatientsId = table.Column<int>(type: "int", nullable: false),
                    ChargeDays = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostRoomChargess_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PreAnesthesiaEvaluations_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    HistoryFrom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HistoryFromOther = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreviousAnesthesiaNone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreviousAnesthesia = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentMedicationsNone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AllergiesReaCcionNone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AirwayType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TMDistance = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MODistance = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NeckRom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RespiratoryWNL = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RespiratoryEnum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TobacooUse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TobacooUseRR = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TobacooUsePacks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TobacooUseForYears = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TobacooUseOut = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TobacooUseOutText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TobacooUsePPPPE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CardioVascularWML = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CardioVascularEnum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VitalsHR = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VitalsBP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VitalsJVPCVP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VitalsPeripheralpulses = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VitalsPPPPE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HepatoGastrointestinalWML = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HepatoGastrointestinalEnum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EthanolUse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EthanolUseFrequancy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EthanolUseHxETOHAbuse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EthanolUseOut = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EthanolUseOutText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NeuroMusculoskeletalWML = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NeuroMusculoskeletalEnum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RenalEndcorineWML = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RenalEndcorineEnum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OtherWML = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OtherEnum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FamilialAnesProblems = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FamilialAnesProblemsNotes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SurgicalDiagnosisOrProblemList = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AsaPhysicalStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NpoPerASAGuidelines = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreAnesthesiaEvaluations_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PreOperativeMarkings_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PreOperativeId = table.Column<int>(type: "int", nullable: false),
                    Mandatory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Checked = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScheduleSurgeryId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreOperativeMarkings_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PreOperatives_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mandatory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreOperatives_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrepareVisitSlips_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    VisitType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScheduleAppointmentID = table.Column<int>(type: "int", nullable: false),
                    DepartmentID = table.Column<int>(type: "int", nullable: false),
                    DoctorsID = table.Column<int>(type: "int", nullable: false),
                    SessionID = table.Column<int>(type: "int", nullable: false),
                    Time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TimeOfVisit = table.Column<DateTime>(type: "datetime2", nullable: false),
                    referralDepartmentID = table.Column<int>(type: "int", nullable: false),
                    referralClinicID = table.Column<int>(type: "int", nullable: false),
                    referralDoctorsID = table.Column<int>(type: "int", nullable: false),
                    MedicoLegalID = table.Column<int>(type: "int", nullable: false),
                    VisitStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OPnumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VisitNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NoCharge = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OPNumberStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    VisitSlipStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CancelReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    referralDoctorsPerc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EndTreatmentCycle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsOnlinePayment = table.Column<bool>(type: "bit", nullable: false),
                    OvarTimeOnline = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    IsConfirmthetermsandconditions = table.Column<bool>(type: "bit", nullable: false),
                    IsVirtual = table.Column<bool>(type: "bit", nullable: false),
                    invDtl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VisitReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Encounterstatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    serviceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    careteamRole = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrepareVisitSlips_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionAbbreviations_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AbbreviationName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionAbbreviations_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionDetailss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    PrescriptionNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DrugID = table.Column<int>(type: "int", nullable: false),
                    Dosage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DosageUnitID = table.Column<int>(type: "int", nullable: false),
                    FerquencyID = table.Column<int>(type: "int", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PeriodTypeID = table.Column<int>(type: "int", nullable: false),
                    DoseQTY = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionDetailss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionDispenseSettings_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Days = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Prescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NoOfDays = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionDispenseSettings_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prescriptions_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    OPNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IPNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrescriptionNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrescriptionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AlertName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prescriptions_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionsDetailss_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrescriptionID = table.Column<int>(type: "int", nullable: false),
                    PrescriptionNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    DrugID = table.Column<int>(type: "int", nullable: false),
                    DosageUnitID = table.Column<int>(type: "int", nullable: false),
                    Dosage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FrequencyID = table.Column<int>(type: "int", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoseQty = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TypeId = table.Column<int>(type: "int", nullable: false),
                    AdminModeId = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContinueDrugDuringAdmission = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    GenericID = table.Column<int>(type: "int", nullable: false),
                    SpecialInstruction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PRN_Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PRN_MaxFreq = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IFNeeded = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Strength = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClinicalPharmacy_Accept = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DiagnosisId = table.Column<int>(type: "int", nullable: false),
                    Refill = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FrequencyofRefills = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FrequencyofRefillsType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NumberofRefills = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContinusRefill = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InsuranceId = table.Column<int>(type: "int", nullable: false),
                    RouteId = table.Column<int>(type: "int", nullable: false),
                    AdministrationSiteId = table.Column<int>(type: "int", nullable: false),
                    Dispense_Allowed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionsDetailss_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionsHeaders_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    PrescriptionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PrescriptionNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    OPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AlertName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comorbidities = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    isStop = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionsHeaders_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProceduresDetailss_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProceduresMasterID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProceduresDetailss_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ProceduresMasters_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProceduresMasters_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ProhibitedDrugDocss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    InvoiceID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProhibitedDrugDocss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProvisionalDiagnosiss_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    AdmitPatientID = table.Column<int>(type: "int", nullable: false),
                    ICDCodeID = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProvisionalDiagnosiss_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReturnDetailss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PurchaseReturnHeaderId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubStoreBatchesId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReturnQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    discount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReturnDetailss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReturnHeaders_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PurchaseReturnNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReturnDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalAmount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubstoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GrnNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntryCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReturnHeaders_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RadiologyReceptionss_Radiology",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeSlot = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DepartmentID = table.Column<int>(type: "int", nullable: false),
                    SpeciaityGroupID = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceptionTypeID = table.Column<int>(type: "int", nullable: false),
                    SessionID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Emp_ReceptionAdmin = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiologyReceptionss_Radiology", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RadReceptioniestSchedules_Radiology",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayID = table.Column<int>(type: "int", nullable: false),
                    EmpID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceptionID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadReceptioniestSchedules_Radiology", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RadReceptionProceduress_Radiology",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServicegroupID = table.Column<int>(type: "int", nullable: false),
                    ProcedureSlot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    receptionID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Creationdate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadReceptionProceduress_Radiology", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RadResultImagess_Laboratory",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResultEntryDetailsID = table.Column<int>(type: "int", nullable: false),
                    ImageName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadResultImagess_Laboratory", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RadResultImagess_Radiology",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResultEntryDetailsID = table.Column<int>(type: "int", nullable: false),
                    ImageName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadResultImagess_Radiology", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RayBodyLoactions_Radiology",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LocationAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocationEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RayBodyLoactions_Radiology", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ReceptionDevicsSchedules_Radiology",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceptionID = table.Column<int>(type: "int", nullable: false),
                    DeviceID = table.Column<int>(type: "int", nullable: false),
                    DayID = table.Column<int>(type: "int", nullable: false),
                    SessionID = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceptionDevicsSchedules_Radiology", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ReceptionStockss_Radiology",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceptionID = table.Column<int>(type: "int", nullable: false),
                    StockID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceptionStockss_Radiology", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ReferralTypes_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HospitalCase = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorCommission = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MonthFees = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralTypes_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReFillPrescriptionss_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FrequencyofRefills = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FrequencyofRefillsType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NumberofRefills = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrescriptionsDetailsId = table.Column<int>(type: "int", nullable: false),
                    ContinusRefill = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReFillPrescriptionss_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefundDrugsDetailss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReturnReceiptHeaderId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceptDetailsId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReturnQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundDrugsDetailss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefundDrugsHeaders_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReceiptNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReturnReceiptNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReturnReceiptDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceiptDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundDrugsHeaders_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegisteringPackageinstallments_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    PackageID = table.Column<int>(type: "int", nullable: false),
                    IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Duration = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisteringPackageinstallments_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegisteringPackages_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    PackageID = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPrecentage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TypeID = table.Column<int>(type: "int", nullable: false),
                    IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InstalmentAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ColectedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BalanceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisteringPackages_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RejectSamples_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RejectionReasonId = table.Column<int>(type: "int", nullable: false),
                    Other = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SampleEntryId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RejectSamples_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReOrderHistoryDetailss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MasterID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StockOnHand = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Shortage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    awaitQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReOrderQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReorderUnit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReOrderHistoryDetailss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReOrderHistoryMasters_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    purchaseReqId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    slowMovement = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReOrderHistoryMasters_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RepetTypes_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepetTypes_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RequestProceduresDetailss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestProceduresID = table.Column<int>(type: "int", nullable: false),
                    ProcedureID = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestProceduresDetailss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RequestProceduresHeaders_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OP_IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPregnant = table.Column<bool>(type: "bit", nullable: false),
                    PregnantWeeks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClinicalDetails = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReqDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReqStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SurgeryID = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestProceduresHeaders_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RequestSuppliesDetailss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestSuppliesHeaderID = table.Column<int>(type: "int", nullable: false),
                    ItemID = table.Column<int>(type: "int", nullable: false),
                    UnitConversionID = table.Column<int>(type: "int", nullable: false),
                    ReturnedQty = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsIncluded = table.Column<bool>(type: "bit", nullable: false),
                    StockBatchId = table.Column<int>(type: "int", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    CurrencyID = table.Column<int>(type: "int", nullable: false),
                    ConvValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StockControlDetailList = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ISCash = table.Column<bool>(type: "bit", nullable: false),
                    InsuranceId = table.Column<int>(type: "int", nullable: false),
                    Dispense = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Verified = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VerifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedBY = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestSuppliesDetailss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RequestSuppliesDetailss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RequestSuppliesHeaderID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitConversionID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReturnedQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsIncluded = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubstoreBatchId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsApproved = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AmountBefore = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AmountAfter = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SponserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntryCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Paid = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Discount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Visa = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VisaReceipt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PateintPaidAmount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SponserPaidAmount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ISCash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ServiceID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrescriptionIdDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    insuranceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PatVat = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SpoVat = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    patientlimitdtl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dosage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FrequencyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestSuppliesDetailss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RequestSuppliesHeaders_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    PatientTypeID = table.Column<int>(type: "int", nullable: false),
                    OP_IPNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    RequestStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubStoreID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    branchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestSuppliesHeaders_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RequestSuppliesHeaders_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PatientID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PatientTypeID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OP_IPNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DoctorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubStoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutPatName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    outPrescriptionDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrescriptionHeaderID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InsuranceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    branchId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscountPercent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscountAmount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestSuppliesHeaders_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RequestSupplyReturnDetailss_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestSupplyReturnHeaderID = table.Column<int>(type: "int", nullable: false),
                    RequestSupplyDetailsID = table.Column<int>(type: "int", nullable: false),
                    ReturnedQty = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnitConversionID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestSupplyReturnDetailss_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RequestSupplyReturnDetailss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RequestSupplyReturnHeaderID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestSupplyDetailsID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReturnedQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitConversionID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PateintPaidAmount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SponserPaidAmount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SponserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestSupplyReturnDetailss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RequestSupplyReturnHeaders_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestSupplyID = table.Column<int>(type: "int", nullable: false),
                    RequestSupplyRetuenCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntryCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestSupplyReturnHeaders_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RequestSupplyReturnHeaders_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RequestSupplyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestSupplyRetuenCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReturnDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntryCodes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestSupplyReturnHeaders_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResultEntryDetails_Findingss_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpID = table.Column<int>(type: "int", nullable: false),
                    Findings = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Concolusion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FindingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultEntryID = table.Column<int>(type: "int", nullable: false),
                    FinalFindEmpId = table.Column<int>(type: "int", nullable: false),
                    StatDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevisedById = table.Column<int>(type: "int", nullable: false),
                    FinalApprovedById = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultEntryDetails_Findingss_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResultEntryDetails_Findingss_Radiology",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResultEntryDetailsID = table.Column<int>(type: "int", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    Findings = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Concolusion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FindingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RevisedById = table.Column<int>(type: "int", nullable: false),
                    FinalApprovedById = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultEntryDetails_Findingss_Radiology", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResultEntryDetails_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResultEntry_Id = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    InvestigationDetailsId = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TestDetailsId = table.Column<int>(type: "int", nullable: false),
                    ResultFile = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ValueP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultEntryDetails_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResultEntryDetailss_Radiology",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResultEntryId = table.Column<int>(type: "int", nullable: false),
                    InvestigationDetailsId = table.Column<int>(type: "int", nullable: false),
                    OPIPNO = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorId = table.Column<int>(type: "int", nullable: false),
                    TimeStudy = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Abnormal = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Redone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Findings = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Conclusion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultEntryDetailss_Radiology", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResultEntryHeaders_Radiology",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    BrabchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultEntryHeaders_Radiology", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResultEntrys_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionID = table.Column<int>(type: "int", nullable: false),
                    SampleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    ServiceID = table.Column<int>(type: "int", nullable: false),
                    ResultId = table.Column<int>(type: "int", nullable: false),
                    ObservedValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TechnicianId = table.Column<int>(type: "int", nullable: false),
                    GrowthOptionId = table.Column<int>(type: "int", nullable: false),
                    Approved = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    DeviceID = table.Column<int>(type: "int", nullable: false),
                    NewSampleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultEntrys_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResultRangesDetailss_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResultRangesID = table.Column<int>(type: "int", nullable: false),
                    AgeFrom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AgeTo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SexId = table.Column<int>(type: "int", nullable: false),
                    ValueFrom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValueTo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinimumValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaximumValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultRangesDetailss_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResultRangess_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionID = table.Column<int>(type: "int", nullable: false),
                    ResultID = table.Column<int>(type: "int", nullable: false),
                    ResultTypeId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultRangess_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Resultss_Laboratory",
                columns: table => new
                {
                    ResultID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionID = table.Column<int>(type: "int", nullable: false),
                    ResultName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultTypeId = table.Column<int>(type: "int", nullable: false),
                    Units = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SIUnits = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConversionFactor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TestID = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resultss_Laboratory", x => x.ResultID);
                });

            migrationBuilder.CreateTable(
                name: "ResultValueDetailss_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgeFrom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AgeTo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SexId = table.Column<int>(type: "int", nullable: false),
                    MinimumValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaximumValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    ResultValueHeaderID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultValueDetailss_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResultValueHeaders_Laboratory",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultValueHeaders_Laboratory", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ReturnDispenseBloodBagss_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PatientID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BagID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BagCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OP_IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DoctorID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ServiceID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReturnAmount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReturnDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntryCodes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnDispenseBloodBagss_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReturningExpiryItemsDetailss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReturningExpiryItemsHeaderID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpiryDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubStoreBatchId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReturnQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitPrice = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SetEntryCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturningExpiryItemsDetailss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReturningExpiryItemsHeaders_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SubStoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReturnNO = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturningExpiryItemsHeaders_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RiskTypes_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DescriptionArabic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescriptionEnglish = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskTypes_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoomAccommodationTypess_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    AccommodationTypeId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomAccommodationTypess_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rooms_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoomNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RoomTypeId = table.Column<int>(type: "int", nullable: false),
                    CostCenterId = table.Column<int>(type: "int", nullable: false),
                    WardId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    AccommodationTypeId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccommodationTypesIDz = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Active = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VIP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoomTransfers_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    IPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FromWardId = table.Column<int>(type: "int", nullable: false),
                    ToWardId = table.Column<int>(type: "int", nullable: false),
                    FromBedId = table.Column<int>(type: "int", nullable: false),
                    ToBedId = table.Column<int>(type: "int", nullable: false),
                    TransferDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransferTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdmitPateintId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomTransfers_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoomTypes_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DayPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    Ward = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccomodationType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VivRoom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Active = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IcuRoom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomTypes_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Routes_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SampleCollectionMedias_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionMediaName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleCollectionMedias_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SampleEntrys_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvestigationRequestDetailsId = table.Column<int>(type: "int", nullable: false),
                    SpecimenID = table.Column<int>(type: "int", nullable: false),
                    LabId = table.Column<int>(type: "int", nullable: false),
                    SampleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Resample = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TechnicanID = table.Column<int>(type: "int", nullable: false),
                    RejectionReasonId = table.Column<int>(type: "int", nullable: false),
                    Other = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CancelReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CancelSample = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsTakeSampleOutSideHospital = table.Column<bool>(type: "bit", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    OutSourceLab = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleEntrys_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SamplesDispatchedDetailss_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SamplesDispatchedId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SampleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TestId = table.Column<int>(type: "int", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    InvestigationDetailID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SamplesDispatchedDetailss_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SamplesDispatchedHeaders_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DispatchNO = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExternalAgencyId = table.Column<int>(type: "int", nullable: false),
                    EnteredBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SamplesDispatchedHeaders_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SamplesReceivedDetailss_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SamplesReceivedId = table.Column<int>(type: "int", nullable: false),
                    ExtSampleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TestId = table.Column<int>(type: "int", nullable: false),
                    SampleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CollectedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CollectedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PatientName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SexId = table.Column<int>(type: "int", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    InvestigationDetailID = table.Column<int>(type: "int", nullable: false),
                    IsCollected = table.Column<bool>(type: "bit", nullable: false),
                    Resample = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LabRequestStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SamplesReceivedDetailss_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SamplesReceivedHeaders_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternalAgencyId = table.Column<int>(type: "int", nullable: false),
                    LabNO = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReferanceNO = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SamplesReceivedHeaders_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SamplesTransferes_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FromLabId = table.Column<int>(type: "int", nullable: false),
                    ToLabId = table.Column<int>(type: "int", nullable: false),
                    SampleNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SamplesReceivedId = table.Column<int>(type: "int", nullable: false),
                    TechnicanId = table.Column<int>(type: "int", nullable: false),
                    RecievedTechnicanId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SamplesTransferes_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SampleTypes_Laboratory",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeNameEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TypeNameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Descr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleTypes_Laboratory", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleAppointments_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    DepartmentID = table.Column<int>(type: "int", nullable: false),
                    DoctorsID = table.Column<int>(type: "int", nullable: false),
                    SessionID = table.Column<int>(type: "int", nullable: false),
                    Time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppointmentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleAppointments_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleOTs_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationID = table.Column<int>(type: "int", nullable: false),
                    OperationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    SurgeonID = table.Column<int>(type: "int", nullable: false),
                    NurseID = table.Column<int>(type: "int", nullable: false),
                    SurgeryID = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleOTs_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleSurgerys_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SurgeonId = table.Column<int>(type: "int", nullable: false),
                    AnesthetistId = table.Column<int>(type: "int", nullable: false),
                    AssistantId = table.Column<int>(type: "int", nullable: false),
                    ReqId = table.Column<int>(type: "int", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    ScheduleSurgeryNO = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScheduleSurgeryStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperationTheatreId = table.Column<int>(type: "int", nullable: false),
                    SurgeryId = table.Column<int>(type: "int", nullable: false),
                    FromDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FromTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdditionalDetailsForScheduledSurgeries = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleSurgerys_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScrapDetails_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ScrapHeaderId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StockBatchId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Date = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitConversionID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScrapQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalReturnQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrapDetails_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScrapHeaders_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MainStockID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScrapNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScrapDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StockID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssueRequestID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntryCodes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrapHeaders_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScrapReturnDetails_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    HeaderID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Amount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubStockBatchID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitConversionFactorId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrapReturnDetails_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScrapReturnHeaders_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OperationNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OperationDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubStockID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubStockToID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntryCodes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrapReturnHeaders_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sectionss_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    InvestigationGroupId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sectionss_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SelectionTypes_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelectionTypes_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Service_ResultValueHeaders_Laboratory",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Service_Id = table.Column<int>(type: "int", nullable: false),
                    ResultValueHeader_Id = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Service_ResultValueHeaders_Laboratory", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ServicesRequestDetailss_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvestigationRequestID = table.Column<int>(type: "int", nullable: false),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ServicesID = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicesRequestDetailss_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShiftTypes_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeFrom = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeTo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftTypes_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpecialityGroupDetailss_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SpecialityMasterID = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Creationdate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssessmentSpecialist = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialityGroupDetailss_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SpecialityGroupMasters_OutPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Creationdate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameRu = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialityGroupMasters_OutPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SpecimenTypeAssociations_Laboratory",
                columns: table => new
                {
                    SpecimenTypeAssociationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SpecimenTypesID = table.Column<int>(type: "int", nullable: false),
                    SectionID = table.Column<int>(type: "int", nullable: false),
                    TestID = table.Column<int>(type: "int", nullable: false),
                    SampleTypeId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecimenTypeAssociations_Laboratory", x => x.SpecimenTypeAssociationID);
                });

            migrationBuilder.CreateTable(
                name: "SpecimenTypess_Laboratory",
                columns: table => new
                {
                    SpecimenTypesID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SpecimenTypesName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    Apprivate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecimenTypess_Laboratory", x => x.SpecimenTypesID);
                });

            migrationBuilder.CreateTable(
                name: "SponsorshipConditionCostCenterss_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SponsorID = table.Column<int>(type: "int", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Discountpercentage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CoverageLimit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CoverageTypeID = table.Column<int>(type: "int", nullable: false),
                    Deductiblepercentage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CostCenterID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsorshipConditionCostCenterss_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SponsorshipConditionDrugss_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SponsorID = table.Column<int>(type: "int", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DrugID = table.Column<int>(type: "int", nullable: false),
                    standardAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SupplierItemID = table.Column<int>(type: "int", nullable: false),
                    Discountpercentage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ISApproval = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsorshipConditionDrugss_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SponsorshipConditionDrugTypess_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SponsorID = table.Column<int>(type: "int", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DrugTypeID = table.Column<int>(type: "int", nullable: false),
                    Discountpercentage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsorshipConditionDrugTypess_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SponsorshipConditions_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SponsorID = table.Column<int>(type: "int", nullable: false),
                    CategoryID = table.Column<int>(type: "int", nullable: false),
                    CarrierID = table.Column<int>(type: "int", nullable: false),
                    CoverageLimit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PerEpisode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PerAnnum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InpatientTreatment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutpatientTreatment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContractNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContractDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Co_paymentamount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsPercentage = table.Column<bool>(type: "bit", nullable: false),
                    IsAmount = table.Column<bool>(type: "bit", nullable: false),
                    IsNet = table.Column<bool>(type: "bit", nullable: false),
                    IsGross = table.Column<bool>(type: "bit", nullable: false),
                    IsBeforeDeductible = table.Column<bool>(type: "bit", nullable: false),
                    IsAfterDeductible = table.Column<bool>(type: "bit", nullable: false),
                    IsVerifyPolicyNumber = table.Column<bool>(type: "bit", nullable: false),
                    IsAutoGenratePolicyNumber = table.Column<bool>(type: "bit", nullable: false),
                    IsImmediatesettlement = table.Column<bool>(type: "bit", nullable: false),
                    IsCreditsettlement = table.Column<bool>(type: "bit", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsorshipConditions_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SponsorshipConditionServicess_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SponsorID = table.Column<int>(type: "int", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    standardAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Discountpercentage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Deductiblepercentage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeductibleAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ISApproval = table.Column<bool>(type: "bit", nullable: false),
                    ServicesID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsorshipConditionServicess_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SponsorshipConditionSupplierGroupsItemss_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SponsorID = table.Column<int>(type: "int", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    standardAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SupplierItemID = table.Column<int>(type: "int", nullable: false),
                    Discountpercentage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ISApproval = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsorshipConditionSupplierGroupsItemss_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SponsorshipConditionSupplierGroupss_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SponsorID = table.Column<int>(type: "int", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveToDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SupplierGroupID = table.Column<int>(type: "int", nullable: false),
                    Discountpercentage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsorshipConditionSupplierGroupss_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StdDosages_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AgeFrom = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AgeTo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dosage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FrequencyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Period = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StdDosages_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustmentReasonss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NameEN = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameAR = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAdjustmentReasonss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockControlDetails_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StockControlID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Qty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Balance = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConvertionFactor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GRNDetailID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GRNDetailBalance = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockControlDetails_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockControls_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubStoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StockOnHand = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaxQuantity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MinQuantity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecordLevel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecordQuantity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Accessability = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitConversionFactorId_ForPurchase = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitConversionFactorId_ForIssue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReOrder = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CriticalQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReOrderQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReorderUnit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockControls_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockTransferEntrys_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StockTransferID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssuedQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DespatchQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderedQTY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransferEntrys_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockTransfers_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RequestedStoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssuingStoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockTransfers_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StrenghtUnitMasters_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrenghtUnitMasters_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Substore_Itemss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SubstoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItemID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Substore_Itemss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubstoreAuthoritys_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SubStoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsLPO = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsEPO = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsGRN = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsCashGRN = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsAddition = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDispense = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    isReturn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsUpdateStock = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsIToD = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsIToPharmacy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReturnToPatient = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReturnToSupplier = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubstoreAuthoritys_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubStoreBatchess_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BatchId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubStoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceivedQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcceptedQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExcessQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Bonus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvailableQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssueBatchID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubStoreBatchess_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubStoresClassificationss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClassificationID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubStoresClassificationss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Substoress_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SubstoreName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CostCenterCompaniesID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AllowSupplierTransactions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDispensingOfDrugs = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsMainStore = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PatientType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MainStockID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MainAccount_GeneralStock = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubAccount_GeneralStock = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MainAccount_CostOfGoods = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubAccount_CostOfGoods = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MainAccount_SalesRevenue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubAccount_SalesRevenue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MainAccount_SettlementByDiscount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubAccount_SettlementByDiscount = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MainAccount_SettlementAsWell = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubAccount_SettlementAsWell = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MainAccount_OutgoingMovements = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubAccount_OutgoingMovements = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubstoreCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubstoreNameAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserCharge = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WardPharm = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StockType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsScrap = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDrugStore = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsProhibitedDrug = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BranchID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Substoress_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplierContactss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SupplierID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactLocation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fax = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierContactss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplierforDrugs_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DrugID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierforDrugs_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplierRelatedCompaniess_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SupplierRelatedCompaniesName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierRelatedCompaniess_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Supplierss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SupplierCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierTypeID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrencyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShippingModeID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryTermID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeliveryTimeType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreditLimit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreditPeriod = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreditPeriodType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentTimeType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentModeID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentGroupID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentTermsID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    POBox = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ZipCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MainAcc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubAcc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierNameAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Parent_Id = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AgentID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StopPayment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HospitalNumForSupplier = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    International_Local = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Matchingmethod = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    allowtoacceptalternativitem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Stoppingsupplierpayments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Differencepaymentdate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Prioritypayment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Aretaxesdeducted = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcceptedRounding = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsItPossibleToRequestQuote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmployeeName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhonNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MobileNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmployeeAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmployeeEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MainAccountToSupplier = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CalcSuppliertax = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDiscountNoticeGiven = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcceptGoodsWithoutPurchaseOrder = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StopPurchaseOrders = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetermineTypeOfReceipt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSovereignSide = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TaxRegistrationNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WithHoldingTax = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Supplierss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupplierTypes_Pharmacy",
                columns: table => new
                {
                    SupplierTypeID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SupplierTypeNameEN = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupplierTypeNameAR = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierTypes_Pharmacy", x => x.SupplierTypeID);
                });

            migrationBuilder.CreateTable(
                name: "Surgerys_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Surgerys_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SurgeryTypes_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurgeryTypes_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tagss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Active = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    OrderNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tagss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelephoneChargess_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    LastIPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChargeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TelephoneNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Duration = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelephoneChargess_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Templates_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TemplateName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Templates_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemporaryDischargeTypes_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEnglish = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HospitalCase = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemporaryDischargeTypes_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemporaryExits_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PateintID = table.Column<int>(type: "int", nullable: false),
                    PatientIPNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    GoOutTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BackTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DoctorApproveAttachmentURL = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientSignConsentAttachmentURL = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActualBackTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModificationBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModificationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemporaryExits_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TestDetailss_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComponentId = table.Column<int>(type: "int", nullable: false),
                    Desciption = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    AgeFrom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AgeTo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinNormalRange = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaxNormalAge = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PanicResult = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TestId = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LabTestType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPositive = table.Column<bool>(type: "bit", nullable: false),
                    Range = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestDetailss_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestResultLinkingEntrys_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TestResultLinkingId = table.Column<int>(type: "int", nullable: false),
                    ResultId = table.Column<int>(type: "int", nullable: false),
                    ResultOrder = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestResultLinkingEntrys_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestResultLinkings_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    TestId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestResultLinkings_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Testss_Laboratory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TestName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    SampleTypeId = table.Column<int>(type: "int", nullable: false),
                    ContainerTypeId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Testss_Laboratory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Testss_Radiology",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TestName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Testss_Radiology", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeBoundServiceRequestDetailss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    TimeBoundServiceRequestID = table.Column<int>(type: "int", nullable: false),
                    ServiceID = table.Column<int>(type: "int", nullable: false),
                    IssueUnitID = table.Column<int>(type: "int", nullable: false),
                    UnitCharge = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeBound = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FromTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalUnits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeBoundServiceRequestDetailss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeBoundServiceRequestHeaders_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    FromDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Todate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    IsPerDay = table.Column<bool>(type: "bit", nullable: false),
                    IsCumulative = table.Column<bool>(type: "bit", nullable: false),
                    AccupancyNO = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeBoundServiceRequestHeaders_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransferRequestDispenses_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PatientID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BloodProductID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BloodTypeID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocationID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransferRequestID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BagNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferRequestDispenses_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransfuionProcessings_BloodBank",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PatientID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reaction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransferRequestID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderStatues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InventoryID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransfuionProcessings_BloodBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TriagCategoryItemss_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DescriptionArabic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescriptionEnglish = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TriagCategoryID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameKa = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriagCategoryItemss_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TriagCategorys_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DescriptionArabic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescriptionEnglish = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Color = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WaitingTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameKa = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriagCategorys_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UCAFs_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Patient = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LMP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlanType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IllnessDuration = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Area_Significant = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrincipleCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SecondCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HirdCod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ourthCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Esstimated = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    dmissionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Physician = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    txtRelationship = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtRelationshipSignature = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    txtRelationshipDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WorkIN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    completed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Chronic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Congenital = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RTA = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Work = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Vaccanation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Checkup = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Physicantric = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    infiritily = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    pregnancy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    reff = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IPOP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VisitDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UCAFs_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnitConversionFactors_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UnitTemplateID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConversionValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitConversionFactors_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Unitss_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UnitNameArabic = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubUnitNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Unitss_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnitTemplates_Pharmacy",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UnitTemplateCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitTemplateNameAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitTemplateName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BaseUnitID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubStoreID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitTemplates_Pharmacy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vac_Clinics_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VacationId = table.Column<int>(type: "int", nullable: false),
                    ClinicId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vac_Clinics_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VaccinationCharts_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    OPNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    vaccinationID = table.Column<int>(type: "int", nullable: false),
                    Doses = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DosageNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DoctorId = table.Column<int>(type: "int", nullable: false),
                    year = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Jan = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Feb = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Apr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    May = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    June = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    July = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Aug = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sep = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Oct = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nov = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Dec = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaccinationCharts_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VaccinationDetailss_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VaccinationID = table.Column<int>(type: "int", nullable: false),
                    DosageNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinMonth = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaxMonth = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ServiceID = table.Column<int>(type: "int", nullable: false),
                    Rate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaccinationDetailss_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VaccinationMasters_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VaccinationName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NODoses = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VaccinationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    ISHIbB = table.Column<bool>(type: "bit", nullable: false),
                    VaccinationNameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaccinationMasters_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VaccinationSchedules_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    ScheduleDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    vaccinationID = table.Column<int>(type: "int", nullable: false),
                    FrequencyID = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaccinationSchedules_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VirtualClinics_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DoctorId = table.Column<int>(type: "int", nullable: false),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    MeetingId = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualClinics_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VitalParameters_Emergency",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VitalParameters_Emergency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VitalParametersDeptWises_OutPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentID = table.Column<int>(type: "int", nullable: false),
                    vitalparameterID = table.Column<int>(type: "int", nullable: false),
                    ISCompulsory = table.Column<bool>(type: "bit", nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FrequencyType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaxValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VitalParametersDeptWises_OutPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VitalParameterss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VitalTypeGroupId = table.Column<int>(type: "int", nullable: false),
                    FindingId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VitalParameterss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VitalSettingss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AgeFrom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AgeTo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PulseFrom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PulseTo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RespRateFrom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RespRateTo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SystolicBPFrom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SystolicBPTo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TempratureFrom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TempratureTo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NameKa = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VitalSettingss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VitalSignss_InPatient",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pulse_BPH = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Urine = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Extremity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Glucose = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TempC = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TempMode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RespRate_MIN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Positions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Bowel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MEWs = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PainScore = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Systole_MM_Hg = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LevelOfConsciouseness = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OxygenSaturation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Diastole_MM_Hg = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    O2Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FallRisk = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Height = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Weight = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IPOP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BloodPressure_SYSTOLIC = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BloodPressure_DIASTOLIC = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CVP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TempF = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BP_MM_Hg = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    BloodTransfusionId = table.Column<int>(type: "int", nullable: false),
                    DonorId = table.Column<int>(type: "int", nullable: false),
                    pain = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    painLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    painDuration = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    painCharac = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    painFreq = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    painRad = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    painmodifie = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BMI = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VitalSignss_InPatient", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "VitalTypeGroups_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VitalTypeGroups_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WardCategorys_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WardCategorys_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WardPatientPrescriptionss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WardPharmacyID = table.Column<int>(type: "int", nullable: false),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    DrugID = table.Column<int>(type: "int", nullable: false),
                    Dosage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnitID = table.Column<int>(type: "int", nullable: false),
                    CurrentQty = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FerquencyID = table.Column<int>(type: "int", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PeriodTypeID = table.Column<int>(type: "int", nullable: false),
                    QTY = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SubStoreBatchId = table.Column<int>(type: "int", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TimeTake = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WitnessID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    PrescriptionTimeTake = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SkipReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrescriptionStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WardPatientPrescriptionss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WardPharmacyDetailss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WardPharmacyID = table.Column<int>(type: "int", nullable: false),
                    DrugID = table.Column<int>(type: "int", nullable: false),
                    Dosage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnitID = table.Column<int>(type: "int", nullable: false),
                    CurrentQty = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FerquencyID = table.Column<int>(type: "int", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PeriodTypeID = table.Column<int>(type: "int", nullable: false),
                    QTY = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SubStoreBatchId = table.Column<int>(type: "int", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WardPharmacyDetailss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WardPharmacyPaymentDetailss_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    WardPharmacyId = table.Column<int>(type: "int", nullable: false),
                    ReceiptNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PaymentModeId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyId = table.Column<int>(type: "int", nullable: false),
                    InstrumentNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InstrumentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Bank = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WardPharmacyPaymentDetailss_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WardPharmacys_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubStoreID = table.Column<int>(type: "int", nullable: false),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    ReceiptNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceiptDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentTypeID = table.Column<int>(type: "int", nullable: false),
                    DoctorID = table.Column<int>(type: "int", nullable: false),
                    PackageID = table.Column<int>(type: "int", nullable: false),
                    SponsorID = table.Column<int>(type: "int", nullable: false),
                    IPNO = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrescriptionNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrescriptionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SponsorCatagoryID = table.Column<int>(type: "int", nullable: false),
                    SelfPayAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CollectedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Balance = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Return = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OpenInvoiceNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InvoiceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CoPay = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CoPay2 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsCoPayByPer = table.Column<bool>(type: "bit", nullable: false),
                    DischMedication = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    EnteredBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WardPharmacys_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Wards_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WardTypeId = table.Column<int>(type: "int", nullable: false),
                    WardCategoryId = table.Column<int>(type: "int", nullable: false),
                    CostCenterId = table.Column<int>(type: "int", nullable: false),
                    PharmcyId = table.Column<int>(type: "int", nullable: false),
                    InventoryId = table.Column<int>(type: "int", nullable: false),
                    MedicalServiceIdz = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Active = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsSecondaryWard = table.Column<bool>(type: "bit", nullable: false),
                    IsEndOfDay = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FloorID = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpecialityID = table.Column<int>(type: "int", nullable: false),
                    IsICU = table.Column<bool>(type: "bit", nullable: false),
                    NurseID = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    IntensiveCareType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEmergency = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wards_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WardTypes_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WardTypes_InPatient", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WishLists_InPatient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CostCenterCompId = table.Column<int>(type: "int", nullable: false),
                    CompanyID = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishLists_InPatient", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ABGss_Emergency");

            migrationBuilder.DropTable(
                name: "AccommodationTypes_InPatient");

            migrationBuilder.DropTable(
                name: "AccommodationTypess_InPatient");

            migrationBuilder.DropTable(
                name: "Additivess_Pharmacy");

            migrationBuilder.DropTable(
                name: "AdministrationSites_Pharmacy");

            migrationBuilder.DropTable(
                name: "AdminstrationMasters_Pharmacy");

            migrationBuilder.DropTable(
                name: "AdmissionCategorys_InPatient");

            migrationBuilder.DropTable(
                name: "AdmissionPurposes_InPatient");

            migrationBuilder.DropTable(
                name: "AdmissionRequests_InPatient");

            migrationBuilder.DropTable(
                name: "AdmitPatientss_InPatient");

            migrationBuilder.DropTable(
                name: "AdmitPatientToNewMedUnits_InPatient");

            migrationBuilder.DropTable(
                name: "AdmittingPatients_OutPatient");

            migrationBuilder.DropTable(
                name: "AllergyDetailss_OutPatient");

            migrationBuilder.DropTable(
                name: "AllergyMasters_OutPatient");

            migrationBuilder.DropTable(
                name: "AllowUsersToShowReportss_Laboratory");

            migrationBuilder.DropTable(
                name: "Antibioticss_Laboratory");

            migrationBuilder.DropTable(
                name: "ArrivalTypes_Emergency");

            migrationBuilder.DropTable(
                name: "Batchess_Pharmacy");

            migrationBuilder.DropTable(
                name: "BedLockPurposes_InPatient");

            migrationBuilder.DropTable(
                name: "BedRenewals_InPatient");

            migrationBuilder.DropTable(
                name: "Beds_InPatient");

            migrationBuilder.DropTable(
                name: "BedStatusChangeTrackers_InPatient");

            migrationBuilder.DropTable(
                name: "BedStatuss_InPatient");

            migrationBuilder.DropTable(
                name: "BedSwaps_InPatient");

            migrationBuilder.DropTable(
                name: "BedTrackers_InPatient");

            migrationBuilder.DropTable(
                name: "BedTypes_InPatient");

            migrationBuilder.DropTable(
                name: "BedWardArrangements_InPatient");

            migrationBuilder.DropTable(
                name: "Blacklists_InPatient");

            migrationBuilder.DropTable(
                name: "BloodBankInventoryBloodGroups_BloodBank");

            migrationBuilder.DropTable(
                name: "BloodBankInventorys_BloodBank");

            migrationBuilder.DropTable(
                name: "BloodBankLPODetails_BloodBank");

            migrationBuilder.DropTable(
                name: "BloodBankLPOHeaders_BloodBank");

            migrationBuilder.DropTable(
                name: "BloodBankSettingss_BloodBank");

            migrationBuilder.DropTable(
                name: "BloodBankss_BloodBank");

            migrationBuilder.DropTable(
                name: "BloodGroupCompatables_BloodBank");

            migrationBuilder.DropTable(
                name: "BloodGroups_BloodBank");

            migrationBuilder.DropTable(
                name: "BloodProducts_BloodBank");

            migrationBuilder.DropTable(
                name: "BloodServicePrices_BloodBank");

            migrationBuilder.DropTable(
                name: "BloodStreamDetailss_BloodBank");

            migrationBuilder.DropTable(
                name: "BloodStreams_BloodBank");

            migrationBuilder.DropTable(
                name: "BloodTestingResultDetails_BloodBank");

            migrationBuilder.DropTable(
                name: "BloodTestingResultMasters_BloodBank");

            migrationBuilder.DropTable(
                name: "BloodTestings_BloodBank");

            migrationBuilder.DropTable(
                name: "BloodTransferRequests_BloodBank");

            migrationBuilder.DropTable(
                name: "BloodTransfusions_BloodBank");

            migrationBuilder.DropTable(
                name: "BloodTransfustionActiontakens_BloodBank");

            migrationBuilder.DropTable(
                name: "Borrowings_Pharmacy");

            migrationBuilder.DropTable(
                name: "Brandss_Pharmacy");

            migrationBuilder.DropTable(
                name: "Buildingss_InPatient");

            migrationBuilder.DropTable(
                name: "CafeteriaChargess_InPatient");

            migrationBuilder.DropTable(
                name: "Campaigns_BloodBank");

            migrationBuilder.DropTable(
                name: "CancelAdmissions_InPatient");

            migrationBuilder.DropTable(
                name: "CancelAdmitPatientInAnotherMedicalUnits_InPatient");

            migrationBuilder.DropTable(
                name: "CancelDischargePatientInAnotherMedicalUnits_InPatient");

            migrationBuilder.DropTable(
                name: "CancelDischargeReasons_InPatient");

            migrationBuilder.DropTable(
                name: "CancelDischarges_InPatient");

            migrationBuilder.DropTable(
                name: "CancelIntialDischarges_InPatient");

            migrationBuilder.DropTable(
                name: "CancellationOfPatientRequests_InPatient");

            migrationBuilder.DropTable(
                name: "CancelTypesMasters_InPatient");

            migrationBuilder.DropTable(
                name: "Cardiac_Electrphysiologys_InPatient");

            migrationBuilder.DropTable(
                name: "CardiacCatheterizations_InPatient");

            migrationBuilder.DropTable(
                name: "CasePrioritys_Emergency");

            migrationBuilder.DropTable(
                name: "ChangePatientDoctors_InPatient");

            migrationBuilder.DropTable(
                name: "clabsibundleDetailss_BloodBank");

            migrationBuilder.DropTable(
                name: "ClabsiBundles_BloodBank");

            migrationBuilder.DropTable(
                name: "ClinicalServicess_OutPatient");

            migrationBuilder.DropTable(
                name: "ClinicalSnomeds_OutPatient");

            migrationBuilder.DropTable(
                name: "ClinicLocations_OutPatient");

            migrationBuilder.DropTable(
                name: "ClinicProceduress_OutPatient");

            migrationBuilder.DropTable(
                name: "ClinicSchedules_OutPatient");

            migrationBuilder.DropTable(
                name: "ClinicSetups_OutPatient");

            migrationBuilder.DropTable(
                name: "ClinicTypes_OutPatient");

            migrationBuilder.DropTable(
                name: "ClottingTimeDetailss_InPatient");

            migrationBuilder.DropTable(
                name: "ClottingTimeMasters_InPatient");

            migrationBuilder.DropTable(
                name: "ComplainSetups_Emergency");

            migrationBuilder.DropTable(
                name: "ConsultaionEnums_InPatient");

            migrationBuilder.DropTable(
                name: "Consultation_Requests_InPatient");

            migrationBuilder.DropTable(
                name: "ConsultationSettings_OutPatient");

            migrationBuilder.DropTable(
                name: "ContainerTypes_Laboratory");

            migrationBuilder.DropTable(
                name: "ContraDrugss_Pharmacy");

            migrationBuilder.DropTable(
                name: "CoronaryInterventions_InPatient");

            migrationBuilder.DropTable(
                name: "CRTPImplantationReports_InPatient");

            migrationBuilder.DropTable(
                name: "CurrentMedications_InPatient");

            migrationBuilder.DropTable(
                name: "DailyCloseDates_OutPatient");

            migrationBuilder.DropTable(
                name: "DBloodBagss_BloodBank");

            migrationBuilder.DropTable(
                name: "DCAFs_InPatient");

            migrationBuilder.DropTable(
                name: "DeliveryDetailss_InPatient");

            migrationBuilder.DropTable(
                name: "Deliverys_InPatient");

            migrationBuilder.DropTable(
                name: "DeliveryTerms_Pharmacy");

            migrationBuilder.DropTable(
                name: "DeliveryTypes_InPatient");

            migrationBuilder.DropTable(
                name: "DespatchingDetailss_Pharmacy");

            migrationBuilder.DropTable(
                name: "DestinationOfPatients_InPatient");

            migrationBuilder.DropTable(
                name: "DestroyingExpiryItemsDetailss_Pharmacy");

            migrationBuilder.DropTable(
                name: "DestroyingExpiryItemsHeaders_Pharmacy");

            migrationBuilder.DropTable(
                name: "DevicesDefinations_Radiology");

            migrationBuilder.DropTable(
                name: "DeviceServicess_Radiology");

            migrationBuilder.DropTable(
                name: "DeviceTechnicians_Radiology");

            migrationBuilder.DropTable(
                name: "DevicsSchedules_Radiology");

            migrationBuilder.DropTable(
                name: "DiagnosisAnswerss_OutPatient");

            migrationBuilder.DropTable(
                name: "DiagnosisGroupss_OutPatient");

            migrationBuilder.DropTable(
                name: "DiagnosisMains_OutPatient");

            migrationBuilder.DropTable(
                name: "DiagnosisQuestions_OutPatient");

            migrationBuilder.DropTable(
                name: "DiagnosisSubs_OutPatient");

            migrationBuilder.DropTable(
                name: "DirectAddationDetailss_Pharmacy");

            migrationBuilder.DropTable(
                name: "DirectAddationHeaders_Pharmacy");

            migrationBuilder.DropTable(
                name: "DirectRad_Diagnosiss_Radiology");

            migrationBuilder.DropTable(
                name: "DirectSubstractDetailss_Pharmacy");

            migrationBuilder.DropTable(
                name: "DirectSubstractHeaders_Pharmacy");

            migrationBuilder.DropTable(
                name: "Discharge_Orders_InPatient");

            migrationBuilder.DropTable(
                name: "DischargeReasons_InPatient");

            migrationBuilder.DropTable(
                name: "DischargeSummaryICDs_InPatient");

            migrationBuilder.DropTable(
                name: "DischargeSummarys_InPatient");

            migrationBuilder.DropTable(
                name: "DischargeTypes_Emergency");

            migrationBuilder.DropTable(
                name: "DischargeTypes_InPatient");

            migrationBuilder.DropTable(
                name: "Disease_InfectionTypess_OutPatient");

            migrationBuilder.DropTable(
                name: "DiseaseCategorys_OutPatient");

            migrationBuilder.DropTable(
                name: "DiseaseMasters_OutPatient");

            migrationBuilder.DropTable(
                name: "DispenseBloodBagss_BloodBank");

            migrationBuilder.DropTable(
                name: "DispenseDrugsDetailss_Pharmacy");

            migrationBuilder.DropTable(
                name: "DispenseDrugsHeaders_Pharmacy");

            migrationBuilder.DropTable(
                name: "Dobutamine_Stress_Echocardiography_Commentss_InPatient");

            migrationBuilder.DropTable(
                name: "Dobutamine_Stress_Echocardiography_MGMs_InPatient");

            migrationBuilder.DropTable(
                name: "Dobutamine_Stress_Echocardiographys_InPatient");

            migrationBuilder.DropTable(
                name: "DoctorGeneralSchedules_OutPatient");

            migrationBuilder.DropTable(
                name: "DoctorInstructionClassifications_InPatient");

            migrationBuilder.DropTable(
                name: "DoctorNotes_InPatient");

            migrationBuilder.DropTable(
                name: "DoctorRemarksAndCommentss_OutPatient");

            migrationBuilder.DropTable(
                name: "DoctorSchedules_Emergency");

            migrationBuilder.DropTable(
                name: "DoctorsPersonalLists_OutPatient");

            migrationBuilder.DropTable(
                name: "DoctorTransfers_InPatient");

            migrationBuilder.DropTable(
                name: "DonationInfos_BloodBank");

            migrationBuilder.DropTable(
                name: "DonationInvestgationMasters_BloodBank");

            migrationBuilder.DropTable(
                name: "DonationRestrictions_BloodBank");

            migrationBuilder.DropTable(
                name: "DonorQuestionnaireDetailss_BloodBank");

            migrationBuilder.DropTable(
                name: "DonorRegistrations_BloodBank");

            migrationBuilder.DropTable(
                name: "DonorVitalSignss_BloodBank");

            migrationBuilder.DropTable(
                name: "DosageFrequencyLinks_Pharmacy");

            migrationBuilder.DropTable(
                name: "DosageSessions_Pharmacy");

            migrationBuilder.DropTable(
                name: "DosageUniteForms_Pharmacy");

            migrationBuilder.DropTable(
                name: "Drug_manufacturerss_Pharmacy");

            migrationBuilder.DropTable(
                name: "DrugAdminModes_Pharmacy");

            migrationBuilder.DropTable(
                name: "DrugAlternatives_Pharmacy");

            migrationBuilder.DropTable(
                name: "DrugCharts_InPatient");

            migrationBuilder.DropTable(
                name: "DrugClassDets_Pharmacy");

            migrationBuilder.DropTable(
                name: "DrugClassificationPeroids_Pharmacy");

            migrationBuilder.DropTable(
                name: "DrugClassifications_Pharmacy");

            migrationBuilder.DropTable(
                name: "DrugClasss_Pharmacy");

            migrationBuilder.DropTable(
                name: "DrugFormss_Pharmacy");

            migrationBuilder.DropTable(
                name: "DrugGroupMasters_Pharmacy");

            migrationBuilder.DropTable(
                name: "DrugPreparationTemplates_Pharmacy");

            migrationBuilder.DropTable(
                name: "Drugss_Pharmacy");

            migrationBuilder.DropTable(
                name: "DrugsWithInfusionRateDetails_StatusHistorys_OutPatient");

            migrationBuilder.DropTable(
                name: "DrugsWithInfusionRateDetailsDetailss_OutPatient");

            migrationBuilder.DropTable(
                name: "DrugsWithInfusionRateDetailss_OutPatient");

            migrationBuilder.DropTable(
                name: "DrugsWithInfusionRateHeaders_OutPatient");

            migrationBuilder.DropTable(
                name: "DrugTemplates_Pharmacy");

            migrationBuilder.DropTable(
                name: "DrugTypess_Pharmacy");

            migrationBuilder.DropTable(
                name: "ECGReports_InPatient");

            migrationBuilder.DropTable(
                name: "EmergencyDetailss_Emergency");

            migrationBuilder.DropTable(
                name: "EmergencyOrganizations_Emergency");

            migrationBuilder.DropTable(
                name: "EmergencySchedules_Emergency");

            migrationBuilder.DropTable(
                name: "EmergencyUnit_Historys_Emergency");

            migrationBuilder.DropTable(
                name: "EmergencyUnits_Emergency");

            migrationBuilder.DropTable(
                name: "EmergencyVisits_Emergency");

            migrationBuilder.DropTable(
                name: "EscortDetailss_InPatient");

            migrationBuilder.DropTable(
                name: "EscortEnterances_InPatient");

            migrationBuilder.DropTable(
                name: "Escorts_InPatient");

            migrationBuilder.DropTable(
                name: "EstimatedmissionDetailss_InPatient");

            migrationBuilder.DropTable(
                name: "Estimatedmissions_InPatient");

            migrationBuilder.DropTable(
                name: "ExamDeliverys_Radiology");

            migrationBuilder.DropTable(
                name: "ExamRequests_Radiology");

            migrationBuilder.DropTable(
                name: "Examss_Radiology");

            migrationBuilder.DropTable(
                name: "ExpenseMasters_Pharmacy");

            migrationBuilder.DropTable(
                name: "ExternalAgenciess_Laboratory");

            migrationBuilder.DropTable(
                name: "ExternalFacilityDetailss_BloodBank");

            migrationBuilder.DropTable(
                name: "ExternalFacilityHeaders_BloodBank");

            migrationBuilder.DropTable(
                name: "EyesightMeasurements_InPatient");

            migrationBuilder.DropTable(
                name: "FacilityMasters_BloodBank");

            migrationBuilder.DropTable(
                name: "Farmss_Laboratory");

            migrationBuilder.DropTable(
                name: "FarmTypes_Laboratory");

            migrationBuilder.DropTable(
                name: "FastEmergancys_Emergency");

            migrationBuilder.DropTable(
                name: "FindingFlags_InPatient");

            migrationBuilder.DropTable(
                name: "Findings_InPatient");

            migrationBuilder.DropTable(
                name: "FlagSettingss_Radiology");

            migrationBuilder.DropTable(
                name: "Floorss_InPatient");

            migrationBuilder.DropTable(
                name: "Frequenciess_Pharmacy");

            migrationBuilder.DropTable(
                name: "FrequencyMasters_InPatient");

            migrationBuilder.DropTable(
                name: "GeneralNursingCarePlan_TagsValuess_InPatient");

            migrationBuilder.DropTable(
                name: "GeneralNursingCarePlanHeaders_InPatient");

            migrationBuilder.DropTable(
                name: "GeneralSetups_OutPatient");

            migrationBuilder.DropTable(
                name: "GenericNamesReplacements_Pharmacy");

            migrationBuilder.DropTable(
                name: "GenericNamess_Pharmacy");

            migrationBuilder.DropTable(
                name: "GitImagess_InPatient");

            migrationBuilder.DropTable(
                name: "GoodsReceivedNoteDetailss_Pharmacy");

            migrationBuilder.DropTable(
                name: "GoodsReceivedNoteExpensess_Pharmacy");

            migrationBuilder.DropTable(
                name: "GoodsReceivedNoteHeaders_Pharmacy");

            migrationBuilder.DropTable(
                name: "GRN_LPOss_Pharmacy");

            migrationBuilder.DropTable(
                name: "GRNDetailsBatchesTransactionss_Pharmacy");

            migrationBuilder.DropTable(
                name: "HeadUpTiltDiagnosiss_InPatient");

            migrationBuilder.DropTable(
                name: "HeadUpTilts_InPatient");

            migrationBuilder.DropTable(
                name: "HeadUpTiltTableICDCodess_InPatient");

            migrationBuilder.DropTable(
                name: "HeadUpTiltTables_InPatient");

            migrationBuilder.DropTable(
                name: "HebaTests_InPatient");

            migrationBuilder.DropTable(
                name: "Holters_InPatient");

            migrationBuilder.DropTable(
                name: "ICDCodes_OutPatient");

            migrationBuilder.DropTable(
                name: "InfectiousDiseaseScreenings_InPatient");

            migrationBuilder.DropTable(
                name: "InfusionRate_Orders_OutPatient");

            migrationBuilder.DropTable(
                name: "InitialDischarges_InPatient");

            migrationBuilder.DropTable(
                name: "InitiateDischargeTypess_InPatient");

            migrationBuilder.DropTable(
                name: "InpatientSettings_InPatient");

            migrationBuilder.DropTable(
                name: "InternalConsumptionEntrys_Pharmacy");

            migrationBuilder.DropTable(
                name: "InternalConsumptions_Pharmacy");

            migrationBuilder.DropTable(
                name: "InventoryAdjustmentEntrys_BloodBank");

            migrationBuilder.DropTable(
                name: "InventoryAdjustments_BloodBank");

            migrationBuilder.DropTable(
                name: "InvestigationRequestDetailss_OutPatient");

            migrationBuilder.DropTable(
                name: "InvestigationRequestHeaders_OutPatient");

            migrationBuilder.DropTable(
                name: "IssueRequestdetailss_Pharmacy");

            migrationBuilder.DropTable(
                name: "IssueRequests_Pharmacy");

            migrationBuilder.DropTable(
                name: "IssuetoDepartmentEntrys_Pharmacy");

            migrationBuilder.DropTable(
                name: "IssuetoDepartmentReturnEntrys_Pharmacy");

            migrationBuilder.DropTable(
                name: "IssuetoDepartmentReturns_Pharmacy");

            migrationBuilder.DropTable(
                name: "IssuetoDepartments_Pharmacy");

            migrationBuilder.DropTable(
                name: "IssuetoInventoryEntrys_BloodBank");

            migrationBuilder.DropTable(
                name: "IssuetoInventorys_BloodBank");

            migrationBuilder.DropTable(
                name: "LabAnalyzerss_Laboratory");

            migrationBuilder.DropTable(
                name: "LaboratorySettings_Laboratory");

            migrationBuilder.DropTable(
                name: "LabsDevicess_Laboratory");

            migrationBuilder.DropTable(
                name: "LabServiceItemLinkMasters_Laboratory");

            migrationBuilder.DropTable(
                name: "LabServicesPeriods_Laboratory");

            migrationBuilder.DropTable(
                name: "Labss_Laboratory");

            migrationBuilder.DropTable(
                name: "LabsStoress_Laboratory");

            migrationBuilder.DropTable(
                name: "LabsTechnicanss_Laboratory");

            migrationBuilder.DropTable(
                name: "LabTestItems_Laboratory");

            migrationBuilder.DropTable(
                name: "LabUnits_Laboratory");

            migrationBuilder.DropTable(
                name: "LegalStatusMasters_Pharmacy");

            migrationBuilder.DropTable(
                name: "LinkCostEstimations_InPatient");

            migrationBuilder.DropTable(
                name: "LocalPurchaseCancelations_Pharmacy");

            migrationBuilder.DropTable(
                name: "LocalPurchaseOrderDetailss_Pharmacy");

            migrationBuilder.DropTable(
                name: "LocalPurchaseOrderExpensess_Pharmacy");

            migrationBuilder.DropTable(
                name: "LocalPurchaseOrderHeaders_Pharmacy");

            migrationBuilder.DropTable(
                name: "LocationDefinations_Radiology");

            migrationBuilder.DropTable(
                name: "LPOApprovingAuthorityHeaders_Pharmacy");

            migrationBuilder.DropTable(
                name: "LPOApprovingAuthoritys_Pharmacy");

            migrationBuilder.DropTable(
                name: "MainSolutions_Pharmacy");

            migrationBuilder.DropTable(
                name: "MaintenanceTypess_InPatient");

            migrationBuilder.DropTable(
                name: "ManualGroupTransfers_OutPatient");

            migrationBuilder.DropTable(
                name: "MedicalConsumablesDetailss_OutPatient");

            migrationBuilder.DropTable(
                name: "MedicalConsumablesHeaders_OutPatient");

            migrationBuilder.DropTable(
                name: "MedicalObservation_CCU_MICUs_InPatient");

            migrationBuilder.DropTable(
                name: "MedicalObservation_Gits_InPatient");

            migrationBuilder.DropTable(
                name: "MedicalObservation_MedicationHistorys_InPatient");

            migrationBuilder.DropTable(
                name: "MedicalObservation_Obs_Gyns_InPatient");

            migrationBuilder.DropTable(
                name: "MedicalObservation_Paediatricss_InPatient");

            migrationBuilder.DropTable(
                name: "MedicalObservation_PastHistorys_InPatient");

            migrationBuilder.DropTable(
                name: "MedicalObservation_Plastics_InPatient");

            migrationBuilder.DropTable(
                name: "MedicalObservation_TagsValuess_InPatient");

            migrationBuilder.DropTable(
                name: "MedicalObservationDentals_InPatient");

            migrationBuilder.DropTable(
                name: "MedicalObservationDepartmentDetailss_InPatient");

            migrationBuilder.DropTable(
                name: "MedicalObservationICDCodess_InPatient");

            migrationBuilder.DropTable(
                name: "MedicalObservationOpticals_InPatient");

            migrationBuilder.DropTable(
                name: "MedicalObservations_InPatient");

            migrationBuilder.DropTable(
                name: "MedicationDispensingPeriod_GenericNamess_Pharmacy");

            migrationBuilder.DropTable(
                name: "MedicationDispensingPeriods_Pharmacy");

            migrationBuilder.DropTable(
                name: "MergeSampless_Laboratory");

            migrationBuilder.DropTable(
                name: "MostCommenInvestigationss_Radiology");

            migrationBuilder.DropTable(
                name: "MovingPatients_InPatient");

            migrationBuilder.DropTable(
                name: "MyocardialPerfusionImagings_InPatient");

            migrationBuilder.DropTable(
                name: "NationalVacations_OutPatient");

            migrationBuilder.DropTable(
                name: "NewBorns_InPatient");

            migrationBuilder.DropTable(
                name: "NormalEmergancys_Emergency");

            migrationBuilder.DropTable(
                name: "NurseAssessment_TagsValuess_InPatient");

            migrationBuilder.DropTable(
                name: "NurseAssessmentHeaders_InPatient");

            migrationBuilder.DropTable(
                name: "NurseStation_Drugs_InPatient");

            migrationBuilder.DropTable(
                name: "NursingAdmissionAssessments_InPatient");

            migrationBuilder.DropTable(
                name: "NursingAssessments_Emergency");

            migrationBuilder.DropTable(
                name: "NursingNotes_InPatient");

            migrationBuilder.DropTable(
                name: "OCAFs_InPatient");

            migrationBuilder.DropTable(
                name: "OnCallDoctorss_Emergency");

            migrationBuilder.DropTable(
                name: "OPDInternalTransfers_OutPatient");

            migrationBuilder.DropTable(
                name: "OpeningPharmacyDetailss_Pharmacy");

            migrationBuilder.DropTable(
                name: "OpeningPharmacyHeaders_Pharmacy");

            migrationBuilder.DropTable(
                name: "OpeningStockDetailss_Pharmacy");

            migrationBuilder.DropTable(
                name: "OpeningStockHeaders_Pharmacy");

            migrationBuilder.DropTable(
                name: "OperationRequests_InPatient");

            migrationBuilder.DropTable(
                name: "OperationRooms_InPatient");

            migrationBuilder.DropTable(
                name: "OperationTheatreDailyDutys_InPatient");

            migrationBuilder.DropTable(
                name: "OperationTheatres_InPatient");

            migrationBuilder.DropTable(
                name: "OperationWards_InPatient");

            migrationBuilder.DropTable(
                name: "OrderCategoryDetails_InPatient");

            migrationBuilder.DropTable(
                name: "OrderCategoryMasters_InPatient");

            migrationBuilder.DropTable(
                name: "OrderMemberShips_InPatient");

            migrationBuilder.DropTable(
                name: "OrderParameters_InPatient");

            migrationBuilder.DropTable(
                name: "OrdersEnterys_InPatient");

            migrationBuilder.DropTable(
                name: "OrderTypes_InPatient");

            migrationBuilder.DropTable(
                name: "OrganismAntibioticSensitivitys_Laboratory");

            migrationBuilder.DropTable(
                name: "Organismss_Laboratory");

            migrationBuilder.DropTable(
                name: "OtherHospitalss_Pharmacy");

            migrationBuilder.DropTable(
                name: "OutRersourceDispenseBagss_BloodBank");

            migrationBuilder.DropTable(
                name: "OutResourcess_BloodBank");

            migrationBuilder.DropTable(
                name: "OutResourceTypes_InPatient");

            migrationBuilder.DropTable(
                name: "PackagesRequestDetailss_OutPatient");

            migrationBuilder.DropTable(
                name: "PackageUnitMasters_Pharmacy");

            migrationBuilder.DropTable(
                name: "PageTagss_InPatient");

            migrationBuilder.DropTable(
                name: "Patient_Familys_InPatient");

            migrationBuilder.DropTable(
                name: "Patient_Farmss_Laboratory");

            migrationBuilder.DropTable(
                name: "PatientAllergyNews_InPatient");

            migrationBuilder.DropTable(
                name: "PatientAllergys_OutPatient");

            migrationBuilder.DropTable(
                name: "PatientAllergyUpdatess_OutPatient");

            migrationBuilder.DropTable(
                name: "PatientDietManagements_InPatient");

            migrationBuilder.DropTable(
                name: "PatientDiets_InPatient");

            migrationBuilder.DropTable(
                name: "PatientDischarges_InPatient");

            migrationBuilder.DropTable(
                name: "PatientFamilyEducations_InPatient");

            migrationBuilder.DropTable(
                name: "PatientFarm_Detailss_Laboratory");

            migrationBuilder.DropTable(
                name: "PatientOrderDetails_InPatient");

            migrationBuilder.DropTable(
                name: "PatientOrderMasters_InPatient");

            migrationBuilder.DropTable(
                name: "PatientRadReceptions_Radiology");

            migrationBuilder.DropTable(
                name: "PatientStatuss_Emergency");

            migrationBuilder.DropTable(
                name: "PatientTypes_InPatient");

            migrationBuilder.DropTable(
                name: "PatientVitalss_OutPatient");

            migrationBuilder.DropTable(
                name: "PaymentGroups_Pharmacy");

            migrationBuilder.DropTable(
                name: "PaymentSuppliers_Pharmacy");

            migrationBuilder.DropTable(
                name: "PaymentTermsMasters_Pharmacy");

            migrationBuilder.DropTable(
                name: "PaymentTermsSchedules_Pharmacy");

            migrationBuilder.DropTable(
                name: "PayPolicys_OutPatient");

            migrationBuilder.DropTable(
                name: "PermanentPacemakerImplantationReports_InPatient");

            migrationBuilder.DropTable(
                name: "PermittedStaffs_Laboratory");

            migrationBuilder.DropTable(
                name: "PharmacyPayments_Pharmacy");

            migrationBuilder.DropTable(
                name: "PharmacySettingss_Pharmacy");

            migrationBuilder.DropTable(
                name: "PharmacySupplierInvoicePayments_Pharmacy");

            migrationBuilder.DropTable(
                name: "PharmInstallationDoctorDegrees_Pharmacy");

            migrationBuilder.DropTable(
                name: "PharmInstallations_Pharmacy");

            migrationBuilder.DropTable(
                name: "PhysicalStockAdjustmentEntrys_Pharmacy");

            migrationBuilder.DropTable(
                name: "PhysicalStockAdjustments_Pharmacy");

            migrationBuilder.DropTable(
                name: "PostOPNursingCarePlan_TagsValuess_InPatient");

            migrationBuilder.DropTable(
                name: "PostOPNursingCarePlanHeaders_InPatient");

            migrationBuilder.DropTable(
                name: "PostRoomChargess_InPatient");

            migrationBuilder.DropTable(
                name: "PreAnesthesiaEvaluations_InPatient");

            migrationBuilder.DropTable(
                name: "PreOperativeMarkings_InPatient");

            migrationBuilder.DropTable(
                name: "PreOperatives_InPatient");

            migrationBuilder.DropTable(
                name: "PrepareVisitSlips_OutPatient");

            migrationBuilder.DropTable(
                name: "PrescriptionAbbreviations_Pharmacy");

            migrationBuilder.DropTable(
                name: "PrescriptionDetailss_InPatient");

            migrationBuilder.DropTable(
                name: "PrescriptionDispenseSettings_InPatient");

            migrationBuilder.DropTable(
                name: "Prescriptions_InPatient");

            migrationBuilder.DropTable(
                name: "PrescriptionsDetailss_OutPatient");

            migrationBuilder.DropTable(
                name: "PrescriptionsHeaders_OutPatient");

            migrationBuilder.DropTable(
                name: "ProceduresDetailss_OutPatient");

            migrationBuilder.DropTable(
                name: "ProceduresMasters_OutPatient");

            migrationBuilder.DropTable(
                name: "ProhibitedDrugDocss_Pharmacy");

            migrationBuilder.DropTable(
                name: "ProvisionalDiagnosiss_InPatient");

            migrationBuilder.DropTable(
                name: "PurchaseReturnDetailss_Pharmacy");

            migrationBuilder.DropTable(
                name: "PurchaseReturnHeaders_Pharmacy");

            migrationBuilder.DropTable(
                name: "RadiologyReceptionss_Radiology");

            migrationBuilder.DropTable(
                name: "RadReceptioniestSchedules_Radiology");

            migrationBuilder.DropTable(
                name: "RadReceptionProceduress_Radiology");

            migrationBuilder.DropTable(
                name: "RadResultImagess_Laboratory");

            migrationBuilder.DropTable(
                name: "RadResultImagess_Radiology");

            migrationBuilder.DropTable(
                name: "RayBodyLoactions_Radiology");

            migrationBuilder.DropTable(
                name: "ReceptionDevicsSchedules_Radiology");

            migrationBuilder.DropTable(
                name: "ReceptionStockss_Radiology");

            migrationBuilder.DropTable(
                name: "ReferralTypes_InPatient");

            migrationBuilder.DropTable(
                name: "ReFillPrescriptionss_OutPatient");

            migrationBuilder.DropTable(
                name: "RefundDrugsDetailss_Pharmacy");

            migrationBuilder.DropTable(
                name: "RefundDrugsHeaders_Pharmacy");

            migrationBuilder.DropTable(
                name: "RegisteringPackageinstallments_InPatient");

            migrationBuilder.DropTable(
                name: "RegisteringPackages_InPatient");

            migrationBuilder.DropTable(
                name: "RejectSamples_Laboratory");

            migrationBuilder.DropTable(
                name: "ReOrderHistoryDetailss_Pharmacy");

            migrationBuilder.DropTable(
                name: "ReOrderHistoryMasters_Pharmacy");

            migrationBuilder.DropTable(
                name: "RepetTypes_InPatient");

            migrationBuilder.DropTable(
                name: "RequestProceduresDetailss_InPatient");

            migrationBuilder.DropTable(
                name: "RequestProceduresHeaders_InPatient");

            migrationBuilder.DropTable(
                name: "RequestSuppliesDetailss_InPatient");

            migrationBuilder.DropTable(
                name: "RequestSuppliesDetailss_Pharmacy");

            migrationBuilder.DropTable(
                name: "RequestSuppliesHeaders_InPatient");

            migrationBuilder.DropTable(
                name: "RequestSuppliesHeaders_Pharmacy");

            migrationBuilder.DropTable(
                name: "RequestSupplyReturnDetailss_InPatient");

            migrationBuilder.DropTable(
                name: "RequestSupplyReturnDetailss_Pharmacy");

            migrationBuilder.DropTable(
                name: "RequestSupplyReturnHeaders_InPatient");

            migrationBuilder.DropTable(
                name: "RequestSupplyReturnHeaders_Pharmacy");

            migrationBuilder.DropTable(
                name: "ResultEntryDetails_Findingss_Laboratory");

            migrationBuilder.DropTable(
                name: "ResultEntryDetails_Findingss_Radiology");

            migrationBuilder.DropTable(
                name: "ResultEntryDetails_Laboratory");

            migrationBuilder.DropTable(
                name: "ResultEntryDetailss_Radiology");

            migrationBuilder.DropTable(
                name: "ResultEntryHeaders_Radiology");

            migrationBuilder.DropTable(
                name: "ResultEntrys_Laboratory");

            migrationBuilder.DropTable(
                name: "ResultRangesDetailss_Laboratory");

            migrationBuilder.DropTable(
                name: "ResultRangess_Laboratory");

            migrationBuilder.DropTable(
                name: "Resultss_Laboratory");

            migrationBuilder.DropTable(
                name: "ResultValueDetailss_Laboratory");

            migrationBuilder.DropTable(
                name: "ResultValueHeaders_Laboratory");

            migrationBuilder.DropTable(
                name: "ReturnDispenseBloodBagss_BloodBank");

            migrationBuilder.DropTable(
                name: "ReturningExpiryItemsDetailss_Pharmacy");

            migrationBuilder.DropTable(
                name: "ReturningExpiryItemsHeaders_Pharmacy");

            migrationBuilder.DropTable(
                name: "RiskTypes_Emergency");

            migrationBuilder.DropTable(
                name: "RoomAccommodationTypess_InPatient");

            migrationBuilder.DropTable(
                name: "Rooms_InPatient");

            migrationBuilder.DropTable(
                name: "RoomTransfers_InPatient");

            migrationBuilder.DropTable(
                name: "RoomTypes_InPatient");

            migrationBuilder.DropTable(
                name: "Routes_Pharmacy");

            migrationBuilder.DropTable(
                name: "SampleCollectionMedias_Laboratory");

            migrationBuilder.DropTable(
                name: "SampleEntrys_Laboratory");

            migrationBuilder.DropTable(
                name: "SamplesDispatchedDetailss_Laboratory");

            migrationBuilder.DropTable(
                name: "SamplesDispatchedHeaders_Laboratory");

            migrationBuilder.DropTable(
                name: "SamplesReceivedDetailss_Laboratory");

            migrationBuilder.DropTable(
                name: "SamplesReceivedHeaders_Laboratory");

            migrationBuilder.DropTable(
                name: "SamplesTransferes_Laboratory");

            migrationBuilder.DropTable(
                name: "SampleTypes_Laboratory");

            migrationBuilder.DropTable(
                name: "ScheduleAppointments_OutPatient");

            migrationBuilder.DropTable(
                name: "ScheduleOTs_InPatient");

            migrationBuilder.DropTable(
                name: "ScheduleSurgerys_InPatient");

            migrationBuilder.DropTable(
                name: "ScrapDetails_Pharmacy");

            migrationBuilder.DropTable(
                name: "ScrapHeaders_Pharmacy");

            migrationBuilder.DropTable(
                name: "ScrapReturnDetails_Pharmacy");

            migrationBuilder.DropTable(
                name: "ScrapReturnHeaders_Pharmacy");

            migrationBuilder.DropTable(
                name: "Sectionss_Laboratory");

            migrationBuilder.DropTable(
                name: "SelectionTypes_Laboratory");

            migrationBuilder.DropTable(
                name: "Service_ResultValueHeaders_Laboratory");

            migrationBuilder.DropTable(
                name: "ServicesRequestDetailss_OutPatient");

            migrationBuilder.DropTable(
                name: "ShiftTypes_Emergency");

            migrationBuilder.DropTable(
                name: "SpecialityGroupDetailss_OutPatient");

            migrationBuilder.DropTable(
                name: "SpecialityGroupMasters_OutPatient");

            migrationBuilder.DropTable(
                name: "SpecimenTypeAssociations_Laboratory");

            migrationBuilder.DropTable(
                name: "SpecimenTypess_Laboratory");

            migrationBuilder.DropTable(
                name: "SponsorshipConditionCostCenterss_OutPatient");

            migrationBuilder.DropTable(
                name: "SponsorshipConditionDrugss_OutPatient");

            migrationBuilder.DropTable(
                name: "SponsorshipConditionDrugTypess_OutPatient");

            migrationBuilder.DropTable(
                name: "SponsorshipConditions_OutPatient");

            migrationBuilder.DropTable(
                name: "SponsorshipConditionServicess_OutPatient");

            migrationBuilder.DropTable(
                name: "SponsorshipConditionSupplierGroupsItemss_OutPatient");

            migrationBuilder.DropTable(
                name: "SponsorshipConditionSupplierGroupss_OutPatient");

            migrationBuilder.DropTable(
                name: "StdDosages_Pharmacy");

            migrationBuilder.DropTable(
                name: "StockAdjustmentReasonss_Pharmacy");

            migrationBuilder.DropTable(
                name: "StockControlDetails_Pharmacy");

            migrationBuilder.DropTable(
                name: "StockControls_Pharmacy");

            migrationBuilder.DropTable(
                name: "StockTransferEntrys_Pharmacy");

            migrationBuilder.DropTable(
                name: "StockTransfers_Pharmacy");

            migrationBuilder.DropTable(
                name: "StrenghtUnitMasters_Pharmacy");

            migrationBuilder.DropTable(
                name: "Substore_Itemss_Pharmacy");

            migrationBuilder.DropTable(
                name: "SubstoreAuthoritys_Pharmacy");

            migrationBuilder.DropTable(
                name: "SubStoreBatchess_Pharmacy");

            migrationBuilder.DropTable(
                name: "SubStoresClassificationss_Pharmacy");

            migrationBuilder.DropTable(
                name: "Substoress_Pharmacy");

            migrationBuilder.DropTable(
                name: "SupplierContactss_Pharmacy");

            migrationBuilder.DropTable(
                name: "SupplierforDrugs_Pharmacy");

            migrationBuilder.DropTable(
                name: "SupplierRelatedCompaniess_Pharmacy");

            migrationBuilder.DropTable(
                name: "Supplierss_Pharmacy");

            migrationBuilder.DropTable(
                name: "SupplierTypes_Pharmacy");

            migrationBuilder.DropTable(
                name: "Surgerys_InPatient");

            migrationBuilder.DropTable(
                name: "SurgeryTypes_InPatient");

            migrationBuilder.DropTable(
                name: "Tagss_InPatient");

            migrationBuilder.DropTable(
                name: "TelephoneChargess_InPatient");

            migrationBuilder.DropTable(
                name: "Templates_Pharmacy");

            migrationBuilder.DropTable(
                name: "TemporaryDischargeTypes_InPatient");

            migrationBuilder.DropTable(
                name: "TemporaryExits_InPatient");

            migrationBuilder.DropTable(
                name: "TestDetailss_Laboratory");

            migrationBuilder.DropTable(
                name: "TestResultLinkingEntrys_Laboratory");

            migrationBuilder.DropTable(
                name: "TestResultLinkings_Laboratory");

            migrationBuilder.DropTable(
                name: "Testss_Laboratory");

            migrationBuilder.DropTable(
                name: "Testss_Radiology");

            migrationBuilder.DropTable(
                name: "TimeBoundServiceRequestDetailss_InPatient");

            migrationBuilder.DropTable(
                name: "TimeBoundServiceRequestHeaders_InPatient");

            migrationBuilder.DropTable(
                name: "TransferRequestDispenses_BloodBank");

            migrationBuilder.DropTable(
                name: "TransfuionProcessings_BloodBank");

            migrationBuilder.DropTable(
                name: "TriagCategoryItemss_Emergency");

            migrationBuilder.DropTable(
                name: "TriagCategorys_Emergency");

            migrationBuilder.DropTable(
                name: "UCAFs_InPatient");

            migrationBuilder.DropTable(
                name: "UnitConversionFactors_Pharmacy");

            migrationBuilder.DropTable(
                name: "Unitss_Pharmacy");

            migrationBuilder.DropTable(
                name: "UnitTemplates_Pharmacy");

            migrationBuilder.DropTable(
                name: "Vac_Clinics_OutPatient");

            migrationBuilder.DropTable(
                name: "VaccinationCharts_OutPatient");

            migrationBuilder.DropTable(
                name: "VaccinationDetailss_OutPatient");

            migrationBuilder.DropTable(
                name: "VaccinationMasters_OutPatient");

            migrationBuilder.DropTable(
                name: "VaccinationSchedules_OutPatient");

            migrationBuilder.DropTable(
                name: "VirtualClinics_InPatient");

            migrationBuilder.DropTable(
                name: "VitalParameters_Emergency");

            migrationBuilder.DropTable(
                name: "VitalParametersDeptWises_OutPatient");

            migrationBuilder.DropTable(
                name: "VitalParameterss_InPatient");

            migrationBuilder.DropTable(
                name: "VitalSettingss_InPatient");

            migrationBuilder.DropTable(
                name: "VitalSignss_InPatient");

            migrationBuilder.DropTable(
                name: "VitalTypeGroups_InPatient");

            migrationBuilder.DropTable(
                name: "WardCategorys_InPatient");

            migrationBuilder.DropTable(
                name: "WardPatientPrescriptionss_InPatient");

            migrationBuilder.DropTable(
                name: "WardPharmacyDetailss_InPatient");

            migrationBuilder.DropTable(
                name: "WardPharmacyPaymentDetailss_InPatient");

            migrationBuilder.DropTable(
                name: "WardPharmacys_InPatient");

            migrationBuilder.DropTable(
                name: "Wards_InPatient");

            migrationBuilder.DropTable(
                name: "WardTypes_InPatient");

            migrationBuilder.DropTable(
                name: "WishLists_InPatient");
        }
    }
}
