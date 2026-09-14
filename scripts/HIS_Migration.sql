-- HIS Data Migration Script
-- Auto-generated from SunCity_Clinics schema

DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000'; -- TODO: Set target TenantId
DECLARE @DefaultCompanyId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000'; -- TODO: Set target CompanyId

PRINT 'Migrating [ApplicationSetup].[CPTCode]...';
SET IDENTITY_INSERT [ApplicationSetup].[CPTCode] ON;
INSERT INTO [ApplicationSetup].[CPTCode] ([Id], [Code], [Description], [MediumDescription], [LargeDescription], [TenantId])
SELECT [Id], [Code], [Description], [MediumDescription], [LargeDescription], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[CPTCode];
SET IDENTITY_INSERT [ApplicationSetup].[CPTCode] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[City]...';
SET IDENTITY_INSERT [ApplicationSetup].[City] ON;
INSERT INTO [ApplicationSetup].[City] ([Id], [GovernorateId], [Name], [CityCode], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameEn], [TenantId])
SELECT [Id], [GovernorateId], [Name], [CityCode], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameEn], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[City];
SET IDENTITY_INSERT [ApplicationSetup].[City] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[ClassificationType]...';
SET IDENTITY_INSERT [ApplicationSetup].[ClassificationType] ON;
INSERT INTO [ApplicationSetup].[ClassificationType] ([Id], [Name], [NameAr], [TenantId])
SELECT [Id], [Name], [NameAr], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[ClassificationType];
SET IDENTITY_INSERT [ApplicationSetup].[ClassificationType] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[ClinicalCareDetails]...';
SET IDENTITY_INSERT [ApplicationSetup].[ClinicalCareDetails] ON;
INSERT INTO [ApplicationSetup].[ClinicalCareDetails] ([Id], [ClinicalCareID], [ServiceID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [ClinicalCareID], [ServiceID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[ClinicalCareDetails];
SET IDENTITY_INSERT [ApplicationSetup].[ClinicalCareDetails] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[ClinicalCareFunction]...';
SET IDENTITY_INSERT [ApplicationSetup].[ClinicalCareFunction] ON;
INSERT INTO [ApplicationSetup].[ClinicalCareFunction] ([Id], [Hematological], [liverfunction], [catdiacenzyms], [RenalFunctionTest], [electrolytes], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Hematological], [liverfunction], [catdiacenzyms], [RenalFunctionTest], [electrolytes], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[ClinicalCareFunction];
SET IDENTITY_INSERT [ApplicationSetup].[ClinicalCareFunction] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[ClinicalCare]...';
SET IDENTITY_INSERT [ApplicationSetup].[ClinicalCare] ON;
INSERT INTO [ApplicationSetup].[ClinicalCare] ([Id], [TypeId], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [TypeId], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[ClinicalCare];
SET IDENTITY_INSERT [ApplicationSetup].[ClinicalCare] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[CompanyContact]...';
SET IDENTITY_INSERT [ApplicationSetup].[CompanyContact] ON;
INSERT INTO [ApplicationSetup].[CompanyContact] ([Id], [CompanyId], [Name], [Designation], [ContactTypeId], [ContactDetails], [TenantId])
SELECT [Id], [CompanyId], [Name], [Designation], [ContactTypeId], [ContactDetails], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[CompanyContact];
SET IDENTITY_INSERT [ApplicationSetup].[CompanyContact] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[Company]...';
INSERT INTO [ApplicationSetup].[Company] ([Id], [HoldingCompany], [ParentCompanyId], [Name], [ReportName], [POBox], [CityId], [Emirate], [ZipCode], [CountryId], [Email], [WebSite], [Logo], [CurrencyId], [NameAr], [VatValue], [CompanyManagerEmployeeID], [taxfileNo], [license], [SerialNum], [Identifire], [CommonName], [TenantId])
SELECT [Id], [HoldingCompany], [ParentCompanyId], [Name], [ReportName], [POBox], [CityId], [Emirate], [ZipCode], [CountryId], [Email], [WebSite], [Logo], [CurrencyId], [NameAr], [VatValue], [CompanyManagerEmployeeID], [taxfileNo], [license], [SerialNum], [Identifire], [CommonName], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[Company];
GO

PRINT 'Migrating [ApplicationSetup].[Components]...';
SET IDENTITY_INSERT [ApplicationSetup].[Components] ON;
INSERT INTO [ApplicationSetup].[Components] ([ID], [ServiceID], [TypeID], [Quantity], [ComponentID], [LinkedServiceID], [TenantId])
SELECT [ID], [ServiceID], [TypeID], [Quantity], [ComponentID], [LinkedServiceID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[Components];
SET IDENTITY_INSERT [ApplicationSetup].[Components] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[ConfidentialityClassification]...';
SET IDENTITY_INSERT [ApplicationSetup].[ConfidentialityClassification] ON;
INSERT INTO [ApplicationSetup].[ConfidentialityClassification] ([Id], [Description], [ClassificationTypeId], [ApplicationRoleId], [DescriptionAr], [TenantId])
SELECT [Id], [Description], [ClassificationTypeId], [ApplicationRoleId], [DescriptionAr], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[ConfidentialityClassification];
SET IDENTITY_INSERT [ApplicationSetup].[ConfidentialityClassification] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[ContactType]...';
SET IDENTITY_INSERT [ApplicationSetup].[ContactType] ON;
INSERT INTO [ApplicationSetup].[ContactType] ([Id], [Name], [CompanyID], [TenantId])
SELECT [Id], [Name], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[ContactType];
SET IDENTITY_INSERT [ApplicationSetup].[ContactType] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[CostCenterCompanies]...';
SET IDENTITY_INSERT [ApplicationSetup].[CostCenterCompanies] ON;
INSERT INTO [ApplicationSetup].[CostCenterCompanies] ([Id], [CostCenterId], [CompanyId], [Applicable], [ViewAppointment], [TenantId])
SELECT [Id], [CostCenterId], [CompanyId], [Applicable], [ViewAppointment], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[CostCenterCompanies];
SET IDENTITY_INSERT [ApplicationSetup].[CostCenterCompanies] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[CostCenterType]...';
SET IDENTITY_INSERT [ApplicationSetup].[CostCenterType] ON;
INSERT INTO [ApplicationSetup].[CostCenterType] ([Id], [Name], [TenantId])
SELECT [Id], [Name], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[CostCenterType];
SET IDENTITY_INSERT [ApplicationSetup].[CostCenterType] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[CostCenter]...';
SET IDENTITY_INSERT [ApplicationSetup].[CostCenter] ON;
INSERT INTO [ApplicationSetup].[CostCenter] ([Id], [Code], [Name], [ConfidentialityClassificationId], [RevenueGeneration], [CostCenterTypeId], [CustomerId], [SupplierId], [CompanyID], [NameAr], [apprivat], [NameKa], [TenantId])
SELECT [Id], [Code], [Name], [ConfidentialityClassificationId], [RevenueGeneration], [CostCenterTypeId], [CustomerId], [SupplierId], [CompanyID], [NameAr], [apprivat], [NameKa], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[CostCenter];
SET IDENTITY_INSERT [ApplicationSetup].[CostCenter] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[CountryB]...';
SET IDENTITY_INSERT [ApplicationSetup].[CountryB] ON;
INSERT INTO [ApplicationSetup].[CountryB] ([Id], [Name], [NameEn], [NABCountryBCode], [NationalityID], [Idx], [TenantId])
SELECT [Id], [Name], [NameEn], [NABCountryBCode], [NationalityID], [Idx], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[CountryB];
SET IDENTITY_INSERT [ApplicationSetup].[CountryB] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[Country]...';
SET IDENTITY_INSERT [ApplicationSetup].[Country] ON;
INSERT INTO [ApplicationSetup].[Country] ([Id], [Name], [NameEn], [NABCountryCode], [NationalityID], [Idx], [TenantId])
SELECT [Id], [Name], [NameEn], [NABCountryCode], [NationalityID], [Idx], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[Country];
SET IDENTITY_INSERT [ApplicationSetup].[Country] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[CurrencyRate]...';
SET IDENTITY_INSERT [ApplicationSetup].[CurrencyRate] ON;
INSERT INTO [ApplicationSetup].[CurrencyRate] ([Id], [CurrencyId], [DateFrom], [DateTo], [BuyRate], [SellRate], [TenantId])
SELECT [Id], [CurrencyId], [DateFrom], [DateTo], [BuyRate], [SellRate], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[CurrencyRate];
SET IDENTITY_INSERT [ApplicationSetup].[CurrencyRate] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[Currency]...';
SET IDENTITY_INSERT [ApplicationSetup].[Currency] ON;
INSERT INTO [ApplicationSetup].[Currency] ([Id], [Name], [NationalityId], [Symbol], [BasicUnit], [UnitConversion], [NameAr], [Rate], [IsDefault], [CountryId], [TenantId])
SELECT [Id], [Name], [NationalityId], [Symbol], [BasicUnit], [UnitConversion], [NameAr], [Rate], [IsDefault], [CountryId], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[Currency];
SET IDENTITY_INSERT [ApplicationSetup].[Currency] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[Customer]...';
SET IDENTITY_INSERT [ApplicationSetup].[Customer] ON;
INSERT INTO [ApplicationSetup].[Customer] ([Id], [Name], [TenantId])
SELECT [Id], [Name], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[Customer];
SET IDENTITY_INSERT [ApplicationSetup].[Customer] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[Dictionary]...';
SET IDENTITY_INSERT [ApplicationSetup].[Dictionary] ON;
INSERT INTO [ApplicationSetup].[Dictionary] ([Id], [Name], [NameEn], [SexId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ReligionId], [TenantId])
SELECT [Id], [Name], [NameEn], [SexId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ReligionId], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[Dictionary];
SET IDENTITY_INSERT [ApplicationSetup].[Dictionary] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[District]...';
SET IDENTITY_INSERT [ApplicationSetup].[District] ON;
INSERT INTO [ApplicationSetup].[District] ([Id], [DistrictName], [CityId], [Code], [DistrictNameEn], [TenantId])
SELECT [Id], [DistrictName], [CityId], [Code], [DistrictNameEn], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[District];
SET IDENTITY_INSERT [ApplicationSetup].[District] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[DoctorFees]...';
SET IDENTITY_INSERT [ApplicationSetup].[DoctorFees] ON;
INSERT INTO [ApplicationSetup].[DoctorFees] ([ID], [DoctorCategoryID], [ORTypeID], [AccommdationID], [TypeID], [DoctorFeesValue], [HospitalFeesValue], [ServiceID], [TenantId])
SELECT [ID], [DoctorCategoryID], [ORTypeID], [AccommdationID], [TypeID], [DoctorFeesValue], [HospitalFeesValue], [ServiceID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[DoctorFees];
SET IDENTITY_INSERT [ApplicationSetup].[DoctorFees] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[Doctor]...';
SET IDENTITY_INSERT [ApplicationSetup].[Doctor] ON;
INSERT INTO [ApplicationSetup].[Doctor] ([Id], [Name], [CostCenterId], [TenantId])
SELECT [Id], [Name], [CostCenterId], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[Doctor];
SET IDENTITY_INSERT [ApplicationSetup].[Doctor] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[ExcludeDoctorsHolidays]...';
SET IDENTITY_INSERT [ApplicationSetup].[ExcludeDoctorsHolidays] ON;
INSERT INTO [ApplicationSetup].[ExcludeDoctorsHolidays] ([Id], [HolidayId], [DoctorId], [Remarks], [TenantId])
SELECT [Id], [HolidayId], [DoctorId], [Remarks], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[ExcludeDoctorsHolidays];
SET IDENTITY_INSERT [ApplicationSetup].[ExcludeDoctorsHolidays] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[Governorate]...';
SET IDENTITY_INSERT [ApplicationSetup].[Governorate] ON;
INSERT INTO [ApplicationSetup].[Governorate] ([Id], [CountryId], [Name], [GovernorateCode], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameEn], [TenantId])
SELECT [Id], [CountryId], [Name], [GovernorateCode], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameEn], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[Governorate];
SET IDENTITY_INSERT [ApplicationSetup].[Governorate] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[Holiday]...';
SET IDENTITY_INSERT [ApplicationSetup].[Holiday] ON;
INSERT INTO [ApplicationSetup].[Holiday] ([Id], [HolidayDate], [Description], [TenantId])
SELECT [Id], [HolidayDate], [Description], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[Holiday];
SET IDENTITY_INSERT [ApplicationSetup].[Holiday] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[HospitalNews]...';
SET IDENTITY_INSERT [ApplicationSetup].[HospitalNews] ON;
INSERT INTO [ApplicationSetup].[HospitalNews] ([Id], [FromDate], [ToDate], [ArabicText], [EnglishText], [TenantId])
SELECT [Id], [FromDate], [ToDate], [ArabicText], [EnglishText], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[HospitalNews];
SET IDENTITY_INSERT [ApplicationSetup].[HospitalNews] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[IdentificationTypes]...';
SET IDENTITY_INSERT [ApplicationSetup].[IdentificationTypes] ON;
INSERT INTO [ApplicationSetup].[IdentificationTypes] ([Id], [Code], [Name], [NameEn], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [LengthText], [AcceptChar], [MilitaryIdentity], [NameRu], [CompanyID], [HLCode], [identitysystem], [NameKa], [TenantId])
SELECT [Id], [Code], [Name], [NameEn], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [LengthText], [AcceptChar], [MilitaryIdentity], [NameRu], [CompanyID], [HLCode], [identitysystem], [NameKa], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[IdentificationTypes];
SET IDENTITY_INSERT [ApplicationSetup].[IdentificationTypes] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[InstallMentDetails]...';
SET IDENTITY_INSERT [ApplicationSetup].[InstallMentDetails] ON;
INSERT INTO [ApplicationSetup].[InstallMentDetails] ([Id], [Name], [Amount], [Duration], [Period], [ServiceId], [TenantId])
SELECT [Id], [Name], [Amount], [Duration], [Period], [ServiceId], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[InstallMentDetails];
SET IDENTITY_INSERT [ApplicationSetup].[InstallMentDetails] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[InsuranceServices]...';
SET IDENTITY_INSERT [ApplicationSetup].[InsuranceServices] ON;
INSERT INTO [ApplicationSetup].[InsuranceServices] ([ID], [InsCompanyId], [ServiceId], [InsCode], [TenantId])
SELECT [ID], [InsCompanyId], [ServiceId], [InsCode], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[InsuranceServices];
SET IDENTITY_INSERT [ApplicationSetup].[InsuranceServices] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[InternalCommunication]...';
SET IDENTITY_INSERT [ApplicationSetup].[InternalCommunication] ON;
INSERT INTO [ApplicationSetup].[InternalCommunication] ([ID], [Code], [subject], [MailDescription], [EmailStatus], [EmployeeIDSender], [EmployeeIDReceiver], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [subject], [MailDescription], [EmailStatus], [EmployeeIDSender], [EmployeeIDReceiver], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[InternalCommunication];
SET IDENTITY_INSERT [ApplicationSetup].[InternalCommunication] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[InvestigationGroupDetail]...';
SET IDENTITY_INSERT [ApplicationSetup].[InvestigationGroupDetail] ON;
INSERT INTO [ApplicationSetup].[InvestigationGroupDetail] ([Id], [InvestigationGroup_Id], [Service_Id], [CompanyID], [TenantId])
SELECT [Id], [InvestigationGroup_Id], [Service_Id], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[InvestigationGroupDetail];
SET IDENTITY_INSERT [ApplicationSetup].[InvestigationGroupDetail] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[InvestigationGroup]...';
SET IDENTITY_INSERT [ApplicationSetup].[InvestigationGroup] ON;
INSERT INTO [ApplicationSetup].[InvestigationGroup] ([Id], [NameAr], [NameEn], [CompanyID], [TenantId])
SELECT [Id], [NameAr], [NameEn], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[InvestigationGroup];
SET IDENTITY_INSERT [ApplicationSetup].[InvestigationGroup] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[Item]...';
SET IDENTITY_INSERT [ApplicationSetup].[Item] ON;
INSERT INTO [ApplicationSetup].[Item] ([Id], [Name], [Code], [Price], [SalePrice], [TenantId])
SELECT [Id], [Name], [Code], [Price], [SalePrice], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[Item];
SET IDENTITY_INSERT [ApplicationSetup].[Item] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[LinkedItem]...';
SET IDENTITY_INSERT [ApplicationSetup].[LinkedItem] ON;
INSERT INTO [ApplicationSetup].[LinkedItem] ([ID], [ServiceID], [TypeID], [Price], [Value], [Total], [LinkedServiceID], [TenantId])
SELECT [ID], [ServiceID], [TypeID], [Price], [Value], [Total], [LinkedServiceID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[LinkedItem];
SET IDENTITY_INSERT [ApplicationSetup].[LinkedItem] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[MilitraryForce]...';
SET IDENTITY_INSERT [ApplicationSetup].[MilitraryForce] ON;
INSERT INTO [ApplicationSetup].[MilitraryForce] ([Id], [Code], [Name], [NameEn], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Code], [Name], [NameEn], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[MilitraryForce];
SET IDENTITY_INSERT [ApplicationSetup].[MilitraryForce] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[MilitraryRanks]...';
SET IDENTITY_INSERT [ApplicationSetup].[MilitraryRanks] ON;
INSERT INTO [ApplicationSetup].[MilitraryRanks] ([Id], [Code], [Name], [NameEn], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Code], [Name], [NameEn], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[MilitraryRanks];
SET IDENTITY_INSERT [ApplicationSetup].[MilitraryRanks] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[Nationality]...';
SET IDENTITY_INSERT [ApplicationSetup].[Nationality] ON;
INSERT INTO [ApplicationSetup].[Nationality] ([Id], [Name], [NameAr], [Citizen], [CountryCode], [TenantId])
SELECT [Id], [Name], [NameAr], [Citizen], [CountryCode], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[Nationality];
SET IDENTITY_INSERT [ApplicationSetup].[Nationality] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[NurseHead]...';
SET IDENTITY_INSERT [ApplicationSetup].[NurseHead] ON;
INSERT INTO [ApplicationSetup].[NurseHead] ([Id], [NurseID], [SpecialityID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [urgent], [Routine], [State], [CompanyID], [TenantId])
SELECT [Id], [NurseID], [SpecialityID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [urgent], [Routine], [State], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[NurseHead];
SET IDENTITY_INSERT [ApplicationSetup].[NurseHead] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[PackageDefType]...';
SET IDENTITY_INSERT [ApplicationSetup].[PackageDefType] ON;
INSERT INTO [ApplicationSetup].[PackageDefType] ([Id], [Name], [TenantId])
SELECT [Id], [Name], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[PackageDefType];
SET IDENTITY_INSERT [ApplicationSetup].[PackageDefType] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[PackageDrags]...';
SET IDENTITY_INSERT [ApplicationSetup].[PackageDrags] ON;
INSERT INTO [ApplicationSetup].[PackageDrags] ([Id], [DrugId], [ServiceId], [TenantId])
SELECT [Id], [DrugId], [ServiceId], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[PackageDrags];
SET IDENTITY_INSERT [ApplicationSetup].[PackageDrags] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[PackageServices]...';
SET IDENTITY_INSERT [ApplicationSetup].[PackageServices] ON;
INSERT INTO [ApplicationSetup].[PackageServices] ([Id], [ServiceId_Pack], [ServiceId], [PackageChange], [Qty], [PackageServiceRelef], [TPackageServiceRelefe], [TenantId])
SELECT [Id], [ServiceId_Pack], [ServiceId], [PackageChange], [Qty], [PackageServiceRelef], [TPackageServiceRelefe], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[PackageServices];
SET IDENTITY_INSERT [ApplicationSetup].[PackageServices] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[PackageSupItem]...';
SET IDENTITY_INSERT [ApplicationSetup].[PackageSupItem] ON;
INSERT INTO [ApplicationSetup].[PackageSupItem] ([Id], [ItemId], [ServiceId], [TenantId])
SELECT [Id], [ItemId], [ServiceId], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[PackageSupItem];
SET IDENTITY_INSERT [ApplicationSetup].[PackageSupItem] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[PagePrefix]...';
SET IDENTITY_INSERT [ApplicationSetup].[PagePrefix] ON;
INSERT INTO [ApplicationSetup].[PagePrefix] ([Id], [Name], [Prefix], [Length], [ModuleName], [TenantId])
SELECT [Id], [Name], [Prefix], [Length], [ModuleName], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[PagePrefix];
SET IDENTITY_INSERT [ApplicationSetup].[PagePrefix] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[PaymentRole]...';
SET IDENTITY_INSERT [ApplicationSetup].[PaymentRole] ON;
INSERT INTO [ApplicationSetup].[PaymentRole] ([Id], [Inpatient], [Outpatient], [CreatedDate], [CreatedBy], [ModifiedDate], [ModifiedBy], [TenantId])
SELECT [Id], [Inpatient], [Outpatient], [CreatedDate], [CreatedBy], [ModifiedDate], [ModifiedBy], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[PaymentRole];
SET IDENTITY_INSERT [ApplicationSetup].[PaymentRole] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[QRImage]...';
SET IDENTITY_INSERT [ApplicationSetup].[QRImage] ON;
INSERT INTO [ApplicationSetup].[QRImage] ([Id], [Image], [InvoiceId], [PatID], [ReportName], [AssetID], [TenantId])
SELECT [Id], [Image], [InvoiceId], [PatID], [ReportName], [AssetID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[QRImage];
SET IDENTITY_INSERT [ApplicationSetup].[QRImage] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[Report_Generator]...';
SET IDENTITY_INSERT [ApplicationSetup].[Report_Generator] ON;
INSERT INTO [ApplicationSetup].[Report_Generator] ([Id], [ReportName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [ReportName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[Report_Generator];
SET IDENTITY_INSERT [ApplicationSetup].[Report_Generator] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[Report_Generator_Columns]...';
SET IDENTITY_INSERT [ApplicationSetup].[Report_Generator_Columns] ON;
INSERT INTO [ApplicationSetup].[Report_Generator_Columns] ([id], [Report_Generator_ID], [ColumnName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [DisplayName], [TenantId])
SELECT [id], [Report_Generator_ID], [ColumnName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [DisplayName], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[Report_Generator_Columns];
SET IDENTITY_INSERT [ApplicationSetup].[Report_Generator_Columns] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[Report_Generator_Conditions]...';
SET IDENTITY_INSERT [ApplicationSetup].[Report_Generator_Conditions] ON;
INSERT INTO [ApplicationSetup].[Report_Generator_Conditions] ([id], [Report_Generator_ID], [ColumnName1], [ColumnName2], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [id], [Report_Generator_ID], [ColumnName1], [ColumnName2], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[Report_Generator_Conditions];
SET IDENTITY_INSERT [ApplicationSetup].[Report_Generator_Conditions] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[Report_Generator_Filters]...';
SET IDENTITY_INSERT [ApplicationSetup].[Report_Generator_Filters] ON;
INSERT INTO [ApplicationSetup].[Report_Generator_Filters] ([Id], [Report_Generator_ID], [ColumnName], [FilterType], [ReferenceTable], [ReferenceColumnID], [ReferenceColumnName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Report_Generator_ID], [ColumnName], [FilterType], [ReferenceTable], [ReferenceColumnID], [ReferenceColumnName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[Report_Generator_Filters];
SET IDENTITY_INSERT [ApplicationSetup].[Report_Generator_Filters] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[Report_Generator_Tables]...';
SET IDENTITY_INSERT [ApplicationSetup].[Report_Generator_Tables] ON;
INSERT INTO [ApplicationSetup].[Report_Generator_Tables] ([id], [Report_Generator_ID], [Tablename], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [id], [Report_Generator_ID], [Tablename], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[Report_Generator_Tables];
SET IDENTITY_INSERT [ApplicationSetup].[Report_Generator_Tables] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[RevenueType]...';
SET IDENTITY_INSERT [ApplicationSetup].[RevenueType] ON;
INSERT INTO [ApplicationSetup].[RevenueType] ([Id], [Name], [NameAr], [TenantId])
SELECT [Id], [Name], [NameAr], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[RevenueType];
SET IDENTITY_INSERT [ApplicationSetup].[RevenueType] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[ServicePackageDepartment]...';
SET IDENTITY_INSERT [ApplicationSetup].[ServicePackageDepartment] ON;
INSERT INTO [ApplicationSetup].[ServicePackageDepartment] ([Id], [PackageID], [DeptID], [CoverageLimit], [Discount], [Percentage], [CompanyID], [TenantId])
SELECT [Id], [PackageID], [DeptID], [CoverageLimit], [Discount], [Percentage], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[ServicePackageDepartment];
SET IDENTITY_INSERT [ApplicationSetup].[ServicePackageDepartment] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[Service]...';
SET IDENTITY_INSERT [ApplicationSetup].[Service] ON;
INSERT INTO [ApplicationSetup].[Service] ([Id], [CostCenterCompaniesId], [RecordName], [ParentId], [GroupId], [RevenueTypeId], [Code], [CPTcodeId], [DrugCoverageLimit], [ItemCoverageLimit], [StanderCharge], [IsDiscountsAllow], [ServiceCharge], [PackageId], [Discount], [IsDiscountByPercentage], [IsApproval], [AreServicesUnderItTempBlocked], [DoneByExternalAgencies], [IsEntryrEquired], [Description], [LastModifiedBy], [LastModifiedDate], [ApproxTimeByMinute], [RecordType], [ServiceType], [PackageDefTypeId], [CPTDescription], [TimeBound], [TimeBoundUnit], [ClassificationId], [GenericClinicID], [DoctorDegreeID], [SpecialtyId], [IsActive], [DoctorID], [OrderCategoryId], [NameAr], [NameEn], [GLMAccount], [CostCenterID], [gender], [RoleID], [GLSAccount], [ItemType], [price], [PriceCurr], [Cost], [CostCurr], [InvItem], [linkeditem], [orderItem], [Doctorfees], [packItem], [InpatientAccountID], [OutpatientAccountID], [ERAccountID], [OrderTimeDurationPerMIN], [OrderProcessingTimePerMIN], [MedicalSheetNameID], [SheetNameID], [OrderIsActive], [OrderPACSSYS], [OrderResultEntryRequired], [OrderApprovalScheduleRequired], [OrderKeepComment], [OrderInstructions], [OrderComment], [hasComponent], [IsOutPatient], [OtherCost], [OtherCostCurr], [PackageMinValue], [PricingType], [HasDevice], [taxable], [CompanyID], [NphiesCode], [NphiesURL], [isDevice], [TenantId])
SELECT [Id], [CostCenterCompaniesId], [RecordName], [ParentId], [GroupId], [RevenueTypeId], [Code], [CPTcodeId], [DrugCoverageLimit], [ItemCoverageLimit], [StanderCharge], [IsDiscountsAllow], [ServiceCharge], [PackageId], [Discount], [IsDiscountByPercentage], [IsApproval], [AreServicesUnderItTempBlocked], [DoneByExternalAgencies], [IsEntryrEquired], [Description], [LastModifiedBy], [LastModifiedDate], [ApproxTimeByMinute], [RecordType], [ServiceType], [PackageDefTypeId], [CPTDescription], [TimeBound], [TimeBoundUnit], [ClassificationId], [GenericClinicID], [DoctorDegreeID], [SpecialtyId], [IsActive], [DoctorID], [OrderCategoryId], [NameAr], [NameEn], [GLMAccount], [CostCenterID], [gender], [RoleID], [GLSAccount], [ItemType], [price], [PriceCurr], [Cost], [CostCurr], [InvItem], [linkeditem], [orderItem], [Doctorfees], [packItem], [InpatientAccountID], [OutpatientAccountID], [ERAccountID], [OrderTimeDurationPerMIN], [OrderProcessingTimePerMIN], [MedicalSheetNameID], [SheetNameID], [OrderIsActive], [OrderPACSSYS], [OrderResultEntryRequired], [OrderApprovalScheduleRequired], [OrderKeepComment], [OrderInstructions], [OrderComment], [hasComponent], [IsOutPatient], [OtherCost], [OtherCostCurr], [PackageMinValue], [PricingType], [HasDevice], [taxable], [CompanyID], [NphiesCode], [NphiesURL], [isDevice], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[Service];
SET IDENTITY_INSERT [ApplicationSetup].[Service] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[Setting]...';
SET IDENTITY_INSERT [ApplicationSetup].[Setting] ON;
INSERT INTO [ApplicationSetup].[Setting] ([Id], [Name], [Value], [Imag_logo], [Vat], [InventoryPolicy], [SalesTaxes], [AutoS_E], [MinDeposit], [ReorderDepositLevel], [Empolyee_CEO], [PeriodOnlineBooking], [CompanyID], [AlertPeriod_OnlineBooking_Minute], [CancelPeriod_OnlineBooking_Minute], [OPDTarget], [IPTarget], [ERTarget], [ORTarget], [LabTarget], [RadTarget], [PharmacyTarget], [HoldingtaxPercentage], [HospitalName], [consent], [chkPlatform], [CostCenterCompanyId], [TenantId])
SELECT [Id], [Name], [Value], [Imag_logo], [Vat], [InventoryPolicy], [SalesTaxes], [AutoS_E], [MinDeposit], [ReorderDepositLevel], [Empolyee_CEO], [PeriodOnlineBooking], [CompanyID], [AlertPeriod_OnlineBooking_Minute], [CancelPeriod_OnlineBooking_Minute], [OPDTarget], [IPTarget], [ERTarget], [ORTarget], [LabTarget], [RadTarget], [PharmacyTarget], [HoldingtaxPercentage], [HospitalName], [consent], [chkPlatform], [CostCenterCompanyId], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[Setting];
SET IDENTITY_INSERT [ApplicationSetup].[Setting] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[Supplier]...';
SET IDENTITY_INSERT [ApplicationSetup].[Supplier] ON;
INSERT INTO [ApplicationSetup].[Supplier] ([Id], [Name], [NameAr], [TenantId])
SELECT [Id], [Name], [NameAr], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[Supplier];
SET IDENTITY_INSERT [ApplicationSetup].[Supplier] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[SystemParameter]...';
SET IDENTITY_INSERT [ApplicationSetup].[SystemParameter] ON;
INSERT INTO [ApplicationSetup].[SystemParameter] ([Id], [Name], [Value], [Active], [TenantId])
SELECT [Id], [Name], [Value], [Active], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[SystemParameter];
SET IDENTITY_INSERT [ApplicationSetup].[SystemParameter] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[TypeOfWeapon]...';
SET IDENTITY_INSERT [ApplicationSetup].[TypeOfWeapon] ON;
INSERT INTO [ApplicationSetup].[TypeOfWeapon] ([Id], [Code], [Name], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [NameAr], [CompanyID], [TenantId])
SELECT [Id], [Code], [Name], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [NameAr], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[TypeOfWeapon];
SET IDENTITY_INSERT [ApplicationSetup].[TypeOfWeapon] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[UserLog]...';
SET IDENTITY_INSERT [ApplicationSetup].[UserLog] ON;
INSERT INTO [ApplicationSetup].[UserLog] ([ID], [UserID], [ActionID], [ActionDate], [AffecterID], [OldData], [NewData], [PageID], [TenantId])
SELECT [ID], [UserID], [ActionID], [ActionDate], [AffecterID], [OldData], [NewData], [PageID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[UserLog];
SET IDENTITY_INSERT [ApplicationSetup].[UserLog] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[UserType]...';
SET IDENTITY_INSERT [ApplicationSetup].[UserType] ON;
INSERT INTO [ApplicationSetup].[UserType] ([Id], [Type], [Description], [FullDescription], [DescriptionAr], [DescriptionEn], [TenantId])
SELECT [Id], [Type], [Description], [FullDescription], [DescriptionAr], [DescriptionEn], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[UserType];
SET IDENTITY_INSERT [ApplicationSetup].[UserType] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[allocationIncome]...';
SET IDENTITY_INSERT [ApplicationSetup].[allocationIncome] ON;
INSERT INTO [ApplicationSetup].[allocationIncome] ([Id], [EntityName], [IncomeAllocation], [ServiceId], [TenantId])
SELECT [Id], [EntityName], [IncomeAllocation], [ServiceId], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[allocationIncome];
SET IDENTITY_INSERT [ApplicationSetup].[allocationIncome] OFF;
GO

PRINT 'Migrating [ApplicationSetup].[patientconsent]...';
SET IDENTITY_INSERT [ApplicationSetup].[patientconsent] ON;
INSERT INTO [ApplicationSetup].[patientconsent] ([Id], [FileName], [PatientID], [TypeID], [Other], [PageName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [RawID], [TenantId])
SELECT [Id], [FileName], [PatientID], [TypeID], [Other], [PageName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [RawID], @TenantId
FROM [SunCity_Clinics].[ApplicationSetup].[patientconsent];
SET IDENTITY_INSERT [ApplicationSetup].[patientconsent] OFF;
GO

PRINT 'Migrating [Billing].[AdvanceReceiptsItemsRefund]...';
SET IDENTITY_INSERT [Billing].[AdvanceReceiptsItemsRefund] ON;
INSERT INTO [Billing].[AdvanceReceiptsItemsRefund] ([Id], [ReceiptNumber], [ItemID], [Quantity], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [ReceiptNumber], [ItemID], [Quantity], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[AdvanceReceiptsItemsRefund];
SET IDENTITY_INSERT [Billing].[AdvanceReceiptsItemsRefund] OFF;
GO

PRINT 'Migrating [Billing].[AdvanceReceiptsItems]...';
SET IDENTITY_INSERT [Billing].[AdvanceReceiptsItems] ON;
INSERT INTO [Billing].[AdvanceReceiptsItems] ([Id], [AdvanceReceiptId], [ItemID], [Quantity], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [AdvanceReceiptId], [ItemID], [Quantity], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[AdvanceReceiptsItems];
SET IDENTITY_INSERT [Billing].[AdvanceReceiptsItems] OFF;
GO

PRINT 'Migrating [Billing].[AdvanceReceiptsPaymentRefund]...';
SET IDENTITY_INSERT [Billing].[AdvanceReceiptsPaymentRefund] ON;
INSERT INTO [Billing].[AdvanceReceiptsPaymentRefund] ([Id], [ReceiptNumber], [PaymentModeID], [Amount], [CurrencyID], [TransactionNo], [TransactionDate], [BankID], [Details], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [ReceiptNumber], [PaymentModeID], [Amount], [CurrencyID], [TransactionNo], [TransactionDate], [BankID], [Details], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[AdvanceReceiptsPaymentRefund];
SET IDENTITY_INSERT [Billing].[AdvanceReceiptsPaymentRefund] OFF;
GO

PRINT 'Migrating [Billing].[AdvanceReceiptsPayment]...';
SET IDENTITY_INSERT [Billing].[AdvanceReceiptsPayment] ON;
INSERT INTO [Billing].[AdvanceReceiptsPayment] ([Id], [ReceiptNumber], [PaymentModeID], [Amount], [CurrencyID], [TransactionNo], [TransactionDate], [BankID], [Details], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Converted], [CompanyID], [TenantId])
SELECT [Id], [ReceiptNumber], [PaymentModeID], [Amount], [CurrencyID], [TransactionNo], [TransactionDate], [BankID], [Details], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Converted], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[AdvanceReceiptsPayment];
SET IDENTITY_INSERT [Billing].[AdvanceReceiptsPayment] OFF;
GO

PRINT 'Migrating [Billing].[AdvanceReceipts]...';
SET IDENTITY_INSERT [Billing].[AdvanceReceipts] ON;
INSERT INTO [Billing].[AdvanceReceipts] ([Id], [PatientID], [DoctorID], [ReceiptNumber], [ReceiptDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Converted], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [DoctorID], [ReceiptNumber], [ReceiptDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Converted], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[AdvanceReceipts];
SET IDENTITY_INSERT [Billing].[AdvanceReceipts] OFF;
GO

PRINT 'Migrating [Billing].[AssginCashierForPOS]...';
SET IDENTITY_INSERT [Billing].[AssginCashierForPOS] ON;
INSERT INTO [Billing].[AssginCashierForPOS] ([ID], [POSID], [UserID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [POSID], [UserID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[AssginCashierForPOS];
SET IDENTITY_INSERT [Billing].[AssginCashierForPOS] OFF;
GO

PRINT 'Migrating [Billing].[AssginCostCenterForPOS]...';
SET IDENTITY_INSERT [Billing].[AssginCostCenterForPOS] ON;
INSERT INTO [Billing].[AssginCostCenterForPOS] ([ID], [POSID], [CostCenterID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [POSID], [CostCenterID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[AssginCostCenterForPOS];
SET IDENTITY_INSERT [Billing].[AssginCostCenterForPOS] OFF;
GO

PRINT 'Migrating [Billing].[CashierBatches]...';
SET IDENTITY_INSERT [Billing].[CashierBatches] ON;
INSERT INTO [Billing].[CashierBatches] ([ID], [CashierID], [POSID], [StartDate], [EndDate], [OpeningAmount], [ClosingAmount], [PayOut], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [CashierID], [POSID], [StartDate], [EndDate], [OpeningAmount], [ClosingAmount], [PayOut], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[CashierBatches];
SET IDENTITY_INSERT [Billing].[CashierBatches] OFF;
GO

PRINT 'Migrating [Billing].[CashierBoxCloseShift]...';
SET IDENTITY_INSERT [Billing].[CashierBoxCloseShift] ON;
INSERT INTO [Billing].[CashierBoxCloseShift] ([ID], [Datetime], [UserID], [BoxID], [Amount], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Datetime], [UserID], [BoxID], [Amount], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[CashierBoxCloseShift];
SET IDENTITY_INSERT [Billing].[CashierBoxCloseShift] OFF;
GO

PRINT 'Migrating [Billing].[CashierBoxCustody]...';
SET IDENTITY_INSERT [Billing].[CashierBoxCustody] ON;
INSERT INTO [Billing].[CashierBoxCustody] ([ID], [BoxID], [CurrencyID], [Date], [Amount], [CustodyType], [IsConfirmed], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SafeBoxID], [TenantId])
SELECT [ID], [BoxID], [CurrencyID], [Date], [Amount], [CustodyType], [IsConfirmed], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SafeBoxID], @TenantId
FROM [SunCity_Clinics].[Billing].[CashierBoxCustody];
SET IDENTITY_INSERT [Billing].[CashierBoxCustody] OFF;
GO

PRINT 'Migrating [Billing].[CashierBoxDailyClose]...';
SET IDENTITY_INSERT [Billing].[CashierBoxDailyClose] ON;
INSERT INTO [Billing].[CashierBoxDailyClose] ([id], [BoxID], [BoxDate], [Value], [CurrencyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [CloseStauts], [TenantId])
SELECT [id], [BoxID], [BoxDate], [Value], [CurrencyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [CloseStauts], @TenantId
FROM [SunCity_Clinics].[Billing].[CashierBoxDailyClose];
SET IDENTITY_INSERT [Billing].[CashierBoxDailyClose] OFF;
GO

PRINT 'Migrating [Billing].[CashierBoxDailyOpening]...';
SET IDENTITY_INSERT [Billing].[CashierBoxDailyOpening] ON;
INSERT INTO [Billing].[CashierBoxDailyOpening] ([ID], [Date], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Date], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[CashierBoxDailyOpening];
SET IDENTITY_INSERT [Billing].[CashierBoxDailyOpening] OFF;
GO

PRINT 'Migrating [Billing].[CashierBoxOpenSessions]...';
SET IDENTITY_INSERT [Billing].[CashierBoxOpenSessions] ON;
INSERT INTO [Billing].[CashierBoxOpenSessions] ([ID], [Datetime], [UserID], [BoxID], [Amount], [Status], [ConfirmDate], [CloseDate], [ConfirmedByCashierBoxOpenSessionID], [ParentCashierBoxOpenSessionID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SessionStartBalance], [InAmount], [OutAmount], [branchID], [SessionEndBalance], [TenantId])
SELECT [ID], [Datetime], [UserID], [BoxID], [Amount], [Status], [ConfirmDate], [CloseDate], [ConfirmedByCashierBoxOpenSessionID], [ParentCashierBoxOpenSessionID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SessionStartBalance], [InAmount], [OutAmount], [branchID], [SessionEndBalance], @TenantId
FROM [SunCity_Clinics].[Billing].[CashierBoxOpenSessions];
SET IDENTITY_INSERT [Billing].[CashierBoxOpenSessions] OFF;
GO

PRINT 'Migrating [Billing].[CashierBoxTransfareToBank]...';
SET IDENTITY_INSERT [Billing].[CashierBoxTransfareToBank] ON;
INSERT INTO [Billing].[CashierBoxTransfareToBank] ([ID], [BoxID], [BoxBalance], [BankID], [TransfareAmount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [EntryCode], [TenantId])
SELECT [ID], [BoxID], [BoxBalance], [BankID], [TransfareAmount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [EntryCode], @TenantId
FROM [SunCity_Clinics].[Billing].[CashierBoxTransfareToBank];
SET IDENTITY_INSERT [Billing].[CashierBoxTransfareToBank] OFF;
GO

PRINT 'Migrating [Billing].[CashierBoxUsers]...';
SET IDENTITY_INSERT [Billing].[CashierBoxUsers] ON;
INSERT INTO [Billing].[CashierBoxUsers] ([ID], [UserID], [BoxID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [UserID], [BoxID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[CashierBoxUsers];
SET IDENTITY_INSERT [Billing].[CashierBoxUsers] OFF;
GO

PRINT 'Migrating [Billing].[CashierBox]...';
SET IDENTITY_INSERT [Billing].[CashierBox] ON;
INSERT INTO [Billing].[CashierBox] ([ID], [NameAr], [NameEn], [Code], [GroupID], [AccNo], [CostCenterNo], [ShortageAccNo], [ShortageCostCenter], [IncreaseAccNo], [IncreaseCostCenter], [CurrencyID], [MaxBalance], [MinBalance], [MaxReturnBalance], [MaxAddValue], [ReorderValue], [RedrawValue], [BoxType], [CashType], [DealingType], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [userID], [PaymentMethod], [BranchId], [TenantId])
SELECT [ID], [NameAr], [NameEn], [Code], [GroupID], [AccNo], [CostCenterNo], [ShortageAccNo], [ShortageCostCenter], [IncreaseAccNo], [IncreaseCostCenter], [CurrencyID], [MaxBalance], [MinBalance], [MaxReturnBalance], [MaxAddValue], [ReorderValue], [RedrawValue], [BoxType], [CashType], [DealingType], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [userID], [PaymentMethod], [BranchId], @TenantId
FROM [SunCity_Clinics].[Billing].[CashierBox];
SET IDENTITY_INSERT [Billing].[CashierBox] OFF;
GO

PRINT 'Migrating [Billing].[CashierGroup]...';
SET IDENTITY_INSERT [Billing].[CashierGroup] ON;
INSERT INTO [Billing].[CashierGroup] ([id], [code], [NameAr], [NameEn], [IsActive], [CompanyID], [TenantId])
SELECT [id], [code], [NameAr], [NameEn], [IsActive], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[CashierGroup];
SET IDENTITY_INSERT [Billing].[CashierGroup] OFF;
GO

PRINT 'Migrating [Billing].[CashierOpenBalance]...';
SET IDENTITY_INSERT [Billing].[CashierOpenBalance] ON;
INSERT INTO [Billing].[CashierOpenBalance] ([ID], [BoxID], [CurrencyID], [Date], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Status], [TenantId])
SELECT [ID], [BoxID], [CurrencyID], [Date], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Status], @TenantId
FROM [SunCity_Clinics].[Billing].[CashierOpenBalance];
SET IDENTITY_INSERT [Billing].[CashierOpenBalance] OFF;
GO

PRINT 'Migrating [Billing].[CashierWorkList]...';
SET IDENTITY_INSERT [Billing].[CashierWorkList] ON;
INSERT INTO [Billing].[CashierWorkList] ([ID], [CashierID], [POSID], [BillID], [BillNo], [PaymentMethodID], [Amount], [CloseDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [CashierID], [POSID], [BillID], [BillNo], [PaymentMethodID], [Amount], [CloseDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[CashierWorkList];
SET IDENTITY_INSERT [Billing].[CashierWorkList] OFF;
GO

PRINT 'Migrating [Billing].[CreditCodeMaster]...';
SET IDENTITY_INSERT [Billing].[CreditCodeMaster] ON;
INSERT INTO [Billing].[CreditCodeMaster] ([Id], [CreditCodeName], [IsActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [CreditCodeName], [IsActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[CreditCodeMaster];
SET IDENTITY_INSERT [Billing].[CreditCodeMaster] OFF;
GO

PRINT 'Migrating [Billing].[CustomerCategoryCostCenters]...';
SET IDENTITY_INSERT [Billing].[CustomerCategoryCostCenters] ON;
INSERT INTO [Billing].[CustomerCategoryCostCenters] ([Id], [CustomerCategoryID], [CostCenterCompaniesID], [DiscountByPer], [CoverageLimit], [IsCoverageLimitPerEpisode], [DeductibleByPer], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [CustomerCategoryID], [CostCenterCompaniesID], [DiscountByPer], [CoverageLimit], [IsCoverageLimitPerEpisode], [DeductibleByPer], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[CustomerCategoryCostCenters];
SET IDENTITY_INSERT [Billing].[CustomerCategoryCostCenters] OFF;
GO

PRINT 'Migrating [Billing].[CustomerCategoryDrugType]...';
SET IDENTITY_INSERT [Billing].[CustomerCategoryDrugType] ON;
INSERT INTO [Billing].[CustomerCategoryDrugType] ([Id], [CustomerCategoryID], [DrugTypesId], [DiscountByPer], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [CustomerCategoryID], [DrugTypesId], [DiscountByPer], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[CustomerCategoryDrugType];
SET IDENTITY_INSERT [Billing].[CustomerCategoryDrugType] OFF;
GO

PRINT 'Migrating [Billing].[CustomerCategoryDrugs]...';
SET IDENTITY_INSERT [Billing].[CustomerCategoryDrugs] ON;
INSERT INTO [Billing].[CustomerCategoryDrugs] ([Id], [CustomerCategoryDrugTypeID], [DrugID], [StdAmount], [Discount], [IsDiscountByPer], [Deductible], [IsDeductibleByPer], [IsApproval], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [CustomerCategoryDrugTypeID], [DrugID], [StdAmount], [Discount], [IsDiscountByPer], [Deductible], [IsDeductibleByPer], [IsApproval], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[CustomerCategoryDrugs];
SET IDENTITY_INSERT [Billing].[CustomerCategoryDrugs] OFF;
GO

PRINT 'Migrating [Billing].[CustomerCategoryServices]...';
SET IDENTITY_INSERT [Billing].[CustomerCategoryServices] ON;
INSERT INTO [Billing].[CustomerCategoryServices] ([Id], [CustomerCategoryCostCentersID], [ServiceID], [StdAmount], [Discount], [IsDiscountByPer], [Deductible], [IsDeductibleByPer], [IsApproval], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [CustomerCategoryCostCentersID], [ServiceID], [StdAmount], [Discount], [IsDiscountByPer], [Deductible], [IsDeductibleByPer], [IsApproval], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[CustomerCategoryServices];
SET IDENTITY_INSERT [Billing].[CustomerCategoryServices] OFF;
GO

PRINT 'Migrating [Billing].[CustomerCategorySupItems]...';
SET IDENTITY_INSERT [Billing].[CustomerCategorySupItems] ON;
INSERT INTO [Billing].[CustomerCategorySupItems] ([Id], [CustomerCategorySuppliesID], [ItemID], [StdAmount], [Discount], [IsDiscountByPer], [Deductible], [IsDeductibleByPer], [IsApproval], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [CustomerCategorySuppliesID], [ItemID], [StdAmount], [Discount], [IsDiscountByPer], [Deductible], [IsDeductibleByPer], [IsApproval], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[CustomerCategorySupItems];
SET IDENTITY_INSERT [Billing].[CustomerCategorySupItems] OFF;
GO

PRINT 'Migrating [Billing].[CustomerCategorySupplies]...';
SET IDENTITY_INSERT [Billing].[CustomerCategorySupplies] ON;
INSERT INTO [Billing].[CustomerCategorySupplies] ([Id], [CustomerCategoryID], [PackageID], [DiscountByPer], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [CustomerCategoryID], [PackageID], [DiscountByPer], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[CustomerCategorySupplies];
SET IDENTITY_INSERT [Billing].[CustomerCategorySupplies] OFF;
GO

PRINT 'Migrating [Billing].[CustomerCategory]...';
SET IDENTITY_INSERT [Billing].[CustomerCategory] ON;
INSERT INTO [Billing].[CustomerCategory] ([Id], [CustomerMasterID], [CategoryName], [CarrierName], [ContactNo], [EffectiveFrom], [EffectiveTo], [CoverageLimit], [IsCoverageLimitPerEpisode], [IsInpatient], [IsOutpatient], [ContactDate], [Copay], [IsCopayByPer], [MaximumCopay], [IsOnNetAmount], [IsAfterDeductible], [IsImmediateSettlment], [DonotEditCopayAndDeductible], [DoVerityPolicyNo], [DoAutoGeneratePolicyNo], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [CustomerMasterID], [CategoryName], [CarrierName], [ContactNo], [EffectiveFrom], [EffectiveTo], [CoverageLimit], [IsCoverageLimitPerEpisode], [IsInpatient], [IsOutpatient], [ContactDate], [Copay], [IsCopayByPer], [MaximumCopay], [IsOnNetAmount], [IsAfterDeductible], [IsImmediateSettlment], [DonotEditCopayAndDeductible], [DoVerityPolicyNo], [DoAutoGeneratePolicyNo], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[CustomerCategory];
SET IDENTITY_INSERT [Billing].[CustomerCategory] OFF;
GO

PRINT 'Migrating [Billing].[CustomerMasterDetails]...';
SET IDENTITY_INSERT [Billing].[CustomerMasterDetails] ON;
INSERT INTO [Billing].[CustomerMasterDetails] ([ID], [CustomerMasterID], [Location], [Name], [Designation], [Type], [Details], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [CustomerMasterID], [Location], [Name], [Designation], [Type], [Details], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[CustomerMasterDetails];
SET IDENTITY_INSERT [Billing].[CustomerMasterDetails] OFF;
GO

PRINT 'Migrating [Billing].[CustomerMaster]...';
SET IDENTITY_INSERT [Billing].[CustomerMaster] ON;
INSERT INTO [Billing].[CustomerMaster] ([Id], [CustomerCode], [CustomerTypeID], [IsActive], [Address], [NationalityID], [CustomerName], [CityID], [ZipCode], [Email], [POBox], [BillingAddress], [BillingNationalityID], [BillingCityID], [BillingZipCode], [BillingWebSite], [BillingPOBox], [IsDefaulter], [CurrencyID], [IPModePayID], [BillCollectorID], [PaymentTermID], [ledgerAccountID], [CreditApprovalRefNo], [OPModePayID], [StopCredit], [DeliveryTime], [CreditLimit], [CyclePeriod], [MinimumAmount], [LastStatementDate], [Notes], [FromPCDate], [ToPCDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [FollowSpecialPriceList], [MainAcc], [SubAcc], [CompanyID], [TenantId])
SELECT [Id], [CustomerCode], [CustomerTypeID], [IsActive], [Address], [NationalityID], [CustomerName], [CityID], [ZipCode], [Email], [POBox], [BillingAddress], [BillingNationalityID], [BillingCityID], [BillingZipCode], [BillingWebSite], [BillingPOBox], [IsDefaulter], [CurrencyID], [IPModePayID], [BillCollectorID], [PaymentTermID], [ledgerAccountID], [CreditApprovalRefNo], [OPModePayID], [StopCredit], [DeliveryTime], [CreditLimit], [CyclePeriod], [MinimumAmount], [LastStatementDate], [Notes], [FromPCDate], [ToPCDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [FollowSpecialPriceList], [MainAcc], [SubAcc], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[CustomerMaster];
SET IDENTITY_INSERT [Billing].[CustomerMaster] OFF;
GO

PRINT 'Migrating [Billing].[CustomerTypes]...';
SET IDENTITY_INSERT [Billing].[CustomerTypes] ON;
INSERT INTO [Billing].[CustomerTypes] ([TypeDescriptionAr], [TypeDescription], [AppliesTo], [IsActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Id], [TenantId])
SELECT [TypeDescriptionAr], [TypeDescription], [AppliesTo], [IsActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Id], @TenantId
FROM [SunCity_Clinics].[Billing].[CustomerTypes];
SET IDENTITY_INSERT [Billing].[CustomerTypes] OFF;
GO

PRINT 'Migrating [Billing].[DepartmentConsumption]...';
SET IDENTITY_INSERT [Billing].[DepartmentConsumption] ON;
INSERT INTO [Billing].[DepartmentConsumption] ([ID], [deptID], [GenericID], [Year], [drugConsump], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [RequestedConsump], [CompanyID], [DrugID], [AdmitModeID], [Strength], [UnitID], [AvgMonthlyConsumed], [OrderQty], [Fromdate], [Todate], [monthNo], [UnitTemplateID], [UnitConversionID], [TenantId])
SELECT [ID], [deptID], [GenericID], [Year], [drugConsump], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [RequestedConsump], [CompanyID], [DrugID], [AdmitModeID], [Strength], [UnitID], [AvgMonthlyConsumed], [OrderQty], [Fromdate], [Todate], [monthNo], [UnitTemplateID], [UnitConversionID], @TenantId
FROM [SunCity_Clinics].[Billing].[DepartmentConsumption];
SET IDENTITY_INSERT [Billing].[DepartmentConsumption] OFF;
GO

PRINT 'Migrating [Billing].[DiscountOFFers]...';
SET IDENTITY_INSERT [Billing].[DiscountOFFers] ON;
INSERT INTO [Billing].[DiscountOFFers] ([Id], [DateFrom], [DateTo], [DiscountDes], [DiscountType], [DiscountValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [DateFrom], [DateTo], [DiscountDes], [DiscountType], [DiscountValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[DiscountOFFers];
SET IDENTITY_INSERT [Billing].[DiscountOFFers] OFF;
GO

PRINT 'Migrating [Billing].[DiscountPriceListDepartment]...';
SET IDENTITY_INSERT [Billing].[DiscountPriceListDepartment] ON;
INSERT INTO [Billing].[DiscountPriceListDepartment] ([PriceListDepartment_ID], [CostcenterID], [Discount_Price], [PriceList_Header_Id], [ISValue], [TenantId])
SELECT [PriceListDepartment_ID], [CostcenterID], [Discount_Price], [PriceList_Header_Id], [ISValue], @TenantId
FROM [SunCity_Clinics].[Billing].[DiscountPriceListDepartment];
SET IDENTITY_INSERT [Billing].[DiscountPriceListDepartment] OFF;
GO

PRINT 'Migrating [Billing].[DiscountPriceListHeader]...';
SET IDENTITY_INSERT [Billing].[DiscountPriceListHeader] ON;
INSERT INTO [Billing].[DiscountPriceListHeader] ([PriceList_Header_Id], [Name_En], [Name_Ar], [Code], [FromDate], [ToDate], [CompanyID], [AllPeroid], [TenantId])
SELECT [PriceList_Header_Id], [Name_En], [Name_Ar], [Code], [FromDate], [ToDate], [CompanyID], [AllPeroid], @TenantId
FROM [SunCity_Clinics].[Billing].[DiscountPriceListHeader];
SET IDENTITY_INSERT [Billing].[DiscountPriceListHeader] OFF;
GO

PRINT 'Migrating [Billing].[DiscountPriceListServices]...';
SET IDENTITY_INSERT [Billing].[DiscountPriceListServices] ON;
INSERT INTO [Billing].[DiscountPriceListServices] ([PriceListService_Id], [PriceListDepartment_ID], [ServiceId], [Discount_Price], [ISValue], [TenantId])
SELECT [PriceListService_Id], [PriceListDepartment_ID], [ServiceId], [Discount_Price], [ISValue], @TenantId
FROM [SunCity_Clinics].[Billing].[DiscountPriceListServices];
SET IDENTITY_INSERT [Billing].[DiscountPriceListServices] OFF;
GO

PRINT 'Migrating [Billing].[DoctorFeesPayment]...';
SET IDENTITY_INSERT [Billing].[DoctorFeesPayment] ON;
INSERT INTO [Billing].[DoctorFeesPayment] ([Id], [DoctorID], [Amount], [payMonth], [Paid], [Createdate], [Paiddate], [BranchID], [TenantId])
SELECT [Id], [DoctorID], [Amount], [payMonth], [Paid], [Createdate], [Paiddate], [BranchID], @TenantId
FROM [SunCity_Clinics].[Billing].[DoctorFeesPayment];
SET IDENTITY_INSERT [Billing].[DoctorFeesPayment] OFF;
GO

PRINT 'Migrating [Billing].[DoctorFeesPercentage]...';
SET IDENTITY_INSERT [Billing].[DoctorFeesPercentage] ON;
INSERT INTO [Billing].[DoctorFeesPercentage] ([ID], [DoctorDetectCashPercentage], [DoctorDetectInsurancePercentage], [HospitalDetectCashPercentage], [HospitalDetectInsurancePercentage], [DoctorServiceCashPercentage], [DoctorServiceInsurancePercentage], [HospitalServiceCashPercentage], [HospitalServiceInsurancePercentage], [DoctorOperationCashPercentage], [DoctorOperationInsurancePercentage], [HospitalOperationCashPercentage], [HospitalOperationInsurancePercentage], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [DoctorSTCashPercentage], [DoctorSTInsurancePercentage], [HospitalSTCashPercentage], [HospitalSTInsurancePercentage], [CompanyID], [TenantId])
SELECT [ID], [DoctorDetectCashPercentage], [DoctorDetectInsurancePercentage], [HospitalDetectCashPercentage], [HospitalDetectInsurancePercentage], [DoctorServiceCashPercentage], [DoctorServiceInsurancePercentage], [HospitalServiceCashPercentage], [HospitalServiceInsurancePercentage], [DoctorOperationCashPercentage], [DoctorOperationInsurancePercentage], [HospitalOperationCashPercentage], [HospitalOperationInsurancePercentage], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [DoctorSTCashPercentage], [DoctorSTInsurancePercentage], [HospitalSTCashPercentage], [HospitalSTInsurancePercentage], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[DoctorFeesPercentage];
SET IDENTITY_INSERT [Billing].[DoctorFeesPercentage] OFF;
GO

PRINT 'Migrating [Billing].[DoctorFees_Doctors]...';
SET IDENTITY_INSERT [Billing].[DoctorFees_Doctors] ON;
INSERT INTO [Billing].[DoctorFees_Doctors] ([Id], [DoctorID], [Detect_Cash_Perc], [Detect_Sponser_Perc], [Service_Cash_Perc], [Service_Sponser_Perc], [Operation_Cash_Perc], [Operation_Sponser_Perc], [Device_Cash_Perc], [Device_Sponser_Perc], [CompanyID], [TenantId])
SELECT [Id], [DoctorID], [Detect_Cash_Perc], [Detect_Sponser_Perc], [Service_Cash_Perc], [Service_Sponser_Perc], [Operation_Cash_Perc], [Operation_Sponser_Perc], [Device_Cash_Perc], [Device_Sponser_Perc], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[DoctorFees_Doctors];
SET IDENTITY_INSERT [Billing].[DoctorFees_Doctors] OFF;
GO

PRINT 'Migrating [Billing].[EclaimEdit]...';
SET IDENTITY_INSERT [Billing].[EclaimEdit] ON;
INSERT INTO [Billing].[EclaimEdit] ([ReceiptDate], [PatientId], [ClaimCount], [PatientMemberShipId], [PatientCode], [PolicyNo], [InsuranceCompaniesNameArabic], [InsuranceCompaniesNameEnglish], [PolicyName], [PatientName], [PatientNameEn], [InvoiceNumber], [InvoiceDate], [PreauthId], [DoctorNameEn], [DoctorNameAr], [SpecialtyNameEn], [SpecialtyNameAr], [ClinicalData], [ICD10_1], [ICD10_2], [ICD10_3], [ICD10_4], [ClaimType], [REFER_IND], [EMER_IND], [TotalInvoiceDeductible], [Tooth_Number_FDI], [ServiceId], [ServiceName], [TreatmentDateFrom], [TreatmentDateTo], [InputtedQuantity], [BilledAmount], [LineItemDiscount], [Vat], [Left_Eye_Test_Reading], [Right_Eye_Test_Reading], [Times], [Per], [UniqueMemberId], [UniquePhysicianId], [DateOfAdmission], [DateOfDischarge], [AdmissionType], [DischargeDisposition], [EmergencyDepartmentDisposition], [AdmissionDiagnosis], [PrincipalDiagnosis], [AdditionalDiagnosis], [ConditionOnsetFlag], [PrincipalProcedure_ACHI], [AdditionalProcedures_ACHI], [AR_DRG], [AdmittedCase], [AuthorizationNumber], [DaysSupply], [EmergencyArrivalCode], [EmergencyServiceStart], [EmergencyWaitingTime], [ServiceEventType], [CareType], [ActivityObjectCode_LOINC], [ActivityObjectCodeValue_LOINC], [id], [OriginalClaimID], [SponserApproveStatus], [ClaimDateTo], [ClaimDateFrom], [EdClaimID], [InsuranceId], [ReasonofRejection], [otherReason], [RejectResponsibility], [TenantId])
SELECT [ReceiptDate], [PatientId], [ClaimCount], [PatientMemberShipId], [PatientCode], [PolicyNo], [InsuranceCompaniesNameArabic], [InsuranceCompaniesNameEnglish], [PolicyName], [PatientName], [PatientNameEn], [InvoiceNumber], [InvoiceDate], [PreauthId], [DoctorNameEn], [DoctorNameAr], [SpecialtyNameEn], [SpecialtyNameAr], [ClinicalData], [ICD10_1], [ICD10_2], [ICD10_3], [ICD10_4], [ClaimType], [REFER_IND], [EMER_IND], [TotalInvoiceDeductible], [Tooth_Number_FDI], [ServiceId], [ServiceName], [TreatmentDateFrom], [TreatmentDateTo], [InputtedQuantity], [BilledAmount], [LineItemDiscount], [Vat], [Left_Eye_Test_Reading], [Right_Eye_Test_Reading], [Times], [Per], [UniqueMemberId], [UniquePhysicianId], [DateOfAdmission], [DateOfDischarge], [AdmissionType], [DischargeDisposition], [EmergencyDepartmentDisposition], [AdmissionDiagnosis], [PrincipalDiagnosis], [AdditionalDiagnosis], [ConditionOnsetFlag], [PrincipalProcedure_ACHI], [AdditionalProcedures_ACHI], [AR_DRG], [AdmittedCase], [AuthorizationNumber], [DaysSupply], [EmergencyArrivalCode], [EmergencyServiceStart], [EmergencyWaitingTime], [ServiceEventType], [CareType], [ActivityObjectCode_LOINC], [ActivityObjectCodeValue_LOINC], [id], [OriginalClaimID], [SponserApproveStatus], [ClaimDateTo], [ClaimDateFrom], [EdClaimID], [InsuranceId], [ReasonofRejection], [otherReason], [RejectResponsibility], @TenantId
FROM [SunCity_Clinics].[Billing].[EclaimEdit];
SET IDENTITY_INSERT [Billing].[EclaimEdit] OFF;
GO

PRINT 'Migrating [Billing].[ICDCodes]...';
SET IDENTITY_INSERT [Billing].[ICDCodes] ON;
INSERT INTO [Billing].[ICDCodes] ([Id], [ICDCode], [ICDCodeDescription], [ICDGroupID], [ICDCodeAlias], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [ICDCode], [ICDCodeDescription], [ICDGroupID], [ICDCodeAlias], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[ICDCodes];
SET IDENTITY_INSERT [Billing].[ICDCodes] OFF;
GO

PRINT 'Migrating [Billing].[ICDGroups]...';
SET IDENTITY_INSERT [Billing].[ICDGroups] ON;
INSERT INTO [Billing].[ICDGroups] ([Id], [ICDGroupCode], [ICDGroupName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [ICDGroupCode], [ICDGroupName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[ICDGroups];
SET IDENTITY_INSERT [Billing].[ICDGroups] OFF;
GO

PRINT 'Migrating [Billing].[INS_Company_Contract_Discount]...';
SET IDENTITY_INSERT [Billing].[INS_Company_Contract_Discount] ON;
INSERT INTO [Billing].[INS_Company_Contract_Discount] ([ID], [Code], [ServiceID], [Discount], [Discount_Type], [ContractID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [DeptID], [TenantId])
SELECT [ID], [Code], [ServiceID], [Discount], [Discount_Type], [ContractID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [DeptID], @TenantId
FROM [SunCity_Clinics].[Billing].[INS_Company_Contract_Discount];
SET IDENTITY_INSERT [Billing].[INS_Company_Contract_Discount] OFF;
GO

PRINT 'Migrating [Billing].[Ins_Company_Policy]...';
SET IDENTITY_INSERT [Billing].[Ins_Company_Policy] ON;
INSERT INTO [Billing].[Ins_Company_Policy] ([ID], [Code], [InsCompanyID], [PolicyHolder], [PolicyNo], [Effective_Date], [Expiry_Date], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [PolicyName], [TenantId])
SELECT [ID], [Code], [InsCompanyID], [PolicyHolder], [PolicyNo], [Effective_Date], [Expiry_Date], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [PolicyName], @TenantId
FROM [SunCity_Clinics].[Billing].[Ins_Company_Policy];
SET IDENTITY_INSERT [Billing].[Ins_Company_Policy] OFF;
GO

PRINT 'Migrating [Billing].[InsuranceAdvice]...';
SET IDENTITY_INSERT [Billing].[InsuranceAdvice] ON;
INSERT INTO [Billing].[InsuranceAdvice] ([ID], [NameEnglish], [NameArabic], [Code], [InsuranceID], [AdviceType], [CurrencyID], [ConversionRate], [Amount], [CreatedBy], [CreatedDate], [CompanyID], [TenantId])
SELECT [ID], [NameEnglish], [NameArabic], [Code], [InsuranceID], [AdviceType], [CurrencyID], [ConversionRate], [Amount], [CreatedBy], [CreatedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[InsuranceAdvice];
SET IDENTITY_INSERT [Billing].[InsuranceAdvice] OFF;
GO

PRINT 'Migrating [Billing].[InsuranceClaimDetail]...';
SET IDENTITY_INSERT [Billing].[InsuranceClaimDetail] ON;
INSERT INTO [Billing].[InsuranceClaimDetail] ([ID], [MasterID], [PatientBillServiceID], [DetailStatus], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [MasterID], [PatientBillServiceID], [DetailStatus], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[InsuranceClaimDetail];
SET IDENTITY_INSERT [Billing].[InsuranceClaimDetail] OFF;
GO

PRINT 'Migrating [Billing].[InsuranceClaimMaster]...';
SET IDENTITY_INSERT [Billing].[InsuranceClaimMaster] ON;
INSERT INTO [Billing].[InsuranceClaimMaster] ([ID], [InsCompID], [InsTotal], [InsName], [MasterStatus], [InsNameAr], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ReissueReason], [TenantId])
SELECT [ID], [InsCompID], [InsTotal], [InsName], [MasterStatus], [InsNameAr], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ReissueReason], @TenantId
FROM [SunCity_Clinics].[Billing].[InsuranceClaimMaster];
SET IDENTITY_INSERT [Billing].[InsuranceClaimMaster] OFF;
GO

PRINT 'Migrating [Billing].[InsuranceCompanies]...';
SET IDENTITY_INSERT [Billing].[InsuranceCompanies] ON;
INSERT INTO [Billing].[InsuranceCompanies] ([ID], [NameEnglish], [NameArabic], [Code], [CountryID], [CompanyTypeID], [CurrencyID], [AccountNumberID], [ParentCompID], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Address], [Website], [Email], [Phone], [FaxNo], [CCHI], [CR_No], [VAT_No], [CompanyID], [Eligability_Website], [OpeningDeposit], [OpeningBalance], [DebitAccountNumberID], [DiscountAccountNumberID], [LossAccountNumberID], [ServiceApproval], [LimitExceed], [Provider_Code], [Provider_Relation_Officer], [Officer_Telephone], [Nph_insurer], [City], [Region], [PostalCode], [buildingNo], [Country], [TenantId])
SELECT [ID], [NameEnglish], [NameArabic], [Code], [CountryID], [CompanyTypeID], [CurrencyID], [AccountNumberID], [ParentCompID], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Address], [Website], [Email], [Phone], [FaxNo], [CCHI], [CR_No], [VAT_No], [CompanyID], [Eligability_Website], [OpeningDeposit], [OpeningBalance], [DebitAccountNumberID], [DiscountAccountNumberID], [LossAccountNumberID], [ServiceApproval], [LimitExceed], [Provider_Code], [Provider_Relation_Officer], [Officer_Telephone], [Nph_insurer], [City], [Region], [PostalCode], [buildingNo], [Country], @TenantId
FROM [SunCity_Clinics].[Billing].[InsuranceCompanies];
SET IDENTITY_INSERT [Billing].[InsuranceCompanies] OFF;
GO

PRINT 'Migrating [Billing].[InsuranceCompanyCodes]...';
SET IDENTITY_INSERT [Billing].[InsuranceCompanyCodes] ON;
INSERT INTO [Billing].[InsuranceCompanyCodes] ([ID], [NameEnglish], [NameArabic], [Code], [CountryID], [CustomerTypeID], [CurrencyID], [AccountNumberID], [CostCenterID], [InsuranceCompanyCodesID], [SubscriptionVoucherNumber], [CouponNumberLength], [Active], [ProhibitionDealing], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [NameEnglish], [NameArabic], [Code], [CountryID], [CustomerTypeID], [CurrencyID], [AccountNumberID], [CostCenterID], [InsuranceCompanyCodesID], [SubscriptionVoucherNumber], [CouponNumberLength], [Active], [ProhibitionDealing], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[InsuranceCompanyCodes];
SET IDENTITY_INSERT [Billing].[InsuranceCompanyCodes] OFF;
GO

PRINT 'Migrating [Billing].[Insurance_Comp_Contract]...';
SET IDENTITY_INSERT [Billing].[Insurance_Comp_Contract] ON;
INSERT INTO [Billing].[Insurance_Comp_Contract] ([ID], [Code], [Issue_Date], [Valid_To], [Renewal_date], [Representitive_LandLine], [Representitive_Mobile], [Representitive_Email], [Discount_Offered], [Deduction_Applied], [Ins_CompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [PriceList_Header_Id], [CompanyID], [TenantId])
SELECT [ID], [Code], [Issue_Date], [Valid_To], [Renewal_date], [Representitive_LandLine], [Representitive_Mobile], [Representitive_Email], [Discount_Offered], [Deduction_Applied], [Ins_CompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [PriceList_Header_Id], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[Insurance_Comp_Contract];
SET IDENTITY_INSERT [Billing].[Insurance_Comp_Contract] OFF;
GO

PRINT 'Migrating [Billing].[Insurance_Setting]...';
SET IDENTITY_INSERT [Billing].[Insurance_Setting] ON;
INSERT INTO [Billing].[Insurance_Setting] ([id], [ServiceApproval], [SponserPerVisit], [LimitExceed], [CompanyID], [TenantId])
SELECT [id], [ServiceApproval], [SponserPerVisit], [LimitExceed], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[Insurance_Setting];
SET IDENTITY_INSERT [Billing].[Insurance_Setting] OFF;
GO

PRINT 'Migrating [Billing].[InvoiceReceipt]...';
SET IDENTITY_INSERT [Billing].[InvoiceReceipt] ON;
INSERT INTO [Billing].[InvoiceReceipt] ([Id], [ReceiptNumber], [InvoiceNumber], [AreLinked], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [ReceiptNumber], [InvoiceNumber], [AreLinked], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[InvoiceReceipt];
SET IDENTITY_INSERT [Billing].[InvoiceReceipt] OFF;
GO

PRINT 'Migrating [Billing].[NphiesAPAItems]...';
SET IDENTITY_INSERT [Billing].[NphiesAPAItems] ON;
INSERT INTO [Billing].[NphiesAPAItems] ([ID], [patInsLimitDtl], [itemsequence], [ServiceName], [ServiceCode], [servicedDate], [unitPrice], [tax], [qty], [net], [patientshare], [invoice], [isPackage], [responseID], [servicedID], [DocDailySchDetailsID], [claimrequest], [status], [serviceType], [daysupply], [patbillserviceid], [visittype], [toothNo], [OptType], [ApprovedAmount], [ServiceURL], [benefit], [ParentID], [TenantId])
SELECT [ID], [patInsLimitDtl], [itemsequence], [ServiceName], [ServiceCode], [servicedDate], [unitPrice], [tax], [qty], [net], [patientshare], [invoice], [isPackage], [responseID], [servicedID], [DocDailySchDetailsID], [claimrequest], [status], [serviceType], [daysupply], [patbillserviceid], [visittype], [toothNo], [OptType], [ApprovedAmount], [ServiceURL], [benefit], [ParentID], @TenantId
FROM [SunCity_Clinics].[Billing].[NphiesAPAItems];
SET IDENTITY_INSERT [Billing].[NphiesAPAItems] OFF;
GO

PRINT 'Migrating [Billing].[NphiesLinks]...';
SET IDENTITY_INSERT [Billing].[NphiesLinks] ON;
INSERT INTO [Billing].[NphiesLinks] ([Id], [Msg], [url], [TenantId])
SELECT [Id], [Msg], [url], @TenantId
FROM [SunCity_Clinics].[Billing].[NphiesLinks];
SET IDENTITY_INSERT [Billing].[NphiesLinks] OFF;
GO

PRINT 'Migrating [Billing].[NphiesPatientInvRequest_claim]...';
SET IDENTITY_INSERT [Billing].[NphiesPatientInvRequest_claim] ON;
INSERT INTO [Billing].[NphiesPatientInvRequest_claim] ([ID], [patInsLimitDtl], [itemsequence], [ServiceName], [ServiceCode], [servicedDate], [unitPrice], [tax], [qty], [net], [patientshare], [invoice], [isPackage], [requestId], [servicedID], [DocDailySchDetailsID], [claimrequest], [status], [serviceType], [daysupply], [patbillserviceid], [visittype], [toothNo], [OptType], [ApprovedAmount], [ServiceURL], [isDevice], [otherCode], [copay], [eligabile], [benifit], [itemState], [resTax], [submitted], [approvedquantity], [rejectedAmount], [approvedRefill], [approvedduration], [tyype], [TenantId])
SELECT [ID], [patInsLimitDtl], [itemsequence], [ServiceName], [ServiceCode], [servicedDate], [unitPrice], [tax], [qty], [net], [patientshare], [invoice], [isPackage], [requestId], [servicedID], [DocDailySchDetailsID], [claimrequest], [status], [serviceType], [daysupply], [patbillserviceid], [visittype], [toothNo], [OptType], [ApprovedAmount], [ServiceURL], [isDevice], [otherCode], [copay], [eligabile], [benifit], [itemState], [resTax], [submitted], [approvedquantity], [rejectedAmount], [approvedRefill], [approvedduration], [tyype], @TenantId
FROM [SunCity_Clinics].[Billing].[NphiesPatientInvRequest_claim];
SET IDENTITY_INSERT [Billing].[NphiesPatientInvRequest_claim] OFF;
GO

PRINT 'Migrating [Billing].[NphiesRequest]...';
INSERT INTO [Billing].[NphiesRequest] ([BundleId], [MsgId], [RequestType], [PatientId], [InsuranceId], [VisitNo], [CreateBy], [CreateDate], [JsonFileName], [eligibilityResponseId], [preAuthRes], [batchId], [communicationPath], [communicationtxt], [APARequestID], [BclaimCount], [TenantId])
SELECT [BundleId], [MsgId], [RequestType], [PatientId], [InsuranceId], [VisitNo], [CreateBy], [CreateDate], [JsonFileName], [eligibilityResponseId], [preAuthRes], [batchId], [communicationPath], [communicationtxt], [APARequestID], [BclaimCount], @TenantId
FROM [SunCity_Clinics].[Billing].[NphiesRequest];
GO

PRINT 'Migrating [Billing].[NphiesResponseExtension]...';
SET IDENTITY_INSERT [Billing].[NphiesResponseExtension] ON;
INSERT INTO [Billing].[NphiesResponseExtension] ([ID], [item], [itemName], [itemNameCode], [typeDisplay], [typeCode], [typeValue], [responseId], [contentf], [category], [TenantId])
SELECT [ID], [item], [itemName], [itemNameCode], [typeDisplay], [typeCode], [typeValue], [responseId], [contentf], [category], @TenantId
FROM [SunCity_Clinics].[Billing].[NphiesResponseExtension];
SET IDENTITY_INSERT [Billing].[NphiesResponseExtension] OFF;
GO

PRINT 'Migrating [Billing].[NphiesResponse]...';
INSERT INTO [Billing].[NphiesResponse] ([BundleId], [ReqbundleId], [ReqMsgId], [EligibleId], [ResponseType], [ResponseStatus], [Disposition], [Identifier], [PatientId], [InsuranceId], [VisitNo], [CreateBy], [CreateDate], [JsonFileName], [OutCome], [msgId], [preAuthRef], [identefiersys], [preAuthRefPeriodSt], [preAuthRefPeriodEnd], [about], [Response], [amount], [fileContent], [reissue], [claimType], [claimSubtype], [reason], [priority], [category], [TenantId])
SELECT [BundleId], [ReqbundleId], [ReqMsgId], [EligibleId], [ResponseType], [ResponseStatus], [Disposition], [Identifier], [PatientId], [InsuranceId], [VisitNo], [CreateBy], [CreateDate], [JsonFileName], [OutCome], [msgId], [preAuthRef], [identefiersys], [preAuthRefPeriodSt], [preAuthRefPeriodEnd], [about], [Response], [amount], [fileContent], [reissue], [claimType], [claimSubtype], [reason], [priority], [category], @TenantId
FROM [SunCity_Clinics].[Billing].[NphiesResponse];
GO

PRINT 'Migrating [Billing].[NpiesPatientInvRequest]...';
SET IDENTITY_INSERT [Billing].[NpiesPatientInvRequest] ON;
INSERT INTO [Billing].[NpiesPatientInvRequest] ([ID], [patInsLimitDtl], [itemsequence], [ServiceURL], [ServiceCode], [servicedDate], [unitPrice], [tax], [qty], [net], [patientshare], [invoice], [isPackage], [requestId], [servicedID], [DocDailySchDetailsID], [claimrequest], [status], [serviceType], [daysupply], [ServiceName], [patbillserviceid], [visittype], [toothNo], [OptType], [ApprovedAmount], [Phreason], [Phsubstitute], [isDevice], [RejectReason], [otherCode], [benefit], [tyype], [TenantId])
SELECT [ID], [patInsLimitDtl], [itemsequence], [ServiceURL], [ServiceCode], [servicedDate], [unitPrice], [tax], [qty], [net], [patientshare], [invoice], [isPackage], [requestId], [servicedID], [DocDailySchDetailsID], [claimrequest], [status], [serviceType], [daysupply], [ServiceName], [patbillserviceid], [visittype], [toothNo], [OptType], [ApprovedAmount], [Phreason], [Phsubstitute], [isDevice], [RejectReason], [otherCode], [benefit], [tyype], @TenantId
FROM [SunCity_Clinics].[Billing].[NpiesPatientInvRequest];
SET IDENTITY_INSERT [Billing].[NpiesPatientInvRequest] OFF;
GO

PRINT 'Migrating [Billing].[OutRersourceDeals]...';
SET IDENTITY_INSERT [Billing].[OutRersourceDeals] ON;
INSERT INTO [Billing].[OutRersourceDeals] ([ID], [OutResourceID], [CreatedDate], [CreatedBy], [CompanyID], [PurchPay], [SalePay], [PaymentDate], [EntryCode], [TenantId])
SELECT [ID], [OutResourceID], [CreatedDate], [CreatedBy], [CompanyID], [PurchPay], [SalePay], [PaymentDate], [EntryCode], @TenantId
FROM [SunCity_Clinics].[Billing].[OutRersourceDeals];
SET IDENTITY_INSERT [Billing].[OutRersourceDeals] OFF;
GO

PRINT 'Migrating [Billing].[PatientBill]...';
SET IDENTITY_INSERT [Billing].[PatientBill] ON;
INSERT INTO [Billing].[PatientBill] ([Id], [PatientID], [PatientName], [BillType], [TotalAmount], [Discount], [DoctorID], [ReceiptNumber], [ReceiptDate], [SponsorID], [Remarks], [ISIP], [PaymentTermsID], [AmountBeforeChange], [AmountAfterChange], [TotalItems], [SponsorAmount], [PatientAmount], [IPOPNo], [CustomerName], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Reverse], [PatPaidAfterReverse], [Paid], [IsEndVisit], [SponserDiscount], [EntryCodes], [TenantId])
SELECT [Id], [PatientID], [PatientName], [BillType], [TotalAmount], [Discount], [DoctorID], [ReceiptNumber], [ReceiptDate], [SponsorID], [Remarks], [ISIP], [PaymentTermsID], [AmountBeforeChange], [AmountAfterChange], [TotalItems], [SponsorAmount], [PatientAmount], [IPOPNo], [CustomerName], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Reverse], [PatPaidAfterReverse], [Paid], [IsEndVisit], [SponserDiscount], [EntryCodes], @TenantId
FROM [SunCity_Clinics].[Billing].[PatientBill];
SET IDENTITY_INSERT [Billing].[PatientBill] OFF;
GO

PRINT 'Migrating [Billing].[PatientBillservicesCanceled]...';
SET IDENTITY_INSERT [Billing].[PatientBillservicesCanceled] ON;
INSERT INTO [Billing].[PatientBillservicesCanceled] ([ID], [CreatedBy], [CreatedDate], [PrepareVisitSlipID], [InvestigationRequestDetailsID], [UserName], [CashBoxID], [CancelReason], [TenantId])
SELECT [ID], [CreatedBy], [CreatedDate], [PrepareVisitSlipID], [InvestigationRequestDetailsID], [UserName], [CashBoxID], [CancelReason], @TenantId
FROM [SunCity_Clinics].[Billing].[PatientBillservicesCanceled];
SET IDENTITY_INSERT [Billing].[PatientBillservicesCanceled] OFF;
GO

PRINT 'Migrating [Billing].[PatientBillservices]...';
SET IDENTITY_INSERT [Billing].[PatientBillservices] ON;
INSERT INTO [Billing].[PatientBillservices] ([Id], [PatientBillID], [OP_IPNumber], [SponsorName], [SponserCategory], [ItemCode], [ItemName], [RequestNumber], [DoctorID], [Quantity], [Amount], [ItemID], [RequestDate], [ItemTypeId], [SupliesAmount], [PatientBillItemTableValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [AmountBefore], [AmountAfter], [SponserCategoryId], [SponserId], [PaymentTermsId], [IsOPToIPConversion], [CompanyID], [EntryCode], [Reverse], [Paid], [CashierId], [IsService], [Discount], [Vat], [ApprovedCode], [Cash], [Visa], [VisaReceipt], [BillReceipts], [PateintPaidAmount], [SponserPaidAmount], [Fee_Type], [ISCash], [ReceiptDescount], [CashierBoxID], [PaidDate], [IsBooking], [ClinicId], [ReceiptNo], [VisitSlipID], [InvRequestDetID], [discountVal], [DepositPay], [PatVat], [SpoVat], [SponserApproveStatus], [Canceled], [CanceledBy], [CanceledDate], [BranchId], [rejectionReason], [otherReason], [suppliesDtlId], [DiscountOfferValue], [RejectResponsibility], [CopayType], [CopayPer], [patientlimitdtl], [claimRequestID], [zatcaState], [SzatcaState], [PostCashCompanyID], [TenantId])
SELECT [Id], [PatientBillID], [OP_IPNumber], [SponsorName], [SponserCategory], [ItemCode], [ItemName], [RequestNumber], [DoctorID], [Quantity], [Amount], [ItemID], [RequestDate], [ItemTypeId], [SupliesAmount], [PatientBillItemTableValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [AmountBefore], [AmountAfter], [SponserCategoryId], [SponserId], [PaymentTermsId], [IsOPToIPConversion], [CompanyID], [EntryCode], [Reverse], [Paid], [CashierId], [IsService], [Discount], [Vat], [ApprovedCode], [Cash], [Visa], [VisaReceipt], [BillReceipts], [PateintPaidAmount], [SponserPaidAmount], [Fee_Type], [ISCash], [ReceiptDescount], [CashierBoxID], [PaidDate], [IsBooking], [ClinicId], [ReceiptNo], [VisitSlipID], [InvRequestDetID], [discountVal], [DepositPay], [PatVat], [SpoVat], [SponserApproveStatus], [Canceled], [CanceledBy], [CanceledDate], [BranchId], [rejectionReason], [otherReason], [suppliesDtlId], [DiscountOfferValue], [RejectResponsibility], [CopayType], [CopayPer], [patientlimitdtl], [claimRequestID], [zatcaState], [SzatcaState], [PostCashCompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[PatientBillservices];
SET IDENTITY_INSERT [Billing].[PatientBillservices] OFF;
GO

PRINT 'Migrating [Billing].[PatientDepositHistory]...';
SET IDENTITY_INSERT [Billing].[PatientDepositHistory] ON;
INSERT INTO [Billing].[PatientDepositHistory] ([Id], [PatientDepositId], [ServiceId], [ServiceAmount], [CreatedDate], [CreatedBy], [BranchId], [CompanyId], [TenantId])
SELECT [Id], [PatientDepositId], [ServiceId], [ServiceAmount], [CreatedDate], [CreatedBy], [BranchId], [CompanyId], @TenantId
FROM [SunCity_Clinics].[Billing].[PatientDepositHistory];
SET IDENTITY_INSERT [Billing].[PatientDepositHistory] OFF;
GO

PRINT 'Migrating [Billing].[PatientDeposit]...';
SET IDENTITY_INSERT [Billing].[PatientDeposit] ON;
INSERT INTO [Billing].[PatientDeposit] ([Id], [PatientID], [DepositAmount], [IPNumber], [CreatedBy], [CreatedDate], [TypeofPayment], [EntryCodes], [CompanyID], [branchId], [CheckNo], [CheckDate], [ChkImg], [returnDepositAmount], [UserReturnDepositAmountID], [returnDepositAmountDate], [TenantId])
SELECT [Id], [PatientID], [DepositAmount], [IPNumber], [CreatedBy], [CreatedDate], [TypeofPayment], [EntryCodes], [CompanyID], [branchId], [CheckNo], [CheckDate], [ChkImg], [returnDepositAmount], [UserReturnDepositAmountID], [returnDepositAmountDate], @TenantId
FROM [SunCity_Clinics].[Billing].[PatientDeposit];
SET IDENTITY_INSERT [Billing].[PatientDeposit] OFF;
GO

PRINT 'Migrating [Billing].[PatientInsuranceLimitMaster]...';
SET IDENTITY_INSERT [Billing].[PatientInsuranceLimitMaster] ON;
INSERT INTO [Billing].[PatientInsuranceLimitMaster] ([Id], [PatientId], [OP_IPNumber], [ApprovalCode], [Total], [OrderType], [PatientType], [Status], [RequestDate], [RequestedBy], [OverrideBy], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ServiceID], [InvRequestDetailID], [DoctorsDailyScheduleDetailsID], [InsCompID], [Op_SecheduleID], [RequestCode], [branchId], [TenantId])
SELECT [Id], [PatientId], [OP_IPNumber], [ApprovalCode], [Total], [OrderType], [PatientType], [Status], [RequestDate], [RequestedBy], [OverrideBy], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ServiceID], [InvRequestDetailID], [DoctorsDailyScheduleDetailsID], [InsCompID], [Op_SecheduleID], [RequestCode], [branchId], @TenantId
FROM [SunCity_Clinics].[Billing].[PatientInsuranceLimitMaster];
SET IDENTITY_INSERT [Billing].[PatientInsuranceLimitMaster] OFF;
GO

PRINT 'Migrating [Billing].[PatientInsuranceLimitdetail]...';
SET IDENTITY_INSERT [Billing].[PatientInsuranceLimitdetail] ON;
INSERT INTO [Billing].[PatientInsuranceLimitdetail] ([Id], [PatientInsuranceLimitMasterId], [Price], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ServiceID], [Quantity], [SubTotal], [InvRequestDetailID], [DoctorsDailyScheduleDetailsID], [Op_SecheduleID], [DetStatus], [IPOPNo], [ApprovalCode], [Pat_type], [ExceedUserID], [SpecialityID], [AdmitPatientID], [isEmpInsurance], [NameSpokenToinsuranceCompany], [NameSpokenToinsuranceCompanyPhone], [AlertDate], [CancelDate], [ReasonofRejection], [otherReason], [Cancelled], [DentalAssessprocedure], [PrescriptionDetailID], [OrderDetailID], [ChargeItemId], [DisChargeId], [ChemotherapyOrderId], [RejectResponsibility], [toothNo], [opticalprocedure], [opticaltype], [referrel], [Phsubstitute], [Phreason], [dentalmissingreason], [eligibleAmount], [copay], [benfit], [note], [TenantId])
SELECT [Id], [PatientInsuranceLimitMasterId], [Price], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ServiceID], [Quantity], [SubTotal], [InvRequestDetailID], [DoctorsDailyScheduleDetailsID], [Op_SecheduleID], [DetStatus], [IPOPNo], [ApprovalCode], [Pat_type], [ExceedUserID], [SpecialityID], [AdmitPatientID], [isEmpInsurance], [NameSpokenToinsuranceCompany], [NameSpokenToinsuranceCompanyPhone], [AlertDate], [CancelDate], [ReasonofRejection], [otherReason], [Cancelled], [DentalAssessprocedure], [PrescriptionDetailID], [OrderDetailID], [ChargeItemId], [DisChargeId], [ChemotherapyOrderId], [RejectResponsibility], [toothNo], [opticalprocedure], [opticaltype], [referrel], [Phsubstitute], [Phreason], [dentalmissingreason], [eligibleAmount], [copay], [benfit], [note], @TenantId
FROM [SunCity_Clinics].[Billing].[PatientInsuranceLimitdetail];
SET IDENTITY_INSERT [Billing].[PatientInsuranceLimitdetail] OFF;
GO

PRINT 'Migrating [Billing].[Patient_InsuranceApproved]...';
SET IDENTITY_INSERT [Billing].[Patient_InsuranceApproved] ON;
INSERT INTO [Billing].[Patient_InsuranceApproved] ([Id], [PatientID], [SponsorID], [DeptID], [DeptAmount], [TotalAmount], [ApprovedCode], [IPOPNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [SponsorID], [DeptID], [DeptAmount], [TotalAmount], [ApprovedCode], [IPOPNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[Patient_InsuranceApproved];
SET IDENTITY_INSERT [Billing].[Patient_InsuranceApproved] OFF;
GO

PRINT 'Migrating [Billing].[PayExaminationPrice]...';
SET IDENTITY_INSERT [Billing].[PayExaminationPrice] ON;
INSERT INTO [Billing].[PayExaminationPrice] ([ID], [PatientBillserviceID], [ReceiptNo], [CreatedBy], [CreationDate], [TenantId])
SELECT [ID], [PatientBillserviceID], [ReceiptNo], [CreatedBy], [CreationDate], @TenantId
FROM [SunCity_Clinics].[Billing].[PayExaminationPrice];
SET IDENTITY_INSERT [Billing].[PayExaminationPrice] OFF;
GO

PRINT 'Migrating [Billing].[PaymentDetails]...';
SET IDENTITY_INSERT [Billing].[PaymentDetails] ON;
INSERT INTO [Billing].[PaymentDetails] ([Id], [AdvanceReceiptId], [PaymentModeId], [Amount], [CurrencyId], [TransactionNo], [TransactionDate], [BankID], [Details], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [ReceiptNumber], [CompanyID], [TenantId])
SELECT [Id], [AdvanceReceiptId], [PaymentModeId], [Amount], [CurrencyId], [TransactionNo], [TransactionDate], [BankID], [Details], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [ReceiptNumber], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[PaymentDetails];
SET IDENTITY_INSERT [Billing].[PaymentDetails] OFF;
GO

PRINT 'Migrating [Billing].[PaymentModes]...';
SET IDENTITY_INSERT [Billing].[PaymentModes] ON;
INSERT INTO [Billing].[PaymentModes] ([Id], [PaymentModeDescription], [TypePaymentID], [IsActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [PaymentModeDescriptionAr], [PaymentModeDescriptionuk], [TenantId])
SELECT [Id], [PaymentModeDescription], [TypePaymentID], [IsActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [PaymentModeDescriptionAr], [PaymentModeDescriptionuk], @TenantId
FROM [SunCity_Clinics].[Billing].[PaymentModes];
SET IDENTITY_INSERT [Billing].[PaymentModes] OFF;
GO

PRINT 'Migrating [Billing].[PaymentReconciliation]...';
SET IDENTITY_INSERT [Billing].[PaymentReconciliation] ON;
INSERT INTO [Billing].[PaymentReconciliation] ([Id], [Msg], [valueMoney], [insID], [startDate], [EndDate], [nphiesFees], [earlyfees], [payment], [ClaimReq], [ClaimRes], [TenantId])
SELECT [Id], [Msg], [valueMoney], [insID], [startDate], [EndDate], [nphiesFees], [earlyfees], [payment], [ClaimReq], [ClaimRes], @TenantId
FROM [SunCity_Clinics].[Billing].[PaymentReconciliation];
SET IDENTITY_INSERT [Billing].[PaymentReconciliation] OFF;
GO

PRINT 'Migrating [Billing].[PharmacyBillDetail]...';
SET IDENTITY_INSERT [Billing].[PharmacyBillDetail] ON;
INSERT INTO [Billing].[PharmacyBillDetail] ([Id], [PharmacyBillHeaderID], [OP_IPNumber], [SponsorName], [SponserCategory], [ItemCode], [ItemName], [RequestNumber], [DoctorID], [Quantity], [Amount], [ItemID], [RequestDate], [ItemTypeId], [SupliesAmount], [PatientBillItemTableValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [AmountBefore], [AmountAfter], [SponserCategoryId], [SponserId], [PaymentTermsId], [IsOPToIPConversion], [CompanyID], [Price], [TenantId])
SELECT [Id], [PharmacyBillHeaderID], [OP_IPNumber], [SponsorName], [SponserCategory], [ItemCode], [ItemName], [RequestNumber], [DoctorID], [Quantity], [Amount], [ItemID], [RequestDate], [ItemTypeId], [SupliesAmount], [PatientBillItemTableValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [AmountBefore], [AmountAfter], [SponserCategoryId], [SponserId], [PaymentTermsId], [IsOPToIPConversion], [CompanyID], [Price], @TenantId
FROM [SunCity_Clinics].[Billing].[PharmacyBillDetail];
SET IDENTITY_INSERT [Billing].[PharmacyBillDetail] OFF;
GO

PRINT 'Migrating [Billing].[PharmacyBillHeader]...';
SET IDENTITY_INSERT [Billing].[PharmacyBillHeader] ON;
INSERT INTO [Billing].[PharmacyBillHeader] ([Id], [PatientID], [PatientName], [BillType], [TotalAmount], [Discount], [DoctorID], [ReceiptNumber], [ReceiptDate], [SponsorID], [Remarks], [ISIP], [PaymentTermsID], [AmountBeforeChange], [AmountAfterChange], [TotalItems], [SponsorAmount], [PatientAmount], [IPOPNo], [CustomerName], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [PatientName], [BillType], [TotalAmount], [Discount], [DoctorID], [ReceiptNumber], [ReceiptDate], [SponsorID], [Remarks], [ISIP], [PaymentTermsID], [AmountBeforeChange], [AmountAfterChange], [TotalItems], [SponsorAmount], [PatientAmount], [IPOPNo], [CustomerName], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[PharmacyBillHeader];
SET IDENTITY_INSERT [Billing].[PharmacyBillHeader] OFF;
GO

PRINT 'Migrating [Billing].[PointOfSale]...';
SET IDENTITY_INSERT [Billing].[PointOfSale] ON;
INSERT INTO [Billing].[PointOfSale] ([ID], [POSCode], [POSName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [POSCode], [POSName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[PointOfSale];
SET IDENTITY_INSERT [Billing].[PointOfSale] OFF;
GO

PRINT 'Migrating [Billing].[PriceListDepartment]...';
SET IDENTITY_INSERT [Billing].[PriceListDepartment] ON;
INSERT INTO [Billing].[PriceListDepartment] ([PriceListDepartment_ID], [PriceList_Header_Id], [CostcenterID], [Discount_Price], [DeptDiscountValue], [Limit_Cost], [CompanyID], [DeptDiscountType], [ContractDiscountValue], [TenantId])
SELECT [PriceListDepartment_ID], [PriceList_Header_Id], [CostcenterID], [Discount_Price], [DeptDiscountValue], [Limit_Cost], [CompanyID], [DeptDiscountType], [ContractDiscountValue], @TenantId
FROM [SunCity_Clinics].[Billing].[PriceListDepartment];
SET IDENTITY_INSERT [Billing].[PriceListDepartment] OFF;
GO

PRINT 'Migrating [Billing].[PriceListHeader]...';
SET IDENTITY_INSERT [Billing].[PriceListHeader] ON;
INSERT INTO [Billing].[PriceListHeader] ([PriceList_Header_Id], [Name_En], [Name_Ar], [Code], [Date], [Defualt], [CompanyID], [TenantId])
SELECT [PriceList_Header_Id], [Name_En], [Name_Ar], [Code], [Date], [Defualt], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[PriceListHeader];
SET IDENTITY_INSERT [Billing].[PriceListHeader] OFF;
GO

PRINT 'Migrating [Billing].[PriceListServices]...';
SET IDENTITY_INSERT [Billing].[PriceListServices] ON;
INSERT INTO [Billing].[PriceListServices] ([PriceListService_Id], [PriceListDepartment_ID], [ServiceId], [Price], [Discount_Price], [ServDiscountValue], [PriceAfterDiscount], [Approve], [OPPriceAfterDiscount], [PreApprovalInsurance], [ServDiscountType], [TenantId])
SELECT [PriceListService_Id], [PriceListDepartment_ID], [ServiceId], [Price], [Discount_Price], [ServDiscountValue], [PriceAfterDiscount], [Approve], [OPPriceAfterDiscount], [PreApprovalInsurance], [ServDiscountType], @TenantId
FROM [SunCity_Clinics].[Billing].[PriceListServices];
SET IDENTITY_INSERT [Billing].[PriceListServices] OFF;
GO

PRINT 'Migrating [Billing].[RCM_Settings]...';
SET IDENTITY_INSERT [Billing].[RCM_Settings] ON;
INSERT INTO [Billing].[RCM_Settings] ([TempBook_Hours], [id], [TenantId])
SELECT [TempBook_Hours], [id], @TenantId
FROM [SunCity_Clinics].[Billing].[RCM_Settings];
SET IDENTITY_INSERT [Billing].[RCM_Settings] OFF;
GO

PRINT 'Migrating [Billing].[RefundReceipts]...';
SET IDENTITY_INSERT [Billing].[RefundReceipts] ON;
INSERT INTO [Billing].[RefundReceipts] ([Id], [ReceiptNumber], [DiscountTypeID], [RefundAmount], [RefundPerecent], [remarks], [TotalRefund], [Balance], [Excess], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [AdvanceReceiptsItemId], [CompanyID], [TenantId])
SELECT [Id], [ReceiptNumber], [DiscountTypeID], [RefundAmount], [RefundPerecent], [remarks], [TotalRefund], [Balance], [Excess], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [AdvanceReceiptsItemId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[RefundReceipts];
SET IDENTITY_INSERT [Billing].[RefundReceipts] OFF;
GO

PRINT 'Migrating [Billing].[RegisterPackageInstallment]...';
SET IDENTITY_INSERT [Billing].[RegisterPackageInstallment] ON;
INSERT INTO [Billing].[RegisterPackageInstallment] ([Id], [RegisterPackageID], [InstallmentName], [Amount], [Duration], [Period], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [RegisterPackageID], [InstallmentName], [Amount], [Duration], [Period], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[RegisterPackageInstallment];
SET IDENTITY_INSERT [Billing].[RegisterPackageInstallment] OFF;
GO

PRINT 'Migrating [Billing].[RegisterPackage]...';
SET IDENTITY_INSERT [Billing].[RegisterPackage] ON;
INSERT INTO [Billing].[RegisterPackage] ([Id], [PatientID], [PatientTypeID], [OPIPNO], [InvestigationRequestDetailsID], [RegistrationNO], [RegisterDate], [PackageName], [PackageAmount], [PackageDiscount], [PackageNetAmount], [TerminatePackageRegistration], [InvoiceNO], [BalanceAmount], [InstallmentsAmount], [CollectedAmount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [PatientTypeID], [OPIPNO], [InvestigationRequestDetailsID], [RegistrationNO], [RegisterDate], [PackageName], [PackageAmount], [PackageDiscount], [PackageNetAmount], [TerminatePackageRegistration], [InvoiceNO], [BalanceAmount], [InstallmentsAmount], [CollectedAmount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[RegisterPackage];
SET IDENTITY_INSERT [Billing].[RegisterPackage] OFF;
GO

PRINT 'Migrating [Billing].[Rejected_Claim]...';
SET IDENTITY_INSERT [Billing].[Rejected_Claim] ON;
INSERT INTO [Billing].[Rejected_Claim] ([id], [PatientBillServiceId], [Rejection_Cause], [Comments], [ClaimMasterID], [RejectDate], [CreatedDate], [CreatedBy], [CompanyID], [TenantId])
SELECT [id], [PatientBillServiceId], [Rejection_Cause], [Comments], [ClaimMasterID], [RejectDate], [CreatedDate], [CreatedBy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[Rejected_Claim];
SET IDENTITY_INSERT [Billing].[Rejected_Claim] OFF;
GO

PRINT 'Migrating [Billing].[RevenueTypes]...';
SET IDENTITY_INSERT [Billing].[RevenueTypes] ON;
INSERT INTO [Billing].[RevenueTypes] ([Id], [RevenueTypeName], [IsDirectIncome], [AllowDiscount], [InventoryType], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [RevenueTypeNameAr], [TenantId])
SELECT [Id], [RevenueTypeName], [IsDirectIncome], [AllowDiscount], [InventoryType], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [RevenueTypeNameAr], @TenantId
FROM [SunCity_Clinics].[Billing].[RevenueTypes];
SET IDENTITY_INSERT [Billing].[RevenueTypes] OFF;
GO

PRINT 'Migrating [Billing].[SpecialUserDiscount]...';
SET IDENTITY_INSERT [Billing].[SpecialUserDiscount] ON;
INSERT INTO [Billing].[SpecialUserDiscount] ([Id], [UserID], [DiscountPercentage], [IsActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [UserID], [DiscountPercentage], [IsActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Billing].[SpecialUserDiscount];
SET IDENTITY_INSERT [Billing].[SpecialUserDiscount] OFF;
GO

PRINT 'Migrating [Billing].[Sponsor]...';
SET IDENTITY_INSERT [Billing].[Sponsor] ON;
INSERT INTO [Billing].[Sponsor] ([Id], [Code], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameAr], [TenantId])
SELECT [Id], [Code], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameAr], @TenantId
FROM [SunCity_Clinics].[Billing].[Sponsor];
SET IDENTITY_INSERT [Billing].[Sponsor] OFF;
GO

PRINT 'Migrating [Billing].[SupplierDues]...';
SET IDENTITY_INSERT [Billing].[SupplierDues] ON;
INSERT INTO [Billing].[SupplierDues] ([Id], [SupplierID], [DueDate], [MoneyAmount], [LPOHeadID], [GRNHeadID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IsPharmacy], [VatTax], [HoldingTax], [expenses], [TenantId])
SELECT [Id], [SupplierID], [DueDate], [MoneyAmount], [LPOHeadID], [GRNHeadID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IsPharmacy], [VatTax], [HoldingTax], [expenses], @TenantId
FROM [SunCity_Clinics].[Billing].[SupplierDues];
SET IDENTITY_INSERT [Billing].[SupplierDues] OFF;
GO

PRINT 'Migrating [Billing].[TaxReturn]...';
SET IDENTITY_INSERT [Billing].[TaxReturn] ON;
INSERT INTO [Billing].[TaxReturn] ([id], [year], [VatSales_Original], [VatSales_Edit], [VatValue], [CitizenSales_Original], [CitizenSales_Edit], [CitizenSales_Net], [NoVatSales_Original], [NoVatSales_Edit], [NoVatSales_Net], [TotalSales_Original], [TotalSales_Edit], [TotalSales_Net], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [VatPurch_Original], [VatPurch_Edit], [VatPurch_Net], [VatExports_Original], [VatExports_Edit], [VatExports_Net], [VatPurchReturn_Original], [VatPurchReturn_Edit], [VatPurchReturn_Net], [NoVatPurch_Original], [NoVatPurch_Edit], [NoVatPurch_Net], [ExemptPurch_Original], [ExemptPurch_Edit], [ExemptPurch_Net], [TotalPurch_Original], [TotalPurch_Edit], [TotalPurch_Net], [TenantId])
SELECT [id], [year], [VatSales_Original], [VatSales_Edit], [VatValue], [CitizenSales_Original], [CitizenSales_Edit], [CitizenSales_Net], [NoVatSales_Original], [NoVatSales_Edit], [NoVatSales_Net], [TotalSales_Original], [TotalSales_Edit], [TotalSales_Net], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [VatPurch_Original], [VatPurch_Edit], [VatPurch_Net], [VatExports_Original], [VatExports_Edit], [VatExports_Net], [VatPurchReturn_Original], [VatPurchReturn_Edit], [VatPurchReturn_Net], [NoVatPurch_Original], [NoVatPurch_Edit], [NoVatPurch_Net], [ExemptPurch_Original], [ExemptPurch_Edit], [ExemptPurch_Net], [TotalPurch_Original], [TotalPurch_Edit], [TotalPurch_Net], @TenantId
FROM [SunCity_Clinics].[Billing].[TaxReturn];
SET IDENTITY_INSERT [Billing].[TaxReturn] OFF;
GO

PRINT 'Migrating [Billing].[TicketingSetting]...';
SET IDENTITY_INSERT [Billing].[TicketingSetting] ON;
INSERT INTO [Billing].[TicketingSetting] ([ID], [visitsNo], [ticketsNo], [Book_mo], [Book_ni], [visitService], [ticket_item], [visit_item], [nightTime], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [userID], [TenantId])
SELECT [ID], [visitsNo], [ticketsNo], [Book_mo], [Book_ni], [visitService], [ticket_item], [visit_item], [nightTime], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [userID], @TenantId
FROM [SunCity_Clinics].[Billing].[TicketingSetting];
SET IDENTITY_INSERT [Billing].[TicketingSetting] OFF;
GO

PRINT 'Migrating [Billing].[VisitorPayment]...';
SET IDENTITY_INSERT [Billing].[VisitorPayment] ON;
INSERT INTO [Billing].[VisitorPayment] ([Id], [PatientId], [IPNumber], [Code], [VisitorName], [Cost], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [Id], [PatientId], [IPNumber], [Code], [VisitorName], [Cost], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[Billing].[VisitorPayment];
SET IDENTITY_INSERT [Billing].[VisitorPayment] OFF;
GO

PRINT 'Migrating [Billing].[VoucherDetails]...';
SET IDENTITY_INSERT [Billing].[VoucherDetails] ON;
INSERT INTO [Billing].[VoucherDetails] ([ID], [VoucherID], [Amount], [Description], [AccountID], [CostCenterID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Cash], [Visa], [TenantId])
SELECT [ID], [VoucherID], [Amount], [Description], [AccountID], [CostCenterID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Cash], [Visa], @TenantId
FROM [SunCity_Clinics].[Billing].[VoucherDetails];
SET IDENTITY_INSERT [Billing].[VoucherDetails] OFF;
GO

PRINT 'Migrating [Billing].[Voucher]...';
SET IDENTITY_INSERT [Billing].[Voucher] ON;
INSERT INTO [Billing].[Voucher] ([ID], [BoxID], [CurrencyID], [VoucherCode], [DescriptionAR], [DescriptionEN], [PersonNameAr], [PersonNameEN], [Date], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [VoucherType], [EntryCode], [IsConfirmed], [PatientID], [branchId], [TenantId])
SELECT [ID], [BoxID], [CurrencyID], [VoucherCode], [DescriptionAR], [DescriptionEN], [PersonNameAr], [PersonNameEN], [Date], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [VoucherType], [EntryCode], [IsConfirmed], [PatientID], [branchId], @TenantId
FROM [SunCity_Clinics].[Billing].[Voucher];
SET IDENTITY_INSERT [Billing].[Voucher] OFF;
GO

PRINT 'Migrating [Billing].[Yearly_Tender]...';
SET IDENTITY_INSERT [Billing].[Yearly_Tender] ON;
INSERT INTO [Billing].[Yearly_Tender] ([id], [YearNo], [DrugID], [GenericID], [DrugFormsID], [Strenght], [MonthNo], [Despensed_InPeriod], [Avarage_Consumption], [Required_Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [LPODetailID], [UnitID], [YearlyID], [IsEmergency], [EmergencyDate], [UnitConversionID], [TenantId])
SELECT [id], [YearNo], [DrugID], [GenericID], [DrugFormsID], [Strenght], [MonthNo], [Despensed_InPeriod], [Avarage_Consumption], [Required_Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [LPODetailID], [UnitID], [YearlyID], [IsEmergency], [EmergencyDate], [UnitConversionID], @TenantId
FROM [SunCity_Clinics].[Billing].[Yearly_Tender];
SET IDENTITY_INSERT [Billing].[Yearly_Tender] OFF;
GO

PRINT 'Migrating [Billing].[ZatcaEInvoice]...';
SET IDENTITY_INSERT [Billing].[ZatcaEInvoice] ON;
INSERT INTO [Billing].[ZatcaEInvoice] ([Id], [PatientBillID], [Xml], [Send], [Signat], [qr], [Response], [XmlEnc], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [Id], [PatientBillID], [Xml], [Send], [Signat], [qr], [Response], [XmlEnc], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[Billing].[ZatcaEInvoice];
SET IDENTITY_INSERT [Billing].[ZatcaEInvoice] OFF;
GO

PRINT 'Migrating [Billing].[ins_Company_ClassCostCenter]...';
SET IDENTITY_INSERT [Billing].[ins_Company_ClassCostCenter] ON;
INSERT INTO [Billing].[ins_Company_ClassCostCenter] ([ID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CostCenterID], [Percentage], [Value], [IsInPatient], [ins_Company_ClassID], [TenantId])
SELECT [ID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CostCenterID], [Percentage], [Value], [IsInPatient], [ins_Company_ClassID], @TenantId
FROM [SunCity_Clinics].[Billing].[ins_Company_ClassCostCenter];
SET IDENTITY_INSERT [Billing].[ins_Company_ClassCostCenter] OFF;
GO

PRINT 'Migrating [Billing].[ins_Company_Class]...';
SET IDENTITY_INSERT [Billing].[ins_Company_Class] ON;
INSERT INTO [Billing].[ins_Company_Class] ([ID], [Code], [PolicyID], [ClassNO], [Network], [Benefit_Room], [Sepcial_Conditions], [Referral_Letter], [Other], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OutPatientInclude_Consultation], [OutPatientInclude_Medication], [OutPatientPre_Approval_Limit], [OutpatientPeriodType], [OutpatientdiscountType], [OutpatientLimitType], [InPatientInclude_Consultation], [InPatientInclude_Medication], [InPatientPre_Approval_Limit], [InpatientPeriodType], [InpatientdiscountType], [InpatientLimitType], [InPatientDeductionPerc], [InPatientDeductionMax], [OutPatientDeductionPerc], [OutPatientDeductionMax], [OutpatientPeriod], [OutpatientDeductibleType], [InpatientDeductibleType], [OutpatientDeductibleTypeForDrug], [OutpatientDeductibleTypeForDrugBrand], [OutpatientDeductibleTypeForDrugGeneric], [InpatientDeductibleTypeForDrug], [InpatientDeductibleTypeForDrugBrand], [InpatientDeductibleTypeForDrugGeneric], [InPatientDeductionPercForDrug], [InPatientDeductionPercForDrugBrand], [InPatientDeductionPercForDrugGeneric], [OutPatientDeductionPercForDrug], [OutPatientDeductionPercForDrugBrand], [OutPatientDeductionPercForDrugGeneric], [InpatientCostCenterID], [InpatientPercentage], [InpatientValue], [OutpatientCostCenterID], [OutpatientPercentage], [OutpatientValue], [Outpatientmaxfordrug], [InpatientPeriod], [Inpatientmaxfordrug], [TenantId])
SELECT [ID], [Code], [PolicyID], [ClassNO], [Network], [Benefit_Room], [Sepcial_Conditions], [Referral_Letter], [Other], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OutPatientInclude_Consultation], [OutPatientInclude_Medication], [OutPatientPre_Approval_Limit], [OutpatientPeriodType], [OutpatientdiscountType], [OutpatientLimitType], [InPatientInclude_Consultation], [InPatientInclude_Medication], [InPatientPre_Approval_Limit], [InpatientPeriodType], [InpatientdiscountType], [InpatientLimitType], [InPatientDeductionPerc], [InPatientDeductionMax], [OutPatientDeductionPerc], [OutPatientDeductionMax], [OutpatientPeriod], [OutpatientDeductibleType], [InpatientDeductibleType], [OutpatientDeductibleTypeForDrug], [OutpatientDeductibleTypeForDrugBrand], [OutpatientDeductibleTypeForDrugGeneric], [InpatientDeductibleTypeForDrug], [InpatientDeductibleTypeForDrugBrand], [InpatientDeductibleTypeForDrugGeneric], [InPatientDeductionPercForDrug], [InPatientDeductionPercForDrugBrand], [InPatientDeductionPercForDrugGeneric], [OutPatientDeductionPercForDrug], [OutPatientDeductionPercForDrugBrand], [OutPatientDeductionPercForDrugGeneric], [InpatientCostCenterID], [InpatientPercentage], [InpatientValue], [OutpatientCostCenterID], [OutpatientPercentage], [OutpatientValue], [Outpatientmaxfordrug], [InpatientPeriod], [Inpatientmaxfordrug], @TenantId
FROM [SunCity_Clinics].[Billing].[ins_Company_Class];
SET IDENTITY_INSERT [Billing].[ins_Company_Class] OFF;
GO

PRINT 'Migrating [BloodBank].[BloodBankInventoryBloodGroup]...';
SET IDENTITY_INSERT [BloodBank].[BloodBankInventoryBloodGroup] ON;
INSERT INTO [BloodBank].[BloodBankInventoryBloodGroup] ([Id], [BloodBankInventoryId], [BloodGroupId], [TenantId])
SELECT [Id], [BloodBankInventoryId], [BloodGroupId], @TenantId
FROM [SunCity_Clinics].[BloodBank].[BloodBankInventoryBloodGroup];
SET IDENTITY_INSERT [BloodBank].[BloodBankInventoryBloodGroup] OFF;
GO

PRINT 'Migrating [BloodBank].[BloodBankInventory]...';
SET IDENTITY_INSERT [BloodBank].[BloodBankInventory] ON;
INSERT INTO [BloodBank].[BloodBankInventory] ([ID], [Code], [BagNo], [Volume], [BloodProductID], [Price], [ExpireDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DonorID], [DonationInfoID], [ParentID], [NoUnits], [CompanyID], [NameAr], [NameEn], [StoreID], [BloodGroupID], [TenantId])
SELECT [ID], [Code], [BagNo], [Volume], [BloodProductID], [Price], [ExpireDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DonorID], [DonationInfoID], [ParentID], [NoUnits], [CompanyID], [NameAr], [NameEn], [StoreID], [BloodGroupID], @TenantId
FROM [SunCity_Clinics].[BloodBank].[BloodBankInventory];
SET IDENTITY_INSERT [BloodBank].[BloodBankInventory] OFF;
GO

PRINT 'Migrating [BloodBank].[BloodBankLPODetail]...';
SET IDENTITY_INSERT [BloodBank].[BloodBankLPODetail] ON;
INSERT INTO [BloodBank].[BloodBankLPODetail] ([Id], [LPOHeaderId], [BloodGroupId], [OrderedQTY], [BonusQTY], [AcceptQTY], [PrevAcceptQTY], [Amount], [Price], [CreatedDate], [CreatedBy], [LastModifiedDate], [LastModifiedBy], [CompanyId], [TenantId])
SELECT [Id], [LPOHeaderId], [BloodGroupId], [OrderedQTY], [BonusQTY], [AcceptQTY], [PrevAcceptQTY], [Amount], [Price], [CreatedDate], [CreatedBy], [LastModifiedDate], [LastModifiedBy], [CompanyId], @TenantId
FROM [SunCity_Clinics].[BloodBank].[BloodBankLPODetail];
SET IDENTITY_INSERT [BloodBank].[BloodBankLPODetail] OFF;
GO

PRINT 'Migrating [BloodBank].[BloodBankLPOHeader]...';
SET IDENTITY_INSERT [BloodBank].[BloodBankLPOHeader] ON;
INSERT INTO [BloodBank].[BloodBankLPOHeader] ([Id], [InventoryId], [SupplierId], [LPOCode], [PurchaseOrderNo], [LPODate], [Status], [CreatedDate], [CreatedBy], [LastModifiedDate], [LastModifiedBy], [CompanyId], [IsEmergency], [Remarks], [TenantId])
SELECT [Id], [InventoryId], [SupplierId], [LPOCode], [PurchaseOrderNo], [LPODate], [Status], [CreatedDate], [CreatedBy], [LastModifiedDate], [LastModifiedBy], [CompanyId], [IsEmergency], [Remarks], @TenantId
FROM [SunCity_Clinics].[BloodBank].[BloodBankLPOHeader];
SET IDENTITY_INSERT [BloodBank].[BloodBankLPOHeader] OFF;
GO

PRINT 'Migrating [BloodBank].[BloodBankSettings]...';
SET IDENTITY_INSERT [BloodBank].[BloodBankSettings] ON;
INSERT INTO [BloodBank].[BloodBankSettings] ([Id], [Expiry], [DonerPeriodBlock], [DonerPeriodWorning], [DPrice], [DonationAccount], [DonationPaidAccount], [CompanyID], [Minbloodbags], [TenantId])
SELECT [Id], [Expiry], [DonerPeriodBlock], [DonerPeriodWorning], [DPrice], [DonationAccount], [DonationPaidAccount], [CompanyID], [Minbloodbags], @TenantId
FROM [SunCity_Clinics].[BloodBank].[BloodBankSettings];
SET IDENTITY_INSERT [BloodBank].[BloodBankSettings] OFF;
GO

PRINT 'Migrating [BloodBank].[BloodBanks]...';
SET IDENTITY_INSERT [BloodBank].[BloodBanks] ON;
INSERT INTO [BloodBank].[BloodBanks] ([id], [code], [NameAr], [NameEn], [Address], [Email], [Phone], [Fax], [IsActive], [PersonOnCharge], [StoreID], [TenantId])
SELECT [id], [code], [NameAr], [NameEn], [Address], [Email], [Phone], [Fax], [IsActive], [PersonOnCharge], [StoreID], @TenantId
FROM [SunCity_Clinics].[BloodBank].[BloodBanks];
SET IDENTITY_INSERT [BloodBank].[BloodBanks] OFF;
GO

PRINT 'Migrating [BloodBank].[BloodGroupCompatable]...';
SET IDENTITY_INSERT [BloodBank].[BloodGroupCompatable] ON;
INSERT INTO [BloodBank].[BloodGroupCompatable] ([ID], [BloodGroupID], [BloodGroupCompatableID], [CreatedBy], [CreatedDate], [CompanyID], [given], [Taken], [TenantId])
SELECT [ID], [BloodGroupID], [BloodGroupCompatableID], [CreatedBy], [CreatedDate], [CompanyID], [given], [Taken], @TenantId
FROM [SunCity_Clinics].[BloodBank].[BloodGroupCompatable];
SET IDENTITY_INSERT [BloodBank].[BloodGroupCompatable] OFF;
GO

PRINT 'Migrating [BloodBank].[BloodGroup]...';
SET IDENTITY_INSERT [BloodBank].[BloodGroup] ON;
INSERT INTO [BloodBank].[BloodGroup] ([ID], [BloodGroupCode], [GroupNameLatin], [GroupNameLocal], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [BloodGroupCode], [GroupNameLatin], [GroupNameLocal], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[BloodBank].[BloodGroup];
SET IDENTITY_INSERT [BloodBank].[BloodGroup] OFF;
GO

PRINT 'Migrating [BloodBank].[BloodProduct]...';
SET IDENTITY_INSERT [BloodBank].[BloodProduct] ON;
INSERT INTO [BloodBank].[BloodProduct] ([ID], [BloodProductCode], [ProductNameLatin], [ProductNameLocal], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BloodGroupId], [Active], [HasExpireDate], [ExpireDateValue], [ExpireDateType], [IsDonation], [NameKa], [TenantId])
SELECT [ID], [BloodProductCode], [ProductNameLatin], [ProductNameLocal], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BloodGroupId], [Active], [HasExpireDate], [ExpireDateValue], [ExpireDateType], [IsDonation], [NameKa], @TenantId
FROM [SunCity_Clinics].[BloodBank].[BloodProduct];
SET IDENTITY_INSERT [BloodBank].[BloodProduct] OFF;
GO

PRINT 'Migrating [BloodBank].[BloodServicePrice]...';
SET IDENTITY_INSERT [BloodBank].[BloodServicePrice] ON;
INSERT INTO [BloodBank].[BloodServicePrice] ([ID], [BloodGroupID], [ServiceID], [BloodProductID], [Price], [CreatedBy], [CreatedDate], [CompanyID], [DonationServiceID], [TenantId])
SELECT [ID], [BloodGroupID], [ServiceID], [BloodProductID], [Price], [CreatedBy], [CreatedDate], [CompanyID], [DonationServiceID], @TenantId
FROM [SunCity_Clinics].[BloodBank].[BloodServicePrice];
SET IDENTITY_INSERT [BloodBank].[BloodServicePrice] OFF;
GO

PRINT 'Migrating [BloodBank].[BloodStreamDetails]...';
SET IDENTITY_INSERT [BloodBank].[BloodStreamDetails] ON;
INSERT INTO [BloodBank].[BloodStreamDetails] ([ID], [BloodStreamId], [Dailyreview], [Performhandhygiene], [Maintainaseptictechnique], [Thedisinfectionofcatheterhub], [UseaCVC], [Usesteriletransparent], [Gauzedressings], [Transparentdressings], [IVadministrationsystem], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Date], [Time], [TenantId])
SELECT [ID], [BloodStreamId], [Dailyreview], [Performhandhygiene], [Maintainaseptictechnique], [Thedisinfectionofcatheterhub], [UseaCVC], [Usesteriletransparent], [Gauzedressings], [Transparentdressings], [IVadministrationsystem], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Date], [Time], @TenantId
FROM [SunCity_Clinics].[BloodBank].[BloodStreamDetails];
SET IDENTITY_INSERT [BloodBank].[BloodStreamDetails] OFF;
GO

PRINT 'Migrating [BloodBank].[BloodStream]...';
SET IDENTITY_INSERT [BloodBank].[BloodStream] ON;
INSERT INTO [BloodBank].[BloodStream] ([Id], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [NurseID], [DoctorID], [PatientId], [Cathetertype], [PerformhandBloodStream], [Usebetadine], [Maximalsterile], [CompanyID], [TenantId])
SELECT [Id], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [NurseID], [DoctorID], [PatientId], [Cathetertype], [PerformhandBloodStream], [Usebetadine], [Maximalsterile], [CompanyID], @TenantId
FROM [SunCity_Clinics].[BloodBank].[BloodStream];
SET IDENTITY_INSERT [BloodBank].[BloodStream] OFF;
GO

PRINT 'Migrating [BloodBank].[BloodTestingResultDetail]...';
SET IDENTITY_INSERT [BloodBank].[BloodTestingResultDetail] ON;
INSERT INTO [BloodBank].[BloodTestingResultDetail] ([ID], [BagID], [CreatedBy], [CreatedDate], [CompanyID], [TestID], [Result], [TenantId])
SELECT [ID], [BagID], [CreatedBy], [CreatedDate], [CompanyID], [TestID], [Result], @TenantId
FROM [SunCity_Clinics].[BloodBank].[BloodTestingResultDetail];
SET IDENTITY_INSERT [BloodBank].[BloodTestingResultDetail] OFF;
GO

PRINT 'Migrating [BloodBank].[BloodTestingResultMaster]...';
SET IDENTITY_INSERT [BloodBank].[BloodTestingResultMaster] ON;
INSERT INTO [BloodBank].[BloodTestingResultMaster] ([ID], [BagID], [CreatedBy], [CreatedDate], [CompanyID], [firstResult], [SecondResult], [TenantId])
SELECT [ID], [BagID], [CreatedBy], [CreatedDate], [CompanyID], [firstResult], [SecondResult], @TenantId
FROM [SunCity_Clinics].[BloodBank].[BloodTestingResultMaster];
SET IDENTITY_INSERT [BloodBank].[BloodTestingResultMaster] OFF;
GO

PRINT 'Migrating [BloodBank].[BloodTesting]...';
SET IDENTITY_INSERT [BloodBank].[BloodTesting] ON;
INSERT INTO [BloodBank].[BloodTesting] ([ID], [CreatedBy], [CreatedDate], [CompanyID], [Description], [Code], [TestType], [LabSection], [ServiceId], [TenantId])
SELECT [ID], [CreatedBy], [CreatedDate], [CompanyID], [Description], [Code], [TestType], [LabSection], [ServiceId], @TenantId
FROM [SunCity_Clinics].[BloodBank].[BloodTesting];
SET IDENTITY_INSERT [BloodBank].[BloodTesting] OFF;
GO

PRINT 'Migrating [BloodBank].[BloodTransferRequest]...';
SET IDENTITY_INSERT [BloodBank].[BloodTransferRequest] ON;
INSERT INTO [BloodBank].[BloodTransferRequest] ([ID], [TransferRequestCode], [RequestDate], [PatientID], [BloodProuductID], [Quantity], [Notes], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [TransferRequestCode], [RequestDate], [PatientID], [BloodProuductID], [Quantity], [Notes], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[BloodBank].[BloodTransferRequest];
SET IDENTITY_INSERT [BloodBank].[BloodTransferRequest] OFF;
GO

PRINT 'Migrating [BloodBank].[BloodTransfusion]...';
SET IDENTITY_INSERT [BloodBank].[BloodTransfusion] ON;
INSERT INTO [BloodBank].[BloodTransfusion] ([Id], [PatientId], [DoctorId], [NurseId], [ReactionDate], [ReactionTime], [Reasonfortransfusion], [Component], [VolumeGiven], [DonationNumberinUnits], [IsChills], [IsUrticaria], [IsTachycadia], [IsChestPain], [IsNausea], [IsDyspnoea], [IsLumbarPain], [IsBurningaroundveinhypotension], [IsHaemoglobinuria], [IsExcessiveBleeding], [IsJaundice], [IsShock], [otherSymptoms], [treatmentgiven], [result], [previoustransfusion], [Reactions], [Pregnancies], [KnownAntiBodies], [TransfusionDate], [TransfusionTime], [signture], [BloodBankrequestrecivedDate], [BloodBankrequestrecivedTime], [LabellingError], [pretransfusion], [preAntibody], [preDAT], [Posttransfusion], [PostAntibody], [PostDAT], [CompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [InvestDtlID], [ISPregnancies], [otherDoctor], [TimeOfInformDR], [TransfusionStartTime], [TransfusionEndTime], [IsTransfusionAction], [dispensedBag], [TenantId])
SELECT [Id], [PatientId], [DoctorId], [NurseId], [ReactionDate], [ReactionTime], [Reasonfortransfusion], [Component], [VolumeGiven], [DonationNumberinUnits], [IsChills], [IsUrticaria], [IsTachycadia], [IsChestPain], [IsNausea], [IsDyspnoea], [IsLumbarPain], [IsBurningaroundveinhypotension], [IsHaemoglobinuria], [IsExcessiveBleeding], [IsJaundice], [IsShock], [otherSymptoms], [treatmentgiven], [result], [previoustransfusion], [Reactions], [Pregnancies], [KnownAntiBodies], [TransfusionDate], [TransfusionTime], [signture], [BloodBankrequestrecivedDate], [BloodBankrequestrecivedTime], [LabellingError], [pretransfusion], [preAntibody], [preDAT], [Posttransfusion], [PostAntibody], [PostDAT], [CompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [InvestDtlID], [ISPregnancies], [otherDoctor], [TimeOfInformDR], [TransfusionStartTime], [TransfusionEndTime], [IsTransfusionAction], [dispensedBag], @TenantId
FROM [SunCity_Clinics].[BloodBank].[BloodTransfusion];
SET IDENTITY_INSERT [BloodBank].[BloodTransfusion] OFF;
GO

PRINT 'Migrating [BloodBank].[BloodTransfustionActiontaken]...';
SET IDENTITY_INSERT [BloodBank].[BloodTransfustionActiontaken] ON;
INSERT INTO [BloodBank].[BloodTransfustionActiontaken] ([Id], [BloodTransfusionId], [TimeofAction], [DetailsofAction], [Comments], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [Id], [BloodTransfusionId], [TimeofAction], [DetailsofAction], [Comments], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[BloodBank].[BloodTransfustionActiontaken];
SET IDENTITY_INSERT [BloodBank].[BloodTransfustionActiontaken] OFF;
GO

PRINT 'Migrating [BloodBank].[Campaign]...';
SET IDENTITY_INSERT [BloodBank].[Campaign] ON;
INSERT INTO [BloodBank].[Campaign] ([ID], [NameEn], [CreatedBy], [CreatedDate], [CompanyID], [NameAr], [CampDate], [location], [NoDoctors], [NoNurses], [NoTechnicians], [Comments], [NoBags], [NoDoners], [Code], [TenantId])
SELECT [ID], [NameEn], [CreatedBy], [CreatedDate], [CompanyID], [NameAr], [CampDate], [location], [NoDoctors], [NoNurses], [NoTechnicians], [Comments], [NoBags], [NoDoners], [Code], @TenantId
FROM [SunCity_Clinics].[BloodBank].[Campaign];
SET IDENTITY_INSERT [BloodBank].[Campaign] OFF;
GO

PRINT 'Migrating [BloodBank].[ClabsiBundle]...';
SET IDENTITY_INSERT [BloodBank].[ClabsiBundle] ON;
INSERT INTO [BloodBank].[ClabsiBundle] ([Id], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [NurseID], [DoctorID], [PatientId], [Appropriateindicationforindwellingurinarycatheterplacement], [RiskforUTIevaluatedpriortopatientcatheterization], [Alternativestoindwellingcatheterplacementdiscussedpriortocatheterization], [AnymajorpreexistingconditionsuchasDMmalnutritionorrenalinsufficiency], [Performhandhygieneimmediatelybeforeandafterinsertionofthecatheterdeviceorsite], [Securecathetertubingtopreventurethralirritationandmovementofurethraltraction], [Usesterile], [Positionthedrainage], [Maintainstrict], [Checksystem], [Strictprolongedimmobilization], [Bladderoutletobstruction], [Improvecomfortforendoflife], [Assist], [CompanyID], [AppropriateindicationforindwellingurinarycatheterplacementOther], [TenantId])
SELECT [Id], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [NurseID], [DoctorID], [PatientId], [Appropriateindicationforindwellingurinarycatheterplacement], [RiskforUTIevaluatedpriortopatientcatheterization], [Alternativestoindwellingcatheterplacementdiscussedpriortocatheterization], [AnymajorpreexistingconditionsuchasDMmalnutritionorrenalinsufficiency], [Performhandhygieneimmediatelybeforeandafterinsertionofthecatheterdeviceorsite], [Securecathetertubingtopreventurethralirritationandmovementofurethraltraction], [Usesterile], [Positionthedrainage], [Maintainstrict], [Checksystem], [Strictprolongedimmobilization], [Bladderoutletobstruction], [Improvecomfortforendoflife], [Assist], [CompanyID], [AppropriateindicationforindwellingurinarycatheterplacementOther], @TenantId
FROM [SunCity_Clinics].[BloodBank].[ClabsiBundle];
SET IDENTITY_INSERT [BloodBank].[ClabsiBundle] OFF;
GO

PRINT 'Migrating [BloodBank].[DBloodBags]...';
SET IDENTITY_INSERT [BloodBank].[DBloodBags] ON;
INSERT INTO [BloodBank].[DBloodBags] ([ID], [DonorID], [DonationType], [donationDate], [ExpiryDate], [Code], [CreatedDate], [CreatedBy], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BloodGroup_ID], [Size], [BagType], [CampaignID], [OutSourceID], [Status], [FirstResult], [SecondResult], [FinalResult], [BagDeal], [VoucherID], [Amount], [ParentID], [ScrapReson], [ScrapDate], [Serial], [Refrigerator], [Volume], [TubeNo], [BladderType], [QuestionnaireNumber], [DonationReason], [DonationPeriod], [DeliveryDate], [Receipt], [PurchasePrice], [PatientId], [NumberTo], [EntryCodes], [StoreID], [AdjustmentEntryID], [AdjustmentEntryState], [TenantId])
SELECT [ID], [DonorID], [DonationType], [donationDate], [ExpiryDate], [Code], [CreatedDate], [CreatedBy], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BloodGroup_ID], [Size], [BagType], [CampaignID], [OutSourceID], [Status], [FirstResult], [SecondResult], [FinalResult], [BagDeal], [VoucherID], [Amount], [ParentID], [ScrapReson], [ScrapDate], [Serial], [Refrigerator], [Volume], [TubeNo], [BladderType], [QuestionnaireNumber], [DonationReason], [DonationPeriod], [DeliveryDate], [Receipt], [PurchasePrice], [PatientId], [NumberTo], [EntryCodes], [StoreID], [AdjustmentEntryID], [AdjustmentEntryState], @TenantId
FROM [SunCity_Clinics].[BloodBank].[DBloodBags];
SET IDENTITY_INSERT [BloodBank].[DBloodBags] OFF;
GO

PRINT 'Migrating [BloodBank].[DispenseBloodBags]...';
SET IDENTITY_INSERT [BloodBank].[DispenseBloodBags] ON;
INSERT INTO [BloodBank].[DispenseBloodBags] ([ID], [PatientID], [BloodGroup], [BagID], [BagSource], [DonorID], [ChequeDonor], [ChequeLocation], [ChequeExpiryDate], [CreatedDate], [CreatedBy], [CompanyID], [DispenseDate], [BagCode], [OP_IPNumber], [DoctorID], [ServiceID], [ReqBloodGroup], [ReqProduct], [ReceiverName], [Quantity], [DepartmentAndWard], [InvestDtlID], [transform], [TenantId])
SELECT [ID], [PatientID], [BloodGroup], [BagID], [BagSource], [DonorID], [ChequeDonor], [ChequeLocation], [ChequeExpiryDate], [CreatedDate], [CreatedBy], [CompanyID], [DispenseDate], [BagCode], [OP_IPNumber], [DoctorID], [ServiceID], [ReqBloodGroup], [ReqProduct], [ReceiverName], [Quantity], [DepartmentAndWard], [InvestDtlID], [transform], @TenantId
FROM [SunCity_Clinics].[BloodBank].[DispenseBloodBags];
SET IDENTITY_INSERT [BloodBank].[DispenseBloodBags] OFF;
GO

PRINT 'Migrating [BloodBank].[DonationInfo]...';
SET IDENTITY_INSERT [BloodBank].[DonationInfo] ON;
INSERT INTO [BloodBank].[DonationInfo] ([ID], [DonationTypeCode], [DonationTypeName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [DonationTypeNameEn], [TenantId])
SELECT [ID], [DonationTypeCode], [DonationTypeName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [DonationTypeNameEn], @TenantId
FROM [SunCity_Clinics].[BloodBank].[DonationInfo];
SET IDENTITY_INSERT [BloodBank].[DonationInfo] OFF;
GO

PRINT 'Migrating [BloodBank].[DonationInvestgationMaster]...';
SET IDENTITY_INSERT [BloodBank].[DonationInvestgationMaster] ON;
INSERT INTO [BloodBank].[DonationInvestgationMaster] ([ID], [InvestgationID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DonorID], [CompanyID], [TenantId])
SELECT [ID], [InvestgationID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DonorID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[BloodBank].[DonationInvestgationMaster];
SET IDENTITY_INSERT [BloodBank].[DonationInvestgationMaster] OFF;
GO

PRINT 'Migrating [BloodBank].[DonationRestriction]...';
SET IDENTITY_INSERT [BloodBank].[DonationRestriction] ON;
INSERT INTO [BloodBank].[DonationRestriction] ([ID], [QuestionnaireNumber], [DonationDate], [NumberTo], [DonationReason], [BladderTypes], [ExpirationDate], [DonationPeriod], [BloodProductID], [PateietCount], [RestrictionCode], [DBloodBank], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [QuestionnaireNumber], [DonationDate], [NumberTo], [DonationReason], [BladderTypes], [ExpirationDate], [DonationPeriod], [BloodProductID], [PateietCount], [RestrictionCode], [DBloodBank], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[BloodBank].[DonationRestriction];
SET IDENTITY_INSERT [BloodBank].[DonationRestriction] OFF;
GO

PRINT 'Migrating [BloodBank].[DonorQuestionnaireDetails]...';
SET IDENTITY_INSERT [BloodBank].[DonorQuestionnaireDetails] ON;
INSERT INTO [BloodBank].[DonorQuestionnaireDetails] ([Id], [QuestionnaireNumber], [QuestionnaireDate], [QuestionnaireResult], [QuestionsURL], [Pulse_BPH], [Urine], [Extremity], [Glucose], [Temp], [TempMode], [RespRate_MIN], [Positions], [Bowel], [MEWs], [PainScore], [Systole_MM_Hg], [LevelOfConsciouseness], [OxygenSaturation], [Diastole_MM_Hg], [O2Amount], [FallRisk], [Height], [Wight], [Comment], [BloodPressure_SYSTOLIC], [BloodPressure_DIASTOLIC], [CVP], [DonorRegistrationId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [Id], [QuestionnaireNumber], [QuestionnaireDate], [QuestionnaireResult], [QuestionsURL], [Pulse_BPH], [Urine], [Extremity], [Glucose], [Temp], [TempMode], [RespRate_MIN], [Positions], [Bowel], [MEWs], [PainScore], [Systole_MM_Hg], [LevelOfConsciouseness], [OxygenSaturation], [Diastole_MM_Hg], [O2Amount], [FallRisk], [Height], [Wight], [Comment], [BloodPressure_SYSTOLIC], [BloodPressure_DIASTOLIC], [CVP], [DonorRegistrationId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[BloodBank].[DonorQuestionnaireDetails];
SET IDENTITY_INSERT [BloodBank].[DonorQuestionnaireDetails] OFF;
GO

PRINT 'Migrating [BloodBank].[DonorRegistration]...';
SET IDENTITY_INSERT [BloodBank].[DonorRegistration] ON;
INSERT INTO [BloodBank].[DonorRegistration] ([ID], [FirstNameLatin], [SecoendNameLatin], [ThirdNameLatin], [LastNameLatin], [FirstNameLocal], [SecoendNameLocal], [ThirdNameLocal], [LastNameLocal], [Gender], [MaritalStatus], [Birthdate], [Age], [Address], [NationalID], [Telephone], [BloodGroupID], [ISactive], [ClincalHistory], [RelativeTelephone], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DateFrom], [DateTo], [CompanyID], [Occupation], [LastDonationDate], [TenantId])
SELECT [ID], [FirstNameLatin], [SecoendNameLatin], [ThirdNameLatin], [LastNameLatin], [FirstNameLocal], [SecoendNameLocal], [ThirdNameLocal], [LastNameLocal], [Gender], [MaritalStatus], [Birthdate], [Age], [Address], [NationalID], [Telephone], [BloodGroupID], [ISactive], [ClincalHistory], [RelativeTelephone], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DateFrom], [DateTo], [CompanyID], [Occupation], [LastDonationDate], @TenantId
FROM [SunCity_Clinics].[BloodBank].[DonorRegistration];
SET IDENTITY_INSERT [BloodBank].[DonorRegistration] OFF;
GO

PRINT 'Migrating [BloodBank].[DonorVitalSigns]...';
SET IDENTITY_INSERT [BloodBank].[DonorVitalSigns] ON;
INSERT INTO [BloodBank].[DonorVitalSigns] ([ID], [DonorID], [Temp], [Pulse], [diaslotic], [systolic], [Weight], [Height], [Unfit], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [DonorID], [Temp], [Pulse], [diaslotic], [systolic], [Weight], [Height], [Unfit], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[BloodBank].[DonorVitalSigns];
SET IDENTITY_INSERT [BloodBank].[DonorVitalSigns] OFF;
GO

PRINT 'Migrating [BloodBank].[ExternalFacilityDetails]...';
SET IDENTITY_INSERT [BloodBank].[ExternalFacilityDetails] ON;
INSERT INTO [BloodBank].[ExternalFacilityDetails] ([ID], [ExternalFacilityID], [BloodProductID], [ProductNo], [ExpiryDate], [Quantity], [Price], [BagNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [a1], [TenantId])
SELECT [ID], [ExternalFacilityID], [BloodProductID], [ProductNo], [ExpiryDate], [Quantity], [Price], [BagNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [a1], @TenantId
FROM [SunCity_Clinics].[BloodBank].[ExternalFacilityDetails];
SET IDENTITY_INSERT [BloodBank].[ExternalFacilityDetails] OFF;
GO

PRINT 'Migrating [BloodBank].[ExternalFacilityHeader]...';
SET IDENTITY_INSERT [BloodBank].[ExternalFacilityHeader] ON;
INSERT INTO [BloodBank].[ExternalFacilityHeader] ([ID], [FacilityID], [ActionDate], [ReciptNo], [Actiontype], [PatientID], [ExternalFacilityCode], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [FacilityID], [ActionDate], [ReciptNo], [Actiontype], [PatientID], [ExternalFacilityCode], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[BloodBank].[ExternalFacilityHeader];
SET IDENTITY_INSERT [BloodBank].[ExternalFacilityHeader] OFF;
GO

PRINT 'Migrating [BloodBank].[FacilityMaster]...';
SET IDENTITY_INSERT [BloodBank].[FacilityMaster] ON;
INSERT INTO [BloodBank].[FacilityMaster] ([ID], [FacilityLatinName], [FacilityLocalName], [FacilityCode], [StartDate], [EndDate], [ISActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [FacilityLatinName], [FacilityLocalName], [FacilityCode], [StartDate], [EndDate], [ISActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[BloodBank].[FacilityMaster];
SET IDENTITY_INSERT [BloodBank].[FacilityMaster] OFF;
GO

PRINT 'Migrating [BloodBank].[InventoryAdjustmentEntry]...';
SET IDENTITY_INSERT [BloodBank].[InventoryAdjustmentEntry] ON;
INSERT INTO [BloodBank].[InventoryAdjustmentEntry] ([ID], [PhysiacalAdjID], [BloodGroupId], [QTYinHand], [QTYAdj], [QtYDifference], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [PhysiacalAdjID], [BloodGroupId], [QTYinHand], [QTYAdj], [QtYDifference], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[BloodBank].[InventoryAdjustmentEntry];
SET IDENTITY_INSERT [BloodBank].[InventoryAdjustmentEntry] OFF;
GO

PRINT 'Migrating [BloodBank].[InventoryAdjustment]...';
SET IDENTITY_INSERT [BloodBank].[InventoryAdjustment] ON;
INSERT INTO [BloodBank].[InventoryAdjustment] ([ID], [StockID], [ReasonAdjusyId], [Date], [Status], [Remarks], [PhysiacalNO], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [StockID], [ReasonAdjusyId], [Date], [Status], [Remarks], [PhysiacalNO], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[BloodBank].[InventoryAdjustment];
SET IDENTITY_INSERT [BloodBank].[InventoryAdjustment] OFF;
GO

PRINT 'Migrating [BloodBank].[IssuetoInventoryEntry]...';
SET IDENTITY_INSERT [BloodBank].[IssuetoInventoryEntry] ON;
INSERT INTO [BloodBank].[IssuetoInventoryEntry] ([ID], [IssuetoInventorytId], [DBloodBagsId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [IssuetoInventorytId], [DBloodBagsId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[BloodBank].[IssuetoInventoryEntry];
SET IDENTITY_INSERT [BloodBank].[IssuetoInventoryEntry] OFF;
GO

PRINT 'Migrating [BloodBank].[IssuetoInventory]...';
SET IDENTITY_INSERT [BloodBank].[IssuetoInventory] ON;
INSERT INTO [BloodBank].[IssuetoInventory] ([ID], [MainStockID], [IssueNumber], [IssueDate], [StockID], [Remarks], [Status], [IssueRequestID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SetEntryCode], [TenantId])
SELECT [ID], [MainStockID], [IssueNumber], [IssueDate], [StockID], [Remarks], [Status], [IssueRequestID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SetEntryCode], @TenantId
FROM [SunCity_Clinics].[BloodBank].[IssuetoInventory];
SET IDENTITY_INSERT [BloodBank].[IssuetoInventory] OFF;
GO

PRINT 'Migrating [BloodBank].[OutRersourceDispenseBags]...';
SET IDENTITY_INSERT [BloodBank].[OutRersourceDispenseBags] ON;
INSERT INTO [BloodBank].[OutRersourceDispenseBags] ([ID], [OutResourceID], [BagID], [CreatedDate], [CreatedBy], [CompanyID], [DispenseDate], [BagCode], [ServiceID], [Amount], [Paid], [EntryCodes], [TenantId])
SELECT [ID], [OutResourceID], [BagID], [CreatedDate], [CreatedBy], [CompanyID], [DispenseDate], [BagCode], [ServiceID], [Amount], [Paid], [EntryCodes], @TenantId
FROM [SunCity_Clinics].[BloodBank].[OutRersourceDispenseBags];
SET IDENTITY_INSERT [BloodBank].[OutRersourceDispenseBags] OFF;
GO

PRINT 'Migrating [BloodBank].[OutResources]...';
SET IDENTITY_INSERT [BloodBank].[OutResources] ON;
INSERT INTO [BloodBank].[OutResources] ([Id], [Code], [OutResourcesEnglishName], [OutResourcesArabicName], [Address], [PhoneNumber], [Administrator], [Active], [Account], [ReturnPeriodNumber], [ReturnPeriodType], [WarningPeriodNumber], [WarningPeriodType], [CompanyID], [IsLab], [Labresponsibleperson], [LabPhone], [LabApi], [TenantId])
SELECT [Id], [Code], [OutResourcesEnglishName], [OutResourcesArabicName], [Address], [PhoneNumber], [Administrator], [Active], [Account], [ReturnPeriodNumber], [ReturnPeriodType], [WarningPeriodNumber], [WarningPeriodType], [CompanyID], [IsLab], [Labresponsibleperson], [LabPhone], [LabApi], @TenantId
FROM [SunCity_Clinics].[BloodBank].[OutResources];
SET IDENTITY_INSERT [BloodBank].[OutResources] OFF;
GO

PRINT 'Migrating [BloodBank].[ReturnDispenseBloodBags]...';
SET IDENTITY_INSERT [BloodBank].[ReturnDispenseBloodBags] ON;
INSERT INTO [BloodBank].[ReturnDispenseBloodBags] ([ID], [PatientID], [BagID], [BagCode], [OP_IPNumber], [DoctorID], [ServiceID], [ReturnAmount], [CreatedDate], [CreatedBy], [ReturnDate], [Status], [CompanyID], [EntryCodes], [TenantId])
SELECT [ID], [PatientID], [BagID], [BagCode], [OP_IPNumber], [DoctorID], [ServiceID], [ReturnAmount], [CreatedDate], [CreatedBy], [ReturnDate], [Status], [CompanyID], [EntryCodes], @TenantId
FROM [SunCity_Clinics].[BloodBank].[ReturnDispenseBloodBags];
SET IDENTITY_INSERT [BloodBank].[ReturnDispenseBloodBags] OFF;
GO

PRINT 'Migrating [BloodBank].[TransferRequestDispense]...';
SET IDENTITY_INSERT [BloodBank].[TransferRequestDispense] ON;
INSERT INTO [BloodBank].[TransferRequestDispense] ([ID], [PatientID], [BloodProductID], [BloodTypeID], [Quantity], [LocationID], [OrderedBy], [RequestDate], [TransferRequestID], [OrderStatus], [BagNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [BloodProductID], [BloodTypeID], [Quantity], [LocationID], [OrderedBy], [RequestDate], [TransferRequestID], [OrderStatus], [BagNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[BloodBank].[TransferRequestDispense];
SET IDENTITY_INSERT [BloodBank].[TransferRequestDispense] OFF;
GO

PRINT 'Migrating [BloodBank].[TransfuionProcessing]...';
SET IDENTITY_INSERT [BloodBank].[TransfuionProcessing] ON;
INSERT INTO [BloodBank].[TransfuionProcessing] ([ID], [PatientID], [Reaction], [TransferRequestID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [OrderStatues], [InventoryID], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [Reaction], [TransferRequestID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [OrderStatues], [InventoryID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[BloodBank].[TransfuionProcessing];
SET IDENTITY_INSERT [BloodBank].[TransfuionProcessing] OFF;
GO

PRINT 'Migrating [BloodBank].[clabsibundleDetails]...';
SET IDENTITY_INSERT [BloodBank].[clabsibundleDetails] ON;
INSERT INTO [BloodBank].[clabsibundleDetails] ([ID], [clabsibundletId], [Keepcatheterproperlysecuredtopreventmovementurethraltraction], [bagbelow], [bagonce], [urineflow], [Maintainclosed], [Performhand], [Obtainurinesampleasepticall], [techniquedisconnection], [Perinealhygiene], [Reassess], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Date], [Time], [TenantId])
SELECT [ID], [clabsibundletId], [Keepcatheterproperlysecuredtopreventmovementurethraltraction], [bagbelow], [bagonce], [urineflow], [Maintainclosed], [Performhand], [Obtainurinesampleasepticall], [techniquedisconnection], [Perinealhygiene], [Reassess], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Date], [Time], @TenantId
FROM [SunCity_Clinics].[BloodBank].[clabsibundleDetails];
SET IDENTITY_INSERT [BloodBank].[clabsibundleDetails] OFF;
GO

PRINT 'Migrating [CSSD].[CSSDRequestDetails]...';
SET IDENTITY_INSERT [CSSD].[CSSDRequestDetails] ON;
INSERT INTO [CSSD].[CSSDRequestDetails] ([Id], [CSSDRequestMasterId], [TrayDetailsId], [CSSDRequestQuantity], [SterilizationMethodId], [CSSDDispensedQuantity], [DiscardQuantity], [DiscardReason], [Status], [TenantId])
SELECT [Id], [CSSDRequestMasterId], [TrayDetailsId], [CSSDRequestQuantity], [SterilizationMethodId], [CSSDDispensedQuantity], [DiscardQuantity], [DiscardReason], [Status], @TenantId
FROM [SunCity_Clinics].[CSSD].[CSSDRequestDetails];
SET IDENTITY_INSERT [CSSD].[CSSDRequestDetails] OFF;
GO

PRINT 'Migrating [CSSD].[CSSDRequest]...';
SET IDENTITY_INSERT [CSSD].[CSSDRequest] ON;
INSERT INTO [CSSD].[CSSDRequest] ([Id], [CSSDRequestCode], [SterilizationRequestedPlace], [SubDepartmentId], [StoreId], [Priority], [ReferencesRequest], [RequestDate], [MaximumDueDate], [Notes], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Status], [CompanyID], [TenantId])
SELECT [Id], [CSSDRequestCode], [SterilizationRequestedPlace], [SubDepartmentId], [StoreId], [Priority], [ReferencesRequest], [RequestDate], [MaximumDueDate], [Notes], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[CSSD].[CSSDRequest];
SET IDENTITY_INSERT [CSSD].[CSSDRequest] OFF;
GO

PRINT 'Migrating [CSSD].[LaundryActionType]...';
SET IDENTITY_INSERT [CSSD].[LaundryActionType] ON;
INSERT INTO [CSSD].[LaundryActionType] ([Id], [EnglishName], [ArabicName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [EnglishName], [ArabicName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[CSSD].[LaundryActionType];
SET IDENTITY_INSERT [CSSD].[LaundryActionType] OFF;
GO

PRINT 'Migrating [CSSD].[LaundryRequest]...';
SET IDENTITY_INSERT [CSSD].[LaundryRequest] ON;
INSERT INTO [CSSD].[LaundryRequest] ([Id], [RequestCode], [RequestFor], [PatientId], [LaundryActionTypeId], [Clothes], [EmployeeID], [FloorId], [RoomId], [BedId], [LaundryItems], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Status], [CompanyID], [TenantId])
SELECT [Id], [RequestCode], [RequestFor], [PatientId], [LaundryActionTypeId], [Clothes], [EmployeeID], [FloorId], [RoomId], [BedId], [LaundryItems], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[CSSD].[LaundryRequest];
SET IDENTITY_INSERT [CSSD].[LaundryRequest] OFF;
GO

PRINT 'Migrating [CSSD].[Machine]...';
SET IDENTITY_INSERT [CSSD].[Machine] ON;
INSERT INTO [CSSD].[Machine] ([ID], [Code], [ArabicName], [EnglishName], [AssetID], [CreationDate], [CreatedBy], [ModifcationDate], [ModifiedBy], [CompanyID], [TenantId])
SELECT [ID], [Code], [ArabicName], [EnglishName], [AssetID], [CreationDate], [CreatedBy], [ModifcationDate], [ModifiedBy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[CSSD].[Machine];
SET IDENTITY_INSERT [CSSD].[Machine] OFF;
GO

PRINT 'Migrating [CSSD].[SterilizationMachines]...';
SET IDENTITY_INSERT [CSSD].[SterilizationMachines] ON;
INSERT INTO [CSSD].[SterilizationMachines] ([ID], [MachineID], [SetrilizationMethodID], [CreationDate], [CreatedBy], [CompanyID], [TenantId])
SELECT [ID], [MachineID], [SetrilizationMethodID], [CreationDate], [CreatedBy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[CSSD].[SterilizationMachines];
SET IDENTITY_INSERT [CSSD].[SterilizationMachines] OFF;
GO

PRINT 'Migrating [CSSD].[SterilizationMethod]...';
SET IDENTITY_INSERT [CSSD].[SterilizationMethod] ON;
INSERT INTO [CSSD].[SterilizationMethod] ([ID], [ArabicName], [EnglishName], [Minutes], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ArabicName], [EnglishName], [Minutes], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[CSSD].[SterilizationMethod];
SET IDENTITY_INSERT [CSSD].[SterilizationMethod] OFF;
GO

PRINT 'Migrating [CSSD].[TrayDetails]...';
SET IDENTITY_INSERT [CSSD].[TrayDetails] ON;
INSERT INTO [CSSD].[TrayDetails] ([ID], [ItemsID], [TrayMasterID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [ID], [ItemsID], [TrayMasterID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[CSSD].[TrayDetails];
SET IDENTITY_INSERT [CSSD].[TrayDetails] OFF;
GO

PRINT 'Migrating [CSSD].[TrayTypes]...';
SET IDENTITY_INSERT [CSSD].[TrayTypes] ON;
INSERT INTO [CSSD].[TrayTypes] ([ID], [ArabicName], [EnglishName], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ArabicName], [EnglishName], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[CSSD].[TrayTypes];
SET IDENTITY_INSERT [CSSD].[TrayTypes] OFF;
GO

PRINT 'Migrating [CSSD].[TraysMaster]...';
SET IDENTITY_INSERT [CSSD].[TraysMaster] ON;
INSERT INTO [CSSD].[TraysMaster] ([ID], [TrayTypeID], [TrayName], [SpecialtyID], [ExpireAfterdays], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [TrayTypeID], [TrayName], [SpecialtyID], [ExpireAfterdays], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[CSSD].[TraysMaster];
SET IDENTITY_INSERT [CSSD].[TraysMaster] OFF;
GO

PRINT 'Migrating [Chemotherapy].[AdministrationRoutes]...';
SET IDENTITY_INSERT [Chemotherapy].[AdministrationRoutes] ON;
INSERT INTO [Chemotherapy].[AdministrationRoutes] ([ID], [Code], [NameAr], [NameEn], [CreatedBy], [CreatedDate], [ModifiedBy], [ModidfiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [NameAr], [NameEn], [CreatedBy], [CreatedDate], [ModifiedBy], [ModidfiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Chemotherapy].[AdministrationRoutes];
SET IDENTITY_INSERT [Chemotherapy].[AdministrationRoutes] OFF;
GO

PRINT 'Migrating [Chemotherapy].[ChemotherapyGroups]...';
SET IDENTITY_INSERT [Chemotherapy].[ChemotherapyGroups] ON;
INSERT INTO [Chemotherapy].[ChemotherapyGroups] ([id], [GroupNameEn], [GroupNameAr], [GroupCode], [CreatedBy], [CreatedDate], [LastModifiedDate], [LastModifiedBy], [CompanyID], [TenantId])
SELECT [id], [GroupNameEn], [GroupNameAr], [GroupCode], [CreatedBy], [CreatedDate], [LastModifiedDate], [LastModifiedBy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Chemotherapy].[ChemotherapyGroups];
SET IDENTITY_INSERT [Chemotherapy].[ChemotherapyGroups] OFF;
GO

PRINT 'Migrating [Chemotherapy].[ChemotherapyOrderDetailsStatus]...';
SET IDENTITY_INSERT [Chemotherapy].[ChemotherapyOrderDetailsStatus] ON;
INSERT INTO [Chemotherapy].[ChemotherapyOrderDetailsStatus] ([Id], [ChemotherapyOrderDetailsId], [StatusId], [Note], [EndNurseId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [Id], [ChemotherapyOrderDetailsId], [StatusId], [Note], [EndNurseId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[Chemotherapy].[ChemotherapyOrderDetailsStatus];
SET IDENTITY_INSERT [Chemotherapy].[ChemotherapyOrderDetailsStatus] OFF;
GO

PRINT 'Migrating [Chemotherapy].[ChemotherapyOrderDetails]...';
SET IDENTITY_INSERT [Chemotherapy].[ChemotherapyOrderDetails] ON;
INSERT INTO [Chemotherapy].[ChemotherapyOrderDetails] ([Id], [ChemotherapyOrderHeaderId], [GenericId], [TradeId], [DefaultDose], [Frequency], [Duration], [DurationType], [DiluteWith], [DilutionVolume], [ExpiryHour], [TotalDose], [Route], [Instructor], [StrengthId], [DrugFormsId], [DiluteUnitConversionFactorId], [TradeUnitConversionFactorId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [ChemotherapySetupDetailsID], [DiluteFormsId], [DiluteStrengthId], [DiluteWithTradeId], [InsuranceId], [ISCash], [TenantId])
SELECT [Id], [ChemotherapyOrderHeaderId], [GenericId], [TradeId], [DefaultDose], [Frequency], [Duration], [DurationType], [DiluteWith], [DilutionVolume], [ExpiryHour], [TotalDose], [Route], [Instructor], [StrengthId], [DrugFormsId], [DiluteUnitConversionFactorId], [TradeUnitConversionFactorId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [ChemotherapySetupDetailsID], [DiluteFormsId], [DiluteStrengthId], [DiluteWithTradeId], [InsuranceId], [ISCash], @TenantId
FROM [SunCity_Clinics].[Chemotherapy].[ChemotherapyOrderDetails];
SET IDENTITY_INSERT [Chemotherapy].[ChemotherapyOrderDetails] OFF;
GO

PRINT 'Migrating [Chemotherapy].[ChemotherapyOrderHeader]...';
SET IDENTITY_INSERT [Chemotherapy].[ChemotherapyOrderHeader] ON;
INSERT INTO [Chemotherapy].[ChemotherapyOrderHeader] ([ID], [OrderCode], [PatientID], [ChemotherapySetupHeaderId], [LabValueChecked], [DoctorID], [StartDate], [TreatmentTypeID], [StartTime], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [AdmitPatientId], [CompanyID], [TenantId])
SELECT [ID], [OrderCode], [PatientID], [ChemotherapySetupHeaderId], [LabValueChecked], [DoctorID], [StartDate], [TreatmentTypeID], [StartTime], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [AdmitPatientId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Chemotherapy].[ChemotherapyOrderHeader];
SET IDENTITY_INSERT [Chemotherapy].[ChemotherapyOrderHeader] OFF;
GO

PRINT 'Migrating [Chemotherapy].[ChemotherapyProtocol]...';
SET IDENTITY_INSERT [Chemotherapy].[ChemotherapyProtocol] ON;
INSERT INTO [Chemotherapy].[ChemotherapyProtocol] ([ID], [ArabicName], [EnglishName], [CompanyID], [TenantId])
SELECT [ID], [ArabicName], [EnglishName], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Chemotherapy].[ChemotherapyProtocol];
SET IDENTITY_INSERT [Chemotherapy].[ChemotherapyProtocol] OFF;
GO

PRINT 'Migrating [Chemotherapy].[ChemotherapySetupDetails]...';
SET IDENTITY_INSERT [Chemotherapy].[ChemotherapySetupDetails] ON;
INSERT INTO [Chemotherapy].[ChemotherapySetupDetails] ([Id], [ChemotherapySetupHeaderId], [GenericId], [TradeId], [DefaultDose], [Frequency], [Duration], [DurationType], [DiluteWith], [DilutionVolume], [ExpiryHour], [TotalDose], [Route], [Instructor], [StrengthId], [DrugFormsId], [DiluteUnitId], [TradeUnitId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DiluteFormsId], [DiluteStrengthId], [TenantId])
SELECT [Id], [ChemotherapySetupHeaderId], [GenericId], [TradeId], [DefaultDose], [Frequency], [Duration], [DurationType], [DiluteWith], [DilutionVolume], [ExpiryHour], [TotalDose], [Route], [Instructor], [StrengthId], [DrugFormsId], [DiluteUnitId], [TradeUnitId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DiluteFormsId], [DiluteStrengthId], @TenantId
FROM [SunCity_Clinics].[Chemotherapy].[ChemotherapySetupDetails];
SET IDENTITY_INSERT [Chemotherapy].[ChemotherapySetupDetails] OFF;
GO

PRINT 'Migrating [Chemotherapy].[ChemotherapySetupHeader]...';
SET IDENTITY_INSERT [Chemotherapy].[ChemotherapySetupHeader] ON;
INSERT INTO [Chemotherapy].[ChemotherapySetupHeader] ([Id], [StageId], [ProtocolId], [GroupId], [NumberofCycles], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [StageId], [ProtocolId], [GroupId], [NumberofCycles], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Chemotherapy].[ChemotherapySetupHeader];
SET IDENTITY_INSERT [Chemotherapy].[ChemotherapySetupHeader] OFF;
GO

PRINT 'Migrating [Chemotherapy].[RoutesofAdministration]...';
SET IDENTITY_INSERT [Chemotherapy].[RoutesofAdministration] ON;
INSERT INTO [Chemotherapy].[RoutesofAdministration] ([ID], [Code], [NameAr], [NameEn], [CreatedBy], [CreatedDate], [ModifiedBy], [ModidfiedDate], [TenantId])
SELECT [ID], [Code], [NameAr], [NameEn], [CreatedBy], [CreatedDate], [ModifiedBy], [ModidfiedDate], @TenantId
FROM [SunCity_Clinics].[Chemotherapy].[RoutesofAdministration];
SET IDENTITY_INSERT [Chemotherapy].[RoutesofAdministration] OFF;
GO

PRINT 'Migrating [ClinicalPharmacy].[AdversDrugReaction]...';
SET IDENTITY_INSERT [ClinicalPharmacy].[AdversDrugReaction] ON;
INSERT INTO [ClinicalPharmacy].[AdversDrugReaction] ([ID], [Code], [PatientID], [Diagnosis], [ReactionDate], [ISrash], [ISAnaPhlaxis], [ISTinnitus], [ISAngina], [ISDiarhea], [ISDrowsinss], [ISExtrapyramidal], [Other], [OtherNote], [ISDoseChange], [ISantidotegiven], [ISDrugDiscontinued], [OtherAction], [NoteOtherAction], [ISRecoverd], [RecoveryDate], [RecoveryNote], [ClinicalPharmsictID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [PatientID], [Diagnosis], [ReactionDate], [ISrash], [ISAnaPhlaxis], [ISTinnitus], [ISAngina], [ISDiarhea], [ISDrowsinss], [ISExtrapyramidal], [Other], [OtherNote], [ISDoseChange], [ISantidotegiven], [ISDrugDiscontinued], [OtherAction], [NoteOtherAction], [ISRecoverd], [RecoveryDate], [RecoveryNote], [ClinicalPharmsictID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ClinicalPharmacy].[AdversDrugReaction];
SET IDENTITY_INSERT [ClinicalPharmacy].[AdversDrugReaction] OFF;
GO

PRINT 'Migrating [ClinicalPharmacy].[Categorized_MedicalErrors]...';
SET IDENTITY_INSERT [ClinicalPharmacy].[Categorized_MedicalErrors] ON;
INSERT INTO [ClinicalPharmacy].[Categorized_MedicalErrors] ([Id], [PrescriptionDetailID], [O1], [O2], [O3], [O4], [O5], [O6], [O7], [O8], [O9], [O10], [O11], [O12], [O13], [O14], [O15], [O16], [O17], [O18], [O19], [O20], [O21], [O22], [T1], [T2], [T3], [T4], [T5], [T6], [T7], [T8], [T9], [D1], [D2], [D3], [D4], [D5], [D6], [D7], [D8], [P1], [P2], [P3], [P4], [P5], [P6], [P7], [P8], [P9], [P10], [P11], [A1], [A2], [A3], [A4], [A5], [A6], [A7], [A8], [A9], [A10], [M1], [M2], [M3], [M4], [M5], [Severity], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ApprovedByDoctor], [DetType], [TenantId])
SELECT [Id], [PrescriptionDetailID], [O1], [O2], [O3], [O4], [O5], [O6], [O7], [O8], [O9], [O10], [O11], [O12], [O13], [O14], [O15], [O16], [O17], [O18], [O19], [O20], [O21], [O22], [T1], [T2], [T3], [T4], [T5], [T6], [T7], [T8], [T9], [D1], [D2], [D3], [D4], [D5], [D6], [D7], [D8], [P1], [P2], [P3], [P4], [P5], [P6], [P7], [P8], [P9], [P10], [P11], [A1], [A2], [A3], [A4], [A5], [A6], [A7], [A8], [A9], [A10], [M1], [M2], [M3], [M4], [M5], [Severity], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ApprovedByDoctor], [DetType], @TenantId
FROM [SunCity_Clinics].[ClinicalPharmacy].[Categorized_MedicalErrors];
SET IDENTITY_INSERT [ClinicalPharmacy].[Categorized_MedicalErrors] OFF;
GO

PRINT 'Migrating [ClinicalPharmacy].[ClinicalIntervention]...';
SET IDENTITY_INSERT [ClinicalPharmacy].[ClinicalIntervention] ON;
INSERT INTO [ClinicalPharmacy].[ClinicalIntervention] ([ID], [Code], [PatientID], [ISantimicromedical], [DrugRelatedProblemEnum], [DrugRelatedProblemNote], [DrugDrugInteractionEnum], [ClinicalSignificanceEnum], [ISCostSaving], [ISADR], [ISEnhanced], [ExpectedOutComeNote], [ClinicalPharmcistNote], [FollowUpNote], [PhyisionResponseEnum], [IFReectedNote], [DrugID], [CostCenterID], [PhyisionSpicialistID], [ClinicalPharmcistID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [PatientID], [ISantimicromedical], [DrugRelatedProblemEnum], [DrugRelatedProblemNote], [DrugDrugInteractionEnum], [ClinicalSignificanceEnum], [ISCostSaving], [ISADR], [ISEnhanced], [ExpectedOutComeNote], [ClinicalPharmcistNote], [FollowUpNote], [PhyisionResponseEnum], [IFReectedNote], [DrugID], [CostCenterID], [PhyisionSpicialistID], [ClinicalPharmcistID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ClinicalPharmacy].[ClinicalIntervention];
SET IDENTITY_INSERT [ClinicalPharmacy].[ClinicalIntervention] OFF;
GO

PRINT 'Migrating [ClinicalPharmacy].[ClinicalPharmacySetting]...';
SET IDENTITY_INSERT [ClinicalPharmacy].[ClinicalPharmacySetting] ON;
INSERT INTO [ClinicalPharmacy].[ClinicalPharmacySetting] ([ID], [SpecialityGroupDetailsID], [Day], [FromTime], [ToTime], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [SpecialityGroupDetailsID], [Day], [FromTime], [ToTime], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ClinicalPharmacy].[ClinicalPharmacySetting];
SET IDENTITY_INSERT [ClinicalPharmacy].[ClinicalPharmacySetting] OFF;
GO

PRINT 'Migrating [ClinicalPharmacy].[MedicationError]...';
SET IDENTITY_INSERT [ClinicalPharmacy].[MedicationError] ON;
INSERT INTO [ClinicalPharmacy].[MedicationError] ([ID], [Code], [PatientID], [MedicationErrorsDate], [CostCenterID], [DateOFError], [SessionID], [ErrorLevelEnum], [ErrorCategoryEnum], [phyisionSpicialistID], [phyisionSpicialityID], [ISWrongStorge], [ISWrongFerquancy], [ISWrongAdmin], [ISWrongPatient], [ISWrongMedication], [ISDelayNewOrder], [ISDelayedAdmin], [ISMissed], [ISNoSignature], [ISOthers], [OtherNote], [ISIncomplitePerscription], [ISIncomplitepatientData], [ISWrongClose], [ISIlligableHandWritting], [ISDelayDispinsing], [ISWrongComment], [ISWronglabeling], [ISHoursNotCoverd], [ISWrongDrug], [ISWrongIV], [ISOldMedications], [ISSystemsheetMissMatch], [ISLostMedication], [ISDelayedNewReporting], [ISnewother], [NewOtherNote], [ISNegligence], [ISMainPowershortage], [IsNoPolicy], [ISMedicationShortage], [IsCommincationProblem], [ISOtherCousesOfError], [OtherCousesOfErrorNote], [ClincalPharmcySignature], [ClincalPharmcistID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Status], [CloseDate], [CloseTime], [CloseNote], [ClosedBy], [CompanyID], [TenantId])
SELECT [ID], [Code], [PatientID], [MedicationErrorsDate], [CostCenterID], [DateOFError], [SessionID], [ErrorLevelEnum], [ErrorCategoryEnum], [phyisionSpicialistID], [phyisionSpicialityID], [ISWrongStorge], [ISWrongFerquancy], [ISWrongAdmin], [ISWrongPatient], [ISWrongMedication], [ISDelayNewOrder], [ISDelayedAdmin], [ISMissed], [ISNoSignature], [ISOthers], [OtherNote], [ISIncomplitePerscription], [ISIncomplitepatientData], [ISWrongClose], [ISIlligableHandWritting], [ISDelayDispinsing], [ISWrongComment], [ISWronglabeling], [ISHoursNotCoverd], [ISWrongDrug], [ISWrongIV], [ISOldMedications], [ISSystemsheetMissMatch], [ISLostMedication], [ISDelayedNewReporting], [ISnewother], [NewOtherNote], [ISNegligence], [ISMainPowershortage], [IsNoPolicy], [ISMedicationShortage], [IsCommincationProblem], [ISOtherCousesOfError], [OtherCousesOfErrorNote], [ClincalPharmcySignature], [ClincalPharmcistID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Status], [CloseDate], [CloseTime], [CloseNote], [ClosedBy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[ClinicalPharmacy].[MedicationError];
SET IDENTITY_INSERT [ClinicalPharmacy].[MedicationError] OFF;
GO

PRINT 'Migrating [Emergency].[ABGs]...';
SET IDENTITY_INSERT [Emergency].[ABGs] ON;
INSERT INTO [Emergency].[ABGs] ([ID], [PatientID], [DoctorID], [PH], [PCO2], [PO2], [SA02], [HCT], [Hb], [BEecf], [Beb], [SBC], [Hco3], [Tco2], [A], [AaDo2], [aA], [RT], [O2cap], [O2CT], [FO2Hb], [KPlus], [NAPlus], [CI], [GLU], [LAC], [BASE], [Comments], [CreatedBy], [CreationDate], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [DoctorID], [PH], [PCO2], [PO2], [SA02], [HCT], [Hb], [BEecf], [Beb], [SBC], [Hco3], [Tco2], [A], [AaDo2], [aA], [RT], [O2cap], [O2CT], [FO2Hb], [KPlus], [NAPlus], [CI], [GLU], [LAC], [BASE], [Comments], [CreatedBy], [CreationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Emergency].[ABGs];
SET IDENTITY_INSERT [Emergency].[ABGs] OFF;
GO

PRINT 'Migrating [Emergency].[ArrivalType]...';
SET IDENTITY_INSERT [Emergency].[ArrivalType] ON;
INSERT INTO [Emergency].[ArrivalType] ([Id], [ArrivalTypeDescArabic], [ArrivalTypeDescEnglish], [Status], [CompanyID], [TenantId])
SELECT [Id], [ArrivalTypeDescArabic], [ArrivalTypeDescEnglish], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Emergency].[ArrivalType];
SET IDENTITY_INSERT [Emergency].[ArrivalType] OFF;
GO

PRINT 'Migrating [Emergency].[CasePriority]...';
SET IDENTITY_INSERT [Emergency].[CasePriority] ON;
INSERT INTO [Emergency].[CasePriority] ([Id], [Code], [Name], [CompanyID], [TenantId])
SELECT [Id], [Code], [Name], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Emergency].[CasePriority];
SET IDENTITY_INSERT [Emergency].[CasePriority] OFF;
GO

PRINT 'Migrating [Emergency].[ComplainSetup]...';
SET IDENTITY_INSERT [Emergency].[ComplainSetup] ON;
INSERT INTO [Emergency].[ComplainSetup] ([Id], [ComplainDescArabic], [ComplainDescEnglish], [Status], [CompanyID], [TenantId])
SELECT [Id], [ComplainDescArabic], [ComplainDescEnglish], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Emergency].[ComplainSetup];
SET IDENTITY_INSERT [Emergency].[ComplainSetup] OFF;
GO

PRINT 'Migrating [Emergency].[DischargeType]...';
SET IDENTITY_INSERT [Emergency].[DischargeType] ON;
INSERT INTO [Emergency].[DischargeType] ([Id], [DischargeTypeDescArabic], [DischargeTypeDescEnglish], [Status], [CompanyID], [TenantId])
SELECT [Id], [DischargeTypeDescArabic], [DischargeTypeDescEnglish], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Emergency].[DischargeType];
SET IDENTITY_INSERT [Emergency].[DischargeType] OFF;
GO

PRINT 'Migrating [Emergency].[DoctorSchedule]...';
SET IDENTITY_INSERT [Emergency].[DoctorSchedule] ON;
INSERT INTO [Emergency].[DoctorSchedule] ([ID], [NameArabic], [NameEnglish], [TimeFrom], [TimeTo], [Status], [CompanyID], [TenantId])
SELECT [ID], [NameArabic], [NameEnglish], [TimeFrom], [TimeTo], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Emergency].[DoctorSchedule];
SET IDENTITY_INSERT [Emergency].[DoctorSchedule] OFF;
GO

PRINT 'Migrating [Emergency].[EmergencyDetails]...';
SET IDENTITY_INSERT [Emergency].[EmergencyDetails] ON;
INSERT INTO [Emergency].[EmergencyDetails] ([ID], [PatientID], [Code], [VisiteDate], [DischargeDate], [DischargeStatuse], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BranchId], [EmergencyUnitId], [wardId], [BedId], [AccommodationType], [TenantId])
SELECT [ID], [PatientID], [Code], [VisiteDate], [DischargeDate], [DischargeStatuse], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BranchId], [EmergencyUnitId], [wardId], [BedId], [AccommodationType], @TenantId
FROM [SunCity_Clinics].[Emergency].[EmergencyDetails];
SET IDENTITY_INSERT [Emergency].[EmergencyDetails] OFF;
GO

PRINT 'Migrating [Emergency].[EmergencyOrganization]...';
SET IDENTITY_INSERT [Emergency].[EmergencyOrganization] ON;
INSERT INTO [Emergency].[EmergencyOrganization] ([ID], [DescriptionArabic], [DescriptionEnglish], [MainOrganization], [Status], [CompanyID], [TenantId])
SELECT [ID], [DescriptionArabic], [DescriptionEnglish], [MainOrganization], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Emergency].[EmergencyOrganization];
SET IDENTITY_INSERT [Emergency].[EmergencyOrganization] OFF;
GO

PRINT 'Migrating [Emergency].[EmergencySchedule]...';
SET IDENTITY_INSERT [Emergency].[EmergencySchedule] ON;
INSERT INTO [Emergency].[EmergencySchedule] ([ID], [DayID], [StartTime], [EndTime], [SubSpecialityID], [DoctorID], [Status], [CreatedBy], [Creationdate], [ModifiedBy], [ModificationDate], [SessionId], [CompanyID], [TenantId])
SELECT [ID], [DayID], [StartTime], [EndTime], [SubSpecialityID], [DoctorID], [Status], [CreatedBy], [Creationdate], [ModifiedBy], [ModificationDate], [SessionId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Emergency].[EmergencySchedule];
SET IDENTITY_INSERT [Emergency].[EmergencySchedule] OFF;
GO

PRINT 'Migrating [Emergency].[EmergencyUnit]...';
SET IDENTITY_INSERT [Emergency].[EmergencyUnit] ON;
INSERT INTO [Emergency].[EmergencyUnit] ([ID], [ArrivalTypeID], [ComplainID], [RiskID], [PatientStatusID], [DoctorID], [PaymentTypeID], [ReferralDoctorID], [ReferralUnitID], [CasePriortyID], [PatientID], [Date], [ERcode], [SeenDateTime], [ComplainDuration], [SpecialityID], [Categoryitem], [ItemID], [Reason], [CategoryitemOthers], [CompanyID], [BranchID], [EncounterType], [TenantId])
SELECT [ID], [ArrivalTypeID], [ComplainID], [RiskID], [PatientStatusID], [DoctorID], [PaymentTypeID], [ReferralDoctorID], [ReferralUnitID], [CasePriortyID], [PatientID], [Date], [ERcode], [SeenDateTime], [ComplainDuration], [SpecialityID], [Categoryitem], [ItemID], [Reason], [CategoryitemOthers], [CompanyID], [BranchID], [EncounterType], @TenantId
FROM [SunCity_Clinics].[Emergency].[EmergencyUnit];
SET IDENTITY_INSERT [Emergency].[EmergencyUnit] OFF;
GO

PRINT 'Migrating [Emergency].[EmergencyUnit_History]...';
SET IDENTITY_INSERT [Emergency].[EmergencyUnit_History] ON;
INSERT INTO [Emergency].[EmergencyUnit_History] ([ID], [EmergencyUnitID], [ChangeDate], [DoctorID], [Reason], [ERCode], [ItemID], [other], [CompanyID], [BranchID], [TenantId])
SELECT [ID], [EmergencyUnitID], [ChangeDate], [DoctorID], [Reason], [ERCode], [ItemID], [other], [CompanyID], [BranchID], @TenantId
FROM [SunCity_Clinics].[Emergency].[EmergencyUnit_History];
SET IDENTITY_INSERT [Emergency].[EmergencyUnit_History] OFF;
GO

PRINT 'Migrating [Emergency].[EmergencyVisit]...';
SET IDENTITY_INSERT [Emergency].[EmergencyVisit] ON;
INSERT INTO [Emergency].[EmergencyVisit] ([Id], [PatientID], [VistNo], [OPCase], [ModeOfArrival], [AccompaniedBy], [RelativesNotifiedID], [PriorityID], [DateOfArrival], [TimeOfArrival], [DepartmentID], [ReferralDepartmentID], [DoctorID], [ReferralDoctorID], [ReferralClinic], [CheifComplaint], [OPNumber], [WordToAdmintID], [BedNoID], [IPNumber], [MedicoLegalDetails], [InsuranceAuthorizationCode], [Remarks], [EnteredBy], [ApprovedBy], [ApprovedDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [VistNo], [OPCase], [ModeOfArrival], [AccompaniedBy], [RelativesNotifiedID], [PriorityID], [DateOfArrival], [TimeOfArrival], [DepartmentID], [ReferralDepartmentID], [DoctorID], [ReferralDoctorID], [ReferralClinic], [CheifComplaint], [OPNumber], [WordToAdmintID], [BedNoID], [IPNumber], [MedicoLegalDetails], [InsuranceAuthorizationCode], [Remarks], [EnteredBy], [ApprovedBy], [ApprovedDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Emergency].[EmergencyVisit];
SET IDENTITY_INSERT [Emergency].[EmergencyVisit] OFF;
GO

PRINT 'Migrating [Emergency].[FastEmergancy]...';
SET IDENTITY_INSERT [Emergency].[FastEmergancy] ON;
INSERT INTO [Emergency].[FastEmergancy] ([ID], [ComeFrom], [patientID], [DoctorID], [PaymentMethodID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Status], [ERCode], [CompanyID], [TenantId])
SELECT [ID], [ComeFrom], [patientID], [DoctorID], [PaymentMethodID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Status], [ERCode], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Emergency].[FastEmergancy];
SET IDENTITY_INSERT [Emergency].[FastEmergancy] OFF;
GO

PRINT 'Migrating [Emergency].[NormalEmergancy]...';
SET IDENTITY_INSERT [Emergency].[NormalEmergancy] ON;
INSERT INTO [Emergency].[NormalEmergancy] ([ID], [patientID], [CameFrom], [PaymentMethodID], [DoctorID], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [ERCode], [CompanyID], [TenantId])
SELECT [ID], [patientID], [CameFrom], [PaymentMethodID], [DoctorID], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [ERCode], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Emergency].[NormalEmergancy];
SET IDENTITY_INSERT [Emergency].[NormalEmergancy] OFF;
GO

PRINT 'Migrating [Emergency].[NursingAssessment]...';
SET IDENTITY_INSERT [Emergency].[NursingAssessment] ON;
INSERT INTO [Emergency].[NursingAssessment] ([Id], [PatientID], [AssessmentID], [Date], [Time], [DoneBy], [OPNumber], [PainIntensily], [TypeOfPainID], [Location], [FrequencyID], [Duration], [MentalStatusID], [SpeechID], [RespirationID], [SkinColorID], [SkinTemperatureID], [SkinMoistureID], [Remarks], [ApprovedBy], [ApprovedDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [AssessmentID], [Date], [Time], [DoneBy], [OPNumber], [PainIntensily], [TypeOfPainID], [Location], [FrequencyID], [Duration], [MentalStatusID], [SpeechID], [RespirationID], [SkinColorID], [SkinTemperatureID], [SkinMoistureID], [Remarks], [ApprovedBy], [ApprovedDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Emergency].[NursingAssessment];
SET IDENTITY_INSERT [Emergency].[NursingAssessment] OFF;
GO

PRINT 'Migrating [Emergency].[OnCallDoctors]...';
SET IDENTITY_INSERT [Emergency].[OnCallDoctors] ON;
INSERT INTO [Emergency].[OnCallDoctors] ([Id], [DoctorId], [Status], [SpicialityID], [CallingDate], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [Id], [DoctorId], [Status], [SpicialityID], [CallingDate], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Emergency].[OnCallDoctors];
SET IDENTITY_INSERT [Emergency].[OnCallDoctors] OFF;
GO

PRINT 'Migrating [Emergency].[PatientStatus]...';
SET IDENTITY_INSERT [Emergency].[PatientStatus] ON;
INSERT INTO [Emergency].[PatientStatus] ([Id], [PatientStatusTypeDescArabic], [PatientStatusTypeDescEnglish], [Status], [CompanyID], [TenantId])
SELECT [Id], [PatientStatusTypeDescArabic], [PatientStatusTypeDescEnglish], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Emergency].[PatientStatus];
SET IDENTITY_INSERT [Emergency].[PatientStatus] OFF;
GO

PRINT 'Migrating [Emergency].[RiskType]...';
SET IDENTITY_INSERT [Emergency].[RiskType] ON;
INSERT INTO [Emergency].[RiskType] ([ID], [DescriptionArabic], [DescriptionEnglish], [Status], [CompanyID], [TenantId])
SELECT [ID], [DescriptionArabic], [DescriptionEnglish], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Emergency].[RiskType];
SET IDENTITY_INSERT [Emergency].[RiskType] OFF;
GO

PRINT 'Migrating [Emergency].[ShiftType]...';
SET IDENTITY_INSERT [Emergency].[ShiftType] ON;
INSERT INTO [Emergency].[ShiftType] ([ID], [NameArabic], [NameEnglish], [Status], [TimeFrom], [TimeTo], [CompanyID], [TenantId])
SELECT [ID], [NameArabic], [NameEnglish], [Status], [TimeFrom], [TimeTo], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Emergency].[ShiftType];
SET IDENTITY_INSERT [Emergency].[ShiftType] OFF;
GO

PRINT 'Migrating [Emergency].[TriagCategoryItems]...';
SET IDENTITY_INSERT [Emergency].[TriagCategoryItems] ON;
INSERT INTO [Emergency].[TriagCategoryItems] ([ID], [DescriptionArabic], [DescriptionEnglish], [TriagCategoryID], [NameKa], [TenantId])
SELECT [ID], [DescriptionArabic], [DescriptionEnglish], [TriagCategoryID], [NameKa], @TenantId
FROM [SunCity_Clinics].[Emergency].[TriagCategoryItems];
SET IDENTITY_INSERT [Emergency].[TriagCategoryItems] OFF;
GO

PRINT 'Migrating [Emergency].[TriagCategory]...';
SET IDENTITY_INSERT [Emergency].[TriagCategory] ON;
INSERT INTO [Emergency].[TriagCategory] ([ID], [DescriptionArabic], [DescriptionEnglish], [Color], [WaitingTime], [NameKa], [TenantId])
SELECT [ID], [DescriptionArabic], [DescriptionEnglish], [Color], [WaitingTime], [NameKa], @TenantId
FROM [SunCity_Clinics].[Emergency].[TriagCategory];
SET IDENTITY_INSERT [Emergency].[TriagCategory] OFF;
GO

PRINT 'Migrating [Emergency].[VitalParameter]...';
SET IDENTITY_INSERT [Emergency].[VitalParameter] ON;
INSERT INTO [Emergency].[VitalParameter] ([Id], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Emergency].[VitalParameter];
SET IDENTITY_INSERT [Emergency].[VitalParameter] OFF;
GO

PRINT 'Migrating [HotelServices].[ActionDefination]...';
SET IDENTITY_INSERT [HotelServices].[ActionDefination] ON;
INSERT INTO [HotelServices].[ActionDefination] ([ID], [ActionName], [ActionDescription], [ActionCode], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ActionName], [ActionDescription], [ActionCode], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[HotelServices].[ActionDefination];
SET IDENTITY_INSERT [HotelServices].[ActionDefination] OFF;
GO

PRINT 'Migrating [HotelServices].[HotelServiceOrder]...';
SET IDENTITY_INSERT [HotelServices].[HotelServiceOrder] ON;
INSERT INTO [HotelServices].[HotelServiceOrder] ([ID], [OrderCode], [RequesterName], [LocationID], [ActionID], [FromTime], [ToTime], [OrderDate], [Status], [CloseDate], [CloseTime], [Note], [RejectDate], [RejectTime], [Reason], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [OrderTypeID], [CompanyID], [TenantId])
SELECT [ID], [OrderCode], [RequesterName], [LocationID], [ActionID], [FromTime], [ToTime], [OrderDate], [Status], [CloseDate], [CloseTime], [Note], [RejectDate], [RejectTime], [Reason], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [OrderTypeID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[HotelServices].[HotelServiceOrder];
SET IDENTITY_INSERT [HotelServices].[HotelServiceOrder] OFF;
GO

PRINT 'Migrating [HotelServices].[ItemAction]...';
SET IDENTITY_INSERT [HotelServices].[ItemAction] ON;
INSERT INTO [HotelServices].[ItemAction] ([ID], [ItemActionCode], [ActionID], [StoreID], [ItemID], [UnitID], [Quantity], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ItemActionCode], [ActionID], [StoreID], [ItemID], [UnitID], [Quantity], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[HotelServices].[ItemAction];
SET IDENTITY_INSERT [HotelServices].[ItemAction] OFF;
GO

PRINT 'Migrating [HotelServices].[ScheduledActions]...';
SET IDENTITY_INSERT [HotelServices].[ScheduledActions] ON;
INSERT INTO [HotelServices].[ScheduledActions] ([ID], [ScheduleActionCode], [LocationID], [ActionID], [TimesNumber], [PeriodNumber], [PeriodType], [Startdate], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ScheduleActionCode], [LocationID], [ActionID], [TimesNumber], [PeriodNumber], [PeriodType], [Startdate], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[HotelServices].[ScheduledActions];
SET IDENTITY_INSERT [HotelServices].[ScheduledActions] OFF;
GO

PRINT 'Migrating [InPatient].[AccommodationType]...';
SET IDENTITY_INSERT [InPatient].[AccommodationType] ON;
INSERT INTO [InPatient].[AccommodationType] ([Id], [TypeCode], [NameArabic], [NameEnglish], [Status], [HospitalCase], [ServiceID], [AccommodationTypeID], [Companion], [BranchId], [IsEmergency], [NameKa], [TenantId])
SELECT [Id], [TypeCode], [NameArabic], [NameEnglish], [Status], [HospitalCase], [ServiceID], [AccommodationTypeID], [Companion], [BranchId], [IsEmergency], [NameKa], @TenantId
FROM [SunCity_Clinics].[InPatient].[AccommodationType];
SET IDENTITY_INSERT [InPatient].[AccommodationType] OFF;
GO

PRINT 'Migrating [InPatient].[AccommodationTypes]...';
SET IDENTITY_INSERT [InPatient].[AccommodationTypes] ON;
INSERT INTO [InPatient].[AccommodationTypes] ([ID], [Code], [NameArabic], [NameEnglish], [ServiceID], [Companion], [AccommodationTypesID], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [ID], [Code], [NameArabic], [NameEnglish], [ServiceID], [Companion], [AccommodationTypesID], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[InPatient].[AccommodationTypes];
SET IDENTITY_INSERT [InPatient].[AccommodationTypes] OFF;
GO

PRINT 'Migrating [InPatient].[AdmissionCategory]...';
SET IDENTITY_INSERT [InPatient].[AdmissionCategory] ON;
INSERT INTO [InPatient].[AdmissionCategory] ([Id], [TypeCode], [NameArabic], [NameEnglish], [Status], [HospitalCase], [TenantId])
SELECT [Id], [TypeCode], [NameArabic], [NameEnglish], [Status], [HospitalCase], @TenantId
FROM [SunCity_Clinics].[InPatient].[AdmissionCategory];
SET IDENTITY_INSERT [InPatient].[AdmissionCategory] OFF;
GO

PRINT 'Migrating [InPatient].[AdmissionPurpose]...';
SET IDENTITY_INSERT [InPatient].[AdmissionPurpose] ON;
INSERT INTO [InPatient].[AdmissionPurpose] ([Id], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[AdmissionPurpose];
SET IDENTITY_INSERT [InPatient].[AdmissionPurpose] OFF;
GO

PRINT 'Migrating [InPatient].[AdmissionRequest]...';
SET IDENTITY_INSERT [InPatient].[AdmissionRequest] ON;
INSERT INTO [InPatient].[AdmissionRequest] ([Id], [PatientID], [OPnumber], [DoctorID], [AdmissiontypeID], [AdmissionWardID], [AdmissionPurposeID], [AdmissionDate], [AdmissionTime], [Days], [SurgeryTypeID], [DeliveryTypeID], [Remarks], [Status], [IPNumber], [RoomTypeID], [BedNO], [InsuranceAuthorizationCode], [Instructions], [MedicalCondition], [MLCTypeID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [AccomodationTypeID], [ExpectedDischargeDate], [EmergencyUnitID], [TenantId])
SELECT [Id], [PatientID], [OPnumber], [DoctorID], [AdmissiontypeID], [AdmissionWardID], [AdmissionPurposeID], [AdmissionDate], [AdmissionTime], [Days], [SurgeryTypeID], [DeliveryTypeID], [Remarks], [Status], [IPNumber], [RoomTypeID], [BedNO], [InsuranceAuthorizationCode], [Instructions], [MedicalCondition], [MLCTypeID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [AccomodationTypeID], [ExpectedDischargeDate], [EmergencyUnitID], @TenantId
FROM [SunCity_Clinics].[InPatient].[AdmissionRequest];
SET IDENTITY_INSERT [InPatient].[AdmissionRequest] OFF;
GO

PRINT 'Migrating [InPatient].[AdmitPatientToNewMedUnit]...';
SET IDENTITY_INSERT [InPatient].[AdmitPatientToNewMedUnit] ON;
INSERT INTO [InPatient].[AdmitPatientToNewMedUnit] ([Id], [PatientID], [PatAccom], [PatWard], [PatRoom], [PatBed], [IsCompanion], [CompAccom], [CompWard], [CompRoom], [CompBed], [CreatedBy], [CreatedDate], [CompanyID], [PatDiet], [CompSex], [CompDiet], [Discharge], [Reason], [DischargeType], [DischargeDoctor], [DischargeDiagnosis], [DeathDate], [DeathTime], [CancelAdmit], [DischargeDate], [TenantId])
SELECT [Id], [PatientID], [PatAccom], [PatWard], [PatRoom], [PatBed], [IsCompanion], [CompAccom], [CompWard], [CompRoom], [CompBed], [CreatedBy], [CreatedDate], [CompanyID], [PatDiet], [CompSex], [CompDiet], [Discharge], [Reason], [DischargeType], [DischargeDoctor], [DischargeDiagnosis], [DeathDate], [DeathTime], [CancelAdmit], [DischargeDate], @TenantId
FROM [SunCity_Clinics].[InPatient].[AdmitPatientToNewMedUnit];
SET IDENTITY_INSERT [InPatient].[AdmitPatientToNewMedUnit] OFF;
GO

PRINT 'Migrating [InPatient].[AdmitPatients]...';
SET IDENTITY_INSERT [InPatient].[AdmitPatients] ON;
INSERT INTO [InPatient].[AdmitPatients] ([Id], [PatientID], [DoctorID], [IPNumber], [IsDayAdmission], [InsuranceAuthorizationCode], [AdmissionRequestID], [AdmissionWardID], [Days], [MLCTypeID], [AdmissionDate], [AdmissionTime], [BedNO], [RoomTypeID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DischargeStatusId], [ExpectedDateOfDischarge], [ExpectedTimeOfDischarge], [CompanyID], [EmergencyUnitID], [AccomodationTypeId], [PatLimitID], [AdmitType], [AdmitNurseID], [NeedEscort], [EscortBedID], [Remarks], [AdmitDoctorID], [AdmitUrgency], [IsIsolation], [AdmitionType], [IsReferral], [RefDoctorName], [RefClinicName], [SponsorID], [BranchId], [ToOPD], [EncounterType], [EncounterAdmit], [Encounterstatus], [serviceType], [careteamrole], [TenantId])
SELECT [Id], [PatientID], [DoctorID], [IPNumber], [IsDayAdmission], [InsuranceAuthorizationCode], [AdmissionRequestID], [AdmissionWardID], [Days], [MLCTypeID], [AdmissionDate], [AdmissionTime], [BedNO], [RoomTypeID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DischargeStatusId], [ExpectedDateOfDischarge], [ExpectedTimeOfDischarge], [CompanyID], [EmergencyUnitID], [AccomodationTypeId], [PatLimitID], [AdmitType], [AdmitNurseID], [NeedEscort], [EscortBedID], [Remarks], [AdmitDoctorID], [AdmitUrgency], [IsIsolation], [AdmitionType], [IsReferral], [RefDoctorName], [RefClinicName], [SponsorID], [BranchId], [ToOPD], [EncounterType], [EncounterAdmit], [Encounterstatus], [serviceType], [careteamrole], @TenantId
FROM [SunCity_Clinics].[InPatient].[AdmitPatients];
SET IDENTITY_INSERT [InPatient].[AdmitPatients] OFF;
GO

PRINT 'Migrating [InPatient].[BedLockPurpose]...';
SET IDENTITY_INSERT [InPatient].[BedLockPurpose] ON;
INSERT INTO [InPatient].[BedLockPurpose] ([Id], [NameEn], [Name], [Default], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [NameEn], [Name], [Default], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[BedLockPurpose];
SET IDENTITY_INSERT [InPatient].[BedLockPurpose] OFF;
GO

PRINT 'Migrating [InPatient].[BedRenewal]...';
SET IDENTITY_INSERT [InPatient].[BedRenewal] ON;
INSERT INTO [InPatient].[BedRenewal] ([Id], [Ward], [Room], [Bed], [StartDate], [EndDate], [Infexted_Renewed], [CompanyID], [TenantId])
SELECT [Id], [Ward], [Room], [Bed], [StartDate], [EndDate], [Infexted_Renewed], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[BedRenewal];
SET IDENTITY_INSERT [InPatient].[BedRenewal] OFF;
GO

PRINT 'Migrating [InPatient].[BedStatusChangeTracker]...';
SET IDENTITY_INSERT [InPatient].[BedStatusChangeTracker] ON;
INSERT INTO [InPatient].[BedStatusChangeTracker] ([Id], [BedLockPurposeId], [BedId], [BedStatusChangeProcessEnumValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [BedLockPurposeId], [BedId], [BedStatusChangeProcessEnumValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[BedStatusChangeTracker];
SET IDENTITY_INSERT [InPatient].[BedStatusChangeTracker] OFF;
GO

PRINT 'Migrating [InPatient].[BedStatus]...';
SET IDENTITY_INSERT [InPatient].[BedStatus] ON;
INSERT INTO [InPatient].[BedStatus] ([Id], [Name], [StatusImage], [CompanyID], [NameAr], [BranchId], [TenantId])
SELECT [Id], [Name], [StatusImage], [CompanyID], [NameAr], [BranchId], @TenantId
FROM [SunCity_Clinics].[InPatient].[BedStatus];
SET IDENTITY_INSERT [InPatient].[BedStatus] OFF;
GO

PRINT 'Migrating [InPatient].[BedSwap]...';
SET IDENTITY_INSERT [InPatient].[BedSwap] ON;
INSERT INTO [InPatient].[BedSwap] ([ID], [BedFrom], [BedTo], [PatientFrom], [PatientTo], [DateSwap], [BedSwapStatus], [RejectionReson], [PatientFromReadyToSwap], [PatientToReadyToSwap], [PatientFromSwap], [PatientToSwap], [SwapReson], [ApproveDate], [RequesterNurseID], [ApprovalNurseID], [PatientFromSwapNurseID], [PatientToSwapNurseID], [PatientFromReadyToSwapNurseID], [PatientToReadyToSwapNurseID], [UCreateby], [PatientFromSwapDate], [PatientToSwapDate], [PatientFromReadyToSwapDate], [PatientToReadyToSwapDate], [PatientFrom_EscortBedFrom], [PatientFrom_EscortBedTo], [PatientTo_EscortBedFrom], [PatientTo_EscortBedTo], [CompanyID], [TenantId])
SELECT [ID], [BedFrom], [BedTo], [PatientFrom], [PatientTo], [DateSwap], [BedSwapStatus], [RejectionReson], [PatientFromReadyToSwap], [PatientToReadyToSwap], [PatientFromSwap], [PatientToSwap], [SwapReson], [ApproveDate], [RequesterNurseID], [ApprovalNurseID], [PatientFromSwapNurseID], [PatientToSwapNurseID], [PatientFromReadyToSwapNurseID], [PatientToReadyToSwapNurseID], [UCreateby], [PatientFromSwapDate], [PatientToSwapDate], [PatientFromReadyToSwapDate], [PatientToReadyToSwapDate], [PatientFrom_EscortBedFrom], [PatientFrom_EscortBedTo], [PatientTo_EscortBedFrom], [PatientTo_EscortBedTo], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[BedSwap];
SET IDENTITY_INSERT [InPatient].[BedSwap] OFF;
GO

PRINT 'Migrating [InPatient].[BedTracker]...';
SET IDENTITY_INSERT [InPatient].[BedTracker] ON;
INSERT INTO [InPatient].[BedTracker] ([Id], [OPIP], [PatientId], [BedId], [FromDateTime], [ToDateTime], [AcType], [TenantId])
SELECT [Id], [OPIP], [PatientId], [BedId], [FromDateTime], [ToDateTime], [AcType], @TenantId
FROM [SunCity_Clinics].[InPatient].[BedTracker];
SET IDENTITY_INSERT [InPatient].[BedTracker] OFF;
GO

PRINT 'Migrating [InPatient].[BedType]...';
SET IDENTITY_INSERT [InPatient].[BedType] ON;
INSERT INTO [InPatient].[BedType] ([Id], [Name], [TypeImage], [CompanyID], [NameAr], [NameKa], [TenantId])
SELECT [Id], [Name], [TypeImage], [CompanyID], [NameAr], [NameKa], @TenantId
FROM [SunCity_Clinics].[InPatient].[BedType];
SET IDENTITY_INSERT [InPatient].[BedType] OFF;
GO

PRINT 'Migrating [InPatient].[BedWardArrangement]...';
SET IDENTITY_INSERT [InPatient].[BedWardArrangement] ON;
INSERT INTO [InPatient].[BedWardArrangement] ([Id], [WardId], [RowsNumber], [BedCount], [CompanyID], [TenantId])
SELECT [Id], [WardId], [RowsNumber], [BedCount], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[BedWardArrangement];
SET IDENTITY_INSERT [InPatient].[BedWardArrangement] OFF;
GO

PRINT 'Migrating [InPatient].[Bed]...';
SET IDENTITY_INSERT [InPatient].[Bed] ON;
INSERT INTO [InPatient].[Bed] ([Id], [BedNumber], [Description], [BedStatusId], [ChildBed], [RoomId], [BedTypeId], [BedSequence], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Code], [Location], [CountableBed], [Active], [DescriptionAr], [AssetID], [IsBed], [BedTypeIds], [BranchId], [TenantId])
SELECT [Id], [BedNumber], [Description], [BedStatusId], [ChildBed], [RoomId], [BedTypeId], [BedSequence], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Code], [Location], [CountableBed], [Active], [DescriptionAr], [AssetID], [IsBed], [BedTypeIds], [BranchId], @TenantId
FROM [SunCity_Clinics].[InPatient].[Bed];
SET IDENTITY_INSERT [InPatient].[Bed] OFF;
GO

PRINT 'Migrating [InPatient].[Blacklist]...';
SET IDENTITY_INSERT [InPatient].[Blacklist] ON;
INSERT INTO [InPatient].[Blacklist] ([BlacklistId], [NationalIdType], [Reason], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [IdentificationNo], [TenantId])
SELECT [BlacklistId], [NationalIdType], [Reason], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [IdentificationNo], @TenantId
FROM [SunCity_Clinics].[InPatient].[Blacklist];
SET IDENTITY_INSERT [InPatient].[Blacklist] OFF;
GO

PRINT 'Migrating [InPatient].[Buildings]...';
SET IDENTITY_INSERT [InPatient].[Buildings] ON;
INSERT INTO [InPatient].[Buildings] ([Id], [BuildingCode], [Description], [NameEn], [BuildingStatusId], [BuildingSequence], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BranchId], [TenantId])
SELECT [Id], [BuildingCode], [Description], [NameEn], [BuildingStatusId], [BuildingSequence], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BranchId], @TenantId
FROM [SunCity_Clinics].[InPatient].[Buildings];
SET IDENTITY_INSERT [InPatient].[Buildings] OFF;
GO

PRINT 'Migrating [InPatient].[CRTPImplantationReport]...';
SET IDENTITY_INSERT [InPatient].[CRTPImplantationReport] ON;
INSERT INTO [InPatient].[CRTPImplantationReport] ([ID], [PatientID], [IPNumber], [DateOfImplantation], [Operator], [Ys], [DoctorID], [IndicationForPermanent], [ECGBeforePacemaker], [PreMedication], [IVAntibiotic], [LocalAnaesthesia], [VenousAccess], [PocketSite], [LeadInsertionSite], [AtrialLead], [RightVentricualLead], [LeftVentricualLead], [BatteryInsertionSite], [BatteryFixation], [WoundClosure], [Subcutaneous], [Skin], [LocalAntibiotic], [PWave], [RWave], [RightVentricularPacingThreshold], [LeftVentricularPacingThreshold], [ImpedanceAtrialLead], [ImpedanceRightVentricularPacingThreshold], [ImpedanceLeftVentricularPacingThreshold], [RVLeadManufacturer], [LVLeadManufacturer], [AtrialLeadManufacturer], [BatteryDataManufacturer], [LVLeadModel], [RVLeadModel], [AtrialLeadModel], [BatteryDataModel], [LVLeadSerialNo], [RVLeadSerialNo], [AtrialLeadSerialNo], [BatteryDataSerialNo], [LastProgrammedData], [ECGAfterImplantation], [Complications], [Antibiotics], [AppointmentsForPacemaker], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [IPNumber], [DateOfImplantation], [Operator], [Ys], [DoctorID], [IndicationForPermanent], [ECGBeforePacemaker], [PreMedication], [IVAntibiotic], [LocalAnaesthesia], [VenousAccess], [PocketSite], [LeadInsertionSite], [AtrialLead], [RightVentricualLead], [LeftVentricualLead], [BatteryInsertionSite], [BatteryFixation], [WoundClosure], [Subcutaneous], [Skin], [LocalAntibiotic], [PWave], [RWave], [RightVentricularPacingThreshold], [LeftVentricularPacingThreshold], [ImpedanceAtrialLead], [ImpedanceRightVentricularPacingThreshold], [ImpedanceLeftVentricularPacingThreshold], [RVLeadManufacturer], [LVLeadManufacturer], [AtrialLeadManufacturer], [BatteryDataManufacturer], [LVLeadModel], [RVLeadModel], [AtrialLeadModel], [BatteryDataModel], [LVLeadSerialNo], [RVLeadSerialNo], [AtrialLeadSerialNo], [BatteryDataSerialNo], [LastProgrammedData], [ECGAfterImplantation], [Complications], [Antibiotics], [AppointmentsForPacemaker], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[CRTPImplantationReport];
SET IDENTITY_INSERT [InPatient].[CRTPImplantationReport] OFF;
GO

PRINT 'Migrating [InPatient].[CafeteriaCharges]...';
SET IDENTITY_INSERT [InPatient].[CafeteriaCharges] ON;
INSERT INTO [InPatient].[CafeteriaCharges] ([Id], [PatientID], [LastIPNumber], [ChargeDate], [ServiceID], [Quantity], [Charge], [IsGuest], [IsNoCharge], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Total], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [LastIPNumber], [ChargeDate], [ServiceID], [Quantity], [Charge], [IsGuest], [IsNoCharge], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Total], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[CafeteriaCharges];
SET IDENTITY_INSERT [InPatient].[CafeteriaCharges] OFF;
GO

PRINT 'Migrating [InPatient].[CancelAdmission]...';
SET IDENTITY_INSERT [InPatient].[CancelAdmission] ON;
INSERT INTO [InPatient].[CancelAdmission] ([Id], [Reason], [PatientId], [DateCancelation], [AdmitPatientsID], [ReasonID], [Note], [TenantId])
SELECT [Id], [Reason], [PatientId], [DateCancelation], [AdmitPatientsID], [ReasonID], [Note], @TenantId
FROM [SunCity_Clinics].[InPatient].[CancelAdmission];
SET IDENTITY_INSERT [InPatient].[CancelAdmission] OFF;
GO

PRINT 'Migrating [InPatient].[CancelAdmitPatientInAnotherMedicalUnit]...';
SET IDENTITY_INSERT [InPatient].[CancelAdmitPatientInAnotherMedicalUnit] ON;
INSERT INTO [InPatient].[CancelAdmitPatientInAnotherMedicalUnit] ([ID], [PatientID], [DischargeReasonID], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [DischargeReasonID], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[CancelAdmitPatientInAnotherMedicalUnit];
SET IDENTITY_INSERT [InPatient].[CancelAdmitPatientInAnotherMedicalUnit] OFF;
GO

PRINT 'Migrating [InPatient].[CancelDischargePatientInAnotherMedicalUnit]...';
SET IDENTITY_INSERT [InPatient].[CancelDischargePatientInAnotherMedicalUnit] ON;
INSERT INTO [InPatient].[CancelDischargePatientInAnotherMedicalUnit] ([ID], [PatientID], [DischargeReasonID], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [DischargeReasonID], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[CancelDischargePatientInAnotherMedicalUnit];
SET IDENTITY_INSERT [InPatient].[CancelDischargePatientInAnotherMedicalUnit] OFF;
GO

PRINT 'Migrating [InPatient].[CancelDischargeReason]...';
SET IDENTITY_INSERT [InPatient].[CancelDischargeReason] ON;
INSERT INTO [InPatient].[CancelDischargeReason] ([ID], [Code], [EnglishName], [ArabicName], [Active], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [EnglishName], [ArabicName], [Active], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[CancelDischargeReason];
SET IDENTITY_INSERT [InPatient].[CancelDischargeReason] OFF;
GO

PRINT 'Migrating [InPatient].[CancelDischarge]...';
SET IDENTITY_INSERT [InPatient].[CancelDischarge] ON;
INSERT INTO [InPatient].[CancelDischarge] ([ID], [PatientID], [DischargeReasonID], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [DischargeReasonID], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[CancelDischarge];
SET IDENTITY_INSERT [InPatient].[CancelDischarge] OFF;
GO

PRINT 'Migrating [InPatient].[CancelIntialDischarge]...';
SET IDENTITY_INSERT [InPatient].[CancelIntialDischarge] ON;
INSERT INTO [InPatient].[CancelIntialDischarge] ([ID], [PatientID], [InitialDischID], [Comment], [TenantId])
SELECT [ID], [PatientID], [InitialDischID], [Comment], @TenantId
FROM [SunCity_Clinics].[InPatient].[CancelIntialDischarge];
SET IDENTITY_INSERT [InPatient].[CancelIntialDischarge] OFF;
GO

PRINT 'Migrating [InPatient].[CancelTypesMaster]...';
SET IDENTITY_INSERT [InPatient].[CancelTypesMaster] ON;
INSERT INTO [InPatient].[CancelTypesMaster] ([ID], [EnglishDescription], [ArabicDescription], [CancelType], [CompanyID], [TenantId])
SELECT [ID], [EnglishDescription], [ArabicDescription], [CancelType], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[CancelTypesMaster];
SET IDENTITY_INSERT [InPatient].[CancelTypesMaster] OFF;
GO

PRINT 'Migrating [InPatient].[CancellationOfPatientRequest]...';
SET IDENTITY_INSERT [InPatient].[CancellationOfPatientRequest] ON;
INSERT INTO [InPatient].[CancellationOfPatientRequest] ([ID], [OrderID], [UserID], [ReasonCancellation], [CancellationDate], [CompanyID], [TenantId])
SELECT [ID], [OrderID], [UserID], [ReasonCancellation], [CancellationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[CancellationOfPatientRequest];
SET IDENTITY_INSERT [InPatient].[CancellationOfPatientRequest] OFF;
GO

PRINT 'Migrating [InPatient].[CardiacCatheterization]...';
SET IDENTITY_INSERT [InPatient].[CardiacCatheterization] ON;
INSERT INTO [InPatient].[CardiacCatheterization] ([CardiacId], [PatientId], [IPNumber], [Date], [RiskFactors], [Operators], [Procedures], [HemodyNamic], [Angiogrephic], [LeftMainCoronaryArtery], [LeftAnteriorDescending], [LeftCircumflexArtery], [RightCoronary], [LeftVentriculography], [Recommendations], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [CardiacId], [PatientId], [IPNumber], [Date], [RiskFactors], [Operators], [Procedures], [HemodyNamic], [Angiogrephic], [LeftMainCoronaryArtery], [LeftAnteriorDescending], [LeftCircumflexArtery], [RightCoronary], [LeftVentriculography], [Recommendations], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[CardiacCatheterization];
SET IDENTITY_INSERT [InPatient].[CardiacCatheterization] OFF;
GO

PRINT 'Migrating [InPatient].[Cardiac_Electrphysiology]...';
SET IDENTITY_INSERT [InPatient].[Cardiac_Electrphysiology] ON;
INSERT INTO [InPatient].[Cardiac_Electrphysiology] ([id], [PatintID], [ReportDate], [DoctorID], [History], [Medications], [ECG], [CardiacProcedure], [Ventricular_Pacing], [Atrial_Pacing], [RadioFreq_Ablation], [Ablation_Target], [RFCurrent], [ECG_DuringRF], [Diagnosis], [Recommendation], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [id], [PatintID], [ReportDate], [DoctorID], [History], [Medications], [ECG], [CardiacProcedure], [Ventricular_Pacing], [Atrial_Pacing], [RadioFreq_Ablation], [Ablation_Target], [RFCurrent], [ECG_DuringRF], [Diagnosis], [Recommendation], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[Cardiac_Electrphysiology];
SET IDENTITY_INSERT [InPatient].[Cardiac_Electrphysiology] OFF;
GO

PRINT 'Migrating [InPatient].[ChangePatientDoctor]...';
SET IDENTITY_INSERT [InPatient].[ChangePatientDoctor] ON;
INSERT INTO [InPatient].[ChangePatientDoctor] ([Id], [PatientId], [OldDoctorId], [NewDoctorId], [ChangeReason], [CompanyID], [TenantId])
SELECT [Id], [PatientId], [OldDoctorId], [NewDoctorId], [ChangeReason], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[ChangePatientDoctor];
SET IDENTITY_INSERT [InPatient].[ChangePatientDoctor] OFF;
GO

PRINT 'Migrating [InPatient].[ClottingTimeDetails]...';
SET IDENTITY_INSERT [InPatient].[ClottingTimeDetails] ON;
INSERT INTO [InPatient].[ClottingTimeDetails] ([Id], [MasterId], [TestDate], [TestTime], [ClottingTime], [Dose], [AdjustedDose], [UserId], [TenantId])
SELECT [Id], [MasterId], [TestDate], [TestTime], [ClottingTime], [Dose], [AdjustedDose], [UserId], @TenantId
FROM [SunCity_Clinics].[InPatient].[ClottingTimeDetails];
SET IDENTITY_INSERT [InPatient].[ClottingTimeDetails] OFF;
GO

PRINT 'Migrating [InPatient].[ClottingTimeMaster]...';
SET IDENTITY_INSERT [InPatient].[ClottingTimeMaster] ON;
INSERT INTO [InPatient].[ClottingTimeMaster] ([Id], [PatientId], [PatientIPNumber], [AdmitDoctorID], [TenantId])
SELECT [Id], [PatientId], [PatientIPNumber], [AdmitDoctorID], @TenantId
FROM [SunCity_Clinics].[InPatient].[ClottingTimeMaster];
SET IDENTITY_INSERT [InPatient].[ClottingTimeMaster] OFF;
GO

PRINT 'Migrating [InPatient].[ConsultaionEnum]...';
INSERT INTO [InPatient].[ConsultaionEnum] ([id], [ParentID], [Name], [NameAr], [ConsType], [TenantId])
SELECT [id], [ParentID], [Name], [NameAr], [ConsType], @TenantId
FROM [SunCity_Clinics].[InPatient].[ConsultaionEnum];
GO

PRINT 'Migrating [InPatient].[Consultation_Request]...';
SET IDENTITY_INSERT [InPatient].[Consultation_Request] ON;
INSERT INTO [InPatient].[Consultation_Request] ([id], [PatientID], [Urgency], [Request_type], [FromDr], [ToDr], [RequestReason], [RequestDate], [Request_Response], [Response_comment], [IPOPNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [RequestTime], [OtherType], [TransferBedNo], [MainReqType], [FromNurseID_Ready], [ToNurseID_Ready], [FromNurseID_Left], [ToNurseID_Arrived], [FromReady_Date], [ToReady_Date], [FromLeft_Date], [ToArrive_Date], [NewEscortBedNo], [HandoverStatus], [CancelRequest], [TenantId])
SELECT [id], [PatientID], [Urgency], [Request_type], [FromDr], [ToDr], [RequestReason], [RequestDate], [Request_Response], [Response_comment], [IPOPNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [RequestTime], [OtherType], [TransferBedNo], [MainReqType], [FromNurseID_Ready], [ToNurseID_Ready], [FromNurseID_Left], [ToNurseID_Arrived], [FromReady_Date], [ToReady_Date], [FromLeft_Date], [ToArrive_Date], [NewEscortBedNo], [HandoverStatus], [CancelRequest], @TenantId
FROM [SunCity_Clinics].[InPatient].[Consultation_Request];
SET IDENTITY_INSERT [InPatient].[Consultation_Request] OFF;
GO

PRINT 'Migrating [InPatient].[CoronaryIntervention]...';
SET IDENTITY_INSERT [InPatient].[CoronaryIntervention] ON;
INSERT INTO [InPatient].[CoronaryIntervention] ([Id], [PatientId], [IPNumber], [RiskFactors], [Operators], [Procedures], [LAD], [RCA], [Equipments], [Comments], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [CompanyID], [TenantId])
SELECT [Id], [PatientId], [IPNumber], [RiskFactors], [Operators], [Procedures], [LAD], [RCA], [Equipments], [Comments], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[CoronaryIntervention];
SET IDENTITY_INSERT [InPatient].[CoronaryIntervention] OFF;
GO

PRINT 'Migrating [InPatient].[CurrentMedication]...';
SET IDENTITY_INSERT [InPatient].[CurrentMedication] ON;
INSERT INTO [InPatient].[CurrentMedication] ([Id], [PatientID], [PrescriptionNo], [DrugID], [Dosage], [DosageUnitID], [FrequencyID], [Period], [PeriodTypeID], [DoseQTY], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [AssesmentID], [Remarks], [ContinueDrugDuringAdmission], [NurseAdmissionAssessmentId], [TenantId])
SELECT [Id], [PatientID], [PrescriptionNo], [DrugID], [Dosage], [DosageUnitID], [FrequencyID], [Period], [PeriodTypeID], [DoseQTY], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [AssesmentID], [Remarks], [ContinueDrugDuringAdmission], [NurseAdmissionAssessmentId], @TenantId
FROM [SunCity_Clinics].[InPatient].[CurrentMedication];
SET IDENTITY_INSERT [InPatient].[CurrentMedication] OFF;
GO

PRINT 'Migrating [InPatient].[DCAF]...';
SET IDENTITY_INSERT [InPatient].[DCAF] ON;
INSERT INTO [InPatient].[DCAF] ([Id], [Patient], [txtEligiberNo], [txtDurationOfIllness], [txtSignificatSigns], [txtDiagnoses], [txtPrimary], [txtSecondary], [txtOtherConditions], [txtOther], [txtHow], [txtWhen], [txtWhere], [txtCompleted_Coded_By], [txtSignature], [txtline_of_managment_When_applicable], [txtEsstimated], [txtAdmissionDate], [txtPhysician], [txtDate], [txtRelationship], [txtRelationshipSignature], [txtRelationsDate], [ChxPlanType], [ChxNewVisit], [ChxFollowUp], [ChxRegularDentalTreatment], [ChxDentalCleaning], [ChxRTA], [ChxWorkRelated], [ChxmanagmentY], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OPNumber], [TenantId])
SELECT [Id], [Patient], [txtEligiberNo], [txtDurationOfIllness], [txtSignificatSigns], [txtDiagnoses], [txtPrimary], [txtSecondary], [txtOtherConditions], [txtOther], [txtHow], [txtWhen], [txtWhere], [txtCompleted_Coded_By], [txtSignature], [txtline_of_managment_When_applicable], [txtEsstimated], [txtAdmissionDate], [txtPhysician], [txtDate], [txtRelationship], [txtRelationshipSignature], [txtRelationsDate], [ChxPlanType], [ChxNewVisit], [ChxFollowUp], [ChxRegularDentalTreatment], [ChxDentalCleaning], [ChxRTA], [ChxWorkRelated], [ChxmanagmentY], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OPNumber], @TenantId
FROM [SunCity_Clinics].[InPatient].[DCAF];
SET IDENTITY_INSERT [InPatient].[DCAF] OFF;
GO

PRINT 'Migrating [InPatient].[DeliveryDetails]...';
SET IDENTITY_INSERT [InPatient].[DeliveryDetails] ON;
INSERT INTO [InPatient].[DeliveryDetails] ([Id], [PatientID], [IdentificationName], [DeliveryModeID], [SexId], [DateofBirth], [TimeofBirth], [Condition], [DeliveryTerm], [Sterilization], [BabyWardID], [BedID], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DeliveryId], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [IdentificationName], [DeliveryModeID], [SexId], [DateofBirth], [TimeofBirth], [Condition], [DeliveryTerm], [Sterilization], [BabyWardID], [BedID], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DeliveryId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[DeliveryDetails];
SET IDENTITY_INSERT [InPatient].[DeliveryDetails] OFF;
GO

PRINT 'Migrating [InPatient].[DeliveryType]...';
SET IDENTITY_INSERT [InPatient].[DeliveryType] ON;
INSERT INTO [InPatient].[DeliveryType] ([Id], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[DeliveryType];
SET IDENTITY_INSERT [InPatient].[DeliveryType] OFF;
GO

PRINT 'Migrating [InPatient].[Delivery]...';
SET IDENTITY_INSERT [InPatient].[Delivery] ON;
INSERT INTO [InPatient].[Delivery] ([Id], [PatientID], [ObstetricianID], [PediatricianID], [DeliveryDate], [MidWifeID], [Notes], [OEHID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [ObstetricianID], [PediatricianID], [DeliveryDate], [MidWifeID], [Notes], [OEHID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[Delivery];
SET IDENTITY_INSERT [InPatient].[Delivery] OFF;
GO

PRINT 'Migrating [InPatient].[DestinationOfPatient]...';
SET IDENTITY_INSERT [InPatient].[DestinationOfPatient] ON;
INSERT INTO [InPatient].[DestinationOfPatient] ([Id], [DestinationName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [DestinationName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[DestinationOfPatient];
SET IDENTITY_INSERT [InPatient].[DestinationOfPatient] OFF;
GO

PRINT 'Migrating [InPatient].[DischargeReason]...';
SET IDENTITY_INSERT [InPatient].[DischargeReason] ON;
INSERT INTO [InPatient].[DischargeReason] ([ID], [Code], [EnglishName], [ArabicName], [Active], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [EnglishName], [ArabicName], [Active], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[DischargeReason];
SET IDENTITY_INSERT [InPatient].[DischargeReason] OFF;
GO

PRINT 'Migrating [InPatient].[DischargeSummaryICD]...';
SET IDENTITY_INSERT [InPatient].[DischargeSummaryICD] ON;
INSERT INTO [InPatient].[DischargeSummaryICD] ([Id], [PatientID], [DischargeSummaryID], [ICDCodeID], [IsFinalDiagnosis], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [DischargeSummaryID], [ICDCodeID], [IsFinalDiagnosis], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[DischargeSummaryICD];
SET IDENTITY_INSERT [InPatient].[DischargeSummaryICD] OFF;
GO

PRINT 'Migrating [InPatient].[DischargeSummary]...';
SET IDENTITY_INSERT [InPatient].[DischargeSummary] ON;
INSERT INTO [InPatient].[DischargeSummary] ([Id], [PatientID], [DestinationID], [DischargeTypeID], [Remarks], [DischargeDate], [DischargeTime], [Approvedate], [ApproveTime], [ApproveDoctorID], [PresentingComplaint], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DoctorID], [DestinationText], [CompanyID], [CoMorbidConditions], [SignificantPhysicalandOtherFindings], [IntrahospitalCourse], [INVRDId], [DiagnosticandTherapeuticProceduresPerformed], [SignificantMedicationsandOtherTreatments], [PresDId], [FollowupInstructions], [NextbookedOPDVisitDate], [whentoseekEmergencyMedicalCare], [IPNumber], [TenantId])
SELECT [Id], [PatientID], [DestinationID], [DischargeTypeID], [Remarks], [DischargeDate], [DischargeTime], [Approvedate], [ApproveTime], [ApproveDoctorID], [PresentingComplaint], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DoctorID], [DestinationText], [CompanyID], [CoMorbidConditions], [SignificantPhysicalandOtherFindings], [IntrahospitalCourse], [INVRDId], [DiagnosticandTherapeuticProceduresPerformed], [SignificantMedicationsandOtherTreatments], [PresDId], [FollowupInstructions], [NextbookedOPDVisitDate], [whentoseekEmergencyMedicalCare], [IPNumber], @TenantId
FROM [SunCity_Clinics].[InPatient].[DischargeSummary];
SET IDENTITY_INSERT [InPatient].[DischargeSummary] OFF;
GO

PRINT 'Migrating [InPatient].[DischargeType]...';
SET IDENTITY_INSERT [InPatient].[DischargeType] ON;
INSERT INTO [InPatient].[DischargeType] ([Id], [TypeCode], [NameArabic], [NameEnglish], [Status], [HospitalCase], [IsDeath], [CompanyID], [TenantId])
SELECT [Id], [TypeCode], [NameArabic], [NameEnglish], [Status], [HospitalCase], [IsDeath], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[DischargeType];
SET IDENTITY_INSERT [InPatient].[DischargeType] OFF;
GO

PRINT 'Migrating [InPatient].[Discharge_Order]...';
SET IDENTITY_INSERT [InPatient].[Discharge_Order] ON;
INSERT INTO [InPatient].[Discharge_Order] ([id], [AdmitPatientID], [OrderDate], [OrderTime], [ReasonID], [FileURL], [HomeMedicine], [PFE], [Discharge_Doc], [DoctorID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ReAdmit], [TenantId])
SELECT [id], [AdmitPatientID], [OrderDate], [OrderTime], [ReasonID], [FileURL], [HomeMedicine], [PFE], [Discharge_Doc], [DoctorID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ReAdmit], @TenantId
FROM [SunCity_Clinics].[InPatient].[Discharge_Order];
SET IDENTITY_INSERT [InPatient].[Discharge_Order] OFF;
GO

PRINT 'Migrating [InPatient].[Dobutamine_Stress_Echocardiography]...';
SET IDENTITY_INSERT [InPatient].[Dobutamine_Stress_Echocardiography] ON;
INSERT INTO [InPatient].[Dobutamine_Stress_Echocardiography] ([Id], [PID], [DateofExam], [InterPretations], [conclustion], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TimeofExam], [CompanyID], [comment], [TenantId])
SELECT [Id], [PID], [DateofExam], [InterPretations], [conclustion], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TimeofExam], [CompanyID], [comment], @TenantId
FROM [SunCity_Clinics].[InPatient].[Dobutamine_Stress_Echocardiography];
SET IDENTITY_INSERT [InPatient].[Dobutamine_Stress_Echocardiography] OFF;
GO

PRINT 'Migrating [InPatient].[Dobutamine_Stress_Echocardiography_Comments]...';
SET IDENTITY_INSERT [InPatient].[Dobutamine_Stress_Echocardiography_Comments] ON;
INSERT INTO [InPatient].[Dobutamine_Stress_Echocardiography_Comments] ([Id], [Dobutamine_Stress_EchocardiographyID], [Dose], [BP], [HR], [CompanyID], [TenantId])
SELECT [Id], [Dobutamine_Stress_EchocardiographyID], [Dose], [BP], [HR], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[Dobutamine_Stress_Echocardiography_Comments];
SET IDENTITY_INSERT [InPatient].[Dobutamine_Stress_Echocardiography_Comments] OFF;
GO

PRINT 'Migrating [InPatient].[Dobutamine_Stress_Echocardiography_MGM]...';
SET IDENTITY_INSERT [InPatient].[Dobutamine_Stress_Echocardiography_MGM] ON;
INSERT INTO [InPatient].[Dobutamine_Stress_Echocardiography_MGM] ([Id], [Dobutamine_Stress_EchocardiographyID], [MGM], [Type], [Basal], [Mid], [Apical], [CompanyID], [TenantId])
SELECT [Id], [Dobutamine_Stress_EchocardiographyID], [MGM], [Type], [Basal], [Mid], [Apical], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[Dobutamine_Stress_Echocardiography_MGM];
SET IDENTITY_INSERT [InPatient].[Dobutamine_Stress_Echocardiography_MGM] OFF;
GO

PRINT 'Migrating [InPatient].[DoctorInstructionClassification]...';
SET IDENTITY_INSERT [InPatient].[DoctorInstructionClassification] ON;
INSERT INTO [InPatient].[DoctorInstructionClassification] ([ID], [Code], [DescriptionAr], [DescriptionEn], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [DescriptionAr], [DescriptionEn], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[DoctorInstructionClassification];
SET IDENTITY_INSERT [InPatient].[DoctorInstructionClassification] OFF;
GO

PRINT 'Migrating [InPatient].[DoctorNote]...';
SET IDENTITY_INSERT [InPatient].[DoctorNote] ON;
INSERT INTO [InPatient].[DoctorNote] ([Id], [MedicalObservationID], [Note], [NoteDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [MedicalObservationID], [Note], [NoteDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[DoctorNote];
SET IDENTITY_INSERT [InPatient].[DoctorNote] OFF;
GO

PRINT 'Migrating [InPatient].[DoctorTransfer]...';
SET IDENTITY_INSERT [InPatient].[DoctorTransfer] ON;
INSERT INTO [InPatient].[DoctorTransfer] ([Id], [PatientID], [FromDepartmentID], [ToDepartmentID], [FromDoctorID], [ToDoctorID], [TransferDate], [Transfertime], [transferReason], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [FromDepartmentID], [ToDepartmentID], [FromDoctorID], [ToDoctorID], [TransferDate], [Transfertime], [transferReason], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[DoctorTransfer];
SET IDENTITY_INSERT [InPatient].[DoctorTransfer] OFF;
GO

PRINT 'Migrating [InPatient].[DrugChart]...';
SET IDENTITY_INSERT [InPatient].[DrugChart] ON;
INSERT INTO [InPatient].[DrugChart] ([Id], [PatientID], [IPNO], [DrugID], [Dosage], [DrugChartDateDate], [Time], [DosageUnitID], [IsChecked], [PrescrirtionDetailsId], [WardPharmacyDetailsId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [IPNO], [DrugID], [Dosage], [DrugChartDateDate], [Time], [DosageUnitID], [IsChecked], [PrescrirtionDetailsId], [WardPharmacyDetailsId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[DrugChart];
SET IDENTITY_INSERT [InPatient].[DrugChart] OFF;
GO

PRINT 'Migrating [InPatient].[ECGReport]...';
SET IDENTITY_INSERT [InPatient].[ECGReport] ON;
INSERT INTO [InPatient].[ECGReport] ([Id], [ECGDate], [ECGTime], [PatientId], [PatientIPNumber], [DoctorId], [Rhythm], [HeartRate], [Axis], [pwaveAmplitude], [pwaveDuration], [PRInterval], [QRSComplexMorphology], [QRSComplexDuration], [STSegment], [TWave], [Interpretion], [userid], [TenantId])
SELECT [Id], [ECGDate], [ECGTime], [PatientId], [PatientIPNumber], [DoctorId], [Rhythm], [HeartRate], [Axis], [pwaveAmplitude], [pwaveDuration], [PRInterval], [QRSComplexMorphology], [QRSComplexDuration], [STSegment], [TWave], [Interpretion], [userid], @TenantId
FROM [SunCity_Clinics].[InPatient].[ECGReport];
SET IDENTITY_INSERT [InPatient].[ECGReport] OFF;
GO

PRINT 'Migrating [InPatient].[EscortDetails]...';
SET IDENTITY_INSERT [InPatient].[EscortDetails] ON;
INSERT INTO [InPatient].[EscortDetails] ([Id], [EscortId], [IPNumber], [EscortName], [IdentityType], [EscortNationalId], [GenderTypeId], [EscortAge], [RelativeId], [EscortNeedBed], [BedId], [IsActive], [Payment], [ApprovalOnTerms], [ApprovalOnTermsFileUrl], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [Id], [EscortId], [IPNumber], [EscortName], [IdentityType], [EscortNationalId], [GenderTypeId], [EscortAge], [RelativeId], [EscortNeedBed], [BedId], [IsActive], [Payment], [ApprovalOnTerms], [ApprovalOnTermsFileUrl], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[InPatient].[EscortDetails];
SET IDENTITY_INSERT [InPatient].[EscortDetails] OFF;
GO

PRINT 'Migrating [InPatient].[EscortEnterance]...';
SET IDENTITY_INSERT [InPatient].[EscortEnterance] ON;
INSERT INTO [InPatient].[EscortEnterance] ([Id], [EscortId], [EnteranceType], [EnteranceTime], [CreatedDate], [CompanyID], [TenantId])
SELECT [Id], [EscortId], [EnteranceType], [EnteranceTime], [CreatedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[EscortEnterance];
SET IDENTITY_INSERT [InPatient].[EscortEnterance] OFF;
GO

PRINT 'Migrating [InPatient].[Escort]...';
SET IDENTITY_INSERT [InPatient].[Escort] ON;
INSERT INTO [InPatient].[Escort] ([EscortId], [PatientID], [IPNumber], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [EscortId], [PatientID], [IPNumber], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[InPatient].[Escort];
SET IDENTITY_INSERT [InPatient].[Escort] OFF;
GO

PRINT 'Migrating [InPatient].[EstimatedmissionDetails]...';
SET IDENTITY_INSERT [InPatient].[EstimatedmissionDetails] ON;
INSERT INTO [InPatient].[EstimatedmissionDetails] ([Id], [EstimatedmissionID], [ServiceID], [Units], [DiscountAmount], [DiscountPrecentage], [NetAmount], [CompanyID], [TenantId])
SELECT [Id], [EstimatedmissionID], [ServiceID], [Units], [DiscountAmount], [DiscountPrecentage], [NetAmount], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[EstimatedmissionDetails];
SET IDENTITY_INSERT [InPatient].[EstimatedmissionDetails] OFF;
GO

PRINT 'Migrating [InPatient].[Estimatedmission]...';
SET IDENTITY_INSERT [InPatient].[Estimatedmission] ON;
INSERT INTO [InPatient].[Estimatedmission] ([Id], [SurgeryDate], [AdmissionRequestId], [EstimateNO], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CostEstimateStatus], [PatientId], [IsLinked], [OPnumber], [DoctorID], [AdmissionDate], [Days], [RoomTypeID], [EstimatedmissionStatus], [CompanyID], [TenantId])
SELECT [Id], [SurgeryDate], [AdmissionRequestId], [EstimateNO], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CostEstimateStatus], [PatientId], [IsLinked], [OPnumber], [DoctorID], [AdmissionDate], [Days], [RoomTypeID], [EstimatedmissionStatus], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[Estimatedmission];
SET IDENTITY_INSERT [InPatient].[Estimatedmission] OFF;
GO

PRINT 'Migrating [InPatient].[EyesightMeasurement]...';
SET IDENTITY_INSERT [InPatient].[EyesightMeasurement] ON;
INSERT INTO [InPatient].[EyesightMeasurement] ([Id], [MedicalObservationID], [RSPHDiest], [RSPHNear], [RCYLDiest], [RCYLNear], [RAXDiest], [RAXNear], [LSPHDiest], [LSPHNear], [LCYLDiest], [LCYLNear], [LAXDiest], [LAXNear], [RReading], [LReading], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [prisimbase], [Prisimamount], [TenantId])
SELECT [Id], [MedicalObservationID], [RSPHDiest], [RSPHNear], [RCYLDiest], [RCYLNear], [RAXDiest], [RAXNear], [LSPHDiest], [LSPHNear], [LCYLDiest], [LCYLNear], [LAXDiest], [LAXNear], [RReading], [LReading], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [prisimbase], [Prisimamount], @TenantId
FROM [SunCity_Clinics].[InPatient].[EyesightMeasurement];
SET IDENTITY_INSERT [InPatient].[EyesightMeasurement] OFF;
GO

PRINT 'Migrating [InPatient].[FindingFlag]...';
SET IDENTITY_INSERT [InPatient].[FindingFlag] ON;
INSERT INTO [InPatient].[FindingFlag] ([Id], [Name], [CompanyID], [TenantId])
SELECT [Id], [Name], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[FindingFlag];
SET IDENTITY_INSERT [InPatient].[FindingFlag] OFF;
GO

PRINT 'Migrating [InPatient].[Finding]...';
SET IDENTITY_INSERT [InPatient].[Finding] ON;
INSERT INTO [InPatient].[Finding] ([Id], [Name], [FindingFlagId], [CompanyID], [TenantId])
SELECT [Id], [Name], [FindingFlagId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[Finding];
SET IDENTITY_INSERT [InPatient].[Finding] OFF;
GO

PRINT 'Migrating [InPatient].[Floors]...';
SET IDENTITY_INSERT [InPatient].[Floors] ON;
INSERT INTO [InPatient].[Floors] ([Id], [FloorCode], [Description], [NameEn], [BuildingID], [FloorStatusId], [FloorSequence], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BranchId], [TenantId])
SELECT [Id], [FloorCode], [Description], [NameEn], [BuildingID], [FloorStatusId], [FloorSequence], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BranchId], @TenantId
FROM [SunCity_Clinics].[InPatient].[Floors];
SET IDENTITY_INSERT [InPatient].[Floors] OFF;
GO

PRINT 'Migrating [InPatient].[FrequencyMaster]...';
SET IDENTITY_INSERT [InPatient].[FrequencyMaster] ON;
INSERT INTO [InPatient].[FrequencyMaster] ([ID], [EnglishNameDescription], [ArabicNameDescription], [Frequency], [Type], [FrequencyType], [CompanyID], [TenantId])
SELECT [ID], [EnglishNameDescription], [ArabicNameDescription], [Frequency], [Type], [FrequencyType], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[FrequencyMaster];
SET IDENTITY_INSERT [InPatient].[FrequencyMaster] OFF;
GO

PRINT 'Migrating [InPatient].[GeneralNursingCarePlanHeader]...';
SET IDENTITY_INSERT [InPatient].[GeneralNursingCarePlanHeader] ON;
INSERT INTO [InPatient].[GeneralNursingCarePlanHeader] ([Id], [PatientId], [Code], [NurseId], [CareModeId], [Date], [Time], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientId], [Code], [NurseId], [CareModeId], [Date], [Time], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[GeneralNursingCarePlanHeader];
SET IDENTITY_INSERT [InPatient].[GeneralNursingCarePlanHeader] OFF;
GO

PRINT 'Migrating [InPatient].[GeneralNursingCarePlan_TagsValues]...';
SET IDENTITY_INSERT [InPatient].[GeneralNursingCarePlan_TagsValues] ON;
INSERT INTO [InPatient].[GeneralNursingCarePlan_TagsValues] ([Id], [Value1], [Value2], [GeneralHeaderId], [PageTagsId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Value1], [Value2], [GeneralHeaderId], [PageTagsId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[GeneralNursingCarePlan_TagsValues];
SET IDENTITY_INSERT [InPatient].[GeneralNursingCarePlan_TagsValues] OFF;
GO

PRINT 'Migrating [InPatient].[GitImages]...';
SET IDENTITY_INSERT [InPatient].[GitImages] ON;
INSERT INTO [InPatient].[GitImages] ([ID], [MedicalObservation_GitID], [TapID], [ImageName], [TenantId])
SELECT [ID], [MedicalObservation_GitID], [TapID], [ImageName], @TenantId
FROM [SunCity_Clinics].[InPatient].[GitImages];
SET IDENTITY_INSERT [InPatient].[GitImages] OFF;
GO

PRINT 'Migrating [InPatient].[HeadUpTiltDiagnosis]...';
SET IDENTITY_INSERT [InPatient].[HeadUpTiltDiagnosis] ON;
INSERT INTO [InPatient].[HeadUpTiltDiagnosis] ([ID], [HeadUpTiltID], [ICDCode], [CompanyID], [TenantId])
SELECT [ID], [HeadUpTiltID], [ICDCode], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[HeadUpTiltDiagnosis];
SET IDENTITY_INSERT [InPatient].[HeadUpTiltDiagnosis] OFF;
GO

PRINT 'Migrating [InPatient].[HeadUpTiltTableICDCodes]...';
SET IDENTITY_INSERT [InPatient].[HeadUpTiltTableICDCodes] ON;
INSERT INTO [InPatient].[HeadUpTiltTableICDCodes] ([Id], [HeadUpTiltTableId], [ICDCodeId], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [CompanyID], [TenantId])
SELECT [Id], [HeadUpTiltTableId], [ICDCodeId], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[HeadUpTiltTableICDCodes];
SET IDENTITY_INSERT [InPatient].[HeadUpTiltTableICDCodes] OFF;
GO

PRINT 'Migrating [InPatient].[HeadUpTiltTable]...';
SET IDENTITY_INSERT [InPatient].[HeadUpTiltTable] ON;
INSERT INTO [InPatient].[HeadUpTiltTable] ([Id], [PatientId], [IPNumber], [ReferredBy], [TiltDate], [History], [Investigations], [Medications], [ProcedureStage1], [ProcedureStage2], [ProcedureOther], [RestingHR], [RestingBPSyst], [RestingBPDiast], [Stage1HR], [Stage1BPSyst], [Stage1BPDiast], [Stage1ECG], [Stage1Symptoms], [Stage2HR], [Stage2BPSyst], [Stage2BPDiast], [Stage2ECG], [Stage2Symptoms], [Diagnosis], [Recommendation], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [CompanyID], [TenantId])
SELECT [Id], [PatientId], [IPNumber], [ReferredBy], [TiltDate], [History], [Investigations], [Medications], [ProcedureStage1], [ProcedureStage2], [ProcedureOther], [RestingHR], [RestingBPSyst], [RestingBPDiast], [Stage1HR], [Stage1BPSyst], [Stage1BPDiast], [Stage1ECG], [Stage1Symptoms], [Stage2HR], [Stage2BPSyst], [Stage2BPDiast], [Stage2ECG], [Stage2Symptoms], [Diagnosis], [Recommendation], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[HeadUpTiltTable];
SET IDENTITY_INSERT [InPatient].[HeadUpTiltTable] OFF;
GO

PRINT 'Migrating [InPatient].[HeadUpTilt]...';
SET IDENTITY_INSERT [InPatient].[HeadUpTilt] ON;
INSERT INTO [InPatient].[HeadUpTilt] ([ID], [PatientID], [History], [ECG], [Echocardiography], [StressECG], [ProcedureStage1], [ProcedureStage2], [Results], [DuringStage1], [DuringStage2], [Recomendation], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [History], [ECG], [Echocardiography], [StressECG], [ProcedureStage1], [ProcedureStage2], [Results], [DuringStage1], [DuringStage2], [Recomendation], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[HeadUpTilt];
SET IDENTITY_INSERT [InPatient].[HeadUpTilt] OFF;
GO

PRINT 'Migrating [InPatient].[HebaTest]...';
SET IDENTITY_INSERT [InPatient].[HebaTest] ON;
INSERT INTO [InPatient].[HebaTest] ([Id], [PatientType], [Name], [Birthdate], [TenantId])
SELECT [Id], [PatientType], [Name], [Birthdate], @TenantId
FROM [SunCity_Clinics].[InPatient].[HebaTest];
SET IDENTITY_INSERT [InPatient].[HebaTest] OFF;
GO

PRINT 'Migrating [InPatient].[Holter]...';
SET IDENTITY_INSERT [InPatient].[Holter] ON;
INSERT INTO [InPatient].[Holter] ([HolterId], [RecorderSerial], [Duration], [PatientId], [HolterOrder], [OverreadingPhysician], [ReferringPhysician], [OrderingPhysician], [HookUpTechnician], [AnalyzingTechnician], [IndicationDiagnosis], [Medications], [QRSComplexes], [VentricularBeats], [SupraventricularBeats], [JunctionalBeats], [TotalTimeClassified], [MinHeartRate], [MinHeartRateDate], [Average], [MaxHeartRate], [MaxHeartRateDate], [BeatsInTachycardia], [SecondsMaxRR], [BeatsInBradycardia], [SecondsMaxRRDate], [Isolated], [Couplets], [BigeminalCycles], [RunsTotaling], [Beats], [SuprIsolated], [SuprCouplets], [SuprBigeminalCycles], [SuprRunTotaling], [SuprBeats], [BeatsLongest], [BLRunBpm], [BLRunBpmDate], [BeatsFastest], [BFRunBpm], [BFRunBpmDate], [Interpretation], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [HolterId], [RecorderSerial], [Duration], [PatientId], [HolterOrder], [OverreadingPhysician], [ReferringPhysician], [OrderingPhysician], [HookUpTechnician], [AnalyzingTechnician], [IndicationDiagnosis], [Medications], [QRSComplexes], [VentricularBeats], [SupraventricularBeats], [JunctionalBeats], [TotalTimeClassified], [MinHeartRate], [MinHeartRateDate], [Average], [MaxHeartRate], [MaxHeartRateDate], [BeatsInTachycardia], [SecondsMaxRR], [BeatsInBradycardia], [SecondsMaxRRDate], [Isolated], [Couplets], [BigeminalCycles], [RunsTotaling], [Beats], [SuprIsolated], [SuprCouplets], [SuprBigeminalCycles], [SuprRunTotaling], [SuprBeats], [BeatsLongest], [BLRunBpm], [BLRunBpmDate], [BeatsFastest], [BFRunBpm], [BFRunBpmDate], [Interpretation], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[InPatient].[Holter];
SET IDENTITY_INSERT [InPatient].[Holter] OFF;
GO

PRINT 'Migrating [InPatient].[InfectiousDiseaseScreening]...';
SET IDENTITY_INSERT [InPatient].[InfectiousDiseaseScreening] ON;
INSERT INTO [InPatient].[InfectiousDiseaseScreening] ([Id], [PatientID], [Q1_TravelOutsideUS], [Q1_TravelLocation], [Q1_HouseholdTravel], [Q1_HouseholdTravelLocation], [Q2_CloseContact], [Q3_Fever], [Q4_CoughShortnessBreathSoreThroat], [Q5_VomitingDiarrhea], [Q6_Rash], [Q7_Fever], [Q8_SevereHeadache], [Q9_DiarrheaVomitingAbdominalPain], [Q10_RespiratoryIllness], [Q11_NewWorseningCough], [Q12_SoreThroat], [Q13_ShortnessOfBreath], [Q14_LossOfSmell], [Q15_LossOfTaste], [Q16_UnexplainedHemorrhage], [Q17_FatigueMuscleSkinChanges], [Comment], [AttachmentName], [AttachmentPath], [Identify_PutMaskGloves], [Identify_GivePatientMask], [Identify_ContactSupervisor], [Isolate_SingleRoom], [Isolate_SeparatePatient6Feet], [Isolate_PPEEscort], [Isolate_UrinalBedpan], [Isolate_ProviderReview], [Inform_ContactInfectionPrevention], [Inform_RiskAssessment], [Inform_DoNotMovePatient], [CreatedDate], [ModifiedDate], [ModifiedBy], [CreatedBy], [TenantId])
SELECT [Id], [PatientID], [Q1_TravelOutsideUS], [Q1_TravelLocation], [Q1_HouseholdTravel], [Q1_HouseholdTravelLocation], [Q2_CloseContact], [Q3_Fever], [Q4_CoughShortnessBreathSoreThroat], [Q5_VomitingDiarrhea], [Q6_Rash], [Q7_Fever], [Q8_SevereHeadache], [Q9_DiarrheaVomitingAbdominalPain], [Q10_RespiratoryIllness], [Q11_NewWorseningCough], [Q12_SoreThroat], [Q13_ShortnessOfBreath], [Q14_LossOfSmell], [Q15_LossOfTaste], [Q16_UnexplainedHemorrhage], [Q17_FatigueMuscleSkinChanges], [Comment], [AttachmentName], [AttachmentPath], [Identify_PutMaskGloves], [Identify_GivePatientMask], [Identify_ContactSupervisor], [Isolate_SingleRoom], [Isolate_SeparatePatient6Feet], [Isolate_PPEEscort], [Isolate_UrinalBedpan], [Isolate_ProviderReview], [Inform_ContactInfectionPrevention], [Inform_RiskAssessment], [Inform_DoNotMovePatient], [CreatedDate], [ModifiedDate], [ModifiedBy], [CreatedBy], @TenantId
FROM [SunCity_Clinics].[InPatient].[InfectiousDiseaseScreening];
SET IDENTITY_INSERT [InPatient].[InfectiousDiseaseScreening] OFF;
GO

PRINT 'Migrating [InPatient].[InitialDischarge]...';
SET IDENTITY_INSERT [InPatient].[InitialDischarge] ON;
INSERT INTO [InPatient].[InitialDischarge] ([ID], [PatientID], [DischargeTypeID], [Date], [Comment], [InitiateDischargeReasonID], [InitiateDischargeTypeID], [Discharged], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [DischargeTypeID], [Date], [Comment], [InitiateDischargeReasonID], [InitiateDischargeTypeID], [Discharged], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[InitialDischarge];
SET IDENTITY_INSERT [InPatient].[InitialDischarge] OFF;
GO

PRINT 'Migrating [InPatient].[InitiateDischargeTypes]...';
SET IDENTITY_INSERT [InPatient].[InitiateDischargeTypes] ON;
INSERT INTO [InPatient].[InitiateDischargeTypes] ([ID], [EnglishName], [ArabicName], [Status], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [EnglishName], [ArabicName], [Status], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[InitiateDischargeTypes];
SET IDENTITY_INSERT [InPatient].[InitiateDischargeTypes] OFF;
GO

PRINT 'Migrating [InPatient].[InpatientSetting]...';
SET IDENTITY_INSERT [InPatient].[InpatientSetting] ON;
INSERT INTO [InPatient].[InpatientSetting] ([ID], [AdmitServiceID], [AdmitPaymentWaitingHours], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ReDepositBalance], [InitialDeposit], [EmployeeId], [OperationTimeslots], [paybeforetakenaservice], [PayDeposit], [Deposit], [Min], [Durationindays], [NumberofFlowUp], [DepositPercentage], [ConsultationServiceID], [ShowSurgicalProceduralConsentForm], [refrealServiceID], [TenantId])
SELECT [ID], [AdmitServiceID], [AdmitPaymentWaitingHours], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ReDepositBalance], [InitialDeposit], [EmployeeId], [OperationTimeslots], [paybeforetakenaservice], [PayDeposit], [Deposit], [Min], [Durationindays], [NumberofFlowUp], [DepositPercentage], [ConsultationServiceID], [ShowSurgicalProceduralConsentForm], [refrealServiceID], @TenantId
FROM [SunCity_Clinics].[InPatient].[InpatientSetting];
SET IDENTITY_INSERT [InPatient].[InpatientSetting] OFF;
GO

PRINT 'Migrating [InPatient].[LinkCostEstimation]...';
SET IDENTITY_INSERT [InPatient].[LinkCostEstimation] ON;
INSERT INTO [InPatient].[LinkCostEstimation] ([Id], [PatientID], [IsLinked], [EstimationNO], [EstimationAmount], [EstimationDays], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [IsLinked], [EstimationNO], [EstimationAmount], [EstimationDays], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[LinkCostEstimation];
SET IDENTITY_INSERT [InPatient].[LinkCostEstimation] OFF;
GO

PRINT 'Migrating [InPatient].[MaintenanceTypes]...';
SET IDENTITY_INSERT [InPatient].[MaintenanceTypes] ON;
INSERT INTO [InPatient].[MaintenanceTypes] ([Id], [TypeCode], [NameArabic], [NameEnglish], [Status], [HospitalCase], [CompanyID], [TenantId])
SELECT [Id], [TypeCode], [NameArabic], [NameEnglish], [Status], [HospitalCase], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[MaintenanceTypes];
SET IDENTITY_INSERT [InPatient].[MaintenanceTypes] OFF;
GO

PRINT 'Migrating [InPatient].[MedicalObservationDental]...';
SET IDENTITY_INSERT [InPatient].[MedicalObservationDental] ON;
INSERT INTO [InPatient].[MedicalObservationDental] ([Id], [MedicalObservationID], [ToothNo], [ServiceID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [insuranceId], [MissingToothReason], [TenantId])
SELECT [Id], [MedicalObservationID], [ToothNo], [ServiceID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [insuranceId], [MissingToothReason], @TenantId
FROM [SunCity_Clinics].[InPatient].[MedicalObservationDental];
SET IDENTITY_INSERT [InPatient].[MedicalObservationDental] OFF;
GO

PRINT 'Migrating [InPatient].[MedicalObservationDepartmentDetails]...';
SET IDENTITY_INSERT [InPatient].[MedicalObservationDepartmentDetails] ON;
INSERT INTO [InPatient].[MedicalObservationDepartmentDetails] ([Id], [BgImgUrl], [SerializedDataObject], [MedicalObservationID], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [TenantId])
SELECT [Id], [BgImgUrl], [SerializedDataObject], [MedicalObservationID], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], @TenantId
FROM [SunCity_Clinics].[InPatient].[MedicalObservationDepartmentDetails];
SET IDENTITY_INSERT [InPatient].[MedicalObservationDepartmentDetails] OFF;
GO

PRINT 'Migrating [InPatient].[MedicalObservationICDCodes]...';
SET IDENTITY_INSERT [InPatient].[MedicalObservationICDCodes] ON;
INSERT INTO [InPatient].[MedicalObservationICDCodes] ([Id], [MedicalObservationID], [ICDCodeID], [Type], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [MedicalObservationID], [ICDCodeID], [Type], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[MedicalObservationICDCodes];
SET IDENTITY_INSERT [InPatient].[MedicalObservationICDCodes] OFF;
GO

PRINT 'Migrating [InPatient].[MedicalObservationOptical]...';
SET IDENTITY_INSERT [InPatient].[MedicalObservationOptical] ON;
INSERT INTO [InPatient].[MedicalObservationOptical] ([Id], [MedicalObservationId], [ServiceID], [ServiceName], [CreatedBy], [CreatedDate], [LensTypeID], [LensTypeName], [insuranceId], [TenantId])
SELECT [Id], [MedicalObservationId], [ServiceID], [ServiceName], [CreatedBy], [CreatedDate], [LensTypeID], [LensTypeName], [insuranceId], @TenantId
FROM [SunCity_Clinics].[InPatient].[MedicalObservationOptical];
SET IDENTITY_INSERT [InPatient].[MedicalObservationOptical] OFF;
GO

PRINT 'Migrating [InPatient].[MedicalObservation]...';
SET IDENTITY_INSERT [InPatient].[MedicalObservation] ON;
INSERT INTO [InPatient].[MedicalObservation] ([Id], [PatientID], [PateintType], [MedicalObservationStatus], [IP_OP], [DoctorID], [MedicalObservationNO], [Complaints], [HistoryOFPresentIllness], [HospitalizationHistory], [IsInsignificant_HospitalizationHistory], [IsInsignificant_PastMedicalHistoryDiseases], [IsInsignificant_PastMedicalHistorySpecialConsiderations], [IsInsignificant_FamilyHistoryDiseases], [IsInsignificant_FamilyHistorySpecialConsiderations], [IsInsignificant_FamilyHistoryPersonalHistory], [IsInsignificant_DrugReactions], [IsInsignificant_CurrMedicationHistory], [FamilyHistoryNotes], [Remarks], [Pulse], [IsRegularPulse], [BP], [Height], [Weight], [TempF], [TempC], [BMI], [SpO2], [GCS], [toothNo], [IsInsignificant_NAD], [IsInsignificant_SystemReview], [SystemReview], [IsInsignificant_LocalExamination], [LocalExamination], [IsInsignificant_Functional], [IsInsignificant_Nutritional], [IsInsignificant_Psychological], [IsInsignificant_SocioEconomic], [IsInsignificant_PainScore], [IsPainManagementDone], [IsInsignificant_AdditionalAssessmente], [AdditionalAssessmente], [ExpectedLengthOfStay], [ExpectedLengthOfStayUnit], [MainLinesOfTreatment], [MeasurableGoalOfImprovement], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SpecialityID], [IsPregnant], [IsLactation], [OperationID], [AnesthesiaDoctorId], [OperationSetupMasterId], [IsOperation], [ActionTaken], [ActionTakenDoctorID], [contor], [dilated], [hernial], [LiverPalp], [LiverRtLobe], [speen], [spleenLTLobe], [SpleenLTLobeComment], [liverRTLobeComment], [RTKidney], [RTKidneyComment], [LTKidney], [LTKidneyComment], [colon], [colonComment], [Ascitls], [AscitlsLevel], [AscitlsComment], [Intestlsound], [lepaticrub], [splenizrub], [LiverLTLobe], [allert], [Oriented], [Memory], [Conscious], [LNPathy], [FlappiesTremor], [clubbing], [cynofis], [jaurdice], [pallor], [Complaints_Psycho], [PrenatalHistory_MaternalInfection], [PrenatalHistory_ExposureToRadiation], [PrenatalHistory_Other], [PrenatalHistory_OtherComment], [NatalHistory_TypesofDelivery], [NatalHistory_Complications], [NatalHistory_ComplicationsComment], [NatalHistory_BirthTruma], [NatalHistory_BirthTrumaComment], [NatalHistory_Milestones], [NatalHistory_MilestonesComment], [NatalHistory_Language], [NatalHistory_Behaviorchildhood_Temper], [NatalHistory_Behaviorchildhood_FeedingHabits], [NatalHistory_Behaviorchildhood_Pica], [NatalHistory_Behaviorchildhood_Tics], [NatalHistory_illnesschildhood_CNS], [NatalHistory_illnesschildhood_Infection], [NatalHistory_illnesschildhood_Epilepsy], [NatalHistory_illnesschildhood_NE], [NatalHistory_illnesschildhood_Encoporesis], [NatalHistory_illnesschildhood_Separationanxiety], [NatalHistory_illnesschildhood_Neuroticdisorders], [NatalHistory_illnesschildhood_EatingProblems], [NatalHistory_Comment], [NatalHistory_Education], [NatalHistory_Adolescence], [NatalHistory_OccupationalHistory], [NatalHistory_SexualHistory], [NatalHistory_MilitaryHistory], [NatalHistory_MaritalHistory], [Treatment_Hospitalization], [Treatment_Psychopharmacology], [Treatment_Psychopharmacology_Comment], [Treatment_Psychotheropy], [Treatment_Psychotheropy_Comment], [Treatment_ECT], [Treatment_ECT_Comment], [Treatment_TMS], [Treatment_TMS_Commrnt], [MentalStatus_Appearance_Cooperative], [MentalStatus_Appearance_Seductive], [MentalStatus_Appearance_Defensive], [MentalStatus_Appearance_Hostile], [MentalStatus_Appearance_Guarded], [MentalStatus_Appearance_Gait], [MentalStatus_Appearance_Mannerism], [MentalStatus_Appearance_Tics], [MentalStatus_Appearance_Retard], [MentalStatus_Appearance_Hyperactive], [MentalStatus_Appearance_Agitated], [MentalStatus_Appearance_Posture], [MentalStatus_Appearance_Grooming], [MentalStatus_Appearance_Healthy], [MentalStatus_Appearance_Frightenend], [MentalStatus_Appearance_Anxicty], [MentalStatus_Appearance_older], [MentalStatus_Appearance_Younger], [MentalStatus_Appearance_PooreyeContact], [MentalStatus_Appearance_FaireyeContact], [MentalStatus_Appearance_NeglectHyg], [MentalStatus_Appearance__FairHyg], [MentalStatus_Appearance__Comment], [MentalStatus_Speech_Rapid], [MentalStatus_Speech_Slow], [MentalStatus_Speech_Pressured], [MentalStatus_Speech_Hesitant], [MentalStatus_Speech_Monotonous], [MentalStatus_Speech_Loud], [MentalStatus_Speech_Whispered], [MentalStatus_Speech_Mumbled], [MentalStatus_Speech_Comment], [MentalStatus_Mood_Depressed], [MentalStatus_Mood_anxious], [MentalStatus_Mood_angry], [MentalStatus_Mood_guilty], [MentalStatus_Mood_anhedonia], [MentalStatus_Mood_alexithymic], [MentalStatus_Mood_Comment], [MentalStatus_Affect_Restricted], [MentalStatus_Affect_Blunted], [MentalStatus_Affect_Flat], [MentalStatus_Affect_Comment], [MentalStatus_Thinking_a_paucity], [MentalStatus_Thinking_a_flight], [MentalStatus_Thinking_a_Rapid], [MentalStatus_Thinking_a_Slow], [MentalStatus_Thinking_a_taneouslt], [MentalStatus_Thinking_a_Question], [MentalStatus_Thinking_a_Directed], [MentalStatus_Thinking_a_Tangentaial], [MentalStatus_Thinking_a_Circumstantial], [MentalStatus_Thinking_b_Delusion], [MentalStatus_Thinking_b_Obsession], [MentalStatus_Thinking_b_Compulsion], [MentalStatus_Thinking_b_Phobias], [MentalStatus_Thinking_b_Suicide], [MentalStatus_Thinking_b_Homicide], [MentalStatus_Thinking_b_Comments], [MentalStatus_Thinking_a_Comments], [MentalStatus_Thinking_a_Block], [MentalStatus_Thinking_a_distractable], [MentalStatus_Thinking_a_Loose], [MentalStatus_Thinking_a_incohorent], [MentalStatus_Thinking_a_wordsalad], [MentalStatus_Thinking_a_neologism], [MentalStatus_Thinking_c_reading], [MentalStatus_Thinking_c_insertion], [MentalStatus_Thinking_c_withdrawal], [MentalStatus_Thinking_c_casting], [MentalStatus_Thinking_c_Comment], [Perception_Comment], [Perception_Illusion], [Perception_Depersonalization], [Perception_Derealization], [Perception_Hallucination], [Sensorium_a_Conscious], [Sensorium_a_Fluctuation], [Sensorium_a_Stupor], [Sensorium_a_Lethargy], [Sensorium_a_FugueState], [Sensorium_a_Coma], [Sensorium_a_Comment], [Sensorium_b_Time], [Sensorium_b_place], [Sensorium_b_Person], [Sensorium_b_Comment], [Sensorium_c_Comment], [Sensorium_c_Subtract], [Sensorium_d_RemoteMemory], [Sensorium_d_RecentMemory], [Sensorium_d_Immediate], [Sensorium_d_Comment], [Sensorium_e_Similarities], [Sensorium_e_Concrete], [Sensorium_e_Abstract], [Sensorium_e_Comment], [Sensorium_f_Completedenial], [Sensorium_f_partial], [Sensorium_f_insight], [Sensorium_f_Comment], [Sensorium_g_judgment], [Sensorium_g_Comment], [NatalHistory_Behaviorchildhood_HighTerror], [Itching], [Itching_Comment], [Itching_Location], [Itching_Duration], [Itching_Freq], [Itching_Character], [Itching_PainScore], [Itching_Radiation], [Itching_WhatModifiesPain], [Itching_OtherComplains], [SimilarCondition1], [PastG6PD], [PastAnomaly], [PastSickle], [FamilyG6PD], [FamilyAnomaly], [FamilySickle], [Family_Historysimilar], [PastPeptic], [Pastthrombosis], [PastPulmonary], [PastThyroid], [PastTuberculosis], [PastHereditary], [FamilyCardiacDeath], [FamilyCancer], [FamilyHereditary], [Pulse_Rate], [Pulse_regular], [Pulse_Rhysim], [Pulse_Volume], [Pulse_Equelity], [Pulse_Charachter], [Pulse_PeriphPulses], [Pulse_PeripheralPerfusion], [Pulse_JVPVolume], [Pulse_JVPWave], [HernialSite], [contourComment], [sound1Comment], [sound2Comment], [sound3Comment], [IsSmoking], [IsSmokingComment], [IsAlcohol], [IsAlcoholComment], [IsDrugs], [IsDrugsComment], [IsDiet], [IsBowels], [IsDietComment], [IsBowelsComment], [IsHandedness], [Menstrual_History_Reg], [LastMenstrualPeriod], [AvarageDuration], [BleedingAmount], [PresentIllness_Onset], [PresentIllness_OnsetComment], [Course], [Duration_Period], [Duration_PeriodType], [History_Stomatitis], [History_StomatitisComment], [History_Halitosis], [History_HalitosisComment], [History_Dyspepsia], [History_DyspepsiaComment], [History_Dysphagia], [History_DysphagiaComment], [History_Odynophagia], [History_OdynophagiaComment], [History_Vomiting], [History_VomitingComment], [History_Vomiting_Nausea], [History_Vomiting_NoAttacks], [History_Vomiting_Freq], [History_Vomiting_Color], [History_Vomiting_Association], [History_Diarrhoea], [History_DiarrhoeaComment], [History_Diarrhoea_Freq], [History_Diarrhoea_Color], [History_Diarrhoea_Blood], [History_Diarrhoea_BloodComment], [History_Diarrhoea_Tenesmus], [History_Diarrhoea_TenesmusComment], [History_Diarrhoea_Association], [History_Constipation], [History_ConstipationComment], [History_Constipation_Freq], [History_Constipation_Consistancy], [History_Constipation_Dyschezia], [History_Constipation_DyscheziaComment], [History_Constipation_Flatulence], [History_Constipation_FlatulenceComment], [History_Constipation_Hema], [History_Constipation_HemaComment], [History_Abdomina], [History_AbdominaComment], [History_Abdomina_Site], [History_Abdomina_Character], [History_Abdomina_increase], [History_Abdomina_Decrease], [History_Abdomina_Referral], [History_Abdomina_Association], [History_Haematemesis], [History_HaematemesisComment], [History_Haematemesis_Color], [History_Haematemesis_Freq], [History_Haematemesis_Association], [IsContraceptive_Method], [Contraceptive_Method], [Jaundice_Yellowish], [Jaundice_Yellowish_Comment], [Jaundice_DarkBrown], [Jaundice_DarkBrown_Comment], [Jaundice_SoftClay], [Jaundice_SoftClay_Comment], [Jaundice_itching], [Jaundice_itching_Comment], [Jaundice_Fever], [Jaundice_Fever_Comment], [Jaundice_AbdominalPain], [Jaundice_AbdominalPain_Comment], [BleedingTendency], [BleedingTendency_Gums], [BleedingTendency_Gums_Comments], [BleedingTendency_Epitaxis], [BleedingTendency_Epitaxis_Comment], [BleedingTendency_Ecchymosis], [BleedingTendency_Ecchymosis_Comment], [EnlargedAbdomen], [EnlargedAbdomen_Site], [EnlargedAbdomen_Site_Comment], [EnlargedAbdomen_Association], [EnlargedAbdomen_Association_Comment], [Encephalopathy], [Encephalopathy_FlappingTremors], [Encephalopathy_FlappingTremors_Comment], [Encephalopathy_Rhythm], [Encephalopathy_Rhythm_Comment], [Encephalopathy_conscious], [Encephalopathy_conscious_Comment], [Encephalopathy_Admitted], [Encephalopathy_Admitted_Comment], [Encephalopathy_Precipitating], [Encephalopathy_Precipitating_Comment], [Toxic], [Toxic_Comment], [Toxic_Appetit], [Toxic_Appetit_Comment], [Toxic_weight], [Toxic_weight_Comment], [Toxic_Fever], [Toxic_Fever_Comment], [Toxic_Sweat], [Toxic_Sweat_Comment], [Contour], [Contour2], [Umbiticus_Comment], [Umbiticus_Site], [Umbiticus_Shape], [Umbiticus_Secretion], [Umbiticus_Vein], [Umbiticus_Hernia], [DilatedVeins], [DilatedVeins_Comment], [DilatedVeins_Site], [DilatedVeins_Direction], [DilatedVeins_Burrowi], [Hernia], [Hernia_Comment], [Hernia2], [DivaricationRecti], [DivaricationRecti_Comment], [Skin_Hair], [Skin_Hair_Comment], [Skin_Pigmentation], [Skin_Pigmentation_Comment], [Skin_Scratch], [Skin_Scratch_Comment], [Skin_Scar], [Skin_Scar_Comment], [Skin_Site], [Skin_Lenght], [Skin_Healing], [Skin_Hernia], [Epigastrium], [Epigastrium_Comment], [Epigastrium_Direction], [Pedal_Oedema], [Pedal_Oedema_Comment], [Venous_Hum], [RenalArteryStenosis], [Venous_HumComment], [RenalArteryStenosis_Comment], [HepaticRubComment], [SplenicRubComment], [RTConsistancy], [RTEdge], [RTPulsation], [RTSize], [RTSurface], [RTTenderness], [LTConsistancy], [LTEdge], [LTPulsation], [LTSize], [LTSurface], [LTTenderness], [SpleenConsistancy], [SpleenEdge], [SpleenPulsation], [SpleenSize], [SpleenSurface], [SpleenTenderness], [PastMedicalComment], [MissingToothReason], [snomedTxt], [snomedCode], [TenantId])
SELECT [Id], [PatientID], [PateintType], [MedicalObservationStatus], [IP_OP], [DoctorID], [MedicalObservationNO], [Complaints], [HistoryOFPresentIllness], [HospitalizationHistory], [IsInsignificant_HospitalizationHistory], [IsInsignificant_PastMedicalHistoryDiseases], [IsInsignificant_PastMedicalHistorySpecialConsiderations], [IsInsignificant_FamilyHistoryDiseases], [IsInsignificant_FamilyHistorySpecialConsiderations], [IsInsignificant_FamilyHistoryPersonalHistory], [IsInsignificant_DrugReactions], [IsInsignificant_CurrMedicationHistory], [FamilyHistoryNotes], [Remarks], [Pulse], [IsRegularPulse], [BP], [Height], [Weight], [TempF], [TempC], [BMI], [SpO2], [GCS], [toothNo], [IsInsignificant_NAD], [IsInsignificant_SystemReview], [SystemReview], [IsInsignificant_LocalExamination], [LocalExamination], [IsInsignificant_Functional], [IsInsignificant_Nutritional], [IsInsignificant_Psychological], [IsInsignificant_SocioEconomic], [IsInsignificant_PainScore], [IsPainManagementDone], [IsInsignificant_AdditionalAssessmente], [AdditionalAssessmente], [ExpectedLengthOfStay], [ExpectedLengthOfStayUnit], [MainLinesOfTreatment], [MeasurableGoalOfImprovement], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SpecialityID], [IsPregnant], [IsLactation], [OperationID], [AnesthesiaDoctorId], [OperationSetupMasterId], [IsOperation], [ActionTaken], [ActionTakenDoctorID], [contor], [dilated], [hernial], [LiverPalp], [LiverRtLobe], [speen], [spleenLTLobe], [SpleenLTLobeComment], [liverRTLobeComment], [RTKidney], [RTKidneyComment], [LTKidney], [LTKidneyComment], [colon], [colonComment], [Ascitls], [AscitlsLevel], [AscitlsComment], [Intestlsound], [lepaticrub], [splenizrub], [LiverLTLobe], [allert], [Oriented], [Memory], [Conscious], [LNPathy], [FlappiesTremor], [clubbing], [cynofis], [jaurdice], [pallor], [Complaints_Psycho], [PrenatalHistory_MaternalInfection], [PrenatalHistory_ExposureToRadiation], [PrenatalHistory_Other], [PrenatalHistory_OtherComment], [NatalHistory_TypesofDelivery], [NatalHistory_Complications], [NatalHistory_ComplicationsComment], [NatalHistory_BirthTruma], [NatalHistory_BirthTrumaComment], [NatalHistory_Milestones], [NatalHistory_MilestonesComment], [NatalHistory_Language], [NatalHistory_Behaviorchildhood_Temper], [NatalHistory_Behaviorchildhood_FeedingHabits], [NatalHistory_Behaviorchildhood_Pica], [NatalHistory_Behaviorchildhood_Tics], [NatalHistory_illnesschildhood_CNS], [NatalHistory_illnesschildhood_Infection], [NatalHistory_illnesschildhood_Epilepsy], [NatalHistory_illnesschildhood_NE], [NatalHistory_illnesschildhood_Encoporesis], [NatalHistory_illnesschildhood_Separationanxiety], [NatalHistory_illnesschildhood_Neuroticdisorders], [NatalHistory_illnesschildhood_EatingProblems], [NatalHistory_Comment], [NatalHistory_Education], [NatalHistory_Adolescence], [NatalHistory_OccupationalHistory], [NatalHistory_SexualHistory], [NatalHistory_MilitaryHistory], [NatalHistory_MaritalHistory], [Treatment_Hospitalization], [Treatment_Psychopharmacology], [Treatment_Psychopharmacology_Comment], [Treatment_Psychotheropy], [Treatment_Psychotheropy_Comment], [Treatment_ECT], [Treatment_ECT_Comment], [Treatment_TMS], [Treatment_TMS_Commrnt], [MentalStatus_Appearance_Cooperative], [MentalStatus_Appearance_Seductive], [MentalStatus_Appearance_Defensive], [MentalStatus_Appearance_Hostile], [MentalStatus_Appearance_Guarded], [MentalStatus_Appearance_Gait], [MentalStatus_Appearance_Mannerism], [MentalStatus_Appearance_Tics], [MentalStatus_Appearance_Retard], [MentalStatus_Appearance_Hyperactive], [MentalStatus_Appearance_Agitated], [MentalStatus_Appearance_Posture], [MentalStatus_Appearance_Grooming], [MentalStatus_Appearance_Healthy], [MentalStatus_Appearance_Frightenend], [MentalStatus_Appearance_Anxicty], [MentalStatus_Appearance_older], [MentalStatus_Appearance_Younger], [MentalStatus_Appearance_PooreyeContact], [MentalStatus_Appearance_FaireyeContact], [MentalStatus_Appearance_NeglectHyg], [MentalStatus_Appearance__FairHyg], [MentalStatus_Appearance__Comment], [MentalStatus_Speech_Rapid], [MentalStatus_Speech_Slow], [MentalStatus_Speech_Pressured], [MentalStatus_Speech_Hesitant], [MentalStatus_Speech_Monotonous], [MentalStatus_Speech_Loud], [MentalStatus_Speech_Whispered], [MentalStatus_Speech_Mumbled], [MentalStatus_Speech_Comment], [MentalStatus_Mood_Depressed], [MentalStatus_Mood_anxious], [MentalStatus_Mood_angry], [MentalStatus_Mood_guilty], [MentalStatus_Mood_anhedonia], [MentalStatus_Mood_alexithymic], [MentalStatus_Mood_Comment], [MentalStatus_Affect_Restricted], [MentalStatus_Affect_Blunted], [MentalStatus_Affect_Flat], [MentalStatus_Affect_Comment], [MentalStatus_Thinking_a_paucity], [MentalStatus_Thinking_a_flight], [MentalStatus_Thinking_a_Rapid], [MentalStatus_Thinking_a_Slow], [MentalStatus_Thinking_a_taneouslt], [MentalStatus_Thinking_a_Question], [MentalStatus_Thinking_a_Directed], [MentalStatus_Thinking_a_Tangentaial], [MentalStatus_Thinking_a_Circumstantial], [MentalStatus_Thinking_b_Delusion], [MentalStatus_Thinking_b_Obsession], [MentalStatus_Thinking_b_Compulsion], [MentalStatus_Thinking_b_Phobias], [MentalStatus_Thinking_b_Suicide], [MentalStatus_Thinking_b_Homicide], [MentalStatus_Thinking_b_Comments], [MentalStatus_Thinking_a_Comments], [MentalStatus_Thinking_a_Block], [MentalStatus_Thinking_a_distractable], [MentalStatus_Thinking_a_Loose], [MentalStatus_Thinking_a_incohorent], [MentalStatus_Thinking_a_wordsalad], [MentalStatus_Thinking_a_neologism], [MentalStatus_Thinking_c_reading], [MentalStatus_Thinking_c_insertion], [MentalStatus_Thinking_c_withdrawal], [MentalStatus_Thinking_c_casting], [MentalStatus_Thinking_c_Comment], [Perception_Comment], [Perception_Illusion], [Perception_Depersonalization], [Perception_Derealization], [Perception_Hallucination], [Sensorium_a_Conscious], [Sensorium_a_Fluctuation], [Sensorium_a_Stupor], [Sensorium_a_Lethargy], [Sensorium_a_FugueState], [Sensorium_a_Coma], [Sensorium_a_Comment], [Sensorium_b_Time], [Sensorium_b_place], [Sensorium_b_Person], [Sensorium_b_Comment], [Sensorium_c_Comment], [Sensorium_c_Subtract], [Sensorium_d_RemoteMemory], [Sensorium_d_RecentMemory], [Sensorium_d_Immediate], [Sensorium_d_Comment], [Sensorium_e_Similarities], [Sensorium_e_Concrete], [Sensorium_e_Abstract], [Sensorium_e_Comment], [Sensorium_f_Completedenial], [Sensorium_f_partial], [Sensorium_f_insight], [Sensorium_f_Comment], [Sensorium_g_judgment], [Sensorium_g_Comment], [NatalHistory_Behaviorchildhood_HighTerror], [Itching], [Itching_Comment], [Itching_Location], [Itching_Duration], [Itching_Freq], [Itching_Character], [Itching_PainScore], [Itching_Radiation], [Itching_WhatModifiesPain], [Itching_OtherComplains], [SimilarCondition1], [PastG6PD], [PastAnomaly], [PastSickle], [FamilyG6PD], [FamilyAnomaly], [FamilySickle], [Family_Historysimilar], [PastPeptic], [Pastthrombosis], [PastPulmonary], [PastThyroid], [PastTuberculosis], [PastHereditary], [FamilyCardiacDeath], [FamilyCancer], [FamilyHereditary], [Pulse_Rate], [Pulse_regular], [Pulse_Rhysim], [Pulse_Volume], [Pulse_Equelity], [Pulse_Charachter], [Pulse_PeriphPulses], [Pulse_PeripheralPerfusion], [Pulse_JVPVolume], [Pulse_JVPWave], [HernialSite], [contourComment], [sound1Comment], [sound2Comment], [sound3Comment], [IsSmoking], [IsSmokingComment], [IsAlcohol], [IsAlcoholComment], [IsDrugs], [IsDrugsComment], [IsDiet], [IsBowels], [IsDietComment], [IsBowelsComment], [IsHandedness], [Menstrual_History_Reg], [LastMenstrualPeriod], [AvarageDuration], [BleedingAmount], [PresentIllness_Onset], [PresentIllness_OnsetComment], [Course], [Duration_Period], [Duration_PeriodType], [History_Stomatitis], [History_StomatitisComment], [History_Halitosis], [History_HalitosisComment], [History_Dyspepsia], [History_DyspepsiaComment], [History_Dysphagia], [History_DysphagiaComment], [History_Odynophagia], [History_OdynophagiaComment], [History_Vomiting], [History_VomitingComment], [History_Vomiting_Nausea], [History_Vomiting_NoAttacks], [History_Vomiting_Freq], [History_Vomiting_Color], [History_Vomiting_Association], [History_Diarrhoea], [History_DiarrhoeaComment], [History_Diarrhoea_Freq], [History_Diarrhoea_Color], [History_Diarrhoea_Blood], [History_Diarrhoea_BloodComment], [History_Diarrhoea_Tenesmus], [History_Diarrhoea_TenesmusComment], [History_Diarrhoea_Association], [History_Constipation], [History_ConstipationComment], [History_Constipation_Freq], [History_Constipation_Consistancy], [History_Constipation_Dyschezia], [History_Constipation_DyscheziaComment], [History_Constipation_Flatulence], [History_Constipation_FlatulenceComment], [History_Constipation_Hema], [History_Constipation_HemaComment], [History_Abdomina], [History_AbdominaComment], [History_Abdomina_Site], [History_Abdomina_Character], [History_Abdomina_increase], [History_Abdomina_Decrease], [History_Abdomina_Referral], [History_Abdomina_Association], [History_Haematemesis], [History_HaematemesisComment], [History_Haematemesis_Color], [History_Haematemesis_Freq], [History_Haematemesis_Association], [IsContraceptive_Method], [Contraceptive_Method], [Jaundice_Yellowish], [Jaundice_Yellowish_Comment], [Jaundice_DarkBrown], [Jaundice_DarkBrown_Comment], [Jaundice_SoftClay], [Jaundice_SoftClay_Comment], [Jaundice_itching], [Jaundice_itching_Comment], [Jaundice_Fever], [Jaundice_Fever_Comment], [Jaundice_AbdominalPain], [Jaundice_AbdominalPain_Comment], [BleedingTendency], [BleedingTendency_Gums], [BleedingTendency_Gums_Comments], [BleedingTendency_Epitaxis], [BleedingTendency_Epitaxis_Comment], [BleedingTendency_Ecchymosis], [BleedingTendency_Ecchymosis_Comment], [EnlargedAbdomen], [EnlargedAbdomen_Site], [EnlargedAbdomen_Site_Comment], [EnlargedAbdomen_Association], [EnlargedAbdomen_Association_Comment], [Encephalopathy], [Encephalopathy_FlappingTremors], [Encephalopathy_FlappingTremors_Comment], [Encephalopathy_Rhythm], [Encephalopathy_Rhythm_Comment], [Encephalopathy_conscious], [Encephalopathy_conscious_Comment], [Encephalopathy_Admitted], [Encephalopathy_Admitted_Comment], [Encephalopathy_Precipitating], [Encephalopathy_Precipitating_Comment], [Toxic], [Toxic_Comment], [Toxic_Appetit], [Toxic_Appetit_Comment], [Toxic_weight], [Toxic_weight_Comment], [Toxic_Fever], [Toxic_Fever_Comment], [Toxic_Sweat], [Toxic_Sweat_Comment], [Contour], [Contour2], [Umbiticus_Comment], [Umbiticus_Site], [Umbiticus_Shape], [Umbiticus_Secretion], [Umbiticus_Vein], [Umbiticus_Hernia], [DilatedVeins], [DilatedVeins_Comment], [DilatedVeins_Site], [DilatedVeins_Direction], [DilatedVeins_Burrowi], [Hernia], [Hernia_Comment], [Hernia2], [DivaricationRecti], [DivaricationRecti_Comment], [Skin_Hair], [Skin_Hair_Comment], [Skin_Pigmentation], [Skin_Pigmentation_Comment], [Skin_Scratch], [Skin_Scratch_Comment], [Skin_Scar], [Skin_Scar_Comment], [Skin_Site], [Skin_Lenght], [Skin_Healing], [Skin_Hernia], [Epigastrium], [Epigastrium_Comment], [Epigastrium_Direction], [Pedal_Oedema], [Pedal_Oedema_Comment], [Venous_Hum], [RenalArteryStenosis], [Venous_HumComment], [RenalArteryStenosis_Comment], [HepaticRubComment], [SplenicRubComment], [RTConsistancy], [RTEdge], [RTPulsation], [RTSize], [RTSurface], [RTTenderness], [LTConsistancy], [LTEdge], [LTPulsation], [LTSize], [LTSurface], [LTTenderness], [SpleenConsistancy], [SpleenEdge], [SpleenPulsation], [SpleenSize], [SpleenSurface], [SpleenTenderness], [PastMedicalComment], [MissingToothReason], [snomedTxt], [snomedCode], @TenantId
FROM [SunCity_Clinics].[InPatient].[MedicalObservation];
SET IDENTITY_INSERT [InPatient].[MedicalObservation] OFF;
GO

PRINT 'Migrating [InPatient].[MedicalObservation_CCU_MICU]...';
SET IDENTITY_INSERT [InPatient].[MedicalObservation_CCU_MICU] ON;
INSERT INTO [InPatient].[MedicalObservation_CCU_MICU] ([Id], [MedicalObservationID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [PrimaryConsultant], [ICUConsultant], [ICUSection], [HospitalAdmissionDate], [FromDate], [ICUAdmissionDate], [TypeOfAdmission], [ProvisionalDiagnosis], [TenantId])
SELECT [Id], [MedicalObservationID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [PrimaryConsultant], [ICUConsultant], [ICUSection], [HospitalAdmissionDate], [FromDate], [ICUAdmissionDate], [TypeOfAdmission], [ProvisionalDiagnosis], @TenantId
FROM [SunCity_Clinics].[InPatient].[MedicalObservation_CCU_MICU];
SET IDENTITY_INSERT [InPatient].[MedicalObservation_CCU_MICU] OFF;
GO

PRINT 'Migrating [InPatient].[MedicalObservation_Git]...';
SET IDENTITY_INSERT [InPatient].[MedicalObservation_Git] ON;
INSERT INTO [InPatient].[MedicalObservation_Git] ([ID], [MedicalObservationID], [Esophagus1], [Stomach1], [Colon1], [Conclusion1], [Recommendet1], [Esophagus2], [Stomach2], [Colon2], [Conclusion2], [Recommendet2], [Enteroscopy3], [Radiology3], [Conclusion3], [Recommendet3], [Enteroscopy4], [Sonosrcphagc4], [Conclusion4], [Recommendet4], [Esophagus5], [Stomach5], [Duodenum5], [Conclusion5], [Recommendet5], [Indication1], [Indication2], [Indication3], [Indication4], [Indication5], [PreMedication1], [PreMedication2], [PreMedication3], [PreMedication4], [PreMedication5], [P_R1], [Report1], [Stomach5_Fundus], [Stomach5_Body], [Stomach5_Pylorous], [RFR_Indecation], [RFR_Premedication], [RFR_siteofablation], [RFR_Frequency], [RFR_Report], [RFR_ReportFile], [LbR_Indecation], [LbR_Premedication], [LbR_txtsiteofbiobsy], [LbR_Typeofneedle], [LbR_Report], [LbR_ReportFile], [smallinteitie], [duodenoscopy], [endooscopy], [CompanyID], [TenantId])
SELECT [ID], [MedicalObservationID], [Esophagus1], [Stomach1], [Colon1], [Conclusion1], [Recommendet1], [Esophagus2], [Stomach2], [Colon2], [Conclusion2], [Recommendet2], [Enteroscopy3], [Radiology3], [Conclusion3], [Recommendet3], [Enteroscopy4], [Sonosrcphagc4], [Conclusion4], [Recommendet4], [Esophagus5], [Stomach5], [Duodenum5], [Conclusion5], [Recommendet5], [Indication1], [Indication2], [Indication3], [Indication4], [Indication5], [PreMedication1], [PreMedication2], [PreMedication3], [PreMedication4], [PreMedication5], [P_R1], [Report1], [Stomach5_Fundus], [Stomach5_Body], [Stomach5_Pylorous], [RFR_Indecation], [RFR_Premedication], [RFR_siteofablation], [RFR_Frequency], [RFR_Report], [RFR_ReportFile], [LbR_Indecation], [LbR_Premedication], [LbR_txtsiteofbiobsy], [LbR_Typeofneedle], [LbR_Report], [LbR_ReportFile], [smallinteitie], [duodenoscopy], [endooscopy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[MedicalObservation_Git];
SET IDENTITY_INSERT [InPatient].[MedicalObservation_Git] OFF;
GO

PRINT 'Migrating [InPatient].[MedicalObservation_MedicationHistory]...';
SET IDENTITY_INSERT [InPatient].[MedicalObservation_MedicationHistory] ON;
INSERT INTO [InPatient].[MedicalObservation_MedicationHistory] ([Id], [MedicalObservationID], [DrugId], [Dosage], [FerquencyID], [ContinueDrugDuringAdmission], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [MedicalObservationID], [DrugId], [Dosage], [FerquencyID], [ContinueDrugDuringAdmission], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[MedicalObservation_MedicationHistory];
SET IDENTITY_INSERT [InPatient].[MedicalObservation_MedicationHistory] OFF;
GO

PRINT 'Migrating [InPatient].[MedicalObservation_Obs_Gyn]...';
SET IDENTITY_INSERT [InPatient].[MedicalObservation_Obs_Gyn] ON;
INSERT INTO [InPatient].[MedicalObservation_Obs_Gyn] ([Id], [MedicalObservationID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [GH_LMP], [GH_PMP], [GH_MenstrualCycle], [GH_TypeofFlow], [GH_Contraception], [GH_OtherContraception], [GH_PapSmearStatus], [GH_PapSmearDate], [GH_PapSmearReport], [GH_HPVVaccinationStatus], [GH_HPVVaccinationDate], [GH_HPVVaccinationReport], [GH_Other], [GH_USGReport], [OH_LMP], [OH_EDD], [OH_EDDBYUSG], [OH_Gravida], [OH_Para], [OH_Abortion], [OH_LiveBirth], [OH_FoetalDeath], [OH_Cause], [OH_Other], [SP_GestationalDiabetes], [SP_DiabetesMellitus], [SP_Hypertension], [SP_Oligohydramnios], [SP_PlacentaPraevia], [SP_MedicalHistory], [SP_Asthma], [SP_SurgicalHistory], [SP_InfertilityTreatment], [SP_Ifyes], [InjectionTetanus1], [InjectionTetanus2], [InjectionTetanus3], [booster], [others], [IsGynecologicalSelected], [IsObstetricHistorySelected], [IsPastObstetricSelected], [IsSignificantHistorySelected], [TenantId])
SELECT [Id], [MedicalObservationID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [GH_LMP], [GH_PMP], [GH_MenstrualCycle], [GH_TypeofFlow], [GH_Contraception], [GH_OtherContraception], [GH_PapSmearStatus], [GH_PapSmearDate], [GH_PapSmearReport], [GH_HPVVaccinationStatus], [GH_HPVVaccinationDate], [GH_HPVVaccinationReport], [GH_Other], [GH_USGReport], [OH_LMP], [OH_EDD], [OH_EDDBYUSG], [OH_Gravida], [OH_Para], [OH_Abortion], [OH_LiveBirth], [OH_FoetalDeath], [OH_Cause], [OH_Other], [SP_GestationalDiabetes], [SP_DiabetesMellitus], [SP_Hypertension], [SP_Oligohydramnios], [SP_PlacentaPraevia], [SP_MedicalHistory], [SP_Asthma], [SP_SurgicalHistory], [SP_InfertilityTreatment], [SP_Ifyes], [InjectionTetanus1], [InjectionTetanus2], [InjectionTetanus3], [booster], [others], [IsGynecologicalSelected], [IsObstetricHistorySelected], [IsPastObstetricSelected], [IsSignificantHistorySelected], @TenantId
FROM [SunCity_Clinics].[InPatient].[MedicalObservation_Obs_Gyn];
SET IDENTITY_INSERT [InPatient].[MedicalObservation_Obs_Gyn] OFF;
GO

PRINT 'Migrating [InPatient].[MedicalObservation_Paediatrics]...';
SET IDENTITY_INSERT [InPatient].[MedicalObservation_Paediatrics] ON;
INSERT INTO [InPatient].[MedicalObservation_Paediatrics] ([Id], [MedicalObservationID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [MedicalObservationID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[MedicalObservation_Paediatrics];
SET IDENTITY_INSERT [InPatient].[MedicalObservation_Paediatrics] OFF;
GO

PRINT 'Migrating [InPatient].[MedicalObservation_PastHistory]...';
SET IDENTITY_INSERT [InPatient].[MedicalObservation_PastHistory] ON;
INSERT INTO [InPatient].[MedicalObservation_PastHistory] ([Id], [POA], [No], [Mode], [Sex], [Weight], [Age], [Remarks], [MedicalObservationobsid], [TenantId])
SELECT [Id], [POA], [No], [Mode], [Sex], [Weight], [Age], [Remarks], [MedicalObservationobsid], @TenantId
FROM [SunCity_Clinics].[InPatient].[MedicalObservation_PastHistory];
SET IDENTITY_INSERT [InPatient].[MedicalObservation_PastHistory] OFF;
GO

PRINT 'Migrating [InPatient].[MedicalObservation_Plastic]...';
SET IDENTITY_INSERT [InPatient].[MedicalObservation_Plastic] ON;
INSERT INTO [InPatient].[MedicalObservation_Plastic] ([id], [MedicalObservationID], [ImageUrl], [UploadDate], [Comment], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [id], [MedicalObservationID], [ImageUrl], [UploadDate], [Comment], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[MedicalObservation_Plastic];
SET IDENTITY_INSERT [InPatient].[MedicalObservation_Plastic] OFF;
GO

PRINT 'Migrating [InPatient].[MedicalObservation_TagsValues]...';
SET IDENTITY_INSERT [InPatient].[MedicalObservation_TagsValues] ON;
INSERT INTO [InPatient].[MedicalObservation_TagsValues] ([Id], [Value1], [Value2], [Value3], [Value4], [MedicalObservationId], [PageTagsId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Value1], [Value2], [Value3], [Value4], [MedicalObservationId], [PageTagsId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[MedicalObservation_TagsValues];
SET IDENTITY_INSERT [InPatient].[MedicalObservation_TagsValues] OFF;
GO

PRINT 'Migrating [InPatient].[MovingPatient]...';
SET IDENTITY_INSERT [InPatient].[MovingPatient] ON;
INSERT INTO [InPatient].[MovingPatient] ([Id], [WardId], [ToCostCenterId], [IsOut], [PateintId], [Remark], [MoveDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [WardId], [ToCostCenterId], [IsOut], [PateintId], [Remark], [MoveDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[MovingPatient];
SET IDENTITY_INSERT [InPatient].[MovingPatient] OFF;
GO

PRINT 'Migrating [InPatient].[MyocardialPerfusionImaging]...';
SET IDENTITY_INSERT [InPatient].[MyocardialPerfusionImaging] ON;
INSERT INTO [InPatient].[MyocardialPerfusionImaging] ([Id], [PatientID], [Date], [Description], [Stress], [IMPRESSION], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [Date], [Description], [Stress], [IMPRESSION], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[MyocardialPerfusionImaging];
SET IDENTITY_INSERT [InPatient].[MyocardialPerfusionImaging] OFF;
GO

PRINT 'Migrating [InPatient].[NewBorn]...';
SET IDENTITY_INSERT [InPatient].[NewBorn] ON;
INSERT INTO [InPatient].[NewBorn] ([ID], [MRN], [BabyNameEn], [BabyNameAr], [MotherID], [FatherID], [FatherFullNameArabic], [FatherFullNameEnglish], [FatherReligionID], [FatherNationalNumber], [FatherIDTypeID], [FatherNationalityID], [FatherJobID], [FatherAddress], [IssueDate], [IssueAddress], [TimeOfBirth], [BabyGenderID], [BloodGroupID], [BloodGroupTypeID], [AttendingDoctorID], [BabyDoctorID], [BabyHeight], [BabyHeightUnitID], [BabyWeight], [BabyWeightUnitID], [BabyAlive], [Twins], [NumberOfTwins], [BabyBirthDate], [SquenceNumber], [BabyAccommTypeID], [BabyWardID], [BabyRoomID], [BabyBedID], [CompanyID], [FatherJob], [PatID], [TenantId])
SELECT [ID], [MRN], [BabyNameEn], [BabyNameAr], [MotherID], [FatherID], [FatherFullNameArabic], [FatherFullNameEnglish], [FatherReligionID], [FatherNationalNumber], [FatherIDTypeID], [FatherNationalityID], [FatherJobID], [FatherAddress], [IssueDate], [IssueAddress], [TimeOfBirth], [BabyGenderID], [BloodGroupID], [BloodGroupTypeID], [AttendingDoctorID], [BabyDoctorID], [BabyHeight], [BabyHeightUnitID], [BabyWeight], [BabyWeightUnitID], [BabyAlive], [Twins], [NumberOfTwins], [BabyBirthDate], [SquenceNumber], [BabyAccommTypeID], [BabyWardID], [BabyRoomID], [BabyBedID], [CompanyID], [FatherJob], [PatID], @TenantId
FROM [SunCity_Clinics].[InPatient].[NewBorn];
SET IDENTITY_INSERT [InPatient].[NewBorn] OFF;
GO

PRINT 'Migrating [InPatient].[NurseAssessmentHeader]...';
SET IDENTITY_INSERT [InPatient].[NurseAssessmentHeader] ON;
INSERT INTO [InPatient].[NurseAssessmentHeader] ([Id], [PatientId], [Code], [NurseId], [CareModeId], [Date], [Time], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientId], [Code], [NurseId], [CareModeId], [Date], [Time], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[NurseAssessmentHeader];
SET IDENTITY_INSERT [InPatient].[NurseAssessmentHeader] OFF;
GO

PRINT 'Migrating [InPatient].[NurseAssessment_TagsValues]...';
SET IDENTITY_INSERT [InPatient].[NurseAssessment_TagsValues] ON;
INSERT INTO [InPatient].[NurseAssessment_TagsValues] ([Id], [Value1], [Value2], [GeneralHeaderId], [PageTagsId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Value1], [Value2], [GeneralHeaderId], [PageTagsId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[NurseAssessment_TagsValues];
SET IDENTITY_INSERT [InPatient].[NurseAssessment_TagsValues] OFF;
GO

PRINT 'Migrating [InPatient].[NurseStation_Drug]...';
SET IDENTITY_INSERT [InPatient].[NurseStation_Drug] ON;
INSERT INTO [InPatient].[NurseStation_Drug] ([Id], [PatientId], [drugID], [ReceivedQty], [DispensedQty], [OPIPNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SourceOrderDetailID], [DestinationOrderDetailID], [pharmacist], [DispensedPharmacy], [ExecutionBy], [TenantId])
SELECT [Id], [PatientId], [drugID], [ReceivedQty], [DispensedQty], [OPIPNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SourceOrderDetailID], [DestinationOrderDetailID], [pharmacist], [DispensedPharmacy], [ExecutionBy], @TenantId
FROM [SunCity_Clinics].[InPatient].[NurseStation_Drug];
SET IDENTITY_INSERT [InPatient].[NurseStation_Drug] OFF;
GO

PRINT 'Migrating [InPatient].[NursingAdmissionAssessment]...';
SET IDENTITY_INSERT [InPatient].[NursingAdmissionAssessment] ON;
INSERT INTO [InPatient].[NursingAdmissionAssessment] ([NursingAdmissionAssessmentID], [ChiefComplaint], [UrgentNeeds], [OrientedTo], [Valuables], [Clothing], [OwnMedication], [EyeGlasses], [Dentures], [OtherAids], [BriefHistoryOfChiefComplaintAndReasonForAdmission], [PreviousMajorRelated_IlnessOrSurgery], [Allergiesto], [DoctorID], [PainScreening], [NumericPainScale], [PainAssessment], [GeneralAppearance], [GeneralAppearanceDescription], [MentalStatus], [Anxious_relatedto], [Useeyeglassesfor], [Head_Ent], [Respiratory], [RespiratoryReferralInitiated], [Neuro_Muscular_Skeletal], [RespiratoryRemark], [LevelofConsciousnessis], [Cardiovascular], [Edemaof], [Ivfluid], [CardiovascularSite], [CardiovascularRate], [CardiovascularReferralInitiated], [CardiovascularRemarks], [Neuro_Muscular_SkeletalReferralInitiated], [Neuro_Muscular_Skeletal_Remark], [SkinAndHair], [SkinAndHairReferralInitiated], [SkinAndHair_Remark], [NutritionalAssessment], [Special_Diet], [Vitamin_Or_Mineral_Supplement], [Genitourinary], [Last_menstrual_period], [Gravida], [Para], [Abortion], [Gestational_diabetes], [GenitourinaryReferralInitiated], [GenitourinaryRemark], [Genitourinary_Frequency_Tiems], [Genitourinary_Frequency_hr], [Genitourinary_Frequency_day], [SPECIAL_ACTIVITIES_OF_DAILY_LIVING_ASSISTANCE], [SPECIAL_ACTIVITIES_ReferralInitiated], [SPECIAL_ACTIVITIES_Remarks], [SPECIAL_ACTIVITIES_OtherSpecify], [SocialAssessment], [SocialAssessment_ReferralInitiated], [SocialAssessment_Remark], [OTHER_OBSERVATIONS_FINDINGS_ACTION_TAKEN], [Signature], [Date], [NAME_OF_ADMITTING_NURSE], [Time_Notified], [Mode_Of_Admission], [PreviousMajorRelated_IlnessOrSurgery_OtherDisease], [Emergency_Admission], [Direct_Admission], [ACCOMPANIED_BY], [Primary_Language], [English], [Temp], [Pulse], [Resp], [Bp], [Ht], [Wt], [PatientID], [HearingDeficit], [VisionDefect], [CreatedDate], [CreatedBy], [CompanyID], [TenantId])
SELECT [NursingAdmissionAssessmentID], [ChiefComplaint], [UrgentNeeds], [OrientedTo], [Valuables], [Clothing], [OwnMedication], [EyeGlasses], [Dentures], [OtherAids], [BriefHistoryOfChiefComplaintAndReasonForAdmission], [PreviousMajorRelated_IlnessOrSurgery], [Allergiesto], [DoctorID], [PainScreening], [NumericPainScale], [PainAssessment], [GeneralAppearance], [GeneralAppearanceDescription], [MentalStatus], [Anxious_relatedto], [Useeyeglassesfor], [Head_Ent], [Respiratory], [RespiratoryReferralInitiated], [Neuro_Muscular_Skeletal], [RespiratoryRemark], [LevelofConsciousnessis], [Cardiovascular], [Edemaof], [Ivfluid], [CardiovascularSite], [CardiovascularRate], [CardiovascularReferralInitiated], [CardiovascularRemarks], [Neuro_Muscular_SkeletalReferralInitiated], [Neuro_Muscular_Skeletal_Remark], [SkinAndHair], [SkinAndHairReferralInitiated], [SkinAndHair_Remark], [NutritionalAssessment], [Special_Diet], [Vitamin_Or_Mineral_Supplement], [Genitourinary], [Last_menstrual_period], [Gravida], [Para], [Abortion], [Gestational_diabetes], [GenitourinaryReferralInitiated], [GenitourinaryRemark], [Genitourinary_Frequency_Tiems], [Genitourinary_Frequency_hr], [Genitourinary_Frequency_day], [SPECIAL_ACTIVITIES_OF_DAILY_LIVING_ASSISTANCE], [SPECIAL_ACTIVITIES_ReferralInitiated], [SPECIAL_ACTIVITIES_Remarks], [SPECIAL_ACTIVITIES_OtherSpecify], [SocialAssessment], [SocialAssessment_ReferralInitiated], [SocialAssessment_Remark], [OTHER_OBSERVATIONS_FINDINGS_ACTION_TAKEN], [Signature], [Date], [NAME_OF_ADMITTING_NURSE], [Time_Notified], [Mode_Of_Admission], [PreviousMajorRelated_IlnessOrSurgery_OtherDisease], [Emergency_Admission], [Direct_Admission], [ACCOMPANIED_BY], [Primary_Language], [English], [Temp], [Pulse], [Resp], [Bp], [Ht], [Wt], [PatientID], [HearingDeficit], [VisionDefect], [CreatedDate], [CreatedBy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[NursingAdmissionAssessment];
SET IDENTITY_INSERT [InPatient].[NursingAdmissionAssessment] OFF;
GO

PRINT 'Migrating [InPatient].[NursingNote]...';
SET IDENTITY_INSERT [InPatient].[NursingNote] ON;
INSERT INTO [InPatient].[NursingNote] ([Id], [EnteredBy], [NursingNotes], [EntryDate], [EntryTime], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [PatientId], [CompanyID], [TenantId])
SELECT [Id], [EnteredBy], [NursingNotes], [EntryDate], [EntryTime], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [PatientId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[NursingNote];
SET IDENTITY_INSERT [InPatient].[NursingNote] OFF;
GO

PRINT 'Migrating [InPatient].[OCAF]...';
SET IDENTITY_INSERT [InPatient].[OCAF] ON;
INSERT INTO [InPatient].[OCAF] ([Id], [Patient], [PlanType], [Bifocal], [Vertex], [Bifocal1], [Glass], [Plastic], [None], [Multi_coated], [Medium], [Coating], [Varilux], [Lenticular], [Photosensitive], [Light], [Vision], [hIndex], [Aspheric], [Dark], [Colored], [chkBifocal], [Thickness], [Anti_Scratch], [Permanent], [Disposable], [Frames], [SpecifyOfPairs], [txtLensesSR], [txtFrameSR], [txtPhysicianSignature], [txtDate], [txtNameRelationship], [txtSignature], [txtDateTime], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IPOP], [TenantId])
SELECT [Id], [Patient], [PlanType], [Bifocal], [Vertex], [Bifocal1], [Glass], [Plastic], [None], [Multi_coated], [Medium], [Coating], [Varilux], [Lenticular], [Photosensitive], [Light], [Vision], [hIndex], [Aspheric], [Dark], [Colored], [chkBifocal], [Thickness], [Anti_Scratch], [Permanent], [Disposable], [Frames], [SpecifyOfPairs], [txtLensesSR], [txtFrameSR], [txtPhysicianSignature], [txtDate], [txtNameRelationship], [txtSignature], [txtDateTime], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IPOP], @TenantId
FROM [SunCity_Clinics].[InPatient].[OCAF];
SET IDENTITY_INSERT [InPatient].[OCAF] OFF;
GO

PRINT 'Migrating [InPatient].[OperationRequest]...';
SET IDENTITY_INSERT [InPatient].[OperationRequest] ON;
INSERT INTO [InPatient].[OperationRequest] ([Id], [DiagnosisId], [ProcedureId], [OperationWardId], [OperationRoomId], [OperationDate], [Comment], [DepId], [PatientId], [EmergencyUnitID], [CompanyID], [TenantId])
SELECT [Id], [DiagnosisId], [ProcedureId], [OperationWardId], [OperationRoomId], [OperationDate], [Comment], [DepId], [PatientId], [EmergencyUnitID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[OperationRequest];
SET IDENTITY_INSERT [InPatient].[OperationRequest] OFF;
GO

PRINT 'Migrating [InPatient].[OperationRoom]...';
SET IDENTITY_INSERT [InPatient].[OperationRoom] ON;
INSERT INTO [InPatient].[OperationRoom] ([ID], [Code], [WardID], [Name], [CompanyID], [TenantId])
SELECT [ID], [Code], [WardID], [Name], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[OperationRoom];
SET IDENTITY_INSERT [InPatient].[OperationRoom] OFF;
GO

PRINT 'Migrating [InPatient].[OperationTheatreDailyDuty]...';
SET IDENTITY_INSERT [InPatient].[OperationTheatreDailyDuty] ON;
INSERT INTO [InPatient].[OperationTheatreDailyDuty] ([Id], [OperationTheatreId], [CostCenterId], [SessionId], [DutyDate], [FromTime], [ToTime], [CompanyID], [TenantId])
SELECT [Id], [OperationTheatreId], [CostCenterId], [SessionId], [DutyDate], [FromTime], [ToTime], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[OperationTheatreDailyDuty];
SET IDENTITY_INSERT [InPatient].[OperationTheatreDailyDuty] OFF;
GO

PRINT 'Migrating [InPatient].[OperationTheatre]...';
SET IDENTITY_INSERT [InPatient].[OperationTheatre] ON;
INSERT INTO [InPatient].[OperationTheatre] ([Id], [Name], [CompanyID], [TenantId])
SELECT [Id], [Name], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[OperationTheatre];
SET IDENTITY_INSERT [InPatient].[OperationTheatre] OFF;
GO

PRINT 'Migrating [InPatient].[OperationWard]...';
SET IDENTITY_INSERT [InPatient].[OperationWard] ON;
INSERT INTO [InPatient].[OperationWard] ([ID], [Code], [FloorID], [Name], [CompanyID], [TenantId])
SELECT [ID], [Code], [FloorID], [Name], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[OperationWard];
SET IDENTITY_INSERT [InPatient].[OperationWard] OFF;
GO

PRINT 'Migrating [InPatient].[OrderCategoryDetail]...';
SET IDENTITY_INSERT [InPatient].[OrderCategoryDetail] ON;
INSERT INTO [InPatient].[OrderCategoryDetail] ([Id], [OrderCategory_Id], [Service_Id], [CompanyID], [TenantId])
SELECT [Id], [OrderCategory_Id], [Service_Id], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[OrderCategoryDetail];
SET IDENTITY_INSERT [InPatient].[OrderCategoryDetail] OFF;
GO

PRINT 'Migrating [InPatient].[OrderCategoryMaster]...';
SET IDENTITY_INSERT [InPatient].[OrderCategoryMaster] ON;
INSERT INTO [InPatient].[OrderCategoryMaster] ([ID], [OrderTypeID], [ArabicNameDescription], [EnglishNameDescription], [Status], [CompanyID], [TenantId])
SELECT [ID], [OrderTypeID], [ArabicNameDescription], [EnglishNameDescription], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[OrderCategoryMaster];
SET IDENTITY_INSERT [InPatient].[OrderCategoryMaster] OFF;
GO

PRINT 'Migrating [InPatient].[OrderMemberShip]...';
SET IDENTITY_INSERT [InPatient].[OrderMemberShip] ON;
INSERT INTO [InPatient].[OrderMemberShip] ([Id], [UserID], [OrderTypeId], [VW_Order_vertified], [VW_Order_Unvertified], [VW_Order_Completed], [VW_Order_Resulted], [VW_Order_Confirmed], [Exc_Order_vertified], [Exc_Order_Unvertified], [Exc_Order_Completed], [Exc_Order_Resulted], [Exc_Order_Confirmed], [Cncl_Order_vertified], [Cncl_Order_Unvertified], [Cncl_Order_Completed], [Cncl_Order_Resulted], [Cncl_Order_Confirmed], [CompanyID], [TenantId])
SELECT [Id], [UserID], [OrderTypeId], [VW_Order_vertified], [VW_Order_Unvertified], [VW_Order_Completed], [VW_Order_Resulted], [VW_Order_Confirmed], [Exc_Order_vertified], [Exc_Order_Unvertified], [Exc_Order_Completed], [Exc_Order_Resulted], [Exc_Order_Confirmed], [Cncl_Order_vertified], [Cncl_Order_Unvertified], [Cncl_Order_Completed], [Cncl_Order_Resulted], [Cncl_Order_Confirmed], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[OrderMemberShip];
SET IDENTITY_INSERT [InPatient].[OrderMemberShip] OFF;
GO

PRINT 'Migrating [InPatient].[OrderParameter]...';
SET IDENTITY_INSERT [InPatient].[OrderParameter] ON;
INSERT INTO [InPatient].[OrderParameter] ([ID], [Code], [Status], [TenantId])
SELECT [ID], [Code], [Status], @TenantId
FROM [SunCity_Clinics].[InPatient].[OrderParameter];
SET IDENTITY_INSERT [InPatient].[OrderParameter] OFF;
GO

PRINT 'Migrating [InPatient].[OrderType]...';
SET IDENTITY_INSERT [InPatient].[OrderType] ON;
INSERT INTO [InPatient].[OrderType] ([ID], [ArabicNameDescription], [EnglishNameDescription], [TypeClass], [OrderSheetType], [InventoryID], [ExpirationDuration], [Status], [CompanyID], [TenantId])
SELECT [ID], [ArabicNameDescription], [EnglishNameDescription], [TypeClass], [OrderSheetType], [InventoryID], [ExpirationDuration], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[OrderType];
SET IDENTITY_INSERT [InPatient].[OrderType] OFF;
GO

PRINT 'Migrating [InPatient].[OrdersEntery]...';
SET IDENTITY_INSERT [InPatient].[OrdersEntery] ON;
INSERT INTO [InPatient].[OrdersEntery] ([ID], [PatientID], [OrderTypeID], [OrderCategoryID], [OrderItemID], [Quantity], [FrequencyMasterID], [Eurgent], [Comment], [Status], [DurationNumber], [DurationType], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [OrderTypeID], [OrderCategoryID], [OrderItemID], [Quantity], [FrequencyMasterID], [Eurgent], [Comment], [Status], [DurationNumber], [DurationType], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[OrdersEntery];
SET IDENTITY_INSERT [InPatient].[OrdersEntery] OFF;
GO

PRINT 'Migrating [InPatient].[OutResourceType]...';
SET IDENTITY_INSERT [InPatient].[OutResourceType] ON;
INSERT INTO [InPatient].[OutResourceType] ([Id], [TypeCode], [NameArabic], [NameEnglish], [Status], [HospitalCase], [CompanyID], [TenantId])
SELECT [Id], [TypeCode], [NameArabic], [NameEnglish], [Status], [HospitalCase], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[OutResourceType];
SET IDENTITY_INSERT [InPatient].[OutResourceType] OFF;
GO

PRINT 'Migrating [InPatient].[PageTags]...';
SET IDENTITY_INSERT [InPatient].[PageTags] ON;
INSERT INTO [InPatient].[PageTags] ([Id], [PageTagsGroupEnumValue], [TagId], [PageEnumValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PageTagsGroupEnumValue], [TagId], [PageEnumValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[PageTags];
SET IDENTITY_INSERT [InPatient].[PageTags] OFF;
GO

PRINT 'Migrating [InPatient].[PatientAllergyNew]...';
SET IDENTITY_INSERT [InPatient].[PatientAllergyNew] ON;
INSERT INTO [InPatient].[PatientAllergyNew] ([Id], [AllergyType], [genericID], [Comment], [PatientId], [CreatedBy], [CreatedDate], [DrugID], [CompanyID], [TenantId])
SELECT [Id], [AllergyType], [genericID], [Comment], [PatientId], [CreatedBy], [CreatedDate], [DrugID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[PatientAllergyNew];
SET IDENTITY_INSERT [InPatient].[PatientAllergyNew] OFF;
GO

PRINT 'Migrating [InPatient].[PatientDietManagement]...';
SET IDENTITY_INSERT [InPatient].[PatientDietManagement] ON;
INSERT INTO [InPatient].[PatientDietManagement] ([Id], [PatientDietId], [BreakfastTime], [LunchTime], [DinnerTime], [ModifiedBy], [ModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientDietId], [BreakfastTime], [LunchTime], [DinnerTime], [ModifiedBy], [ModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[PatientDietManagement];
SET IDENTITY_INSERT [InPatient].[PatientDietManagement] OFF;
GO

PRINT 'Migrating [InPatient].[PatientDiet]...';
SET IDENTITY_INSERT [InPatient].[PatientDiet] ON;
INSERT INTO [InPatient].[PatientDiet] ([Id], [PatientId], [DietId], [PNumber], [ModifiedBy], [ModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientId], [DietId], [PNumber], [ModifiedBy], [ModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[PatientDiet];
SET IDENTITY_INSERT [InPatient].[PatientDiet] OFF;
GO

PRINT 'Migrating [InPatient].[PatientDischarge]...';
SET IDENTITY_INSERT [InPatient].[PatientDischarge] ON;
INSERT INTO [InPatient].[PatientDischarge] ([ID], [PatientId], [DisChDoctorID], [DisChargeTypeId], [DisChDiagnosisId], [Comment], [DeathDate], [DeathTime], [EmergencyUnitID], [CreationDate], [DischargeReasonID], [Active], [DischargDate], [DischargeOrderID], [HomeMed], [BedClear], [Possisions], [IPNo], [SecurityDischargeTime], [CompanyID], [BranchId], [TenantId])
SELECT [ID], [PatientId], [DisChDoctorID], [DisChargeTypeId], [DisChDiagnosisId], [Comment], [DeathDate], [DeathTime], [EmergencyUnitID], [CreationDate], [DischargeReasonID], [Active], [DischargDate], [DischargeOrderID], [HomeMed], [BedClear], [Possisions], [IPNo], [SecurityDischargeTime], [CompanyID], [BranchId], @TenantId
FROM [SunCity_Clinics].[InPatient].[PatientDischarge];
SET IDENTITY_INSERT [InPatient].[PatientDischarge] OFF;
GO

PRINT 'Migrating [InPatient].[PatientFamilyEducation]...';
SET IDENTITY_INSERT [InPatient].[PatientFamilyEducation] ON;
INSERT INTO [InPatient].[PatientFamilyEducation] ([Id], [PatientId], [DateofAssessment], [Time], [Dept], [Educationgivento], [Relationship], [LiteracyLevel], [Willingness], [PrimaryLanguage], [UnderstoodLanguage], [A_None], [A_Anxiety_Fear], [A_LanguageBarrier], [A_Denial], [A_Sensory_deﬁcit], [A_BeliefsandValues], [A_Literacy], [A_CulturalPractice], [A_PhysicalImpairment], [A_Pain_Discomfort], [A_Emotional], [A_Cognitive_impairment], [A_Lackofconﬁdence], [A_Financial_Problems], [A_Others], [A_Others_text], [I_None], [I_Obtaintranslator], [I_TeachFamily], [I_Respectvalues], [I_Review_Repeat], [I_Reassurance], [I_RespectCultural], [I_Appropritatesubstitution], [I_Others], [I_Others_text], [B_Diagnosis], [B_Treatment], [B_Pain], [B_Self_care], [B_regimen], [B_Discharge], [B_hand], [B_stoma], [B_Injection], [B_Dietary], [B_Tube], [B_Rehabilitation], [B_Antenatal], [B_urinary], [B_SafeEffective], [B_tracheotomy], [B_Implants], [B_Coping], [TM_lecture], [TM_Demonstration], [TM_Discussion], [TM_Audio], [TM_Model], [TM_Verbal], [OPIP], [CompanyID], [TenantId])
SELECT [Id], [PatientId], [DateofAssessment], [Time], [Dept], [Educationgivento], [Relationship], [LiteracyLevel], [Willingness], [PrimaryLanguage], [UnderstoodLanguage], [A_None], [A_Anxiety_Fear], [A_LanguageBarrier], [A_Denial], [A_Sensory_deﬁcit], [A_BeliefsandValues], [A_Literacy], [A_CulturalPractice], [A_PhysicalImpairment], [A_Pain_Discomfort], [A_Emotional], [A_Cognitive_impairment], [A_Lackofconﬁdence], [A_Financial_Problems], [A_Others], [A_Others_text], [I_None], [I_Obtaintranslator], [I_TeachFamily], [I_Respectvalues], [I_Review_Repeat], [I_Reassurance], [I_RespectCultural], [I_Appropritatesubstitution], [I_Others], [I_Others_text], [B_Diagnosis], [B_Treatment], [B_Pain], [B_Self_care], [B_regimen], [B_Discharge], [B_hand], [B_stoma], [B_Injection], [B_Dietary], [B_Tube], [B_Rehabilitation], [B_Antenatal], [B_urinary], [B_SafeEffective], [B_tracheotomy], [B_Implants], [B_Coping], [TM_lecture], [TM_Demonstration], [TM_Discussion], [TM_Audio], [TM_Model], [TM_Verbal], [OPIP], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[PatientFamilyEducation];
SET IDENTITY_INSERT [InPatient].[PatientFamilyEducation] OFF;
GO

PRINT 'Migrating [InPatient].[PatientOrderDetail]...';
SET IDENTITY_INSERT [InPatient].[PatientOrderDetail] ON;
INSERT INTO [InPatient].[PatientOrderDetail] ([ID], [PatientOrderMasterId], [ServiceId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Status], [Qty], [DrugID], [GenericID], [DoseUnitID], [DrugForm], [Strength], [PrescriptionsDetailsID], [OrderTypeId], [Location], [FrequencyID], [StartDate], [StartTime], [VerificationFromUserId], [VerificationToNurseId], [VerificationDateFrom], [VerificationDateTo], [isVerified], [ExecutionBy], [IsPaused], [IsSkipped], [SkipInstructionID], [SkipReason], [SkipDate], [InsuranceId], [RequestSuppliesDetailID], [Amount], [ServiceGroupName], [Comments], [TenantId])
SELECT [ID], [PatientOrderMasterId], [ServiceId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Status], [Qty], [DrugID], [GenericID], [DoseUnitID], [DrugForm], [Strength], [PrescriptionsDetailsID], [OrderTypeId], [Location], [FrequencyID], [StartDate], [StartTime], [VerificationFromUserId], [VerificationToNurseId], [VerificationDateFrom], [VerificationDateTo], [isVerified], [ExecutionBy], [IsPaused], [IsSkipped], [SkipInstructionID], [SkipReason], [SkipDate], [InsuranceId], [RequestSuppliesDetailID], [Amount], [ServiceGroupName], [Comments], @TenantId
FROM [SunCity_Clinics].[InPatient].[PatientOrderDetail];
SET IDENTITY_INSERT [InPatient].[PatientOrderDetail] OFF;
GO

PRINT 'Migrating [InPatient].[PatientOrderMaster]...';
SET IDENTITY_INSERT [InPatient].[PatientOrderMaster] ON;
INSERT INTO [InPatient].[PatientOrderMaster] ([ID], [PatientId], [DoctorId], [OrderDate], [Period], [PeriodType], [RepetTypeId], [OrderCategId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OrderTarget], [OPIPNo], [ISMedicine], [Priority], [IsEditable], [OrderStatus], [Code], [FrequencyID], [StartDate], [StartTime], [TransferJustification], [TransferUserId], [IsOperation], [BranchId], [TenantId])
SELECT [ID], [PatientId], [DoctorId], [OrderDate], [Period], [PeriodType], [RepetTypeId], [OrderCategId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OrderTarget], [OPIPNo], [ISMedicine], [Priority], [IsEditable], [OrderStatus], [Code], [FrequencyID], [StartDate], [StartTime], [TransferJustification], [TransferUserId], [IsOperation], [BranchId], @TenantId
FROM [SunCity_Clinics].[InPatient].[PatientOrderMaster];
SET IDENTITY_INSERT [InPatient].[PatientOrderMaster] OFF;
GO

PRINT 'Migrating [InPatient].[PatientType]...';
SET IDENTITY_INSERT [InPatient].[PatientType] ON;
INSERT INTO [InPatient].[PatientType] ([Id], [TypeCode], [NameArabic], [NameEnglish], [Status], [HospitalCase], [TenantId])
SELECT [Id], [TypeCode], [NameArabic], [NameEnglish], [Status], [HospitalCase], @TenantId
FROM [SunCity_Clinics].[InPatient].[PatientType];
SET IDENTITY_INSERT [InPatient].[PatientType] OFF;
GO

PRINT 'Migrating [InPatient].[Patient_Family]...';
SET IDENTITY_INSERT [InPatient].[Patient_Family] ON;
INSERT INTO [InPatient].[Patient_Family] ([ID], [PF_Id], [confirmeddiagnosis_understood], [confirmeddiagnosis_report], [safeeffective_understood], [safeeffective_report], [druginteraction_understood], [druginteraction_report], [drugfood_understood], [drugfood_report], [Nutrition_understood], [Nutrition_report], [Nutrition_explained], [safeequipment_understood], [safeequipment_report], [safeequipment_explained], [Painmanagement_understood], [Painmanagement_report], [rehabilitationtechnique_understood], [rehabilitationtechnique_report], [rehabilitationtechnique_explained], [dischargeplanhome_understood], [dischargeplanhome_report], [dischargeplanfollowup_understood], [dischargeplanfollowup_report], [preventivemeasureinfection_understood], [preventivemeasureinfection_report], [preventivemeasurepersonal_understood], [preventivemeasurepersonal_report], [othersImplans_understood], [othersImplans_report], [othersConsent_understood], [othersConsent_report], [othersFinancial_understood], [othersFinancial_report], [othersCommunity_understood], [othersCommunity_report], [date_time], [TenantId])
SELECT [ID], [PF_Id], [confirmeddiagnosis_understood], [confirmeddiagnosis_report], [safeeffective_understood], [safeeffective_report], [druginteraction_understood], [druginteraction_report], [drugfood_understood], [drugfood_report], [Nutrition_understood], [Nutrition_report], [Nutrition_explained], [safeequipment_understood], [safeequipment_report], [safeequipment_explained], [Painmanagement_understood], [Painmanagement_report], [rehabilitationtechnique_understood], [rehabilitationtechnique_report], [rehabilitationtechnique_explained], [dischargeplanhome_understood], [dischargeplanhome_report], [dischargeplanfollowup_understood], [dischargeplanfollowup_report], [preventivemeasureinfection_understood], [preventivemeasureinfection_report], [preventivemeasurepersonal_understood], [preventivemeasurepersonal_report], [othersImplans_understood], [othersImplans_report], [othersConsent_understood], [othersConsent_report], [othersFinancial_understood], [othersFinancial_report], [othersCommunity_understood], [othersCommunity_report], [date_time], @TenantId
FROM [SunCity_Clinics].[InPatient].[Patient_Family];
SET IDENTITY_INSERT [InPatient].[Patient_Family] OFF;
GO

PRINT 'Migrating [InPatient].[PermanentPacemakerImplantationReport]...';
SET IDENTITY_INSERT [InPatient].[PermanentPacemakerImplantationReport] ON;
INSERT INTO [InPatient].[PermanentPacemakerImplantationReport] ([ID], [PatientID], [IPNumber], [DateOfImplantation], [Ys], [DoctorID], [IndicationForPermanent], [ECGBeforePacemaker], [PreMedication], [IVAntibiotic], [LocalAnaesthesia], [VenousAccess], [PocketSite], [LeadInsertionSite], [AtrialLead], [VentricualLead], [BatteryInsertionSite], [BatteryFixation], [WoundClosure], [Subcutaneous], [Skin], [LocalAntibiotic], [PWave], [RWave], [VentricularPacingThreshold], [ImpedanceAtrialLead], [ImpedanceVentricularLead], [VLeadManufacturer], [VLeadModel], [AtrialLeadManufacturer], [BatteryDataManufacturer], [Complications], [Antibiotics], [AppointmentsForPacemaker], [ArtialPacingThreshold], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [AtrialLeadModel], [BatteryDataModel], [VLeadSerialNo], [AtrialLeadSerialNo], [BatteryDataSerialNo], [VLeadType], [AtrialLeadType], [BatteryDataType], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [IPNumber], [DateOfImplantation], [Ys], [DoctorID], [IndicationForPermanent], [ECGBeforePacemaker], [PreMedication], [IVAntibiotic], [LocalAnaesthesia], [VenousAccess], [PocketSite], [LeadInsertionSite], [AtrialLead], [VentricualLead], [BatteryInsertionSite], [BatteryFixation], [WoundClosure], [Subcutaneous], [Skin], [LocalAntibiotic], [PWave], [RWave], [VentricularPacingThreshold], [ImpedanceAtrialLead], [ImpedanceVentricularLead], [VLeadManufacturer], [VLeadModel], [AtrialLeadManufacturer], [BatteryDataManufacturer], [Complications], [Antibiotics], [AppointmentsForPacemaker], [ArtialPacingThreshold], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [AtrialLeadModel], [BatteryDataModel], [VLeadSerialNo], [AtrialLeadSerialNo], [BatteryDataSerialNo], [VLeadType], [AtrialLeadType], [BatteryDataType], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[PermanentPacemakerImplantationReport];
SET IDENTITY_INSERT [InPatient].[PermanentPacemakerImplantationReport] OFF;
GO

PRINT 'Migrating [InPatient].[PostOPNursingCarePlanHeader]...';
SET IDENTITY_INSERT [InPatient].[PostOPNursingCarePlanHeader] ON;
INSERT INTO [InPatient].[PostOPNursingCarePlanHeader] ([Id], [PatientId], [Code], [NurseId], [CareModeId], [Date], [Time], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IPOP], [TenantId])
SELECT [Id], [PatientId], [Code], [NurseId], [CareModeId], [Date], [Time], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IPOP], @TenantId
FROM [SunCity_Clinics].[InPatient].[PostOPNursingCarePlanHeader];
SET IDENTITY_INSERT [InPatient].[PostOPNursingCarePlanHeader] OFF;
GO

PRINT 'Migrating [InPatient].[PostOPNursingCarePlan_TagsValues]...';
SET IDENTITY_INSERT [InPatient].[PostOPNursingCarePlan_TagsValues] ON;
INSERT INTO [InPatient].[PostOPNursingCarePlan_TagsValues] ([Id], [Value1], [Value2], [GeneralHeaderId], [PageTagsId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Value1], [Value2], [GeneralHeaderId], [PageTagsId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[PostOPNursingCarePlan_TagsValues];
SET IDENTITY_INSERT [InPatient].[PostOPNursingCarePlan_TagsValues] OFF;
GO

PRINT 'Migrating [InPatient].[PostRoomCharges]...';
SET IDENTITY_INSERT [InPatient].[PostRoomCharges] ON;
INSERT INTO [InPatient].[PostRoomCharges] ([Id], [AdmitPatientsId], [ChargeDays], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [AdmitPatientsId], [ChargeDays], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[PostRoomCharges];
SET IDENTITY_INSERT [InPatient].[PostRoomCharges] OFF;
GO

PRINT 'Migrating [InPatient].[PreAnesthesiaEvaluation]...';
SET IDENTITY_INSERT [InPatient].[PreAnesthesiaEvaluation] ON;
INSERT INTO [InPatient].[PreAnesthesiaEvaluation] ([ID], [PatientID], [HistoryFrom], [HistoryFromOther], [PreviousAnesthesiaNone], [PreviousAnesthesia], [CurrentMedicationsNone], [AllergiesReaCcionNone], [AirwayType], [TMDistance], [MODistance], [NeckRom], [RespiratoryWNL], [RespiratoryEnum], [TobacooUse], [TobacooUseRR], [TobacooUsePacks], [TobacooUseForYears], [TobacooUseOut], [TobacooUseOutText], [TobacooUsePPPPE], [CardioVascularWML], [CardioVascularEnum], [VitalsHR], [VitalsBP], [VitalsJVPCVP], [VitalsPeripheralpulses], [VitalsPPPPE], [HepatoGastrointestinalWML], [HepatoGastrointestinalEnum], [EthanolUse], [EthanolUseFrequancy], [EthanolUseHxETOHAbuse], [EthanolUseOut], [EthanolUseOutText], [NeuroMusculoskeletalWML], [NeuroMusculoskeletalEnum], [RenalEndcorineWML], [RenalEndcorineEnum], [OtherWML], [OtherEnum], [FamilialAnesProblems], [FamilialAnesProblemsNotes], [SurgicalDiagnosisOrProblemList], [AsaPhysicalStatus], [NpoPerASAGuidelines], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [HistoryFrom], [HistoryFromOther], [PreviousAnesthesiaNone], [PreviousAnesthesia], [CurrentMedicationsNone], [AllergiesReaCcionNone], [AirwayType], [TMDistance], [MODistance], [NeckRom], [RespiratoryWNL], [RespiratoryEnum], [TobacooUse], [TobacooUseRR], [TobacooUsePacks], [TobacooUseForYears], [TobacooUseOut], [TobacooUseOutText], [TobacooUsePPPPE], [CardioVascularWML], [CardioVascularEnum], [VitalsHR], [VitalsBP], [VitalsJVPCVP], [VitalsPeripheralpulses], [VitalsPPPPE], [HepatoGastrointestinalWML], [HepatoGastrointestinalEnum], [EthanolUse], [EthanolUseFrequancy], [EthanolUseHxETOHAbuse], [EthanolUseOut], [EthanolUseOutText], [NeuroMusculoskeletalWML], [NeuroMusculoskeletalEnum], [RenalEndcorineWML], [RenalEndcorineEnum], [OtherWML], [OtherEnum], [FamilialAnesProblems], [FamilialAnesProblemsNotes], [SurgicalDiagnosisOrProblemList], [AsaPhysicalStatus], [NpoPerASAGuidelines], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[PreAnesthesiaEvaluation];
SET IDENTITY_INSERT [InPatient].[PreAnesthesiaEvaluation] OFF;
GO

PRINT 'Migrating [InPatient].[PreOperativeMarking]...';
SET IDENTITY_INSERT [InPatient].[PreOperativeMarking] ON;
INSERT INTO [InPatient].[PreOperativeMarking] ([Id], [PreOperativeId], [Mandatory], [Checked], [ScheduleSurgeryId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PreOperativeId], [Mandatory], [Checked], [ScheduleSurgeryId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[PreOperativeMarking];
SET IDENTITY_INSERT [InPatient].[PreOperativeMarking] OFF;
GO

PRINT 'Migrating [InPatient].[PreOperative]...';
SET IDENTITY_INSERT [InPatient].[PreOperative] ON;
INSERT INTO [InPatient].[PreOperative] ([Id], [Description], [Mandatory], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Description], [Mandatory], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[PreOperative];
SET IDENTITY_INSERT [InPatient].[PreOperative] OFF;
GO

PRINT 'Migrating [InPatient].[PrescriptionDetails]...';
SET IDENTITY_INSERT [InPatient].[PrescriptionDetails] ON;
INSERT INTO [InPatient].[PrescriptionDetails] ([Id], [PatientID], [PrescriptionNo], [DrugID], [Dosage], [DosageUnitID], [FerquencyID], [Period], [PeriodTypeID], [DoseQTY], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [PrescriptionNo], [DrugID], [Dosage], [DosageUnitID], [FerquencyID], [Period], [PeriodTypeID], [DoseQTY], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[PrescriptionDetails];
SET IDENTITY_INSERT [InPatient].[PrescriptionDetails] OFF;
GO

PRINT 'Migrating [InPatient].[PrescriptionDispenseSetting]...';
SET IDENTITY_INSERT [InPatient].[PrescriptionDispenseSetting] ON;
INSERT INTO [InPatient].[PrescriptionDispenseSetting] ([Id], [Days], [Prescription], [NoOfDays], [CompanyID], [TenantId])
SELECT [Id], [Days], [Prescription], [NoOfDays], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[PrescriptionDispenseSetting];
SET IDENTITY_INSERT [InPatient].[PrescriptionDispenseSetting] OFF;
GO

PRINT 'Migrating [InPatient].[Prescription]...';
SET IDENTITY_INSERT [InPatient].[Prescription] ON;
INSERT INTO [InPatient].[Prescription] ([Id], [PatientID], [OPNo], [IPNo], [PrescriptionNo], [PrescriptionDate], [AlertName], [DoctorID], [Notes], [TotalAmount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [OPNo], [IPNo], [PrescriptionNo], [PrescriptionDate], [AlertName], [DoctorID], [Notes], [TotalAmount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[Prescription];
SET IDENTITY_INSERT [InPatient].[Prescription] OFF;
GO

PRINT 'Migrating [InPatient].[ProvisionalDiagnosis]...';
SET IDENTITY_INSERT [InPatient].[ProvisionalDiagnosis] ON;
INSERT INTO [InPatient].[ProvisionalDiagnosis] ([PatientID], [AdmitPatientID], [ICDCodeID], [Type], [ID], [TenantId])
SELECT [PatientID], [AdmitPatientID], [ICDCodeID], [Type], [ID], @TenantId
FROM [SunCity_Clinics].[InPatient].[ProvisionalDiagnosis];
SET IDENTITY_INSERT [InPatient].[ProvisionalDiagnosis] OFF;
GO

PRINT 'Migrating [InPatient].[ReferralType]...';
SET IDENTITY_INSERT [InPatient].[ReferralType] ON;
INSERT INTO [InPatient].[ReferralType] ([Id], [TypeCode], [NameArabic], [NameEnglish], [Status], [HospitalCase], [DoctorCommission], [AccNo], [MonthFees], [CompanyID], [TenantId])
SELECT [Id], [TypeCode], [NameArabic], [NameEnglish], [Status], [HospitalCase], [DoctorCommission], [AccNo], [MonthFees], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[ReferralType];
SET IDENTITY_INSERT [InPatient].[ReferralType] OFF;
GO

PRINT 'Migrating [InPatient].[RegisteringPackage]...';
SET IDENTITY_INSERT [InPatient].[RegisteringPackage] ON;
INSERT INTO [InPatient].[RegisteringPackage] ([Id], [PatientID], [PackageID], [Amount], [NetAmount], [DiscountAmount], [DiscountPrecentage], [TypeID], [IPNumber], [InstalmentAmount], [ColectedAmount], [InvoiceNumber], [BalanceAmount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [PackageID], [Amount], [NetAmount], [DiscountAmount], [DiscountPrecentage], [TypeID], [IPNumber], [InstalmentAmount], [ColectedAmount], [InvoiceNumber], [BalanceAmount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[RegisteringPackage];
SET IDENTITY_INSERT [InPatient].[RegisteringPackage] OFF;
GO

PRINT 'Migrating [InPatient].[RegisteringPackageinstallment]...';
SET IDENTITY_INSERT [InPatient].[RegisteringPackageinstallment] ON;
INSERT INTO [InPatient].[RegisteringPackageinstallment] ([Id], [PatientID], [PackageID], [IPNumber], [InvoiceNumber], [Amount], [Duration], [Period], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [PackageID], [IPNumber], [InvoiceNumber], [Amount], [Duration], [Period], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[RegisteringPackageinstallment];
SET IDENTITY_INSERT [InPatient].[RegisteringPackageinstallment] OFF;
GO

PRINT 'Migrating [InPatient].[RepetType]...';
SET IDENTITY_INSERT [InPatient].[RepetType] ON;
INSERT INTO [InPatient].[RepetType] ([ID], [Code], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[RepetType];
SET IDENTITY_INSERT [InPatient].[RepetType] OFF;
GO

PRINT 'Migrating [InPatient].[RequestProceduresDetails]...';
SET IDENTITY_INSERT [InPatient].[RequestProceduresDetails] ON;
INSERT INTO [InPatient].[RequestProceduresDetails] ([Id], [RequestProceduresID], [ProcedureID], [Amount], [Priority], [IsApproved], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [RequestProceduresID], [ProcedureID], [Amount], [Priority], [IsApproved], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[RequestProceduresDetails];
SET IDENTITY_INSERT [InPatient].[RequestProceduresDetails] OFF;
GO

PRINT 'Migrating [InPatient].[RequestProceduresHeader]...';
SET IDENTITY_INSERT [InPatient].[RequestProceduresHeader] ON;
INSERT INTO [InPatient].[RequestProceduresHeader] ([Id], [PatientID], [Type], [DoctorID], [TotalAmount], [OP_IPNumber], [IsPregnant], [PregnantWeeks], [ClinicalDetails], [ReqDate], [ReqStatus], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [SurgeryID], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [Type], [DoctorID], [TotalAmount], [OP_IPNumber], [IsPregnant], [PregnantWeeks], [ClinicalDetails], [ReqDate], [ReqStatus], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [SurgeryID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[RequestProceduresHeader];
SET IDENTITY_INSERT [InPatient].[RequestProceduresHeader] OFF;
GO

PRINT 'Migrating [InPatient].[RequestSuppliesDetails]...';
SET IDENTITY_INSERT [InPatient].[RequestSuppliesDetails] ON;
INSERT INTO [InPatient].[RequestSuppliesDetails] ([Id], [RequestSuppliesHeaderID], [ItemID], [UnitConversionID], [ReturnedQty], [Quantity], [IsIncluded], [StockBatchId], [IsApproved], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [CurrencyID], [ConvValue], [Price], [StockControlDetailList], [ISCash], [InsuranceId], [Dispense], [Verified], [VerifiedDate], [VerifiedBY], [IsCancelled], [TenantId])
SELECT [Id], [RequestSuppliesHeaderID], [ItemID], [UnitConversionID], [ReturnedQty], [Quantity], [IsIncluded], [StockBatchId], [IsApproved], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [CurrencyID], [ConvValue], [Price], [StockControlDetailList], [ISCash], [InsuranceId], [Dispense], [Verified], [VerifiedDate], [VerifiedBY], [IsCancelled], @TenantId
FROM [SunCity_Clinics].[InPatient].[RequestSuppliesDetails];
SET IDENTITY_INSERT [InPatient].[RequestSuppliesDetails] OFF;
GO

PRINT 'Migrating [InPatient].[RequestSuppliesHeader]...';
SET IDENTITY_INSERT [InPatient].[RequestSuppliesHeader] ON;
INSERT INTO [InPatient].[RequestSuppliesHeader] ([Id], [PatientID], [PatientTypeID], [OP_IPNo], [RequestNo], [DoctorID], [RequestStatus], [SubStoreID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [branchId], [TenantId])
SELECT [Id], [PatientID], [PatientTypeID], [OP_IPNo], [RequestNo], [DoctorID], [RequestStatus], [SubStoreID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [branchId], @TenantId
FROM [SunCity_Clinics].[InPatient].[RequestSuppliesHeader];
SET IDENTITY_INSERT [InPatient].[RequestSuppliesHeader] OFF;
GO

PRINT 'Migrating [InPatient].[RequestSupplyReturnDetails]...';
SET IDENTITY_INSERT [InPatient].[RequestSupplyReturnDetails] ON;
INSERT INTO [InPatient].[RequestSupplyReturnDetails] ([ID], [RequestSupplyReturnHeaderID], [RequestSupplyDetailsID], [ReturnedQty], [UnitConversionID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [ID], [RequestSupplyReturnHeaderID], [RequestSupplyDetailsID], [ReturnedQty], [UnitConversionID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[InPatient].[RequestSupplyReturnDetails];
SET IDENTITY_INSERT [InPatient].[RequestSupplyReturnDetails] OFF;
GO

PRINT 'Migrating [InPatient].[RequestSupplyReturnHeader]...';
SET IDENTITY_INSERT [InPatient].[RequestSupplyReturnHeader] ON;
INSERT INTO [InPatient].[RequestSupplyReturnHeader] ([ID], [RequestSupplyID], [RequestSupplyRetuenCode], [ReturnDate], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [EntryCode], [CompanyID], [TenantId])
SELECT [ID], [RequestSupplyID], [RequestSupplyRetuenCode], [ReturnDate], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [EntryCode], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[RequestSupplyReturnHeader];
SET IDENTITY_INSERT [InPatient].[RequestSupplyReturnHeader] OFF;
GO

PRINT 'Migrating [InPatient].[RoomAccommodationTypes]...';
SET IDENTITY_INSERT [InPatient].[RoomAccommodationTypes] ON;
INSERT INTO [InPatient].[RoomAccommodationTypes] ([Id], [RoomId], [AccommodationTypeId], [TenantId])
SELECT [Id], [RoomId], [AccommodationTypeId], @TenantId
FROM [SunCity_Clinics].[InPatient].[RoomAccommodationTypes];
SET IDENTITY_INSERT [InPatient].[RoomAccommodationTypes] OFF;
GO

PRINT 'Migrating [InPatient].[RoomTransfer]...';
SET IDENTITY_INSERT [InPatient].[RoomTransfer] ON;
INSERT INTO [InPatient].[RoomTransfer] ([Id], [PatientID], [IPNumber], [FromWardId], [ToWardId], [FromBedId], [ToBedId], [TransferDate], [TransferTime], [Remarks], [AdmitPateintId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [IPNumber], [FromWardId], [ToWardId], [FromBedId], [ToBedId], [TransferDate], [TransferTime], [Remarks], [AdmitPateintId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[RoomTransfer];
SET IDENTITY_INSERT [InPatient].[RoomTransfer] OFF;
GO

PRINT 'Migrating [InPatient].[RoomType]...';
SET IDENTITY_INSERT [InPatient].[RoomType] ON;
INSERT INTO [InPatient].[RoomType] ([Id], [Code], [Name], [NameEn], [DayPrice], [CompanyID], [Ward], [AccomodationType], [VivRoom], [Active], [IcuRoom], [TenantId])
SELECT [Id], [Code], [Name], [NameEn], [DayPrice], [CompanyID], [Ward], [AccomodationType], [VivRoom], [Active], [IcuRoom], @TenantId
FROM [SunCity_Clinics].[InPatient].[RoomType];
SET IDENTITY_INSERT [InPatient].[RoomType] OFF;
GO

PRINT 'Migrating [InPatient].[Room]...';
SET IDENTITY_INSERT [InPatient].[Room] ON;
INSERT INTO [InPatient].[Room] ([Id], [RoomNumber], [RoomTypeId], [CostCenterId], [WardId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [AccommodationTypeId], [Code], [AccommodationTypesIDz], [Active], [VIP], [Phone], [BranchId], [TenantId])
SELECT [Id], [RoomNumber], [RoomTypeId], [CostCenterId], [WardId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [AccommodationTypeId], [Code], [AccommodationTypesIDz], [Active], [VIP], [Phone], [BranchId], @TenantId
FROM [SunCity_Clinics].[InPatient].[Room];
SET IDENTITY_INSERT [InPatient].[Room] OFF;
GO

PRINT 'Migrating [InPatient].[ScheduleOT]...';
SET IDENTITY_INSERT [InPatient].[ScheduleOT] ON;
INSERT INTO [InPatient].[ScheduleOT] ([Id], [OperationID], [OperationDate], [PatientID], [SurgeonID], [NurseID], [SurgeryID], [StartTime], [EndTime], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [OperationID], [OperationDate], [PatientID], [SurgeonID], [NurseID], [SurgeryID], [StartTime], [EndTime], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[ScheduleOT];
SET IDENTITY_INSERT [InPatient].[ScheduleOT] OFF;
GO

PRINT 'Migrating [InPatient].[ScheduleSurgery]...';
SET IDENTITY_INSERT [InPatient].[ScheduleSurgery] ON;
INSERT INTO [InPatient].[ScheduleSurgery] ([Id], [SurgeonId], [AnesthetistId], [AssistantId], [ReqId], [PatientId], [ScheduleSurgeryNO], [ScheduleSurgeryStatus], [OperationTheatreId], [SurgeryId], [FromDate], [ToDate], [FromTime], [ToTime], [AdditionalDetailsForScheduledSurgeries], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [SurgeonId], [AnesthetistId], [AssistantId], [ReqId], [PatientId], [ScheduleSurgeryNO], [ScheduleSurgeryStatus], [OperationTheatreId], [SurgeryId], [FromDate], [ToDate], [FromTime], [ToTime], [AdditionalDetailsForScheduledSurgeries], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[ScheduleSurgery];
SET IDENTITY_INSERT [InPatient].[ScheduleSurgery] OFF;
GO

PRINT 'Migrating [InPatient].[SurgeryType]...';
SET IDENTITY_INSERT [InPatient].[SurgeryType] ON;
INSERT INTO [InPatient].[SurgeryType] ([Id], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameAr], [TenantId])
SELECT [Id], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameAr], @TenantId
FROM [SunCity_Clinics].[InPatient].[SurgeryType];
SET IDENTITY_INSERT [InPatient].[SurgeryType] OFF;
GO

PRINT 'Migrating [InPatient].[Surgery]...';
SET IDENTITY_INSERT [InPatient].[Surgery] ON;
INSERT INTO [InPatient].[Surgery] ([Id], [TypeCode], [Code], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameAr], [TenantId])
SELECT [Id], [TypeCode], [Code], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameAr], @TenantId
FROM [SunCity_Clinics].[InPatient].[Surgery];
SET IDENTITY_INSERT [InPatient].[Surgery] OFF;
GO

PRINT 'Migrating [InPatient].[Tags]...';
SET IDENTITY_INSERT [InPatient].[Tags] ON;
INSERT INTO [InPatient].[Tags] ([Id], [Name], [Code], [Type], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OrderNo], [TenantId])
SELECT [Id], [Name], [Code], [Type], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OrderNo], @TenantId
FROM [SunCity_Clinics].[InPatient].[Tags];
SET IDENTITY_INSERT [InPatient].[Tags] OFF;
GO

PRINT 'Migrating [InPatient].[TelephoneCharges]...';
SET IDENTITY_INSERT [InPatient].[TelephoneCharges] ON;
INSERT INTO [InPatient].[TelephoneCharges] ([Id], [PatientID], [LastIPNumber], [ChargeDate], [TelephoneNo], [Duration], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [LastIPNumber], [ChargeDate], [TelephoneNo], [Duration], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[TelephoneCharges];
SET IDENTITY_INSERT [InPatient].[TelephoneCharges] OFF;
GO

PRINT 'Migrating [InPatient].[TemporaryDischargeType]...';
SET IDENTITY_INSERT [InPatient].[TemporaryDischargeType] ON;
INSERT INTO [InPatient].[TemporaryDischargeType] ([Id], [TypeCode], [NameArabic], [NameEnglish], [Status], [HospitalCase], [CompanyID], [TenantId])
SELECT [Id], [TypeCode], [NameArabic], [NameEnglish], [Status], [HospitalCase], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[TemporaryDischargeType];
SET IDENTITY_INSERT [InPatient].[TemporaryDischargeType] OFF;
GO

PRINT 'Migrating [InPatient].[TemporaryExit]...';
SET IDENTITY_INSERT [InPatient].[TemporaryExit] ON;
INSERT INTO [InPatient].[TemporaryExit] ([ID], [PateintID], [PatientIPNo], [DoctorID], [GoOutTime], [BackTime], [DoctorApproveAttachmentURL], [PatientSignConsentAttachmentURL], [ActualBackTime], [CreatedBy], [CreationDate], [LastModificationBy], [LastModificationDate], [CompanyID], [TenantId])
SELECT [ID], [PateintID], [PatientIPNo], [DoctorID], [GoOutTime], [BackTime], [DoctorApproveAttachmentURL], [PatientSignConsentAttachmentURL], [ActualBackTime], [CreatedBy], [CreationDate], [LastModificationBy], [LastModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[TemporaryExit];
SET IDENTITY_INSERT [InPatient].[TemporaryExit] OFF;
GO

PRINT 'Migrating [InPatient].[TimeBoundServiceRequestDetails]...';
SET IDENTITY_INSERT [InPatient].[TimeBoundServiceRequestDetails] ON;
INSERT INTO [InPatient].[TimeBoundServiceRequestDetails] ([Id], [PatientID], [TimeBoundServiceRequestID], [ServiceID], [IssueUnitID], [UnitCharge], [TimeBound], [FromTime], [ToTime], [TotalUnits], [TotalAmount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [TimeBoundServiceRequestID], [ServiceID], [IssueUnitID], [UnitCharge], [TimeBound], [FromTime], [ToTime], [TotalUnits], [TotalAmount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[TimeBoundServiceRequestDetails];
SET IDENTITY_INSERT [InPatient].[TimeBoundServiceRequestDetails] OFF;
GO

PRINT 'Migrating [InPatient].[TimeBoundServiceRequestHeader]...';
SET IDENTITY_INSERT [InPatient].[TimeBoundServiceRequestHeader] ON;
INSERT INTO [InPatient].[TimeBoundServiceRequestHeader] ([Id], [PatientID], [FromDate], [Todate], [DoctorID], [IsPerDay], [IsCumulative], [AccupancyNO], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [FromDate], [Todate], [DoctorID], [IsPerDay], [IsCumulative], [AccupancyNO], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[TimeBoundServiceRequestHeader];
SET IDENTITY_INSERT [InPatient].[TimeBoundServiceRequestHeader] OFF;
GO

PRINT 'Migrating [InPatient].[UCAF]...';
SET IDENTITY_INSERT [InPatient].[UCAF] ON;
INSERT INTO [InPatient].[UCAF] ([Id], [Patient], [LMP], [PlanType], [IllnessDuration], [Area_Significant], [PrincipleCode], [SecondCode], [HirdCod], [ourthCode], [Esstimated], [dmissionDate], [Physician], [txtDate], [txtRelationship], [txtRelationshipSignature], [txtRelationshipDate], [WorkIN], [completed], [Chronic], [Congenital], [RTA], [Work], [Vaccanation], [Checkup], [Physicantric], [infiritily], [pregnancy], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [reff], [IPOP], [VisitDate], [TenantId])
SELECT [Id], [Patient], [LMP], [PlanType], [IllnessDuration], [Area_Significant], [PrincipleCode], [SecondCode], [HirdCod], [ourthCode], [Esstimated], [dmissionDate], [Physician], [txtDate], [txtRelationship], [txtRelationshipSignature], [txtRelationshipDate], [WorkIN], [completed], [Chronic], [Congenital], [RTA], [Work], [Vaccanation], [Checkup], [Physicantric], [infiritily], [pregnancy], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [reff], [IPOP], [VisitDate], @TenantId
FROM [SunCity_Clinics].[InPatient].[UCAF];
SET IDENTITY_INSERT [InPatient].[UCAF] OFF;
GO

PRINT 'Migrating [InPatient].[VirtualClinic]...';
SET IDENTITY_INSERT [InPatient].[VirtualClinic] ON;
INSERT INTO [InPatient].[VirtualClinic] ([Id], [DoctorId], [PatientId], [MeetingId], [CreatedDate], [status], [TenantId])
SELECT [Id], [DoctorId], [PatientId], [MeetingId], [CreatedDate], [status], @TenantId
FROM [SunCity_Clinics].[InPatient].[VirtualClinic];
SET IDENTITY_INSERT [InPatient].[VirtualClinic] OFF;
GO

PRINT 'Migrating [InPatient].[VitalParameters]...';
SET IDENTITY_INSERT [InPatient].[VitalParameters] ON;
INSERT INTO [InPatient].[VitalParameters] ([Id], [Name], [Code], [Description], [VitalTypeGroupId], [FindingId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Name], [Code], [Description], [VitalTypeGroupId], [FindingId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[VitalParameters];
SET IDENTITY_INSERT [InPatient].[VitalParameters] OFF;
GO

PRINT 'Migrating [InPatient].[VitalSettings]...';
SET IDENTITY_INSERT [InPatient].[VitalSettings] ON;
INSERT INTO [InPatient].[VitalSettings] ([Id], [Category], [AgeFrom], [AgeTo], [PulseFrom], [PulseTo], [RespRateFrom], [RespRateTo], [SystolicBPFrom], [SystolicBPTo], [TempratureFrom], [TempratureTo], [Notes], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [NameKa], [TenantId])
SELECT [Id], [Category], [AgeFrom], [AgeTo], [PulseFrom], [PulseTo], [RespRateFrom], [RespRateTo], [SystolicBPFrom], [SystolicBPTo], [TempratureFrom], [TempratureTo], [Notes], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [NameKa], @TenantId
FROM [SunCity_Clinics].[InPatient].[VitalSettings];
SET IDENTITY_INSERT [InPatient].[VitalSettings] OFF;
GO

PRINT 'Migrating [InPatient].[VitalSigns]...';
SET IDENTITY_INSERT [InPatient].[VitalSigns] ON;
INSERT INTO [InPatient].[VitalSigns] ([ID], [Pulse_BPH], [Urine], [Extremity], [Glucose], [TempC], [TempMode], [RespRate_MIN], [Positions], [Bowel], [MEWs], [PainScore], [Systole_MM_Hg], [LevelOfConsciouseness], [OxygenSaturation], [Diastole_MM_Hg], [O2Amount], [FallRisk], [Height], [Weight], [PatientID], [DoctorID], [Comment], [Date], [IPOP], [BloodPressure_SYSTOLIC], [BloodPressure_DIASTOLIC], [CVP], [SourceName], [TempF], [BP_MM_Hg], [CompanyID], [BloodTransfusionId], [DonorId], [pain], [painLocation], [painDuration], [painCharac], [painFreq], [painRad], [painmodifie], [BMI], [category], [TenantId])
SELECT [ID], [Pulse_BPH], [Urine], [Extremity], [Glucose], [TempC], [TempMode], [RespRate_MIN], [Positions], [Bowel], [MEWs], [PainScore], [Systole_MM_Hg], [LevelOfConsciouseness], [OxygenSaturation], [Diastole_MM_Hg], [O2Amount], [FallRisk], [Height], [Weight], [PatientID], [DoctorID], [Comment], [Date], [IPOP], [BloodPressure_SYSTOLIC], [BloodPressure_DIASTOLIC], [CVP], [SourceName], [TempF], [BP_MM_Hg], [CompanyID], [BloodTransfusionId], [DonorId], [pain], [painLocation], [painDuration], [painCharac], [painFreq], [painRad], [painmodifie], [BMI], [category], @TenantId
FROM [SunCity_Clinics].[InPatient].[VitalSigns];
SET IDENTITY_INSERT [InPatient].[VitalSigns] OFF;
GO

PRINT 'Migrating [InPatient].[VitalTypeGroup]...';
SET IDENTITY_INSERT [InPatient].[VitalTypeGroup] ON;
INSERT INTO [InPatient].[VitalTypeGroup] ([Id], [Name], [CompanyID], [TenantId])
SELECT [Id], [Name], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[VitalTypeGroup];
SET IDENTITY_INSERT [InPatient].[VitalTypeGroup] OFF;
GO

PRINT 'Migrating [InPatient].[WardCategory]...';
SET IDENTITY_INSERT [InPatient].[WardCategory] ON;
INSERT INTO [InPatient].[WardCategory] ([Id], [Name], [CompanyID], [BranchId], [TenantId])
SELECT [Id], [Name], [CompanyID], [BranchId], @TenantId
FROM [SunCity_Clinics].[InPatient].[WardCategory];
SET IDENTITY_INSERT [InPatient].[WardCategory] OFF;
GO

PRINT 'Migrating [InPatient].[WardPatientPrescriptions]...';
SET IDENTITY_INSERT [InPatient].[WardPatientPrescriptions] ON;
INSERT INTO [InPatient].[WardPatientPrescriptions] ([Id], [WardPharmacyID], [PatientID], [DrugID], [Dosage], [UnitID], [CurrentQty], [FerquencyID], [Period], [PeriodTypeID], [QTY], [Price], [SubStoreBatchId], [ExpiryDate], [TimeTake], [WitnessID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [PrescriptionTimeTake], [SkipReason], [PrescriptionStatus], [TenantId])
SELECT [Id], [WardPharmacyID], [PatientID], [DrugID], [Dosage], [UnitID], [CurrentQty], [FerquencyID], [Period], [PeriodTypeID], [QTY], [Price], [SubStoreBatchId], [ExpiryDate], [TimeTake], [WitnessID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [PrescriptionTimeTake], [SkipReason], [PrescriptionStatus], @TenantId
FROM [SunCity_Clinics].[InPatient].[WardPatientPrescriptions];
SET IDENTITY_INSERT [InPatient].[WardPatientPrescriptions] OFF;
GO

PRINT 'Migrating [InPatient].[WardPharmacyDetails]...';
SET IDENTITY_INSERT [InPatient].[WardPharmacyDetails] ON;
INSERT INTO [InPatient].[WardPharmacyDetails] ([Id], [WardPharmacyID], [DrugID], [Dosage], [UnitID], [CurrentQty], [FerquencyID], [Period], [PeriodTypeID], [QTY], [Price], [SubStoreBatchId], [ExpiryDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [WardPharmacyID], [DrugID], [Dosage], [UnitID], [CurrentQty], [FerquencyID], [Period], [PeriodTypeID], [QTY], [Price], [SubStoreBatchId], [ExpiryDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[WardPharmacyDetails];
SET IDENTITY_INSERT [InPatient].[WardPharmacyDetails] OFF;
GO

PRINT 'Migrating [InPatient].[WardPharmacyPaymentDetails]...';
SET IDENTITY_INSERT [InPatient].[WardPharmacyPaymentDetails] ON;
INSERT INTO [InPatient].[WardPharmacyPaymentDetails] ([Id], [PatientID], [WardPharmacyId], [ReceiptNo], [PaymentModeId], [Amount], [CurrencyId], [InstrumentNo], [InstrumentDate], [Bank], [Remark], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [WardPharmacyId], [ReceiptNo], [PaymentModeId], [Amount], [CurrencyId], [InstrumentNo], [InstrumentDate], [Bank], [Remark], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[WardPharmacyPaymentDetails];
SET IDENTITY_INSERT [InPatient].[WardPharmacyPaymentDetails] OFF;
GO

PRINT 'Migrating [InPatient].[WardPharmacy]...';
SET IDENTITY_INSERT [InPatient].[WardPharmacy] ON;
INSERT INTO [InPatient].[WardPharmacy] ([Id], [SubStoreID], [PatientID], [ReceiptNo], [ReceiptDate], [PaymentTypeID], [DoctorID], [PackageID], [SponsorID], [IPNO], [PrescriptionNo], [PrescriptionDate], [SponsorCatagoryID], [SelfPayAmount], [TotalAmount], [CollectedAmount], [Balance], [Return], [OpenInvoiceNo], [InvoiceAmount], [CoPay], [CoPay2], [IsCoPayByPer], [DischMedication], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [EnteredBy], [TenantId])
SELECT [Id], [SubStoreID], [PatientID], [ReceiptNo], [ReceiptDate], [PaymentTypeID], [DoctorID], [PackageID], [SponsorID], [IPNO], [PrescriptionNo], [PrescriptionDate], [SponsorCatagoryID], [SelfPayAmount], [TotalAmount], [CollectedAmount], [Balance], [Return], [OpenInvoiceNo], [InvoiceAmount], [CoPay], [CoPay2], [IsCoPayByPer], [DischMedication], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [EnteredBy], @TenantId
FROM [SunCity_Clinics].[InPatient].[WardPharmacy];
SET IDENTITY_INSERT [InPatient].[WardPharmacy] OFF;
GO

PRINT 'Migrating [InPatient].[WardType]...';
SET IDENTITY_INSERT [InPatient].[WardType] ON;
INSERT INTO [InPatient].[WardType] ([Id], [Code], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameAr], [BranchId], [TenantId])
SELECT [Id], [Code], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameAr], [BranchId], @TenantId
FROM [SunCity_Clinics].[InPatient].[WardType];
SET IDENTITY_INSERT [InPatient].[WardType] OFF;
GO

PRINT 'Migrating [InPatient].[Ward]...';
SET IDENTITY_INSERT [InPatient].[Ward] ON;
INSERT INTO [InPatient].[Ward] ([Id], [Code], [Name], [WardTypeId], [WardCategoryId], [CostCenterId], [PharmcyId], [InventoryId], [MedicalServiceIdz], [Active], [Description], [IsSecondaryWard], [IsEndOfDay], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [FloorID], [CompanyID], [NameAr], [SpecialityID], [IsICU], [NurseID], [BranchId], [IntensiveCareType], [IsEmergency], [TenantId])
SELECT [Id], [Code], [Name], [WardTypeId], [WardCategoryId], [CostCenterId], [PharmcyId], [InventoryId], [MedicalServiceIdz], [Active], [Description], [IsSecondaryWard], [IsEndOfDay], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [FloorID], [CompanyID], [NameAr], [SpecialityID], [IsICU], [NurseID], [BranchId], [IntensiveCareType], [IsEmergency], @TenantId
FROM [SunCity_Clinics].[InPatient].[Ward];
SET IDENTITY_INSERT [InPatient].[Ward] OFF;
GO

PRINT 'Migrating [InPatient].[WishList]...';
SET IDENTITY_INSERT [InPatient].[WishList] ON;
INSERT INTO [InPatient].[WishList] ([Id], [Description], [Remarks], [Type], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CostCenterCompId], [CompanyID], [TenantId])
SELECT [Id], [Description], [Remarks], [Type], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CostCenterCompId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InPatient].[WishList];
SET IDENTITY_INSERT [InPatient].[WishList] OFF;
GO

PRINT 'Migrating [InfectionControl].[AIDS_Survey]...';
SET IDENTITY_INSERT [InfectionControl].[AIDS_Survey] ON;
INSERT INTO [InfectionControl].[AIDS_Survey] ([Id], [AIDS_InvestigationID], [DiarrheaFor1Month], [LossOfweight], [FeverFor1Month], [FeverFor1MonthContinous], [FeverFor1MonthDiscontinuous], [rash], [Inflammation_respiratory_system], [Pulmonary_tuberculosis], [Pneumonia], [Pneumocystis_pneumonia], [Fungal_infections_throat], [Simple_herpes], [Herpes_zoster], [Skin_cancers], [Opportunistic_infection], [Others], [Others_Comment], [Transfer_Reason], [Transfer_Reason_Comment], [Operation], [Operation_Type], [Operation_Place], [Operation_Date], [BloodTransfusion], [BloodTransfusion_Type], [BloodTransfusion_Units], [BloodTransfusion_Place], [BloodTransfusion_Date], [BloodTransfusion_DonorName], [SexualRelations], [SexualRelations_Sex], [SexualRelations_OtherSex], [SexualRelations_SameSex], [Drug_addicted], [Survey_Date], [Survey_EmpName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [AIDS_InvestigationID], [DiarrheaFor1Month], [LossOfweight], [FeverFor1Month], [FeverFor1MonthContinous], [FeverFor1MonthDiscontinuous], [rash], [Inflammation_respiratory_system], [Pulmonary_tuberculosis], [Pneumonia], [Pneumocystis_pneumonia], [Fungal_infections_throat], [Simple_herpes], [Herpes_zoster], [Skin_cancers], [Opportunistic_infection], [Others], [Others_Comment], [Transfer_Reason], [Transfer_Reason_Comment], [Operation], [Operation_Type], [Operation_Place], [Operation_Date], [BloodTransfusion], [BloodTransfusion_Type], [BloodTransfusion_Units], [BloodTransfusion_Place], [BloodTransfusion_Date], [BloodTransfusion_DonorName], [SexualRelations], [SexualRelations_Sex], [SexualRelations_OtherSex], [SexualRelations_SameSex], [Drug_addicted], [Survey_Date], [Survey_EmpName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[AIDS_Survey];
SET IDENTITY_INSERT [InfectionControl].[AIDS_Survey] OFF;
GO

PRINT 'Migrating [InfectionControl].[AIDS_investigation]...';
SET IDENTITY_INSERT [InfectionControl].[AIDS_investigation] ON;
INSERT INTO [InfectionControl].[AIDS_investigation] ([id], [City], [DepartmentID], [ReportedBy], [ReportDate], [PatientID], [EmpID], [PhoneNo], [ResidenceCity], [ResidenceState], [HealthBureau], [Street], [State], [OtherArea], [OtherCity], [Occupation], [MarryState], [disease], [FirstSample], [FirstTest], [FirstResult], [SecondSample], [SecondTest], [SecondResult], [Diagnose], [disease_StartDate], [AdmitDate], [Result], [DoctorID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [id], [City], [DepartmentID], [ReportedBy], [ReportDate], [PatientID], [EmpID], [PhoneNo], [ResidenceCity], [ResidenceState], [HealthBureau], [Street], [State], [OtherArea], [OtherCity], [Occupation], [MarryState], [disease], [FirstSample], [FirstTest], [FirstResult], [SecondSample], [SecondTest], [SecondResult], [Diagnose], [disease_StartDate], [AdmitDate], [Result], [DoctorID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[AIDS_investigation];
SET IDENTITY_INSERT [InfectionControl].[AIDS_investigation] OFF;
GO

PRINT 'Migrating [InfectionControl].[AreaGroups]...';
SET IDENTITY_INSERT [InfectionControl].[AreaGroups] ON;
INSERT INTO [InfectionControl].[AreaGroups] ([ID], [AreaId], [GroupId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [GroupOrder], [CompanyID], [TenantId])
SELECT [ID], [AreaId], [GroupId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [GroupOrder], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[AreaGroups];
SET IDENTITY_INSERT [InfectionControl].[AreaGroups] OFF;
GO

PRINT 'Migrating [InfectionControl].[Areas]...';
SET IDENTITY_INSERT [InfectionControl].[Areas] ON;
INSERT INTO [InfectionControl].[Areas] ([Id], [NameAr], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameKa], [TenantId])
SELECT [Id], [NameAr], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameKa], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[Areas];
SET IDENTITY_INSERT [InfectionControl].[Areas] OFF;
GO

PRINT 'Migrating [InfectionControl].[BloodExposure]...';
SET IDENTITY_INSERT [InfectionControl].[BloodExposure] ON;
INSERT INTO [InfectionControl].[BloodExposure] ([Id], [PatientID], [TypeOfExposureId], [PatientInvestgationHBSAGType], [PatientInvestgationHBSAGComment], [PatientInvestgationANTIHCVType], [PatientInvestgationANTIHCVComment], [PatientInvestgationHIVType], [PatientInvestgationHIVComment], [StaffInvestgationHBSAGType], [StaffInvestgationHBSAGComment], [StaffInvestgationANTIHCVType], [StaffInvestgationANTIHCVComment], [StaffInvestgationHIVType], [StaffInvestgationHIVComment], [BaseLineDate], [FirstFolowUpDate], [SecandFolowUpDate], [ThirdFolowUpDate], [BaseLineComment], [FirstFolowUpComment], [SecandFolowUpComment], [ThirdFolowUpComment], [SourceOfExposureId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [EmpID], [EmpFullName], [StaffInvestgationHBSAbType], [StaffInvestgationHBSAbComment], [Incident_Details], [injury_Depth], [Material_volume], [Was_fluid_injected], [skin_Condition], [PPE], [HBV_Staff_Vaccine], [Staff_HBV_TestResult], [Staff_HBV_TestDate], [Post_Exposure_Management], [Post_Exposure_Management_Yes], [Post_Exposure_Management_No], [Patient_HbsAg_Result], [Patient_HbsAg_TestDate], [Patient_HCV_ELISA_Result], [Patient_HCV_ELISA_TestDate], [Patient_HIV_ELISA_Result], [Patient_HIV_ELISA_TestDate], [Ishighrisk], [Ishighrisk_Desc], [FollowUp_BaseLine_Date], [FollowUp_BaseLine_HCV], [FollowUp_BaseLine_HBsAg], [FollowUp_BaseLine_HIV], [FollowUp_6W_Date], [FollowUp_6W_HCV], [FollowUp_6W_HBsAg], [FollowUp_6W_HIV], [FollowUp_3M_Date], [FollowUp_3M_HCV], [FollowUp_3M_HBsAg], [FollowUp_3M_HIV], [FollowUp_6M_Date], [FollowUp_6M_HCV], [FollowUp_6M_HBsAg], [FollowUp_6M_HIV], [FollowUp_12M_Date], [FollowUp_12M_HCV], [FollowUp_12M_HBsAg], [FollowUp_12M_HIV], [Treatment_HBIG_Dose1], [Treatment_HBIG_Dose1_Date], [Treatment_HBIG_Dose1_Comment], [Treatment_HBIG_Dose2], [Treatment_HBIG_Dose2_Date], [Treatment_HBIG_Dose2_Comment], [Treatment_HBV_Dose1], [Treatment_HBV_Dose1_Date], [Treatment_HBV_Dose1_Comment], [Treatment_HBV_Dose2], [Treatment_HBV_Dose2_Date], [Treatment_HBV_Dose2_Comment], [Treatment_HBV_Dose3], [Treatment_HBV_Dose3_Date], [Treatment_HBV_Dose3_Comment], [HIV_antiretroviral1], [HIV_antiretroviral2], [HIV_antiretroviral3], [IncidentDate], [CompanyID], [BloodProductRecipient], [ElevatedEnzymes], [Dialysis], [Hemophilia], [Other], [TenantId])
SELECT [Id], [PatientID], [TypeOfExposureId], [PatientInvestgationHBSAGType], [PatientInvestgationHBSAGComment], [PatientInvestgationANTIHCVType], [PatientInvestgationANTIHCVComment], [PatientInvestgationHIVType], [PatientInvestgationHIVComment], [StaffInvestgationHBSAGType], [StaffInvestgationHBSAGComment], [StaffInvestgationANTIHCVType], [StaffInvestgationANTIHCVComment], [StaffInvestgationHIVType], [StaffInvestgationHIVComment], [BaseLineDate], [FirstFolowUpDate], [SecandFolowUpDate], [ThirdFolowUpDate], [BaseLineComment], [FirstFolowUpComment], [SecandFolowUpComment], [ThirdFolowUpComment], [SourceOfExposureId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [EmpID], [EmpFullName], [StaffInvestgationHBSAbType], [StaffInvestgationHBSAbComment], [Incident_Details], [injury_Depth], [Material_volume], [Was_fluid_injected], [skin_Condition], [PPE], [HBV_Staff_Vaccine], [Staff_HBV_TestResult], [Staff_HBV_TestDate], [Post_Exposure_Management], [Post_Exposure_Management_Yes], [Post_Exposure_Management_No], [Patient_HbsAg_Result], [Patient_HbsAg_TestDate], [Patient_HCV_ELISA_Result], [Patient_HCV_ELISA_TestDate], [Patient_HIV_ELISA_Result], [Patient_HIV_ELISA_TestDate], [Ishighrisk], [Ishighrisk_Desc], [FollowUp_BaseLine_Date], [FollowUp_BaseLine_HCV], [FollowUp_BaseLine_HBsAg], [FollowUp_BaseLine_HIV], [FollowUp_6W_Date], [FollowUp_6W_HCV], [FollowUp_6W_HBsAg], [FollowUp_6W_HIV], [FollowUp_3M_Date], [FollowUp_3M_HCV], [FollowUp_3M_HBsAg], [FollowUp_3M_HIV], [FollowUp_6M_Date], [FollowUp_6M_HCV], [FollowUp_6M_HBsAg], [FollowUp_6M_HIV], [FollowUp_12M_Date], [FollowUp_12M_HCV], [FollowUp_12M_HBsAg], [FollowUp_12M_HIV], [Treatment_HBIG_Dose1], [Treatment_HBIG_Dose1_Date], [Treatment_HBIG_Dose1_Comment], [Treatment_HBIG_Dose2], [Treatment_HBIG_Dose2_Date], [Treatment_HBIG_Dose2_Comment], [Treatment_HBV_Dose1], [Treatment_HBV_Dose1_Date], [Treatment_HBV_Dose1_Comment], [Treatment_HBV_Dose2], [Treatment_HBV_Dose2_Date], [Treatment_HBV_Dose2_Comment], [Treatment_HBV_Dose3], [Treatment_HBV_Dose3_Date], [Treatment_HBV_Dose3_Comment], [HIV_antiretroviral1], [HIV_antiretroviral2], [HIV_antiretroviral3], [IncidentDate], [CompanyID], [BloodProductRecipient], [ElevatedEnzymes], [Dialysis], [Hemophilia], [Other], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[BloodExposure];
SET IDENTITY_INSERT [InfectionControl].[BloodExposure] OFF;
GO

PRINT 'Migrating [InfectionControl].[Element]...';
SET IDENTITY_INSERT [InfectionControl].[Element] ON;
INSERT INTO [InfectionControl].[Element] ([Id], [NameAr], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameKa], [TenantId])
SELECT [Id], [NameAr], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameKa], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[Element];
SET IDENTITY_INSERT [InfectionControl].[Element] OFF;
GO

PRINT 'Migrating [InfectionControl].[Emp_OtherVaccination_Details]...';
SET IDENTITY_INSERT [InfectionControl].[Emp_OtherVaccination_Details] ON;
INSERT INTO [InfectionControl].[Emp_OtherVaccination_Details] ([Id], [MasterID], [infDate], [TenantId])
SELECT [Id], [MasterID], [infDate], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[Emp_OtherVaccination_Details];
SET IDENTITY_INSERT [InfectionControl].[Emp_OtherVaccination_Details] OFF;
GO

PRINT 'Migrating [InfectionControl].[Emp_OtherVaccination_master]...';
SET IDENTITY_INSERT [InfectionControl].[Emp_OtherVaccination_master] ON;
INSERT INTO [InfectionControl].[Emp_OtherVaccination_master] ([Id], [EmpID], [CHDate1], [CHDate2], [MenDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BranchId], [TenantId])
SELECT [Id], [EmpID], [CHDate1], [CHDate2], [MenDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BranchId], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[Emp_OtherVaccination_master];
SET IDENTITY_INSERT [InfectionControl].[Emp_OtherVaccination_master] OFF;
GO

PRINT 'Migrating [InfectionControl].[EmployeesCovid_19Detail]...';
SET IDENTITY_INSERT [InfectionControl].[EmployeesCovid_19Detail] ON;
INSERT INTO [InfectionControl].[EmployeesCovid_19Detail] ([ID], [MasterID], [VaccineNumber], [VaccineType], [VaccineDate], [Executed], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [PreviousAntibioticsPeriod], [CompanyID], [path], [AccptedByCountryLowNo], [ExcutedDate], [TenantId])
SELECT [ID], [MasterID], [VaccineNumber], [VaccineType], [VaccineDate], [Executed], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [PreviousAntibioticsPeriod], [CompanyID], [path], [AccptedByCountryLowNo], [ExcutedDate], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[EmployeesCovid_19Detail];
SET IDENTITY_INSERT [InfectionControl].[EmployeesCovid_19Detail] OFF;
GO

PRINT 'Migrating [InfectionControl].[EmployeesCovid_19Master]...';
SET IDENTITY_INSERT [InfectionControl].[EmployeesCovid_19Master] ON;
INSERT INTO [InfectionControl].[EmployeesCovid_19Master] ([ID], [Vaccination], [AccptedByCountryLow], [PCRTestRequired], [PCRPlannedDate], [PCRExecutedDate], [PCRResulte], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [PreviousAntibioticsPeriod], [CompanyID], [EmployeeId], [TreatmentDone], [PCRTestPath], [TenantId])
SELECT [ID], [Vaccination], [AccptedByCountryLow], [PCRTestRequired], [PCRPlannedDate], [PCRExecutedDate], [PCRResulte], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [PreviousAntibioticsPeriod], [CompanyID], [EmployeeId], [TreatmentDone], [PCRTestPath], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[EmployeesCovid_19Master];
SET IDENTITY_INSERT [InfectionControl].[EmployeesCovid_19Master] OFF;
GO

PRINT 'Migrating [InfectionControl].[EmployeesVaccinations]...';
SET IDENTITY_INSERT [InfectionControl].[EmployeesVaccinations] ON;
INSERT INTO [InfectionControl].[EmployeesVaccinations] ([ID], [EmployeID], [TypeOfVaccination], [FirstDose], [SecondDose], [ThirdDose], [FourthDose], [FifthDose], [SixthDose], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [CompanyID], [TenantId])
SELECT [ID], [EmployeID], [TypeOfVaccination], [FirstDose], [SecondDose], [ThirdDose], [FourthDose], [FifthDose], [SixthDose], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[EmployeesVaccinations];
SET IDENTITY_INSERT [InfectionControl].[EmployeesVaccinations] OFF;
GO

PRINT 'Migrating [InfectionControl].[GroupElements]...';
SET IDENTITY_INSERT [InfectionControl].[GroupElements] ON;
INSERT INTO [InfectionControl].[GroupElements] ([ID], [GroupId], [ElementId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [ElementOrder], [CompanyID], [TenantId])
SELECT [ID], [GroupId], [ElementId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [ElementOrder], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[GroupElements];
SET IDENTITY_INSERT [InfectionControl].[GroupElements] OFF;
GO

PRINT 'Migrating [InfectionControl].[Group]...';
SET IDENTITY_INSERT [InfectionControl].[Group] ON;
INSERT INTO [InfectionControl].[Group] ([ID], [Code], [NameAr], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameKa], [TenantId])
SELECT [ID], [Code], [NameAr], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameKa], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[Group];
SET IDENTITY_INSERT [InfectionControl].[Group] OFF;
GO

PRINT 'Migrating [InfectionControl].[HBV_Vaccination]...';
SET IDENTITY_INSERT [InfectionControl].[HBV_Vaccination] ON;
INSERT INTO [InfectionControl].[HBV_Vaccination] ([id], [EmpID], [InitDose1Date], [InitDose2Date], [InitDose3Date], [InitCheck], [Test1Dose1Date], [Test1Dose2Date], [Test1Dose3Date], [Test1Check], [Test2Dose1Date], [Test2Dose2Date], [Test2Dose3Date], [Test2Check], [Test3Dose1Date], [Test3Dose2Date], [Test3Dose3Date], [Test3Check], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [id], [EmpID], [InitDose1Date], [InitDose2Date], [InitDose3Date], [InitCheck], [Test1Dose1Date], [Test1Dose2Date], [Test1Dose3Date], [Test1Check], [Test2Dose1Date], [Test2Dose2Date], [Test2Dose3Date], [Test2Check], [Test3Dose1Date], [Test3Dose2Date], [Test3Dose3Date], [Test3Check], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[HBV_Vaccination];
SET IDENTITY_INSERT [InfectionControl].[HBV_Vaccination] OFF;
GO

PRINT 'Migrating [InfectionControl].[HandHigineDet]...';
SET IDENTITY_INSERT [InfectionControl].[HandHigineDet] ON;
INSERT INTO [InfectionControl].[HandHigineDet] ([Id], [HandHigineID], [ProfCat], [EmpID], [indecation], [HHaction], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [gloves], [TenantId])
SELECT [Id], [HandHigineID], [ProfCat], [EmpID], [indecation], [HHaction], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [gloves], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[HandHigineDet];
SET IDENTITY_INSERT [InfectionControl].[HandHigineDet] OFF;
GO

PRINT 'Migrating [InfectionControl].[HandHigine]...';
SET IDENTITY_INSERT [InfectionControl].[HandHigine] ON;
INSERT INTO [InfectionControl].[HandHigine] ([Id], [service], [date], [wardID], [DeptID], [StartTime], [Endtime], [SessionDuration], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BranchID], [Facility], [TenantId])
SELECT [Id], [service], [date], [wardID], [DeptID], [StartTime], [Endtime], [SessionDuration], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BranchID], [Facility], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[HandHigine];
SET IDENTITY_INSERT [InfectionControl].[HandHigine] OFF;
GO

PRINT 'Migrating [InfectionControl].[ICRelatedSvsEvaluation]...';
SET IDENTITY_INSERT [InfectionControl].[ICRelatedSvsEvaluation] ON;
INSERT INTO [InfectionControl].[ICRelatedSvsEvaluation] ([Id], [AdmitedId], [ServiceId], [CreationDate], [CreatedBy], [TenantId])
SELECT [Id], [AdmitedId], [ServiceId], [CreationDate], [CreatedBy], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[ICRelatedSvsEvaluation];
SET IDENTITY_INSERT [InfectionControl].[ICRelatedSvsEvaluation] OFF;
GO

PRINT 'Migrating [InfectionControl].[ICUServices]...';
SET IDENTITY_INSERT [InfectionControl].[ICUServices] ON;
INSERT INTO [InfectionControl].[ICUServices] ([Id], [CVC], [Ventilator], [Drain], [ServiceID], [TenantId])
SELECT [Id], [CVC], [Ventilator], [Drain], [ServiceID], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[ICUServices];
SET IDENTITY_INSERT [InfectionControl].[ICUServices] OFF;
GO

PRINT 'Migrating [InfectionControl].[IncidentReportReview]...';
SET IDENTITY_INSERT [InfectionControl].[IncidentReportReview] ON;
INSERT INTO [InfectionControl].[IncidentReportReview] ([ID], [OVRFormID], [IncidenceCategory], [RiskScoring], [ReportingTimeGap], [IncidenceType], [UnderlyingReason], [Repetition], [Acceptable], [CreateBy], [CreateDate], [Lastrecurrence], [OverQ], [OverQRtextWhy], [Empid], [reportingdepartmentId], [relateddepartmentId], [CompanyID], [OtherIncidenceType], [OtherUnderlyingReason], [SatisfactoryCorrective], [SatisfactoryPreventive], [TenantId])
SELECT [ID], [OVRFormID], [IncidenceCategory], [RiskScoring], [ReportingTimeGap], [IncidenceType], [UnderlyingReason], [Repetition], [Acceptable], [CreateBy], [CreateDate], [Lastrecurrence], [OverQ], [OverQRtextWhy], [Empid], [reportingdepartmentId], [relateddepartmentId], [CompanyID], [OtherIncidenceType], [OtherUnderlyingReason], [SatisfactoryCorrective], [SatisfactoryPreventive], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[IncidentReportReview];
SET IDENTITY_INSERT [InfectionControl].[IncidentReportReview] OFF;
GO

PRINT 'Migrating [InfectionControl].[InfectionEvaluationDetail]...';
SET IDENTITY_INSERT [InfectionControl].[InfectionEvaluationDetail] ON;
INSERT INTO [InfectionControl].[InfectionEvaluationDetail] ([Id], [InfectionEvaluationHeader_ID], [Element_Id], [Result], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [groupID], [ElementChar], [Note], [CompanyID], [TenantId])
SELECT [Id], [InfectionEvaluationHeader_ID], [Element_Id], [Result], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [groupID], [ElementChar], [Note], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[InfectionEvaluationDetail];
SET IDENTITY_INSERT [InfectionControl].[InfectionEvaluationDetail] OFF;
GO

PRINT 'Migrating [InfectionControl].[InfectionEvaluationHeader]...';
SET IDENTITY_INSERT [InfectionControl].[InfectionEvaluationHeader] ON;
INSERT INTO [InfectionControl].[InfectionEvaluationHeader] ([Id], [Area_ID], [DateOfInspection], [TotalScore], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Area_ID], [DateOfInspection], [TotalScore], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[InfectionEvaluationHeader];
SET IDENTITY_INSERT [InfectionControl].[InfectionEvaluationHeader] OFF;
GO

PRINT 'Migrating [InfectionControl].[KPIs]...';
SET IDENTITY_INSERT [InfectionControl].[KPIs] ON;
INSERT INTO [InfectionControl].[KPIs] ([ID], [KPIsCategory], [KPIName], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [PreviousAntibioticsPeriod], [CompanyID], [KPIFormula], [ICUServiceId], [TenantId])
SELECT [ID], [KPIsCategory], [KPIName], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [PreviousAntibioticsPeriod], [CompanyID], [KPIFormula], [ICUServiceId], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[KPIs];
SET IDENTITY_INSERT [InfectionControl].[KPIs] OFF;
GO

PRINT 'Migrating [InfectionControl].[MARSA]...';
SET IDENTITY_INSERT [InfectionControl].[MARSA] ON;
INSERT INTO [InfectionControl].[MARSA] ([Id], [PatientId], [EmployeeId], [IsEmployee], [IPNumber], [EntryDate], [EntryLocation], [EntryNumber], [CurrentEntryLocation], [HasExEntry], [ExEntryDate], [HasPreviousMARSA], [PreviousAntibiotics], [MedicalActions], [Cannula], [CentralCannula], [UrinaryCatheter], [HickmanLine], [Other], [OperativeActions], [OldHistory], [PatientCondition], [Contamination], [FirstCultureType], [FirstCultureDate], [FirstMicrobeType], [FirstReportNumber], [FirstReportDate], [ReportDepartment], [AntibiotcType], [AntibioticStartDate], [AntibioticEndDate], [SecondCultureType], [SecondCultureDate], [SecondMicrobeType], [SecondReportNumber], [SecondReportDate], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [PreviousAntibioticsPeriod], [CompanyID], [TenantId])
SELECT [Id], [PatientId], [EmployeeId], [IsEmployee], [IPNumber], [EntryDate], [EntryLocation], [EntryNumber], [CurrentEntryLocation], [HasExEntry], [ExEntryDate], [HasPreviousMARSA], [PreviousAntibiotics], [MedicalActions], [Cannula], [CentralCannula], [UrinaryCatheter], [HickmanLine], [Other], [OperativeActions], [OldHistory], [PatientCondition], [Contamination], [FirstCultureType], [FirstCultureDate], [FirstMicrobeType], [FirstReportNumber], [FirstReportDate], [ReportDepartment], [AntibiotcType], [AntibioticStartDate], [AntibioticEndDate], [SecondCultureType], [SecondCultureDate], [SecondMicrobeType], [SecondReportNumber], [SecondReportDate], [CreationDate], [CreatedBy], [ModificationDate], [ModifiedBy], [PreviousAntibioticsPeriod], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[MARSA];
SET IDENTITY_INSERT [InfectionControl].[MARSA] OFF;
GO

PRINT 'Migrating [InfectionControl].[NotificationsAcupuncture]...';
SET IDENTITY_INSERT [InfectionControl].[NotificationsAcupuncture] ON;
INSERT INTO [InfectionControl].[NotificationsAcupuncture] ([ID], [Patient_ID], [Employee_Id], [job], [Workplace], [IncidentPlace], [IncidentNumber], [IncidentDate], [IncidentTime], [WorkduringIncident], [HowIncidenthappened], [Affectedbodypart], [positionofvaccinations], [AIDS], [hepatitis], [analysisNote], [Replayaftertwomonths], [infectioncontrolReplay], [IsOppose], [Date], [UserId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Department], [TypeofIncident], [Positionanalysis], [DateofTest], [CompanyID], [TenantId])
SELECT [ID], [Patient_ID], [Employee_Id], [job], [Workplace], [IncidentPlace], [IncidentNumber], [IncidentDate], [IncidentTime], [WorkduringIncident], [HowIncidenthappened], [Affectedbodypart], [positionofvaccinations], [AIDS], [hepatitis], [analysisNote], [Replayaftertwomonths], [infectioncontrolReplay], [IsOppose], [Date], [UserId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Department], [TypeofIncident], [Positionanalysis], [DateofTest], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[NotificationsAcupuncture];
SET IDENTITY_INSERT [InfectionControl].[NotificationsAcupuncture] OFF;
GO

PRINT 'Migrating [InfectionControl].[OVRForm]...';
SET IDENTITY_INSERT [InfectionControl].[OVRForm] ON;
INSERT INTO [InfectionControl].[OVRForm] ([ID], [Date_of_Incidnt], [Time_of_Incidnt], [Brief_Description], [Incidence_resulted], [Reported_by_name], [Reported_by_Department], [Reported_StaffID], [FileUpload], [Branch], [Department], [Code], [CreatedDate], [CreatedBy], [location], [CompanyID], [exactlocation], [TenantId])
SELECT [ID], [Date_of_Incidnt], [Time_of_Incidnt], [Brief_Description], [Incidence_resulted], [Reported_by_name], [Reported_by_Department], [Reported_StaffID], [FileUpload], [Branch], [Department], [Code], [CreatedDate], [CreatedBy], [location], [CompanyID], [exactlocation], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[OVRForm];
SET IDENTITY_INSERT [InfectionControl].[OVRForm] OFF;
GO

PRINT 'Migrating [InfectionControl].[PersonInvolved]...';
SET IDENTITY_INSERT [InfectionControl].[PersonInvolved] ON;
INSERT INTO [InfectionControl].[PersonInvolved] ([ID], [Type], [EmployeeId], [SubDepartmentId], [Staffid], [PatientId], [MRN], [MobilePatient], [SpecialtyPatient], [VisitorName], [MobileVisitor], [OVRFormID], [CreatedDate], [CreatedBy], [TenantId])
SELECT [ID], [Type], [EmployeeId], [SubDepartmentId], [Staffid], [PatientId], [MRN], [MobilePatient], [SpecialtyPatient], [VisitorName], [MobileVisitor], [OVRFormID], [CreatedDate], [CreatedBy], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[PersonInvolved];
SET IDENTITY_INSERT [InfectionControl].[PersonInvolved] OFF;
GO

PRINT 'Migrating [InfectionControl].[PreventiveAction]...';
SET IDENTITY_INSERT [InfectionControl].[PreventiveAction] ON;
INSERT INTO [InfectionControl].[PreventiveAction] ([ID], [EmployeeId], [SubDepartmentId], [Staffid], [Title], [OVRFormID], [CreatedDate], [CreatedBy], [CorrectiveDesc], [type], [TenantId])
SELECT [ID], [EmployeeId], [SubDepartmentId], [Staffid], [Title], [OVRFormID], [CreatedDate], [CreatedBy], [CorrectiveDesc], [type], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[PreventiveAction];
SET IDENTITY_INSERT [InfectionControl].[PreventiveAction] OFF;
GO

PRINT 'Migrating [InfectionControl].[PreventiveMedicine]...';
SET IDENTITY_INSERT [InfectionControl].[PreventiveMedicine] ON;
INSERT INTO [InfectionControl].[PreventiveMedicine] ([Id], [Type], [PersonID], [NationalID], [phonenumber], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Gender], [Job], [DateOfBirth], [Age], [MaritalStatusId], [HealthManagement], [Healthoffice], [CityId], [street], [District], [Village], [DiscoveredDate], [Primarydiagnosis], [Finaldiagnosis], [DiseaseDate], [AdmissionHospitalDate], [ReportDate], [ExitDate], [patientdischargeStatus], [firstsample], [firstTypeoflaboratoryexamination], [firstLaboratoryresults], [Secondsample], [SecondTypeoflaboratoryexamination], [SecondLaboratoryresults], [Code], [CompanyID], [TenantId])
SELECT [Id], [Type], [PersonID], [NationalID], [phonenumber], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Gender], [Job], [DateOfBirth], [Age], [MaritalStatusId], [HealthManagement], [Healthoffice], [CityId], [street], [District], [Village], [DiscoveredDate], [Primarydiagnosis], [Finaldiagnosis], [DiseaseDate], [AdmissionHospitalDate], [ReportDate], [ExitDate], [patientdischargeStatus], [firstsample], [firstTypeoflaboratoryexamination], [firstLaboratoryresults], [Secondsample], [SecondTypeoflaboratoryexamination], [SecondLaboratoryresults], [Code], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[PreventiveMedicine];
SET IDENTITY_INSERT [InfectionControl].[PreventiveMedicine] OFF;
GO

PRINT 'Migrating [InfectionControl].[StaffHealthCareHepatitisB]...';
SET IDENTITY_INSERT [InfectionControl].[StaffHealthCareHepatitisB] ON;
INSERT INTO [InfectionControl].[StaffHealthCareHepatitisB] ([id], [EmpID], [HBVaccinebeforehiring], [NPF], [NEF], [NPS], [NES], [NPT], [NET], [UPF], [UEF], [ResaultID], [txtResponde], [UNPF], [UNEF], [UNPS], [UNES], [UNPT], [UNET], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [FileName], [TenantId])
SELECT [id], [EmpID], [HBVaccinebeforehiring], [NPF], [NEF], [NPS], [NES], [NPT], [NET], [UPF], [UEF], [ResaultID], [txtResponde], [UNPF], [UNEF], [UNPS], [UNES], [UNPT], [UNET], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [FileName], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[StaffHealthCareHepatitisB];
SET IDENTITY_INSERT [InfectionControl].[StaffHealthCareHepatitisB] OFF;
GO

PRINT 'Migrating [InfectionControl].[StaffHealthCareProgramDetails]...';
SET IDENTITY_INSERT [InfectionControl].[StaffHealthCareProgramDetails] ON;
INSERT INTO [InfectionControl].[StaffHealthCareProgramDetails] ([Id], [StaffHealthCareProgramId], [DiseaseMasterId], [StatuesId], [Comment], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Category], [StatusDate], [StatusID2], [StatusDate2], [Comment2], [StatusID3], [StatusDate3], [Comment3], [BeforeEmployment], [TenantId])
SELECT [Id], [StaffHealthCareProgramId], [DiseaseMasterId], [StatuesId], [Comment], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Category], [StatusDate], [StatusID2], [StatusDate2], [Comment2], [StatusID3], [StatusDate3], [Comment3], [BeforeEmployment], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[StaffHealthCareProgramDetails];
SET IDENTITY_INSERT [InfectionControl].[StaffHealthCareProgramDetails] OFF;
GO

PRINT 'Migrating [InfectionControl].[StaffHealthCareProgramDetails_Vac]...';
SET IDENTITY_INSERT [InfectionControl].[StaffHealthCareProgramDetails_Vac] ON;
INSERT INTO [InfectionControl].[StaffHealthCareProgramDetails_Vac] ([Id], [StaffHealthCareProgramId], [Vaccinationd], [StatuesId], [Comment], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [StatusDate], [StatusID2], [StatusDate2], [Comment2], [StatusID3], [StatusDate3], [Comment3], [BeforeEmployment], [TenantId])
SELECT [Id], [StaffHealthCareProgramId], [Vaccinationd], [StatuesId], [Comment], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [StatusDate], [StatusID2], [StatusDate2], [Comment2], [StatusID3], [StatusDate3], [Comment3], [BeforeEmployment], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[StaffHealthCareProgramDetails_Vac];
SET IDENTITY_INSERT [InfectionControl].[StaffHealthCareProgramDetails_Vac] OFF;
GO

PRINT 'Migrating [InfectionControl].[StaffHealthCareProgram]...';
SET IDENTITY_INSERT [InfectionControl].[StaffHealthCareProgram] ON;
INSERT INTO [InfectionControl].[StaffHealthCareProgram] ([Id], [SubDeptId], [EmployeeId], [Comment], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [SubDeptId], [EmployeeId], [Comment], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[StaffHealthCareProgram];
SET IDENTITY_INSERT [InfectionControl].[StaffHealthCareProgram] OFF;
GO

PRINT 'Migrating [InfectionControl].[Vap]...';
SET IDENTITY_INSERT [InfectionControl].[Vap] ON;
INSERT INTO [InfectionControl].[Vap] ([Id], [PatientId], [DoctorId], [Elevatingtheheadofthebed], [Manageventilatedpatients], [Interruptsedationdaily], [Theendotrachealtube], [Contaminatedcondensate], [Theventilatorcircuit], [Performoralcare], [Performspontaneous], [Provideearlyexercise], [Minimizepooling], [TransActionDate], [CreatedDate], [CreatedBy], [LastModifieDate], [ModifiedBy], [CompanyID], [IPOP], [TenantId])
SELECT [Id], [PatientId], [DoctorId], [Elevatingtheheadofthebed], [Manageventilatedpatients], [Interruptsedationdaily], [Theendotrachealtube], [Contaminatedcondensate], [Theventilatorcircuit], [Performoralcare], [Performspontaneous], [Provideearlyexercise], [Minimizepooling], [TransActionDate], [CreatedDate], [CreatedBy], [LastModifieDate], [ModifiedBy], [CompanyID], [IPOP], @TenantId
FROM [SunCity_Clinics].[InfectionControl].[Vap];
SET IDENTITY_INSERT [InfectionControl].[Vap] OFF;
GO

PRINT 'Migrating [Laboratory].[AllowUsersToShowReports]...';
SET IDENTITY_INSERT [Laboratory].[AllowUsersToShowReports] ON;
INSERT INTO [Laboratory].[AllowUsersToShowReports] ([ID], [EmployeeID], [ResultEntryHeaderD], [CreatedBy], [CreationDate], [CompanyID], [TenantId])
SELECT [ID], [EmployeeID], [ResultEntryHeaderD], [CreatedBy], [CreationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[AllowUsersToShowReports];
SET IDENTITY_INSERT [Laboratory].[AllowUsersToShowReports] OFF;
GO

PRINT 'Migrating [Laboratory].[Antibiotics]...';
SET IDENTITY_INSERT [Laboratory].[Antibiotics] ON;
INSERT INTO [Laboratory].[Antibiotics] ([AntibioticsID], [AntibioticName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [AntibioticsID], [AntibioticName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[Antibiotics];
SET IDENTITY_INSERT [Laboratory].[Antibiotics] OFF;
GO

PRINT 'Migrating [Laboratory].[ContainerType]...';
SET IDENTITY_INSERT [Laboratory].[ContainerType] ON;
INSERT INTO [Laboratory].[ContainerType] ([Id], [NameAr], [NameEn], [Description], [CreatedBy], [CreatedDate], [ModifiedBy], [TenantId])
SELECT [Id], [NameAr], [NameEn], [Description], [CreatedBy], [CreatedDate], [ModifiedBy], @TenantId
FROM [SunCity_Clinics].[Laboratory].[ContainerType];
SET IDENTITY_INSERT [Laboratory].[ContainerType] OFF;
GO

PRINT 'Migrating [Laboratory].[ExternalAgencies]...';
SET IDENTITY_INSERT [Laboratory].[ExternalAgencies] ON;
INSERT INTO [Laboratory].[ExternalAgencies] ([Id], [HospitalId], [PatientId], [CustomerId], [NationalityId], [IsActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [HospitalId], [PatientId], [CustomerId], [NationalityId], [IsActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[ExternalAgencies];
SET IDENTITY_INSERT [Laboratory].[ExternalAgencies] OFF;
GO

PRINT 'Migrating [Laboratory].[FarmType]...';
SET IDENTITY_INSERT [Laboratory].[FarmType] ON;
INSERT INTO [Laboratory].[FarmType] ([id], [NameAr], [NameEn], [Active], [CompanyID], [TenantId])
SELECT [id], [NameAr], [NameEn], [Active], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[FarmType];
SET IDENTITY_INSERT [Laboratory].[FarmType] OFF;
GO

PRINT 'Migrating [Laboratory].[Farms]...';
SET IDENTITY_INSERT [Laboratory].[Farms] ON;
INSERT INTO [Laboratory].[Farms] ([id], [NameAr], [NameEn], [FarmTypeId], [ServiceID], [CompanyID], [TenantId])
SELECT [id], [NameAr], [NameEn], [FarmTypeId], [ServiceID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[Farms];
SET IDENTITY_INSERT [Laboratory].[Farms] OFF;
GO

PRINT 'Migrating [Laboratory].[LabAnalyzers]...';
SET IDENTITY_INSERT [Laboratory].[LabAnalyzers] ON;
INSERT INTO [Laboratory].[LabAnalyzers] ([Id], [AnalyzerName], [Isactive], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [AnalyzerName], [Isactive], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[LabAnalyzers];
SET IDENTITY_INSERT [Laboratory].[LabAnalyzers] OFF;
GO

PRINT 'Migrating [Laboratory].[LabServiceItemLinkMaster]...';
SET IDENTITY_INSERT [Laboratory].[LabServiceItemLinkMaster] ON;
INSERT INTO [Laboratory].[LabServiceItemLinkMaster] ([Id], [ServiceId], [ItemId], [UnitUsed], [Wastage], [UnitCost], [WastageCost], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [ServiceId], [ItemId], [UnitUsed], [Wastage], [UnitCost], [WastageCost], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[LabServiceItemLinkMaster];
SET IDENTITY_INSERT [Laboratory].[LabServiceItemLinkMaster] OFF;
GO

PRINT 'Migrating [Laboratory].[LabServicesPeriod]...';
SET IDENTITY_INSERT [Laboratory].[LabServicesPeriod] ON;
INSERT INTO [Laboratory].[LabServicesPeriod] ([ID], [ServiceID], [Period], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [PeriodType], [CompanyID], [TenantId])
SELECT [ID], [ServiceID], [Period], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [PeriodType], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[LabServicesPeriod];
SET IDENTITY_INSERT [Laboratory].[LabServicesPeriod] OFF;
GO

PRINT 'Migrating [Laboratory].[LabTestItem]...';
SET IDENTITY_INSERT [Laboratory].[LabTestItem] ON;
INSERT INTO [Laboratory].[LabTestItem] ([Id], [ServiceId], [ResultValueId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [ServiceId], [ResultValueId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[LabTestItem];
SET IDENTITY_INSERT [Laboratory].[LabTestItem] OFF;
GO

PRINT 'Migrating [Laboratory].[LabUnit]...';
SET IDENTITY_INSERT [Laboratory].[LabUnit] ON;
INSERT INTO [Laboratory].[LabUnit] ([Id], [UnitName], [CompanyID], [TenantId])
SELECT [Id], [UnitName], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[LabUnit];
SET IDENTITY_INSERT [Laboratory].[LabUnit] OFF;
GO

PRINT 'Migrating [Laboratory].[LaboratorySetting]...';
SET IDENTITY_INSERT [Laboratory].[LaboratorySetting] ON;
INSERT INTO [Laboratory].[LaboratorySetting] ([Id], [EmpID], [Type], [CompanyID], [TenantId])
SELECT [Id], [EmpID], [Type], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[LaboratorySetting];
SET IDENTITY_INSERT [Laboratory].[LaboratorySetting] OFF;
GO

PRINT 'Migrating [Laboratory].[LabsDevices]...';
INSERT INTO [Laboratory].[LabsDevices] ([LabCode], [DeviceId], [CompanyID], [BranchId], [TenantId])
SELECT [LabCode], [DeviceId], [CompanyID], [BranchId], @TenantId
FROM [SunCity_Clinics].[Laboratory].[LabsDevices];
GO

PRINT 'Migrating [Laboratory].[LabsStores]...';
INSERT INTO [Laboratory].[LabsStores] ([LabCode], [StoreId], [CompanyID], [TenantId])
SELECT [LabCode], [StoreId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[LabsStores];
GO

PRINT 'Migrating [Laboratory].[LabsTechnicans]...';
SET IDENTITY_INSERT [Laboratory].[LabsTechnicans] ON;
INSERT INTO [Laboratory].[LabsTechnicans] ([Id], [LabCode], [TechnicanId], [DayId], [CompanyID], [TenantId])
SELECT [Id], [LabCode], [TechnicanId], [DayId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[LabsTechnicans];
SET IDENTITY_INSERT [Laboratory].[LabsTechnicans] OFF;
GO

PRINT 'Migrating [Laboratory].[Labs]...';
SET IDENTITY_INSERT [Laboratory].[Labs] ON;
INSERT INTO [Laboratory].[Labs] ([LabCode], [LabName], [LabAdminId], [Location], [SessionId], [labType], [IsActive], [CompanyID], [LabNameAr], [Code], [TenantId])
SELECT [LabCode], [LabName], [LabAdminId], [Location], [SessionId], [labType], [IsActive], [CompanyID], [LabNameAr], [Code], @TenantId
FROM [SunCity_Clinics].[Laboratory].[Labs];
SET IDENTITY_INSERT [Laboratory].[Labs] OFF;
GO

PRINT 'Migrating [Laboratory].[MergeSamples]...';
SET IDENTITY_INSERT [Laboratory].[MergeSamples] ON;
INSERT INTO [Laboratory].[MergeSamples] ([Id], [SampleNumberId], [MergedWithSampleId], [NewSampleNumber], [CreateDate], [CreatedBy], [CompanyID], [TenantId])
SELECT [Id], [SampleNumberId], [MergedWithSampleId], [NewSampleNumber], [CreateDate], [CreatedBy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[MergeSamples];
SET IDENTITY_INSERT [Laboratory].[MergeSamples] OFF;
GO

PRINT 'Migrating [Laboratory].[OrganismAntibioticSensitivity]...';
SET IDENTITY_INSERT [Laboratory].[OrganismAntibioticSensitivity] ON;
INSERT INTO [Laboratory].[OrganismAntibioticSensitivity] ([Id], [SectionId], [TestId], [OrganismId], [AntibioticId], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [SectionId], [TestId], [OrganismId], [AntibioticId], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[OrganismAntibioticSensitivity];
SET IDENTITY_INSERT [Laboratory].[OrganismAntibioticSensitivity] OFF;
GO

PRINT 'Migrating [Laboratory].[Organisms]...';
SET IDENTITY_INSERT [Laboratory].[Organisms] ON;
INSERT INTO [Laboratory].[Organisms] ([OrganismsID], [OrganismsName], [Frequent], [LessFrequent], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [OrganismsID], [OrganismsName], [Frequent], [LessFrequent], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[Organisms];
SET IDENTITY_INSERT [Laboratory].[Organisms] OFF;
GO

PRINT 'Migrating [Laboratory].[PatientFarm_Details]...';
SET IDENTITY_INSERT [Laboratory].[PatientFarm_Details] ON;
INSERT INTO [Laboratory].[PatientFarm_Details] ([id], [PatientFarmID], [AntibioticID], [TestResult], [AntibioticTestDate], [TenantId])
SELECT [id], [PatientFarmID], [AntibioticID], [TestResult], [AntibioticTestDate], @TenantId
FROM [SunCity_Clinics].[Laboratory].[PatientFarm_Details];
SET IDENTITY_INSERT [Laboratory].[PatientFarm_Details] OFF;
GO

PRINT 'Migrating [Laboratory].[Patient_Farms]...';
SET IDENTITY_INSERT [Laboratory].[Patient_Farms] ON;
INSERT INTO [Laboratory].[Patient_Farms] ([id], [PatientID], [TestDate], [FarmID], [PatientType], [CompanyID], [TenantId])
SELECT [id], [PatientID], [TestDate], [FarmID], [PatientType], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[Patient_Farms];
SET IDENTITY_INSERT [Laboratory].[Patient_Farms] OFF;
GO

PRINT 'Migrating [Laboratory].[PermittedStaff]...';
SET IDENTITY_INSERT [Laboratory].[PermittedStaff] ON;
INSERT INTO [Laboratory].[PermittedStaff] ([id], [InvestigationGroup], [ReportEntry], [ReportValidation], [CreatedBy], [CreatedDate], [LastModifiedDate], [LastModifiedBy], [CompanyID], [TenantId])
SELECT [id], [InvestigationGroup], [ReportEntry], [ReportValidation], [CreatedBy], [CreatedDate], [LastModifiedDate], [LastModifiedBy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[PermittedStaff];
SET IDENTITY_INSERT [Laboratory].[PermittedStaff] OFF;
GO

PRINT 'Migrating [Laboratory].[RadResultImages]...';
SET IDENTITY_INSERT [Laboratory].[RadResultImages] ON;
INSERT INTO [Laboratory].[RadResultImages] ([ID], [ResultEntryDetailsID], [ImageName], [TenantId])
SELECT [ID], [ResultEntryDetailsID], [ImageName], @TenantId
FROM [SunCity_Clinics].[Laboratory].[RadResultImages];
SET IDENTITY_INSERT [Laboratory].[RadResultImages] OFF;
GO

PRINT 'Migrating [Laboratory].[RejectSample]...';
SET IDENTITY_INSERT [Laboratory].[RejectSample] ON;
INSERT INTO [Laboratory].[RejectSample] ([Id], [RejectionReasonId], [Other], [SampleEntryId], [TenantId])
SELECT [Id], [RejectionReasonId], [Other], [SampleEntryId], @TenantId
FROM [SunCity_Clinics].[Laboratory].[RejectSample];
SET IDENTITY_INSERT [Laboratory].[RejectSample] OFF;
GO

PRINT 'Migrating [Laboratory].[ResultEntryDetail]...';
SET IDENTITY_INSERT [Laboratory].[ResultEntryDetail] ON;
INSERT INTO [Laboratory].[ResultEntryDetail] ([Id], [ResultEntry_Id], [Value], [TestDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyId], [InvestigationDetailsId], [Remarks], [Comments], [TestDetailsId], [ResultFile], [BranchId], [ValueP], [TenantId])
SELECT [Id], [ResultEntry_Id], [Value], [TestDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyId], [InvestigationDetailsId], [Remarks], [Comments], [TestDetailsId], [ResultFile], [BranchId], [ValueP], @TenantId
FROM [SunCity_Clinics].[Laboratory].[ResultEntryDetail];
SET IDENTITY_INSERT [Laboratory].[ResultEntryDetail] OFF;
GO

PRINT 'Migrating [Laboratory].[ResultEntryDetails_Findings]...';
SET IDENTITY_INSERT [Laboratory].[ResultEntryDetails_Findings] ON;
INSERT INTO [Laboratory].[ResultEntryDetails_Findings] ([Id], [EmpID], [Findings], [Concolusion], [FindingDate], [Status], [ResultEntryID], [FinalFindEmpId], [StatDate], [RevisedById], [FinalApprovedById], [CompanyID], [BranchId], [TenantId])
SELECT [Id], [EmpID], [Findings], [Concolusion], [FindingDate], [Status], [ResultEntryID], [FinalFindEmpId], [StatDate], [RevisedById], [FinalApprovedById], [CompanyID], [BranchId], @TenantId
FROM [SunCity_Clinics].[Laboratory].[ResultEntryDetails_Findings];
SET IDENTITY_INSERT [Laboratory].[ResultEntryDetails_Findings] OFF;
GO

PRINT 'Migrating [Laboratory].[ResultEntry]...';
SET IDENTITY_INSERT [Laboratory].[ResultEntry] ON;
INSERT INTO [Laboratory].[ResultEntry] ([Id], [SectionID], [SampleNo], [PatientId], [ServiceID], [ResultId], [ObservedValue], [TechnicianId], [GrowthOptionId], [Approved], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Comments], [CompanyID], [DeviceID], [NewSampleNo], [BranchId], [TenantId])
SELECT [Id], [SectionID], [SampleNo], [PatientId], [ServiceID], [ResultId], [ObservedValue], [TechnicianId], [GrowthOptionId], [Approved], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Comments], [CompanyID], [DeviceID], [NewSampleNo], [BranchId], @TenantId
FROM [SunCity_Clinics].[Laboratory].[ResultEntry];
SET IDENTITY_INSERT [Laboratory].[ResultEntry] OFF;
GO

PRINT 'Migrating [Laboratory].[ResultRangesDetails]...';
SET IDENTITY_INSERT [Laboratory].[ResultRangesDetails] ON;
INSERT INTO [Laboratory].[ResultRangesDetails] ([Id], [ResultRangesID], [AgeFrom], [AgeTo], [SexId], [ValueFrom], [ValueTo], [MinimumValue], [MaximumValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Description], [TenantId])
SELECT [Id], [ResultRangesID], [AgeFrom], [AgeTo], [SexId], [ValueFrom], [ValueTo], [MinimumValue], [MaximumValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Description], @TenantId
FROM [SunCity_Clinics].[Laboratory].[ResultRangesDetails];
SET IDENTITY_INSERT [Laboratory].[ResultRangesDetails] OFF;
GO

PRINT 'Migrating [Laboratory].[ResultRanges]...';
SET IDENTITY_INSERT [Laboratory].[ResultRanges] ON;
INSERT INTO [Laboratory].[ResultRanges] ([Id], [SectionID], [ResultID], [ResultTypeId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [SectionID], [ResultID], [ResultTypeId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[ResultRanges];
SET IDENTITY_INSERT [Laboratory].[ResultRanges] OFF;
GO

PRINT 'Migrating [Laboratory].[ResultValueDetails]...';
SET IDENTITY_INSERT [Laboratory].[ResultValueDetails] ON;
INSERT INTO [Laboratory].[ResultValueDetails] ([Id], [AgeFrom], [AgeTo], [SexId], [MinimumValue], [MaximumValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ResultValueHeaderID], [TenantId])
SELECT [Id], [AgeFrom], [AgeTo], [SexId], [MinimumValue], [MaximumValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ResultValueHeaderID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[ResultValueDetails];
SET IDENTITY_INSERT [Laboratory].[ResultValueDetails] OFF;
GO

PRINT 'Migrating [Laboratory].[ResultValueHeader]...';
SET IDENTITY_INSERT [Laboratory].[ResultValueHeader] ON;
INSERT INTO [Laboratory].[ResultValueHeader] ([ID], [NameEn], [NameAr], [CompanyID], [TenantId])
SELECT [ID], [NameEn], [NameAr], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[ResultValueHeader];
SET IDENTITY_INSERT [Laboratory].[ResultValueHeader] OFF;
GO

PRINT 'Migrating [Laboratory].[Results]...';
SET IDENTITY_INSERT [Laboratory].[Results] ON;
INSERT INTO [Laboratory].[Results] ([ResultID], [SectionID], [ResultName], [ResultTypeId], [Units], [SIUnits], [ConversionFactor], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TestID], [CompanyID], [TenantId])
SELECT [ResultID], [SectionID], [ResultName], [ResultTypeId], [Units], [SIUnits], [ConversionFactor], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TestID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[Results];
SET IDENTITY_INSERT [Laboratory].[Results] OFF;
GO

PRINT 'Migrating [Laboratory].[SampleCollectionMedia]...';
SET IDENTITY_INSERT [Laboratory].[SampleCollectionMedia] ON;
INSERT INTO [Laboratory].[SampleCollectionMedia] ([Id], [CollectionMediaName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [CollectionMediaName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[SampleCollectionMedia];
SET IDENTITY_INSERT [Laboratory].[SampleCollectionMedia] OFF;
GO

PRINT 'Migrating [Laboratory].[SampleEntry]...';
SET IDENTITY_INSERT [Laboratory].[SampleEntry] ON;
INSERT INTO [Laboratory].[SampleEntry] ([Id], [InvestigationRequestDetailsId], [SpecimenID], [LabId], [SampleNo], [Resample], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TechnicanID], [RejectionReasonId], [Other], [CancelReason], [CancelSample], [IsTakeSampleOutSideHospital], [BranchId], [OutSourceLab], [TenantId])
SELECT [Id], [InvestigationRequestDetailsId], [SpecimenID], [LabId], [SampleNo], [Resample], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TechnicanID], [RejectionReasonId], [Other], [CancelReason], [CancelSample], [IsTakeSampleOutSideHospital], [BranchId], [OutSourceLab], @TenantId
FROM [SunCity_Clinics].[Laboratory].[SampleEntry];
SET IDENTITY_INSERT [Laboratory].[SampleEntry] OFF;
GO

PRINT 'Migrating [Laboratory].[SampleType]...';
SET IDENTITY_INSERT [Laboratory].[SampleType] ON;
INSERT INTO [Laboratory].[SampleType] ([id], [TypeNameEn], [TypeNameAr], [Descr], [CreatedBy], [CreatedDate], [LastModifiedDate], [LastModifiedBy], [CompanyID], [TenantId])
SELECT [id], [TypeNameEn], [TypeNameAr], [Descr], [CreatedBy], [CreatedDate], [LastModifiedDate], [LastModifiedBy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[SampleType];
SET IDENTITY_INSERT [Laboratory].[SampleType] OFF;
GO

PRINT 'Migrating [Laboratory].[SamplesDispatchedDetails]...';
SET IDENTITY_INSERT [Laboratory].[SamplesDispatchedDetails] ON;
INSERT INTO [Laboratory].[SamplesDispatchedDetails] ([Id], [SamplesDispatchedId], [Date], [Time], [SampleNo], [TestId], [PatientId], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [InvestigationDetailID], [TenantId])
SELECT [Id], [SamplesDispatchedId], [Date], [Time], [SampleNo], [TestId], [PatientId], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [InvestigationDetailID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[SamplesDispatchedDetails];
SET IDENTITY_INSERT [Laboratory].[SamplesDispatchedDetails] OFF;
GO

PRINT 'Migrating [Laboratory].[SamplesDispatchedHeader]...';
SET IDENTITY_INSERT [Laboratory].[SamplesDispatchedHeader] ON;
INSERT INTO [Laboratory].[SamplesDispatchedHeader] ([Id], [DispatchNO], [Date], [ExternalAgencyId], [EnteredBy], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [DispatchNO], [Date], [ExternalAgencyId], [EnteredBy], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[SamplesDispatchedHeader];
SET IDENTITY_INSERT [Laboratory].[SamplesDispatchedHeader] OFF;
GO

PRINT 'Migrating [Laboratory].[SamplesReceivedDetails]...';
SET IDENTITY_INSERT [Laboratory].[SamplesReceivedDetails] ON;
INSERT INTO [Laboratory].[SamplesReceivedDetails] ([Id], [SamplesReceivedId], [ExtSampleNo], [TestId], [SampleNo], [CollectedDate], [CollectedTime], [PatientName], [SexId], [DateOfBirth], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [InvestigationDetailID], [IsCollected], [Resample], [LabRequestStatus], [BranchId], [TenantId])
SELECT [Id], [SamplesReceivedId], [ExtSampleNo], [TestId], [SampleNo], [CollectedDate], [CollectedTime], [PatientName], [SexId], [DateOfBirth], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [InvestigationDetailID], [IsCollected], [Resample], [LabRequestStatus], [BranchId], @TenantId
FROM [SunCity_Clinics].[Laboratory].[SamplesReceivedDetails];
SET IDENTITY_INSERT [Laboratory].[SamplesReceivedDetails] OFF;
GO

PRINT 'Migrating [Laboratory].[SamplesReceivedHeader]...';
SET IDENTITY_INSERT [Laboratory].[SamplesReceivedHeader] ON;
INSERT INTO [Laboratory].[SamplesReceivedHeader] ([Id], [ExternalAgencyId], [LabNO], [Date], [ReferanceNO], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [PatientID], [BranchId], [TenantId])
SELECT [Id], [ExternalAgencyId], [LabNO], [Date], [ReferanceNO], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [PatientID], [BranchId], @TenantId
FROM [SunCity_Clinics].[Laboratory].[SamplesReceivedHeader];
SET IDENTITY_INSERT [Laboratory].[SamplesReceivedHeader] OFF;
GO

PRINT 'Migrating [Laboratory].[SamplesTransfere]...';
SET IDENTITY_INSERT [Laboratory].[SamplesTransfere] ON;
INSERT INTO [Laboratory].[SamplesTransfere] ([Id], [FromLabId], [ToLabId], [SampleNumber], [SamplesReceivedId], [TechnicanId], [RecievedTechnicanId], [TenantId])
SELECT [Id], [FromLabId], [ToLabId], [SampleNumber], [SamplesReceivedId], [TechnicanId], [RecievedTechnicanId], @TenantId
FROM [SunCity_Clinics].[Laboratory].[SamplesTransfere];
SET IDENTITY_INSERT [Laboratory].[SamplesTransfere] OFF;
GO

PRINT 'Migrating [Laboratory].[Sections]...';
SET IDENTITY_INSERT [Laboratory].[Sections] ON;
INSERT INTO [Laboratory].[Sections] ([Id], [SectionName], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [InvestigationGroupId], [TenantId])
SELECT [Id], [SectionName], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [InvestigationGroupId], @TenantId
FROM [SunCity_Clinics].[Laboratory].[Sections];
SET IDENTITY_INSERT [Laboratory].[Sections] OFF;
GO

PRINT 'Migrating [Laboratory].[SelectionType]...';
SET IDENTITY_INSERT [Laboratory].[SelectionType] ON;
INSERT INTO [Laboratory].[SelectionType] ([Id], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[SelectionType];
SET IDENTITY_INSERT [Laboratory].[SelectionType] OFF;
GO

PRINT 'Migrating [Laboratory].[Service_ResultValueHeader]...';
SET IDENTITY_INSERT [Laboratory].[Service_ResultValueHeader] ON;
INSERT INTO [Laboratory].[Service_ResultValueHeader] ([id], [Service_Id], [ResultValueHeader_Id], [CompanyID], [TenantId])
SELECT [id], [Service_Id], [ResultValueHeader_Id], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[Service_ResultValueHeader];
SET IDENTITY_INSERT [Laboratory].[Service_ResultValueHeader] OFF;
GO

PRINT 'Migrating [Laboratory].[SpecimenTypeAssociation]...';
SET IDENTITY_INSERT [Laboratory].[SpecimenTypeAssociation] ON;
INSERT INTO [Laboratory].[SpecimenTypeAssociation] ([SpecimenTypeAssociationID], [SpecimenTypesID], [SectionID], [TestID], [SampleTypeId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [SpecimenTypeAssociationID], [SpecimenTypesID], [SectionID], [TestID], [SampleTypeId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[SpecimenTypeAssociation];
SET IDENTITY_INSERT [Laboratory].[SpecimenTypeAssociation] OFF;
GO

PRINT 'Migrating [Laboratory].[SpecimenTypes]...';
SET IDENTITY_INSERT [Laboratory].[SpecimenTypes] ON;
INSERT INTO [Laboratory].[SpecimenTypes] ([SpecimenTypesID], [SpecimenTypesName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Apprivate], [TenantId])
SELECT [SpecimenTypesID], [SpecimenTypesName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Apprivate], @TenantId
FROM [SunCity_Clinics].[Laboratory].[SpecimenTypes];
SET IDENTITY_INSERT [Laboratory].[SpecimenTypes] OFF;
GO

PRINT 'Migrating [Laboratory].[TestDetails]...';
SET IDENTITY_INSERT [Laboratory].[TestDetails] ON;
INSERT INTO [Laboratory].[TestDetails] ([Id], [ComponentId], [Desciption], [UnitId], [AgeFrom], [AgeTo], [Gender], [MinNormalRange], [MaxNormalAge], [PanicResult], [TestId], [CreatedDate], [ModifiedDate], [CreatedBy], [ModifiedBy], [LabTestType], [IsPositive], [Range], [TenantId])
SELECT [Id], [ComponentId], [Desciption], [UnitId], [AgeFrom], [AgeTo], [Gender], [MinNormalRange], [MaxNormalAge], [PanicResult], [TestId], [CreatedDate], [ModifiedDate], [CreatedBy], [ModifiedBy], [LabTestType], [IsPositive], [Range], @TenantId
FROM [SunCity_Clinics].[Laboratory].[TestDetails];
SET IDENTITY_INSERT [Laboratory].[TestDetails] OFF;
GO

PRINT 'Migrating [Laboratory].[TestResultLinkingEntry]...';
SET IDENTITY_INSERT [Laboratory].[TestResultLinkingEntry] ON;
INSERT INTO [Laboratory].[TestResultLinkingEntry] ([Id], [TestResultLinkingId], [ResultId], [ResultOrder], [IsActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [TestResultLinkingId], [ResultId], [ResultOrder], [IsActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[TestResultLinkingEntry];
SET IDENTITY_INSERT [Laboratory].[TestResultLinkingEntry] OFF;
GO

PRINT 'Migrating [Laboratory].[TestResultLinking]...';
SET IDENTITY_INSERT [Laboratory].[TestResultLinking] ON;
INSERT INTO [Laboratory].[TestResultLinking] ([Id], [SectionId], [TestId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [SectionId], [TestId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laboratory].[TestResultLinking];
SET IDENTITY_INSERT [Laboratory].[TestResultLinking] OFF;
GO

PRINT 'Migrating [Laboratory].[Tests]...';
SET IDENTITY_INSERT [Laboratory].[Tests] ON;
INSERT INTO [Laboratory].[Tests] ([Id], [TestName], [Description], [SectionId], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ServiceId], [SampleTypeId], [ContainerTypeId], [TenantId])
SELECT [Id], [TestName], [Description], [SectionId], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ServiceId], [SampleTypeId], [ContainerTypeId], @TenantId
FROM [SunCity_Clinics].[Laboratory].[Tests];
SET IDENTITY_INSERT [Laboratory].[Tests] OFF;
GO

PRINT 'Migrating [Laundry].[LaundryActions]...';
SET IDENTITY_INSERT [Laundry].[LaundryActions] ON;
INSERT INTO [Laundry].[LaundryActions] ([Id], [NameEn], [NameAr], [CreatedDate], [CreatedBy], [LastModifiedDate], [LastModifiedBy], [CompanyID], [TenantId])
SELECT [Id], [NameEn], [NameAr], [CreatedDate], [CreatedBy], [LastModifiedDate], [LastModifiedBy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Laundry].[LaundryActions];
SET IDENTITY_INSERT [Laundry].[LaundryActions] OFF;
GO

PRINT 'Migrating [LegalAffairs].[AffairDetails]...';
SET IDENTITY_INSERT [LegalAffairs].[AffairDetails] ON;
INSERT INTO [LegalAffairs].[AffairDetails] ([Id], [AffairsID], [AffairDate], [Decisionsissued], [DecisionsExcuted], [succinctness], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [AffairsID], [AffairDate], [Decisionsissued], [DecisionsExcuted], [succinctness], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[AffairDetails];
SET IDENTITY_INSERT [LegalAffairs].[AffairDetails] OFF;
GO

PRINT 'Migrating [LegalAffairs].[Affair]...';
SET IDENTITY_INSERT [LegalAffairs].[Affair] ON;
INSERT INTO [LegalAffairs].[Affair] ([Id], [AffairSerial], [AffairType], [AffairNo], [Incomingdate], [Name], [Description], [opponentName], [RecordNo], [AmountOfTheFee], [Appealprocedures], [RegistratioNumber], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [MemberID], [GeneralInventoryNo], [status], [IncomingID], [affairStatus], [CompanyID], [TenantId])
SELECT [Id], [AffairSerial], [AffairType], [AffairNo], [Incomingdate], [Name], [Description], [opponentName], [RecordNo], [AmountOfTheFee], [Appealprocedures], [RegistratioNumber], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [MemberID], [GeneralInventoryNo], [status], [IncomingID], [affairStatus], [CompanyID], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[Affair];
SET IDENTITY_INSERT [LegalAffairs].[Affair] OFF;
GO

PRINT 'Migrating [LegalAffairs].[ComplaintDetails]...';
SET IDENTITY_INSERT [LegalAffairs].[ComplaintDetails] ON;
INSERT INTO [LegalAffairs].[ComplaintDetails] ([ID], [ActionsTaken], [ActionsDate], [ComplaintId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [ID], [ActionsTaken], [ActionsDate], [ComplaintId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[ComplaintDetails];
SET IDENTITY_INSERT [LegalAffairs].[ComplaintDetails] OFF;
GO

PRINT 'Migrating [LegalAffairs].[Complaint]...';
SET IDENTITY_INSERT [LegalAffairs].[Complaint] ON;
INSERT INTO [LegalAffairs].[Complaint] ([ComplaintId], [IncomingId], [ReferralDate], [PersonComplainedName], [ComplaintTitle], [AssignedMemberId], [FinalDecision], [AccreditationStatus], [ApprovalDate], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [EntryType], [ComplaintFile], [TenantId])
SELECT [ComplaintId], [IncomingId], [ReferralDate], [PersonComplainedName], [ComplaintTitle], [AssignedMemberId], [FinalDecision], [AccreditationStatus], [ApprovalDate], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [EntryType], [ComplaintFile], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[Complaint];
SET IDENTITY_INSERT [LegalAffairs].[Complaint] OFF;
GO

PRINT 'Migrating [LegalAffairs].[CourtDetails]...';
SET IDENTITY_INSERT [LegalAffairs].[CourtDetails] ON;
INSERT INTO [LegalAffairs].[CourtDetails] ([ID], [CourtType], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CourtID], [TenantId])
SELECT [ID], [CourtType], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CourtID], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[CourtDetails];
SET IDENTITY_INSERT [LegalAffairs].[CourtDetails] OFF;
GO

PRINT 'Migrating [LegalAffairs].[CourtMaster]...';
SET IDENTITY_INSERT [LegalAffairs].[CourtMaster] ON;
INSERT INTO [LegalAffairs].[CourtMaster] ([CourtID], [CourtName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [CourtID], [CourtName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[CourtMaster];
SET IDENTITY_INSERT [LegalAffairs].[CourtMaster] OFF;
GO

PRINT 'Migrating [LegalAffairs].[ExecutionAndReservationsDetails]...';
SET IDENTITY_INSERT [LegalAffairs].[ExecutionAndReservationsDetails] ON;
INSERT INTO [LegalAffairs].[ExecutionAndReservationsDetails] ([Id], [ExecutionAndReservationsID], [procedureDate], [procedure], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [Id], [ExecutionAndReservationsID], [procedureDate], [procedure], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[ExecutionAndReservationsDetails];
SET IDENTITY_INSERT [LegalAffairs].[ExecutionAndReservationsDetails] OFF;
GO

PRINT 'Migrating [LegalAffairs].[ExecutionAndReservations]...';
SET IDENTITY_INSERT [LegalAffairs].[ExecutionAndReservations] ON;
INSERT INTO [LegalAffairs].[ExecutionAndReservations] ([Id], [ExecutionAndReservationsSerial], [AffairID], [Subject], [opponentName], [EmployeeID], [ExecutionAndReservationsNo], [Nextprocedure], [DueDate], [Finalprocedur], [FinalprocedurDate], [Note], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [ExecutionAndReservationsSerial], [AffairID], [Subject], [opponentName], [EmployeeID], [ExecutionAndReservationsNo], [Nextprocedure], [DueDate], [Finalprocedur], [FinalprocedurDate], [Note], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[ExecutionAndReservations];
SET IDENTITY_INSERT [LegalAffairs].[ExecutionAndReservations] OFF;
GO

PRINT 'Migrating [LegalAffairs].[Fatwa]...';
SET IDENTITY_INSERT [LegalAffairs].[Fatwa] ON;
INSERT INTO [LegalAffairs].[Fatwa] ([Id], [IncomingId], [ReferralDate], [ReferralEmployee], [Description], [RegistrationNo], [FatwaDate], [Status], [ApprovalDate], [CreatedDate], [code], [CompanyID], [TenantId])
SELECT [Id], [IncomingId], [ReferralDate], [ReferralEmployee], [Description], [RegistrationNo], [FatwaDate], [Status], [ApprovalDate], [CreatedDate], [code], [CompanyID], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[Fatwa];
SET IDENTITY_INSERT [LegalAffairs].[Fatwa] OFF;
GO

PRINT 'Migrating [LegalAffairs].[GeneralSaving]...';
SET IDENTITY_INSERT [LegalAffairs].[GeneralSaving] ON;
INSERT INTO [LegalAffairs].[GeneralSaving] ([ID], [SavingType], [IncomeID], [SerialNo], [GeneralInventoryNumber], [TransferDate], [EmployeeId], [EmployeeName], [Topic], [FinalDecision], [Notes], [CreatedBy], [CreationDate], [CompanyID], [TenantId])
SELECT [ID], [SavingType], [IncomeID], [SerialNo], [GeneralInventoryNumber], [TransferDate], [EmployeeId], [EmployeeName], [Topic], [FinalDecision], [Notes], [CreatedBy], [CreationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[GeneralSaving];
SET IDENTITY_INSERT [LegalAffairs].[GeneralSaving] OFF;
GO

PRINT 'Migrating [LegalAffairs].[Incoming]...';
SET IDENTITY_INSERT [LegalAffairs].[Incoming] ON;
INSERT INTO [LegalAffairs].[Incoming] ([Id], [IncomingType], [ImportNo], [GeneralInventoryNo], [ImportDate], [ImportEntity], [Title], [Notes], [CreatedDate], [CompanyID], [TenantId])
SELECT [Id], [IncomingType], [ImportNo], [GeneralInventoryNo], [ImportDate], [ImportEntity], [Title], [Notes], [CreatedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[Incoming];
SET IDENTITY_INSERT [LegalAffairs].[Incoming] OFF;
GO

PRINT 'Migrating [LegalAffairs].[Investigation]...';
SET IDENTITY_INSERT [LegalAffairs].[Investigation] ON;
INSERT INTO [LegalAffairs].[Investigation] ([Id], [IncomingId], [ReferralDate], [ReferralEmployee], [Description], [Actions], [ActionsDate], [FinalAction], [Status], [Code], [DistributionNo], [ApprovalDate], [CreatedDate], [CompanyID], [TenantId])
SELECT [Id], [IncomingId], [ReferralDate], [ReferralEmployee], [Description], [Actions], [ActionsDate], [FinalAction], [Status], [Code], [DistributionNo], [ApprovalDate], [CreatedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[Investigation];
SET IDENTITY_INSERT [LegalAffairs].[Investigation] OFF;
GO

PRINT 'Migrating [LegalAffairs].[LegalContracts]...';
SET IDENTITY_INSERT [LegalAffairs].[LegalContracts] ON;
INSERT INTO [LegalAffairs].[LegalContracts] ([ID], [IncomingID], [EmployeeID], [ReferralDate], [Description], [RegistrationNo], [Status], [ContractFile], [ApprovalFile], [ApprovalDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Code], [DistributionNo], [CompanyID], [TenantId])
SELECT [ID], [IncomingID], [EmployeeID], [ReferralDate], [Description], [RegistrationNo], [Status], [ContractFile], [ApprovalFile], [ApprovalDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Code], [DistributionNo], [CompanyID], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[LegalContracts];
SET IDENTITY_INSERT [LegalAffairs].[LegalContracts] OFF;
GO

PRINT 'Migrating [LegalAffairs].[LegalSetting]...';
SET IDENTITY_INSERT [LegalAffairs].[LegalSetting] ON;
INSERT INTO [LegalAffairs].[LegalSetting] ([Id], [MgrId], [CreatedBy], [CreatedDate], [TenantId])
SELECT [Id], [MgrId], [CreatedBy], [CreatedDate], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[LegalSetting];
SET IDENTITY_INSERT [LegalAffairs].[LegalSetting] OFF;
GO

PRINT 'Migrating [LegalAffairs].[LowyersTasks]...';
SET IDENTITY_INSERT [LegalAffairs].[LowyersTasks] ON;
INSERT INTO [LegalAffairs].[LowyersTasks] ([Id], [EmpId], [TaskType], [Distripution], [DistriputionNo], [SchDate], [CreatedBy], [CreatedDate], [BackupNo], [TenantId])
SELECT [Id], [EmpId], [TaskType], [Distripution], [DistriputionNo], [SchDate], [CreatedBy], [CreatedDate], [BackupNo], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[LowyersTasks];
SET IDENTITY_INSERT [LegalAffairs].[LowyersTasks] OFF;
GO

PRINT 'Migrating [LegalAffairs].[OtherIssue]...';
SET IDENTITY_INSERT [LegalAffairs].[OtherIssue] ON;
INSERT INTO [LegalAffairs].[OtherIssue] ([Id], [IncomingId], [ReferralDate], [ReferralEmployee], [LegalSubject], [MembershipNo], [Actions], [ActionsDate], [FinalAction], [FinalActionDate], [Status], [ApprovalDate], [Code], [DistributionNo], [CreatedDate], [CompanyID], [TenantId])
SELECT [Id], [IncomingId], [ReferralDate], [ReferralEmployee], [LegalSubject], [MembershipNo], [Actions], [ActionsDate], [FinalAction], [FinalActionDate], [Status], [ApprovalDate], [Code], [DistributionNo], [CreatedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[OtherIssue];
SET IDENTITY_INSERT [LegalAffairs].[OtherIssue] OFF;
GO

PRINT 'Migrating [LegalAffairs].[OutgoingIssues]...';
SET IDENTITY_INSERT [LegalAffairs].[OutgoingIssues] ON;
INSERT INTO [LegalAffairs].[OutgoingIssues] ([ID], [outgoingType], [outgoingIssueCode], [outgoingDate], [Issuer], [recipient], [outgoingSummary], [fileType], [fileUpload], [AffairID], [IncomingID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [outgoingType], [outgoingIssueCode], [outgoingDate], [Issuer], [recipient], [outgoingSummary], [fileType], [fileUpload], [AffairID], [IncomingID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[OutgoingIssues];
SET IDENTITY_INSERT [LegalAffairs].[OutgoingIssues] OFF;
GO

PRINT 'Migrating [LegalAffairs].[StabDetails]...';
SET IDENTITY_INSERT [LegalAffairs].[StabDetails] ON;
INSERT INTO [LegalAffairs].[StabDetails] ([Id], [StabID], [StabDate], [Descision], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [StabID], [StabDate], [Descision], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[StabDetails];
SET IDENTITY_INSERT [LegalAffairs].[StabDetails] OFF;
GO

PRINT 'Migrating [LegalAffairs].[Stab]...';
SET IDENTITY_INSERT [LegalAffairs].[Stab] ON;
INSERT INTO [LegalAffairs].[Stab] ([Id], [GeneralInventoryNo], [StabSerial], [StabNo], [Name], [AffairNo], [MemberID], [AmountOfTheFee], [ImportNumber], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [status], [IncomingID], [AffairID], [CompanyID], [TenantId])
SELECT [Id], [GeneralInventoryNo], [StabSerial], [StabNo], [Name], [AffairNo], [MemberID], [AmountOfTheFee], [ImportNumber], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [status], [IncomingID], [AffairID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[Stab];
SET IDENTITY_INSERT [LegalAffairs].[Stab] OFF;
GO

PRINT 'Migrating [LegalAffairs].[StoppedCanceledAffair]...';
SET IDENTITY_INSERT [LegalAffairs].[StoppedCanceledAffair] ON;
INSERT INTO [LegalAffairs].[StoppedCanceledAffair] ([Id], [Code], [AffairId], [PronouncedJudgment], [PronouncedJudgmentDate], [LawerEmployeeId], [LastDateToExpedite], [LastDayToExpedite], [LastTimeToExpedite], [ExtentofBenefit], [ActionsTaken], [Note], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Code], [AffairId], [PronouncedJudgment], [PronouncedJudgmentDate], [LawerEmployeeId], [LastDateToExpedite], [LastDayToExpedite], [LastTimeToExpedite], [ExtentofBenefit], [ActionsTaken], [Note], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[StoppedCanceledAffair];
SET IDENTITY_INSERT [LegalAffairs].[StoppedCanceledAffair] OFF;
GO

PRINT 'Migrating [LegalAffairs].[WorkDistributionAmongTechnicalMembers]...';
SET IDENTITY_INSERT [LegalAffairs].[WorkDistributionAmongTechnicalMembers] ON;
INSERT INTO [LegalAffairs].[WorkDistributionAmongTechnicalMembers] ([ID], [SerialNo], [DecisionDate], [DecisionSourceName], [Decision], [AssignedMemberID], [AssignedMemberName], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [SerialNo], [DecisionDate], [DecisionSourceName], [Decision], [AssignedMemberID], [AssignedMemberName], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[WorkDistributionAmongTechnicalMembers];
SET IDENTITY_INSERT [LegalAffairs].[WorkDistributionAmongTechnicalMembers] OFF;
GO

PRINT 'Migrating [LegalAffairs].[lawyer]...';
SET IDENTITY_INSERT [LegalAffairs].[lawyer] ON;
INSERT INTO [LegalAffairs].[lawyer] ([ID], [EmployeeID], [LawyerWorkType], [lawyerWorkDistributionType], [DistributionNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BackupNo], [TenantId])
SELECT [ID], [EmployeeID], [LawyerWorkType], [lawyerWorkDistributionType], [DistributionNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BackupNo], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[lawyer];
SET IDENTITY_INSERT [LegalAffairs].[lawyer] OFF;
GO

PRINT 'Migrating [LegalAffairs].[succinctness]...';
SET IDENTITY_INSERT [LegalAffairs].[succinctness] ON;
INSERT INTO [LegalAffairs].[succinctness] ([Id], [CourtSerial], [AffairType], [AffairNo], [Incomingdate], [Name], [Description], [opponentName], [RecordNo], [AmountOfTheFee], [Appealprocedures], [RegistratioNumber], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [MemberID], [GeneralInventoryNo], [status], [AffairID], [CompanyID], [TenantId])
SELECT [Id], [CourtSerial], [AffairType], [AffairNo], [Incomingdate], [Name], [Description], [opponentName], [RecordNo], [AmountOfTheFee], [Appealprocedures], [RegistratioNumber], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [MemberID], [GeneralInventoryNo], [status], [AffairID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[LegalAffairs].[succinctness];
SET IDENTITY_INSERT [LegalAffairs].[succinctness] OFF;
GO

PRINT 'Migrating [Maintenance].[AsstesWarenty]...';
SET IDENTITY_INSERT [Maintenance].[AsstesWarenty] ON;
INSERT INTO [Maintenance].[AsstesWarenty] ([Id], [AssetesWarentyCode], [AssteID], [PurchaseDate], [LocationID], [AssetEmployeeID], [AssetSerialNumber], [equipment], [Nonequipment], [Medical], [NonMedical], [WarentyPeriod], [WarentyType], [DaysNotes], [AlarmDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [AssetesWarentyCode], [AssteID], [PurchaseDate], [LocationID], [AssetEmployeeID], [AssetSerialNumber], [equipment], [Nonequipment], [Medical], [NonMedical], [WarentyPeriod], [WarentyType], [DaysNotes], [AlarmDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Maintenance].[AsstesWarenty];
SET IDENTITY_INSERT [Maintenance].[AsstesWarenty] OFF;
GO

PRINT 'Migrating [Maintenance].[CustomMaintenanceRequestDetails]...';
SET IDENTITY_INSERT [Maintenance].[CustomMaintenanceRequestDetails] ON;
INSERT INTO [Maintenance].[CustomMaintenanceRequestDetails] ([Id], [CustomMaintenanceRequestId], [ItemId], [OrderedQTY], [UnitConversionFactorId], [UnitPrice], [IsApproved], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [CustomMaintenanceRequestId], [ItemId], [OrderedQTY], [UnitConversionFactorId], [UnitPrice], [IsApproved], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Maintenance].[CustomMaintenanceRequestDetails];
SET IDENTITY_INSERT [Maintenance].[CustomMaintenanceRequestDetails] OFF;
GO

PRINT 'Migrating [Maintenance].[CustomMaintenanceRequest]...';
SET IDENTITY_INSERT [Maintenance].[CustomMaintenanceRequest] ON;
INSERT INTO [Maintenance].[CustomMaintenanceRequest] ([Id], [Code], [AssetID], [AssetSerialNumber], [AssetPurchaseDate], [LocationID], [DaysNotice], [AssetEmployeeID], [equipment], [Nonequipment], [Medical], [NonMedical], [AlarmDate], [Comment], [ExpectedDays], [EmployeeID], [Periorty], [Status], [MaintenanceRequestId], [LastMaintenanceDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [CompanyDateRequest], [RequestItemsStatus], [RequestItemsRejectedReason], [TenantId])
SELECT [Id], [Code], [AssetID], [AssetSerialNumber], [AssetPurchaseDate], [LocationID], [DaysNotice], [AssetEmployeeID], [equipment], [Nonequipment], [Medical], [NonMedical], [AlarmDate], [Comment], [ExpectedDays], [EmployeeID], [Periorty], [Status], [MaintenanceRequestId], [LastMaintenanceDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [CompanyDateRequest], [RequestItemsStatus], [RequestItemsRejectedReason], @TenantId
FROM [SunCity_Clinics].[Maintenance].[CustomMaintenanceRequest];
SET IDENTITY_INSERT [Maintenance].[CustomMaintenanceRequest] OFF;
GO

PRINT 'Migrating [Maintenance].[DirectMaintenanceRequestsToMaintenanceManager]...';
SET IDENTITY_INSERT [Maintenance].[DirectMaintenanceRequestsToMaintenanceManager] ON;
INSERT INTO [Maintenance].[DirectMaintenanceRequestsToMaintenanceManager] ([ID], [MaintenanceRequestID], [MaintenanceTypeID], [CreationDate], [CreatedBy], [CompanyID], [TenantId])
SELECT [ID], [MaintenanceRequestID], [MaintenanceTypeID], [CreationDate], [CreatedBy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Maintenance].[DirectMaintenanceRequestsToMaintenanceManager];
SET IDENTITY_INSERT [Maintenance].[DirectMaintenanceRequestsToMaintenanceManager] OFF;
GO

PRINT 'Migrating [Maintenance].[EmergencyMaintenance]...';
SET IDENTITY_INSERT [Maintenance].[EmergencyMaintenance] ON;
INSERT INTO [Maintenance].[EmergencyMaintenance] ([ID], [MachineID], [MaintenanceName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Code], [IsAsset], [DeptID], [Status], [RequestDate], [MaintenanceDesc], [TenantId])
SELECT [ID], [MachineID], [MaintenanceName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Code], [IsAsset], [DeptID], [Status], [RequestDate], [MaintenanceDesc], @TenantId
FROM [SunCity_Clinics].[Maintenance].[EmergencyMaintenance];
SET IDENTITY_INSERT [Maintenance].[EmergencyMaintenance] OFF;
GO

PRINT 'Migrating [Maintenance].[Employee_GasBon]...';
SET IDENTITY_INSERT [Maintenance].[Employee_GasBon] ON;
INSERT INTO [Maintenance].[Employee_GasBon] ([id], [EmpID], [BonNoFrom], [BonNoTo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [RecieveDate], [IsClosed], [TenantId])
SELECT [id], [EmpID], [BonNoFrom], [BonNoTo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [RecieveDate], [IsClosed], @TenantId
FROM [SunCity_Clinics].[Maintenance].[Employee_GasBon];
SET IDENTITY_INSERT [Maintenance].[Employee_GasBon] OFF;
GO

PRINT 'Migrating [Maintenance].[Machine_Maintenance_Schedule]...';
SET IDENTITY_INSERT [Maintenance].[Machine_Maintenance_Schedule] ON;
INSERT INTO [Maintenance].[Machine_Maintenance_Schedule] ([ID], [MachineID], [PeriodType], [PeriodVal], [MaintenanceName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Code], [IsAsset], [DeptID], [Status], [visitsNo], [Desc], [Delayforavisit], [TenantId])
SELECT [ID], [MachineID], [PeriodType], [PeriodVal], [MaintenanceName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Code], [IsAsset], [DeptID], [Status], [visitsNo], [Desc], [Delayforavisit], @TenantId
FROM [SunCity_Clinics].[Maintenance].[Machine_Maintenance_Schedule];
SET IDENTITY_INSERT [Maintenance].[Machine_Maintenance_Schedule] OFF;
GO

PRINT 'Migrating [Maintenance].[Machines]...';
SET IDENTITY_INSERT [Maintenance].[Machines] ON;
INSERT INTO [Maintenance].[Machines] ([id], [Code], [NameAr], [NameEn], [IsMedical], [RecievedBy], [LocationID], [ManfactureYear], [AgentID], [Model], [Serial], [Manfacture_Country], [DeptID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Work_StartDate], [TenantId])
SELECT [id], [Code], [NameAr], [NameEn], [IsMedical], [RecievedBy], [LocationID], [ManfactureYear], [AgentID], [Model], [Serial], [Manfacture_Country], [DeptID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Work_StartDate], @TenantId
FROM [SunCity_Clinics].[Maintenance].[Machines];
SET IDENTITY_INSERT [Maintenance].[Machines] OFF;
GO

PRINT 'Migrating [Maintenance].[MaintenanceRecorde]...';
SET IDENTITY_INSERT [Maintenance].[MaintenanceRecorde] ON;
INSERT INTO [Maintenance].[MaintenanceRecorde] ([Id], [MaintenanceRecordeCode], [MaintenanceTypeID], [AssteID], [PurchaseDate], [LocationID], [AssetEmployeeID], [AssetSerialNumber], [equipment], [Nonequipment], [Medical], [NonMedical], [MaintenancePeriod], [DaysNotes], [DaysExpected], [AlarmDate], [LastMaintenanceDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [MaintenanceRecordeCode], [MaintenanceTypeID], [AssteID], [PurchaseDate], [LocationID], [AssetEmployeeID], [AssetSerialNumber], [equipment], [Nonequipment], [Medical], [NonMedical], [MaintenancePeriod], [DaysNotes], [DaysExpected], [AlarmDate], [LastMaintenanceDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Maintenance].[MaintenanceRecorde];
SET IDENTITY_INSERT [Maintenance].[MaintenanceRecorde] OFF;
GO

PRINT 'Migrating [Maintenance].[MaintenanceRequest]...';
SET IDENTITY_INSERT [Maintenance].[MaintenanceRequest] ON;
INSERT INTO [Maintenance].[MaintenanceRequest] ([Id], [Code], [RequesterName], [SubDepartemntId], [RequestDate], [RequestTypeId], [Description], [Status], [HospitalMangerApproveDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [AssetLocationID], [AssetID], [Priority], [CompanyID], [QCID], [AssetLocationOther], [ShaseNo], [manufacture_Company], [Model], [Country], [manufactureYear], [TenantId])
SELECT [Id], [Code], [RequesterName], [SubDepartemntId], [RequestDate], [RequestTypeId], [Description], [Status], [HospitalMangerApproveDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [AssetLocationID], [AssetID], [Priority], [CompanyID], [QCID], [AssetLocationOther], [ShaseNo], [manufacture_Company], [Model], [Country], [manufactureYear], @TenantId
FROM [SunCity_Clinics].[Maintenance].[MaintenanceRequest];
SET IDENTITY_INSERT [Maintenance].[MaintenanceRequest] OFF;
GO

PRINT 'Migrating [Maintenance].[MaintenanceRequestsToWorkshop]...';
SET IDENTITY_INSERT [Maintenance].[MaintenanceRequestsToWorkshop] ON;
INSERT INTO [Maintenance].[MaintenanceRequestsToWorkshop] ([Id], [MaintenenceRequestId], [WorkshopId], [CreationDate], [CreatedBy], [CompanyID], [TenantId])
SELECT [Id], [MaintenenceRequestId], [WorkshopId], [CreationDate], [CreatedBy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Maintenance].[MaintenanceRequestsToWorkshop];
SET IDENTITY_INSERT [Maintenance].[MaintenanceRequestsToWorkshop] OFF;
GO

PRINT 'Migrating [Maintenance].[MaintenanceSetting]...';
SET IDENTITY_INSERT [Maintenance].[MaintenanceSetting] ON;
INSERT INTO [Maintenance].[MaintenanceSetting] ([ID], [DirectorOfTheEngineeringDepartmentID], [GasValueInLiter], [DieselValueInLiter], [OilLiterCouponValue], [CompanyID], [TenantId])
SELECT [ID], [DirectorOfTheEngineeringDepartmentID], [GasValueInLiter], [DieselValueInLiter], [OilLiterCouponValue], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Maintenance].[MaintenanceSetting];
SET IDENTITY_INSERT [Maintenance].[MaintenanceSetting] OFF;
GO

PRINT 'Migrating [Maintenance].[MaintenanceType]...';
SET IDENTITY_INSERT [Maintenance].[MaintenanceType] ON;
INSERT INTO [Maintenance].[MaintenanceType] ([Id], [MaintenanceCode], [MaintenanceName], [MaintenanceDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [EmpId], [TenantId])
SELECT [Id], [MaintenanceCode], [MaintenanceName], [MaintenanceDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [EmpId], @TenantId
FROM [SunCity_Clinics].[Maintenance].[MaintenanceType];
SET IDENTITY_INSERT [Maintenance].[MaintenanceType] OFF;
GO

PRINT 'Migrating [Maintenance].[Maintenance_SpareParts]...';
SET IDENTITY_INSERT [Maintenance].[Maintenance_SpareParts] ON;
INSERT INTO [Maintenance].[Maintenance_SpareParts] ([ID], [MaintenanceID], [SparePartID], [Qty], [IsUrgent], [UnitConversionFactorId], [CompanyID], [TenantId])
SELECT [ID], [MaintenanceID], [SparePartID], [Qty], [IsUrgent], [UnitConversionFactorId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Maintenance].[Maintenance_SpareParts];
SET IDENTITY_INSERT [Maintenance].[Maintenance_SpareParts] OFF;
GO

PRINT 'Migrating [Maintenance].[OrginalMaintenanceRequest]...';
SET IDENTITY_INSERT [Maintenance].[OrginalMaintenanceRequest] ON;
INSERT INTO [Maintenance].[OrginalMaintenanceRequest] ([Id], [MaintenanceRecordeID], [Code], [AssetID], [AssetSerialNumber], [AssetCode], [AssetName], [LocationID], [LocationName], [DaysNotice], [Status], [AssetEmployeeID], [AlarmDate], [Comment], [ExpectedDays], [LastMaintenanceDate], [TechID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [MaintenanceRecordeID], [Code], [AssetID], [AssetSerialNumber], [AssetCode], [AssetName], [LocationID], [LocationName], [DaysNotice], [Status], [AssetEmployeeID], [AlarmDate], [Comment], [ExpectedDays], [LastMaintenanceDate], [TechID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Maintenance].[OrginalMaintenanceRequest];
SET IDENTITY_INSERT [Maintenance].[OrginalMaintenanceRequest] OFF;
GO

PRINT 'Migrating [Maintenance].[VehicleReq_Persons]...';
SET IDENTITY_INSERT [Maintenance].[VehicleReq_Persons] ON;
INSERT INTO [Maintenance].[VehicleReq_Persons] ([id], [VehicleReqID], [PersonName], [PersonJob], [TenantId])
SELECT [id], [VehicleReqID], [PersonName], [PersonJob], @TenantId
FROM [SunCity_Clinics].[Maintenance].[VehicleReq_Persons];
SET IDENTITY_INSERT [Maintenance].[VehicleReq_Persons] OFF;
GO

PRINT 'Migrating [Maintenance].[VehicleRequest]...';
SET IDENTITY_INSERT [Maintenance].[VehicleRequest] ON;
INSERT INTO [Maintenance].[VehicleRequest] ([id], [Code], [VehicleID], [DriverID], [StartKilometer], [Endkilometer], [StartTime], [EndTime], [RequestedDepartment], [RequestedBy], [RequestedByJob], [MissionNotes], [StartDate], [EndDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Status], [RequestDate], [BonID], [ReadingWhenSupply], [RoadType], [OperatingNo], [CompanyID], [TenantId])
SELECT [id], [Code], [VehicleID], [DriverID], [StartKilometer], [Endkilometer], [StartTime], [EndTime], [RequestedDepartment], [RequestedBy], [RequestedByJob], [MissionNotes], [StartDate], [EndDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Status], [RequestDate], [BonID], [ReadingWhenSupply], [RoadType], [OperatingNo], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Maintenance].[VehicleRequest];
SET IDENTITY_INSERT [Maintenance].[VehicleRequest] OFF;
GO

PRINT 'Migrating [Maintenance].[Vehicles]...';
SET IDENTITY_INSERT [Maintenance].[Vehicles] ON;
INSERT INTO [Maintenance].[Vehicles] ([id], [Code], [CarNo], [ManufactureType], [ChassisNo], [EngineSN], [CarType], [CylindersCapacity], [CylindersNo], [StartDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [countryside_GasConsume], [City_GasConsume], [Gas_Type], [CompanyID], [TenantId])
SELECT [id], [Code], [CarNo], [ManufactureType], [ChassisNo], [EngineSN], [CarType], [CylindersCapacity], [CylindersNo], [StartDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [countryside_GasConsume], [City_GasConsume], [Gas_Type], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Maintenance].[Vehicles];
SET IDENTITY_INSERT [Maintenance].[Vehicles] OFF;
GO

PRINT 'Migrating [Maintenance].[WarentyAlarmRequest]...';
SET IDENTITY_INSERT [Maintenance].[WarentyAlarmRequest] ON;
INSERT INTO [Maintenance].[WarentyAlarmRequest] ([Id], [Code], [AssetID], [AssetSerialNumber], [AssetCode], [AssetName], [LocationID], [LocationName], [DaysNotice], [Status], [AssetEmployeeID], [AlarmDate], [Comment], [WarentyExpiryDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [AssetWarentyID], [CompanyID], [TenantId])
SELECT [Id], [Code], [AssetID], [AssetSerialNumber], [AssetCode], [AssetName], [LocationID], [LocationName], [DaysNotice], [Status], [AssetEmployeeID], [AlarmDate], [Comment], [WarentyExpiryDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [AssetWarentyID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Maintenance].[WarentyAlarmRequest];
SET IDENTITY_INSERT [Maintenance].[WarentyAlarmRequest] OFF;
GO

PRINT 'Migrating [Maintenance].[WorkOrders]...';
SET IDENTITY_INSERT [Maintenance].[WorkOrders] ON;
INSERT INTO [Maintenance].[WorkOrders] ([ID], [MaintenanceID], [MaintenanceType], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Code], [IsAsset], [DeptID], [Status], [StartDate], [EmpID], [RequestDate], [WorkshopID], [TenantId])
SELECT [ID], [MaintenanceID], [MaintenanceType], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Code], [IsAsset], [DeptID], [Status], [StartDate], [EmpID], [RequestDate], [WorkshopID], @TenantId
FROM [SunCity_Clinics].[Maintenance].[WorkOrders];
SET IDENTITY_INSERT [Maintenance].[WorkOrders] OFF;
GO

PRINT 'Migrating [Maintenance].[WorkShop]...';
SET IDENTITY_INSERT [Maintenance].[WorkShop] ON;
INSERT INTO [Maintenance].[WorkShop] ([id], [Code], [NameEn], [NameAr], [DeptID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IsMedical], [StoreID], [ManagerEmpId], [MaintenenceTypeId], [TenantId])
SELECT [id], [Code], [NameEn], [NameAr], [DeptID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IsMedical], [StoreID], [ManagerEmpId], [MaintenenceTypeId], @TenantId
FROM [SunCity_Clinics].[Maintenance].[WorkShop];
SET IDENTITY_INSERT [Maintenance].[WorkShop] OFF;
GO

PRINT 'Migrating [Maintenance].[WorkShop_Authority]...';
SET IDENTITY_INSERT [Maintenance].[WorkShop_Authority] ON;
INSERT INTO [Maintenance].[WorkShop_Authority] ([id], [UserID], [WorkShopID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [id], [UserID], [WorkShopID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Maintenance].[WorkShop_Authority];
SET IDENTITY_INSERT [Maintenance].[WorkShop_Authority] OFF;
GO

PRINT 'Migrating [Maintenance].[WorkShop_Employee]...';
SET IDENTITY_INSERT [Maintenance].[WorkShop_Employee] ON;
INSERT INTO [Maintenance].[WorkShop_Employee] ([id], [EmpID], [WorkShopID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [JobDescription], [TenantId])
SELECT [id], [EmpID], [WorkShopID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [JobDescription], @TenantId
FROM [SunCity_Clinics].[Maintenance].[WorkShop_Employee];
SET IDENTITY_INSERT [Maintenance].[WorkShop_Employee] OFF;
GO

PRINT 'Migrating [Marketing].[ActivityPhases]...';
SET IDENTITY_INSERT [Marketing].[ActivityPhases] ON;
INSERT INTO [Marketing].[ActivityPhases] ([ID], [PhaseCode], [PhaseName], [PhaseDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [PhaseCode], [PhaseName], [PhaseDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[ActivityPhases];
SET IDENTITY_INSERT [Marketing].[ActivityPhases] OFF;
GO

PRINT 'Migrating [Marketing].[ActivityPlans]...';
SET IDENTITY_INSERT [Marketing].[ActivityPlans] ON;
INSERT INTO [Marketing].[ActivityPlans] ([ID], [PlanCode], [PlanName], [PlanDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [PlanCode], [PlanName], [PlanDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[ActivityPlans];
SET IDENTITY_INSERT [Marketing].[ActivityPlans] OFF;
GO

PRINT 'Migrating [Marketing].[ActivityProcessDetails]...';
SET IDENTITY_INSERT [Marketing].[ActivityProcessDetails] ON;
INSERT INTO [Marketing].[ActivityProcessDetails] ([ID], [ProcessActivityID], [StageName], [ActivityNumber], [Purpose], [StartInDays], [EndInDays], [StartTime], [EndTime], [Notes], [ResponsibilityID], [EmployeeID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ProcessActivityID], [StageName], [ActivityNumber], [Purpose], [StartInDays], [EndInDays], [StartTime], [EndTime], [Notes], [ResponsibilityID], [EmployeeID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[ActivityProcessDetails];
SET IDENTITY_INSERT [Marketing].[ActivityProcessDetails] OFF;
GO

PRINT 'Migrating [Marketing].[ActivityProcessHeader]...';
SET IDENTITY_INSERT [Marketing].[ActivityProcessHeader] ON;
INSERT INTO [Marketing].[ActivityProcessHeader] ([ID], [ProcessActivityCode], [ProcessActivityName], [ProcessDescription], [ProcessTypeEnum], [ISActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ProcessActivityCode], [ProcessActivityName], [ProcessDescription], [ProcessTypeEnum], [ISActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[ActivityProcessHeader];
SET IDENTITY_INSERT [Marketing].[ActivityProcessHeader] OFF;
GO

PRINT 'Migrating [Marketing].[ActivityTypes]...';
SET IDENTITY_INSERT [Marketing].[ActivityTypes] ON;
INSERT INTO [Marketing].[ActivityTypes] ([ID], [TypeCode], [TypeName], [TypeDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [TypeCode], [TypeName], [TypeDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[ActivityTypes];
SET IDENTITY_INSERT [Marketing].[ActivityTypes] OFF;
GO

PRINT 'Migrating [Marketing].[BusinessClassification]...';
SET IDENTITY_INSERT [Marketing].[BusinessClassification] ON;
INSERT INTO [Marketing].[BusinessClassification] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[BusinessClassification];
SET IDENTITY_INSERT [Marketing].[BusinessClassification] OFF;
GO

PRINT 'Migrating [Marketing].[CallListsTarget]...';
SET IDENTITY_INSERT [Marketing].[CallListsTarget] ON;
INSERT INTO [Marketing].[CallListsTarget] ([ID], [CallListsId], [Name], [EmployeeId], [ContactFor], [Status], [PlannedDate], [PlannedTime], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [CallListsId], [Name], [EmployeeId], [ContactFor], [Status], [PlannedDate], [PlannedTime], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[CallListsTarget];
SET IDENTITY_INSERT [Marketing].[CallListsTarget] OFF;
GO

PRINT 'Migrating [Marketing].[CallLists]...';
SET IDENTITY_INSERT [Marketing].[CallLists] ON;
INSERT INTO [Marketing].[CallLists] ([ID], [Code], [Description], [EmployeeId], [QuestionnaireId], [CampaignId], [ActivityNumber], [StartDate], [EndDate], [StartTime], [EndTime], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Description], [EmployeeId], [QuestionnaireId], [CampaignId], [ActivityNumber], [StartDate], [EndDate], [StartTime], [EndTime], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[CallLists];
SET IDENTITY_INSERT [Marketing].[CallLists] OFF;
GO

PRINT 'Migrating [Marketing].[CampaignGroup]...';
SET IDENTITY_INSERT [Marketing].[CampaignGroup] ON;
INSERT INTO [Marketing].[CampaignGroup] ([ID], [CampaignGroupCode], [CampaignGroupName], [CampaignGroupDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [CampaignGroupCode], [CampaignGroupName], [CampaignGroupDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[CampaignGroup];
SET IDENTITY_INSERT [Marketing].[CampaignGroup] OFF;
GO

PRINT 'Migrating [Marketing].[CampaignTarget]...';
SET IDENTITY_INSERT [Marketing].[CampaignTarget] ON;
INSERT INTO [Marketing].[CampaignTarget] ([ID], [CampaignTargetCode], [CampaignTargetName], [CampaignTargetDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [CampaignTargetCode], [CampaignTargetName], [CampaignTargetDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[CampaignTarget];
SET IDENTITY_INSERT [Marketing].[CampaignTarget] OFF;
GO

PRINT 'Migrating [Marketing].[CampaignTypes]...';
SET IDENTITY_INSERT [Marketing].[CampaignTypes] ON;
INSERT INTO [Marketing].[CampaignTypes] ([ID], [CampaignTypeCode], [CampaignTypeName], [CampaignTypeDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [CampaignTypeCode], [CampaignTypeName], [CampaignTypeDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[CampaignTypes];
SET IDENTITY_INSERT [Marketing].[CampaignTypes] OFF;
GO

PRINT 'Migrating [Marketing].[Campaign]...';
SET IDENTITY_INSERT [Marketing].[Campaign] ON;
INSERT INTO [Marketing].[Campaign] ([ID], [StatusEnum], [Code], [CampaignTypesLookupId], [CampaignGroupsId], [CampaignTargetsLookupId], [Name], [SubCompaignIdFormat], [QuestionnaireId], [StartDate], [EndDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [StatusEnum], [Code], [CampaignTypesLookupId], [CampaignGroupsId], [CampaignTargetsLookupId], [Name], [SubCompaignIdFormat], [QuestionnaireId], [StartDate], [EndDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[Campaign];
SET IDENTITY_INSERT [Marketing].[Campaign] OFF;
GO

PRINT 'Migrating [Marketing].[CarrierCompany]...';
SET IDENTITY_INSERT [Marketing].[CarrierCompany] ON;
INSERT INTO [Marketing].[CarrierCompany] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[CarrierCompany];
SET IDENTITY_INSERT [Marketing].[CarrierCompany] OFF;
GO

PRINT 'Migrating [Marketing].[Carrier]...';
SET IDENTITY_INSERT [Marketing].[Carrier] ON;
INSERT INTO [Marketing].[Carrier] ([ID], [Code], [Name], [AddressDescription], [CountryID], [CityID], [Street], [PostalCode], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [AddressDescription], [CountryID], [CityID], [Street], [PostalCode], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[Carrier];
SET IDENTITY_INSERT [Marketing].[Carrier] OFF;
GO

PRINT 'Migrating [Marketing].[CaseCategory]...';
SET IDENTITY_INSERT [Marketing].[CaseCategory] ON;
INSERT INTO [Marketing].[CaseCategory] ([ID], [CategoryCode], [CategoryName], [CategoryDescription], [CategorytypeEnum], [ParentID], [EmployeeID], [CostCenterID], [Email], [QuestionnaireID], [ActivityProcessID], [TypeEnum], [ActivityTypeID], [Purpose], [ActivityPhaseID], [ActivityProcessIDFollow], [TypeEnumFollow], [ActivityTypeIDFollow], [PurposeFollow], [ActivityPhaseIDFollow], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [CategoryCode], [CategoryName], [CategoryDescription], [CategorytypeEnum], [ParentID], [EmployeeID], [CostCenterID], [Email], [QuestionnaireID], [ActivityProcessID], [TypeEnum], [ActivityTypeID], [Purpose], [ActivityPhaseID], [ActivityProcessIDFollow], [TypeEnumFollow], [ActivityTypeIDFollow], [PurposeFollow], [ActivityPhaseIDFollow], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[CaseCategory];
SET IDENTITY_INSERT [Marketing].[CaseCategory] OFF;
GO

PRINT 'Migrating [Marketing].[Character]...';
SET IDENTITY_INSERT [Marketing].[Character] ON;
INSERT INTO [Marketing].[Character] ([ID], [CharacterCode], [CharacterName], [CharacterDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [CharacterCode], [CharacterName], [CharacterDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[Character];
SET IDENTITY_INSERT [Marketing].[Character] OFF;
GO

PRINT 'Migrating [Marketing].[CommissionCalculation]...';
SET IDENTITY_INSERT [Marketing].[CommissionCalculation] ON;
INSERT INTO [Marketing].[CommissionCalculation] ([ID], [ItemId], [CustomerId], [EmployeeId], [DiscountEnumId], [BasicEnumId], [CommissionPercentage], [DateFrom], [DateTo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CommissionCode], [CompanyID], [TenantId])
SELECT [ID], [ItemId], [CustomerId], [EmployeeId], [DiscountEnumId], [BasicEnumId], [CommissionPercentage], [DateFrom], [DateTo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CommissionCode], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[CommissionCalculation];
SET IDENTITY_INSERT [Marketing].[CommissionCalculation] OFF;
GO

PRINT 'Migrating [Marketing].[CompanyChains]...';
SET IDENTITY_INSERT [Marketing].[CompanyChains] ON;
INSERT INTO [Marketing].[CompanyChains] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[CompanyChains];
SET IDENTITY_INSERT [Marketing].[CompanyChains] OFF;
GO

PRINT 'Migrating [Marketing].[Contact]...';
SET IDENTITY_INSERT [Marketing].[Contact] ON;
INSERT INTO [Marketing].[Contact] ([ID], [Code], [CompanyLookup], [FirstName], [MiddleName], [LastName], [JobTitle], [FunctionPersonId], [Profession], [Department], [CountryId], [PostalCode], [Street], [CityId], [State], [Phone], [Extension], [Fax], [EMailAddress], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [CompanyLookup], [FirstName], [MiddleName], [LastName], [JobTitle], [FunctionPersonId], [Profession], [Department], [CountryId], [PostalCode], [Street], [CityId], [State], [Phone], [Extension], [Fax], [EMailAddress], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[Contact];
SET IDENTITY_INSERT [Marketing].[Contact] OFF;
GO

PRINT 'Migrating [Marketing].[CustomerPriceDiscountGroup]...';
SET IDENTITY_INSERT [Marketing].[CustomerPriceDiscountGroup] ON;
INSERT INTO [Marketing].[CustomerPriceDiscountGroup] ([ID], [ShowTypeEnumID], [PriceGroups], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ShowTypeEnumID], [PriceGroups], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[CustomerPriceDiscountGroup];
SET IDENTITY_INSERT [Marketing].[CustomerPriceDiscountGroup] OFF;
GO

PRINT 'Migrating [Marketing].[CustomerRebateGroup]...';
SET IDENTITY_INSERT [Marketing].[CustomerRebateGroup] ON;
INSERT INTO [Marketing].[CustomerRebateGroup] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[CustomerRebateGroup];
SET IDENTITY_INSERT [Marketing].[CustomerRebateGroup] OFF;
GO

PRINT 'Migrating [Marketing].[CustomerTMAGroup]...';
SET IDENTITY_INSERT [Marketing].[CustomerTMAGroup] ON;
INSERT INTO [Marketing].[CustomerTMAGroup] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[CustomerTMAGroup];
SET IDENTITY_INSERT [Marketing].[CustomerTMAGroup] OFF;
GO

PRINT 'Migrating [Marketing].[Customers]...';
SET IDENTITY_INSERT [Marketing].[Customers] ON;
INSERT INTO [Marketing].[Customers] ([ID], [CustomerCode], [CustomerName], [CustomerDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [CustomerCode], [CustomerName], [CustomerDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[Customers];
SET IDENTITY_INSERT [Marketing].[Customers] OFF;
GO

PRINT 'Migrating [Marketing].[DeliveryModeItems]...';
SET IDENTITY_INSERT [Marketing].[DeliveryModeItems] ON;
INSERT INTO [Marketing].[DeliveryModeItems] ([ID], [DeliveryModeID], [ItemID], [TypeEnum], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [DeliveryModeID], [ItemID], [TypeEnum], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[DeliveryModeItems];
SET IDENTITY_INSERT [Marketing].[DeliveryModeItems] OFF;
GO

PRINT 'Migrating [Marketing].[DeliveryModesAddress]...';
SET IDENTITY_INSERT [Marketing].[DeliveryModesAddress] ON;
INSERT INTO [Marketing].[DeliveryModesAddress] ([ID], [DeliveryModeID], [CountryID], [CityID], [TypeEnum], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [DeliveryModeID], [CountryID], [CityID], [TypeEnum], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[DeliveryModesAddress];
SET IDENTITY_INSERT [Marketing].[DeliveryModesAddress] OFF;
GO

PRINT 'Migrating [Marketing].[DeliveryModesHeader]...';
SET IDENTITY_INSERT [Marketing].[DeliveryModesHeader] ON;
INSERT INTO [Marketing].[DeliveryModesHeader] ([ID], [Code], [Name], [Description], [ServiceEnum], [ExpediteID], [CarrierID], [CarrierCompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [ServiceEnum], [ExpediteID], [CarrierID], [CarrierCompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[DeliveryModesHeader];
SET IDENTITY_INSERT [Marketing].[DeliveryModesHeader] OFF;
GO

PRINT 'Migrating [Marketing].[DeliveryReasons]...';
SET IDENTITY_INSERT [Marketing].[DeliveryReasons] ON;
INSERT INTO [Marketing].[DeliveryReasons] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[DeliveryReasons];
SET IDENTITY_INSERT [Marketing].[DeliveryReasons] OFF;
GO

PRINT 'Migrating [Marketing].[Destination]...';
SET IDENTITY_INSERT [Marketing].[Destination] ON;
INSERT INTO [Marketing].[Destination] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[Destination];
SET IDENTITY_INSERT [Marketing].[Destination] OFF;
GO

PRINT 'Migrating [Marketing].[EmailCategories]...';
SET IDENTITY_INSERT [Marketing].[EmailCategories] ON;
INSERT INTO [Marketing].[EmailCategories] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[EmailCategories];
SET IDENTITY_INSERT [Marketing].[EmailCategories] OFF;
GO

PRINT 'Migrating [Marketing].[EmailGroups]...';
SET IDENTITY_INSERT [Marketing].[EmailGroups] ON;
INSERT INTO [Marketing].[EmailGroups] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [EmailCategoryID], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [EmailCategoryID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[EmailGroups];
SET IDENTITY_INSERT [Marketing].[EmailGroups] OFF;
GO

PRINT 'Migrating [Marketing].[Expedite]...';
SET IDENTITY_INSERT [Marketing].[Expedite] ON;
INSERT INTO [Marketing].[Expedite] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[Expedite];
SET IDENTITY_INSERT [Marketing].[Expedite] OFF;
GO

PRINT 'Migrating [Marketing].[ItemDiscountGroup]...';
SET IDENTITY_INSERT [Marketing].[ItemDiscountGroup] ON;
INSERT INTO [Marketing].[ItemDiscountGroup] ([ID], [ShowTypeEnumID], [PriceGroups], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ShowTypeEnumID], [PriceGroups], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[ItemDiscountGroup];
SET IDENTITY_INSERT [Marketing].[ItemDiscountGroup] OFF;
GO

PRINT 'Migrating [Marketing].[ItemFreightGroups]...';
SET IDENTITY_INSERT [Marketing].[ItemFreightGroups] ON;
INSERT INTO [Marketing].[ItemFreightGroups] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[ItemFreightGroups];
SET IDENTITY_INSERT [Marketing].[ItemFreightGroups] OFF;
GO

PRINT 'Migrating [Marketing].[ItemListDetails]...';
SET IDENTITY_INSERT [Marketing].[ItemListDetails] ON;
INSERT INTO [Marketing].[ItemListDetails] ([ID], [ListId], [ItemId], [Quantity], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ListId], [ItemId], [Quantity], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[ItemListDetails];
SET IDENTITY_INSERT [Marketing].[ItemListDetails] OFF;
GO

PRINT 'Migrating [Marketing].[ItemListHeader]...';
SET IDENTITY_INSERT [Marketing].[ItemListHeader] ON;
INSERT INTO [Marketing].[ItemListHeader] ([ID], [ListCode], [ListDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ListCode], [ListDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[ItemListHeader];
SET IDENTITY_INSERT [Marketing].[ItemListHeader] OFF;
GO

PRINT 'Migrating [Marketing].[ItemRebateGroups]...';
SET IDENTITY_INSERT [Marketing].[ItemRebateGroups] ON;
INSERT INTO [Marketing].[ItemRebateGroups] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[ItemRebateGroups];
SET IDENTITY_INSERT [Marketing].[ItemRebateGroups] OFF;
GO

PRINT 'Migrating [Marketing].[ItemSalesControl]...';
SET IDENTITY_INSERT [Marketing].[ItemSalesControl] ON;
INSERT INTO [Marketing].[ItemSalesControl] ([ID], [itemID], [SalesControlEnumID], [StartDate], [EndDate], [StockID], [BatchID], [SerialNumber], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [itemID], [SalesControlEnumID], [StartDate], [EndDate], [StockID], [BatchID], [SerialNumber], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[ItemSalesControl];
SET IDENTITY_INSERT [Marketing].[ItemSalesControl] OFF;
GO

PRINT 'Migrating [Marketing].[LeadsTransActionAddress]...';
SET IDENTITY_INSERT [Marketing].[LeadsTransActionAddress] ON;
INSERT INTO [Marketing].[LeadsTransActionAddress] ([ID], [LeadsId], [Address], [Description], [Purpose], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [LeadsId], [Address], [Description], [Purpose], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[LeadsTransActionAddress];
SET IDENTITY_INSERT [Marketing].[LeadsTransActionAddress] OFF;
GO

PRINT 'Migrating [Marketing].[LeadsTransActionContactInformation]...';
SET IDENTITY_INSERT [Marketing].[LeadsTransActionContactInformation] ON;
INSERT INTO [Marketing].[LeadsTransActionContactInformation] ([ID], [LeadsId], [Type], [Description], [NumberOrAddress], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [LeadsId], [Type], [Description], [NumberOrAddress], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[LeadsTransActionContactInformation];
SET IDENTITY_INSERT [Marketing].[LeadsTransActionContactInformation] OFF;
GO

PRINT 'Migrating [Marketing].[LeadsTransAction]...';
SET IDENTITY_INSERT [Marketing].[LeadsTransAction] ON;
INSERT INTO [Marketing].[LeadsTransAction] ([ID], [Code], [Subject], [Status], [QualifyingProcess], [EmployeeId], [LeadsTypesId], [PriorityId], [RatingId], [SalesUnitId], [Comment], [Note], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Subject], [Status], [QualifyingProcess], [EmployeeId], [LeadsTypesId], [PriorityId], [RatingId], [SalesUnitId], [Comment], [Note], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[LeadsTransAction];
SET IDENTITY_INSERT [Marketing].[LeadsTransAction] OFF;
GO

PRINT 'Migrating [Marketing].[Leads]...';
SET IDENTITY_INSERT [Marketing].[Leads] ON;
INSERT INTO [Marketing].[Leads] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[Leads];
SET IDENTITY_INSERT [Marketing].[Leads] OFF;
GO

PRINT 'Migrating [Marketing].[LineOFBusiness]...';
SET IDENTITY_INSERT [Marketing].[LineOFBusiness] ON;
INSERT INTO [Marketing].[LineOFBusiness] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[LineOFBusiness];
SET IDENTITY_INSERT [Marketing].[LineOFBusiness] OFF;
GO

PRINT 'Migrating [Marketing].[MailingCategories]...';
SET IDENTITY_INSERT [Marketing].[MailingCategories] ON;
INSERT INTO [Marketing].[MailingCategories] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[MailingCategories];
SET IDENTITY_INSERT [Marketing].[MailingCategories] OFF;
GO

PRINT 'Migrating [Marketing].[MailingItems]...';
SET IDENTITY_INSERT [Marketing].[MailingItems] ON;
INSERT INTO [Marketing].[MailingItems] ([ID], [MailingCategoriesID], [Item], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [MailingCategoriesID], [Item], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[MailingItems];
SET IDENTITY_INSERT [Marketing].[MailingItems] OFF;
GO

PRINT 'Migrating [Marketing].[OpportunitiesCompetitors]...';
SET IDENTITY_INSERT [Marketing].[OpportunitiesCompetitors] ON;
INSERT INTO [Marketing].[OpportunitiesCompetitors] ([ID], [OpportunitiesId], [Winner], [Competitor], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [OpportunitiesId], [Winner], [Competitor], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[OpportunitiesCompetitors];
SET IDENTITY_INSERT [Marketing].[OpportunitiesCompetitors] OFF;
GO

PRINT 'Migrating [Marketing].[OpportunitiesContactInformation]...';
SET IDENTITY_INSERT [Marketing].[OpportunitiesContactInformation] ON;
INSERT INTO [Marketing].[OpportunitiesContactInformation] ([ID], [OpportunitiesId], [Type], [Description], [Extension], [ContactNumberOrAddress], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [OpportunitiesId], [Type], [Description], [Extension], [ContactNumberOrAddress], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[OpportunitiesContactInformation];
SET IDENTITY_INSERT [Marketing].[OpportunitiesContactInformation] OFF;
GO

PRINT 'Migrating [Marketing].[OpportunitiesContact]...';
SET IDENTITY_INSERT [Marketing].[OpportunitiesContact] ON;
INSERT INTO [Marketing].[OpportunitiesContact] ([ID], [OpportunitiesId], [Name], [ContactFor], [Telephone], [Extension], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [OpportunitiesId], [Name], [ContactFor], [Telephone], [Extension], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[OpportunitiesContact];
SET IDENTITY_INSERT [Marketing].[OpportunitiesContact] OFF;
GO

PRINT 'Migrating [Marketing].[Opportunities]...';
SET IDENTITY_INSERT [Marketing].[Opportunities] ON;
INSERT INTO [Marketing].[Opportunities] ([ID], [Code], [Subject], [Name], [Status], [EmployeeId], [PrognosisId], [Date], [ProbabilityId], [SalesUnitId], [SalesProcessId], [EstimatedRevenue], [InternetURL], [ExternalURL], [Notes], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Subject], [Name], [Status], [EmployeeId], [PrognosisId], [Date], [ProbabilityId], [SalesUnitId], [SalesProcessId], [EstimatedRevenue], [InternetURL], [ExternalURL], [Notes], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[Opportunities];
SET IDENTITY_INSERT [Marketing].[Opportunities] OFF;
GO

PRINT 'Migrating [Marketing].[PackageAppearance]...';
SET IDENTITY_INSERT [Marketing].[PackageAppearance] ON;
INSERT INTO [Marketing].[PackageAppearance] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[PackageAppearance];
SET IDENTITY_INSERT [Marketing].[PackageAppearance] OFF;
GO

PRINT 'Migrating [Marketing].[Phases]...';
SET IDENTITY_INSERT [Marketing].[Phases] ON;
INSERT INTO [Marketing].[Phases] ([ID], [Code], [Name], [Description], [SortNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [SortNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[Phases];
SET IDENTITY_INSERT [Marketing].[Phases] OFF;
GO

PRINT 'Migrating [Marketing].[Probability]...';
SET IDENTITY_INSERT [Marketing].[Probability] ON;
INSERT INTO [Marketing].[Probability] ([ID], [Code], [Probabilitys], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Probabilitys], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[Probability];
SET IDENTITY_INSERT [Marketing].[Probability] OFF;
GO

PRINT 'Migrating [Marketing].[Prognosis]...';
SET IDENTITY_INSERT [Marketing].[Prognosis] ON;
INSERT INTO [Marketing].[Prognosis] ([ID], [Code], [Name], [Description], [FromDays], [ToDays], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [FromDays], [ToDays], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[Prognosis];
SET IDENTITY_INSERT [Marketing].[Prognosis] OFF;
GO

PRINT 'Migrating [Marketing].[ProspectRelation]...';
SET IDENTITY_INSERT [Marketing].[ProspectRelation] ON;
INSERT INTO [Marketing].[ProspectRelation] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[ProspectRelation];
SET IDENTITY_INSERT [Marketing].[ProspectRelation] OFF;
GO

PRINT 'Migrating [Marketing].[Prospect]...';
SET IDENTITY_INSERT [Marketing].[Prospect] ON;
INSERT INTO [Marketing].[Prospect] ([ID], [Code], [RecordTypeEnum], [FirstName], [MiddleName], [LastName], [RelationTypesLookup], [DeliveryLookup], [DeliveryModeLookup], [CurrencyId], [CountryId], [PostalCode], [Street], [CityId], [State], [Phone], [Extension], [Fax], [EMailAddress], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [RecordTypeEnum], [FirstName], [MiddleName], [LastName], [RelationTypesLookup], [DeliveryLookup], [DeliveryModeLookup], [CurrencyId], [CountryId], [PostalCode], [Street], [CityId], [State], [Phone], [Extension], [Fax], [EMailAddress], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[Prospect];
SET IDENTITY_INSERT [Marketing].[Prospect] OFF;
GO

PRINT 'Migrating [Marketing].[QualifyingProcess]...';
SET IDENTITY_INSERT [Marketing].[QualifyingProcess] ON;
INSERT INTO [Marketing].[QualifyingProcess] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[QualifyingProcess];
SET IDENTITY_INSERT [Marketing].[QualifyingProcess] OFF;
GO

PRINT 'Migrating [Marketing].[Questionnaire]...';
SET IDENTITY_INSERT [Marketing].[Questionnaire] ON;
INSERT INTO [Marketing].[Questionnaire] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[Questionnaire];
SET IDENTITY_INSERT [Marketing].[Questionnaire] OFF;
GO

PRINT 'Migrating [Marketing].[QuotationsDocumentConclusions]...';
SET IDENTITY_INSERT [Marketing].[QuotationsDocumentConclusions] ON;
INSERT INTO [Marketing].[QuotationsDocumentConclusions] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[QuotationsDocumentConclusions];
SET IDENTITY_INSERT [Marketing].[QuotationsDocumentConclusions] OFF;
GO

PRINT 'Migrating [Marketing].[QuotationsDocumentTitles]...';
SET IDENTITY_INSERT [Marketing].[QuotationsDocumentTitles] ON;
INSERT INTO [Marketing].[QuotationsDocumentTitles] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[QuotationsDocumentTitles];
SET IDENTITY_INSERT [Marketing].[QuotationsDocumentTitles] OFF;
GO

PRINT 'Migrating [Marketing].[QuotationsDocumentintroDuctions]...';
SET IDENTITY_INSERT [Marketing].[QuotationsDocumentintroDuctions] ON;
INSERT INTO [Marketing].[QuotationsDocumentintroDuctions] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[QuotationsDocumentintroDuctions];
SET IDENTITY_INSERT [Marketing].[QuotationsDocumentintroDuctions] OFF;
GO

PRINT 'Migrating [Marketing].[QuotationsTemplateGroups]...';
SET IDENTITY_INSERT [Marketing].[QuotationsTemplateGroups] ON;
INSERT INTO [Marketing].[QuotationsTemplateGroups] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[QuotationsTemplateGroups];
SET IDENTITY_INSERT [Marketing].[QuotationsTemplateGroups] OFF;
GO

PRINT 'Migrating [Marketing].[QuotationsType]...';
SET IDENTITY_INSERT [Marketing].[QuotationsType] ON;
INSERT INTO [Marketing].[QuotationsType] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[QuotationsType];
SET IDENTITY_INSERT [Marketing].[QuotationsType] OFF;
GO

PRINT 'Migrating [Marketing].[Rating]...';
SET IDENTITY_INSERT [Marketing].[Rating] ON;
INSERT INTO [Marketing].[Rating] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[Rating];
SET IDENTITY_INSERT [Marketing].[Rating] OFF;
GO

PRINT 'Migrating [Marketing].[ReasonsCanceld]...';
SET IDENTITY_INSERT [Marketing].[ReasonsCanceld] ON;
INSERT INTO [Marketing].[ReasonsCanceld] ([ID], [ReasonCancelCode], [ReasonCancelName], [ReasonCancelDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ReasonCancelCode], [ReasonCancelName], [ReasonCancelDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[ReasonsCanceld];
SET IDENTITY_INSERT [Marketing].[ReasonsCanceld] OFF;
GO

PRINT 'Migrating [Marketing].[RebateProgramType]...';
SET IDENTITY_INSERT [Marketing].[RebateProgramType] ON;
INSERT INTO [Marketing].[RebateProgramType] ([ID], [Code], [Description], [RebateType], [DefualtAccuralAccountID], [DefualtexpensesAccountID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Description], [RebateType], [DefualtAccuralAccountID], [DefualtexpensesAccountID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[RebateProgramType];
SET IDENTITY_INSERT [Marketing].[RebateProgramType] OFF;
GO

PRINT 'Migrating [Marketing].[Responsibilties]...';
SET IDENTITY_INSERT [Marketing].[Responsibilties] ON;
INSERT INTO [Marketing].[Responsibilties] ([ID], [ResponsibilityCode], [ResponsibilityName], [ResponsibilityDescription], [ISLeads], [ISopportunity], [ISProspect], [ISCustomer], [ISVendor], [ISCampaign], [ISCallList], [ISCase], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ResponsibilityCode], [ResponsibilityName], [ResponsibilityDescription], [ISLeads], [ISopportunity], [ISProspect], [ISCustomer], [ISVendor], [ISCampaign], [ISCallList], [ISCase], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[Responsibilties];
SET IDENTITY_INSERT [Marketing].[Responsibilties] OFF;
GO

PRINT 'Migrating [Marketing].[ReturnSalesOrderDetails]...';
SET IDENTITY_INSERT [Marketing].[ReturnSalesOrderDetails] ON;
INSERT INTO [Marketing].[ReturnSalesOrderDetails] ([ID], [ReturnSalesOrder], [StockId], [ItemId], [Quantity], [UnitId], [UnitPrice], [NetAmount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ReturnSalesOrder], [StockId], [ItemId], [Quantity], [UnitId], [UnitPrice], [NetAmount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[ReturnSalesOrderDetails];
SET IDENTITY_INSERT [Marketing].[ReturnSalesOrderDetails] OFF;
GO

PRINT 'Migrating [Marketing].[ReturnSalesOrder]...';
SET IDENTITY_INSERT [Marketing].[ReturnSalesOrder] ON;
INSERT INTO [Marketing].[ReturnSalesOrder] ([ID], [ReturnReasonCodeId], [SalesOrderId], [Code], [CustomerAccount], [Name], [ContactId], [DeliveryName], [DeliveryAddress], [Address], [Status], [DeliveryContact], [InvoiceAccount], [OrderType], [CustomerRequisition], [CustomerReference], [CurrencyId], [RequestedShipDate], [StocksId], [RequestedReceiptDate], [DeliverydateControlId], [ModeOfDeliveryId], [DeliveryTermsId], [ExpediteId], [CarrierAccounNumber], [ShippingLocationTimeZone], [LastModifiedBy], [CreatedBy], [CreatedDate], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ReturnReasonCodeId], [SalesOrderId], [Code], [CustomerAccount], [Name], [ContactId], [DeliveryName], [DeliveryAddress], [Address], [Status], [DeliveryContact], [InvoiceAccount], [OrderType], [CustomerRequisition], [CustomerReference], [CurrencyId], [RequestedShipDate], [StocksId], [RequestedReceiptDate], [DeliverydateControlId], [ModeOfDeliveryId], [DeliveryTermsId], [ExpediteId], [CarrierAccounNumber], [ShippingLocationTimeZone], [LastModifiedBy], [CreatedBy], [CreatedDate], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[ReturnSalesOrder];
SET IDENTITY_INSERT [Marketing].[ReturnSalesOrder] OFF;
GO

PRINT 'Migrating [Marketing].[ReturnSalesReasonCode]...';
SET IDENTITY_INSERT [Marketing].[ReturnSalesReasonCode] ON;
INSERT INTO [Marketing].[ReturnSalesReasonCode] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[ReturnSalesReasonCode];
SET IDENTITY_INSERT [Marketing].[ReturnSalesReasonCode] OFF;
GO

PRINT 'Migrating [Marketing].[SalesAgreementClassifications]...';
SET IDENTITY_INSERT [Marketing].[SalesAgreementClassifications] ON;
INSERT INTO [Marketing].[SalesAgreementClassifications] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[SalesAgreementClassifications];
SET IDENTITY_INSERT [Marketing].[SalesAgreementClassifications] OFF;
GO

PRINT 'Migrating [Marketing].[SalesDistricts]...';
SET IDENTITY_INSERT [Marketing].[SalesDistricts] ON;
INSERT INTO [Marketing].[SalesDistricts] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[SalesDistricts];
SET IDENTITY_INSERT [Marketing].[SalesDistricts] OFF;
GO

PRINT 'Migrating [Marketing].[SalesOrderDetails]...';
SET IDENTITY_INSERT [Marketing].[SalesOrderDetails] ON;
INSERT INTO [Marketing].[SalesOrderDetails] ([ID], [SalesOrder], [StockId], [ItemId], [Quantity], [UnitId], [UnitPrice], [Discount], [DiscountPercentage], [NetAmount], [DeliveryNow], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [SalesOrder], [StockId], [ItemId], [Quantity], [UnitId], [UnitPrice], [Discount], [DiscountPercentage], [NetAmount], [DeliveryNow], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[SalesOrderDetails];
SET IDENTITY_INSERT [Marketing].[SalesOrderDetails] OFF;
GO

PRINT 'Migrating [Marketing].[SalesOrderInvoice]...';
SET IDENTITY_INSERT [Marketing].[SalesOrderInvoice] ON;
INSERT INTO [Marketing].[SalesOrderInvoice] ([ID], [SalesOrder], [SalesOrderCode], [NetAmount], [CustomerAccountId], [InvoiceAccountId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [SalesOrder], [SalesOrderCode], [NetAmount], [CustomerAccountId], [InvoiceAccountId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[SalesOrderInvoice];
SET IDENTITY_INSERT [Marketing].[SalesOrderInvoice] OFF;
GO

PRINT 'Migrating [Marketing].[SalesOrderPools]...';
SET IDENTITY_INSERT [Marketing].[SalesOrderPools] ON;
INSERT INTO [Marketing].[SalesOrderPools] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[SalesOrderPools];
SET IDENTITY_INSERT [Marketing].[SalesOrderPools] OFF;
GO

PRINT 'Migrating [Marketing].[SalesOrder]...';
SET IDENTITY_INSERT [Marketing].[SalesOrder] ON;
INSERT INTO [Marketing].[SalesOrder] ([ID], [Code], [CustomerAccount], [Name], [ContactId], [DeliveryName], [DeliveryAddress], [Address], [Status], [DeliveryContact], [InvoiceAccount], [OrderType], [CustomerRequisition], [CustomerReference], [CurrencyId], [RequestedShipDate], [StocksId], [RequestedReceiptDate], [DeliverydateControlId], [ModeOfDeliveryId], [DeliveryTermsId], [ExpediteId], [CarrierAccounNumber], [ShippingLocationTimeZone], [LastModifiedBy], [CreatedBy], [CreatedDate], [LastModifiedDate], [SalesBickStatus], [CompanyID], [TenantId])
SELECT [ID], [Code], [CustomerAccount], [Name], [ContactId], [DeliveryName], [DeliveryAddress], [Address], [Status], [DeliveryContact], [InvoiceAccount], [OrderType], [CustomerRequisition], [CustomerReference], [CurrencyId], [RequestedShipDate], [StocksId], [RequestedReceiptDate], [DeliverydateControlId], [ModeOfDeliveryId], [DeliveryTermsId], [ExpediteId], [CarrierAccounNumber], [ShippingLocationTimeZone], [LastModifiedBy], [CreatedBy], [CreatedDate], [LastModifiedDate], [SalesBickStatus], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[SalesOrder];
SET IDENTITY_INSERT [Marketing].[SalesOrder] OFF;
GO

PRINT 'Migrating [Marketing].[SalesOrigin]...';
SET IDENTITY_INSERT [Marketing].[SalesOrigin] ON;
INSERT INTO [Marketing].[SalesOrigin] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[SalesOrigin];
SET IDENTITY_INSERT [Marketing].[SalesOrigin] OFF;
GO

PRINT 'Migrating [Marketing].[SalesQuotations]...';
SET IDENTITY_INSERT [Marketing].[SalesQuotations] ON;
INSERT INTO [Marketing].[SalesQuotations] ([ID], [Code], [AccountType], [CustomerAccount], [InvoiceAccount], [ProspectId], [Name], [ContactId], [CompanyName], [DeliveryName], [DeliveryAddress], [Address], [QuotationType], [ExpirationDate], [CustomerRequisition], [CustomerReference], [OpportunityId], [CurrencyId], [StocksId], [RequestedReceiptDate], [RequestedShipDate], [DeliverydateControlId], [ModeOfDeliveryId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [ShippingLocationTimeZone], [CompanyID], [TenantId])
SELECT [ID], [Code], [AccountType], [CustomerAccount], [InvoiceAccount], [ProspectId], [Name], [ContactId], [CompanyName], [DeliveryName], [DeliveryAddress], [Address], [QuotationType], [ExpirationDate], [CustomerRequisition], [CustomerReference], [OpportunityId], [CurrencyId], [StocksId], [RequestedReceiptDate], [RequestedShipDate], [DeliverydateControlId], [ModeOfDeliveryId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [ShippingLocationTimeZone], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[SalesQuotations];
SET IDENTITY_INSERT [Marketing].[SalesQuotations] OFF;
GO

PRINT 'Migrating [Marketing].[SalesUnit]...';
SET IDENTITY_INSERT [Marketing].[SalesUnit] ON;
INSERT INTO [Marketing].[SalesUnit] ([ID], [Code], [Name], [Description], [ParentId], [EmployeeId], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [ParentId], [EmployeeId], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[SalesUnit];
SET IDENTITY_INSERT [Marketing].[SalesUnit] OFF;
GO

PRINT 'Migrating [Marketing].[Salutation]...';
SET IDENTITY_INSERT [Marketing].[Salutation] ON;
INSERT INTO [Marketing].[Salutation] ([ID], [SalutationCode], [SalutationName], [SalutationDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [SalutationCode], [SalutationName], [SalutationDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[Salutation];
SET IDENTITY_INSERT [Marketing].[Salutation] OFF;
GO

PRINT 'Migrating [Marketing].[Segments]...';
SET IDENTITY_INSERT [Marketing].[Segments] ON;
INSERT INTO [Marketing].[Segments] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[Segments];
SET IDENTITY_INSERT [Marketing].[Segments] OFF;
GO

PRINT 'Migrating [Marketing].[StageOptions]...';
SET IDENTITY_INSERT [Marketing].[StageOptions] ON;
INSERT INTO [Marketing].[StageOptions] ([ID], [ProcessActivityDetailsID], [ActivityTypeEnum], [ActivityName], [ActivityNumber], [Purpose], [StartInDays], [EndInDays], [StartTime], [EndTime], [Notes], [ResponsibilityID], [EmployeeID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Priority], [CompanyID], [TenantId])
SELECT [ID], [ProcessActivityDetailsID], [ActivityTypeEnum], [ActivityName], [ActivityNumber], [Purpose], [StartInDays], [EndInDays], [StartTime], [EndTime], [Notes], [ResponsibilityID], [EmployeeID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Priority], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[StageOptions];
SET IDENTITY_INSERT [Marketing].[StageOptions] OFF;
GO

PRINT 'Migrating [Marketing].[StatisticsGroup]...';
SET IDENTITY_INSERT [Marketing].[StatisticsGroup] ON;
INSERT INTO [Marketing].[StatisticsGroup] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[StatisticsGroup];
SET IDENTITY_INSERT [Marketing].[StatisticsGroup] OFF;
GO

PRINT 'Migrating [Marketing].[Status]...';
SET IDENTITY_INSERT [Marketing].[Status] ON;
INSERT INTO [Marketing].[Status] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[Status];
SET IDENTITY_INSERT [Marketing].[Status] OFF;
GO

PRINT 'Migrating [Marketing].[SupplementaryItemCustomer]...';
SET IDENTITY_INSERT [Marketing].[SupplementaryItemCustomer] ON;
INSERT INTO [Marketing].[SupplementaryItemCustomer] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[SupplementaryItemCustomer];
SET IDENTITY_INSERT [Marketing].[SupplementaryItemCustomer] OFF;
GO

PRINT 'Migrating [Marketing].[SupplemetaryItemItemCroup]...';
SET IDENTITY_INSERT [Marketing].[SupplemetaryItemItemCroup] ON;
INSERT INTO [Marketing].[SupplemetaryItemItemCroup] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[SupplemetaryItemItemCroup];
SET IDENTITY_INSERT [Marketing].[SupplemetaryItemItemCroup] OFF;
GO

PRINT 'Migrating [Marketing].[TeleMarketingReasonCanceled]...';
SET IDENTITY_INSERT [Marketing].[TeleMarketingReasonCanceled] ON;
INSERT INTO [Marketing].[TeleMarketingReasonCanceled] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[TeleMarketingReasonCanceled];
SET IDENTITY_INSERT [Marketing].[TeleMarketingReasonCanceled] OFF;
GO

PRINT 'Migrating [Marketing].[TeleMarketing]...';
SET IDENTITY_INSERT [Marketing].[TeleMarketing] ON;
INSERT INTO [Marketing].[TeleMarketing] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[TeleMarketing];
SET IDENTITY_INSERT [Marketing].[TeleMarketing] OFF;
GO

PRINT 'Migrating [Marketing].[TermsOFDelivery]...';
SET IDENTITY_INSERT [Marketing].[TermsOFDelivery] ON;
INSERT INTO [Marketing].[TermsOFDelivery] ([ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [FreightCovergeTermsEnum], [applyFreeMinimume], [FreeMinimume], [FreightChargeTermsEmun], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [FreightCovergeTermsEnum], [applyFreeMinimume], [FreeMinimume], [FreightChargeTermsEmun], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[TermsOFDelivery];
SET IDENTITY_INSERT [Marketing].[TermsOFDelivery] OFF;
GO

PRINT 'Migrating [Marketing].[complinentery]...';
SET IDENTITY_INSERT [Marketing].[complinentery] ON;
INSERT INTO [Marketing].[complinentery] ([ID], [complinenteryCode], [complinenteryName], [complinenteryDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [complinenteryCode], [complinenteryName], [complinenteryDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[complinentery];
SET IDENTITY_INSERT [Marketing].[complinentery] OFF;
GO

PRINT 'Migrating [Marketing].[decision]...';
SET IDENTITY_INSERT [Marketing].[decision] ON;
INSERT INTO [Marketing].[decision] ([ID], [decisionCode], [decisionName], [decisionDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [decisionCode], [decisionName], [decisionDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[decision];
SET IDENTITY_INSERT [Marketing].[decision] OFF;
GO

PRINT 'Migrating [Marketing].[functions]...';
SET IDENTITY_INSERT [Marketing].[functions] ON;
INSERT INTO [Marketing].[functions] ([ID], [functionsCode], [functionsName], [functionsDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [functionsCode], [functionsName], [functionsDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[functions];
SET IDENTITY_INSERT [Marketing].[functions] OFF;
GO

PRINT 'Migrating [Marketing].[interest]...';
SET IDENTITY_INSERT [Marketing].[interest] ON;
INSERT INTO [Marketing].[interest] ([ID], [interestCode], [interestName], [interestDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [interestCode], [interestName], [interestDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[interest];
SET IDENTITY_INSERT [Marketing].[interest] OFF;
GO

PRINT 'Migrating [Marketing].[loyalty]...';
SET IDENTITY_INSERT [Marketing].[loyalty] ON;
INSERT INTO [Marketing].[loyalty] ([ID], [loyaltyCode], [loyaltyName], [loyaltyDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [loyaltyCode], [loyaltyName], [loyaltyDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Marketing].[loyalty];
SET IDENTITY_INSERT [Marketing].[loyalty] OFF;
GO

PRINT 'Migrating [Nuitration].[AssginDiteMeal]...';
SET IDENTITY_INSERT [Nuitration].[AssginDiteMeal] ON;
INSERT INTO [Nuitration].[AssginDiteMeal] ([ID], [MealTypeID], [DietTypeID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [MealTypeID], [DietTypeID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nuitration].[AssginDiteMeal];
SET IDENTITY_INSERT [Nuitration].[AssginDiteMeal] OFF;
GO

PRINT 'Migrating [Nuitration].[AssignItemMeals]...';
SET IDENTITY_INSERT [Nuitration].[AssignItemMeals] ON;
INSERT INTO [Nuitration].[AssignItemMeals] ([ID], [MealTypeID], [DietTypeID], [ItemGroupID], [ItemID], [ItemsNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ItemUnit], [StockID], [TenantId])
SELECT [ID], [MealTypeID], [DietTypeID], [ItemGroupID], [ItemID], [ItemsNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ItemUnit], [StockID], @TenantId
FROM [SunCity_Clinics].[Nuitration].[AssignItemMeals];
SET IDENTITY_INSERT [Nuitration].[AssignItemMeals] OFF;
GO

PRINT 'Migrating [Nuitration].[DietRequest]...';
SET IDENTITY_INSERT [Nuitration].[DietRequest] ON;
INSERT INTO [Nuitration].[DietRequest] ([ID], [RequestDate], [PatientID], [PatientLocationID], [DietTypeID], [TemporaryMealReq], [Nourishment], [FoodPreferences], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [RequestCode], [CompanyID], [RecievedDateTimeforNurse], [IPOP], [TenantId])
SELECT [ID], [RequestDate], [PatientID], [PatientLocationID], [DietTypeID], [TemporaryMealReq], [Nourishment], [FoodPreferences], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [RequestCode], [CompanyID], [RecievedDateTimeforNurse], [IPOP], @TenantId
FROM [SunCity_Clinics].[Nuitration].[DietRequest];
SET IDENTITY_INSERT [Nuitration].[DietRequest] OFF;
GO

PRINT 'Migrating [Nuitration].[DietTypes]...';
SET IDENTITY_INSERT [Nuitration].[DietTypes] ON;
INSERT INTO [Nuitration].[DietTypes] ([ID], [LatinDescription], [LocalDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DietCode], [CompanyID], [TenantId])
SELECT [ID], [LatinDescription], [LocalDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DietCode], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nuitration].[DietTypes];
SET IDENTITY_INSERT [Nuitration].[DietTypes] OFF;
GO

PRINT 'Migrating [Nuitration].[ItemGroup]...';
SET IDENTITY_INSERT [Nuitration].[ItemGroup] ON;
INSERT INTO [Nuitration].[ItemGroup] ([ID], [LatinDescription], [LocalDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [ItemGroupCode], [CompanyID], [TenantId])
SELECT [ID], [LatinDescription], [LocalDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [ItemGroupCode], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nuitration].[ItemGroup];
SET IDENTITY_INSERT [Nuitration].[ItemGroup] OFF;
GO

PRINT 'Migrating [Nuitration].[Items]...';
SET IDENTITY_INSERT [Nuitration].[Items] ON;
INSERT INTO [Nuitration].[Items] ([ID], [LatinDescription], [LocalDescription], [ItemGroupID], [CaloriesNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [itemCode], [CompanyID], [GeneralStoresItemId], [TenantId])
SELECT [ID], [LatinDescription], [LocalDescription], [ItemGroupID], [CaloriesNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [itemCode], [CompanyID], [GeneralStoresItemId], @TenantId
FROM [SunCity_Clinics].[Nuitration].[Items];
SET IDENTITY_INSERT [Nuitration].[Items] OFF;
GO

PRINT 'Migrating [Nuitration].[MealRequest]...';
SET IDENTITY_INSERT [Nuitration].[MealRequest] ON;
INSERT INTO [Nuitration].[MealRequest] ([ID], [RequestDate], [PatientID], [MealTypeID], [ISCombanion], [MealNumber], [DietTypeID], [Note], [MRNCode], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DietRequestCode], [DietRequestDate], [CompanyID], [RecievedDateTimeforNurse], [TenantId])
SELECT [ID], [RequestDate], [PatientID], [MealTypeID], [ISCombanion], [MealNumber], [DietTypeID], [Note], [MRNCode], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DietRequestCode], [DietRequestDate], [CompanyID], [RecievedDateTimeforNurse], @TenantId
FROM [SunCity_Clinics].[Nuitration].[MealRequest];
SET IDENTITY_INSERT [Nuitration].[MealRequest] OFF;
GO

PRINT 'Migrating [Nuitration].[MealTimes]...';
SET IDENTITY_INSERT [Nuitration].[MealTimes] ON;
INSERT INTO [Nuitration].[MealTimes] ([ID], [MealTypeID], [AddTimeFrom], [AddTimeTo], [ChangeTimeFrom], [ChangeTimeTo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [MealTypeID], [AddTimeFrom], [AddTimeTo], [ChangeTimeFrom], [ChangeTimeTo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nuitration].[MealTimes];
SET IDENTITY_INSERT [Nuitration].[MealTimes] OFF;
GO

PRINT 'Migrating [Nuitration].[MealTypes]...';
SET IDENTITY_INSERT [Nuitration].[MealTypes] ON;
INSERT INTO [Nuitration].[MealTypes] ([ID], [LatinDescription], [LocalDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [MealTypeCode], [CompanyID], [DefaultDietType], [TenantId])
SELECT [ID], [LatinDescription], [LocalDescription], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [MealTypeCode], [CompanyID], [DefaultDietType], @TenantId
FROM [SunCity_Clinics].[Nuitration].[MealTypes];
SET IDENTITY_INSERT [Nuitration].[MealTypes] OFF;
GO

PRINT 'Migrating [Nuitration].[TPNCentralLineFormulas]...';
SET IDENTITY_INSERT [Nuitration].[TPNCentralLineFormulas] ON;
INSERT INTO [Nuitration].[TPNCentralLineFormulas] ([Id], [TPNTemplateId], [FormulaType], [DrugId], [Value], [UCFId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [GenericID], [StrengthID], [DrugFormID], [TenantId])
SELECT [Id], [TPNTemplateId], [FormulaType], [DrugId], [Value], [UCFId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [GenericID], [StrengthID], [DrugFormID], @TenantId
FROM [SunCity_Clinics].[Nuitration].[TPNCentralLineFormulas];
SET IDENTITY_INSERT [Nuitration].[TPNCentralLineFormulas] OFF;
GO

PRINT 'Migrating [Nuitration].[TPNOrderCentralLineFormulas]...';
SET IDENTITY_INSERT [Nuitration].[TPNOrderCentralLineFormulas] ON;
INSERT INTO [Nuitration].[TPNOrderCentralLineFormulas] ([Id], [TPNCentralLineFormulasId], [TPNOrderHeaderId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DispensedDrugId], [TenantId])
SELECT [Id], [TPNCentralLineFormulasId], [TPNOrderHeaderId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DispensedDrugId], @TenantId
FROM [SunCity_Clinics].[Nuitration].[TPNOrderCentralLineFormulas];
SET IDENTITY_INSERT [Nuitration].[TPNOrderCentralLineFormulas] OFF;
GO

PRINT 'Migrating [Nuitration].[TPNOrderHeader]...';
SET IDENTITY_INSERT [Nuitration].[TPNOrderHeader] ON;
INSERT INTO [Nuitration].[TPNOrderHeader] ([Id], [TPNOrderCode], [DoctorId], [AdmitPatientId], [TPNTemplateId], [Status], [FatEmulsion], [Rate], [NumberofHours], [CancelReason], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [Id], [TPNOrderCode], [DoctorId], [AdmitPatientId], [TPNTemplateId], [Status], [FatEmulsion], [Rate], [NumberofHours], [CancelReason], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[Nuitration].[TPNOrderHeader];
SET IDENTITY_INSERT [Nuitration].[TPNOrderHeader] OFF;
GO

PRINT 'Migrating [Nuitration].[TPNOrderPeripheralLineFormulas]...';
SET IDENTITY_INSERT [Nuitration].[TPNOrderPeripheralLineFormulas] ON;
INSERT INTO [Nuitration].[TPNOrderPeripheralLineFormulas] ([Id], [TPNPeripheralLineFormulasId], [TPNOrderHeaderId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DispensedDrugId], [TenantId])
SELECT [Id], [TPNPeripheralLineFormulasId], [TPNOrderHeaderId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DispensedDrugId], @TenantId
FROM [SunCity_Clinics].[Nuitration].[TPNOrderPeripheralLineFormulas];
SET IDENTITY_INSERT [Nuitration].[TPNOrderPeripheralLineFormulas] OFF;
GO

PRINT 'Migrating [Nuitration].[TPNPeripheralLineFormulas]...';
SET IDENTITY_INSERT [Nuitration].[TPNPeripheralLineFormulas] ON;
INSERT INTO [Nuitration].[TPNPeripheralLineFormulas] ([Id], [TPNTemplateId], [FormulaType], [DrugId], [Value], [UCFId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [GenericID], [StrengthID], [DrugFormID], [TenantId])
SELECT [Id], [TPNTemplateId], [FormulaType], [DrugId], [Value], [UCFId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [GenericID], [StrengthID], [DrugFormID], @TenantId
FROM [SunCity_Clinics].[Nuitration].[TPNPeripheralLineFormulas];
SET IDENTITY_INSERT [Nuitration].[TPNPeripheralLineFormulas] OFF;
GO

PRINT 'Migrating [Nuitration].[TPNTemplate]...';
SET IDENTITY_INSERT [Nuitration].[TPNTemplate] ON;
INSERT INTO [Nuitration].[TPNTemplate] ([Id], [TemplateCode], [TemplateName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [TemplateCode], [TemplateName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nuitration].[TPNTemplate];
SET IDENTITY_INSERT [Nuitration].[TPNTemplate] OFF;
GO

PRINT 'Migrating [Nursing].[AssignNurseToPatients]...';
SET IDENTITY_INSERT [Nursing].[AssignNurseToPatients] ON;
INSERT INTO [Nursing].[AssignNurseToPatients] ([Id], [SpecialityId], [WardId], [IPNumber], [PatientId], [SessionId], [NurseId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [Id], [SpecialityId], [WardId], [IPNumber], [PatientId], [SessionId], [NurseId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[Nursing].[AssignNurseToPatients];
SET IDENTITY_INSERT [Nursing].[AssignNurseToPatients] OFF;
GO

PRINT 'Migrating [Nursing].[Bed]...';
SET IDENTITY_INSERT [Nursing].[Bed] ON;
INSERT INTO [Nursing].[Bed] ([Id], [WardID], [RoomID], [BedNumber], [BedDescription], [BedStatues], [IsChild], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [WardID], [RoomID], [BedNumber], [BedDescription], [BedStatues], [IsChild], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[Bed];
SET IDENTITY_INSERT [Nursing].[Bed] OFF;
GO

PRINT 'Migrating [Nursing].[CareCategory]...';
SET IDENTITY_INSERT [Nursing].[CareCategory] ON;
INSERT INTO [Nursing].[CareCategory] ([ID], [NameArabic], [NameEnglish], [Active], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [NameArabic], [NameEnglish], [Active], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[CareCategory];
SET IDENTITY_INSERT [Nursing].[CareCategory] OFF;
GO

PRINT 'Migrating [Nursing].[Clothing]...';
SET IDENTITY_INSERT [Nursing].[Clothing] ON;
INSERT INTO [Nursing].[Clothing] ([ID], [Code], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[Clothing];
SET IDENTITY_INSERT [Nursing].[Clothing] OFF;
GO

PRINT 'Migrating [Nursing].[Designation]...';
SET IDENTITY_INSERT [Nursing].[Designation] ON;
INSERT INTO [Nursing].[Designation] ([Id], [Code], [Description], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Code], [Description], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[Designation];
SET IDENTITY_INSERT [Nursing].[Designation] OFF;
GO

PRINT 'Migrating [Nursing].[DischargNurseData]...';
SET IDENTITY_INSERT [Nursing].[DischargNurseData] ON;
INSERT INTO [Nursing].[DischargNurseData] ([ID], [PatientId], [InformationSource], [AccompanyName], [RelativeRelationId], [NurseId], [DischargStatusId], [Temperature], [Pulse], [reaction], [BloodPressure], [Height], [weight], [OrganicStatus], [PsychologicalStatus], [Notes], [Instructions], [ChildAccompanion], [ChildRelativeRelationId], [RoutineTests], [purity], [bracelet], [Newbornbrochure], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [PatientId], [InformationSource], [AccompanyName], [RelativeRelationId], [NurseId], [DischargStatusId], [Temperature], [Pulse], [reaction], [BloodPressure], [Height], [weight], [OrganicStatus], [PsychologicalStatus], [Notes], [Instructions], [ChildAccompanion], [ChildRelativeRelationId], [RoutineTests], [purity], [bracelet], [Newbornbrochure], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[DischargNurseData];
SET IDENTITY_INSERT [Nursing].[DischargNurseData] OFF;
GO

PRINT 'Migrating [Nursing].[DischargNurseInstruction]...';
SET IDENTITY_INSERT [Nursing].[DischargNurseInstruction] ON;
INSERT INTO [Nursing].[DischargNurseInstruction] ([ID], [DischargNurseDataId], [NurseInstructionsId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [DischargNurseDataId], [NurseInstructionsId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[DischargNurseInstruction];
SET IDENTITY_INSERT [Nursing].[DischargNurseInstruction] OFF;
GO

PRINT 'Migrating [Nursing].[DischargeChecklist]...';
SET IDENTITY_INSERT [Nursing].[DischargeChecklist] ON;
INSERT INTO [Nursing].[DischargeChecklist] ([Id], [PatientId], [DischargePermitsAvailable], [OutpatientClinicAppointmentMade], [X_Rays], [AddressoGraphCardWithOPC], [TakeHomeMedications], [VaccinesGiven], [SickLeave], [CopiesOfLaboratory], [X_RayFilms], [ValuablesReturned], [CheckAndRemoveHeplock], [ChargeNurse], [HouseKeeping], [Dietary], [NursesNotes], [DischargeBook], [BedStatement], [Re_ArrangeFile], [FileReturnedToMedicalRecords], [TenantId])
SELECT [Id], [PatientId], [DischargePermitsAvailable], [OutpatientClinicAppointmentMade], [X_Rays], [AddressoGraphCardWithOPC], [TakeHomeMedications], [VaccinesGiven], [SickLeave], [CopiesOfLaboratory], [X_RayFilms], [ValuablesReturned], [CheckAndRemoveHeplock], [ChargeNurse], [HouseKeeping], [Dietary], [NursesNotes], [DischargeBook], [BedStatement], [Re_ArrangeFile], [FileReturnedToMedicalRecords], @TenantId
FROM [SunCity_Clinics].[Nursing].[DischargeChecklist];
SET IDENTITY_INSERT [Nursing].[DischargeChecklist] OFF;
GO

PRINT 'Migrating [Nursing].[DoctorInstructions]...';
SET IDENTITY_INSERT [Nursing].[DoctorInstructions] ON;
INSERT INTO [Nursing].[DoctorInstructions] ([Id], [PatientID], [DoctorID], [InstructionsDesc], [InstructionsStatues], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [DoctorInstructionClassID], [IpNumber], [FrequencyId], [FrequencyNumber], [orderurencyId], [OrderStartDate], [OrderdurationId], [OrderDurationNumber], [TenantId])
SELECT [Id], [PatientID], [DoctorID], [InstructionsDesc], [InstructionsStatues], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [DoctorInstructionClassID], [IpNumber], [FrequencyId], [FrequencyNumber], [orderurencyId], [OrderStartDate], [OrderdurationId], [OrderDurationNumber], @TenantId
FROM [SunCity_Clinics].[Nursing].[DoctorInstructions];
SET IDENTITY_INSERT [Nursing].[DoctorInstructions] OFF;
GO

PRINT 'Migrating [Nursing].[DrugChart]...';
SET IDENTITY_INSERT [Nursing].[DrugChart] ON;
INSERT INTO [Nursing].[DrugChart] ([Id], [PatientID], [IPNO], [DrugCode], [DrugUnit], [Time], [DateFrom], [DateTo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [IPNO], [DrugCode], [DrugUnit], [Time], [DateFrom], [DateTo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[DrugChart];
SET IDENTITY_INSERT [Nursing].[DrugChart] OFF;
GO

PRINT 'Migrating [Nursing].[EmployeeHaifTimeDays]...';
SET IDENTITY_INSERT [Nursing].[EmployeeHaifTimeDays] ON;
INSERT INTO [Nursing].[EmployeeHaifTimeDays] ([ID], [EmployeeID], [DayId], [TenantId])
SELECT [ID], [EmployeeID], [DayId], @TenantId
FROM [SunCity_Clinics].[Nursing].[EmployeeHaifTimeDays];
SET IDENTITY_INSERT [Nursing].[EmployeeHaifTimeDays] OFF;
GO

PRINT 'Migrating [Nursing].[EmployeeJobHistory]...';
SET IDENTITY_INSERT [Nursing].[EmployeeJobHistory] ON;
INSERT INTO [Nursing].[EmployeeJobHistory] ([ID], [EmployeeID], [JobID], [ChangeDate], [UserID], [CompanyID], [TenantId])
SELECT [ID], [EmployeeID], [JobID], [ChangeDate], [UserID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[EmployeeJobHistory];
SET IDENTITY_INSERT [Nursing].[EmployeeJobHistory] OFF;
GO

PRINT 'Migrating [Nursing].[EmployeeSubDepartmentHistory]...';
SET IDENTITY_INSERT [Nursing].[EmployeeSubDepartmentHistory] ON;
INSERT INTO [Nursing].[EmployeeSubDepartmentHistory] ([ID], [EmployeeID], [SubDepartmentID], [ChangeDate], [UserID], [CompanyID], [TenantId])
SELECT [ID], [EmployeeID], [SubDepartmentID], [ChangeDate], [UserID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[EmployeeSubDepartmentHistory];
SET IDENTITY_INSERT [Nursing].[EmployeeSubDepartmentHistory] OFF;
GO

PRINT 'Migrating [Nursing].[EmployeeTypeofContractHistory]...';
SET IDENTITY_INSERT [Nursing].[EmployeeTypeofContractHistory] ON;
INSERT INTO [Nursing].[EmployeeTypeofContractHistory] ([ID], [EmployeeID], [TypeofContractID], [ChangeDate], [UserID], [CompanyID], [TenantId])
SELECT [ID], [EmployeeID], [TypeofContractID], [ChangeDate], [UserID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[EmployeeTypeofContractHistory];
SET IDENTITY_INSERT [Nursing].[EmployeeTypeofContractHistory] OFF;
GO

PRINT 'Migrating [Nursing].[Employee]...';
SET IDENTITY_INSERT [Nursing].[Employee] ON;
INSERT INTO [Nursing].[Employee] ([Id], [EmployeeCode], [FirstName], [MiddleName], [BeforeLastName], [LastName], [Type], [IsActive], [SubDepartmentId], [JobId], [EmpDesignation], [EmpGrade], [EmergencyContactName], [EmergencyContactRelation], [EmergencyContactPhone], [LocalAddress1], [LocalAddress2], [LocalAddress3], [LocalAddress4], [PermanentAddress1], [PermanentAddress2], [PermanentAddress3], [PermanentAddress4], [BirthDate], [Gender], [Nationality], [MaritalStatus], [AppiontmentDate], [JoiningDate], [ConfermationDate], [ProbationPeriod], [Remark], [Image], [UserId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [NoticePeriod], [DistrictID], [GovernemenrID], [RelaginID], [BirthDatePlace], [NationalID], [NationalIDEnddate], [holidaysRoleID], [NoramalDaysVacation], [CasualDaysVacation], [Status], [MilitaryState], [MilitaryStateEndDate], [SessionID], [CardNumber], [CompanyID], [FirstNameEn], [MiddleNameEn], [BeforeLastNameEn], [LastNameEn], [FirstNameAr], [MiddleNameAr], [BeforeLastNameAr], [LastNameAr], [ResidencyExpire], [EmpImage], [hasInsurance], [InsuranceID], [TaxesID], [Classification], [durabilitytypes], [TypeofContractId], [WorkPhone], [PlaceIssuingIdentity], [passportnum], [Passportexpirationdate], [Contractstartingdate], [expirydateofcontract], [Drivinglicensenumber], [dateofissuanceoflicense], [Licenseexpirationdate], [SyndicateCard], [WorkPermit], [ExpirDateOfWorkPermit], [BloodType], [InsuranceDate], [InsuranceValue], [InsuranceJobTitle], [InsuranceNum], [IsCader], [BasicSalaryType], [BasicSalary], [BasicSalaryOf306], [ProbationPeriodType], [IsResearch], [IsSpecialNeeds], [JobDegreeID], [IdentificationTypeID], [ExcludeFromPayroll], [UnionID], [IsHalfTime], [TerminateID], [TerminateReason], [boxmembershipnumber], [IsCEO], [BoxMemberShipDate], [CountryId], [SalayFrom], [BankName], [BankBranch], [EmployeeAccount], [ClearanceDate], [EmployeeCodeInEPayment], [EmployeeType], [OtherPaymenyMethod], [ProbationPeriodEndDate], [NoticePeriodNumberN], [NoticePeriodN], [ISTravelling], [NumberOfTickets], [CountryTravelling], [EmployeeEmail], [EmployeeJobEmail], [transferallow], [housingAllow], [transportallowance], [housingallowance], [PensionAccount], [VacAccount], [TicketAccount], [SalaryAccount], [TerminateDate], [BranchId], [PatientID], [AttendanceMachineCode], [StaffType], [ClincalStaff], [TenantId])
SELECT [Id], [EmployeeCode], [FirstName], [MiddleName], [BeforeLastName], [LastName], [Type], [IsActive], [SubDepartmentId], [JobId], [EmpDesignation], [EmpGrade], [EmergencyContactName], [EmergencyContactRelation], [EmergencyContactPhone], [LocalAddress1], [LocalAddress2], [LocalAddress3], [LocalAddress4], [PermanentAddress1], [PermanentAddress2], [PermanentAddress3], [PermanentAddress4], [BirthDate], [Gender], [Nationality], [MaritalStatus], [AppiontmentDate], [JoiningDate], [ConfermationDate], [ProbationPeriod], [Remark], [Image], [UserId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [NoticePeriod], [DistrictID], [GovernemenrID], [RelaginID], [BirthDatePlace], [NationalID], [NationalIDEnddate], [holidaysRoleID], [NoramalDaysVacation], [CasualDaysVacation], [Status], [MilitaryState], [MilitaryStateEndDate], [SessionID], [CardNumber], [CompanyID], [FirstNameEn], [MiddleNameEn], [BeforeLastNameEn], [LastNameEn], [FirstNameAr], [MiddleNameAr], [BeforeLastNameAr], [LastNameAr], [ResidencyExpire], [EmpImage], [hasInsurance], [InsuranceID], [TaxesID], [Classification], [durabilitytypes], [TypeofContractId], [WorkPhone], [PlaceIssuingIdentity], [passportnum], [Passportexpirationdate], [Contractstartingdate], [expirydateofcontract], [Drivinglicensenumber], [dateofissuanceoflicense], [Licenseexpirationdate], [SyndicateCard], [WorkPermit], [ExpirDateOfWorkPermit], [BloodType], [InsuranceDate], [InsuranceValue], [InsuranceJobTitle], [InsuranceNum], [IsCader], [BasicSalaryType], [BasicSalary], [BasicSalaryOf306], [ProbationPeriodType], [IsResearch], [IsSpecialNeeds], [JobDegreeID], [IdentificationTypeID], [ExcludeFromPayroll], [UnionID], [IsHalfTime], [TerminateID], [TerminateReason], [boxmembershipnumber], [IsCEO], [BoxMemberShipDate], [CountryId], [SalayFrom], [BankName], [BankBranch], [EmployeeAccount], [ClearanceDate], [EmployeeCodeInEPayment], [EmployeeType], [OtherPaymenyMethod], [ProbationPeriodEndDate], [NoticePeriodNumberN], [NoticePeriodN], [ISTravelling], [NumberOfTickets], [CountryTravelling], [EmployeeEmail], [EmployeeJobEmail], [transferallow], [housingAllow], [transportallowance], [housingallowance], [PensionAccount], [VacAccount], [TicketAccount], [SalaryAccount], [TerminateDate], [BranchId], [PatientID], [AttendanceMachineCode], [StaffType], [ClincalStaff], @TenantId
FROM [SunCity_Clinics].[Nursing].[Employee];
SET IDENTITY_INSERT [Nursing].[Employee] OFF;
GO

PRINT 'Migrating [Nursing].[FollowUpPatientRestraint]...';
SET IDENTITY_INSERT [Nursing].[FollowUpPatientRestraint] ON;
INSERT INTO [Nursing].[FollowUpPatientRestraint] ([Id], [Time], [PatientStatus], [TightnessOfRestraints], [PeripheralVasculature], [CN_RN], [DateTimeOfRemoval], [RemovedBy], [PatientRestraintID], [TenantId])
SELECT [Id], [Time], [PatientStatus], [TightnessOfRestraints], [PeripheralVasculature], [CN_RN], [DateTimeOfRemoval], [RemovedBy], [PatientRestraintID], @TenantId
FROM [SunCity_Clinics].[Nursing].[FollowUpPatientRestraint];
SET IDENTITY_INSERT [Nursing].[FollowUpPatientRestraint] OFF;
GO

PRINT 'Migrating [Nursing].[Grade]...';
SET IDENTITY_INSERT [Nursing].[Grade] ON;
INSERT INTO [Nursing].[Grade] ([Id], [Code], [Description], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Code], [Description], [Name], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[Grade];
SET IDENTITY_INSERT [Nursing].[Grade] OFF;
GO

PRINT 'Migrating [Nursing].[GradingType]...';
SET IDENTITY_INSERT [Nursing].[GradingType] ON;
INSERT INTO [Nursing].[GradingType] ([ID], [NameArabic], [NameEnglish], [Active], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [NameArabic], [NameEnglish], [Active], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[GradingType];
SET IDENTITY_INSERT [Nursing].[GradingType] OFF;
GO

PRINT 'Migrating [Nursing].[HabitMaster]...';
SET IDENTITY_INSERT [Nursing].[HabitMaster] ON;
INSERT INTO [Nursing].[HabitMaster] ([ID], [NameArabic], [NameEnglish], [Active], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [NameArabic], [NameEnglish], [Active], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[HabitMaster];
SET IDENTITY_INSERT [Nursing].[HabitMaster] OFF;
GO

PRINT 'Migrating [Nursing].[ICDCodes]...';
SET IDENTITY_INSERT [Nursing].[ICDCodes] ON;
INSERT INTO [Nursing].[ICDCodes] ([Id], [ICDCode], [ICDDescription], [ICDAlias], [ICDGroupsId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ParentId], [Level], [TenantId])
SELECT [Id], [ICDCode], [ICDDescription], [ICDAlias], [ICDGroupsId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ParentId], [Level], @TenantId
FROM [SunCity_Clinics].[Nursing].[ICDCodes];
SET IDENTITY_INSERT [Nursing].[ICDCodes] OFF;
GO

PRINT 'Migrating [Nursing].[ICDGroups]...';
SET IDENTITY_INSERT [Nursing].[ICDGroups] ON;
INSERT INTO [Nursing].[ICDGroups] ([Id], [GroupCode], [GroupName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [GroupCode], [GroupName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[ICDGroups];
SET IDENTITY_INSERT [Nursing].[ICDGroups] OFF;
GO

PRINT 'Migrating [Nursing].[InitialNursingAssessmentDet]...';
SET IDENTITY_INSERT [Nursing].[InitialNursingAssessmentDet] ON;
INSERT INTO [Nursing].[InitialNursingAssessmentDet] ([Id], [InitialNursingAssessmentID], [X], [Y], [Desc], [TenantId])
SELECT [Id], [InitialNursingAssessmentID], [X], [Y], [Desc], @TenantId
FROM [SunCity_Clinics].[Nursing].[InitialNursingAssessmentDet];
SET IDENTITY_INSERT [Nursing].[InitialNursingAssessmentDet] OFF;
GO

PRINT 'Migrating [Nursing].[InitialNursingAssessment]...';
SET IDENTITY_INSERT [Nursing].[InitialNursingAssessment] ON;
INSERT INTO [Nursing].[InitialNursingAssessment] ([Id], [ArrivalTypeId], [ArrivalType], [Bloodsugar], [Length], [weight], [Collectibles], [WithPatiant], [takeBy], [Securedby], [Explainedby], [ExplainedOther], [Phone], [Toilet], [Remote], [CallingBell], [Braceletdefinition], [NursingTools], [BedSides], [Bedheight], [WetGround], [NonSmoking], [InfoBy], [InfoOther], [Allergy], [AllergyType], [AllergyOther], [Smoked], [Alcoholdrinker], [Drugs], [Neglecting], [ChronicDiseases], [BloadTransfer], [BloadTransferV], [pain], [painV], [Reqular], [salt], [Protein], [Fat], [Dry], [inflammation], [Appetite], [nausea], [Lossofteeth], [vomiting], [Weightloss], [pregnant], [old], [Diarrhea], [swallowing], [Small], [Tubefeeding], [Parenteralnutrition], [himself], [fully], [Devices], [nutrition], [Cleanliness], [Enteringthebathroom], [movement], [Distortions], [Musclestiffness], [bedlieutenant], [Musclepain], [walker], [Wheelchair], [Transmissiondevice], [Seatlifter], [DeviceOther], [Amputation], [follower], [Sleep], [Psychological], [Worried], [Depressed], [Uncooperative], [Huffy], [blustery], [resistant], [sleepOther], [Difficultysleeping], [PsychologicalOther], [sleepHelp], [DegreeofriskoffallingTotal], [Possibilityoffalling], [preventfalls], [surroundingenvironment], [surroundingenvironmentother], [Auditory], [Visual], [Kinetics], [Spokenlanguage], [Place], [PlaceOther], [HouseHelp], [Takecareofyourself], [Shower], [Useofthebathroom], [Climbingstairs], [Performinghomejobs], [Visitingdoctors], [Medicalfollowup], [DeviceUse], [DeviceUseOther], [WithDevice], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [AdmitPatientsID], [PatientID], [BodyMassIndex], [FamilyMemberName], [RelationShip], [AbuseAdd], [CompanyID], [Historyoffalling], [SecondaryDiagnosis], [Ambulatoryaid], [IV], [Gait], [Mentalstatus], [TenantId])
SELECT [Id], [ArrivalTypeId], [ArrivalType], [Bloodsugar], [Length], [weight], [Collectibles], [WithPatiant], [takeBy], [Securedby], [Explainedby], [ExplainedOther], [Phone], [Toilet], [Remote], [CallingBell], [Braceletdefinition], [NursingTools], [BedSides], [Bedheight], [WetGround], [NonSmoking], [InfoBy], [InfoOther], [Allergy], [AllergyType], [AllergyOther], [Smoked], [Alcoholdrinker], [Drugs], [Neglecting], [ChronicDiseases], [BloadTransfer], [BloadTransferV], [pain], [painV], [Reqular], [salt], [Protein], [Fat], [Dry], [inflammation], [Appetite], [nausea], [Lossofteeth], [vomiting], [Weightloss], [pregnant], [old], [Diarrhea], [swallowing], [Small], [Tubefeeding], [Parenteralnutrition], [himself], [fully], [Devices], [nutrition], [Cleanliness], [Enteringthebathroom], [movement], [Distortions], [Musclestiffness], [bedlieutenant], [Musclepain], [walker], [Wheelchair], [Transmissiondevice], [Seatlifter], [DeviceOther], [Amputation], [follower], [Sleep], [Psychological], [Worried], [Depressed], [Uncooperative], [Huffy], [blustery], [resistant], [sleepOther], [Difficultysleeping], [PsychologicalOther], [sleepHelp], [DegreeofriskoffallingTotal], [Possibilityoffalling], [preventfalls], [surroundingenvironment], [surroundingenvironmentother], [Auditory], [Visual], [Kinetics], [Spokenlanguage], [Place], [PlaceOther], [HouseHelp], [Takecareofyourself], [Shower], [Useofthebathroom], [Climbingstairs], [Performinghomejobs], [Visitingdoctors], [Medicalfollowup], [DeviceUse], [DeviceUseOther], [WithDevice], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [AdmitPatientsID], [PatientID], [BodyMassIndex], [FamilyMemberName], [RelationShip], [AbuseAdd], [CompanyID], [Historyoffalling], [SecondaryDiagnosis], [Ambulatoryaid], [IV], [Gait], [Mentalstatus], @TenantId
FROM [SunCity_Clinics].[Nursing].[InitialNursingAssessment];
SET IDENTITY_INSERT [Nursing].[InitialNursingAssessment] OFF;
GO

PRINT 'Migrating [Nursing].[InitiateDischarge]...';
SET IDENTITY_INSERT [Nursing].[InitiateDischarge] ON;
INSERT INTO [Nursing].[InitiateDischarge] ([Id], [PatientID], [IPNO], [WardID], [RoomID], [BedID], [ExpectedDateOfDischarge], [ExpectedTimeOfDischarge], [DoctorID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [IPNO], [WardID], [RoomID], [BedID], [ExpectedDateOfDischarge], [ExpectedTimeOfDischarge], [DoctorID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[InitiateDischarge];
SET IDENTITY_INSERT [Nursing].[InitiateDischarge] OFF;
GO

PRINT 'Migrating [Nursing].[InstructionsMaster]...';
SET IDENTITY_INSERT [Nursing].[InstructionsMaster] ON;
INSERT INTO [Nursing].[InstructionsMaster] ([ID], [NameArabic], [NameEnglish], [Active], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [NameArabic], [NameEnglish], [Active], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[InstructionsMaster];
SET IDENTITY_INSERT [Nursing].[InstructionsMaster] OFF;
GO

PRINT 'Migrating [Nursing].[InternalTransferHandover]...';
SET IDENTITY_INSERT [Nursing].[InternalTransferHandover] ON;
INSERT INTO [Nursing].[InternalTransferHandover] ([Id], [ConsultationRequestId], [IsRelativesNotified], [TransferMethod], [PatientFunctionalAbility], [PatientAtRiskFor], [IsIsolation], [IsolationSpecify], [Skin], [EquipmentsNeeded], [EquipmentsNeededSpecify], [AirwayMaintaining], [AirwayMaintainingSpecify], [NurseIDHandover], [NurseIDApprove], [CompanyID], [CreatedDate], [CreatedBy], [LastModifiedDate], [LastModifiedBy], [EndorsementofMedication], [EndorsementofMedicationSpecify], [PatientAtRiskForSpecify], [TenantId])
SELECT [Id], [ConsultationRequestId], [IsRelativesNotified], [TransferMethod], [PatientFunctionalAbility], [PatientAtRiskFor], [IsIsolation], [IsolationSpecify], [Skin], [EquipmentsNeeded], [EquipmentsNeededSpecify], [AirwayMaintaining], [AirwayMaintainingSpecify], [NurseIDHandover], [NurseIDApprove], [CompanyID], [CreatedDate], [CreatedBy], [LastModifiedDate], [LastModifiedBy], [EndorsementofMedication], [EndorsementofMedicationSpecify], [PatientAtRiskForSpecify], @TenantId
FROM [SunCity_Clinics].[Nursing].[InternalTransferHandover];
SET IDENTITY_INSERT [Nursing].[InternalTransferHandover] OFF;
GO

PRINT 'Migrating [Nursing].[InterventionsBasedonAssessedRisk]...';
SET IDENTITY_INSERT [Nursing].[InterventionsBasedonAssessedRisk] ON;
INSERT INTO [Nursing].[InterventionsBasedonAssessedRisk] ([ID], [PatientID], [HigtRisk], [InitialNursingAssessment], [Orientationtosurroundings], [MedicationInformation], [Callforassistance], [Userubber], [Securecallbell], [Ensureclothing], [Ensurepatients], [Placefootwear], [Monitorenvironment], [Keepbathroom], [Useraised], [Maintainbed], [Usechairs], [Usesafety], [Monitorforchange], [identifyasfalls], [Assistand], [Monitorfororthostatichypotension], [Ensureadequatehydration], [Monitorbloodsugarsasassessed], [Movepatientclosertonursingstation], [Addedroundtheclock], [Hourlysafetychecks], [Regularpainassessment], [Usenutritionalsupplements], [Raiseupperside], [Callforassistancewithambulation], [Donotlowersiderails], [Notifynurse], [Assesspatientaftervisitors], [Asappropriateconsider], [Placemattressonfloor], [Healthcareproviders], [Consultphysical], [Initiateconstant], [Keepsiderails], [Instructparents], [Adjustandsecure], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [HigtRisk], [InitialNursingAssessment], [Orientationtosurroundings], [MedicationInformation], [Callforassistance], [Userubber], [Securecallbell], [Ensureclothing], [Ensurepatients], [Placefootwear], [Monitorenvironment], [Keepbathroom], [Useraised], [Maintainbed], [Usechairs], [Usesafety], [Monitorforchange], [identifyasfalls], [Assistand], [Monitorfororthostatichypotension], [Ensureadequatehydration], [Monitorbloodsugarsasassessed], [Movepatientclosertonursingstation], [Addedroundtheclock], [Hourlysafetychecks], [Regularpainassessment], [Usenutritionalsupplements], [Raiseupperside], [Callforassistancewithambulation], [Donotlowersiderails], [Notifynurse], [Assesspatientaftervisitors], [Asappropriateconsider], [Placemattressonfloor], [Healthcareproviders], [Consultphysical], [Initiateconstant], [Keepsiderails], [Instructparents], [Adjustandsecure], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[InterventionsBasedonAssessedRisk];
SET IDENTITY_INSERT [Nursing].[InterventionsBasedonAssessedRisk] OFF;
GO

PRINT 'Migrating [Nursing].[NurseActionNotes]...';
SET IDENTITY_INSERT [Nursing].[NurseActionNotes] ON;
INSERT INTO [Nursing].[NurseActionNotes] ([ID], [DoctorInstructionID], [ActionID], [NusreInstruction], [CreatedBy], [CreationDate], [ModifiedBy], [Modificationdate], [CompanyID], [TenantId])
SELECT [ID], [DoctorInstructionID], [ActionID], [NusreInstruction], [CreatedBy], [CreationDate], [ModifiedBy], [Modificationdate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[NurseActionNotes];
SET IDENTITY_INSERT [Nursing].[NurseActionNotes] OFF;
GO

PRINT 'Migrating [Nursing].[NurseAdmissionAssessment]...';
SET IDENTITY_INSERT [Nursing].[NurseAdmissionAssessment] ON;
INSERT INTO [Nursing].[NurseAdmissionAssessment] ([ID], [PatientID], [DiagnosisOnAdmissionID], [SourceOfInformationID], [AllergyID], [AdmissionConditionID], [ValuablesID], [AllergyTypeID], [DoctorID], [Instructions], [Remarks], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [DiagnosisOnAdmissionID], [SourceOfInformationID], [AllergyID], [AdmissionConditionID], [ValuablesID], [AllergyTypeID], [DoctorID], [Instructions], [Remarks], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[NurseAdmissionAssessment];
SET IDENTITY_INSERT [Nursing].[NurseAdmissionAssessment] OFF;
GO

PRINT 'Migrating [Nursing].[NurseCarePlan]...';
SET IDENTITY_INSERT [Nursing].[NurseCarePlan] ON;
INSERT INTO [Nursing].[NurseCarePlan] ([ID], [PatientMRID], [NurseDiagnosis_Ar], [NurseDiagnosis_En], [RT_Ar], [RT_En], [AEB_Ar], [AEB_En], [Exchanging], [Valuing], [Preciving], [Communicating], [Konwing], [Choosing], [Moving], [Feeling], [Repating], [NonOfThose], [NurseAssessment_Ar], [NurseAssessment_En], [ExpectedOutcomes], [Interventions], [Rationale], [Evaluation], [Date], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [PatientMRID], [NurseDiagnosis_Ar], [NurseDiagnosis_En], [RT_Ar], [RT_En], [AEB_Ar], [AEB_En], [Exchanging], [Valuing], [Preciving], [Communicating], [Konwing], [Choosing], [Moving], [Feeling], [Repating], [NonOfThose], [NurseAssessment_Ar], [NurseAssessment_En], [ExpectedOutcomes], [Interventions], [Rationale], [Evaluation], [Date], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[NurseCarePlan];
SET IDENTITY_INSERT [Nursing].[NurseCarePlan] OFF;
GO

PRINT 'Migrating [Nursing].[NurseCategory]...';
SET IDENTITY_INSERT [Nursing].[NurseCategory] ON;
INSERT INTO [Nursing].[NurseCategory] ([ID], [NameArabic], [NameEnglish], [Active], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [NameArabic], [NameEnglish], [Active], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[NurseCategory];
SET IDENTITY_INSERT [Nursing].[NurseCategory] OFF;
GO

PRINT 'Migrating [Nursing].[NurseDischargeAssessment]...';
SET IDENTITY_INSERT [Nursing].[NurseDischargeAssessment] ON;
INSERT INTO [Nursing].[NurseDischargeAssessment] ([ID], [RiskFallLevelID], [AssessRiskFallingID], [FeelSafeAtHome], [WhyNoFeelSafeAtHome], [HaveYouBeenHurtPhysicaly], [WhyNoHaveYouBeenHurtPhysicaly], [DrugsAddict], [AlcoholAddict], [Smoker], [Nothing], [SorePlace], [DegreeSizePlace], [ColorPlace], [SoreSecretion], [CrimpedSkin], [InjuredWoundSkin], [HealthySkin], [SkinCohesinSkin], [RedSpotsSkin], [BedSoresSkin], [CompanyID], [PatientID], [TenantId])
SELECT [ID], [RiskFallLevelID], [AssessRiskFallingID], [FeelSafeAtHome], [WhyNoFeelSafeAtHome], [HaveYouBeenHurtPhysicaly], [WhyNoHaveYouBeenHurtPhysicaly], [DrugsAddict], [AlcoholAddict], [Smoker], [Nothing], [SorePlace], [DegreeSizePlace], [ColorPlace], [SoreSecretion], [CrimpedSkin], [InjuredWoundSkin], [HealthySkin], [SkinCohesinSkin], [RedSpotsSkin], [BedSoresSkin], [CompanyID], [PatientID], @TenantId
FROM [SunCity_Clinics].[Nursing].[NurseDischargeAssessment];
SET IDENTITY_INSERT [Nursing].[NurseDischargeAssessment] OFF;
GO

PRINT 'Migrating [Nursing].[NurseDischargeTransfer]...';
SET IDENTITY_INSERT [Nursing].[NurseDischargeTransfer] ON;
INSERT INTO [Nursing].[NurseDischargeTransfer] ([Id], [Date], [DischargeOtherHospital], [TransferringUnit], [TransportationMethod], [HospitalFile], [PatientDischargeSummary], [ReportsAttached], [AlertConsciousCoherent], [NormalRespiratoryFunction], [ArtificialAirwayDetails], [IncapacitatingPainStatus], [PainControlMedications], [MedicationAdministrationKnowledge], [DietTolerance], [UnassistedEatingDrinking], [NGTubeInsertionStatus], [ParentAssistantFeedingSkills], [HygieneNeedsIndependentlyMeet], [NeedsAssistance], [NeedsAssistanceAmbulating], [TransferFromSittingOrStanding], [MobilityAidsUsed], [SkinAssessmentResults], [BreakInSkinIntegrity], [WoundCareAbility], [SignsOfInfectionFree], [AdequateBowelMovements], [SafeEquipmentUse], [SafetyEquipmentNeeded], [PatientValuablesReturned], [Comments], [RNSignatureAndID], [TransferUnitNurseAndID], [NurseSignatureAcceptingUnitAndID], [Time], [PatientId], [CompanyId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [Id], [Date], [DischargeOtherHospital], [TransferringUnit], [TransportationMethod], [HospitalFile], [PatientDischargeSummary], [ReportsAttached], [AlertConsciousCoherent], [NormalRespiratoryFunction], [ArtificialAirwayDetails], [IncapacitatingPainStatus], [PainControlMedications], [MedicationAdministrationKnowledge], [DietTolerance], [UnassistedEatingDrinking], [NGTubeInsertionStatus], [ParentAssistantFeedingSkills], [HygieneNeedsIndependentlyMeet], [NeedsAssistance], [NeedsAssistanceAmbulating], [TransferFromSittingOrStanding], [MobilityAidsUsed], [SkinAssessmentResults], [BreakInSkinIntegrity], [WoundCareAbility], [SignsOfInfectionFree], [AdequateBowelMovements], [SafeEquipmentUse], [SafetyEquipmentNeeded], [PatientValuablesReturned], [Comments], [RNSignatureAndID], [TransferUnitNurseAndID], [NurseSignatureAcceptingUnitAndID], [Time], [PatientId], [CompanyId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[Nursing].[NurseDischargeTransfer];
SET IDENTITY_INSERT [Nursing].[NurseDischargeTransfer] OFF;
GO

PRINT 'Migrating [Nursing].[NurseInstruction]...';
SET IDENTITY_INSERT [Nursing].[NurseInstruction] ON;
INSERT INTO [Nursing].[NurseInstruction] ([ID], [Code], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[NurseInstruction];
SET IDENTITY_INSERT [Nursing].[NurseInstruction] OFF;
GO

PRINT 'Migrating [Nursing].[NurseMaster]...';
SET IDENTITY_INSERT [Nursing].[NurseMaster] ON;
INSERT INTO [Nursing].[NurseMaster] ([Id], [Type], [Code], [Name], [NameEn], [Qualifications], [DesignationId], [Specialize], [GardeId], [Adress1], [Adress2], [Adress3], [EmployeeId], [Email], [Phone], [Remark], [Mobile], [Analysis], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Sign], [DepartmentId], [CompanyID], [IsHeadNurse], [HeadNurseID], [NurseLocationsId], [TenantId])
SELECT [Id], [Type], [Code], [Name], [NameEn], [Qualifications], [DesignationId], [Specialize], [GardeId], [Adress1], [Adress2], [Adress3], [EmployeeId], [Email], [Phone], [Remark], [Mobile], [Analysis], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Sign], [DepartmentId], [CompanyID], [IsHeadNurse], [HeadNurseID], [NurseLocationsId], @TenantId
FROM [SunCity_Clinics].[Nursing].[NurseMaster];
SET IDENTITY_INSERT [Nursing].[NurseMaster] OFF;
GO

PRINT 'Migrating [Nursing].[NurseQulification]...';
SET IDENTITY_INSERT [Nursing].[NurseQulification] ON;
INSERT INTO [Nursing].[NurseQulification] ([ID], [Code], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[NurseQulification];
SET IDENTITY_INSERT [Nursing].[NurseQulification] OFF;
GO

PRINT 'Migrating [Nursing].[NurseSchedule]...';
SET IDENTITY_INSERT [Nursing].[NurseSchedule] ON;
INSERT INTO [Nursing].[NurseSchedule] ([ID], [NurseId], [Date], [WardId], [SessionId], [OnCallDuty], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ClinicID], [PatientType], [Dayint], [TenantId])
SELECT [ID], [NurseId], [Date], [WardId], [SessionId], [OnCallDuty], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ClinicID], [PatientType], [Dayint], @TenantId
FROM [SunCity_Clinics].[Nursing].[NurseSchedule];
SET IDENTITY_INSERT [Nursing].[NurseSchedule] OFF;
GO

PRINT 'Migrating [Nursing].[NurseSpecialization]...';
SET IDENTITY_INSERT [Nursing].[NurseSpecialization] ON;
INSERT INTO [Nursing].[NurseSpecialization] ([ID], [Code], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[NurseSpecialization];
SET IDENTITY_INSERT [Nursing].[NurseSpecialization] OFF;
GO

PRINT 'Migrating [Nursing].[NursingInitialAssessment]...';
SET IDENTITY_INSERT [Nursing].[NursingInitialAssessment] ON;
INSERT INTO [Nursing].[NursingInitialAssessment] ([ID], [PatientID], [Date], [MovementID], [ConsciousnessID], [BraceletID], [CareOfCannulasID], [AwarenessID], [RiskID], [SkinID], [NutritionID], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [Date], [MovementID], [ConsciousnessID], [BraceletID], [CareOfCannulasID], [AwarenessID], [RiskID], [SkinID], [NutritionID], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[NursingInitialAssessment];
SET IDENTITY_INSERT [Nursing].[NursingInitialAssessment] OFF;
GO

PRINT 'Migrating [Nursing].[NursingNotes]...';
SET IDENTITY_INSERT [Nursing].[NursingNotes] ON;
INSERT INTO [Nursing].[NursingNotes] ([Id], [PatientID], [EntryDate], [EntryTime], [EnteredBy], [NursingNote], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [OP_OP], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [EntryDate], [EntryTime], [EnteredBy], [NursingNote], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [OP_OP], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[NursingNotes];
SET IDENTITY_INSERT [Nursing].[NursingNotes] OFF;
GO

PRINT 'Migrating [Nursing].[NursingParameters]...';
SET IDENTITY_INSERT [Nursing].[NursingParameters] ON;
INSERT INTO [Nursing].[NursingParameters] ([Id], [Code], [Name], [Type], [Description], [FindingId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Code], [Name], [Type], [Description], [FindingId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[NursingParameters];
SET IDENTITY_INSERT [Nursing].[NursingParameters] OFF;
GO

PRINT 'Migrating [Nursing].[PatientEquipment]...';
SET IDENTITY_INSERT [Nursing].[PatientEquipment] ON;
INSERT INTO [Nursing].[PatientEquipment] ([ID], [NameArabic], [NameEnglish], [Active], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [NameArabic], [NameEnglish], [Active], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[PatientEquipment];
SET IDENTITY_INSERT [Nursing].[PatientEquipment] OFF;
GO

PRINT 'Migrating [Nursing].[PatientPackages]...';
SET IDENTITY_INSERT [Nursing].[PatientPackages] ON;
INSERT INTO [Nursing].[PatientPackages] ([Id], [InvestigationRequestDetailsID], [ServiceId_Pack], [ServiceId], [ServiceStatus], [Closed], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [Id], [InvestigationRequestDetailsID], [ServiceId_Pack], [ServiceId], [ServiceStatus], [Closed], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[Nursing].[PatientPackages];
SET IDENTITY_INSERT [Nursing].[PatientPackages] OFF;
GO

PRINT 'Migrating [Nursing].[PatientRestraint]...';
SET IDENTITY_INSERT [Nursing].[PatientRestraint] ON;
INSERT INTO [Nursing].[PatientRestraint] ([Id], [PatientId], [CheckCouse], [IsOtherCouse], [OtherCouse], [AdditionalInformation], [CheckType], [DrugName], [Strength], [Dose], [Frequency], [Route], [Date], [Time], [CheckBoxPhy], [IsOtherPhysical], [OtherPhysical], [TargetDuration], [DrugNamePhysician], [StrengthPhysician], [DosePhysician], [FrequencyPhysician], [RoutePhysician], [DatePhysician], [TimePhysician], [NurseID], [NurseName], [GivenBy], [RNID], [RnSignature], [DrugNameMedicationDetail], [StrengthMedicationDetail], [DoseMedicationDetail], [RouteOfAdministration], [FrequencyOfAdministration], [DateOfAdministration], [TimeOfAdministration], [MedicationsGiven], [TenantId])
SELECT [Id], [PatientId], [CheckCouse], [IsOtherCouse], [OtherCouse], [AdditionalInformation], [CheckType], [DrugName], [Strength], [Dose], [Frequency], [Route], [Date], [Time], [CheckBoxPhy], [IsOtherPhysical], [OtherPhysical], [TargetDuration], [DrugNamePhysician], [StrengthPhysician], [DosePhysician], [FrequencyPhysician], [RoutePhysician], [DatePhysician], [TimePhysician], [NurseID], [NurseName], [GivenBy], [RNID], [RnSignature], [DrugNameMedicationDetail], [StrengthMedicationDetail], [DoseMedicationDetail], [RouteOfAdministration], [FrequencyOfAdministration], [DateOfAdministration], [TimeOfAdministration], [MedicationsGiven], @TenantId
FROM [SunCity_Clinics].[Nursing].[PatientRestraint];
SET IDENTITY_INSERT [Nursing].[PatientRestraint] OFF;
GO

PRINT 'Migrating [Nursing].[PatientValuableClothing]...';
SET IDENTITY_INSERT [Nursing].[PatientValuableClothing] ON;
INSERT INTO [Nursing].[PatientValuableClothing] ([ID], [PatientValuableId], [ClothingID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [PatientValuableId], [ClothingID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[PatientValuableClothing];
SET IDENTITY_INSERT [Nursing].[PatientValuableClothing] OFF;
GO

PRINT 'Migrating [Nursing].[PatientValuablePersonalItem]...';
SET IDENTITY_INSERT [Nursing].[PatientValuablePersonalItem] ON;
INSERT INTO [Nursing].[PatientValuablePersonalItem] ([ID], [PatientValuableId], [PersonalItemID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [PatientValuableId], [PersonalItemID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[PatientValuablePersonalItem];
SET IDENTITY_INSERT [Nursing].[PatientValuablePersonalItem] OFF;
GO

PRINT 'Migrating [Nursing].[PatientValuableProsthetic]...';
SET IDENTITY_INSERT [Nursing].[PatientValuableProsthetic] ON;
INSERT INTO [Nursing].[PatientValuableProsthetic] ([ID], [PatientValuableId], [ProstheticID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [PatientValuableId], [ProstheticID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[PatientValuableProsthetic];
SET IDENTITY_INSERT [Nursing].[PatientValuableProsthetic] OFF;
GO

PRINT 'Migrating [Nursing].[PatientValuable]...';
SET IDENTITY_INSERT [Nursing].[PatientValuable] ON;
INSERT INTO [Nursing].[PatientValuable] ([ID], [NurseShiftName], [NurseShiftNameEn], [PatientID], [NurseID], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Comments], [ClothingPlacedInStorage], [CheckedBy], [ResidentORResponsibleParty], [TenantId])
SELECT [ID], [NurseShiftName], [NurseShiftNameEn], [PatientID], [NurseID], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Comments], [ClothingPlacedInStorage], [CheckedBy], [ResidentORResponsibleParty], @TenantId
FROM [SunCity_Clinics].[Nursing].[PatientValuable];
SET IDENTITY_INSERT [Nursing].[PatientValuable] OFF;
GO

PRINT 'Migrating [Nursing].[Patient_HandOver]...';
SET IDENTITY_INSERT [Nursing].[Patient_HandOver] ON;
INSERT INTO [Nursing].[Patient_HandOver] ([ID], [FromNurseID], [ToNurseID], [HandOverDate], [PatientID], [Obs_Device], [Obs_Comment], [Ass_FallRisk], [Ass_FallPosibility], [Ass_RedSideRails], [Ass_BedHeight], [Ass_WetFloor], [Ass_DietType], [Ass_Salt], [Ass_Protein], [Ass_Fat], [Ass_FallPrecaution], [TenantId])
SELECT [ID], [FromNurseID], [ToNurseID], [HandOverDate], [PatientID], [Obs_Device], [Obs_Comment], [Ass_FallRisk], [Ass_FallPosibility], [Ass_RedSideRails], [Ass_BedHeight], [Ass_WetFloor], [Ass_DietType], [Ass_Salt], [Ass_Protein], [Ass_Fat], [Ass_FallPrecaution], @TenantId
FROM [SunCity_Clinics].[Nursing].[Patient_HandOver];
SET IDENTITY_INSERT [Nursing].[Patient_HandOver] OFF;
GO

PRINT 'Migrating [Nursing].[PediatricAssessment]...';
SET IDENTITY_INSERT [Nursing].[PediatricAssessment] ON;
INSERT INTO [Nursing].[PediatricAssessment] ([Id], [TxtDiagnosis], [AdmissionSource], [OtherSource], [TransportedWith], [OtherTransport], [AdmissionMode], [OtherMode], [PhysicianNotifiedList], [NotifiedTime], [Reason], [Medication], [MedicationLocation], [ImmunizationHistory], [FamilyHistory], [PastMedicalHistorySource], [PastMedicalHistory], [PastSurgicalHistory], [LastHospitalAdmission], [BreathingCirculation], [ApicalHeartRate], [IsHeartRateRegular], [IsRbIrregular], [LimbsCondition], [CommentsBreathingCirculation], [TemperatureConditions], [NoProblemsBodyTemperature], [CommentsTemperature], [CommunicationNeurologicalSymptoms], [NoProblemsCommunicationNeurological], [CommentsCommunicationNeurological], [ReferralForNutritionalScreening], [NutritionalScreeningDate], [ListNutritionalScreening], [DieticianReferralReasons], [CommentsDieticianReferral], [EliminationDetails], [IsEliminationComment], [CommentsElimination], [PersonalCleanlinessSymptoms], [ListEye], [ListMouth], [ListSkin], [NoProblemsPersonalCleanliness], [HasVenousAccessDevice], [VenousAccessType], [VenousAccessLocation], [VenousAccessInsertionDate], [CommentsVenousAccess], [MobilitySymptoms], [NoProblemsMobility], [CommentsMobility], [SleepPatternSymptoms], [Bedtime], [SleepHours], [TakesNaps], [ComfortItem], [NoProblemsSleep], [SafeEnvironmentConcerns], [CommentsSafeEnvironment], [SafeEnvironmentComment], [FunctionalScreeningDate], [FunctionalScreening], [NoProblemsFunctionalScreening], [CommentsFunctionalScreening], [PhysicianNotifiedComment], [PhysicianNotified], [LivesWith], [HasSiblingsInHousehold], [SocialHistoryHowMany], [NumberOfSiblings], [SocialHistoryPhoneNo], [IqamaNumber], [OrientationDetails], [OtherOrientationDetails], [InformationSource], [PatientId], [AllergyId], [NoProblemsBreathingCirculation], [TenantId])
SELECT [Id], [TxtDiagnosis], [AdmissionSource], [OtherSource], [TransportedWith], [OtherTransport], [AdmissionMode], [OtherMode], [PhysicianNotifiedList], [NotifiedTime], [Reason], [Medication], [MedicationLocation], [ImmunizationHistory], [FamilyHistory], [PastMedicalHistorySource], [PastMedicalHistory], [PastSurgicalHistory], [LastHospitalAdmission], [BreathingCirculation], [ApicalHeartRate], [IsHeartRateRegular], [IsRbIrregular], [LimbsCondition], [CommentsBreathingCirculation], [TemperatureConditions], [NoProblemsBodyTemperature], [CommentsTemperature], [CommunicationNeurologicalSymptoms], [NoProblemsCommunicationNeurological], [CommentsCommunicationNeurological], [ReferralForNutritionalScreening], [NutritionalScreeningDate], [ListNutritionalScreening], [DieticianReferralReasons], [CommentsDieticianReferral], [EliminationDetails], [IsEliminationComment], [CommentsElimination], [PersonalCleanlinessSymptoms], [ListEye], [ListMouth], [ListSkin], [NoProblemsPersonalCleanliness], [HasVenousAccessDevice], [VenousAccessType], [VenousAccessLocation], [VenousAccessInsertionDate], [CommentsVenousAccess], [MobilitySymptoms], [NoProblemsMobility], [CommentsMobility], [SleepPatternSymptoms], [Bedtime], [SleepHours], [TakesNaps], [ComfortItem], [NoProblemsSleep], [SafeEnvironmentConcerns], [CommentsSafeEnvironment], [SafeEnvironmentComment], [FunctionalScreeningDate], [FunctionalScreening], [NoProblemsFunctionalScreening], [CommentsFunctionalScreening], [PhysicianNotifiedComment], [PhysicianNotified], [LivesWith], [HasSiblingsInHousehold], [SocialHistoryHowMany], [NumberOfSiblings], [SocialHistoryPhoneNo], [IqamaNumber], [OrientationDetails], [OtherOrientationDetails], [InformationSource], [PatientId], [AllergyId], [NoProblemsBreathingCirculation], @TenantId
FROM [SunCity_Clinics].[Nursing].[PediatricAssessment];
SET IDENTITY_INSERT [Nursing].[PediatricAssessment] OFF;
GO

PRINT 'Migrating [Nursing].[PersonalItem]...';
SET IDENTITY_INSERT [Nursing].[PersonalItem] ON;
INSERT INTO [Nursing].[PersonalItem] ([ID], [Code], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[PersonalItem];
SET IDENTITY_INSERT [Nursing].[PersonalItem] OFF;
GO

PRINT 'Migrating [Nursing].[Prosthetic]...';
SET IDENTITY_INSERT [Nursing].[Prosthetic] ON;
INSERT INTO [Nursing].[Prosthetic] ([ID], [Code], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[Prosthetic];
SET IDENTITY_INSERT [Nursing].[Prosthetic] OFF;
GO

PRINT 'Migrating [Nursing].[ReceivedMealsDetails]...';
SET IDENTITY_INSERT [Nursing].[ReceivedMealsDetails] ON;
INSERT INTO [Nursing].[ReceivedMealsDetails] ([Id], [MasterId], [DietId], [DietCount], [MealId], [TenantId])
SELECT [Id], [MasterId], [DietId], [DietCount], [MealId], @TenantId
FROM [SunCity_Clinics].[Nursing].[ReceivedMealsDetails];
SET IDENTITY_INSERT [Nursing].[ReceivedMealsDetails] OFF;
GO

PRINT 'Migrating [Nursing].[ReceivedMealsMaster]...';
SET IDENTITY_INSERT [Nursing].[ReceivedMealsMaster] ON;
INSERT INTO [Nursing].[ReceivedMealsMaster] ([Id], [NurseId], [ReceiptDay], [ReceiptDate1], [ReceiptDate2], [ReceiptDate3], [MealType], [CompanyID], [TenantId])
SELECT [Id], [NurseId], [ReceiptDay], [ReceiptDate1], [ReceiptDate2], [ReceiptDate3], [MealType], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[ReceivedMealsMaster];
SET IDENTITY_INSERT [Nursing].[ReceivedMealsMaster] OFF;
GO

PRINT 'Migrating [Nursing].[Room]...';
SET IDENTITY_INSERT [Nursing].[Room] ON;
INSERT INTO [Nursing].[Room] ([Id], [WardId], [Number], [Type], [Specialty], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [WardId], [Number], [Type], [Specialty], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[Room];
SET IDENTITY_INSERT [Nursing].[Room] OFF;
GO

PRINT 'Migrating [Nursing].[TPNOrderNurseStation]...';
SET IDENTITY_INSERT [Nursing].[TPNOrderNurseStation] ON;
INSERT INTO [Nursing].[TPNOrderNurseStation] ([Id], [DrugId], [UnitConversionFactorId], [Quentity], [TPNOrderId], [CreatedBy], [CreationDate], [TenantId])
SELECT [Id], [DrugId], [UnitConversionFactorId], [Quentity], [TPNOrderId], [CreatedBy], [CreationDate], @TenantId
FROM [SunCity_Clinics].[Nursing].[TPNOrderNurseStation];
SET IDENTITY_INSERT [Nursing].[TPNOrderNurseStation] OFF;
GO

PRINT 'Migrating [Nursing].[TreatmentCycle]...';
SET IDENTITY_INSERT [Nursing].[TreatmentCycle] ON;
INSERT INTO [Nursing].[TreatmentCycle] ([TreatmentCycleId], [NumOfFollowUpVisit], [Period], [PeriodType], [SpecialityGroupDetailsId], [CreatedBy], [CreationDate], [LastModifiedBy], [LastModificationDate], [TenantId])
SELECT [TreatmentCycleId], [NumOfFollowUpVisit], [Period], [PeriodType], [SpecialityGroupDetailsId], [CreatedBy], [CreationDate], [LastModifiedBy], [LastModificationDate], @TenantId
FROM [SunCity_Clinics].[Nursing].[TreatmentCycle];
SET IDENTITY_INSERT [Nursing].[TreatmentCycle] OFF;
GO

PRINT 'Migrating [Nursing].[ValuablesMaster]...';
SET IDENTITY_INSERT [Nursing].[ValuablesMaster] ON;
INSERT INTO [Nursing].[ValuablesMaster] ([ID], [NameArabic], [NameEnglish], [Status], [Condition], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [ValuableTypes], [CompanyID], [TenantId])
SELECT [ID], [NameArabic], [NameEnglish], [Status], [Condition], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [ValuableTypes], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[ValuablesMaster];
SET IDENTITY_INSERT [Nursing].[ValuablesMaster] OFF;
GO

PRINT 'Migrating [Nursing].[Ward]...';
SET IDENTITY_INSERT [Nursing].[Ward] ON;
INSERT INTO [Nursing].[Ward] ([Id], [WardName], [Type], [Specialty], [Category], [Description], [InActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [WardName], [Type], [Specialty], [Category], [Description], [InActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Nursing].[Ward];
SET IDENTITY_INSERT [Nursing].[Ward] OFF;
GO

PRINT 'Migrating [Nursing].[patientacuity]...';
SET IDENTITY_INSERT [Nursing].[patientacuity] ON;
INSERT INTO [Nursing].[patientacuity] ([ID], [PatientID], [Date], [ChkAAssessmentQ8h], [ChkAAssessmentAlert], [ChkBAssessment4h], [ChkBAssessmentCIWA], [CHKCAssessmentq2h], [CHkcAssessmentDelirium], [ChkCAssessmentCIWA], [ChkDAssessmentdetermined], [CreatedBy], [CreatedDate], [LastModifiedBy], [CompanyID], [ChkARespiratoryStableonroomair], [ChkBRespiratoryOxygen], [ChkCRespiratoryOxygen], [ChkCRespiratoryTracheostomy], [ChkDRespiratoryOxygen], [ChkDmaintain], [ChkACardiacVS], [ChkBCardiacLowGrade], [ChkBCardiacPacemaker], [ChkBCardiacHR], [ChkCChange], [ChkDUnstable], [ChkDAtrial], [ChkAMedicationPO], [chkAMedicationBloodglucose], [ChkBMedicationTPN], [chkBMedicationBloodglucose], [chkBMedicationBlooddraws], [chkBMedicationDialysis], [chkCMedicationCBI], [chkCMedicationunitblood], [chkCMedicationFluidbouls], [chkDMedicationbloodtransfusion], [chkDChemotherapy], [ChkADrainagedevicesdrains], [ChkBDrainagedevicesChest], [ChkBDrainagedevicesNasogastric], [ChkBDrainagedevicesConttinuous], [ChkCDrainagedevicesChest], [ChkCDrainagedevicesDrain], [ChkCDrainagedevicesBouls], [ChkDDrainagedevicesDrain], [ChkDDrainagedevicesChest], [ChkAPainManagementPain], [ChkBPainManagementPatient], [ChkBPainManagementNausea], [ChkCPainManagementPainmanagement], [ChkDPainManagementUncontrolled], [ChkAAdmitStable], [ChkAAdmitRoutine], [ChkBAdmitDischarge], [ChkCAdmitNew], [ChkCAdmitComplex], [ChkCAdmitDischarge], [ChkDAdmitComplicated], [ChkDAdmitTransfer], [ChkAEducationCalm], [ChkBEducationAnxious], [ChkBEducationEducation], [ChkCEducationtrach], [ChkCEducationTransfer], [ChkCEducationRequires], [ChkDEducationEnd], [ChkAWoundQD], [ChkAWoundWound], [ChkAWoundOne], [ChkBWoundostomy], [ChkBWoundEnema], [ChkBWoundBowel], [ChkBWoundIncontinent], [ChkCWoundTID], [ChkCWoundHigh], [ChkCWoundMultiple], [ChkDWoundActive], [ChkDWoundtoilet], [ChkAADlsindependent], [ChkAADlsStandard], [ChkBADlsAssist], [ChkBADlsAssistPerson], [ChkBADlsIsolation], [ChkCADlsTurn], [ChkCADlsBedrest], [ChkCADlsRespiratory], [ChkDADlsParaplegic], [ChkDADlsTotal], [ChkASafetyFalls], [ChkBSafetySitter], [ChkCSafetyBed], [ChkCSafetySensory], [ChkDSafetyHighly], [ChkDSafetyRestraints], [Status], [LastModifiedDate], [TenantId])
SELECT [ID], [PatientID], [Date], [ChkAAssessmentQ8h], [ChkAAssessmentAlert], [ChkBAssessment4h], [ChkBAssessmentCIWA], [CHKCAssessmentq2h], [CHkcAssessmentDelirium], [ChkCAssessmentCIWA], [ChkDAssessmentdetermined], [CreatedBy], [CreatedDate], [LastModifiedBy], [CompanyID], [ChkARespiratoryStableonroomair], [ChkBRespiratoryOxygen], [ChkCRespiratoryOxygen], [ChkCRespiratoryTracheostomy], [ChkDRespiratoryOxygen], [ChkDmaintain], [ChkACardiacVS], [ChkBCardiacLowGrade], [ChkBCardiacPacemaker], [ChkBCardiacHR], [ChkCChange], [ChkDUnstable], [ChkDAtrial], [ChkAMedicationPO], [chkAMedicationBloodglucose], [ChkBMedicationTPN], [chkBMedicationBloodglucose], [chkBMedicationBlooddraws], [chkBMedicationDialysis], [chkCMedicationCBI], [chkCMedicationunitblood], [chkCMedicationFluidbouls], [chkDMedicationbloodtransfusion], [chkDChemotherapy], [ChkADrainagedevicesdrains], [ChkBDrainagedevicesChest], [ChkBDrainagedevicesNasogastric], [ChkBDrainagedevicesConttinuous], [ChkCDrainagedevicesChest], [ChkCDrainagedevicesDrain], [ChkCDrainagedevicesBouls], [ChkDDrainagedevicesDrain], [ChkDDrainagedevicesChest], [ChkAPainManagementPain], [ChkBPainManagementPatient], [ChkBPainManagementNausea], [ChkCPainManagementPainmanagement], [ChkDPainManagementUncontrolled], [ChkAAdmitStable], [ChkAAdmitRoutine], [ChkBAdmitDischarge], [ChkCAdmitNew], [ChkCAdmitComplex], [ChkCAdmitDischarge], [ChkDAdmitComplicated], [ChkDAdmitTransfer], [ChkAEducationCalm], [ChkBEducationAnxious], [ChkBEducationEducation], [ChkCEducationtrach], [ChkCEducationTransfer], [ChkCEducationRequires], [ChkDEducationEnd], [ChkAWoundQD], [ChkAWoundWound], [ChkAWoundOne], [ChkBWoundostomy], [ChkBWoundEnema], [ChkBWoundBowel], [ChkBWoundIncontinent], [ChkCWoundTID], [ChkCWoundHigh], [ChkCWoundMultiple], [ChkDWoundActive], [ChkDWoundtoilet], [ChkAADlsindependent], [ChkAADlsStandard], [ChkBADlsAssist], [ChkBADlsAssistPerson], [ChkBADlsIsolation], [ChkCADlsTurn], [ChkCADlsBedrest], [ChkCADlsRespiratory], [ChkDADlsParaplegic], [ChkDADlsTotal], [ChkASafetyFalls], [ChkBSafetySitter], [ChkCSafetyBed], [ChkCSafetySensory], [ChkDSafetyHighly], [ChkDSafetyRestraints], [Status], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[Nursing].[patientacuity];
SET IDENTITY_INSERT [Nursing].[patientacuity] OFF;
GO

PRINT 'Migrating [Operations].[AnesthesiaMedication]...';
SET IDENTITY_INSERT [Operations].[AnesthesiaMedication] ON;
INSERT INTO [Operations].[AnesthesiaMedication] ([Id], [AnesthesiaRecordId], [TimeTaken], [DrugId], [GenericID], [Dose], [Frequancy], [Route], [Strength], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Signature], [TenantId])
SELECT [Id], [AnesthesiaRecordId], [TimeTaken], [DrugId], [GenericID], [Dose], [Frequancy], [Route], [Strength], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Signature], @TenantId
FROM [SunCity_Clinics].[Operations].[AnesthesiaMedication];
SET IDENTITY_INSERT [Operations].[AnesthesiaMedication] OFF;
GO

PRINT 'Migrating [Operations].[AnesthesiaPatientConsent]...';
SET IDENTITY_INSERT [Operations].[AnesthesiaPatientConsent] ON;
INSERT INTO [Operations].[AnesthesiaPatientConsent] ([Id], [PatientId], [Date], [Time], [TypeOfSurgery], [NameOfSurgeon], [MethodsOfAnesthesia], [ExplanationOfAnesthesia], [DoctorName], [IsConfirm], [ComplicationsOf], [HerebyState], [DecidedToHave], [SignatureOfPatient], [PatientDate], [PatientTime], [SignatureOfRelative], [RelativeDate], [RelativeTime], [SignatureOfWitness], [WitnessDate], [WitnessTime], [SignatureOfInterpreter], [InterpreterDate], [InterpreterTime], [SignatureOfAnesthesiologist], [LicenseNumber], [OfficialHospitalStamp], [AnesthesiologistDate], [AnesthesiologistTime], [Notes], [PreparedName], [PreparedTitle], [PreparedDepartment], [PreparedOrganization], [PatientData], [Obstetrician], [LabourAnalgesiaDate], [LabourAnalgesiaTime], [AnesthesiologistName], [AnesthesiaProvider], [SignatureOfPatientForLabour], [PatientForLabourDate], [PatientForLabourTime], [SignatureOfRelativeForLabour], [RelativeForLabourDate], [RelativeForLabourTime], [SignatureOfInterpreterForLabour], [SignatureOfWitnessForLabour], [WitnessDetailed], [SignatureOfTheWitness], [TheWitnessForLabourDate], [TheWitnessForLabourTime], [SignatureOfAnesthesiologistDeclaration], [DHALicenseNumber], [HospitalStamp], [AnesthesiologistDeclarationDate], [AnesthesiologistDeclarationTime], [NotesAnesthesiologist], [TenantId])
SELECT [Id], [PatientId], [Date], [Time], [TypeOfSurgery], [NameOfSurgeon], [MethodsOfAnesthesia], [ExplanationOfAnesthesia], [DoctorName], [IsConfirm], [ComplicationsOf], [HerebyState], [DecidedToHave], [SignatureOfPatient], [PatientDate], [PatientTime], [SignatureOfRelative], [RelativeDate], [RelativeTime], [SignatureOfWitness], [WitnessDate], [WitnessTime], [SignatureOfInterpreter], [InterpreterDate], [InterpreterTime], [SignatureOfAnesthesiologist], [LicenseNumber], [OfficialHospitalStamp], [AnesthesiologistDate], [AnesthesiologistTime], [Notes], [PreparedName], [PreparedTitle], [PreparedDepartment], [PreparedOrganization], [PatientData], [Obstetrician], [LabourAnalgesiaDate], [LabourAnalgesiaTime], [AnesthesiologistName], [AnesthesiaProvider], [SignatureOfPatientForLabour], [PatientForLabourDate], [PatientForLabourTime], [SignatureOfRelativeForLabour], [RelativeForLabourDate], [RelativeForLabourTime], [SignatureOfInterpreterForLabour], [SignatureOfWitnessForLabour], [WitnessDetailed], [SignatureOfTheWitness], [TheWitnessForLabourDate], [TheWitnessForLabourTime], [SignatureOfAnesthesiologistDeclaration], [DHALicenseNumber], [HospitalStamp], [AnesthesiologistDeclarationDate], [AnesthesiologistDeclarationTime], [NotesAnesthesiologist], @TenantId
FROM [SunCity_Clinics].[Operations].[AnesthesiaPatientConsent];
SET IDENTITY_INSERT [Operations].[AnesthesiaPatientConsent] OFF;
GO

PRINT 'Migrating [Operations].[AnesthesiaRecord]...';
SET IDENTITY_INSERT [Operations].[AnesthesiaRecord] ON;
INSERT INTO [Operations].[AnesthesiaRecord] ([Id], [PatientId], [AnesthesiaDate], [AnesthesiaFrom], [AnesthesiaTo], [SurgeryFrom], [SurgeryTo], [RoomInTime], [RoomOutTime], [CreatedDate], [LastModifiedDate], [PatientReassessed], [Pulse], [Resp], [BPTop], [BPBottom], [TEMPVital], [CVS], [RS], [CNS], [OtherSystems], [ChartReviewed], [ConsentSigned], [NPOSince], [FullStomach], [PainManagement], [Awake], [Anxious], [Uncooperative], [Calm], [Sedated], [ReducedLOC], [AnesthesiaMachineNo], [ArmSecuredLeft], [ArmSecuredRight], [ArmTuckedLeft], [ArmTuckedRight], [ArmsLess90], [PressureChecked], [EyeCare], [TapedClosed], [Ointment], [Prone], [NoPressureOrbits], [NoPressureNose], [NoPressureEars], [NoPressureGenitals], [Esophageal], [Precordial], [Suprasternal], [NonInvasiveBP], [ECG], [ContinuousECG], [ETAgentAnalyzer], [PulseOximeter], [NerveStimulator], [NerveStimulatorUlnar], [NerveStimulatorTibial], [NerveStimulatorFacial], [NerveStimulatorComment], [EndTidalCO2], [OxygenFiO2Monitor], [TempEquipmentDegree], [FluidBloodWarmer], [BodyWarmer], [AirwayHumidifier], [NGOGTube], [FoleyOR], [Ward], [DopplerComment], [ArterialLineComment], [CLineCVPComment], [PALineComment], [IVsComment], [Intravenous], [PreO2], [RSI], [CricoidPressure], [InductionInhalation], [IM], [PR], [MaintenanceInhalation], [InhalationIV], [GARegionalCombination], [TIVA], [SedationAndAnalgesia], [Epidural], [Thoracic], [Lumbar], [Caudal], [SAB], [Ankle], [Femoral], [Axillary], [Interscalene], [CSE], [Bier], [Supraclavicular], [WristBlock], [DNB], [Others], [Position], [SeeRemarks], [Prep], [Local], [Site], [Needle], [IntroducerLocalAnaesthetic], [Narcotic], [Additive], [TestDoseRx], [Attempts], [TechniqueLevel], [Catheter], [TestDoseResponse], [LOR], [Skin], [Secured], [OralETT], [RAE], [ArmoredETT], [NasalETT], [LMANo], [Stylet], [ClassicUnique], [Fastrach], [ProSeal], [DL], [Flexible], [Other], [TubeSize], [FOI], [AirwayAwake], [Blade], [Bougie], [AirwayAttempts], [DLT], [Grade], [NerveBlocks], [Topical], [Nebulizer], [AirwaySeeRemarks], [AtraumaticIntubationLMA], [SecuredAt], [ETCO2Present], [BreathSoundsBilateral], [CuffedMinOccPressure], [UncuffedETTleaksAt], [OralAirway], [NasalAirway], [BiteBlock], [TwoHandedTech], [Easy], [HeadTilt], [MaxJawThrust], [CircleSystem], [NRB], [Bains], [ViaTracheotomyStoma], [NasalCannula], [SimpleO2Mask], [AirwayComments], [OperationReservationId], [CompanyID], [TenantId])
SELECT [Id], [PatientId], [AnesthesiaDate], [AnesthesiaFrom], [AnesthesiaTo], [SurgeryFrom], [SurgeryTo], [RoomInTime], [RoomOutTime], [CreatedDate], [LastModifiedDate], [PatientReassessed], [Pulse], [Resp], [BPTop], [BPBottom], [TEMPVital], [CVS], [RS], [CNS], [OtherSystems], [ChartReviewed], [ConsentSigned], [NPOSince], [FullStomach], [PainManagement], [Awake], [Anxious], [Uncooperative], [Calm], [Sedated], [ReducedLOC], [AnesthesiaMachineNo], [ArmSecuredLeft], [ArmSecuredRight], [ArmTuckedLeft], [ArmTuckedRight], [ArmsLess90], [PressureChecked], [EyeCare], [TapedClosed], [Ointment], [Prone], [NoPressureOrbits], [NoPressureNose], [NoPressureEars], [NoPressureGenitals], [Esophageal], [Precordial], [Suprasternal], [NonInvasiveBP], [ECG], [ContinuousECG], [ETAgentAnalyzer], [PulseOximeter], [NerveStimulator], [NerveStimulatorUlnar], [NerveStimulatorTibial], [NerveStimulatorFacial], [NerveStimulatorComment], [EndTidalCO2], [OxygenFiO2Monitor], [TempEquipmentDegree], [FluidBloodWarmer], [BodyWarmer], [AirwayHumidifier], [NGOGTube], [FoleyOR], [Ward], [DopplerComment], [ArterialLineComment], [CLineCVPComment], [PALineComment], [IVsComment], [Intravenous], [PreO2], [RSI], [CricoidPressure], [InductionInhalation], [IM], [PR], [MaintenanceInhalation], [InhalationIV], [GARegionalCombination], [TIVA], [SedationAndAnalgesia], [Epidural], [Thoracic], [Lumbar], [Caudal], [SAB], [Ankle], [Femoral], [Axillary], [Interscalene], [CSE], [Bier], [Supraclavicular], [WristBlock], [DNB], [Others], [Position], [SeeRemarks], [Prep], [Local], [Site], [Needle], [IntroducerLocalAnaesthetic], [Narcotic], [Additive], [TestDoseRx], [Attempts], [TechniqueLevel], [Catheter], [TestDoseResponse], [LOR], [Skin], [Secured], [OralETT], [RAE], [ArmoredETT], [NasalETT], [LMANo], [Stylet], [ClassicUnique], [Fastrach], [ProSeal], [DL], [Flexible], [Other], [TubeSize], [FOI], [AirwayAwake], [Blade], [Bougie], [AirwayAttempts], [DLT], [Grade], [NerveBlocks], [Topical], [Nebulizer], [AirwaySeeRemarks], [AtraumaticIntubationLMA], [SecuredAt], [ETCO2Present], [BreathSoundsBilateral], [CuffedMinOccPressure], [UncuffedETTleaksAt], [OralAirway], [NasalAirway], [BiteBlock], [TwoHandedTech], [Easy], [HeadTilt], [MaxJawThrust], [CircleSystem], [NRB], [Bains], [ViaTracheotomyStoma], [NasalCannula], [SimpleO2Mask], [AirwayComments], [OperationReservationId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[AnesthesiaRecord];
SET IDENTITY_INSERT [Operations].[AnesthesiaRecord] OFF;
GO

PRINT 'Migrating [Operations].[AnesthesiaType]...';
SET IDENTITY_INSERT [Operations].[AnesthesiaType] ON;
INSERT INTO [Operations].[AnesthesiaType] ([ID], [Name], [NameEn], [Active], [MainAnesthesia], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Name], [NameEn], [Active], [MainAnesthesia], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[AnesthesiaType];
SET IDENTITY_INSERT [Operations].[AnesthesiaType] OFF;
GO

PRINT 'Migrating [Operations].[AnesthesiaVitalSigns]...';
SET IDENTITY_INSERT [Operations].[AnesthesiaVitalSigns] ON;
INSERT INTO [Operations].[AnesthesiaVitalSigns] ([Id], [AnesthesiaRecordId], [TimeTaken], [HeartRate], [RespiratoryRate], [FluidName], [FluidAmount], [Urine], [Gastric], [EBL], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [AnesthesiaRecordId], [TimeTaken], [HeartRate], [RespiratoryRate], [FluidName], [FluidAmount], [Urine], [Gastric], [EBL], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[AnesthesiaVitalSigns];
SET IDENTITY_INSERT [Operations].[AnesthesiaVitalSigns] OFF;
GO

PRINT 'Migrating [Operations].[BodyFluidBalanceChart]...';
SET IDENTITY_INSERT [Operations].[BodyFluidBalanceChart] ON;
INSERT INTO [Operations].[BodyFluidBalanceChart] ([BodyFluidBalanceChartId], [PatientId], [IPNumber], [TotalBalance], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [BodyFluidBalanceChartId], [PatientId], [IPNumber], [TotalBalance], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[BodyFluidBalanceChart];
SET IDENTITY_INSERT [Operations].[BodyFluidBalanceChart] OFF;
GO

PRINT 'Migrating [Operations].[BodyFluidBalanceDetails]...';
SET IDENTITY_INSERT [Operations].[BodyFluidBalanceDetails] ON;
INSERT INTO [Operations].[BodyFluidBalanceDetails] ([BodyFluidBalanceDetailsId], [BodyFluidBalanceChartId], [Date], [Time], [Fluid], [BloodProduct], [Rael], [Oral], [Urine], [Drain], [Others], [TypeOfBodyFluidBalance], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [TenantId])
SELECT [BodyFluidBalanceDetailsId], [BodyFluidBalanceChartId], [Date], [Time], [Fluid], [BloodProduct], [Rael], [Oral], [Urine], [Drain], [Others], [TypeOfBodyFluidBalance], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], @TenantId
FROM [SunCity_Clinics].[Operations].[BodyFluidBalanceDetails];
SET IDENTITY_INSERT [Operations].[BodyFluidBalanceDetails] OFF;
GO

PRINT 'Migrating [Operations].[CancelOperationMaster]...';
SET IDENTITY_INSERT [Operations].[CancelOperationMaster] ON;
INSERT INTO [Operations].[CancelOperationMaster] ([ID], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[CancelOperationMaster];
SET IDENTITY_INSERT [Operations].[CancelOperationMaster] OFF;
GO

PRINT 'Migrating [Operations].[CathForm]...';
SET IDENTITY_INSERT [Operations].[CathForm] ON;
INSERT INTO [Operations].[CathForm] ([ID], [PatientID], [Height], [Weight], [DiagnosticCardiacCath], [OrdinaryConsent], [PTCA], [STENT], [Company], [Cash], [ORCap], [IDBand], [IVCannula], [Dentures], [Glasses], [Shaved], [BladderEmpty], [FoleyCatheter], [IVInfusionRunning], [Food], [Drug], [Contrast], [Other], [AbsentRadial], [WeakPedal], [GoodPostTibia], [ECGDate], [ChestDate], [EchocardiographyDate], [StressDate], [StressOther], [CABGDate], [Graft], [CathDate], [Guiding], [StentUsed], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [HighRiskConsent], [CondomCatheter], [ECG], [Chest], [Echocardiography], [Stress], [CABG], [Cath], [TenantId])
SELECT [ID], [PatientID], [Height], [Weight], [DiagnosticCardiacCath], [OrdinaryConsent], [PTCA], [STENT], [Company], [Cash], [ORCap], [IDBand], [IVCannula], [Dentures], [Glasses], [Shaved], [BladderEmpty], [FoleyCatheter], [IVInfusionRunning], [Food], [Drug], [Contrast], [Other], [AbsentRadial], [WeakPedal], [GoodPostTibia], [ECGDate], [ChestDate], [EchocardiographyDate], [StressDate], [StressOther], [CABGDate], [Graft], [CathDate], [Guiding], [StentUsed], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [HighRiskConsent], [CondomCatheter], [ECG], [Chest], [Echocardiography], [Stress], [CABG], [Cath], @TenantId
FROM [SunCity_Clinics].[Operations].[CathForm];
SET IDENTITY_INSERT [Operations].[CathForm] OFF;
GO

PRINT 'Migrating [Operations].[ComplicationMaster]...';
SET IDENTITY_INSERT [Operations].[ComplicationMaster] ON;
INSERT INTO [Operations].[ComplicationMaster] ([ID], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[ComplicationMaster];
SET IDENTITY_INSERT [Operations].[ComplicationMaster] OFF;
GO

PRINT 'Migrating [Operations].[InstrumentCategoryMaster]...';
SET IDENTITY_INSERT [Operations].[InstrumentCategoryMaster] ON;
INSERT INTO [Operations].[InstrumentCategoryMaster] ([ID], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[InstrumentCategoryMaster];
SET IDENTITY_INSERT [Operations].[InstrumentCategoryMaster] OFF;
GO

PRINT 'Migrating [Operations].[InvestigationGroup]...';
SET IDENTITY_INSERT [Operations].[InvestigationGroup] ON;
INSERT INTO [Operations].[InvestigationGroup] ([ID], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[InvestigationGroup];
SET IDENTITY_INSERT [Operations].[InvestigationGroup] OFF;
GO

PRINT 'Migrating [Operations].[OperationClassification]...';
SET IDENTITY_INSERT [Operations].[OperationClassification] ON;
INSERT INTO [Operations].[OperationClassification] ([ID], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[OperationClassification];
SET IDENTITY_INSERT [Operations].[OperationClassification] OFF;
GO

PRINT 'Migrating [Operations].[OperationJobMaster]...';
SET IDENTITY_INSERT [Operations].[OperationJobMaster] ON;
INSERT INTO [Operations].[OperationJobMaster] ([ID], [Name], [NameEn], [CategoryId], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Name], [NameEn], [CategoryId], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[OperationJobMaster];
SET IDENTITY_INSERT [Operations].[OperationJobMaster] OFF;
GO

PRINT 'Migrating [Operations].[OperationLinkedItems]...';
SET IDENTITY_INSERT [Operations].[OperationLinkedItems] ON;
INSERT INTO [Operations].[OperationLinkedItems] ([Id], [ItemId], [OperationReservationId], [Code], [IsConfirmed], [CreatedDate], [CreatedBy], [ModifiedDate], [ModifiedBy], [BranchId], [CompanyId], [PreoperativeNurseId], [HandOverNurseId], [IsConfirmedFormHandoverNurse], [LinkedItemID], [TenantId])
SELECT [Id], [ItemId], [OperationReservationId], [Code], [IsConfirmed], [CreatedDate], [CreatedBy], [ModifiedDate], [ModifiedBy], [BranchId], [CompanyId], [PreoperativeNurseId], [HandOverNurseId], [IsConfirmedFormHandoverNurse], [LinkedItemID], @TenantId
FROM [SunCity_Clinics].[Operations].[OperationLinkedItems];
SET IDENTITY_INSERT [Operations].[OperationLinkedItems] OFF;
GO

PRINT 'Migrating [Operations].[OperationPostureMaster]...';
SET IDENTITY_INSERT [Operations].[OperationPostureMaster] ON;
INSERT INTO [Operations].[OperationPostureMaster] ([ID], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[OperationPostureMaster];
SET IDENTITY_INSERT [Operations].[OperationPostureMaster] OFF;
GO

PRINT 'Migrating [Operations].[OperationPriorityMaster]...';
SET IDENTITY_INSERT [Operations].[OperationPriorityMaster] ON;
INSERT INTO [Operations].[OperationPriorityMaster] ([ID], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[OperationPriorityMaster];
SET IDENTITY_INSERT [Operations].[OperationPriorityMaster] OFF;
GO

PRINT 'Migrating [Operations].[OperationReservation]...';
SET IDENTITY_INSERT [Operations].[OperationReservation] ON;
INSERT INTO [Operations].[OperationReservation] ([ID], [PatientId], [IP_Number], [SurgeonId], [SurgeonAssistantId], [AnesthesiologistId], [MedicalEngineerId], [DiagnosisId], [OTCategoryId], [OTRoomId], [OTDate], [SpecialityId], [SurgeryId], [OTTypeId], [PriorityId], [Comment], [Aproved], [PostPoned], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OPStatus], [OTCode], [Varification], [AprovedID], [StartTime], [EndTime], [OperationSetupID], [Done], [NurseActionStatus], [NurseInitTime], [NurseDoneTime], [InsuranceId], [RejectionReason], [branchId], [needICU], [icWard], [icRoom], [icBed], [TenantId])
SELECT [ID], [PatientId], [IP_Number], [SurgeonId], [SurgeonAssistantId], [AnesthesiologistId], [MedicalEngineerId], [DiagnosisId], [OTCategoryId], [OTRoomId], [OTDate], [SpecialityId], [SurgeryId], [OTTypeId], [PriorityId], [Comment], [Aproved], [PostPoned], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OPStatus], [OTCode], [Varification], [AprovedID], [StartTime], [EndTime], [OperationSetupID], [Done], [NurseActionStatus], [NurseInitTime], [NurseDoneTime], [InsuranceId], [RejectionReason], [branchId], [needICU], [icWard], [icRoom], [icBed], @TenantId
FROM [SunCity_Clinics].[Operations].[OperationReservation];
SET IDENTITY_INSERT [Operations].[OperationReservation] OFF;
GO

PRINT 'Migrating [Operations].[OperationRoomScedule]...';
SET IDENTITY_INSERT [Operations].[OperationRoomScedule] ON;
INSERT INTO [Operations].[OperationRoomScedule] ([ID], [OperationRoomID], [DayOfWeek], [StartTime], [EndTime], [SpecialityID], [CompanyID], [TenantId])
SELECT [ID], [OperationRoomID], [DayOfWeek], [StartTime], [EndTime], [SpecialityID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[OperationRoomScedule];
SET IDENTITY_INSERT [Operations].[OperationRoomScedule] OFF;
GO

PRINT 'Migrating [Operations].[OperationRoom]...';
SET IDENTITY_INSERT [Operations].[OperationRoom] ON;
INSERT INTO [Operations].[OperationRoom] ([ID], [Code], [NameAr], [NameEn], [Location], [CleanaceType], [OperationRoomType], [IsDayCase], [MaintenanceDayOfWeek], [MaintenanceDayCount], [SterilizationMinutesBetweenOperations], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SpecialityID], [WardID], [StockID], [SubstoreID], [IsICU], [TenantId])
SELECT [ID], [Code], [NameAr], [NameEn], [Location], [CleanaceType], [OperationRoomType], [IsDayCase], [MaintenanceDayOfWeek], [MaintenanceDayCount], [SterilizationMinutesBetweenOperations], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SpecialityID], [WardID], [StockID], [SubstoreID], [IsICU], @TenantId
FROM [SunCity_Clinics].[Operations].[OperationRoom];
SET IDENTITY_INSERT [Operations].[OperationRoom] OFF;
GO

PRINT 'Migrating [Operations].[OperationSetupMaster]...';
SET IDENTITY_INSERT [Operations].[OperationSetupMaster] ON;
INSERT INTO [Operations].[OperationSetupMaster] ([ID], [OperationId], [SpecialityId], [OTTypeId], [ServiceId], [PriorityId], [InvestigationGroupId], [PreprationGroupId], [FollowUpPeriod], [FollowUpNum], [OTDuration], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OperationTypeId], [TenantId])
SELECT [ID], [OperationId], [SpecialityId], [OTTypeId], [ServiceId], [PriorityId], [InvestigationGroupId], [PreprationGroupId], [FollowUpPeriod], [FollowUpNum], [OTDuration], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OperationTypeId], @TenantId
FROM [SunCity_Clinics].[Operations].[OperationSetupMaster];
SET IDENTITY_INSERT [Operations].[OperationSetupMaster] OFF;
GO

PRINT 'Migrating [Operations].[OperationSignOut]...';
SET IDENTITY_INSERT [Operations].[OperationSignOut] ON;
INSERT INTO [Operations].[OperationSignOut] ([ID], [PatientID], [NurseID], [SignOutDateTime], [SystemSignOutDateTime], [NameOfTheProcedurePerformedMatchingConsent], [InstrumentsSpongeAndNeedlesCountAreCorrect], [SpecimensAreProperlyLabeledIInCloudingPtNameAndIDNumber], [ClinicalTeamReviewTheKeyConcernsRegardingPatientRecoveryOrDischarge], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OperationReservationId], [TenantId])
SELECT [ID], [PatientID], [NurseID], [SignOutDateTime], [SystemSignOutDateTime], [NameOfTheProcedurePerformedMatchingConsent], [InstrumentsSpongeAndNeedlesCountAreCorrect], [SpecimensAreProperlyLabeledIInCloudingPtNameAndIDNumber], [ClinicalTeamReviewTheKeyConcernsRegardingPatientRecoveryOrDischarge], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OperationReservationId], @TenantId
FROM [SunCity_Clinics].[Operations].[OperationSignOut];
SET IDENTITY_INSERT [Operations].[OperationSignOut] OFF;
GO

PRINT 'Migrating [Operations].[OperationTypeMaster]...';
SET IDENTITY_INSERT [Operations].[OperationTypeMaster] ON;
INSERT INTO [Operations].[OperationTypeMaster] ([ID], [ArabicName], [EnglishName], [Active], [OProomPrice], [SergentPrice], [AnesthesiaPrice], [AssistantPrice], [CreatedDate], [CreatedBy], [LastModifiedBy], [LastModifiedDate], [OperationPriorityID], [TenantId])
SELECT [ID], [ArabicName], [EnglishName], [Active], [OProomPrice], [SergentPrice], [AnesthesiaPrice], [AssistantPrice], [CreatedDate], [CreatedBy], [LastModifiedBy], [LastModifiedDate], [OperationPriorityID], @TenantId
FROM [SunCity_Clinics].[Operations].[OperationTypeMaster];
SET IDENTITY_INSERT [Operations].[OperationTypeMaster] OFF;
GO

PRINT 'Migrating [Operations].[OperationWardMaster]...';
SET IDENTITY_INSERT [Operations].[OperationWardMaster] ON;
INSERT INTO [Operations].[OperationWardMaster] ([ID], [Code], [ArabicDescription], [EnglishDescription], [BuildID], [FloorID], [PharmacyID], [InventoryID], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [ArabicDescription], [EnglishDescription], [BuildID], [FloorID], [PharmacyID], [InventoryID], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[OperationWardMaster];
SET IDENTITY_INSERT [Operations].[OperationWardMaster] OFF;
GO

PRINT 'Migrating [Operations].[OperationWardRooms]...';
SET IDENTITY_INSERT [Operations].[OperationWardRooms] ON;
INSERT INTO [Operations].[OperationWardRooms] ([ID], [OperationWardMasterID], [Code], [RoomNO], [RoomTypeID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [OperationWardMasterID], [Code], [RoomNO], [RoomTypeID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[OperationWardRooms];
SET IDENTITY_INSERT [Operations].[OperationWardRooms] OFF;
GO

PRINT 'Migrating [Operations].[OperationWardSpecialities]...';
SET IDENTITY_INSERT [Operations].[OperationWardSpecialities] ON;
INSERT INTO [Operations].[OperationWardSpecialities] ([ID], [OperationWardMasterID], [SpecialityID], [CompanyID], [TenantId])
SELECT [ID], [OperationWardMasterID], [SpecialityID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[OperationWardSpecialities];
SET IDENTITY_INSERT [Operations].[OperationWardSpecialities] OFF;
GO

PRINT 'Migrating [Operations].[OperationWardWorkDay]...';
SET IDENTITY_INSERT [Operations].[OperationWardWorkDay] ON;
INSERT INTO [Operations].[OperationWardWorkDay] ([ID], [OperationWardMasterID], [WorkDayID], [CompanyID], [TenantId])
SELECT [ID], [OperationWardMasterID], [WorkDayID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[OperationWardWorkDay];
SET IDENTITY_INSERT [Operations].[OperationWardWorkDay] OFF;
GO

PRINT 'Migrating [Operations].[OperationsManage]...';
SET IDENTITY_INSERT [Operations].[OperationsManage] ON;
INSERT INTO [Operations].[OperationsManage] ([ID], [PatientId], [SurgeonId], [OperationID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OPStatus], [PreOperativeDiagnosis], [procedureID], [transOR], [transORTime], [NPO], [wearingWard], [wearingOT], [wardNote], [otNote], [wardConsentComplete], [otConsentComplete], [wardXray], [otxray], [wardXmatch], [otXmatch], [wardBR], [otBR], [wardObs], [otObs], [arrival], [anesBegin], [enesEnd], [surgBegin], [surgEnd], [delayReson], [isAlive], [complainID], [complication], [posture], [induction], [anes], [SurgComments], [ReservationID], [TenantId])
SELECT [ID], [PatientId], [SurgeonId], [OperationID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OPStatus], [PreOperativeDiagnosis], [procedureID], [transOR], [transORTime], [NPO], [wearingWard], [wearingOT], [wardNote], [otNote], [wardConsentComplete], [otConsentComplete], [wardXray], [otxray], [wardXmatch], [otXmatch], [wardBR], [otBR], [wardObs], [otObs], [arrival], [anesBegin], [enesEnd], [surgBegin], [surgEnd], [delayReson], [isAlive], [complainID], [complication], [posture], [induction], [anes], [SurgComments], [ReservationID], @TenantId
FROM [SunCity_Clinics].[Operations].[OperationsManage];
SET IDENTITY_INSERT [Operations].[OperationsManage] OFF;
GO

PRINT 'Migrating [Operations].[OperationsSetting]...';
SET IDENTITY_INSERT [Operations].[OperationsSetting] ON;
INSERT INTO [Operations].[OperationsSetting] ([ID], [OperationVitalSigns], [RecoveryVitalSigns], [WardVitalSigns], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OperationTimeslots], [TenantId])
SELECT [ID], [OperationVitalSigns], [RecoveryVitalSigns], [WardVitalSigns], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OperationTimeslots], @TenantId
FROM [SunCity_Clinics].[Operations].[OperationsSetting];
SET IDENTITY_INSERT [Operations].[OperationsSetting] OFF;
GO

PRINT 'Migrating [Operations].[Operations_Ward]...';
SET IDENTITY_INSERT [Operations].[Operations_Ward] ON;
INSERT INTO [Operations].[Operations_Ward] ([ID], [Code], [ArabicDescription], [EnglishDescription], [FloorID], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [ArabicDescription], [EnglishDescription], [FloorID], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[Operations_Ward];
SET IDENTITY_INSERT [Operations].[Operations_Ward] OFF;
GO

PRINT 'Migrating [Operations].[Operative]...';
SET IDENTITY_INSERT [Operations].[Operative] ON;
INSERT INTO [Operations].[Operative] ([Id], [PreOperativeDiagnosis], [PostOperativeDiagnosis], [SurgeonName], [Anesthesiologist], [FirstAssistant], [SecondAssistant], [AnesthesiaTechnician], [ProcedureDone], [TimeStarted], [TimeEnded], [OperativeDetails], [ComplicationsOccurred], [ComplicationsOccurredDetails], [BloodLossInCC], [BloodTransfusedInCC], [RegistryDevices], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PreOperativeDiagnosis], [PostOperativeDiagnosis], [SurgeonName], [Anesthesiologist], [FirstAssistant], [SecondAssistant], [AnesthesiaTechnician], [ProcedureDone], [TimeStarted], [TimeEnded], [OperativeDetails], [ComplicationsOccurred], [ComplicationsOccurredDetails], [BloodLossInCC], [BloodTransfusedInCC], [RegistryDevices], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[Operative];
SET IDENTITY_INSERT [Operations].[Operative] OFF;
GO

PRINT 'Migrating [Operations].[PatientBloodTransfusionConsent]...';
SET IDENTITY_INSERT [Operations].[PatientBloodTransfusionConsent] ON;
INSERT INTO [Operations].[PatientBloodTransfusionConsent] ([Id], [NameOfPatientGuardianKin], [SignatureOfPatientGuardianKin], [DateSignatureOfPatientGuardianKin], [Relationship], [NameAndSignatureOfWitness], [NameAndSignatureOfInterpreter], [NameAndSignatureOfPhysician], [DateOfPhysician], [TimeOfPhysician], [CompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [Id], [NameOfPatientGuardianKin], [SignatureOfPatientGuardianKin], [DateSignatureOfPatientGuardianKin], [Relationship], [NameAndSignatureOfWitness], [NameAndSignatureOfInterpreter], [NameAndSignatureOfPhysician], [DateOfPhysician], [TimeOfPhysician], [CompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[Operations].[PatientBloodTransfusionConsent];
SET IDENTITY_INSERT [Operations].[PatientBloodTransfusionConsent] OFF;
GO

PRINT 'Migrating [Operations].[PatientRiskConsent]...';
SET IDENTITY_INSERT [Operations].[PatientRiskConsent] ON;
INSERT INTO [Operations].[PatientRiskConsent] ([Id], [MedicalProblemsEn], [MedicalProblemsAr], [OperativeDetailsEn], [OperativeDetailsAr], [NameOfPatientGuardianKin], [SignatureOfPatientGuardianKin], [DateSignatureOfPatientGuardianKin], [Relationship], [NameAndSignatureOfWitness], [NameAndSignatureOfInterpreter], [DateOfPhysician], [TimeOfPhysician], [CompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [NameAndSignatureOfPhysician], [TenantId])
SELECT [Id], [MedicalProblemsEn], [MedicalProblemsAr], [OperativeDetailsEn], [OperativeDetailsAr], [NameOfPatientGuardianKin], [SignatureOfPatientGuardianKin], [DateSignatureOfPatientGuardianKin], [Relationship], [NameAndSignatureOfWitness], [NameAndSignatureOfInterpreter], [DateOfPhysician], [TimeOfPhysician], [CompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [NameAndSignatureOfPhysician], @TenantId
FROM [SunCity_Clinics].[Operations].[PatientRiskConsent];
SET IDENTITY_INSERT [Operations].[PatientRiskConsent] OFF;
GO

PRINT 'Migrating [Operations].[PositionMaster]...';
SET IDENTITY_INSERT [Operations].[PositionMaster] ON;
INSERT INTO [Operations].[PositionMaster] ([ID], [ArabicName], [EnglishName], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ArabicName], [EnglishName], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[PositionMaster];
SET IDENTITY_INSERT [Operations].[PositionMaster] OFF;
GO

PRINT 'Migrating [Operations].[PostAnesthesiaCare]...';
SET IDENTITY_INSERT [Operations].[PostAnesthesiaCare] ON;
INSERT INTO [Operations].[PostAnesthesiaCare] ([Id], [PatientId], [PreOperativeId], [OperationReservationId], [IPNumber], [AnesthesiaDate], [AnesthesiaTime], [Operation], [SurgeryDuration], [Anesthesiologist], [Surgeon], [GA], [Epidural], [Spinal], [AnethOther], [AnesthOtherComment], [ETT], [Extubated], [OPAirway], [Awake], [Sedated], [Stupors], [Drowsy], [Comatose], [NGT], [PortVac], [ChestTube], [FoleyCatheter], [TPiece], [NasalCanula], [VentilationMask], [CanulaOther], [CanulaOtherComment], [UrineOutput], [Drain], [BalanceIn], [BalanceOut], [BalanceAmount], [TransferAwake], [TransferStable], [TransferPainFree], [DischargeToWard], [DischargeToICU], [DischargeToCCU], [DischargeToOther], [DischargeToOtherComment], [TransferTime], [Instructions], [CreatedDate], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientId], [PreOperativeId], [OperationReservationId], [IPNumber], [AnesthesiaDate], [AnesthesiaTime], [Operation], [SurgeryDuration], [Anesthesiologist], [Surgeon], [GA], [Epidural], [Spinal], [AnethOther], [AnesthOtherComment], [ETT], [Extubated], [OPAirway], [Awake], [Sedated], [Stupors], [Drowsy], [Comatose], [NGT], [PortVac], [ChestTube], [FoleyCatheter], [TPiece], [NasalCanula], [VentilationMask], [CanulaOther], [CanulaOtherComment], [UrineOutput], [Drain], [BalanceIn], [BalanceOut], [BalanceAmount], [TransferAwake], [TransferStable], [TransferPainFree], [DischargeToWard], [DischargeToICU], [DischargeToCCU], [DischargeToOther], [DischargeToOtherComment], [TransferTime], [Instructions], [CreatedDate], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[PostAnesthesiaCare];
SET IDENTITY_INSERT [Operations].[PostAnesthesiaCare] OFF;
GO

PRINT 'Migrating [Operations].[PostAnesthesiaVitalSigns]...';
SET IDENTITY_INSERT [Operations].[PostAnesthesiaVitalSigns] ON;
INSERT INTO [Operations].[PostAnesthesiaVitalSigns] ([Id], [PostAnesthesiaId], [BPTop], [BPBottom], [HeartRate], [RespiratoryRate], [Temperature], [FIO2], [SaO2], [PreOpData], [TimeTaken], [Remarks], [CreatedDate], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PostAnesthesiaId], [BPTop], [BPBottom], [HeartRate], [RespiratoryRate], [Temperature], [FIO2], [SaO2], [PreOpData], [TimeTaken], [Remarks], [CreatedDate], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[PostAnesthesiaVitalSigns];
SET IDENTITY_INSERT [Operations].[PostAnesthesiaVitalSigns] OFF;
GO

PRINT 'Migrating [Operations].[PreOperativeHandOver]...';
SET IDENTITY_INSERT [Operations].[PreOperativeHandOver] ON;
INSERT INTO [Operations].[PreOperativeHandOver] ([ID], [ProcedureName], [PreOperativeId], [ReceivedNurseId], [NurseAccompaniedFromWardID], [NewImplantsDetails], [ConfirmPreOperativeImplants], [SystemDate], [ActivityDate], [CreatedBy], [CreatedDate], [CompanyID], [TenantId])
SELECT [ID], [ProcedureName], [PreOperativeId], [ReceivedNurseId], [NurseAccompaniedFromWardID], [NewImplantsDetails], [ConfirmPreOperativeImplants], [SystemDate], [ActivityDate], [CreatedBy], [CreatedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[PreOperativeHandOver];
SET IDENTITY_INSERT [Operations].[PreOperativeHandOver] OFF;
GO

PRINT 'Migrating [Operations].[PreOperative]...';
SET IDENTITY_INSERT [Operations].[PreOperative] ON;
INSERT INTO [Operations].[PreOperative] ([ID], [PatientID], [ProcedureId], [MedicalObservationID], [HasImplants], [ImplantsDetails], [TranseredWardID], [SystemDate], [ActivityDate], [StaffAccompanyingNurseID], [PatientIPOP], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [RespiratoryRate], [Temp], [Pulse], [O2Saturation], [BPSystolic], [BPDiastolic], [HasPain], [PainScore], [Location], [Character], [Duration], [Frequency], [OperationReservationId], [TranseredRoomID], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [ProcedureId], [MedicalObservationID], [HasImplants], [ImplantsDetails], [TranseredWardID], [SystemDate], [ActivityDate], [StaffAccompanyingNurseID], [PatientIPOP], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [RespiratoryRate], [Temp], [Pulse], [O2Saturation], [BPSystolic], [BPDiastolic], [HasPain], [PainScore], [Location], [Character], [Duration], [Frequency], [OperationReservationId], [TranseredRoomID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[PreOperative];
SET IDENTITY_INSERT [Operations].[PreOperative] OFF;
GO

PRINT 'Migrating [Operations].[PreOperative_PreProceduralRecord]...';
SET IDENTITY_INSERT [Operations].[PreOperative_PreProceduralRecord] ON;
INSERT INTO [Operations].[PreOperative_PreProceduralRecord] ([ID], [PreOperativeID], [PreProceduralRecord], [SelectedOption], [Confirmed], [CompanyID], [TenantId])
SELECT [ID], [PreOperativeID], [PreProceduralRecord], [SelectedOption], [Confirmed], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[PreOperative_PreProceduralRecord];
SET IDENTITY_INSERT [Operations].[PreOperative_PreProceduralRecord] OFF;
GO

PRINT 'Migrating [Operations].[PreProceduralRecord]...';
SET IDENTITY_INSERT [Operations].[PreProceduralRecord] ON;
INSERT INTO [Operations].[PreProceduralRecord] ([PreProceduralRecordId], [PreProceduralRecordNameAr], [AllowApplicable], [PreProceduralRecordNameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [PreProceduralRecordId], [PreProceduralRecordNameAr], [AllowApplicable], [PreProceduralRecordNameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[PreProceduralRecord];
SET IDENTITY_INSERT [Operations].[PreProceduralRecord] OFF;
GO

PRINT 'Migrating [Operations].[PreprationGroup]...';
SET IDENTITY_INSERT [Operations].[PreprationGroup] ON;
INSERT INTO [Operations].[PreprationGroup] ([ID], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[PreprationGroup];
SET IDENTITY_INSERT [Operations].[PreprationGroup] OFF;
GO

PRINT 'Migrating [Operations].[Prroperativeverification]...';
SET IDENTITY_INSERT [Operations].[Prroperativeverification] ON;
INSERT INTO [Operations].[Prroperativeverification] ([id], [PatientID], [DoctorAssessmentID], [VitalSigns], [Temp], [Saturation], [Systolic], [Diastolic], [Pain], [PainScore], [CorrectPatientCompleted], [CorrectProcedureCompleted], [informedConsentCompleted], [AnesthesiaCompleted], [HighRiskCompleted], [ConsentCompleted], [OthersCompleted], [informedConsentAppropriate], [AnesthesiaAppropriate], [HighRiskAppropriate], [ConsentAppropriate], [OthersAppropriate], [BAvailable], [CAvailable], [DAvailable], [EAvailable], [BAvailableSigned], [CAvailableSigned], [DAvailableSigned], [EAvailableSigned], [BAvailableLabeled], [CAvailableLabeled], [DAvailableLabeled], [EAvailableLabeled], [Sitemarked], [ElectroAvailable], [SuctionUnitAvailable], [DisposableAvailable], [SterileAvailable], [MicroscopeAvailable], [ENTAvailable], [LaparoscopicAvailable], [HysteroscopyAvailable], [UrologicAvailable], [ArthroscopicAvailable], [ProsthesisAvailable], [implantsAvailable], [ScrewsAvailable], [InternalAvailable], [UltrasoundAvailable], [BloodAvailable], [FluoroscopyAvailable], [InfusionAvailable], [JavixAvailable], [InstrumentsAvailable], [OthersAvailable], [NPOAvailable], [CorrectAvailable], [SurgicalAvailable], [InformedAvailable], [AnesthesiaAvailable], [HighAvailable], [ConsentAvailable], [ImagesAvailable], [LaboratoryAvailable], [OtherAvailableAvailable], [RequestedAvailable], [ElectroFunctional], [SuctionUnitFunctional], [DisposableFunctional], [SterileFunctional], [MicroscopeFunctional], [ENTFunctional], [LaparoscopicFunctional], [HysteroscopyFunctional], [UrologicFunctional], [ArthroscopicFunctional], [ProsthesisFunctional], [implantsFunctional], [ScrewsFunctional], [InternalFunctional], [UltrasoundFunctional], [BloodFunctional], [FluoroscopyFunctional], [InfusionFunctional], [JavixFunctional], [InstrumentsFunctional], [OthersFunctional], [NPOFunctional], [CorrectFunctional], [SurgicalFunctional], [InformedFunctional], [AnesthesiaFunctional], [HighFunctional], [ConsentFunctional], [ImagesFunctional], [LaboratoryFunctional], [OtherFunctional], [RequestedFunctional], [Pulse], [CreatedDate], [CreatedBy], [OtherSpecify], [Specify], [ReciveNurse], [CirculatingNurse], [TransfertoORbed], [ComfortmeasureImplemented], [NPOafterMidnight], [PreOperativeId], [CompanyID], [TenantId])
SELECT [id], [PatientID], [DoctorAssessmentID], [VitalSigns], [Temp], [Saturation], [Systolic], [Diastolic], [Pain], [PainScore], [CorrectPatientCompleted], [CorrectProcedureCompleted], [informedConsentCompleted], [AnesthesiaCompleted], [HighRiskCompleted], [ConsentCompleted], [OthersCompleted], [informedConsentAppropriate], [AnesthesiaAppropriate], [HighRiskAppropriate], [ConsentAppropriate], [OthersAppropriate], [BAvailable], [CAvailable], [DAvailable], [EAvailable], [BAvailableSigned], [CAvailableSigned], [DAvailableSigned], [EAvailableSigned], [BAvailableLabeled], [CAvailableLabeled], [DAvailableLabeled], [EAvailableLabeled], [Sitemarked], [ElectroAvailable], [SuctionUnitAvailable], [DisposableAvailable], [SterileAvailable], [MicroscopeAvailable], [ENTAvailable], [LaparoscopicAvailable], [HysteroscopyAvailable], [UrologicAvailable], [ArthroscopicAvailable], [ProsthesisAvailable], [implantsAvailable], [ScrewsAvailable], [InternalAvailable], [UltrasoundAvailable], [BloodAvailable], [FluoroscopyAvailable], [InfusionAvailable], [JavixAvailable], [InstrumentsAvailable], [OthersAvailable], [NPOAvailable], [CorrectAvailable], [SurgicalAvailable], [InformedAvailable], [AnesthesiaAvailable], [HighAvailable], [ConsentAvailable], [ImagesAvailable], [LaboratoryAvailable], [OtherAvailableAvailable], [RequestedAvailable], [ElectroFunctional], [SuctionUnitFunctional], [DisposableFunctional], [SterileFunctional], [MicroscopeFunctional], [ENTFunctional], [LaparoscopicFunctional], [HysteroscopyFunctional], [UrologicFunctional], [ArthroscopicFunctional], [ProsthesisFunctional], [implantsFunctional], [ScrewsFunctional], [InternalFunctional], [UltrasoundFunctional], [BloodFunctional], [FluoroscopyFunctional], [InfusionFunctional], [JavixFunctional], [InstrumentsFunctional], [OthersFunctional], [NPOFunctional], [CorrectFunctional], [SurgicalFunctional], [InformedFunctional], [AnesthesiaFunctional], [HighFunctional], [ConsentFunctional], [ImagesFunctional], [LaboratoryFunctional], [OtherFunctional], [RequestedFunctional], [Pulse], [CreatedDate], [CreatedBy], [OtherSpecify], [Specify], [ReciveNurse], [CirculatingNurse], [TransfertoORbed], [ComfortmeasureImplemented], [NPOafterMidnight], [PreOperativeId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[Prroperativeverification];
SET IDENTITY_INSERT [Operations].[Prroperativeverification] OFF;
GO

PRINT 'Migrating [Operations].[SurgicalInstrumentsMaster]...';
SET IDENTITY_INSERT [Operations].[SurgicalInstrumentsMaster] ON;
INSERT INTO [Operations].[SurgicalInstrumentsMaster] ([ID], [ArabicName], [EnglishName], [Active], [InstrumentsCategoryID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ArabicName], [EnglishName], [Active], [InstrumentsCategoryID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[SurgicalInstrumentsMaster];
SET IDENTITY_INSERT [Operations].[SurgicalInstrumentsMaster] OFF;
GO

PRINT 'Migrating [Operations].[SurgicalProceduralConsent]...';
SET IDENTITY_INSERT [Operations].[SurgicalProceduralConsent] ON;
INSERT INTO [Operations].[SurgicalProceduralConsent] ([Id], [PatientId], [DoctorId], [performthefollowingsurgery], [performthefollowingsurgeryAr], [Thepossiblerisks], [ThepossiblerisksAr], [Witness], [Interpreter], [LegalGuardian], [Thereasonwhythepatientisunabletosigntheconsentform], [relationship], [underage], [CreatedDate], [LastModifiedDate], [CreatedBy], [LastModifiedBy], [CompanyID], [TenantId])
SELECT [Id], [PatientId], [DoctorId], [performthefollowingsurgery], [performthefollowingsurgeryAr], [Thepossiblerisks], [ThepossiblerisksAr], [Witness], [Interpreter], [LegalGuardian], [Thereasonwhythepatientisunabletosigntheconsentform], [relationship], [underage], [CreatedDate], [LastModifiedDate], [CreatedBy], [LastModifiedBy], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[SurgicalProceduralConsent];
SET IDENTITY_INSERT [Operations].[SurgicalProceduralConsent] OFF;
GO

PRINT 'Migrating [Operations].[SurgicalSpecimens]...';
SET IDENTITY_INSERT [Operations].[SurgicalSpecimens] ON;
INSERT INTO [Operations].[SurgicalSpecimens] ([Id], [OriginOfSpecimens], [SizeAndDescription], [SentToBiopsy], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OperativeId], [TenantId])
SELECT [Id], [OriginOfSpecimens], [SizeAndDescription], [SentToBiopsy], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OperativeId], @TenantId
FROM [SunCity_Clinics].[Operations].[SurgicalSpecimens];
SET IDENTITY_INSERT [Operations].[SurgicalSpecimens] OFF;
GO

PRINT 'Migrating [Operations].[SurgicalSuppliesMaster]...';
SET IDENTITY_INSERT [Operations].[SurgicalSuppliesMaster] ON;
INSERT INTO [Operations].[SurgicalSuppliesMaster] ([ID], [ArabicName], [EnglishName], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [ServiceID], [CompanyID], [TenantId])
SELECT [ID], [ArabicName], [EnglishName], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [ServiceID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[SurgicalSuppliesMaster];
SET IDENTITY_INSERT [Operations].[SurgicalSuppliesMaster] OFF;
GO

PRINT 'Migrating [Operations].[TimeOut]...';
SET IDENTITY_INSERT [Operations].[TimeOut] ON;
INSERT INTO [Operations].[TimeOut] ([TimeOutId], [PatientId], [EmployeeId], [Date], [TimeOutTime], [ProcedureStartedDate], [PreoperativeVerification], [CorrectPatientIdentity], [CorrectSite], [CorrectSide], [CorrectProcedure], [AvailabilityOfAllEquipments], [AvailabilityOfCorrectImplants], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [OperationReservationId], [CompanyID], [TenantId])
SELECT [TimeOutId], [PatientId], [EmployeeId], [Date], [TimeOutTime], [ProcedureStartedDate], [PreoperativeVerification], [CorrectPatientIdentity], [CorrectSite], [CorrectSide], [CorrectProcedure], [AvailabilityOfAllEquipments], [AvailabilityOfCorrectImplants], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [OperationReservationId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[TimeOut];
SET IDENTITY_INSERT [Operations].[TimeOut] OFF;
GO

PRINT 'Migrating [Operations].[VerificationProcess]...';
SET IDENTITY_INSERT [Operations].[VerificationProcess] ON;
INSERT INTO [Operations].[VerificationProcess] ([ID], [OperationId], [PrOperativeDiagnosisId], [ProcedureId], [ComfortMeasureImplement], [TranferToORbed], [TranferToOR], [NPOAfteerMidnight], [PatientWearingIdBraceletWard], [PatientWearingIdBraceletOT], [PatientNotesAndLabelsWard], [PatientNotesAndLabelsOT], [ConsentFromCompleteWard], [ConsentFromCompleteOT], [XRaysScanECGWard], [XRaysScanECGOT], [BloodResultsWard], [BloodResultsOT], [XMAtchFromWard], [XMAtchFromOT], [SergicalObservationChartWard], [SergicalObservationChartOT], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [OperationId], [PrOperativeDiagnosisId], [ProcedureId], [ComfortMeasureImplement], [TranferToORbed], [TranferToOR], [NPOAfteerMidnight], [PatientWearingIdBraceletWard], [PatientWearingIdBraceletOT], [PatientNotesAndLabelsWard], [PatientNotesAndLabelsOT], [ConsentFromCompleteWard], [ConsentFromCompleteOT], [XRaysScanECGWard], [XRaysScanECGOT], [BloodResultsWard], [BloodResultsOT], [XMAtchFromWard], [XMAtchFromOT], [SergicalObservationChartWard], [SergicalObservationChartOT], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Operations].[VerificationProcess];
SET IDENTITY_INSERT [Operations].[VerificationProcess] OFF;
GO

PRINT 'Migrating [OutPatient].[AdmittingPatient]...';
SET IDENTITY_INSERT [OutPatient].[AdmittingPatient] ON;
INSERT INTO [OutPatient].[AdmittingPatient] ([Id], [PatientID], [Approx], [DoctorsID], [AdmitDate], [AdmitTime], [MLCID], [Remarks], [WardId], [BedNoID], [ISDaycase], [ISEmergency], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [IPNumber], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [Approx], [DoctorsID], [AdmitDate], [AdmitTime], [MLCID], [Remarks], [WardId], [BedNoID], [ISDaycase], [ISEmergency], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [IPNumber], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[AdmittingPatient];
SET IDENTITY_INSERT [OutPatient].[AdmittingPatient] OFF;
GO

PRINT 'Migrating [OutPatient].[AllergyDetails]...';
SET IDENTITY_INSERT [OutPatient].[AllergyDetails] ON;
INSERT INTO [OutPatient].[AllergyDetails] ([ID], [Code], [NameArabic], [NameEnglish], [AllergyMasterID], [TenantId])
SELECT [ID], [Code], [NameArabic], [NameEnglish], [AllergyMasterID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[AllergyDetails];
SET IDENTITY_INSERT [OutPatient].[AllergyDetails] OFF;
GO

PRINT 'Migrating [OutPatient].[AllergyMaster]...';
SET IDENTITY_INSERT [OutPatient].[AllergyMaster] ON;
INSERT INTO [OutPatient].[AllergyMaster] ([ID], [Code], [NameArabic], [NameEnglish], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [NameArabic], [NameEnglish], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[AllergyMaster];
SET IDENTITY_INSERT [OutPatient].[AllergyMaster] OFF;
GO

PRINT 'Migrating [OutPatient].[ClinicLocation]...';
SET IDENTITY_INSERT [OutPatient].[ClinicLocation] ON;
INSERT INTO [OutPatient].[ClinicLocation] ([ID], [Code], [NameArabic], [NameEnglish], [CompanyID], [CreatedBy], [CreatedDate], [ModifiedBy], [ModificationDate], [TenantId])
SELECT [ID], [Code], [NameArabic], [NameEnglish], [CompanyID], [CreatedBy], [CreatedDate], [ModifiedBy], [ModificationDate], @TenantId
FROM [SunCity_Clinics].[OutPatient].[ClinicLocation];
SET IDENTITY_INSERT [OutPatient].[ClinicLocation] OFF;
GO

PRINT 'Migrating [OutPatient].[ClinicProcedures]...';
SET IDENTITY_INSERT [OutPatient].[ClinicProcedures] ON;
INSERT INTO [OutPatient].[ClinicProcedures] ([ID], [ProcedureID], [ServiceID], [ProcedureSlot], [FollowUpNum], [FollowUpDuration], [ItFollowUp], [status], [ClinicID], [CreatedBy], [Creationdate], [ModifiedBy], [ModificationDate], [FollowUpPeriod], [CompanyID], [TenantId])
SELECT [ID], [ProcedureID], [ServiceID], [ProcedureSlot], [FollowUpNum], [FollowUpDuration], [ItFollowUp], [status], [ClinicID], [CreatedBy], [Creationdate], [ModifiedBy], [ModificationDate], [FollowUpPeriod], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[ClinicProcedures];
SET IDENTITY_INSERT [OutPatient].[ClinicProcedures] OFF;
GO

PRINT 'Migrating [OutPatient].[ClinicSchedule]...';
SET IDENTITY_INSERT [OutPatient].[ClinicSchedule] ON;
INSERT INTO [OutPatient].[ClinicSchedule] ([ID], [DayID], [StartTime], [EndTime], [SubSpecialityID], [DoctorID], [Status], [ClinicID], [CreatedBy], [Creationdate], [ModifiedBy], [ModificationDate], [SessionId], [CompanyID], [TenantId])
SELECT [ID], [DayID], [StartTime], [EndTime], [SubSpecialityID], [DoctorID], [Status], [ClinicID], [CreatedBy], [Creationdate], [ModifiedBy], [ModificationDate], [SessionId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[ClinicSchedule];
SET IDENTITY_INSERT [OutPatient].[ClinicSchedule] OFF;
GO

PRINT 'Migrating [OutPatient].[ClinicSetup]...';
SET IDENTITY_INSERT [OutPatient].[ClinicSetup] ON;
INSERT INTO [OutPatient].[ClinicSetup] ([ID], [Code], [NameArabic], [NameEnglish], [TimeSlot], [OverBooking], [DepartmentID], [SpeciaityGroupID], [Status], [MedicalRecordLocationID], [ClinicLocationID], [PharmcyID], [ClinicTypeID], [StotreID], [PaidDealingID], [SessionID], [WalkInPatientOnly], [FutureBooking], [EndOfDay], [CreatedBy], [Creationdate], [ModifiedBy], [ModificationDate], [NameRu], [CompanyID], [BranchId], [TenantId])
SELECT [ID], [Code], [NameArabic], [NameEnglish], [TimeSlot], [OverBooking], [DepartmentID], [SpeciaityGroupID], [Status], [MedicalRecordLocationID], [ClinicLocationID], [PharmcyID], [ClinicTypeID], [StotreID], [PaidDealingID], [SessionID], [WalkInPatientOnly], [FutureBooking], [EndOfDay], [CreatedBy], [Creationdate], [ModifiedBy], [ModificationDate], [NameRu], [CompanyID], [BranchId], @TenantId
FROM [SunCity_Clinics].[OutPatient].[ClinicSetup];
SET IDENTITY_INSERT [OutPatient].[ClinicSetup] OFF;
GO

PRINT 'Migrating [OutPatient].[ClinicType]...';
SET IDENTITY_INSERT [OutPatient].[ClinicType] ON;
INSERT INTO [OutPatient].[ClinicType] ([ID], [Code], [NameArabic], [NameEnglish], [CompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [ID], [Code], [NameArabic], [NameEnglish], [CompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[OutPatient].[ClinicType];
SET IDENTITY_INSERT [OutPatient].[ClinicType] OFF;
GO

PRINT 'Migrating [OutPatient].[ClinicalServices]...';
SET IDENTITY_INSERT [OutPatient].[ClinicalServices] ON;
INSERT INTO [OutPatient].[ClinicalServices] ([Id], [Service], [Cost], [doctorId], [ServiceDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Service], [Cost], [doctorId], [ServiceDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[ClinicalServices];
SET IDENTITY_INSERT [OutPatient].[ClinicalServices] OFF;
GO

PRINT 'Migrating [OutPatient].[ClinicalSnomed]...';
SET IDENTITY_INSERT [OutPatient].[ClinicalSnomed] ON;
INSERT INTO [OutPatient].[ClinicalSnomed] ([ID], [Code], [Name], [TenantId])
SELECT [ID], [Code], [Name], @TenantId
FROM [SunCity_Clinics].[OutPatient].[ClinicalSnomed];
SET IDENTITY_INSERT [OutPatient].[ClinicalSnomed] OFF;
GO

PRINT 'Migrating [OutPatient].[ConsultationSetting]...';
SET IDENTITY_INSERT [OutPatient].[ConsultationSetting] ON;
INSERT INTO [OutPatient].[ConsultationSetting] ([Id], [SpecilaityGroupMasterId], [IsClinicPrice], [IsSpecilaityPrice], [ClinicId], [ServiceId], [SpecialtyPrice], [CreatedBy], [CreatedDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [Id], [SpecilaityGroupMasterId], [IsClinicPrice], [IsSpecilaityPrice], [ClinicId], [ServiceId], [SpecialtyPrice], [CreatedBy], [CreatedDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[ConsultationSetting];
SET IDENTITY_INSERT [OutPatient].[ConsultationSetting] OFF;
GO

PRINT 'Migrating [OutPatient].[DailyCloseDate]...';
SET IDENTITY_INSERT [OutPatient].[DailyCloseDate] ON;
INSERT INTO [OutPatient].[DailyCloseDate] ([ID], [CloseDate], [CompanyID], [TenantId])
SELECT [ID], [CloseDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[DailyCloseDate];
SET IDENTITY_INSERT [OutPatient].[DailyCloseDate] OFF;
GO

PRINT 'Migrating [OutPatient].[DiagnosisAnswers]...';
SET IDENTITY_INSERT [OutPatient].[DiagnosisAnswers] ON;
INSERT INTO [OutPatient].[DiagnosisAnswers] ([Id], [DiagnosisGroupID], [DiagnosisAnswerName], [DiagnosisQuestionID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [DiagnosisGroupID], [DiagnosisAnswerName], [DiagnosisQuestionID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[DiagnosisAnswers];
SET IDENTITY_INSERT [OutPatient].[DiagnosisAnswers] OFF;
GO

PRINT 'Migrating [OutPatient].[DiagnosisGroups]...';
SET IDENTITY_INSERT [OutPatient].[DiagnosisGroups] ON;
INSERT INTO [OutPatient].[DiagnosisGroups] ([Id], [DiagnosisGroupName], [CostCenterID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [DiagnosisGroupName], [CostCenterID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[DiagnosisGroups];
SET IDENTITY_INSERT [OutPatient].[DiagnosisGroups] OFF;
GO

PRINT 'Migrating [OutPatient].[DiagnosisMain]...';
SET IDENTITY_INSERT [OutPatient].[DiagnosisMain] ON;
INSERT INTO [OutPatient].[DiagnosisMain] ([Id], [Code], [NameEng], [NameArab], [CompanyID], [TenantId])
SELECT [Id], [Code], [NameEng], [NameArab], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[DiagnosisMain];
SET IDENTITY_INSERT [OutPatient].[DiagnosisMain] OFF;
GO

PRINT 'Migrating [OutPatient].[DiagnosisQuestion]...';
SET IDENTITY_INSERT [OutPatient].[DiagnosisQuestion] ON;
INSERT INTO [OutPatient].[DiagnosisQuestion] ([Id], [CostCenterID], [DiagnosisGroupID], [DiagnosisQuestionName], [OrderQuest], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [CostCenterID], [DiagnosisGroupID], [DiagnosisQuestionName], [OrderQuest], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[DiagnosisQuestion];
SET IDENTITY_INSERT [OutPatient].[DiagnosisQuestion] OFF;
GO

PRINT 'Migrating [OutPatient].[DiagnosisSub]...';
SET IDENTITY_INSERT [OutPatient].[DiagnosisSub] ON;
INSERT INTO [OutPatient].[DiagnosisSub] ([Id], [Code], [NameEng], [NameArab], [MainId], [CompanyID], [TenantId])
SELECT [Id], [Code], [NameEng], [NameArab], [MainId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[DiagnosisSub];
SET IDENTITY_INSERT [OutPatient].[DiagnosisSub] OFF;
GO

PRINT 'Migrating [OutPatient].[DiseaseCategory]...';
SET IDENTITY_INSERT [OutPatient].[DiseaseCategory] ON;
INSERT INTO [OutPatient].[DiseaseCategory] ([Id], [DiseaseCategoryName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [DiseaseCategoryName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[DiseaseCategory];
SET IDENTITY_INSERT [OutPatient].[DiseaseCategory] OFF;
GO

PRINT 'Migrating [OutPatient].[DiseaseMaster]...';
SET IDENTITY_INSERT [OutPatient].[DiseaseMaster] ON;
INSERT INTO [OutPatient].[DiseaseMaster] ([Id], [DiseaseCategoryID], [DiseaseName], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IsChronic], [IsCommunicable], [ISHIbB], [DiseaseNameAr], [TenantId])
SELECT [Id], [DiseaseCategoryID], [DiseaseName], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IsChronic], [IsCommunicable], [ISHIbB], [DiseaseNameAr], @TenantId
FROM [SunCity_Clinics].[OutPatient].[DiseaseMaster];
SET IDENTITY_INSERT [OutPatient].[DiseaseMaster] OFF;
GO

PRINT 'Migrating [OutPatient].[Disease_InfectionTypes]...';
SET IDENTITY_INSERT [OutPatient].[Disease_InfectionTypes] ON;
INSERT INTO [OutPatient].[Disease_InfectionTypes] ([id], [DiseaseID], [InfectionTypeID], [TenantId])
SELECT [id], [DiseaseID], [InfectionTypeID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[Disease_InfectionTypes];
SET IDENTITY_INSERT [OutPatient].[Disease_InfectionTypes] OFF;
GO

PRINT 'Migrating [OutPatient].[DoctorGeneralSchedule]...';
SET IDENTITY_INSERT [OutPatient].[DoctorGeneralSchedule] ON;
INSERT INTO [OutPatient].[DoctorGeneralSchedule] ([ID], [DayID], [SubSpecialityID], [DoctorID], [Status], [CreatedBy], [CreationDate], [SessionId], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [DayID], [SubSpecialityID], [DoctorID], [Status], [CreatedBy], [CreationDate], [SessionId], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[DoctorGeneralSchedule];
SET IDENTITY_INSERT [OutPatient].[DoctorGeneralSchedule] OFF;
GO

PRINT 'Migrating [OutPatient].[DoctorRemarksAndComments]...';
SET IDENTITY_INSERT [OutPatient].[DoctorRemarksAndComments] ON;
INSERT INTO [OutPatient].[DoctorRemarksAndComments] ([ID], [DoctorID], [OPNumber], [PatientID], [DoctorRemark], [PatientComment], [IsPatientComment], [Date], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [DoctorID], [OPNumber], [PatientID], [DoctorRemark], [PatientComment], [IsPatientComment], [Date], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[DoctorRemarksAndComments];
SET IDENTITY_INSERT [OutPatient].[DoctorRemarksAndComments] OFF;
GO

PRINT 'Migrating [OutPatient].[DoctorsPersonalList]...';
SET IDENTITY_INSERT [OutPatient].[DoctorsPersonalList] ON;
INSERT INTO [OutPatient].[DoctorsPersonalList] ([Id], [Description], [Remarks], [Type], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [doctorId], [CompanyID], [TenantId])
SELECT [Id], [Description], [Remarks], [Type], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [doctorId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[DoctorsPersonalList];
SET IDENTITY_INSERT [OutPatient].[DoctorsPersonalList] OFF;
GO

PRINT 'Migrating [OutPatient].[DrugsWithInfusionRateDetailsDetails]...';
SET IDENTITY_INSERT [OutPatient].[DrugsWithInfusionRateDetailsDetails] ON;
INSERT INTO [OutPatient].[DrugsWithInfusionRateDetailsDetails] ([Id], [DrugsWithInfusionRateDetailsID], [DrugID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [GenericID], [TotalDose], [Volume], [TemplateId], [TenantId])
SELECT [Id], [DrugsWithInfusionRateDetailsID], [DrugID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [GenericID], [TotalDose], [Volume], [TemplateId], @TenantId
FROM [SunCity_Clinics].[OutPatient].[DrugsWithInfusionRateDetailsDetails];
SET IDENTITY_INSERT [OutPatient].[DrugsWithInfusionRateDetailsDetails] OFF;
GO

PRINT 'Migrating [OutPatient].[DrugsWithInfusionRateDetails]...';
SET IDENTITY_INSERT [OutPatient].[DrugsWithInfusionRateDetails] ON;
INSERT INTO [OutPatient].[DrugsWithInfusionRateDetails] ([Id], [DrugsWithInfusionRateID], [PrescriptionNo], [PatientID], [DoctorID], [DrugID], [DosageUnitID], [Dosage], [FrequencyID], [Period], [DoseQty], [StartDate], [StartTime], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TypeId], [AdminModeId], [Remarks], [ContinueDrugDuringAdmission], [CompanyID], [GenericID], [SpecialInstruction], [PRN_Reason], [PRN_MaxFreq], [IFNeeded], [Strength], [ClinicalPharmacy_Accept], [Cannula], [TemplateId], [MainSolution], [Volume], [DosageBaseUnit], [RegularContinous], [InfusionRate], [InfusionRatePeriod], [AdministrationType], [Status], [TenantId])
SELECT [Id], [DrugsWithInfusionRateID], [PrescriptionNo], [PatientID], [DoctorID], [DrugID], [DosageUnitID], [Dosage], [FrequencyID], [Period], [DoseQty], [StartDate], [StartTime], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TypeId], [AdminModeId], [Remarks], [ContinueDrugDuringAdmission], [CompanyID], [GenericID], [SpecialInstruction], [PRN_Reason], [PRN_MaxFreq], [IFNeeded], [Strength], [ClinicalPharmacy_Accept], [Cannula], [TemplateId], [MainSolution], [Volume], [DosageBaseUnit], [RegularContinous], [InfusionRate], [InfusionRatePeriod], [AdministrationType], [Status], @TenantId
FROM [SunCity_Clinics].[OutPatient].[DrugsWithInfusionRateDetails];
SET IDENTITY_INSERT [OutPatient].[DrugsWithInfusionRateDetails] OFF;
GO

PRINT 'Migrating [OutPatient].[DrugsWithInfusionRateDetails_StatusHistory]...';
SET IDENTITY_INSERT [OutPatient].[DrugsWithInfusionRateDetails_StatusHistory] ON;
INSERT INTO [OutPatient].[DrugsWithInfusionRateDetails_StatusHistory] ([Id], [DrugsWithInfusionRateDetailsID], [StatusID], [Note], [CreatedBy], [CreatedDate], [TenantId])
SELECT [Id], [DrugsWithInfusionRateDetailsID], [StatusID], [Note], [CreatedBy], [CreatedDate], @TenantId
FROM [SunCity_Clinics].[OutPatient].[DrugsWithInfusionRateDetails_StatusHistory];
SET IDENTITY_INSERT [OutPatient].[DrugsWithInfusionRateDetails_StatusHistory] OFF;
GO

PRINT 'Migrating [OutPatient].[DrugsWithInfusionRateHeader]...';
SET IDENTITY_INSERT [OutPatient].[DrugsWithInfusionRateHeader] ON;
INSERT INTO [OutPatient].[DrugsWithInfusionRateHeader] ([Id], [PatientID], [PrescriptionDate], [PrescriptionNo], [DoctorID], [OPNumber], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [AlertName], [CompanyID], [Status], [Comorbidities], [TenantId])
SELECT [Id], [PatientID], [PrescriptionDate], [PrescriptionNo], [DoctorID], [OPNumber], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [AlertName], [CompanyID], [Status], [Comorbidities], @TenantId
FROM [SunCity_Clinics].[OutPatient].[DrugsWithInfusionRateHeader];
SET IDENTITY_INSERT [OutPatient].[DrugsWithInfusionRateHeader] OFF;
GO

PRINT 'Migrating [OutPatient].[GeneralSetup]...';
SET IDENTITY_INSERT [OutPatient].[GeneralSetup] ON;
INSERT INTO [OutPatient].[GeneralSetup] ([ID], [NumOfDays], [StartTime], [EndTime], [NumOfFollowUp], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [NumOfDays], [StartTime], [EndTime], [NumOfFollowUp], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[GeneralSetup];
SET IDENTITY_INSERT [OutPatient].[GeneralSetup] OFF;
GO

PRINT 'Migrating [OutPatient].[ICDCode]...';
SET IDENTITY_INSERT [OutPatient].[ICDCode] ON;
INSERT INTO [OutPatient].[ICDCode] ([Id], [PatientID], [ISInPatient], [ISOutPatient], [OPNO], [OPDate], [ICDCode], [ICDDesc], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [ISInPatient], [ISOutPatient], [OPNO], [OPDate], [ICDCode], [ICDDesc], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[ICDCode];
SET IDENTITY_INSERT [OutPatient].[ICDCode] OFF;
GO

PRINT 'Migrating [OutPatient].[InfusionRate_Order]...';
SET IDENTITY_INSERT [OutPatient].[InfusionRate_Order] ON;
INSERT INTO [OutPatient].[InfusionRate_Order] ([ID], [PatientID], [IPNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [DoctorID], [TenantId])
SELECT [ID], [PatientID], [IPNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [DoctorID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[InfusionRate_Order];
SET IDENTITY_INSERT [OutPatient].[InfusionRate_Order] OFF;
GO

PRINT 'Migrating [OutPatient].[InvestigationRequestDetails]...';
SET IDENTITY_INSERT [OutPatient].[InvestigationRequestDetails] ON;
INSERT INTO [OutPatient].[InvestigationRequestDetails] ([Id], [InvestigationRequestId], [QTY], [Amount], [Priority], [ServiceId], [IsApproved], [ApprovalId], [Isoverride], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [PatientOrderDetailsID], [StartDate], [EndDate], [StartNurseID], [EndNurseID], [Done], [Cancelled], [RayLocation], [IsNoDeposit], [PaymentType], [Status], [CancelledBy], [CancelledReason], [Comments], [BranchId], [insuranceId], [prescDtlID], [toothNo], [opticaltype], [TenantId])
SELECT [Id], [InvestigationRequestId], [QTY], [Amount], [Priority], [ServiceId], [IsApproved], [ApprovalId], [Isoverride], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [PatientOrderDetailsID], [StartDate], [EndDate], [StartNurseID], [EndNurseID], [Done], [Cancelled], [RayLocation], [IsNoDeposit], [PaymentType], [Status], [CancelledBy], [CancelledReason], [Comments], [BranchId], [insuranceId], [prescDtlID], [toothNo], [opticaltype], @TenantId
FROM [SunCity_Clinics].[OutPatient].[InvestigationRequestDetails];
SET IDENTITY_INSERT [OutPatient].[InvestigationRequestDetails] OFF;
GO

PRINT 'Migrating [OutPatient].[InvestigationRequestHeader]...';
SET IDENTITY_INSERT [OutPatient].[InvestigationRequestHeader] ON;
INSERT INTO [OutPatient].[InvestigationRequestHeader] ([Id], [PatientID], [Type], [DoctorID], [TotalAmount], [OP_IPNumber], [WardID], [IsPregnant], [PregnantWeeks], [ClinicalDetails], [ReqDate], [ReqStatus], [SurgeryID], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [RequestStatus], [CompanyID], [PatientOrderMasterPriority], [BranchId], [selfmotivated], [referraltype], [organizationdoctor], [referredDoctor], [TenantId])
SELECT [Id], [PatientID], [Type], [DoctorID], [TotalAmount], [OP_IPNumber], [WardID], [IsPregnant], [PregnantWeeks], [ClinicalDetails], [ReqDate], [ReqStatus], [SurgeryID], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [RequestStatus], [CompanyID], [PatientOrderMasterPriority], [BranchId], [selfmotivated], [referraltype], [organizationdoctor], [referredDoctor], @TenantId
FROM [SunCity_Clinics].[OutPatient].[InvestigationRequestHeader];
SET IDENTITY_INSERT [OutPatient].[InvestigationRequestHeader] OFF;
GO

PRINT 'Migrating [OutPatient].[ManualGroupTransfer]...';
SET IDENTITY_INSERT [OutPatient].[ManualGroupTransfer] ON;
INSERT INTO [OutPatient].[ManualGroupTransfer] ([ID], [FromClinicID], [FromDocID], [FromSchedualeDate], [PatientID], [FromTime], [ToClinicID], [ToDocID], [ToSchedualeDate], [ToTime], [CreatedBy], [CreatedDate], [TenantId])
SELECT [ID], [FromClinicID], [FromDocID], [FromSchedualeDate], [PatientID], [FromTime], [ToClinicID], [ToDocID], [ToSchedualeDate], [ToTime], [CreatedBy], [CreatedDate], @TenantId
FROM [SunCity_Clinics].[OutPatient].[ManualGroupTransfer];
SET IDENTITY_INSERT [OutPatient].[ManualGroupTransfer] OFF;
GO

PRINT 'Migrating [OutPatient].[MedicalConsumablesDetails]...';
SET IDENTITY_INSERT [OutPatient].[MedicalConsumablesDetails] ON;
INSERT INTO [OutPatient].[MedicalConsumablesDetails] ([Id], [PatientID], [MedicalConsumablesID], [ItemID], [UniteID], [Quantity], [Amount], [ISIncluded], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [SubstoreID], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [MedicalConsumablesID], [ItemID], [UniteID], [Quantity], [Amount], [ISIncluded], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [SubstoreID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[MedicalConsumablesDetails];
SET IDENTITY_INSERT [OutPatient].[MedicalConsumablesDetails] OFF;
GO

PRINT 'Migrating [OutPatient].[MedicalConsumablesHeader]...';
SET IDENTITY_INSERT [OutPatient].[MedicalConsumablesHeader] ON;
INSERT INTO [OutPatient].[MedicalConsumablesHeader] ([Id], [PatientID], [PatientType], [DoctorID], [TotalAmount], [OPNumber], [IPNumber], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [RequestNO], [RequestStatus], [CompanyID], [BranchId], [TenantId])
SELECT [Id], [PatientID], [PatientType], [DoctorID], [TotalAmount], [OPNumber], [IPNumber], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [RequestNO], [RequestStatus], [CompanyID], [BranchId], @TenantId
FROM [SunCity_Clinics].[OutPatient].[MedicalConsumablesHeader];
SET IDENTITY_INSERT [OutPatient].[MedicalConsumablesHeader] OFF;
GO

PRINT 'Migrating [OutPatient].[NationalVacation]...';
SET IDENTITY_INSERT [OutPatient].[NationalVacation] ON;
INSERT INTO [OutPatient].[NationalVacation] ([Id], [Code], [Name], [StartDate], [EndDate], [CompanyID], [TenantId])
SELECT [Id], [Code], [Name], [StartDate], [EndDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[NationalVacation];
SET IDENTITY_INSERT [OutPatient].[NationalVacation] OFF;
GO

PRINT 'Migrating [OutPatient].[OPDInternalTransfer]...';
SET IDENTITY_INSERT [OutPatient].[OPDInternalTransfer] ON;
INSERT INTO [OutPatient].[OPDInternalTransfer] ([Id], [PatientID], [FromDoctorID], [ToDoctorID], [OldPrepareVisitSlipID], [NewPrepareVisitSlipID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [Id], [PatientID], [FromDoctorID], [ToDoctorID], [OldPrepareVisitSlipID], [NewPrepareVisitSlipID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[OutPatient].[OPDInternalTransfer];
SET IDENTITY_INSERT [OutPatient].[OPDInternalTransfer] OFF;
GO

PRINT 'Migrating [OutPatient].[PackagesRequestDetails]...';
SET IDENTITY_INSERT [OutPatient].[PackagesRequestDetails] ON;
INSERT INTO [OutPatient].[PackagesRequestDetails] ([Id], [InvestigationRequestID], [PatientID], [Type], [packagesID], [Amount], [Comment], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [InvestigationRequestID], [PatientID], [Type], [packagesID], [Amount], [Comment], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[PackagesRequestDetails];
SET IDENTITY_INSERT [OutPatient].[PackagesRequestDetails] OFF;
GO

PRINT 'Migrating [OutPatient].[PatientAllergyUpdates]...';
SET IDENTITY_INSERT [OutPatient].[PatientAllergyUpdates] ON;
INSERT INTO [OutPatient].[PatientAllergyUpdates] ([Id], [IsAllergy], [AllergyDesc], [Category], [PatientID], [CreatorName], [CreateDate], [AllergyDetailsID], [CompanyID], [TenantId])
SELECT [Id], [IsAllergy], [AllergyDesc], [Category], [PatientID], [CreatorName], [CreateDate], [AllergyDetailsID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[PatientAllergyUpdates];
SET IDENTITY_INSERT [OutPatient].[PatientAllergyUpdates] OFF;
GO

PRINT 'Migrating [OutPatient].[PatientAllergy]...';
SET IDENTITY_INSERT [OutPatient].[PatientAllergy] ON;
INSERT INTO [OutPatient].[PatientAllergy] ([ID], [PatientId], [AllergyDetailID], [TypeId], [CategoryId], [ReactionId], [ReactionType], [SeverityId], [SourceId], [Comment], [Createby], [CreateDate], [CompanyID], [TenantId])
SELECT [ID], [PatientId], [AllergyDetailID], [TypeId], [CategoryId], [ReactionId], [ReactionType], [SeverityId], [SourceId], [Comment], [Createby], [CreateDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[PatientAllergy];
SET IDENTITY_INSERT [OutPatient].[PatientAllergy] OFF;
GO

PRINT 'Migrating [OutPatient].[PatientVitals]...';
SET IDENTITY_INSERT [OutPatient].[PatientVitals] ON;
INSERT INTO [OutPatient].[PatientVitals] ([Id], [OP_IP], [ReadingDate], [StartDate], [PeroidType], [Frequency], [MonitoredDays], [StartTime], [VitalParametersDeptWiseId], [PateintId], [Value], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [OP_IP], [ReadingDate], [StartDate], [PeroidType], [Frequency], [MonitoredDays], [StartTime], [VitalParametersDeptWiseId], [PateintId], [Value], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[PatientVitals];
SET IDENTITY_INSERT [OutPatient].[PatientVitals] OFF;
GO

PRINT 'Migrating [OutPatient].[PayPolicy]...';
SET IDENTITY_INSERT [OutPatient].[PayPolicy] ON;
INSERT INTO [OutPatient].[PayPolicy] ([ID], [Code], [NameArabic], [NameEnglish], [CompanyID], [TenantId])
SELECT [ID], [Code], [NameArabic], [NameEnglish], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[PayPolicy];
SET IDENTITY_INSERT [OutPatient].[PayPolicy] OFF;
GO

PRINT 'Migrating [OutPatient].[PrepareVisitSlip]...';
SET IDENTITY_INSERT [OutPatient].[PrepareVisitSlip] ON;
INSERT INTO [OutPatient].[PrepareVisitSlip] ([Id], [PatientID], [VisitType], [ScheduleAppointmentID], [DepartmentID], [DoctorsID], [SessionID], [Time], [TimeOfVisit], [referralDepartmentID], [referralClinicID], [referralDoctorsID], [MedicoLegalID], [VisitStatus], [OPnumber], [VisitNo], [Remarks], [NoCharge], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Purpose], [OPNumberStatus], [CompanyID], [IsApproved], [VisitSlipStatus], [CancelReason], [referralDoctorsPerc], [EndTreatmentCycle], [IsOnlinePayment], [OvarTimeOnline], [BranchId], [IsConfirmthetermsandconditions], [IsVirtual], [invDtl], [VisitReason], [Encounterstatus], [serviceType], [careteamRole], [TenantId])
SELECT [Id], [PatientID], [VisitType], [ScheduleAppointmentID], [DepartmentID], [DoctorsID], [SessionID], [Time], [TimeOfVisit], [referralDepartmentID], [referralClinicID], [referralDoctorsID], [MedicoLegalID], [VisitStatus], [OPnumber], [VisitNo], [Remarks], [NoCharge], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Purpose], [OPNumberStatus], [CompanyID], [IsApproved], [VisitSlipStatus], [CancelReason], [referralDoctorsPerc], [EndTreatmentCycle], [IsOnlinePayment], [OvarTimeOnline], [BranchId], [IsConfirmthetermsandconditions], [IsVirtual], [invDtl], [VisitReason], [Encounterstatus], [serviceType], [careteamRole], @TenantId
FROM [SunCity_Clinics].[OutPatient].[PrepareVisitSlip];
SET IDENTITY_INSERT [OutPatient].[PrepareVisitSlip] OFF;
GO

PRINT 'Migrating [OutPatient].[PrescriptionsDetails]...';
SET IDENTITY_INSERT [OutPatient].[PrescriptionsDetails] ON;
INSERT INTO [OutPatient].[PrescriptionsDetails] ([Id], [PrescriptionID], [PrescriptionNo], [PatientID], [DoctorID], [DrugID], [DosageUnitID], [Dosage], [FrequencyID], [Period], [DoseQty], [StartDate], [StartTime], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TypeId], [AdminModeId], [Remarks], [ContinueDrugDuringAdmission], [CompanyID], [GenericID], [SpecialInstruction], [PRN_Reason], [PRN_MaxFreq], [IFNeeded], [Strength], [ClinicalPharmacy_Accept], [EndDate], [EndTime], [DiagnosisId], [Refill], [FrequencyofRefills], [FrequencyofRefillsType], [NumberofRefills], [ContinusRefill], [InsuranceId], [RouteId], [AdministrationSiteId], [Dispense_Allowed], [IsCancelled], [TenantId])
SELECT [Id], [PrescriptionID], [PrescriptionNo], [PatientID], [DoctorID], [DrugID], [DosageUnitID], [Dosage], [FrequencyID], [Period], [DoseQty], [StartDate], [StartTime], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TypeId], [AdminModeId], [Remarks], [ContinueDrugDuringAdmission], [CompanyID], [GenericID], [SpecialInstruction], [PRN_Reason], [PRN_MaxFreq], [IFNeeded], [Strength], [ClinicalPharmacy_Accept], [EndDate], [EndTime], [DiagnosisId], [Refill], [FrequencyofRefills], [FrequencyofRefillsType], [NumberofRefills], [ContinusRefill], [InsuranceId], [RouteId], [AdministrationSiteId], [Dispense_Allowed], [IsCancelled], @TenantId
FROM [SunCity_Clinics].[OutPatient].[PrescriptionsDetails];
SET IDENTITY_INSERT [OutPatient].[PrescriptionsDetails] OFF;
GO

PRINT 'Migrating [OutPatient].[PrescriptionsHeader]...';
SET IDENTITY_INSERT [OutPatient].[PrescriptionsHeader] ON;
INSERT INTO [OutPatient].[PrescriptionsHeader] ([Id], [PatientID], [PrescriptionDate], [PrescriptionNo], [DoctorID], [OPNumber], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [AlertName], [CompanyID], [Status], [Comorbidities], [isStop], [TenantId])
SELECT [Id], [PatientID], [PrescriptionDate], [PrescriptionNo], [DoctorID], [OPNumber], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [AlertName], [CompanyID], [Status], [Comorbidities], [isStop], @TenantId
FROM [SunCity_Clinics].[OutPatient].[PrescriptionsHeader];
SET IDENTITY_INSERT [OutPatient].[PrescriptionsHeader] OFF;
GO

PRINT 'Migrating [OutPatient].[ProceduresDetails]...';
SET IDENTITY_INSERT [OutPatient].[ProceduresDetails] ON;
INSERT INTO [OutPatient].[ProceduresDetails] ([ID], [Code], [NameArabic], [NameEnglish], [ProceduresMasterID], [CreatedBy], [CreatedDate], [ModifiedBy], [ModificationDate], [TenantId])
SELECT [ID], [Code], [NameArabic], [NameEnglish], [ProceduresMasterID], [CreatedBy], [CreatedDate], [ModifiedBy], [ModificationDate], @TenantId
FROM [SunCity_Clinics].[OutPatient].[ProceduresDetails];
SET IDENTITY_INSERT [OutPatient].[ProceduresDetails] OFF;
GO

PRINT 'Migrating [OutPatient].[ProceduresMaster]...';
SET IDENTITY_INSERT [OutPatient].[ProceduresMaster] ON;
INSERT INTO [OutPatient].[ProceduresMaster] ([ID], [Code], [NameArabic], [NameEnglish], [CompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [ID], [Code], [NameArabic], [NameEnglish], [CompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[OutPatient].[ProceduresMaster];
SET IDENTITY_INSERT [OutPatient].[ProceduresMaster] OFF;
GO

PRINT 'Migrating [OutPatient].[ReFillPrescriptions]...';
SET IDENTITY_INSERT [OutPatient].[ReFillPrescriptions] ON;
INSERT INTO [OutPatient].[ReFillPrescriptions] ([Id], [FrequencyofRefills], [FrequencyofRefillsType], [NumberofRefills], [PrescriptionsDetailsId], [ContinusRefill], [TenantId])
SELECT [Id], [FrequencyofRefills], [FrequencyofRefillsType], [NumberofRefills], [PrescriptionsDetailsId], [ContinusRefill], @TenantId
FROM [SunCity_Clinics].[OutPatient].[ReFillPrescriptions];
SET IDENTITY_INSERT [OutPatient].[ReFillPrescriptions] OFF;
GO

PRINT 'Migrating [OutPatient].[ScheduleAppointment]...';
SET IDENTITY_INSERT [OutPatient].[ScheduleAppointment] ON;
INSERT INTO [OutPatient].[ScheduleAppointment] ([Id], [PatientID], [DepartmentID], [DoctorsID], [SessionID], [Time], [AppointmentDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [DepartmentID], [DoctorsID], [SessionID], [Time], [AppointmentDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[ScheduleAppointment];
SET IDENTITY_INSERT [OutPatient].[ScheduleAppointment] OFF;
GO

PRINT 'Migrating [OutPatient].[ServicesRequestDetails]...';
SET IDENTITY_INSERT [OutPatient].[ServicesRequestDetails] ON;
INSERT INTO [OutPatient].[ServicesRequestDetails] ([Id], [InvestigationRequestID], [PatientID], [Type], [ServicesID], [Amount], [Comment], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [InvestigationRequestID], [PatientID], [Type], [ServicesID], [Amount], [Comment], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[ServicesRequestDetails];
SET IDENTITY_INSERT [OutPatient].[ServicesRequestDetails] OFF;
GO

PRINT 'Migrating [OutPatient].[SpecialityGroupDetails]...';
SET IDENTITY_INSERT [OutPatient].[SpecialityGroupDetails] ON;
INSERT INTO [OutPatient].[SpecialityGroupDetails] ([ID], [SpecialityMasterID], [Code], [Name], [CreatedBy], [Creationdate], [ModifiedBy], [ModificationDate], [NameAr], [AssessmentSpecialist], [TenantId])
SELECT [ID], [SpecialityMasterID], [Code], [Name], [CreatedBy], [Creationdate], [ModifiedBy], [ModificationDate], [NameAr], [AssessmentSpecialist], @TenantId
FROM [SunCity_Clinics].[OutPatient].[SpecialityGroupDetails];
SET IDENTITY_INSERT [OutPatient].[SpecialityGroupDetails] OFF;
GO

PRINT 'Migrating [OutPatient].[SpecialityGroupMaster]...';
SET IDENTITY_INSERT [OutPatient].[SpecialityGroupMaster] ON;
INSERT INTO [OutPatient].[SpecialityGroupMaster] ([ID], [Code], [Name], [CreatedBy], [Creationdate], [ModifiedBy], [ModificationDate], [NameAr], [NameRu], [CompanyID], [BranchId], [TenantId])
SELECT [ID], [Code], [Name], [CreatedBy], [Creationdate], [ModifiedBy], [ModificationDate], [NameAr], [NameRu], [CompanyID], [BranchId], @TenantId
FROM [SunCity_Clinics].[OutPatient].[SpecialityGroupMaster];
SET IDENTITY_INSERT [OutPatient].[SpecialityGroupMaster] OFF;
GO

PRINT 'Migrating [OutPatient].[SponsorshipConditionCostCenters]...';
SET IDENTITY_INSERT [OutPatient].[SponsorshipConditionCostCenters] ON;
INSERT INTO [OutPatient].[SponsorshipConditionCostCenters] ([Id], [SponsorID], [EffectiveDate], [EffectiveToDate], [Discountpercentage], [CoverageLimit], [CoverageTypeID], [Deductiblepercentage], [CostCenterID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [SponsorID], [EffectiveDate], [EffectiveToDate], [Discountpercentage], [CoverageLimit], [CoverageTypeID], [Deductiblepercentage], [CostCenterID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[SponsorshipConditionCostCenters];
SET IDENTITY_INSERT [OutPatient].[SponsorshipConditionCostCenters] OFF;
GO

PRINT 'Migrating [OutPatient].[SponsorshipConditionDrugTypes]...';
SET IDENTITY_INSERT [OutPatient].[SponsorshipConditionDrugTypes] ON;
INSERT INTO [OutPatient].[SponsorshipConditionDrugTypes] ([Id], [SponsorID], [EffectiveDate], [EffectiveToDate], [DrugTypeID], [Discountpercentage], [DiscountAmount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [SponsorID], [EffectiveDate], [EffectiveToDate], [DrugTypeID], [Discountpercentage], [DiscountAmount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[SponsorshipConditionDrugTypes];
SET IDENTITY_INSERT [OutPatient].[SponsorshipConditionDrugTypes] OFF;
GO

PRINT 'Migrating [OutPatient].[SponsorshipConditionDrugs]...';
SET IDENTITY_INSERT [OutPatient].[SponsorshipConditionDrugs] ON;
INSERT INTO [OutPatient].[SponsorshipConditionDrugs] ([Id], [SponsorID], [EffectiveDate], [EffectiveToDate], [DrugID], [standardAmount], [SupplierItemID], [Discountpercentage], [DiscountAmount], [NetAmount], [ISApproval], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [SponsorID], [EffectiveDate], [EffectiveToDate], [DrugID], [standardAmount], [SupplierItemID], [Discountpercentage], [DiscountAmount], [NetAmount], [ISApproval], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[SponsorshipConditionDrugs];
SET IDENTITY_INSERT [OutPatient].[SponsorshipConditionDrugs] OFF;
GO

PRINT 'Migrating [OutPatient].[SponsorshipConditionServices]...';
SET IDENTITY_INSERT [OutPatient].[SponsorshipConditionServices] ON;
INSERT INTO [OutPatient].[SponsorshipConditionServices] ([Id], [SponsorID], [EffectiveDate], [EffectiveToDate], [standardAmount], [Discountpercentage], [NetAmount], [DiscountAmount], [Deductiblepercentage], [DeductibleAmount], [ISApproval], [ServicesID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [SponsorID], [EffectiveDate], [EffectiveToDate], [standardAmount], [Discountpercentage], [NetAmount], [DiscountAmount], [Deductiblepercentage], [DeductibleAmount], [ISApproval], [ServicesID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[SponsorshipConditionServices];
SET IDENTITY_INSERT [OutPatient].[SponsorshipConditionServices] OFF;
GO

PRINT 'Migrating [OutPatient].[SponsorshipConditionSupplierGroupsItems]...';
SET IDENTITY_INSERT [OutPatient].[SponsorshipConditionSupplierGroupsItems] ON;
INSERT INTO [OutPatient].[SponsorshipConditionSupplierGroupsItems] ([Id], [SponsorID], [EffectiveDate], [EffectiveToDate], [standardAmount], [SupplierItemID], [Discountpercentage], [DiscountAmount], [NetAmount], [ISApproval], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [SponsorID], [EffectiveDate], [EffectiveToDate], [standardAmount], [SupplierItemID], [Discountpercentage], [DiscountAmount], [NetAmount], [ISApproval], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[SponsorshipConditionSupplierGroupsItems];
SET IDENTITY_INSERT [OutPatient].[SponsorshipConditionSupplierGroupsItems] OFF;
GO

PRINT 'Migrating [OutPatient].[SponsorshipConditionSupplierGroups]...';
SET IDENTITY_INSERT [OutPatient].[SponsorshipConditionSupplierGroups] ON;
INSERT INTO [OutPatient].[SponsorshipConditionSupplierGroups] ([Id], [SponsorID], [EffectiveDate], [EffectiveToDate], [SupplierGroupID], [Discountpercentage], [DiscountAmount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [SponsorID], [EffectiveDate], [EffectiveToDate], [SupplierGroupID], [Discountpercentage], [DiscountAmount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[SponsorshipConditionSupplierGroups];
SET IDENTITY_INSERT [OutPatient].[SponsorshipConditionSupplierGroups] OFF;
GO

PRINT 'Migrating [OutPatient].[SponsorshipCondition]...';
SET IDENTITY_INSERT [OutPatient].[SponsorshipCondition] ON;
INSERT INTO [OutPatient].[SponsorshipCondition] ([Id], [SponsorID], [CategoryID], [CarrierID], [CoverageLimit], [PerEpisode], [PerAnnum], [InpatientTreatment], [OutpatientTreatment], [ContractNumber], [ContractDate], [EffectiveDate], [EffectiveToDate], [Co_paymentamount], [IsPercentage], [IsAmount], [IsNet], [IsGross], [IsBeforeDeductible], [IsAfterDeductible], [IsVerifyPolicyNumber], [IsAutoGenratePolicyNumber], [IsImmediatesettlement], [IsCreditsettlement], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [SponsorID], [CategoryID], [CarrierID], [CoverageLimit], [PerEpisode], [PerAnnum], [InpatientTreatment], [OutpatientTreatment], [ContractNumber], [ContractDate], [EffectiveDate], [EffectiveToDate], [Co_paymentamount], [IsPercentage], [IsAmount], [IsNet], [IsGross], [IsBeforeDeductible], [IsAfterDeductible], [IsVerifyPolicyNumber], [IsAutoGenratePolicyNumber], [IsImmediatesettlement], [IsCreditsettlement], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[SponsorshipCondition];
SET IDENTITY_INSERT [OutPatient].[SponsorshipCondition] OFF;
GO

PRINT 'Migrating [OutPatient].[Vac-Clinic]...';
INSERT INTO [OutPatient].[Vac-Clinic] ([VacationId], [ClinicId], [Id], [TenantId])
SELECT [VacationId], [ClinicId], [Id], @TenantId
FROM [SunCity_Clinics].[OutPatient].[Vac-Clinic];
GO

PRINT 'Migrating [OutPatient].[Vac_Clinic]...';
INSERT INTO [OutPatient].[Vac_Clinic] ([Id], [VacationId], [ClinicId], [TenantId])
SELECT [Id], [VacationId], [ClinicId], @TenantId
FROM [SunCity_Clinics].[OutPatient].[Vac_Clinic];
GO

PRINT 'Migrating [OutPatient].[VaccinationChart]...';
SET IDENTITY_INSERT [OutPatient].[VaccinationChart] ON;
INSERT INTO [OutPatient].[VaccinationChart] ([Id], [PatientID], [OPNumber], [vaccinationID], [Doses], [DosageNo], [DoctorId], [year], [Jan], [Feb], [Mar], [Apr], [May], [June], [July], [Aug], [Sep], [Oct], [Nov], [Dec], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [OPNumber], [vaccinationID], [Doses], [DosageNo], [DoctorId], [year], [Jan], [Feb], [Mar], [Apr], [May], [June], [July], [Aug], [Sep], [Oct], [Nov], [Dec], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[VaccinationChart];
SET IDENTITY_INSERT [OutPatient].[VaccinationChart] OFF;
GO

PRINT 'Migrating [OutPatient].[VaccinationDetails]...';
SET IDENTITY_INSERT [OutPatient].[VaccinationDetails] ON;
INSERT INTO [OutPatient].[VaccinationDetails] ([Id], [VaccinationID], [DosageNo], [MinMonth], [MaxMonth], [ServiceID], [Rate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [VaccinationID], [DosageNo], [MinMonth], [MaxMonth], [ServiceID], [Rate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[VaccinationDetails];
SET IDENTITY_INSERT [OutPatient].[VaccinationDetails] OFF;
GO

PRINT 'Migrating [OutPatient].[VaccinationMaster]...';
SET IDENTITY_INSERT [OutPatient].[VaccinationMaster] ON;
INSERT INTO [OutPatient].[VaccinationMaster] ([Id], [VaccinationName], [NODoses], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [VaccinationTime], [CompanyID], [ISHIbB], [VaccinationNameAr], [TenantId])
SELECT [Id], [VaccinationName], [NODoses], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [VaccinationTime], [CompanyID], [ISHIbB], [VaccinationNameAr], @TenantId
FROM [SunCity_Clinics].[OutPatient].[VaccinationMaster];
SET IDENTITY_INSERT [OutPatient].[VaccinationMaster] OFF;
GO

PRINT 'Migrating [OutPatient].[VaccinationSchedule]...';
SET IDENTITY_INSERT [OutPatient].[VaccinationSchedule] ON;
INSERT INTO [OutPatient].[VaccinationSchedule] ([Id], [PatientID], [ScheduleDate], [vaccinationID], [FrequencyID], [StartDate], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [ScheduleDate], [vaccinationID], [FrequencyID], [StartDate], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[VaccinationSchedule];
SET IDENTITY_INSERT [OutPatient].[VaccinationSchedule] OFF;
GO

PRINT 'Migrating [OutPatient].[VitalParametersDeptWise]...';
SET IDENTITY_INSERT [OutPatient].[VitalParametersDeptWise] ON;
INSERT INTO [OutPatient].[VitalParametersDeptWise] ([Id], [DepartmentID], [vitalparameterID], [ISCompulsory], [Frequency], [FrequencyType], [MinValue], [MaxValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [DepartmentID], [vitalparameterID], [ISCompulsory], [Frequency], [FrequencyType], [MinValue], [MaxValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[OutPatient].[VitalParametersDeptWise];
SET IDENTITY_INSERT [OutPatient].[VitalParametersDeptWise] OFF;
GO

PRINT 'Migrating [PFEducation].[EducationalFiles]...';
SET IDENTITY_INSERT [PFEducation].[EducationalFiles] ON;
INSERT INTO [PFEducation].[EducationalFiles] ([FileNameAr], [FileNameEn], [Filepath], [CreateDate], [CreateBy], [ID], [TenantId])
SELECT [FileNameAr], [FileNameEn], [Filepath], [CreateDate], [CreateBy], [ID], @TenantId
FROM [SunCity_Clinics].[PFEducation].[EducationalFiles];
SET IDENTITY_INSERT [PFEducation].[EducationalFiles] OFF;
GO

PRINT 'Migrating [PFEducation].[InstructionComments]...';
SET IDENTITY_INSERT [PFEducation].[InstructionComments] ON;
INSERT INTO [PFEducation].[InstructionComments] ([Id], [InstructionListId], [Comment], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [Id], [InstructionListId], [Comment], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[PFEducation].[InstructionComments];
SET IDENTITY_INSERT [PFEducation].[InstructionComments] OFF;
GO

PRINT 'Migrating [PFEducation].[InstructionList]...';
SET IDENTITY_INSERT [PFEducation].[InstructionList] ON;
INSERT INTO [PFEducation].[InstructionList] ([ID], [NameAr], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [NameAr], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[PFEducation].[InstructionList];
SET IDENTITY_INSERT [PFEducation].[InstructionList] OFF;
GO

PRINT 'Migrating [PFEducation].[Need_NeedExecution]...';
SET IDENTITY_INSERT [PFEducation].[Need_NeedExecution] ON;
INSERT INTO [PFEducation].[Need_NeedExecution] ([ID], [NeedID], [NeedExecutionID], [TenantId])
SELECT [ID], [NeedID], [NeedExecutionID], @TenantId
FROM [SunCity_Clinics].[PFEducation].[Need_NeedExecution];
SET IDENTITY_INSERT [PFEducation].[Need_NeedExecution] OFF;
GO

PRINT 'Migrating [PFEducation].[NeedsExecution]...';
INSERT INTO [PFEducation].[NeedsExecution] ([ID], [ENeedEn], [ENeedAr], [TenantId])
SELECT [ID], [ENeedEn], [ENeedAr], @TenantId
FROM [SunCity_Clinics].[PFEducation].[NeedsExecution];
GO

PRINT 'Migrating [PFEducation].[Needs]...';
INSERT INTO [PFEducation].[Needs] ([ID], [NeedEn], [NeedAr], [Mandatory], [DefualtCheck], [TenantId])
SELECT [ID], [NeedEn], [NeedAr], [Mandatory], [DefualtCheck], @TenantId
FROM [SunCity_Clinics].[PFEducation].[Needs];
GO

PRINT 'Migrating [PFEducation].[PFEBarriersReduce]...';
SET IDENTITY_INSERT [PFEducation].[PFEBarriersReduce] ON;
INSERT INTO [PFEducation].[PFEBarriersReduce] ([ID], [PFE_ID], [BarReduceID], [RValue], [TenantId])
SELECT [ID], [PFE_ID], [BarReduceID], [RValue], @TenantId
FROM [SunCity_Clinics].[PFEducation].[PFEBarriersReduce];
SET IDENTITY_INSERT [PFEducation].[PFEBarriersReduce] OFF;
GO

PRINT 'Migrating [PFEducation].[PFEBarriers]...';
SET IDENTITY_INSERT [PFEducation].[PFEBarriers] ON;
INSERT INTO [PFEducation].[PFEBarriers] ([ID], [PFE_ID], [BarrierID], [BValue], [TenantId])
SELECT [ID], [PFE_ID], [BarrierID], [BValue], @TenantId
FROM [SunCity_Clinics].[PFEducation].[PFEBarriers];
SET IDENTITY_INSERT [PFEducation].[PFEBarriers] OFF;
GO

PRINT 'Migrating [PFEducation].[PFEDiagnosis]...';
SET IDENTITY_INSERT [PFEducation].[PFEDiagnosis] ON;
INSERT INTO [PFEducation].[PFEDiagnosis] ([ID], [PFE_ID], [DiagnosisID], [BValue], [TenantId])
SELECT [ID], [PFE_ID], [DiagnosisID], [BValue], @TenantId
FROM [SunCity_Clinics].[PFEducation].[PFEDiagnosis];
SET IDENTITY_INSERT [PFEducation].[PFEDiagnosis] OFF;
GO

PRINT 'Migrating [PFEducation].[PFEGroupExecution]...';
SET IDENTITY_INSERT [PFEducation].[PFEGroupExecution] ON;
INSERT INTO [PFEducation].[PFEGroupExecution] ([ID], [grp_ID], [ENeedID], [CreateBy], [CreateDate], [TenantId])
SELECT [ID], [grp_ID], [ENeedID], [CreateBy], [CreateDate], @TenantId
FROM [SunCity_Clinics].[PFEducation].[PFEGroupExecution];
SET IDENTITY_INSERT [PFEducation].[PFEGroupExecution] OFF;
GO

PRINT 'Migrating [PFEducation].[PFENeedsExecution]...';
SET IDENTITY_INSERT [PFEducation].[PFENeedsExecution] ON;
INSERT INTO [PFEducation].[PFENeedsExecution] ([ID], [PFE_ID], [ENeedID], [Understand_ENeedValue], [Repeate_ENeedValue], [CreateBy], [CreateDate], [GrpID], [CompanyID], [TeachingMethod], [OtherSpecialty], [TenantId])
SELECT [ID], [PFE_ID], [ENeedID], [Understand_ENeedValue], [Repeate_ENeedValue], [CreateBy], [CreateDate], [GrpID], [CompanyID], [TeachingMethod], [OtherSpecialty], @TenantId
FROM [SunCity_Clinics].[PFEducation].[PFENeedsExecution];
SET IDENTITY_INSERT [PFEducation].[PFENeedsExecution] OFF;
GO

PRINT 'Migrating [PFEducation].[PFENeeds]...';
SET IDENTITY_INSERT [PFEducation].[PFENeeds] ON;
INSERT INTO [PFEducation].[PFENeeds] ([ID], [PFE_ID], [NeedID], [NeedValue], [Mandatory], [CreateBy], [CreateDate], [OtherNeeds], [TenantId])
SELECT [ID], [PFE_ID], [NeedID], [NeedValue], [Mandatory], [CreateBy], [CreateDate], [OtherNeeds], @TenantId
FROM [SunCity_Clinics].[PFEducation].[PFENeeds];
SET IDENTITY_INSERT [PFEducation].[PFENeeds] OFF;
GO

PRINT 'Migrating [PFEducation].[PatientDocuments]...';
SET IDENTITY_INSERT [PFEducation].[PatientDocuments] ON;
INSERT INTO [PFEducation].[PatientDocuments] ([Id], [PatientId], [OPIP], [EducationalFileId], [DoctorId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientId], [OPIP], [EducationalFileId], [DoctorId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[PFEducation].[PatientDocuments];
SET IDENTITY_INSERT [PFEducation].[PatientDocuments] OFF;
GO

PRINT 'Migrating [PFEducation].[PatientEduFiles]...';
SET IDENTITY_INSERT [PFEducation].[PatientEduFiles] ON;
INSERT INTO [PFEducation].[PatientEduFiles] ([Id], [PatientId], [OPIPNumber], [FilePathID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [Id], [PatientId], [OPIPNumber], [FilePathID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[PFEducation].[PatientEduFiles];
SET IDENTITY_INSERT [PFEducation].[PatientEduFiles] OFF;
GO

PRINT 'Migrating [PFEducation].[PatientFamilyEducation]...';
SET IDENTITY_INSERT [PFEducation].[PatientFamilyEducation] ON;
INSERT INTO [PFEducation].[PatientFamilyEducation] ([Id], [PatientId], [DateofAssessment], [Time], [Dept], [Educationgivento], [Relationship], [LiteracyLevel], [Willingness], [PrimaryLanguage], [UnderstoodLanguage], [A_None], [A_Anxiety_Fear], [A_LanguageBarrier], [A_Denial], [A_Sensory_deﬁcit], [A_BeliefsandValues], [A_Literacy], [A_CulturalPractice], [A_PhysicalImpairment], [A_Pain_Discomfort], [A_Emotional], [A_Cognitive_impairment], [A_Lackofconﬁdence], [A_Financial_Problems], [A_Others], [A_Others_text], [I_None], [I_Obtaintranslator], [I_TeachFamily], [I_Respectvalues], [I_Review_Repeat], [I_Reassurance], [I_RespectCultural], [I_Appropritatesubstitution], [I_Others], [I_Others_text], [TM_lecture], [TM_Demonstration], [TM_Discussion], [TM_Audio], [TM_Model], [TM_Verbal], [OPIP], [CreateDate], [CreateBy], [CompanyID], [EducationgiventoV], [PrimaryLanguageV], [UnderstoodLanguageV], [TenantId])
SELECT [Id], [PatientId], [DateofAssessment], [Time], [Dept], [Educationgivento], [Relationship], [LiteracyLevel], [Willingness], [PrimaryLanguage], [UnderstoodLanguage], [A_None], [A_Anxiety_Fear], [A_LanguageBarrier], [A_Denial], [A_Sensory_deﬁcit], [A_BeliefsandValues], [A_Literacy], [A_CulturalPractice], [A_PhysicalImpairment], [A_Pain_Discomfort], [A_Emotional], [A_Cognitive_impairment], [A_Lackofconﬁdence], [A_Financial_Problems], [A_Others], [A_Others_text], [I_None], [I_Obtaintranslator], [I_TeachFamily], [I_Respectvalues], [I_Review_Repeat], [I_Reassurance], [I_RespectCultural], [I_Appropritatesubstitution], [I_Others], [I_Others_text], [TM_lecture], [TM_Demonstration], [TM_Discussion], [TM_Audio], [TM_Model], [TM_Verbal], [OPIP], [CreateDate], [CreateBy], [CompanyID], [EducationgiventoV], [PrimaryLanguageV], [UnderstoodLanguageV], @TenantId
FROM [SunCity_Clinics].[PFEducation].[PatientFamilyEducation];
SET IDENTITY_INSERT [PFEducation].[PatientFamilyEducation] OFF;
GO

PRINT 'Migrating [PFEducation].[PatientInstructionList]...';
SET IDENTITY_INSERT [PFEducation].[PatientInstructionList] ON;
INSERT INTO [PFEducation].[PatientInstructionList] ([Id], [PatientId], [InstructionListId], [Comment], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientId], [InstructionListId], [Comment], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[PFEducation].[PatientInstructionList];
SET IDENTITY_INSERT [PFEducation].[PatientInstructionList] OFF;
GO

PRINT 'Migrating [PatientPortal].[PatientComplaint]...';
SET IDENTITY_INSERT [PatientPortal].[PatientComplaint] ON;
INSERT INTO [PatientPortal].[PatientComplaint] ([Id], [PatientId], [ComplaintDate], [Complaintregarding], [TheNatureofComplaint], [ComplaintAgainst], [ComplaintDetails], [CreatedDate], [LastModifiedDate], [TenantId])
SELECT [Id], [PatientId], [ComplaintDate], [Complaintregarding], [TheNatureofComplaint], [ComplaintAgainst], [ComplaintDetails], [CreatedDate], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[PatientPortal].[PatientComplaint];
SET IDENTITY_INSERT [PatientPortal].[PatientComplaint] OFF;
GO

PRINT 'Migrating [Pharmacy].[Additives]...';
SET IDENTITY_INSERT [Pharmacy].[Additives] ON;
INSERT INTO [Pharmacy].[Additives] ([Id], [GenericId], [DrugId], [TemplateId], [TenantId])
SELECT [Id], [GenericId], [DrugId], [TemplateId], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[Additives];
SET IDENTITY_INSERT [Pharmacy].[Additives] OFF;
GO

PRINT 'Migrating [Pharmacy].[AdministrationSite]...';
SET IDENTITY_INSERT [Pharmacy].[AdministrationSite] ON;
INSERT INTO [Pharmacy].[AdministrationSite] ([Id], [Value], [Description], [TenantId])
SELECT [Id], [Value], [Description], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[AdministrationSite];
SET IDENTITY_INSERT [Pharmacy].[AdministrationSite] OFF;
GO

PRINT 'Migrating [Pharmacy].[AdminstrationMaster]...';
SET IDENTITY_INSERT [Pharmacy].[AdminstrationMaster] ON;
INSERT INTO [Pharmacy].[AdminstrationMaster] ([ID], [Code], [NameArabic], [NameEnglish], [Status], [CompanyID], [TenantId])
SELECT [ID], [Code], [NameArabic], [NameEnglish], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[AdminstrationMaster];
SET IDENTITY_INSERT [Pharmacy].[AdminstrationMaster] OFF;
GO

PRINT 'Migrating [Pharmacy].[Batches]...';
SET IDENTITY_INSERT [Pharmacy].[Batches] ON;
INSERT INTO [Pharmacy].[Batches] ([Id], [BatchNo], [GRNDetailsId], [ExpiryDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [LPODetailsId], [TenantId])
SELECT [Id], [BatchNo], [GRNDetailsId], [ExpiryDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [LPODetailsId], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[Batches];
SET IDENTITY_INSERT [Pharmacy].[Batches] OFF;
GO

PRINT 'Migrating [Pharmacy].[Borrowing]...';
SET IDENTITY_INSERT [Pharmacy].[Borrowing] ON;
INSERT INTO [Pharmacy].[Borrowing] ([ID], [NameEN], [NameAR], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [BranchId], [TenantId])
SELECT [ID], [NameEN], [NameAR], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [BranchId], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[Borrowing];
SET IDENTITY_INSERT [Pharmacy].[Borrowing] OFF;
GO

PRINT 'Migrating [Pharmacy].[Brands]...';
SET IDENTITY_INSERT [Pharmacy].[Brands] ON;
INSERT INTO [Pharmacy].[Brands] ([Id], [BrandName], [GenericNamesId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [BrandName], [GenericNamesId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[Brands];
SET IDENTITY_INSERT [Pharmacy].[Brands] OFF;
GO

PRINT 'Migrating [Pharmacy].[ContraDrugs]...';
SET IDENTITY_INSERT [Pharmacy].[ContraDrugs] ON;
INSERT INTO [Pharmacy].[ContraDrugs] ([ID], [DrugID], [ContraDrugID], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [DrugID], [ContraDrugID], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[ContraDrugs];
SET IDENTITY_INSERT [Pharmacy].[ContraDrugs] OFF;
GO

PRINT 'Migrating [Pharmacy].[DeliveryTerm]...';
SET IDENTITY_INSERT [Pharmacy].[DeliveryTerm] ON;
INSERT INTO [Pharmacy].[DeliveryTerm] ([ID], [DeliveryTermName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [DeliveryTermName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DeliveryTerm];
SET IDENTITY_INSERT [Pharmacy].[DeliveryTerm] OFF;
GO

PRINT 'Migrating [Pharmacy].[DespatchingDetails]...';
SET IDENTITY_INSERT [Pharmacy].[DespatchingDetails] ON;
INSERT INTO [Pharmacy].[DespatchingDetails] ([ID], [TransferRequesEntrytId], [SubstoreBatchId], [RecievedQTY], [AcceptedQTY], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [TransferRequesEntrytId], [SubstoreBatchId], [RecievedQTY], [AcceptedQTY], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DespatchingDetails];
SET IDENTITY_INSERT [Pharmacy].[DespatchingDetails] OFF;
GO

PRINT 'Migrating [Pharmacy].[DestroyingExpiryItemsDetails]...';
SET IDENTITY_INSERT [Pharmacy].[DestroyingExpiryItemsDetails] ON;
INSERT INTO [Pharmacy].[DestroyingExpiryItemsDetails] ([ID], [DestroyingExpiryItemsHeaderId], [ExpiryDate], [DrugID], [SubStoreBatchId], [DestroyQTY], [UnitPrice], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SetEntryCode], [TenantId])
SELECT [ID], [DestroyingExpiryItemsHeaderId], [ExpiryDate], [DrugID], [SubStoreBatchId], [DestroyQTY], [UnitPrice], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SetEntryCode], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DestroyingExpiryItemsDetails];
SET IDENTITY_INSERT [Pharmacy].[DestroyingExpiryItemsDetails] OFF;
GO

PRINT 'Migrating [Pharmacy].[DestroyingExpiryItemsHeader]...';
SET IDENTITY_INSERT [Pharmacy].[DestroyingExpiryItemsHeader] ON;
INSERT INTO [Pharmacy].[DestroyingExpiryItemsHeader] ([ID], [SubStoreID], [DestroyNO], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [SubStoreID], [DestroyNO], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DestroyingExpiryItemsHeader];
SET IDENTITY_INSERT [Pharmacy].[DestroyingExpiryItemsHeader] OFF;
GO

PRINT 'Migrating [Pharmacy].[DirectAddationDetails]...';
SET IDENTITY_INSERT [Pharmacy].[DirectAddationDetails] ON;
INSERT INTO [Pharmacy].[DirectAddationDetails] ([ID], [HeaderID], [DrugID], [QTY], [Price], [Amount], [UnitConversionFactorId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Condition], [Note], [TenantId])
SELECT [ID], [HeaderID], [DrugID], [QTY], [Price], [Amount], [UnitConversionFactorId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Condition], [Note], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DirectAddationDetails];
SET IDENTITY_INSERT [Pharmacy].[DirectAddationDetails] OFF;
GO

PRINT 'Migrating [Pharmacy].[DirectAddationHeader]...';
SET IDENTITY_INSERT [Pharmacy].[DirectAddationHeader] ON;
INSERT INTO [Pharmacy].[DirectAddationHeader] ([ID], [OperationNo], [OperationDate], [SubStockID], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BorrowingID], [TenantId])
SELECT [ID], [OperationNo], [OperationDate], [SubStockID], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BorrowingID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DirectAddationHeader];
SET IDENTITY_INSERT [Pharmacy].[DirectAddationHeader] OFF;
GO

PRINT 'Migrating [Pharmacy].[DirectSubstractDetails]...';
SET IDENTITY_INSERT [Pharmacy].[DirectSubstractDetails] ON;
INSERT INTO [Pharmacy].[DirectSubstractDetails] ([ID], [HeaderID], [DrugID], [QTY], [Price], [Amount], [SubStockBatchID], [UnitConversionFactorId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [ID], [HeaderID], [DrugID], [QTY], [Price], [Amount], [SubStockBatchID], [UnitConversionFactorId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DirectSubstractDetails];
SET IDENTITY_INSERT [Pharmacy].[DirectSubstractDetails] OFF;
GO

PRINT 'Migrating [Pharmacy].[DirectSubstractHeader]...';
SET IDENTITY_INSERT [Pharmacy].[DirectSubstractHeader] ON;
INSERT INTO [Pharmacy].[DirectSubstractHeader] ([ID], [OperationNo], [OperationDate], [SubStockID], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BorrowingID], [TenantId])
SELECT [ID], [OperationNo], [OperationDate], [SubStockID], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BorrowingID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DirectSubstractHeader];
SET IDENTITY_INSERT [Pharmacy].[DirectSubstractHeader] OFF;
GO

PRINT 'Migrating [Pharmacy].[DispenseDrugsDetails]...';
SET IDENTITY_INSERT [Pharmacy].[DispenseDrugsDetails] ON;
INSERT INTO [Pharmacy].[DispenseDrugsDetails] ([ID], [PatientID], [IPnumber], [OPnumber], [ReceiptNo], [ReceiptDate], [DrugID], [QTY], [Amount], [ExpiryDate], [BatchNO], [Instructions], [PaymentID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [IPnumber], [OPnumber], [ReceiptNo], [ReceiptDate], [DrugID], [QTY], [Amount], [ExpiryDate], [BatchNO], [Instructions], [PaymentID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DispenseDrugsDetails];
SET IDENTITY_INSERT [Pharmacy].[DispenseDrugsDetails] OFF;
GO

PRINT 'Migrating [Pharmacy].[DispenseDrugsHeader]...';
SET IDENTITY_INSERT [Pharmacy].[DispenseDrugsHeader] ON;
INSERT INTO [Pharmacy].[DispenseDrugsHeader] ([ID], [PatientID], [IPnumber], [OPnumber], [PrescriptionNo], [ReceiptNo], [ReceiptDate], [DoctorID], [SponserID], [ConvertToselF], [PrescriptionDate], [PaymentTypeID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [IPnumber], [OPnumber], [PrescriptionNo], [ReceiptNo], [ReceiptDate], [DoctorID], [SponserID], [ConvertToselF], [PrescriptionDate], [PaymentTypeID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DispenseDrugsHeader];
SET IDENTITY_INSERT [Pharmacy].[DispenseDrugsHeader] OFF;
GO

PRINT 'Migrating [Pharmacy].[DosageFrequencyLink]...';
SET IDENTITY_INSERT [Pharmacy].[DosageFrequencyLink] ON;
INSERT INTO [Pharmacy].[DosageFrequencyLink] ([Id], [DosageId], [FrequencyId], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [DosageId], [FrequencyId], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DosageFrequencyLink];
SET IDENTITY_INSERT [Pharmacy].[DosageFrequencyLink] OFF;
GO

PRINT 'Migrating [Pharmacy].[DosageSession]...';
SET IDENTITY_INSERT [Pharmacy].[DosageSession] ON;
INSERT INTO [Pharmacy].[DosageSession] ([Id], [DosageSessionName], [DosageSessionTime], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [DosageSessionName], [DosageSessionTime], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DosageSession];
SET IDENTITY_INSERT [Pharmacy].[DosageSession] OFF;
GO

PRINT 'Migrating [Pharmacy].[DosageUniteForm]...';
SET IDENTITY_INSERT [Pharmacy].[DosageUniteForm] ON;
INSERT INTO [Pharmacy].[DosageUniteForm] ([ID], [Code], [NameArabic], [NameEnglish], [Status], [CompanyID], [TenantId])
SELECT [ID], [Code], [NameArabic], [NameEnglish], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DosageUniteForm];
SET IDENTITY_INSERT [Pharmacy].[DosageUniteForm] OFF;
GO

PRINT 'Migrating [Pharmacy].[DrugAdminMode]...';
SET IDENTITY_INSERT [Pharmacy].[DrugAdminMode] ON;
INSERT INTO [Pharmacy].[DrugAdminMode] ([Id], [DrugId], [AdminModeId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [DrugId], [AdminModeId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DrugAdminMode];
SET IDENTITY_INSERT [Pharmacy].[DrugAdminMode] OFF;
GO

PRINT 'Migrating [Pharmacy].[DrugAlternative]...';
SET IDENTITY_INSERT [Pharmacy].[DrugAlternative] ON;
INSERT INTO [Pharmacy].[DrugAlternative] ([Id], [MainDrug], [ReplacedDrug], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [MainDrug], [ReplacedDrug], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DrugAlternative];
SET IDENTITY_INSERT [Pharmacy].[DrugAlternative] OFF;
GO

PRINT 'Migrating [Pharmacy].[DrugClassDet]...';
SET IDENTITY_INSERT [Pharmacy].[DrugClassDet] ON;
INSERT INTO [Pharmacy].[DrugClassDet] ([Id], [DrugClassID], [DrugID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [Id], [DrugClassID], [DrugID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DrugClassDet];
SET IDENTITY_INSERT [Pharmacy].[DrugClassDet] OFF;
GO

PRINT 'Migrating [Pharmacy].[DrugClass]...';
SET IDENTITY_INSERT [Pharmacy].[DrugClass] ON;
INSERT INTO [Pharmacy].[DrugClass] ([Id], [ClassName], [ClassNameAr], [Priority], [CompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Serial], [TenantId])
SELECT [Id], [ClassName], [ClassNameAr], [Priority], [CompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Serial], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DrugClass];
SET IDENTITY_INSERT [Pharmacy].[DrugClass] OFF;
GO

PRINT 'Migrating [Pharmacy].[DrugClassificationPeroid]...';
SET IDENTITY_INSERT [Pharmacy].[DrugClassificationPeroid] ON;
INSERT INTO [Pharmacy].[DrugClassificationPeroid] ([Id], [DrugClassificationId], [Peroid], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate], [TenantId])
SELECT [Id], [DrugClassificationId], [Peroid], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DrugClassificationPeroid];
SET IDENTITY_INSERT [Pharmacy].[DrugClassificationPeroid] OFF;
GO

PRINT 'Migrating [Pharmacy].[DrugClassification]...';
SET IDENTITY_INSERT [Pharmacy].[DrugClassification] ON;
INSERT INTO [Pharmacy].[DrugClassification] ([Id], [NameEn], [NameAr], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [NameEn], [NameAr], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DrugClassification];
SET IDENTITY_INSERT [Pharmacy].[DrugClassification] OFF;
GO

PRINT 'Migrating [Pharmacy].[DrugForms]...';
SET IDENTITY_INSERT [Pharmacy].[DrugForms] ON;
INSERT INTO [Pharmacy].[DrugForms] ([Id], [FormName], [FormNameArabic], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [FormName], [FormNameArabic], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DrugForms];
SET IDENTITY_INSERT [Pharmacy].[DrugForms] OFF;
GO

PRINT 'Migrating [Pharmacy].[DrugGroupMaster]...';
SET IDENTITY_INSERT [Pharmacy].[DrugGroupMaster] ON;
INSERT INTO [Pharmacy].[DrugGroupMaster] ([ID], [Code], [NameArabic], [NameEnglish], [Status], [CompanyID], [TenantId])
SELECT [ID], [Code], [NameArabic], [NameEnglish], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DrugGroupMaster];
SET IDENTITY_INSERT [Pharmacy].[DrugGroupMaster] OFF;
GO

PRINT 'Migrating [Pharmacy].[DrugPreparationTemplate]...';
SET IDENTITY_INSERT [Pharmacy].[DrugPreparationTemplate] ON;
INSERT INTO [Pharmacy].[DrugPreparationTemplate] ([ID], [PreparationDrugID], [SubDrugID], [QTY], [UnitID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [PreparationDrugID], [SubDrugID], [QTY], [UnitID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DrugPreparationTemplate];
SET IDENTITY_INSERT [Pharmacy].[DrugPreparationTemplate] OFF;
GO

PRINT 'Migrating [Pharmacy].[DrugTemplate]...';
SET IDENTITY_INSERT [Pharmacy].[DrugTemplate] ON;
INSERT INTO [Pharmacy].[DrugTemplate] ([Id], [DrugId], [GenericId], [Dosage], [DosageBaseUnitId], [DosageQnt], [RouteId], [FrequId], [Duration], [DurationTypeId], [AdminSiteId], [DrugStengthId], [DrugFormId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [DrugId], [GenericId], [Dosage], [DosageBaseUnitId], [DosageQnt], [RouteId], [FrequId], [Duration], [DurationTypeId], [AdminSiteId], [DrugStengthId], [DrugFormId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DrugTemplate];
SET IDENTITY_INSERT [Pharmacy].[DrugTemplate] OFF;
GO

PRINT 'Migrating [Pharmacy].[DrugTypes]...';
SET IDENTITY_INSERT [Pharmacy].[DrugTypes] ON;
INSERT INTO [Pharmacy].[DrugTypes] ([Id], [TypeNameAr], [TypeName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [TypeNameAr], [TypeName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[DrugTypes];
SET IDENTITY_INSERT [Pharmacy].[DrugTypes] OFF;
GO

PRINT 'Migrating [Pharmacy].[Drug_manufacturers]...';
SET IDENTITY_INSERT [Pharmacy].[Drug_manufacturers] ON;
INSERT INTO [Pharmacy].[Drug_manufacturers] ([id], [Code], [Name], [NameAr], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [id], [Code], [Name], [NameAr], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[Drug_manufacturers];
SET IDENTITY_INSERT [Pharmacy].[Drug_manufacturers] OFF;
GO

PRINT 'Migrating [Pharmacy].[Drugs]...';
SET IDENTITY_INSERT [Pharmacy].[Drugs] ON;
INSERT INTO [Pharmacy].[Drugs] ([ID], [DrugCode], [DrugName], [BrandID], [DrugFormID], [DrugTypeID], [BaseUnitID], [DosageUnitID], [DrugStrength], [StorageConditions], [Instractions], [Notes], [ClassificationId], [OPSellingPrice], [IPSellingPrice], [Status], [IsCombination], [AdminModeId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [UnitTemplateID], [BarCode], [Batch], [CostPrice], [LocalBarCode], [ServiceId], [SFDA], [IsProhibited], [manufacturer_ID], [GenericName_ID], [DrugNameAr], [HighRisk], [NphiesCode], [Istaxable], [BrandType], [AllergyTestNeeded], [TenantId])
SELECT [ID], [DrugCode], [DrugName], [BrandID], [DrugFormID], [DrugTypeID], [BaseUnitID], [DosageUnitID], [DrugStrength], [StorageConditions], [Instractions], [Notes], [ClassificationId], [OPSellingPrice], [IPSellingPrice], [Status], [IsCombination], [AdminModeId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [UnitTemplateID], [BarCode], [Batch], [CostPrice], [LocalBarCode], [ServiceId], [SFDA], [IsProhibited], [manufacturer_ID], [GenericName_ID], [DrugNameAr], [HighRisk], [NphiesCode], [Istaxable], [BrandType], [AllergyTestNeeded], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[Drugs];
SET IDENTITY_INSERT [Pharmacy].[Drugs] OFF;
GO

PRINT 'Migrating [Pharmacy].[ExpenseMaster]...';
SET IDENTITY_INSERT [Pharmacy].[ExpenseMaster] ON;
INSERT INTO [Pharmacy].[ExpenseMaster] ([ID], [ExpenseName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [AccountID], [TenantId])
SELECT [ID], [ExpenseName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [AccountID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[ExpenseMaster];
SET IDENTITY_INSERT [Pharmacy].[ExpenseMaster] OFF;
GO

PRINT 'Migrating [Pharmacy].[Frequencies]...';
SET IDENTITY_INSERT [Pharmacy].[Frequencies] ON;
INSERT INTO [Pharmacy].[Frequencies] ([Id], [Name], [Value], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameAr], [TenantId])
SELECT [Id], [Name], [Value], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameAr], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[Frequencies];
SET IDENTITY_INSERT [Pharmacy].[Frequencies] OFF;
GO

PRINT 'Migrating [Pharmacy].[GRNDetailsBatchesTransactions]...';
SET IDENTITY_INSERT [Pharmacy].[GRNDetailsBatchesTransactions] ON;
INSERT INTO [Pharmacy].[GRNDetailsBatchesTransactions] ([Id], [GRNDetailsID], [SubStoreID], [ReceivedQty], [AcceptedQty], [ExcessQty], [Bonus], [AvailableQty], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IssueBatchID], [BatchId], [TenantId])
SELECT [Id], [GRNDetailsID], [SubStoreID], [ReceivedQty], [AcceptedQty], [ExcessQty], [Bonus], [AvailableQty], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IssueBatchID], [BatchId], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[GRNDetailsBatchesTransactions];
SET IDENTITY_INSERT [Pharmacy].[GRNDetailsBatchesTransactions] OFF;
GO

PRINT 'Migrating [Pharmacy].[GRN_LPOs]...';
SET IDENTITY_INSERT [Pharmacy].[GRN_LPOs] ON;
INSERT INTO [Pharmacy].[GRN_LPOs] ([id], [GRNID], [LPOID], [TenantId])
SELECT [id], [GRNID], [LPOID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[GRN_LPOs];
SET IDENTITY_INSERT [Pharmacy].[GRN_LPOs] OFF;
GO

PRINT 'Migrating [Pharmacy].[GenericNamesReplacement]...';
SET IDENTITY_INSERT [Pharmacy].[GenericNamesReplacement] ON;
INSERT INTO [Pharmacy].[GenericNamesReplacement] ([Id], [MainGeneric], [ReplacedGeneric], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [MainGeneric], [ReplacedGeneric], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[GenericNamesReplacement];
SET IDENTITY_INSERT [Pharmacy].[GenericNamesReplacement] OFF;
GO

PRINT 'Migrating [Pharmacy].[GenericNames]...';
SET IDENTITY_INSERT [Pharmacy].[GenericNames] ON;
INSERT INTO [Pharmacy].[GenericNames] ([Id], [GenericNamesAr], [GenericNames], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [GenericNamesAr], [GenericNames], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[GenericNames];
SET IDENTITY_INSERT [Pharmacy].[GenericNames] OFF;
GO

PRINT 'Migrating [Pharmacy].[GoodsReceivedNoteDetails]...';
SET IDENTITY_INSERT [Pharmacy].[GoodsReceivedNoteDetails] ON;
INSERT INTO [Pharmacy].[GoodsReceivedNoteDetails] ([ID], [GRNId], [DrugID], [AcceptQTY], [OrderedQTY], [BonusQTY], [PriceLC], [AmountLC], [DiscountAmount], [DiscountPrecint], [PriceFC], [UnitConversionFactorId], [AmountFC], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SellPrice], [LPODetID], [TenantId])
SELECT [ID], [GRNId], [DrugID], [AcceptQTY], [OrderedQTY], [BonusQTY], [PriceLC], [AmountLC], [DiscountAmount], [DiscountPrecint], [PriceFC], [UnitConversionFactorId], [AmountFC], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SellPrice], [LPODetID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[GoodsReceivedNoteDetails];
SET IDENTITY_INSERT [Pharmacy].[GoodsReceivedNoteDetails] OFF;
GO

PRINT 'Migrating [Pharmacy].[GoodsReceivedNoteExpenses]...';
SET IDENTITY_INSERT [Pharmacy].[GoodsReceivedNoteExpenses] ON;
INSERT INTO [Pharmacy].[GoodsReceivedNoteExpenses] ([ID], [ExpensesID], [GRNId], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ExpensesID], [GRNId], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[GoodsReceivedNoteExpenses];
SET IDENTITY_INSERT [Pharmacy].[GoodsReceivedNoteExpenses] OFF;
GO

PRINT 'Migrating [Pharmacy].[GoodsReceivedNoteHeader]...';
SET IDENTITY_INSERT [Pharmacy].[GoodsReceivedNoteHeader] ON;
INSERT INTO [Pharmacy].[GoodsReceivedNoteHeader] ([ID], [LPOId], [GRNNO], [GRNDate], [PaymentTermsID], [DeliveryNoteNo], [DeliveryNoteDate], [InvoiceNO], [InvoiceDate], [TotalExpensesAmount], [TotallandedAmount], [TotalGRNValueFC], [TotalGRNValueLC], [GRNPreparedDate], [Remarks], [Status], [ISOpeneingStock], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TaxValue], [TransType_ID], [Direct], [OpeningBalance], [issueToDepartment], [StockID], [SetEntryCode], [SupplierId], [Holdingtax], [VatValue], [TenantId])
SELECT [ID], [LPOId], [GRNNO], [GRNDate], [PaymentTermsID], [DeliveryNoteNo], [DeliveryNoteDate], [InvoiceNO], [InvoiceDate], [TotalExpensesAmount], [TotallandedAmount], [TotalGRNValueFC], [TotalGRNValueLC], [GRNPreparedDate], [Remarks], [Status], [ISOpeneingStock], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TaxValue], [TransType_ID], [Direct], [OpeningBalance], [issueToDepartment], [StockID], [SetEntryCode], [SupplierId], [Holdingtax], [VatValue], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[GoodsReceivedNoteHeader];
SET IDENTITY_INSERT [Pharmacy].[GoodsReceivedNoteHeader] OFF;
GO

PRINT 'Migrating [Pharmacy].[InternalConsumptionEntry]...';
SET IDENTITY_INSERT [Pharmacy].[InternalConsumptionEntry] ON;
INSERT INTO [Pharmacy].[InternalConsumptionEntry] ([ID], [InternalConsumptionID], [DrugID], [SubStoreBatchId], [Date], [UnitID], [CurrentQty], [QtyConsumed], [SubStoreID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [InternalConsumptionID], [DrugID], [SubStoreBatchId], [Date], [UnitID], [CurrentQty], [QtyConsumed], [SubStoreID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[InternalConsumptionEntry];
SET IDENTITY_INSERT [Pharmacy].[InternalConsumptionEntry] OFF;
GO

PRINT 'Migrating [Pharmacy].[InternalConsumption]...';
SET IDENTITY_INSERT [Pharmacy].[InternalConsumption] ON;
INSERT INTO [Pharmacy].[InternalConsumption] ([ID], [InternalConsumptionNumber], [Date], [SubStoreID], [Remarks], [IsActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [InternalConsumptionNumber], [Date], [SubStoreID], [Remarks], [IsActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[InternalConsumption];
SET IDENTITY_INSERT [Pharmacy].[InternalConsumption] OFF;
GO

PRINT 'Migrating [Pharmacy].[IssueRequest]...';
SET IDENTITY_INSERT [Pharmacy].[IssueRequest] ON;
INSERT INTO [Pharmacy].[IssueRequest] ([ID], [RequestNumber], [RequestDate], [MainSubstoreID], [SubSubstoreID], [Status], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [Priority], [EmpRequestID], [TenantId])
SELECT [ID], [RequestNumber], [RequestDate], [MainSubstoreID], [SubSubstoreID], [Status], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [Priority], [EmpRequestID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[IssueRequest];
SET IDENTITY_INSERT [Pharmacy].[IssueRequest] OFF;
GO

PRINT 'Migrating [Pharmacy].[IssueRequestdetails]...';
SET IDENTITY_INSERT [Pharmacy].[IssueRequestdetails] ON;
INSERT INTO [Pharmacy].[IssueRequestdetails] ([ID], [IssueRequestId], [DrugID], [UnitConversionID], [Quantity], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [IssueRequestId], [DrugID], [UnitConversionID], [Quantity], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[IssueRequestdetails];
SET IDENTITY_INSERT [Pharmacy].[IssueRequestdetails] OFF;
GO

PRINT 'Migrating [Pharmacy].[IssuetoDepartmentEntry]...';
SET IDENTITY_INSERT [Pharmacy].[IssuetoDepartmentEntry] ON;
INSERT INTO [Pharmacy].[IssuetoDepartmentEntry] ([ID], [IssuetoDepartmentId], [DrugID], [StockBatchId], [Date], [UnitConversionID], [CurrentQty], [IssueQty], [TotalReturnQty], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [IssuetoDepartmentId], [DrugID], [StockBatchId], [Date], [UnitConversionID], [CurrentQty], [IssueQty], [TotalReturnQty], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[IssuetoDepartmentEntry];
SET IDENTITY_INSERT [Pharmacy].[IssuetoDepartmentEntry] OFF;
GO

PRINT 'Migrating [Pharmacy].[IssuetoDepartmentReturnEntry]...';
SET IDENTITY_INSERT [Pharmacy].[IssuetoDepartmentReturnEntry] ON;
INSERT INTO [Pharmacy].[IssuetoDepartmentReturnEntry] ([ID], [IssuetoDepartmentReturnID], [IssueToDepartementEntryID], [IssueReturnQty], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [IssuetoDepartmentReturnID], [IssueToDepartementEntryID], [IssueReturnQty], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[IssuetoDepartmentReturnEntry];
SET IDENTITY_INSERT [Pharmacy].[IssuetoDepartmentReturnEntry] OFF;
GO

PRINT 'Migrating [Pharmacy].[IssuetoDepartmentReturn]...';
SET IDENTITY_INSERT [Pharmacy].[IssuetoDepartmentReturn] ON;
INSERT INTO [Pharmacy].[IssuetoDepartmentReturn] ([ID], [StockID], [IssueReturnNumber], [IssueReturnDate], [IssueNumber], [IssueDate], [Remarks], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SetEntryCode], [TenantId])
SELECT [ID], [StockID], [IssueReturnNumber], [IssueReturnDate], [IssueNumber], [IssueDate], [Remarks], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SetEntryCode], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[IssuetoDepartmentReturn];
SET IDENTITY_INSERT [Pharmacy].[IssuetoDepartmentReturn] OFF;
GO

PRINT 'Migrating [Pharmacy].[IssuetoDepartment]...';
SET IDENTITY_INSERT [Pharmacy].[IssuetoDepartment] ON;
INSERT INTO [Pharmacy].[IssuetoDepartment] ([ID], [MainStockID], [IssueNumber], [IssueDate], [StockID], [Remarks], [Status], [IssueRequestID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SetEntryCode], [TenantId])
SELECT [ID], [MainStockID], [IssueNumber], [IssueDate], [StockID], [Remarks], [Status], [IssueRequestID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SetEntryCode], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[IssuetoDepartment];
SET IDENTITY_INSERT [Pharmacy].[IssuetoDepartment] OFF;
GO

PRINT 'Migrating [Pharmacy].[LPOApprovingAuthorityHeader]...';
SET IDENTITY_INSERT [Pharmacy].[LPOApprovingAuthorityHeader] ON;
INSERT INTO [Pharmacy].[LPOApprovingAuthorityHeader] ([Id], [LevelName], [LevelAmount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [LevelName], [LevelAmount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[LPOApprovingAuthorityHeader];
SET IDENTITY_INSERT [Pharmacy].[LPOApprovingAuthorityHeader] OFF;
GO

PRINT 'Migrating [Pharmacy].[LPOApprovingAuthority]...';
SET IDENTITY_INSERT [Pharmacy].[LPOApprovingAuthority] ON;
INSERT INTO [Pharmacy].[LPOApprovingAuthority] ([ID], [UserID], [LevelID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [UserID], [LevelID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[LPOApprovingAuthority];
SET IDENTITY_INSERT [Pharmacy].[LPOApprovingAuthority] OFF;
GO

PRINT 'Migrating [Pharmacy].[LegalStatusMaster]...';
SET IDENTITY_INSERT [Pharmacy].[LegalStatusMaster] ON;
INSERT INTO [Pharmacy].[LegalStatusMaster] ([ID], [Code], [NameArabic], [NameEnglish], [Status], [CompanyID], [TenantId])
SELECT [ID], [Code], [NameArabic], [NameEnglish], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[LegalStatusMaster];
SET IDENTITY_INSERT [Pharmacy].[LegalStatusMaster] OFF;
GO

PRINT 'Migrating [Pharmacy].[LocalPurchaseCancelation]...';
SET IDENTITY_INSERT [Pharmacy].[LocalPurchaseCancelation] ON;
INSERT INTO [Pharmacy].[LocalPurchaseCancelation] ([ID], [CancelDate], [CancelNumber], [Narration], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [CancelDate], [CancelNumber], [Narration], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[LocalPurchaseCancelation];
SET IDENTITY_INSERT [Pharmacy].[LocalPurchaseCancelation] OFF;
GO

PRINT 'Migrating [Pharmacy].[LocalPurchaseOrderDetails]...';
SET IDENTITY_INSERT [Pharmacy].[LocalPurchaseOrderDetails] ON;
INSERT INTO [Pharmacy].[LocalPurchaseOrderDetails] ([ID], [DrugID], [LPOID], [OrderedQTY], [BonusQuantity], [Amount], [Price], [DiscountAmount], [DiscountPrecint], [UnitConversionFactorId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [AcceptQTY], [BonusQTY], [AmountFC], [PriceFC], [SellPrice], [PrevAcceptQTY], [GenericID], [TenantId])
SELECT [ID], [DrugID], [LPOID], [OrderedQTY], [BonusQuantity], [Amount], [Price], [DiscountAmount], [DiscountPrecint], [UnitConversionFactorId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [AcceptQTY], [BonusQTY], [AmountFC], [PriceFC], [SellPrice], [PrevAcceptQTY], [GenericID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[LocalPurchaseOrderDetails];
SET IDENTITY_INSERT [Pharmacy].[LocalPurchaseOrderDetails] OFF;
GO

PRINT 'Migrating [Pharmacy].[LocalPurchaseOrderExpenses]...';
SET IDENTITY_INSERT [Pharmacy].[LocalPurchaseOrderExpenses] ON;
INSERT INTO [Pharmacy].[LocalPurchaseOrderExpenses] ([ID], [ExpensesID], [LPOID], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ExpensesID], [LPOID], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[LocalPurchaseOrderExpenses];
SET IDENTITY_INSERT [Pharmacy].[LocalPurchaseOrderExpenses] OFF;
GO

PRINT 'Migrating [Pharmacy].[LocalPurchaseOrderHeader]...';
SET IDENTITY_INSERT [Pharmacy].[LocalPurchaseOrderHeader] ON;
INSERT INTO [Pharmacy].[LocalPurchaseOrderHeader] ([ID], [SubStoreID], [SupplierID], [PODate], [CurrencyID], [PONumber], [ShippingModeID], [ShippingTermsID], [DeliveryTime], [DeliveryTimeType], [PaymentModeID], [PaymentTermsID], [Status], [TotalExpenses], [TotalAmount], [NetAmount], [DiscountAmount], [DiscountPrecint], [Remarks], [IsEmergency], [LocalPurchaseCancelationId], [UnifiedPurchCommission], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TaxValue], [Direct], [openingBalance], [issueToDepartment], [PurchRequestID], [TenantId])
SELECT [ID], [SubStoreID], [SupplierID], [PODate], [CurrencyID], [PONumber], [ShippingModeID], [ShippingTermsID], [DeliveryTime], [DeliveryTimeType], [PaymentModeID], [PaymentTermsID], [Status], [TotalExpenses], [TotalAmount], [NetAmount], [DiscountAmount], [DiscountPrecint], [Remarks], [IsEmergency], [LocalPurchaseCancelationId], [UnifiedPurchCommission], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TaxValue], [Direct], [openingBalance], [issueToDepartment], [PurchRequestID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[LocalPurchaseOrderHeader];
SET IDENTITY_INSERT [Pharmacy].[LocalPurchaseOrderHeader] OFF;
GO

PRINT 'Migrating [Pharmacy].[MainSolution]...';
SET IDENTITY_INSERT [Pharmacy].[MainSolution] ON;
INSERT INTO [Pharmacy].[MainSolution] ([Id], [GenericId], [DrugId], [TemplateId], [TenantId])
SELECT [Id], [GenericId], [DrugId], [TemplateId], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[MainSolution];
SET IDENTITY_INSERT [Pharmacy].[MainSolution] OFF;
GO

PRINT 'Migrating [Pharmacy].[MedicationDispensingPeriod]...';
SET IDENTITY_INSERT [Pharmacy].[MedicationDispensingPeriod] ON;
INSERT INTO [Pharmacy].[MedicationDispensingPeriod] ([ID], [NameArabic], [NameEnglish], [FirstTimeWarning], [SecondTimeWarning], [TradeName], [CompanyID], [TenantId])
SELECT [ID], [NameArabic], [NameEnglish], [FirstTimeWarning], [SecondTimeWarning], [TradeName], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[MedicationDispensingPeriod];
SET IDENTITY_INSERT [Pharmacy].[MedicationDispensingPeriod] OFF;
GO

PRINT 'Migrating [Pharmacy].[MedicationDispensingPeriod_GenericNames]...';
SET IDENTITY_INSERT [Pharmacy].[MedicationDispensingPeriod_GenericNames] ON;
INSERT INTO [Pharmacy].[MedicationDispensingPeriod_GenericNames] ([ID], [MedicationPeriod_Id], [GenericName_Id], [CompanyID], [TenantId])
SELECT [ID], [MedicationPeriod_Id], [GenericName_Id], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[MedicationDispensingPeriod_GenericNames];
SET IDENTITY_INSERT [Pharmacy].[MedicationDispensingPeriod_GenericNames] OFF;
GO

PRINT 'Migrating [Pharmacy].[OpeningPharmacyDetails]...';
SET IDENTITY_INSERT [Pharmacy].[OpeningPharmacyDetails] ON;
INSERT INTO [Pharmacy].[OpeningPharmacyDetails] ([ID], [OpeningPharmacyID], [DrugID], [BatchID], [ExpiryDate], [BatchNo], [PhysicQTY], [PharmacyInHand], [DifferenceQTY], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OpenBalance], [TenantId])
SELECT [ID], [OpeningPharmacyID], [DrugID], [BatchID], [ExpiryDate], [BatchNo], [PhysicQTY], [PharmacyInHand], [DifferenceQTY], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [OpenBalance], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[OpeningPharmacyDetails];
SET IDENTITY_INSERT [Pharmacy].[OpeningPharmacyDetails] OFF;
GO

PRINT 'Migrating [Pharmacy].[OpeningPharmacyHeader]...';
SET IDENTITY_INSERT [Pharmacy].[OpeningPharmacyHeader] ON;
INSERT INTO [Pharmacy].[OpeningPharmacyHeader] ([ID], [PharmacyID], [Status], [Year], [Remarks], [Date], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [CloseYear], [TenantId])
SELECT [ID], [PharmacyID], [Status], [Year], [Remarks], [Date], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [CloseYear], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[OpeningPharmacyHeader];
SET IDENTITY_INSERT [Pharmacy].[OpeningPharmacyHeader] OFF;
GO

PRINT 'Migrating [Pharmacy].[OpeningStockDetails]...';
SET IDENTITY_INSERT [Pharmacy].[OpeningStockDetails] ON;
INSERT INTO [Pharmacy].[OpeningStockDetails] ([ID], [OpeningStockID], [DrugID], [BatchID], [ExpiryDate], [BatchNo], [PhysicQTY], [StockInHand], [DifferenceQTY], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [OpeningStockID], [DrugID], [BatchID], [ExpiryDate], [BatchNo], [PhysicQTY], [StockInHand], [DifferenceQTY], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[OpeningStockDetails];
SET IDENTITY_INSERT [Pharmacy].[OpeningStockDetails] OFF;
GO

PRINT 'Migrating [Pharmacy].[OpeningStockHeader]...';
SET IDENTITY_INSERT [Pharmacy].[OpeningStockHeader] ON;
INSERT INTO [Pharmacy].[OpeningStockHeader] ([ID], [SubStoreID], [Status], [Year], [Remarks], [Date], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [SubStoreID], [Status], [Year], [Remarks], [Date], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[OpeningStockHeader];
SET IDENTITY_INSERT [Pharmacy].[OpeningStockHeader] OFF;
GO

PRINT 'Migrating [Pharmacy].[OtherHospitals]...';
SET IDENTITY_INSERT [Pharmacy].[OtherHospitals] ON;
INSERT INTO [Pharmacy].[OtherHospitals] ([Id], [HospitalName], [POBox], [City], [Zip], [Email], [WebSite], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [HospitalName], [POBox], [City], [Zip], [Email], [WebSite], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[OtherHospitals];
SET IDENTITY_INSERT [Pharmacy].[OtherHospitals] OFF;
GO

PRINT 'Migrating [Pharmacy].[PackageUnitMaster]...';
SET IDENTITY_INSERT [Pharmacy].[PackageUnitMaster] ON;
INSERT INTO [Pharmacy].[PackageUnitMaster] ([ID], [Code], [NameArabic], [NameEnglish], [Status], [CompanyID], [TenantId])
SELECT [ID], [Code], [NameArabic], [NameEnglish], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[PackageUnitMaster];
SET IDENTITY_INSERT [Pharmacy].[PackageUnitMaster] OFF;
GO

PRINT 'Migrating [Pharmacy].[PaymentGroup]...';
SET IDENTITY_INSERT [Pharmacy].[PaymentGroup] ON;
INSERT INTO [Pharmacy].[PaymentGroup] ([PaymentGroupID], [PaymentGroupName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [PaymentGroupID], [PaymentGroupName], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[PaymentGroup];
SET IDENTITY_INSERT [Pharmacy].[PaymentGroup] OFF;
GO

PRINT 'Migrating [Pharmacy].[PaymentSupplier]...';
SET IDENTITY_INSERT [Pharmacy].[PaymentSupplier] ON;
INSERT INTO [Pharmacy].[PaymentSupplier] ([Id], [SupplierID], [CurrencyID], [ForiegnValue], [ConversionValue], [LocalValue], [Description], [Sub_SupplierAccount], [Main_SupplierAccount], [Sub_BoxAccount], [Main_BoxAccount], [PaymentDate], [PaymentType], [Checkno], [EntryCodes], [CompanyID], [TenantId])
SELECT [Id], [SupplierID], [CurrencyID], [ForiegnValue], [ConversionValue], [LocalValue], [Description], [Sub_SupplierAccount], [Main_SupplierAccount], [Sub_BoxAccount], [Main_BoxAccount], [PaymentDate], [PaymentType], [Checkno], [EntryCodes], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[PaymentSupplier];
SET IDENTITY_INSERT [Pharmacy].[PaymentSupplier] OFF;
GO

PRINT 'Migrating [Pharmacy].[PaymentTermsMaster]...';
SET IDENTITY_INSERT [Pharmacy].[PaymentTermsMaster] ON;
INSERT INTO [Pharmacy].[PaymentTermsMaster] ([ID], [PaymentTermCode], [PaymentTermDesc], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameKa], [TenantId])
SELECT [ID], [PaymentTermCode], [PaymentTermDesc], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameKa], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[PaymentTermsMaster];
SET IDENTITY_INSERT [Pharmacy].[PaymentTermsMaster] OFF;
GO

PRINT 'Migrating [Pharmacy].[PaymentTermsSchedule]...';
SET IDENTITY_INSERT [Pharmacy].[PaymentTermsSchedule] ON;
INSERT INTO [Pharmacy].[PaymentTermsSchedule] ([ID], [PaymentTermsID], [ScheduleDay], [SchedulePrecentage], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TimeFrom], [TenantId])
SELECT [ID], [PaymentTermsID], [ScheduleDay], [SchedulePrecentage], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TimeFrom], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[PaymentTermsSchedule];
SET IDENTITY_INSERT [Pharmacy].[PaymentTermsSchedule] OFF;
GO

PRINT 'Migrating [Pharmacy].[PharmInstallationDoctorDegree]...';
SET IDENTITY_INSERT [Pharmacy].[PharmInstallationDoctorDegree] ON;
INSERT INTO [Pharmacy].[PharmInstallationDoctorDegree] ([Id], [DoctorDegreeId], [CompanyID], [TenantId])
SELECT [Id], [DoctorDegreeId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[PharmInstallationDoctorDegree];
SET IDENTITY_INSERT [Pharmacy].[PharmInstallationDoctorDegree] OFF;
GO

PRINT 'Migrating [Pharmacy].[PharmInstallation]...';
SET IDENTITY_INSERT [Pharmacy].[PharmInstallation] ON;
INSERT INTO [Pharmacy].[PharmInstallation] ([Id], [BarCode], [CreatedBy], [JEwithBatch], [Alert0Qty], [Order0Qty], [IBarCode], [DispenseOPIP], [storeIntegration], [ReturnToDeposit], [UpdatePrice], [SubStoreID], [CreatedDate], [CompanyID], [IsUpdateStock], [IsAddition], [IsDispense], [isReturn], [IsIToD], [IsIToPharmacy], [IstraferStock], [ExpiaryPeriodDays], [TenantId])
SELECT [Id], [BarCode], [CreatedBy], [JEwithBatch], [Alert0Qty], [Order0Qty], [IBarCode], [DispenseOPIP], [storeIntegration], [ReturnToDeposit], [UpdatePrice], [SubStoreID], [CreatedDate], [CompanyID], [IsUpdateStock], [IsAddition], [IsDispense], [isReturn], [IsIToD], [IsIToPharmacy], [IstraferStock], [ExpiaryPeriodDays], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[PharmInstallation];
SET IDENTITY_INSERT [Pharmacy].[PharmInstallation] OFF;
GO

PRINT 'Migrating [Pharmacy].[PharmacyPayment]...';
SET IDENTITY_INSERT [Pharmacy].[PharmacyPayment] ON;
INSERT INTO [Pharmacy].[PharmacyPayment] ([ID], [Date], [SupplierID], [chequeNumber], [Value], [chequeStatus], [DueDate], [MainAccount_Supplier], [SubAccount_Supplier], [MainAccount_Bank], [SubAccount_Bank], [MainAccount_PaymentPaper], [SubAccount_PaymentPaper], [CreationDate], [CreatedBy], [Modificationdate], [ModifiedBy], [ForiegnValue], [ConversionValue], [CurrencyID], [SetEntryCode], [PaymentMode], [SupplierDuesId], [CompanyID], [TenantId])
SELECT [ID], [Date], [SupplierID], [chequeNumber], [Value], [chequeStatus], [DueDate], [MainAccount_Supplier], [SubAccount_Supplier], [MainAccount_Bank], [SubAccount_Bank], [MainAccount_PaymentPaper], [SubAccount_PaymentPaper], [CreationDate], [CreatedBy], [Modificationdate], [ModifiedBy], [ForiegnValue], [ConversionValue], [CurrencyID], [SetEntryCode], [PaymentMode], [SupplierDuesId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[PharmacyPayment];
SET IDENTITY_INSERT [Pharmacy].[PharmacyPayment] OFF;
GO

PRINT 'Migrating [Pharmacy].[PharmacySettings]...';
SET IDENTITY_INSERT [Pharmacy].[PharmacySettings] ON;
INSERT INTO [Pharmacy].[PharmacySettings] ([ID], [MinQtyStatus ], [MinUserId], [MaxQtyStatus], [MaxUserId], [ExchangePolicy], [UnifiedPurchaseSupplierID], [PharmacyMgr], [PharmacySubDepartmentID], [CompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Vat], [DispenseQuantity], [PhysicalStockAdjustment], [ExpiaryPeriodDays], [RecessionPeriodInDays], [ReorderLvel], [slowMove], [TenantId])
SELECT [ID], [MinQtyStatus ], [MinUserId], [MaxQtyStatus], [MaxUserId], [ExchangePolicy], [UnifiedPurchaseSupplierID], [PharmacyMgr], [PharmacySubDepartmentID], [CompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Vat], [DispenseQuantity], [PhysicalStockAdjustment], [ExpiaryPeriodDays], [RecessionPeriodInDays], [ReorderLvel], [slowMove], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[PharmacySettings];
SET IDENTITY_INSERT [Pharmacy].[PharmacySettings] OFF;
GO

PRINT 'Migrating [Pharmacy].[PharmacySupplierInvoicePayment]...';
SET IDENTITY_INSERT [Pharmacy].[PharmacySupplierInvoicePayment] ON;
INSERT INTO [Pharmacy].[PharmacySupplierInvoicePayment] ([Id], [GRNHeaderID], [SupplierID], [CurrencyID], [ForiegnValue], [ConversionValue], [LocalValue], [CashHeaderID], [PharmacyHeaderID], [PaymentDate], [PaymentType], [CompanyID], [EntryCode], [TenantId])
SELECT [Id], [GRNHeaderID], [SupplierID], [CurrencyID], [ForiegnValue], [ConversionValue], [LocalValue], [CashHeaderID], [PharmacyHeaderID], [PaymentDate], [PaymentType], [CompanyID], [EntryCode], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[PharmacySupplierInvoicePayment];
SET IDENTITY_INSERT [Pharmacy].[PharmacySupplierInvoicePayment] OFF;
GO

PRINT 'Migrating [Pharmacy].[PhysicalStockAdjustmentEntry]...';
SET IDENTITY_INSERT [Pharmacy].[PhysicalStockAdjustmentEntry] ON;
INSERT INTO [Pharmacy].[PhysicalStockAdjustmentEntry] ([ID], [PhysiacalAdjID], [BatchID], [QTYinHand], [QTYAdj], [QtYDifference], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [DrugID], [TenantId])
SELECT [ID], [PhysiacalAdjID], [BatchID], [QTYinHand], [QTYAdj], [QtYDifference], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [DrugID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[PhysicalStockAdjustmentEntry];
SET IDENTITY_INSERT [Pharmacy].[PhysicalStockAdjustmentEntry] OFF;
GO

PRINT 'Migrating [Pharmacy].[PhysicalStockAdjustment]...';
SET IDENTITY_INSERT [Pharmacy].[PhysicalStockAdjustment] ON;
INSERT INTO [Pharmacy].[PhysicalStockAdjustment] ([ID], [SubStoreID], [ReasonAdjusyId], [Date], [Status], [Remarks], [PhysiacalNO], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [SubStoreID], [ReasonAdjusyId], [Date], [Status], [Remarks], [PhysiacalNO], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[PhysicalStockAdjustment];
SET IDENTITY_INSERT [Pharmacy].[PhysicalStockAdjustment] OFF;
GO

PRINT 'Migrating [Pharmacy].[PrescriptionAbbreviation]...';
SET IDENTITY_INSERT [Pharmacy].[PrescriptionAbbreviation] ON;
INSERT INTO [Pharmacy].[PrescriptionAbbreviation] ([ID], [Code], [Quantity], [AbbreviationName], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Quantity], [AbbreviationName], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[PrescriptionAbbreviation];
SET IDENTITY_INSERT [Pharmacy].[PrescriptionAbbreviation] OFF;
GO

PRINT 'Migrating [Pharmacy].[ProhibitedDrugDocs]...';
SET IDENTITY_INSERT [Pharmacy].[ProhibitedDrugDocs] ON;
INSERT INTO [Pharmacy].[ProhibitedDrugDocs] ([ID], [InvoiceID], [ImageName], [TenantId])
SELECT [ID], [InvoiceID], [ImageName], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[ProhibitedDrugDocs];
SET IDENTITY_INSERT [Pharmacy].[ProhibitedDrugDocs] OFF;
GO

PRINT 'Migrating [Pharmacy].[PurchaseReturnDetails]...';
SET IDENTITY_INSERT [Pharmacy].[PurchaseReturnDetails] ON;
INSERT INTO [Pharmacy].[PurchaseReturnDetails] ([ID], [PurchaseReturnHeaderId], [SubStoreBatchesId], [ReturnQTY], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [discount], [Price], [TenantId])
SELECT [ID], [PurchaseReturnHeaderId], [SubStoreBatchesId], [ReturnQTY], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [discount], [Price], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[PurchaseReturnDetails];
SET IDENTITY_INSERT [Pharmacy].[PurchaseReturnDetails] OFF;
GO

PRINT 'Migrating [Pharmacy].[PurchaseReturnHeader]...';
SET IDENTITY_INSERT [Pharmacy].[PurchaseReturnHeader] ON;
INSERT INTO [Pharmacy].[PurchaseReturnHeader] ([ID], [PurchaseReturnNumber], [ReturnDate], [TotalAmount], [Remarks], [SubstoreID], [Status], [GrnNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [EntryCode], [TenantId])
SELECT [ID], [PurchaseReturnNumber], [ReturnDate], [TotalAmount], [Remarks], [SubstoreID], [Status], [GrnNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [EntryCode], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[PurchaseReturnHeader];
SET IDENTITY_INSERT [Pharmacy].[PurchaseReturnHeader] OFF;
GO

PRINT 'Migrating [Pharmacy].[ReOrderHistoryDetails]...';
SET IDENTITY_INSERT [Pharmacy].[ReOrderHistoryDetails] ON;
INSERT INTO [Pharmacy].[ReOrderHistoryDetails] ([ID], [DrugID], [MasterID], [StockOnHand], [Shortage], [awaitQty], [ReOrderQTY], [ReorderUnit], [TenantId])
SELECT [ID], [DrugID], [MasterID], [StockOnHand], [Shortage], [awaitQty], [ReOrderQTY], [ReorderUnit], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[ReOrderHistoryDetails];
SET IDENTITY_INSERT [Pharmacy].[ReOrderHistoryDetails] OFF;
GO

PRINT 'Migrating [Pharmacy].[ReOrderHistoryMaster]...';
SET IDENTITY_INSERT [Pharmacy].[ReOrderHistoryMaster] ON;
INSERT INTO [Pharmacy].[ReOrderHistoryMaster] ([ID], [purchaseReqId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Code], [slowMovement], [TenantId])
SELECT [ID], [purchaseReqId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Code], [slowMovement], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[ReOrderHistoryMaster];
SET IDENTITY_INSERT [Pharmacy].[ReOrderHistoryMaster] OFF;
GO

PRINT 'Migrating [Pharmacy].[RefundDrugsDetails]...';
SET IDENTITY_INSERT [Pharmacy].[RefundDrugsDetails] ON;
INSERT INTO [Pharmacy].[RefundDrugsDetails] ([ID], [ReturnReceiptHeaderId], [ReceptDetailsId], [ReturnQTY], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ReturnReceiptHeaderId], [ReceptDetailsId], [ReturnQTY], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[RefundDrugsDetails];
SET IDENTITY_INSERT [Pharmacy].[RefundDrugsDetails] OFF;
GO

PRINT 'Migrating [Pharmacy].[RefundDrugsHeader]...';
SET IDENTITY_INSERT [Pharmacy].[RefundDrugsHeader] ON;
INSERT INTO [Pharmacy].[RefundDrugsHeader] ([ID], [ReceiptNo], [ReturnReceiptNo], [ReturnReceiptDate], [ReceiptDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ReceiptNo], [ReturnReceiptNo], [ReturnReceiptDate], [ReceiptDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[RefundDrugsHeader];
SET IDENTITY_INSERT [Pharmacy].[RefundDrugsHeader] OFF;
GO

PRINT 'Migrating [Pharmacy].[RequestSuppliesDetails]...';
SET IDENTITY_INSERT [Pharmacy].[RequestSuppliesDetails] ON;
INSERT INTO [Pharmacy].[RequestSuppliesDetails] ([Id], [RequestSuppliesHeaderID], [DrugID], [UnitConversionID], [ReturnedQty], [Quantity], [IsIncluded], [SubstoreBatchId], [IsApproved], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Price], [Amount], [AmountBefore], [AmountAfter], [SponserId], [EntryCode], [Paid], [Discount], [ApprovedId], [Cash], [Visa], [VisaReceipt], [PateintPaidAmount], [SponserPaidAmount], [ISCash], [ServiceID], [PrescriptionIdDetails], [insuranceId], [PatVat], [SpoVat], [patientlimitdtl], [Dosage], [FrequencyID], [TenantId])
SELECT [Id], [RequestSuppliesHeaderID], [DrugID], [UnitConversionID], [ReturnedQty], [Quantity], [IsIncluded], [SubstoreBatchId], [IsApproved], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Price], [Amount], [AmountBefore], [AmountAfter], [SponserId], [EntryCode], [Paid], [Discount], [ApprovedId], [Cash], [Visa], [VisaReceipt], [PateintPaidAmount], [SponserPaidAmount], [ISCash], [ServiceID], [PrescriptionIdDetails], [insuranceId], [PatVat], [SpoVat], [patientlimitdtl], [Dosage], [FrequencyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[RequestSuppliesDetails];
SET IDENTITY_INSERT [Pharmacy].[RequestSuppliesDetails] OFF;
GO

PRINT 'Migrating [Pharmacy].[RequestSuppliesHeader]...';
SET IDENTITY_INSERT [Pharmacy].[RequestSuppliesHeader] ON;
INSERT INTO [Pharmacy].[RequestSuppliesHeader] ([Id], [PatientID], [PatientTypeID], [OP_IPNo], [RequestNo], [DoctorID], [RequestStatus], [SubStoreID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Status], [OutPatName], [outPrescriptionDate], [PrescriptionHeaderID], [InsuranceId], [branchId], [DiscountPercent], [DiscountAmount], [TenantId])
SELECT [Id], [PatientID], [PatientTypeID], [OP_IPNo], [RequestNo], [DoctorID], [RequestStatus], [SubStoreID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Status], [OutPatName], [outPrescriptionDate], [PrescriptionHeaderID], [InsuranceId], [branchId], [DiscountPercent], [DiscountAmount], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[RequestSuppliesHeader];
SET IDENTITY_INSERT [Pharmacy].[RequestSuppliesHeader] OFF;
GO

PRINT 'Migrating [Pharmacy].[RequestSupplyReturnDetails]...';
SET IDENTITY_INSERT [Pharmacy].[RequestSupplyReturnDetails] ON;
INSERT INTO [Pharmacy].[RequestSupplyReturnDetails] ([ID], [RequestSupplyReturnHeaderID], [RequestSupplyDetailsID], [ReturnedQty], [UnitConversionID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [PateintPaidAmount], [SponserPaidAmount], [SponserId], [TenantId])
SELECT [ID], [RequestSupplyReturnHeaderID], [RequestSupplyDetailsID], [ReturnedQty], [UnitConversionID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [PateintPaidAmount], [SponserPaidAmount], [SponserId], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[RequestSupplyReturnDetails];
SET IDENTITY_INSERT [Pharmacy].[RequestSupplyReturnDetails] OFF;
GO

PRINT 'Migrating [Pharmacy].[RequestSupplyReturnHeader]...';
SET IDENTITY_INSERT [Pharmacy].[RequestSupplyReturnHeader] ON;
INSERT INTO [Pharmacy].[RequestSupplyReturnHeader] ([ID], [RequestSupplyID], [RequestSupplyRetuenCode], [ReturnDate], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [EntryCodes], [CompanyID], [TenantId])
SELECT [ID], [RequestSupplyID], [RequestSupplyRetuenCode], [ReturnDate], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [EntryCodes], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[RequestSupplyReturnHeader];
SET IDENTITY_INSERT [Pharmacy].[RequestSupplyReturnHeader] OFF;
GO

PRINT 'Migrating [Pharmacy].[ReturningExpiryItemsDetails]...';
SET IDENTITY_INSERT [Pharmacy].[ReturningExpiryItemsDetails] ON;
INSERT INTO [Pharmacy].[ReturningExpiryItemsDetails] ([ID], [ReturningExpiryItemsHeaderID], [ExpiryDate], [DrugID], [SubStoreBatchId], [ReturnQTY], [UnitPrice], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SetEntryCode], [TenantId])
SELECT [ID], [ReturningExpiryItemsHeaderID], [ExpiryDate], [DrugID], [SubStoreBatchId], [ReturnQTY], [UnitPrice], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SetEntryCode], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[ReturningExpiryItemsDetails];
SET IDENTITY_INSERT [Pharmacy].[ReturningExpiryItemsDetails] OFF;
GO

PRINT 'Migrating [Pharmacy].[ReturningExpiryItemsHeader]...';
SET IDENTITY_INSERT [Pharmacy].[ReturningExpiryItemsHeader] ON;
INSERT INTO [Pharmacy].[ReturningExpiryItemsHeader] ([ID], [SubStoreID], [SupplierID], [ReturnNO], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [SubStoreID], [SupplierID], [ReturnNO], [Amount], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[ReturningExpiryItemsHeader];
SET IDENTITY_INSERT [Pharmacy].[ReturningExpiryItemsHeader] OFF;
GO

PRINT 'Migrating [Pharmacy].[Route]...';
SET IDENTITY_INSERT [Pharmacy].[Route] ON;
INSERT INTO [Pharmacy].[Route] ([Id], [Value], [Description], [TenantId])
SELECT [Id], [Value], [Description], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[Route];
SET IDENTITY_INSERT [Pharmacy].[Route] OFF;
GO

PRINT 'Migrating [Pharmacy].[ScrapDetail]...';
SET IDENTITY_INSERT [Pharmacy].[ScrapDetail] ON;
INSERT INTO [Pharmacy].[ScrapDetail] ([ID], [ScrapHeaderId], [DrugID], [StockBatchId], [Date], [UnitConversionID], [CurrentQty], [ScrapQty], [TotalReturnQty], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ScrapHeaderId], [DrugID], [StockBatchId], [Date], [UnitConversionID], [CurrentQty], [ScrapQty], [TotalReturnQty], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[ScrapDetail];
SET IDENTITY_INSERT [Pharmacy].[ScrapDetail] OFF;
GO

PRINT 'Migrating [Pharmacy].[ScrapHeader]...';
SET IDENTITY_INSERT [Pharmacy].[ScrapHeader] ON;
INSERT INTO [Pharmacy].[ScrapHeader] ([ID], [MainStockID], [ScrapNumber], [ScrapDate], [StockID], [Remarks], [Status], [IssueRequestID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [EntryCodes], [TenantId])
SELECT [ID], [MainStockID], [ScrapNumber], [ScrapDate], [StockID], [Remarks], [Status], [IssueRequestID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [EntryCodes], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[ScrapHeader];
SET IDENTITY_INSERT [Pharmacy].[ScrapHeader] OFF;
GO

PRINT 'Migrating [Pharmacy].[ScrapReturnDetail]...';
SET IDENTITY_INSERT [Pharmacy].[ScrapReturnDetail] ON;
INSERT INTO [Pharmacy].[ScrapReturnDetail] ([ID], [HeaderID], [DrugID], [QTY], [Price], [Amount], [SubStockBatchID], [UnitConversionFactorId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [ID], [HeaderID], [DrugID], [QTY], [Price], [Amount], [SubStockBatchID], [UnitConversionFactorId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[ScrapReturnDetail];
SET IDENTITY_INSERT [Pharmacy].[ScrapReturnDetail] OFF;
GO

PRINT 'Migrating [Pharmacy].[ScrapReturnHeader]...';
SET IDENTITY_INSERT [Pharmacy].[ScrapReturnHeader] ON;
INSERT INTO [Pharmacy].[ScrapReturnHeader] ([ID], [OperationNo], [OperationDate], [SubStockID], [SubStockToID], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [EntryCodes], [TenantId])
SELECT [ID], [OperationNo], [OperationDate], [SubStockID], [SubStockToID], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [EntryCodes], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[ScrapReturnHeader];
SET IDENTITY_INSERT [Pharmacy].[ScrapReturnHeader] OFF;
GO

PRINT 'Migrating [Pharmacy].[StdDosage]...';
SET IDENTITY_INSERT [Pharmacy].[StdDosage] ON;
INSERT INTO [Pharmacy].[StdDosage] ([ID], [DrugID], [AgeFrom], [AgeTo], [Dosage], [FrequencyID], [Period], [Type], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [DrugID], [AgeFrom], [AgeTo], [Dosage], [FrequencyID], [Period], [Type], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[StdDosage];
SET IDENTITY_INSERT [Pharmacy].[StdDosage] OFF;
GO

PRINT 'Migrating [Pharmacy].[StockAdjustmentReasons]...';
SET IDENTITY_INSERT [Pharmacy].[StockAdjustmentReasons] ON;
INSERT INTO [Pharmacy].[StockAdjustmentReasons] ([ID], [NameEN], [NameAR], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [NameEN], [NameAR], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[StockAdjustmentReasons];
SET IDENTITY_INSERT [Pharmacy].[StockAdjustmentReasons] OFF;
GO

PRINT 'Migrating [Pharmacy].[StockControlDetail]...';
SET IDENTITY_INSERT [Pharmacy].[StockControlDetail] ON;
INSERT INTO [Pharmacy].[StockControlDetail] ([ID], [StockControlID], [Qty], [Price], [Balance], [UnitID], [ConvertionFactor], [GRNDetailID], [GRNDetailBalance], [SignValue], [TransactionDate], [TransactionType], [TransactionNotes], [UserID], [TenantId])
SELECT [ID], [StockControlID], [Qty], [Price], [Balance], [UnitID], [ConvertionFactor], [GRNDetailID], [GRNDetailBalance], [SignValue], [TransactionDate], [TransactionType], [TransactionNotes], [UserID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[StockControlDetail];
SET IDENTITY_INSERT [Pharmacy].[StockControlDetail] OFF;
GO

PRINT 'Migrating [Pharmacy].[StockControl]...';
SET IDENTITY_INSERT [Pharmacy].[StockControl] ON;
INSERT INTO [Pharmacy].[StockControl] ([ID], [DrugID], [SubStoreID], [StockOnHand], [MaxQuantity], [MinQuantity], [RecordLevel], [RecordQuantity], [Accessability], [UnitConversionFactorId_ForPurchase], [UnitConversionFactorId_ForIssue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ReOrder], [CriticalQTY], [ReOrderQTY], [ReorderUnit], [TenantId])
SELECT [ID], [DrugID], [SubStoreID], [StockOnHand], [MaxQuantity], [MinQuantity], [RecordLevel], [RecordQuantity], [Accessability], [UnitConversionFactorId_ForPurchase], [UnitConversionFactorId_ForIssue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [ReOrder], [CriticalQTY], [ReOrderQTY], [ReorderUnit], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[StockControl];
SET IDENTITY_INSERT [Pharmacy].[StockControl] OFF;
GO

PRINT 'Migrating [Pharmacy].[StockTransferEntry]...';
SET IDENTITY_INSERT [Pharmacy].[StockTransferEntry] ON;
INSERT INTO [Pharmacy].[StockTransferEntry] ([ID], [StockTransferID], [DrugID], [IssuedQTY], [DespatchQTY], [OrderedQTY], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [StockTransferID], [DrugID], [IssuedQTY], [DespatchQTY], [OrderedQTY], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[StockTransferEntry];
SET IDENTITY_INSERT [Pharmacy].[StockTransferEntry] OFF;
GO

PRINT 'Migrating [Pharmacy].[StockTransfer]...';
SET IDENTITY_INSERT [Pharmacy].[StockTransfer] ON;
INSERT INTO [Pharmacy].[StockTransfer] ([ID], [RequestedStoreID], [RequestedDate], [IssuingStoreID], [RefNo], [Status], [RequestNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [RequestedStoreID], [RequestedDate], [IssuingStoreID], [RefNo], [Status], [RequestNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[StockTransfer];
SET IDENTITY_INSERT [Pharmacy].[StockTransfer] OFF;
GO

PRINT 'Migrating [Pharmacy].[StrenghtUnitMaster]...';
SET IDENTITY_INSERT [Pharmacy].[StrenghtUnitMaster] ON;
INSERT INTO [Pharmacy].[StrenghtUnitMaster] ([ID], [Code], [NameArabic], [NameEnglish], [Status], [CompanyID], [TenantId])
SELECT [ID], [Code], [NameArabic], [NameEnglish], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[StrenghtUnitMaster];
SET IDENTITY_INSERT [Pharmacy].[StrenghtUnitMaster] OFF;
GO

PRINT 'Migrating [Pharmacy].[SubStoreBatches]...';
SET IDENTITY_INSERT [Pharmacy].[SubStoreBatches] ON;
INSERT INTO [Pharmacy].[SubStoreBatches] ([Id], [BatchId], [SubStoreID], [ReceivedQty], [AcceptedQty], [ExcessQty], [Bonus], [AvailableQty], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IssueBatchID], [TenantId])
SELECT [Id], [BatchId], [SubStoreID], [ReceivedQty], [AcceptedQty], [ExcessQty], [Bonus], [AvailableQty], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IssueBatchID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[SubStoreBatches];
SET IDENTITY_INSERT [Pharmacy].[SubStoreBatches] OFF;
GO

PRINT 'Migrating [Pharmacy].[SubStoresClassifications]...';
SET IDENTITY_INSERT [Pharmacy].[SubStoresClassifications] ON;
INSERT INTO [Pharmacy].[SubStoresClassifications] ([Id], [ClassificationID], [StoreID], [TenantId])
SELECT [Id], [ClassificationID], [StoreID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[SubStoresClassifications];
SET IDENTITY_INSERT [Pharmacy].[SubStoresClassifications] OFF;
GO

PRINT 'Migrating [Pharmacy].[SubstoreAuthority]...';
SET IDENTITY_INSERT [Pharmacy].[SubstoreAuthority] ON;
INSERT INTO [Pharmacy].[SubstoreAuthority] ([ID], [SubStoreID], [UserID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IsLPO], [IsEPO], [IsGRN], [IsCashGRN], [IsAddition], [IsDispense], [isReturn], [IsUpdateStock], [IsIToD], [IsIToPharmacy], [ReturnToPatient], [ReturnToSupplier], [TenantId])
SELECT [ID], [SubStoreID], [UserID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IsLPO], [IsEPO], [IsGRN], [IsCashGRN], [IsAddition], [IsDispense], [isReturn], [IsUpdateStock], [IsIToD], [IsIToPharmacy], [ReturnToPatient], [ReturnToSupplier], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[SubstoreAuthority];
SET IDENTITY_INSERT [Pharmacy].[SubstoreAuthority] OFF;
GO

PRINT 'Migrating [Pharmacy].[Substore_Items]...';
SET IDENTITY_INSERT [Pharmacy].[Substore_Items] ON;
INSERT INTO [Pharmacy].[Substore_Items] ([ID], [SubstoreID], [ItemID], [Quantity], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [SubstoreID], [ItemID], [Quantity], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[Substore_Items];
SET IDENTITY_INSERT [Pharmacy].[Substore_Items] OFF;
GO

PRINT 'Migrating [Pharmacy].[Substores]...';
SET IDENTITY_INSERT [Pharmacy].[Substores] ON;
INSERT INTO [Pharmacy].[Substores] ([ID], [SubstoreName], [CostCenterCompaniesID], [AllowSupplierTransactions], [IsDispensingOfDrugs], [IsMainStore], [IsActive], [PatientType], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [MainStockID], [MainAccount_GeneralStock], [SubAccount_GeneralStock], [MainAccount_CostOfGoods], [SubAccount_CostOfGoods], [MainAccount_SalesRevenue], [SubAccount_SalesRevenue], [MainAccount_SettlementByDiscount], [SubAccount_SettlementByDiscount], [MainAccount_SettlementAsWell], [SubAccount_SettlementAsWell], [MainAccount_OutgoingMovements], [SubAccount_OutgoingMovements], [SubstoreCode], [SubstoreNameAr], [Location], [UserCharge], [phone], [WardPharm], [StockType], [IsScrap], [IsDrugStore], [IsProhibitedDrug], [BranchID], [TenantId])
SELECT [ID], [SubstoreName], [CostCenterCompaniesID], [AllowSupplierTransactions], [IsDispensingOfDrugs], [IsMainStore], [IsActive], [PatientType], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [MainStockID], [MainAccount_GeneralStock], [SubAccount_GeneralStock], [MainAccount_CostOfGoods], [SubAccount_CostOfGoods], [MainAccount_SalesRevenue], [SubAccount_SalesRevenue], [MainAccount_SettlementByDiscount], [SubAccount_SettlementByDiscount], [MainAccount_SettlementAsWell], [SubAccount_SettlementAsWell], [MainAccount_OutgoingMovements], [SubAccount_OutgoingMovements], [SubstoreCode], [SubstoreNameAr], [Location], [UserCharge], [phone], [WardPharm], [StockType], [IsScrap], [IsDrugStore], [IsProhibitedDrug], [BranchID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[Substores];
SET IDENTITY_INSERT [Pharmacy].[Substores] OFF;
GO

PRINT 'Migrating [Pharmacy].[SupplierContacts]...';
SET IDENTITY_INSERT [Pharmacy].[SupplierContacts] ON;
INSERT INTO [Pharmacy].[SupplierContacts] ([ID], [SupplierID], [ContactName], [ContactLocation], [SupplierType], [ContactType], [ContactDescription], [Address], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Email], [Phone], [Fax], [TenantId])
SELECT [ID], [SupplierID], [ContactName], [ContactLocation], [SupplierType], [ContactType], [ContactDescription], [Address], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Email], [Phone], [Fax], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[SupplierContacts];
SET IDENTITY_INSERT [Pharmacy].[SupplierContacts] OFF;
GO

PRINT 'Migrating [Pharmacy].[SupplierRelatedCompanies]...';
SET IDENTITY_INSERT [Pharmacy].[SupplierRelatedCompanies] ON;
INSERT INTO [Pharmacy].[SupplierRelatedCompanies] ([ID], [SupplierRelatedCompaniesName], [SupplierID], [CompanyID], [TenantId])
SELECT [ID], [SupplierRelatedCompaniesName], [SupplierID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[SupplierRelatedCompanies];
SET IDENTITY_INSERT [Pharmacy].[SupplierRelatedCompanies] OFF;
GO

PRINT 'Migrating [Pharmacy].[SupplierType]...';
SET IDENTITY_INSERT [Pharmacy].[SupplierType] ON;
INSERT INTO [Pharmacy].[SupplierType] ([SupplierTypeID], [SupplierTypeNameEN], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SupplierTypeNameAR], [TenantId])
SELECT [SupplierTypeID], [SupplierTypeNameEN], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SupplierTypeNameAR], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[SupplierType];
SET IDENTITY_INSERT [Pharmacy].[SupplierType] OFF;
GO

PRINT 'Migrating [Pharmacy].[SupplierforDrug]...';
SET IDENTITY_INSERT [Pharmacy].[SupplierforDrug] ON;
INSERT INTO [Pharmacy].[SupplierforDrug] ([ID], [DrugID], [SupplierID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [DrugID], [SupplierID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[SupplierforDrug];
SET IDENTITY_INSERT [Pharmacy].[SupplierforDrug] OFF;
GO

PRINT 'Migrating [Pharmacy].[Suppliers]...';
SET IDENTITY_INSERT [Pharmacy].[Suppliers] ON;
INSERT INTO [Pharmacy].[Suppliers] ([ID], [SupplierCode], [SupplierName], [SupplierTypeID], [CurrencyID], [Status], [ShippingModeID], [DeliveryTermID], [DeliveryTime], [DeliveryTimeType], [CreditLimit], [CreditPeriod], [CreditPeriodType], [PaymentTime], [PaymentTimeType], [PaymentModeID], [PaymentGroupID], [PaymentTermsID], [Address1], [Address2], [Address3], [POBox], [City], [Country], [ZipCode], [Email], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [MainAcc], [SubAcc], [CompanyID], [SupplierNameAr], [Parent_Id], [AgentID], [StopPayment], [HospitalNumForSupplier], [International_Local], [Matchingmethod], [allowtoacceptalternativitem], [Stoppingsupplierpayments], [Reason], [PaymentTerms], [Differencepaymentdate], [Prioritypayment], [Aretaxesdeducted], [AcceptedRounding], [IsItPossibleToRequestQuote], [EmployeeName], [PhonNumber], [MobileNumber], [EmployeeAddress], [EmployeeEmail], [MainAccountToSupplier], [CalcSuppliertax], [IsDiscountNoticeGiven], [AcceptGoodsWithoutPurchaseOrder], [StopPurchaseOrders], [DetermineTypeOfReceipt], [IsSovereignSide], [TaxRegistrationNumber], [WithHoldingTax], [TenantId])
SELECT [ID], [SupplierCode], [SupplierName], [SupplierTypeID], [CurrencyID], [Status], [ShippingModeID], [DeliveryTermID], [DeliveryTime], [DeliveryTimeType], [CreditLimit], [CreditPeriod], [CreditPeriodType], [PaymentTime], [PaymentTimeType], [PaymentModeID], [PaymentGroupID], [PaymentTermsID], [Address1], [Address2], [Address3], [POBox], [City], [Country], [ZipCode], [Email], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [MainAcc], [SubAcc], [CompanyID], [SupplierNameAr], [Parent_Id], [AgentID], [StopPayment], [HospitalNumForSupplier], [International_Local], [Matchingmethod], [allowtoacceptalternativitem], [Stoppingsupplierpayments], [Reason], [PaymentTerms], [Differencepaymentdate], [Prioritypayment], [Aretaxesdeducted], [AcceptedRounding], [IsItPossibleToRequestQuote], [EmployeeName], [PhonNumber], [MobileNumber], [EmployeeAddress], [EmployeeEmail], [MainAccountToSupplier], [CalcSuppliertax], [IsDiscountNoticeGiven], [AcceptGoodsWithoutPurchaseOrder], [StopPurchaseOrders], [DetermineTypeOfReceipt], [IsSovereignSide], [TaxRegistrationNumber], [WithHoldingTax], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[Suppliers];
SET IDENTITY_INSERT [Pharmacy].[Suppliers] OFF;
GO

PRINT 'Migrating [Pharmacy].[Template]...';
SET IDENTITY_INSERT [Pharmacy].[Template] ON;
INSERT INTO [Pharmacy].[Template] ([Id], [TemplateCode], [TemplateName], [CompanyID], [TenantId])
SELECT [Id], [TemplateCode], [TemplateName], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[Template];
SET IDENTITY_INSERT [Pharmacy].[Template] OFF;
GO

PRINT 'Migrating [Pharmacy].[UnitConversionFactor]...';
SET IDENTITY_INSERT [Pharmacy].[UnitConversionFactor] ON;
INSERT INTO [Pharmacy].[UnitConversionFactor] ([ID], [UnitTemplateID], [UnitID], [ConversionValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [UnitTemplateID], [UnitID], [ConversionValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[UnitConversionFactor];
SET IDENTITY_INSERT [Pharmacy].[UnitConversionFactor] OFF;
GO

PRINT 'Migrating [Pharmacy].[UnitTemplate]...';
SET IDENTITY_INSERT [Pharmacy].[UnitTemplate] ON;
INSERT INTO [Pharmacy].[UnitTemplate] ([ID], [UnitTemplateCode], [UnitTemplateNameAr], [UnitTemplateName], [BaseUnitID], [SubStoreID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [UnitTemplateCode], [UnitTemplateNameAr], [UnitTemplateName], [BaseUnitID], [SubStoreID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[UnitTemplate];
SET IDENTITY_INSERT [Pharmacy].[UnitTemplate] OFF;
GO

PRINT 'Migrating [Pharmacy].[Units]...';
SET IDENTITY_INSERT [Pharmacy].[Units] ON;
INSERT INTO [Pharmacy].[Units] ([ID], [UnitNameArabic], [UnitName], [SubUnitNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Code], [TenantId])
SELECT [ID], [UnitNameArabic], [UnitName], [SubUnitNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Code], @TenantId
FROM [SunCity_Clinics].[Pharmacy].[Units];
SET IDENTITY_INSERT [Pharmacy].[Units] OFF;
GO

PRINT 'Migrating [Radiology].[DeviceServices]...';
SET IDENTITY_INSERT [Radiology].[DeviceServices] ON;
INSERT INTO [Radiology].[DeviceServices] ([Id], [DeviceId], [ServiceId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Active], [HostCode], [DeviceValue], [CompanyID], [BranchId], [TenantId])
SELECT [Id], [DeviceId], [ServiceId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Active], [HostCode], [DeviceValue], [CompanyID], [BranchId], @TenantId
FROM [SunCity_Clinics].[Radiology].[DeviceServices];
SET IDENTITY_INSERT [Radiology].[DeviceServices] OFF;
GO

PRINT 'Migrating [Radiology].[DeviceTechnician]...';
SET IDENTITY_INSERT [Radiology].[DeviceTechnician] ON;
INSERT INTO [Radiology].[DeviceTechnician] ([ID], [TechnicianID], [Device], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [TechnicianID], [Device], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Radiology].[DeviceTechnician];
SET IDENTITY_INSERT [Radiology].[DeviceTechnician] OFF;
GO

PRINT 'Migrating [Radiology].[DevicesDefination]...';
SET IDENTITY_INSERT [Radiology].[DevicesDefination] ON;
INSERT INTO [Radiology].[DevicesDefination] ([ID], [Code], [Serial], [NameEn], [NameAr], [Location], [DeviceType], [Emergency], [ISActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [CommSetting], [CommPort], [FormName], [IPAddres], [investigationgroup], [BranchId], [TenantId])
SELECT [ID], [Code], [Serial], [NameEn], [NameAr], [Location], [DeviceType], [Emergency], [ISActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [CommSetting], [CommPort], [FormName], [IPAddres], [investigationgroup], [BranchId], @TenantId
FROM [SunCity_Clinics].[Radiology].[DevicesDefination];
SET IDENTITY_INSERT [Radiology].[DevicesDefination] OFF;
GO

PRINT 'Migrating [Radiology].[DevicsSchedule]...';
SET IDENTITY_INSERT [Radiology].[DevicsSchedule] ON;
INSERT INTO [Radiology].[DevicsSchedule] ([ID], [DeviceID], [UserID], [HolidayID], [WorkFromTime], [WorkToTime], [WorkFromDate], [WorkToDate], [OffFromTime], [OffToTime], [OffFromDate], [OffToDate], [StatusEnum], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Code], [CompanyID], [TenantId])
SELECT [ID], [DeviceID], [UserID], [HolidayID], [WorkFromTime], [WorkToTime], [WorkFromDate], [WorkToDate], [OffFromTime], [OffToTime], [OffFromDate], [OffToDate], [StatusEnum], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Code], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Radiology].[DevicsSchedule];
SET IDENTITY_INSERT [Radiology].[DevicsSchedule] OFF;
GO

PRINT 'Migrating [Radiology].[DirectRad_Diagnosis]...';
SET IDENTITY_INSERT [Radiology].[DirectRad_Diagnosis] ON;
INSERT INTO [Radiology].[DirectRad_Diagnosis] ([id], [InvestegationReqID], [ICDCodeID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [id], [InvestegationReqID], [ICDCodeID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Radiology].[DirectRad_Diagnosis];
SET IDENTITY_INSERT [Radiology].[DirectRad_Diagnosis] OFF;
GO

PRINT 'Migrating [Radiology].[ExamDelivery]...';
SET IDENTITY_INSERT [Radiology].[ExamDelivery] ON;
INSERT INTO [Radiology].[ExamDelivery] ([ID], [PatientID], [IPNumber], [AccessionNo], [UserID], [ISReport], [ISCD], [ISFilm], [DeliveryTo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [IPNumber], [AccessionNo], [UserID], [ISReport], [ISCD], [ISFilm], [DeliveryTo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Radiology].[ExamDelivery];
SET IDENTITY_INSERT [Radiology].[ExamDelivery] OFF;
GO

PRINT 'Migrating [Radiology].[ExamRequest]...';
SET IDENTITY_INSERT [Radiology].[ExamRequest] ON;
INSERT INTO [Radiology].[ExamRequest] ([ID], [PatientID], [IPNumber], [AccessionNo], [ExamDate], [DeviceID], [Status], [ExamID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [UserID], [DoctorID], [Notes], [FlagsID], [ReservedDateFrom], [ReservedDateTo], [ReservedTimeFrom], [ReservedTimeTo], [TimeStartExam], [TimeEndExam], [ReportNotes], [ServiceId], [RequestDate], [RequestNumber], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [IPNumber], [AccessionNo], [ExamDate], [DeviceID], [Status], [ExamID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [UserID], [DoctorID], [Notes], [FlagsID], [ReservedDateFrom], [ReservedDateTo], [ReservedTimeFrom], [ReservedTimeTo], [TimeStartExam], [TimeEndExam], [ReportNotes], [ServiceId], [RequestDate], [RequestNumber], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Radiology].[ExamRequest];
SET IDENTITY_INSERT [Radiology].[ExamRequest] OFF;
GO

PRINT 'Migrating [Radiology].[Exams]...';
SET IDENTITY_INSERT [Radiology].[Exams] ON;
INSERT INTO [Radiology].[Exams] ([ID], [Code], [LatinName], [LocalName], [ISActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DeviceID], [Minutes], [InvestegationGroupEnum], [ISpregnant], [ISDiabetic], [CompanyID], [TenantId])
SELECT [ID], [Code], [LatinName], [LocalName], [ISActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DeviceID], [Minutes], [InvestegationGroupEnum], [ISpregnant], [ISDiabetic], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Radiology].[Exams];
SET IDENTITY_INSERT [Radiology].[Exams] OFF;
GO

PRINT 'Migrating [Radiology].[FlagSettings]...';
SET IDENTITY_INSERT [Radiology].[FlagSettings] ON;
INSERT INTO [Radiology].[FlagSettings] ([ID], [Code], [LatinName], [LocalName], [ToolTip], [FlagIcon], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [LatinName], [LocalName], [ToolTip], [FlagIcon], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Radiology].[FlagSettings];
SET IDENTITY_INSERT [Radiology].[FlagSettings] OFF;
GO

PRINT 'Migrating [Radiology].[LocationDefination]...';
SET IDENTITY_INSERT [Radiology].[LocationDefination] ON;
INSERT INTO [Radiology].[LocationDefination] ([ID], [Code], [LatinName], [LocalName], [ISActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [NumberOFPatient], [CompanyID], [TenantId])
SELECT [ID], [Code], [LatinName], [LocalName], [ISActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [NumberOFPatient], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Radiology].[LocationDefination];
SET IDENTITY_INSERT [Radiology].[LocationDefination] OFF;
GO

PRINT 'Migrating [Radiology].[MostCommenInvestigations]...';
SET IDENTITY_INSERT [Radiology].[MostCommenInvestigations] ON;
INSERT INTO [Radiology].[MostCommenInvestigations] ([ID], [InvestigationGroupEnum], [InvestigationID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [InvestigationGroupEnum], [InvestigationID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Radiology].[MostCommenInvestigations];
SET IDENTITY_INSERT [Radiology].[MostCommenInvestigations] OFF;
GO

PRINT 'Migrating [Radiology].[PatientRadReception]...';
SET IDENTITY_INSERT [Radiology].[PatientRadReception] ON;
INSERT INTO [Radiology].[PatientRadReception] ([ID], [Code], [TechnicianID], [DeviceID], [ReceptionID], [PatientID], [PatientOPIP], [RadStartDate], [status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [InvestigationRequestDetailsID], [Approve], [ChangeReason], [BranchId], [TenantId])
SELECT [ID], [Code], [TechnicianID], [DeviceID], [ReceptionID], [PatientID], [PatientOPIP], [RadStartDate], [status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [InvestigationRequestDetailsID], [Approve], [ChangeReason], [BranchId], @TenantId
FROM [SunCity_Clinics].[Radiology].[PatientRadReception];
SET IDENTITY_INSERT [Radiology].[PatientRadReception] OFF;
GO

PRINT 'Migrating [Radiology].[RadReceptionProcedures]...';
SET IDENTITY_INSERT [Radiology].[RadReceptionProcedures] ON;
INSERT INTO [Radiology].[RadReceptionProcedures] ([ID], [ServicegroupID], [ProcedureSlot], [receptionID], [CreatedBy], [Creationdate], [ModifiedBy], [ModificationDate], [TenantId])
SELECT [ID], [ServicegroupID], [ProcedureSlot], [receptionID], [CreatedBy], [Creationdate], [ModifiedBy], [ModificationDate], @TenantId
FROM [SunCity_Clinics].[Radiology].[RadReceptionProcedures];
SET IDENTITY_INSERT [Radiology].[RadReceptionProcedures] OFF;
GO

PRINT 'Migrating [Radiology].[RadReceptioniestSchedule]...';
SET IDENTITY_INSERT [Radiology].[RadReceptioniestSchedule] ON;
INSERT INTO [Radiology].[RadReceptioniestSchedule] ([ID], [DayID], [EmpID], [Status], [ReceptionID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [SessionId], [CompanyID], [TenantId])
SELECT [ID], [DayID], [EmpID], [Status], [ReceptionID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [SessionId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Radiology].[RadReceptioniestSchedule];
SET IDENTITY_INSERT [Radiology].[RadReceptioniestSchedule] OFF;
GO

PRINT 'Migrating [Radiology].[RadResultImages]...';
SET IDENTITY_INSERT [Radiology].[RadResultImages] ON;
INSERT INTO [Radiology].[RadResultImages] ([ID], [ResultEntryDetailsID], [ImageName], [TenantId])
SELECT [ID], [ResultEntryDetailsID], [ImageName], @TenantId
FROM [SunCity_Clinics].[Radiology].[RadResultImages];
SET IDENTITY_INSERT [Radiology].[RadResultImages] OFF;
GO

PRINT 'Migrating [Radiology].[RadiologyReceptions]...';
SET IDENTITY_INSERT [Radiology].[RadiologyReceptions] ON;
INSERT INTO [Radiology].[RadiologyReceptions] ([ID], [Code], [NameAr], [NameEn], [TimeSlot], [DepartmentID], [SpeciaityGroupID], [IsActive], [Location], [ReceptionTypeID], [SessionID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Emp_ReceptionAdmin], [CompanyID], [BranchId], [TenantId])
SELECT [ID], [Code], [NameAr], [NameEn], [TimeSlot], [DepartmentID], [SpeciaityGroupID], [IsActive], [Location], [ReceptionTypeID], [SessionID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Emp_ReceptionAdmin], [CompanyID], [BranchId], @TenantId
FROM [SunCity_Clinics].[Radiology].[RadiologyReceptions];
SET IDENTITY_INSERT [Radiology].[RadiologyReceptions] OFF;
GO

PRINT 'Migrating [Radiology].[RayBodyLoaction]...';
SET IDENTITY_INSERT [Radiology].[RayBodyLoaction] ON;
INSERT INTO [Radiology].[RayBodyLoaction] ([ID], [LocationAr], [LocationEn], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], [TenantId])
SELECT [ID], [LocationAr], [LocationEn], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Radiology].[RayBodyLoaction];
SET IDENTITY_INSERT [Radiology].[RayBodyLoaction] OFF;
GO

PRINT 'Migrating [Radiology].[ReceptionDevicsSchedule]...';
SET IDENTITY_INSERT [Radiology].[ReceptionDevicsSchedule] ON;
INSERT INTO [Radiology].[ReceptionDevicsSchedule] ([ID], [ReceptionID], [DeviceID], [DayID], [SessionID], [IsActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [ReceptionID], [DeviceID], [DayID], [SessionID], [IsActive], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Radiology].[ReceptionDevicsSchedule];
SET IDENTITY_INSERT [Radiology].[ReceptionDevicsSchedule] OFF;
GO

PRINT 'Migrating [Radiology].[ReceptionStocks]...';
SET IDENTITY_INSERT [Radiology].[ReceptionStocks] ON;
INSERT INTO [Radiology].[ReceptionStocks] ([ID], [ReceptionID], [StockID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IsActive], [TenantId])
SELECT [ID], [ReceptionID], [StockID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IsActive], @TenantId
FROM [SunCity_Clinics].[Radiology].[ReceptionStocks];
SET IDENTITY_INSERT [Radiology].[ReceptionStocks] OFF;
GO

PRINT 'Migrating [Radiology].[ResultEntryDetails]...';
SET IDENTITY_INSERT [Radiology].[ResultEntryDetails] ON;
INSERT INTO [Radiology].[ResultEntryDetails] ([Id], [ResultEntryId], [InvestigationDetailsId], [OPIPNO], [DoctorId], [TimeStudy], [Abnormal], [Redone], [Remarks], [Comments], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Findings], [Conclusion], [TenantId])
SELECT [Id], [ResultEntryId], [InvestigationDetailsId], [OPIPNO], [DoctorId], [TimeStudy], [Abnormal], [Redone], [Remarks], [Comments], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [Findings], [Conclusion], @TenantId
FROM [SunCity_Clinics].[Radiology].[ResultEntryDetails];
SET IDENTITY_INSERT [Radiology].[ResultEntryDetails] OFF;
GO

PRINT 'Migrating [Radiology].[ResultEntryDetails_Findings]...';
SET IDENTITY_INSERT [Radiology].[ResultEntryDetails_Findings] ON;
INSERT INTO [Radiology].[ResultEntryDetails_Findings] ([Id], [ResultEntryDetailsID], [DoctorID], [Findings], [Concolusion], [FindingDate], [Status], [RevisedById], [FinalApprovedById], [CompanyID], [TenantId])
SELECT [Id], [ResultEntryDetailsID], [DoctorID], [Findings], [Concolusion], [FindingDate], [Status], [RevisedById], [FinalApprovedById], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Radiology].[ResultEntryDetails_Findings];
SET IDENTITY_INSERT [Radiology].[ResultEntryDetails_Findings] OFF;
GO

PRINT 'Migrating [Radiology].[ResultEntryHeader]...';
SET IDENTITY_INSERT [Radiology].[ResultEntryHeader] ON;
INSERT INTO [Radiology].[ResultEntryHeader] ([Id], [PatientId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BrabchId], [TenantId])
SELECT [Id], [PatientId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [BrabchId], @TenantId
FROM [SunCity_Clinics].[Radiology].[ResultEntryHeader];
SET IDENTITY_INSERT [Radiology].[ResultEntryHeader] OFF;
GO

PRINT 'Migrating [Radiology].[Tests]...';
SET IDENTITY_INSERT [Radiology].[Tests] ON;
INSERT INTO [Radiology].[Tests] ([Id], [TestName], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [TestName], [Description], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Radiology].[Tests];
SET IDENTITY_INSERT [Radiology].[Tests] OFF;
GO

PRINT 'Migrating [Registration].[AdmissionRequest]...';
SET IDENTITY_INSERT [Registration].[AdmissionRequest] ON;
INSERT INTO [Registration].[AdmissionRequest] ([Id], [PatientID], [OPNumber], [RecommendedWard], [VacantBeds], [DoctorID], [SurgeryType], [DeliveryType], [PurposeOfAdmission], [AdmissionRequestType], [AdmitOn], [Time], [ExpectedDays], [ProvisionalDateOfSD], [DiagnosisAndClinicalData], [BillingCode], [InstructionsToAdmissionClerkForPreparationOfEstimate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [OPNumber], [RecommendedWard], [VacantBeds], [DoctorID], [SurgeryType], [DeliveryType], [PurposeOfAdmission], [AdmissionRequestType], [AdmitOn], [Time], [ExpectedDays], [ProvisionalDateOfSD], [DiagnosisAndClinicalData], [BillingCode], [InstructionsToAdmissionClerkForPreparationOfEstimate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[AdmissionRequest];
SET IDENTITY_INSERT [Registration].[AdmissionRequest] OFF;
GO

PRINT 'Migrating [Registration].[Age]...';
SET IDENTITY_INSERT [Registration].[Age] ON;
INSERT INTO [Registration].[Age] ([Id], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[Age];
SET IDENTITY_INSERT [Registration].[Age] OFF;
GO

PRINT 'Migrating [Registration].[Areas]...';
SET IDENTITY_INSERT [Registration].[Areas] ON;
INSERT INTO [Registration].[Areas] ([Id], [AreaName], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [AreaName], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[Areas];
SET IDENTITY_INSERT [Registration].[Areas] OFF;
GO

PRINT 'Migrating [Registration].[CallDoctor]...';
SET IDENTITY_INSERT [Registration].[CallDoctor] ON;
INSERT INTO [Registration].[CallDoctor] ([ID], [DoctorID], [PatientID], [DateTime], [Priorty], [SpitialityID], [WardID], [RoomID], [BedID], [CommunicationMethod], [Comment], [Status], [CompanyID], [TenantId])
SELECT [ID], [DoctorID], [PatientID], [DateTime], [Priorty], [SpitialityID], [WardID], [RoomID], [BedID], [CommunicationMethod], [Comment], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[CallDoctor];
SET IDENTITY_INSERT [Registration].[CallDoctor] OFF;
GO

PRINT 'Migrating [Registration].[ConfidentialityCode]...';
SET IDENTITY_INSERT [Registration].[ConfidentialityCode] ON;
INSERT INTO [Registration].[ConfidentialityCode] ([Id], [Value], [Description], [NameKa], [TenantId])
SELECT [Id], [Value], [Description], [NameKa], @TenantId
FROM [SunCity_Clinics].[Registration].[ConfidentialityCode];
SET IDENTITY_INSERT [Registration].[ConfidentialityCode] OFF;
GO

PRINT 'Migrating [Registration].[ConsultationCharges]...';
SET IDENTITY_INSERT [Registration].[ConsultationCharges] ON;
INSERT INTO [Registration].[ConsultationCharges] ([Id], [Amount], [AllowDiscount], [Discount], [DiscountType], [ConsultationType], [RevenueType], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [ServiceCode], [DoctorId], [DepartmentId], [CompanyID], [TenantId])
SELECT [Id], [Amount], [AllowDiscount], [Discount], [DiscountType], [ConsultationType], [RevenueType], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [ServiceCode], [DoctorId], [DepartmentId], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[ConsultationCharges];
SET IDENTITY_INSERT [Registration].[ConsultationCharges] OFF;
GO

PRINT 'Migrating [Registration].[DeficiencySetup]...';
SET IDENTITY_INSERT [Registration].[DeficiencySetup] ON;
INSERT INTO [Registration].[DeficiencySetup] ([Id], [Code], [DeficiencySetupName], [Completion], [Signature], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Code], [DeficiencySetupName], [Completion], [Signature], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[DeficiencySetup];
SET IDENTITY_INSERT [Registration].[DeficiencySetup] OFF;
GO

PRINT 'Migrating [Registration].[DoctorDegree]...';
SET IDENTITY_INSERT [Registration].[DoctorDegree] ON;
INSERT INTO [Registration].[DoctorDegree] ([Id], [Name], [Code], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameAr], [TenantId])
SELECT [Id], [Name], [Code], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameAr], @TenantId
FROM [SunCity_Clinics].[Registration].[DoctorDegree];
SET IDENTITY_INSERT [Registration].[DoctorDegree] OFF;
GO

PRINT 'Migrating [Registration].[DoctorDutyRostersDetails]...';
SET IDENTITY_INSERT [Registration].[DoctorDutyRostersDetails] ON;
INSERT INTO [Registration].[DoctorDutyRostersDetails] ([Id], [DoctorDutyRostersHeaderId], [RegisterDate], [Day], [SessionDateFrom], [SessionDateTo], [SessionsID], [GenericClinicID], [TimeSlot], [TimeSlotFlowUp], [FlowUpNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [DoctorDutyRostersHeaderId], [RegisterDate], [Day], [SessionDateFrom], [SessionDateTo], [SessionsID], [GenericClinicID], [TimeSlot], [TimeSlotFlowUp], [FlowUpNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[DoctorDutyRostersDetails];
SET IDENTITY_INSERT [Registration].[DoctorDutyRostersDetails] OFF;
GO

PRINT 'Migrating [Registration].[DoctorDutyRosters]...';
SET IDENTITY_INSERT [Registration].[DoctorDutyRosters] ON;
INSERT INTO [Registration].[DoctorDutyRosters] ([Id], [DepartmentID], [DoctorID], [DateFrom], [DateTo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [DepartmentID], [DoctorID], [DateFrom], [DateTo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[DoctorDutyRosters];
SET IDENTITY_INSERT [Registration].[DoctorDutyRosters] OFF;
GO

PRINT 'Migrating [Registration].[DoctorType]...';
SET IDENTITY_INSERT [Registration].[DoctorType] ON;
INSERT INTO [Registration].[DoctorType] ([Id], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[DoctorType];
SET IDENTITY_INSERT [Registration].[DoctorType] OFF;
GO

PRINT 'Migrating [Registration].[DoctorsDailyScheduleDetails]...';
SET IDENTITY_INSERT [Registration].[DoctorsDailyScheduleDetails] ON;
INSERT INTO [Registration].[DoctorsDailyScheduleDetails] ([Id], [DoctorsDailyScheduleID], [PatientID], [Time], [Sponcer], [ContactNo], [Comments], [Visited], [Triage], [Seen], [VisitTypesEnumValue], [PaymentTypesEnumValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [OverBookingStatus], [VisitStatus], [CompanyID], [ProcedureId], [ClinicId], [PatientArrivalDate], [PatientSeenDate], [EndVisitDate], [ISCash], [ApprovedId], [refDoctor], [referralDoctorsPerc], [EncounterTypeId], [IsVirtual], [ReferralNo], [Visitreason], [careteamRole], [TenantId])
SELECT [Id], [DoctorsDailyScheduleID], [PatientID], [Time], [Sponcer], [ContactNo], [Comments], [Visited], [Triage], [Seen], [VisitTypesEnumValue], [PaymentTypesEnumValue], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [OverBookingStatus], [VisitStatus], [CompanyID], [ProcedureId], [ClinicId], [PatientArrivalDate], [PatientSeenDate], [EndVisitDate], [ISCash], [ApprovedId], [refDoctor], [referralDoctorsPerc], [EncounterTypeId], [IsVirtual], [ReferralNo], [Visitreason], [careteamRole], @TenantId
FROM [SunCity_Clinics].[Registration].[DoctorsDailyScheduleDetails];
SET IDENTITY_INSERT [Registration].[DoctorsDailyScheduleDetails] OFF;
GO

PRINT 'Migrating [Registration].[DoctorsDailySchedule]...';
SET IDENTITY_INSERT [Registration].[DoctorsDailySchedule] ON;
INSERT INTO [Registration].[DoctorsDailySchedule] ([Id], [DoctorID], [DepartmentID], [ScheduleDate], [SessionID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [DoctorID], [DepartmentID], [ScheduleDate], [SessionID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[DoctorsDailySchedule];
SET IDENTITY_INSERT [Registration].[DoctorsDailySchedule] OFF;
GO

PRINT 'Migrating [Registration].[Doctors]...';
SET IDENTITY_INSERT [Registration].[Doctors] ON;
INSERT INTO [Registration].[Doctors] ([Id], [Code], [Name], [Email], [NameAr], [EmployeeId], [DepartmentId], [TypeId], [PrimaryDepartmentId], [SpecialtyId], [PrimaryDoctortId], [PhoneNumber], [MobileNumber], [ReviewVisitDays], [FollowupVisitDays], [Address], [Remarks], [Active], [GenericClinicID], [DoctorDegreeID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DesignationId], [ImageUrl], [CompanyID], [ImageUrlSignature], [UserName], [SpecialityID2], [SpecialityID3], [MedicalServiceID], [DoctorDegreeID2], [DoctorDegreeID3], [UserId], [IsUnitHead], [RefTypeID], [cader], [NameRu], [BranchID], [LINCESE], [role], [TenantId])
SELECT [Id], [Code], [Name], [Email], [NameAr], [EmployeeId], [DepartmentId], [TypeId], [PrimaryDepartmentId], [SpecialtyId], [PrimaryDoctortId], [PhoneNumber], [MobileNumber], [ReviewVisitDays], [FollowupVisitDays], [Address], [Remarks], [Active], [GenericClinicID], [DoctorDegreeID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DesignationId], [ImageUrl], [CompanyID], [ImageUrlSignature], [UserName], [SpecialityID2], [SpecialityID3], [MedicalServiceID], [DoctorDegreeID2], [DoctorDegreeID3], [UserId], [IsUnitHead], [RefTypeID], [cader], [NameRu], [BranchID], [LINCESE], [role], @TenantId
FROM [SunCity_Clinics].[Registration].[Doctors];
SET IDENTITY_INSERT [Registration].[Doctors] OFF;
GO

PRINT 'Migrating [Registration].[Equipments]...';
SET IDENTITY_INSERT [Registration].[Equipments] ON;
INSERT INTO [Registration].[Equipments] ([Id], [Name], [DepartmentId], [Remarks], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Name], [DepartmentId], [Remarks], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[Equipments];
SET IDENTITY_INSERT [Registration].[Equipments] OFF;
GO

PRINT 'Migrating [Registration].[FavouriteDiagnosis]...';
SET IDENTITY_INSERT [Registration].[FavouriteDiagnosis] ON;
INSERT INTO [Registration].[FavouriteDiagnosis] ([Id], [DiagnosisId], [DoctorId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [DiagnosisId], [DoctorId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[FavouriteDiagnosis];
SET IDENTITY_INSERT [Registration].[FavouriteDiagnosis] OFF;
GO

PRINT 'Migrating [Registration].[FavouriteDisease]...';
SET IDENTITY_INSERT [Registration].[FavouriteDisease] ON;
INSERT INTO [Registration].[FavouriteDisease] ([Id], [DiseaseId], [DoctorId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [DiseaseId], [DoctorId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[FavouriteDisease];
SET IDENTITY_INSERT [Registration].[FavouriteDisease] OFF;
GO

PRINT 'Migrating [Registration].[FavouriteDrugs]...';
SET IDENTITY_INSERT [Registration].[FavouriteDrugs] ON;
INSERT INTO [Registration].[FavouriteDrugs] ([Id], [DrugId], [DoctorId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [DrugId], [DoctorId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[FavouriteDrugs];
SET IDENTITY_INSERT [Registration].[FavouriteDrugs] OFF;
GO

PRINT 'Migrating [Registration].[GeneralConsent]...';
SET IDENTITY_INSERT [Registration].[GeneralConsent] ON;
INSERT INTO [Registration].[GeneralConsent] ([ID], [PatientID], [IdentityDocument], [DocumentNumber], [FirstName], [SecondName], [FamilyName], [DateOfBirth], [Age], [Gender], [MaritalStatus], [Nationality], [VisaStatus], [Address], [POBox], [City], [Mobile], [Landline], [Fax], [Email], [EmergencyContactName], [EmergencyContactRelationship], [EmergencyContactMobile], [ReferredBy], [HospitalClinic], [PaymentMethods], [PatientName], [PatientSignature], [SignatureDate], [SignatureTime], [AttendantGuardianName], [AttendantGuardianSignature], [AttendantGuardianSignatureDate], [AttendantGuardianSignatureTime], [SignatureInput1], [SignatureInput2], [SignatureInput3], [SignatureInput4], [SignatureInput5], [ChkAgreement], [AgreementPatientName], [AgreementSignature], [AgreementDate], [AgreementTime], [AgreementAttendantName], [AgreementAttendantSignature], [AgreementAttendantDate], [AgreementAttendantTime], [Comment], [TenantId])
SELECT [ID], [PatientID], [IdentityDocument], [DocumentNumber], [FirstName], [SecondName], [FamilyName], [DateOfBirth], [Age], [Gender], [MaritalStatus], [Nationality], [VisaStatus], [Address], [POBox], [City], [Mobile], [Landline], [Fax], [Email], [EmergencyContactName], [EmergencyContactRelationship], [EmergencyContactMobile], [ReferredBy], [HospitalClinic], [PaymentMethods], [PatientName], [PatientSignature], [SignatureDate], [SignatureTime], [AttendantGuardianName], [AttendantGuardianSignature], [AttendantGuardianSignatureDate], [AttendantGuardianSignatureTime], [SignatureInput1], [SignatureInput2], [SignatureInput3], [SignatureInput4], [SignatureInput5], [ChkAgreement], [AgreementPatientName], [AgreementSignature], [AgreementDate], [AgreementTime], [AgreementAttendantName], [AgreementAttendantSignature], [AgreementAttendantDate], [AgreementAttendantTime], [Comment], @TenantId
FROM [SunCity_Clinics].[Registration].[GeneralConsent];
SET IDENTITY_INSERT [Registration].[GeneralConsent] OFF;
GO

PRINT 'Migrating [Registration].[GenerateDeficiencySetup]...';
SET IDENTITY_INSERT [Registration].[GenerateDeficiencySetup] ON;
INSERT INTO [Registration].[GenerateDeficiencySetup] ([Id], [Code], [PatientID], [OP_IPnumber], [EmployeeID], [DoctorID], [DeficiencySetupID], [DateComplete], [DeficiencyDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Code], [PatientID], [OP_IPnumber], [EmployeeID], [DoctorID], [DeficiencySetupID], [DateComplete], [DeficiencyDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[GenerateDeficiencySetup];
SET IDENTITY_INSERT [Registration].[GenerateDeficiencySetup] OFF;
GO

PRINT 'Migrating [Registration].[GenericClinics]...';
SET IDENTITY_INSERT [Registration].[GenericClinics] ON;
INSERT INTO [Registration].[GenericClinics] ([Id], [Clinic], [DepartmentId], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Clinic], [DepartmentId], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[GenericClinics];
SET IDENTITY_INSERT [Registration].[GenericClinics] OFF;
GO

PRINT 'Migrating [Registration].[LockDoctorDutyRoster]...';
SET IDENTITY_INSERT [Registration].[LockDoctorDutyRoster] ON;
INSERT INTO [Registration].[LockDoctorDutyRoster] ([Id], [RosterId], [LockFrom], [LockTo], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [RosterId], [LockFrom], [LockTo], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[LockDoctorDutyRoster];
SET IDENTITY_INSERT [Registration].[LockDoctorDutyRoster] OFF;
GO

PRINT 'Migrating [Registration].[MarketingMedia]...';
SET IDENTITY_INSERT [Registration].[MarketingMedia] ON;
INSERT INTO [Registration].[MarketingMedia] ([Id], [Name], [NameEn], [Description], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Name], [NameEn], [Description], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[MarketingMedia];
SET IDENTITY_INSERT [Registration].[MarketingMedia] OFF;
GO

PRINT 'Migrating [Registration].[MedicalAlerts]...';
SET IDENTITY_INSERT [Registration].[MedicalAlerts] ON;
INSERT INTO [Registration].[MedicalAlerts] ([Id], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[MedicalAlerts];
SET IDENTITY_INSERT [Registration].[MedicalAlerts] OFF;
GO

PRINT 'Migrating [Registration].[MedicalRecordDepartments]...';
SET IDENTITY_INSERT [Registration].[MedicalRecordDepartments] ON;
INSERT INTO [Registration].[MedicalRecordDepartments] ([Id], [CounterName], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [CounterName], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[MedicalRecordDepartments];
SET IDENTITY_INSERT [Registration].[MedicalRecordDepartments] OFF;
GO

PRINT 'Migrating [Registration].[MedicalServices]...';
SET IDENTITY_INSERT [Registration].[MedicalServices] ON;
INSERT INTO [Registration].[MedicalServices] ([ID], [Code], [NameAr], [NameEn], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [Status], [CompanyID], [TenantId])
SELECT [ID], [Code], [NameAr], [NameEn], [CreatedBy], [CreationDate], [ModifiedBy], [ModificationDate], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[MedicalServices];
SET IDENTITY_INSERT [Registration].[MedicalServices] OFF;
GO

PRINT 'Migrating [Registration].[MergePatientDetail]...';
SET IDENTITY_INSERT [Registration].[MergePatientDetail] ON;
INSERT INTO [Registration].[MergePatientDetail] ([ID], [MergePatientID], [PatientIDToMerge], [PatientName], [CompanyID], [TenantId])
SELECT [ID], [MergePatientID], [PatientIDToMerge], [PatientName], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[MergePatientDetail];
SET IDENTITY_INSERT [Registration].[MergePatientDetail] OFF;
GO

PRINT 'Migrating [Registration].[MergePatient]...';
SET IDENTITY_INSERT [Registration].[MergePatient] ON;
INSERT INTO [Registration].[MergePatient] ([ID], [SourcePatientID], [PatientName], [IsMereged], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [SourcePatientID], [PatientName], [IsMereged], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[MergePatient];
SET IDENTITY_INSERT [Registration].[MergePatient] OFF;
GO

PRINT 'Migrating [Registration].[Military]...';
SET IDENTITY_INSERT [Registration].[Military] ON;
INSERT INTO [Registration].[Military] ([Id], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameKa], [TenantId])
SELECT [Id], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameKa], @TenantId
FROM [SunCity_Clinics].[Registration].[Military];
SET IDENTITY_INSERT [Registration].[Military] OFF;
GO

PRINT 'Migrating [Registration].[MinistryAlerts]...';
SET IDENTITY_INSERT [Registration].[MinistryAlerts] ON;
INSERT INTO [Registration].[MinistryAlerts] ([Id], [Name], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Name], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[MinistryAlerts];
SET IDENTITY_INSERT [Registration].[MinistryAlerts] OFF;
GO

PRINT 'Migrating [Registration].[Nationalities]...';
SET IDENTITY_INSERT [Registration].[Nationalities] ON;
INSERT INTO [Registration].[Nationalities] ([Id], [Name], [NameEn], [NationalityGroupId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Citizen], [CountryCode], [TenantId])
SELECT [Id], [Name], [NameEn], [NationalityGroupId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Citizen], [CountryCode], @TenantId
FROM [SunCity_Clinics].[Registration].[Nationalities];
SET IDENTITY_INSERT [Registration].[Nationalities] OFF;
GO

PRINT 'Migrating [Registration].[NationalityGroups]...';
SET IDENTITY_INSERT [Registration].[NationalityGroups] ON;
INSERT INTO [Registration].[NationalityGroups] ([Id], [GroupName], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [GroupNameAr], [TenantId])
SELECT [Id], [GroupName], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [GroupNameAr], @TenantId
FROM [SunCity_Clinics].[Registration].[NationalityGroups];
SET IDENTITY_INSERT [Registration].[NationalityGroups] OFF;
GO

PRINT 'Migrating [Registration].[PatientAllergy]...';
SET IDENTITY_INSERT [Registration].[PatientAllergy] ON;
INSERT INTO [Registration].[PatientAllergy] ([Id], [AllergyDesc], [AllergyTypeEnumValue], [PatientID], [CreatorName], [CompanyID], [TenantId])
SELECT [Id], [AllergyDesc], [AllergyTypeEnumValue], [PatientID], [CreatorName], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[PatientAllergy];
SET IDENTITY_INSERT [Registration].[PatientAllergy] OFF;
GO

PRINT 'Migrating [Registration].[PatientComment]...';
SET IDENTITY_INSERT [Registration].[PatientComment] ON;
INSERT INTO [Registration].[PatientComment] ([ID], [PatientID], [Active], [Eurgent], [Comment], [Complan], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [Active], [Eurgent], [Comment], [Complan], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[PatientComment];
SET IDENTITY_INSERT [Registration].[PatientComment] OFF;
GO

PRINT 'Migrating [Registration].[PatientFamilyMembers]...';
SET IDENTITY_INSERT [Registration].[PatientFamilyMembers] ON;
INSERT INTO [Registration].[PatientFamilyMembers] ([Id], [PatientID], [RelPatientID], [Sex], [DOB], [Status], [Relation], [Address], [ContactNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [RelPatientID], [Sex], [DOB], [Status], [Relation], [Address], [ContactNo], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[PatientFamilyMembers];
SET IDENTITY_INSERT [Registration].[PatientFamilyMembers] OFF;
GO

PRINT 'Migrating [Registration].[PatientInsurance]...';
SET IDENTITY_INSERT [Registration].[PatientInsurance] ON;
INSERT INTO [Registration].[PatientInsurance] ([ID], [PatientId], [SponsorId], [SponsorName], [Ins_PolicyID], [ins_ClassID], [MemberShipId], [SCHSLicense], [DHAMemberID], [coverageClass], [Subscriberelation], [subscriperType], [payeeType], [insuranceName], [TenantId])
SELECT [ID], [PatientId], [SponsorId], [SponsorName], [Ins_PolicyID], [ins_ClassID], [MemberShipId], [SCHSLicense], [DHAMemberID], [coverageClass], [Subscriberelation], [subscriperType], [payeeType], [insuranceName], @TenantId
FROM [SunCity_Clinics].[Registration].[PatientInsurance];
SET IDENTITY_INSERT [Registration].[PatientInsurance] OFF;
GO

PRINT 'Migrating [Registration].[PatientMedicalAlerts]...';
SET IDENTITY_INSERT [Registration].[PatientMedicalAlerts] ON;
INSERT INTO [Registration].[PatientMedicalAlerts] ([Id], [PatientId], [MedicalAlertId], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [MedicalAlert_Id], [Patient_Id], [CompanyID], [TenantId])
SELECT [Id], [PatientId], [MedicalAlertId], [Remarks], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [MedicalAlert_Id], [Patient_Id], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[PatientMedicalAlerts];
SET IDENTITY_INSERT [Registration].[PatientMedicalAlerts] OFF;
GO

PRINT 'Migrating [Registration].[PatientPaymentTypeHistory]...';
SET IDENTITY_INSERT [Registration].[PatientPaymentTypeHistory] ON;
INSERT INTO [Registration].[PatientPaymentTypeHistory] ([ID], [PatientID], [PaymentTypeID], [InsuranceCompanyID], [Ins_Company_PolicyID], [ins_Company_ClassID], [FromDate], [ToDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [PatientID], [PaymentTypeID], [InsuranceCompanyID], [Ins_Company_PolicyID], [ins_Company_ClassID], [FromDate], [ToDate], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[PatientPaymentTypeHistory];
SET IDENTITY_INSERT [Registration].[PatientPaymentTypeHistory] OFF;
GO

PRINT 'Migrating [Registration].[PatientSurveyDetail]...';
SET IDENTITY_INSERT [Registration].[PatientSurveyDetail] ON;
INSERT INTO [Registration].[PatientSurveyDetail] ([Id], [MasterID], [ServQuestID], [ServAnswer], [TenantId])
SELECT [Id], [MasterID], [ServQuestID], [ServAnswer], @TenantId
FROM [SunCity_Clinics].[Registration].[PatientSurveyDetail];
SET IDENTITY_INSERT [Registration].[PatientSurveyDetail] OFF;
GO

PRINT 'Migrating [Registration].[PatientSurvey]...';
SET IDENTITY_INSERT [Registration].[PatientSurvey] ON;
INSERT INTO [Registration].[PatientSurvey] ([Id], [patientID], [OPIP], [CreatedDate], [ServiceTypeIDz], [DoctorID], [TenantId])
SELECT [Id], [patientID], [OPIP], [CreatedDate], [ServiceTypeIDz], [DoctorID], @TenantId
FROM [SunCity_Clinics].[Registration].[PatientSurvey];
SET IDENTITY_INSERT [Registration].[PatientSurvey] OFF;
GO

PRINT 'Migrating [Registration].[Patients]...';
SET IDENTITY_INSERT [Registration].[Patients] ON;
INSERT INTO [Registration].[Patients] ([Id], [Code], [TitleId], [FirstName], [MiddleName1], [MiddleName2], [Main_Acc], [Sub_Acc], [LastName], [SponsorId], [SponsorName], [PaymentType], [RegistrationDate], [PrimaryDoctorId], [ImageUrl], [StreetAddress], [StateProvince], [City], [ZipCode], [SponserCategoryId], [SponserCategory], [PhoneNumber], [MobileNumber], [Fax], [Email], [ReligionId], [NationalityId], [Employer], [Occupation], [PassportNumber], [PassportValidityEndDate], [PatientClassificationId], [PatientType], [EmergencyContactPerson], [ContactPersonRelation], [ContactNumber], [SMSAlert], [MRDCollectionPointId], [HearAbout], [OccupationalHistory], [PastHistory], [FamilyHistory], [Notes], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [RegistrationType], [AlternateNo], [RefernceNo], [POBox], [InsuranceCompanyId], [GenderId], [MaritalStatusId], [DateOfBirth], [AreaIsland], [BloodGroup], [TypeId], [CountryId], [CityId], [CardNo], [RegisterPatientsType], [CompanyID], [TranslatedFallName], [IsThereSimmilerNames], [DistrictId], [IdentificationId], [IdNumber], [ERCodeID], [Smoker], [Address], [ImportantPatient], [SecurityTypeId], [AdvertisingTypeId], [ReferralTypeId], [MilitaryNumber], [MilitaryRankID], [TypeOfWeapenID], [ServiceTypeID], [BloodGropID], [ValidUpTo], [ChieOfStaffOfTheWar], [ForceCommander], [MinistersOffice], [Age], [Ins_PolicyID], [ins_ClassID], [MemberShipId], [SCHSLicense], [SCHC_LicenceNO], [ISAllergy], [Residance], [SocialEconomicStatus], [FastRegServiceBarcode], [IsVisitor], [IsResisding], [HomeCountryId], [HomeCityId], [HomeDistrictId], [hasCovidVaccines], [IsPreviouslyInfected], [IsHeadofFamily], [HeadofFamilyName], [RelativeId], [MedicalFileNumber], [EmployeeCardNumber], [IsFirstDose], [IsSecondDose], [VaccineTypeIdSt], [IsPaientEmployee], [VaccineTypeIdNd], [NationalID_UK], [NationalPassport_UK], [InternationalPassport_UK], [branchId], [ConfidentialityCode], [IsLivingalone], [Isalcohol], [Isdrugaddicted], [socialhistoryothers], [weight], [height], [taxNumber], [LegalEntity], [Password], [estimateage], [IsEligible], [IsExpiryDate], [UniqueRegNo], [EncounterType], [GovernorateID], [PlatformMRN], [newbornId], [PostCashCompanyID], [TenantId])
SELECT [Id], [Code], [TitleId], [FirstName], [MiddleName1], [MiddleName2], [Main_Acc], [Sub_Acc], [LastName], [SponsorId], [SponsorName], [PaymentType], [RegistrationDate], [PrimaryDoctorId], [ImageUrl], [StreetAddress], [StateProvince], [City], [ZipCode], [SponserCategoryId], [SponserCategory], [PhoneNumber], [MobileNumber], [Fax], [Email], [ReligionId], [NationalityId], [Employer], [Occupation], [PassportNumber], [PassportValidityEndDate], [PatientClassificationId], [PatientType], [EmergencyContactPerson], [ContactPersonRelation], [ContactNumber], [SMSAlert], [MRDCollectionPointId], [HearAbout], [OccupationalHistory], [PastHistory], [FamilyHistory], [Notes], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [RegistrationType], [AlternateNo], [RefernceNo], [POBox], [InsuranceCompanyId], [GenderId], [MaritalStatusId], [DateOfBirth], [AreaIsland], [BloodGroup], [TypeId], [CountryId], [CityId], [CardNo], [RegisterPatientsType], [CompanyID], [TranslatedFallName], [IsThereSimmilerNames], [DistrictId], [IdentificationId], [IdNumber], [ERCodeID], [Smoker], [Address], [ImportantPatient], [SecurityTypeId], [AdvertisingTypeId], [ReferralTypeId], [MilitaryNumber], [MilitaryRankID], [TypeOfWeapenID], [ServiceTypeID], [BloodGropID], [ValidUpTo], [ChieOfStaffOfTheWar], [ForceCommander], [MinistersOffice], [Age], [Ins_PolicyID], [ins_ClassID], [MemberShipId], [SCHSLicense], [SCHC_LicenceNO], [ISAllergy], [Residance], [SocialEconomicStatus], [FastRegServiceBarcode], [IsVisitor], [IsResisding], [HomeCountryId], [HomeCityId], [HomeDistrictId], [hasCovidVaccines], [IsPreviouslyInfected], [IsHeadofFamily], [HeadofFamilyName], [RelativeId], [MedicalFileNumber], [EmployeeCardNumber], [IsFirstDose], [IsSecondDose], [VaccineTypeIdSt], [IsPaientEmployee], [VaccineTypeIdNd], [NationalID_UK], [NationalPassport_UK], [InternationalPassport_UK], [branchId], [ConfidentialityCode], [IsLivingalone], [Isalcohol], [Isdrugaddicted], [socialhistoryothers], [weight], [height], [taxNumber], [LegalEntity], [Password], [estimateage], [IsEligible], [IsExpiryDate], [UniqueRegNo], [EncounterType], [GovernorateID], [PlatformMRN], [newbornId], [PostCashCompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[Patients];
SET IDENTITY_INSERT [Registration].[Patients] OFF;
GO

PRINT 'Migrating [Registration].[PostCashCompanies]...';
SET IDENTITY_INSERT [Registration].[PostCashCompanies] ON;
INSERT INTO [Registration].[PostCashCompanies] ([Id], [CompanyName], [TenantId])
SELECT [Id], [CompanyName], @TenantId
FROM [SunCity_Clinics].[Registration].[PostCashCompanies];
SET IDENTITY_INSERT [Registration].[PostCashCompanies] OFF;
GO

PRINT 'Migrating [Registration].[Registration.Companies]...';
SET IDENTITY_INSERT [Registration].[Registration.Companies] ON;
INSERT INTO [Registration].[Registration.Companies] ([Id], [CompanyName], [TenantId])
SELECT [Id], [CompanyName], @TenantId
FROM [SunCity_Clinics].[Registration].[Registration.Companies];
SET IDENTITY_INSERT [Registration].[Registration.Companies] OFF;
GO

PRINT 'Migrating [Registration].[RegistrationFields]...';
SET IDENTITY_INSERT [Registration].[RegistrationFields] ON;
INSERT INTO [Registration].[RegistrationFields] ([Id], [Name], [CompanyID], [TenantId])
SELECT [Id], [Name], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[RegistrationFields];
SET IDENTITY_INSERT [Registration].[RegistrationFields] OFF;
GO

PRINT 'Migrating [Registration].[RegistrationParameters]...';
SET IDENTITY_INSERT [Registration].[RegistrationParameters] ON;
INSERT INTO [Registration].[RegistrationParameters] ([Id], [Mandatory], [PromptIfMissing], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [RegistrationFieldID], [DepartmentID], [UserID], [CompanyID], [CountryCode], [SponsorCode], [IdCode], [SexCode], [CompanyCode], [ServiceID], [ISEnglishName], [TenantId])
SELECT [Id], [Mandatory], [PromptIfMissing], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [RegistrationFieldID], [DepartmentID], [UserID], [CompanyID], [CountryCode], [SponsorCode], [IdCode], [SexCode], [CompanyCode], [ServiceID], [ISEnglishName], @TenantId
FROM [SunCity_Clinics].[Registration].[RegistrationParameters];
SET IDENTITY_INSERT [Registration].[RegistrationParameters] OFF;
GO

PRINT 'Migrating [Registration].[Relation]...';
SET IDENTITY_INSERT [Registration].[Relation] ON;
INSERT INTO [Registration].[Relation] ([Id], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[Relation];
SET IDENTITY_INSERT [Registration].[Relation] OFF;
GO

PRINT 'Migrating [Registration].[Religions]...';
SET IDENTITY_INSERT [Registration].[Religions] ON;
INSERT INTO [Registration].[Religions] ([Id], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Apprivat], [TenantId])
SELECT [Id], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [Apprivat], @TenantId
FROM [SunCity_Clinics].[Registration].[Religions];
SET IDENTITY_INSERT [Registration].[Religions] OFF;
GO

PRINT 'Migrating [Registration].[RequestMedicalRecordTransfer]...';
SET IDENTITY_INSERT [Registration].[RequestMedicalRecordTransfer] ON;
INSERT INTO [Registration].[RequestMedicalRecordTransfer] ([Id], [PatientID], [OPNumber], [RequestedBy], [MRD], [RequiredOn], [Time], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [Id], [PatientID], [OPNumber], [RequestedBy], [MRD], [RequiredOn], [Time], [Status], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[RequestMedicalRecordTransfer];
SET IDENTITY_INSERT [Registration].[RequestMedicalRecordTransfer] OFF;
GO

PRINT 'Migrating [Registration].[ScheduleStatus]...';
SET IDENTITY_INSERT [Registration].[ScheduleStatus] ON;
INSERT INTO [Registration].[ScheduleStatus] ([Id], [DoctorsDailyScheduleID], [PatientID], [Date], [Status], [CompanyID], [TenantId])
SELECT [Id], [DoctorsDailyScheduleID], [PatientID], [Date], [Status], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[ScheduleStatus];
SET IDENTITY_INSERT [Registration].[ScheduleStatus] OFF;
GO

PRINT 'Migrating [Registration].[Sessions]...';
SET IDENTITY_INSERT [Registration].[Sessions] ON;
INSERT INTO [Registration].[Sessions] ([Id], [SessionName], [StartTime], [EndTime], [CreatedDate], [CreatedBy], [LastModifiedDate], [LastModifiedBy], [CompanyID], [SessionNameAr], [ShiftType], [LateTimeIn], [BreakOut], [BreakIn], [EarlyOut], [LateTimeOut], [TenantId])
SELECT [Id], [SessionName], [StartTime], [EndTime], [CreatedDate], [CreatedBy], [LastModifiedDate], [LastModifiedBy], [CompanyID], [SessionNameAr], [ShiftType], [LateTimeIn], [BreakOut], [BreakIn], [EarlyOut], [LateTimeOut], @TenantId
FROM [SunCity_Clinics].[Registration].[Sessions];
SET IDENTITY_INSERT [Registration].[Sessions] OFF;
GO

PRINT 'Migrating [Registration].[Sex]...';
SET IDENTITY_INSERT [Registration].[Sex] ON;
INSERT INTO [Registration].[Sex] ([Id], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameKa], [TenantId])
SELECT [Id], [Name], [NameEn], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [NameKa], @TenantId
FROM [SunCity_Clinics].[Registration].[Sex];
SET IDENTITY_INSERT [Registration].[Sex] OFF;
GO

PRINT 'Migrating [Registration].[Specialty]...';
SET IDENTITY_INSERT [Registration].[Specialty] ON;
INSERT INTO [Registration].[Specialty] ([Id], [SpecialtyName], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SpecialtyNameAr], [TenantId])
SELECT [Id], [SpecialtyName], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [SpecialtyNameAr], @TenantId
FROM [SunCity_Clinics].[Registration].[Specialty];
SET IDENTITY_INSERT [Registration].[Specialty] OFF;
GO

PRINT 'Migrating [Registration].[SponsorItem]...';
INSERT INTO [Registration].[SponsorItem] ([Id], [Sponsor_Id], [Item_Id], [CoPaymentPercentage], [TenantId])
SELECT [Id], [Sponsor_Id], [Item_Id], [CoPaymentPercentage], @TenantId
FROM [SunCity_Clinics].[Registration].[SponsorItem];
GO

PRINT 'Migrating [Registration].[Sponsor]...';
INSERT INTO [Registration].[Sponsor] ([Id], [Name], [CompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [NameAr], [TenantId])
SELECT [Id], [Name], [CompanyID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [NameAr], @TenantId
FROM [SunCity_Clinics].[Registration].[Sponsor];
GO

PRINT 'Migrating [Registration].[SurveyAnswers]...';
SET IDENTITY_INSERT [Registration].[SurveyAnswers] ON;
INSERT INTO [Registration].[SurveyAnswers] ([Id], [QuestAns], [QuestId], [CreatedBy], [CreatedDate], [TenantId])
SELECT [Id], [QuestAns], [QuestId], [CreatedBy], [CreatedDate], @TenantId
FROM [SunCity_Clinics].[Registration].[SurveyAnswers];
SET IDENTITY_INSERT [Registration].[SurveyAnswers] OFF;
GO

PRINT 'Migrating [Registration].[SurveyQuestions]...';
SET IDENTITY_INSERT [Registration].[SurveyQuestions] ON;
INSERT INTO [Registration].[SurveyQuestions] ([Id], [ServQuest], [QuestType], [CreatedBy], [CreatedDate], [CompanyID], [branchId], [NonDetail], [NameKa], [TenantId])
SELECT [Id], [ServQuest], [QuestType], [CreatedBy], [CreatedDate], [CompanyID], [branchId], [NonDetail], [NameKa], @TenantId
FROM [SunCity_Clinics].[Registration].[SurveyQuestions];
SET IDENTITY_INSERT [Registration].[SurveyQuestions] OFF;
GO

PRINT 'Migrating [Registration].[Technicians]...';
SET IDENTITY_INSERT [Registration].[Technicians] ON;
INSERT INTO [Registration].[Technicians] ([Id], [Code], [Name], [Email], [UserName], [TypeId], [PrimaryDepartmentId], [SpecialtyId], [PrimaryDoctortId], [EmployeeId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DepartmentId], [PhoneNumber], [MobileNumber], [Address], [Remarks], [Active], [Designation], [ImageUrl], [CompanyID], [TenantId])
SELECT [Id], [Code], [Name], [Email], [UserName], [TypeId], [PrimaryDepartmentId], [SpecialtyId], [PrimaryDoctortId], [EmployeeId], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [DepartmentId], [PhoneNumber], [MobileNumber], [Address], [Remarks], [Active], [Designation], [ImageUrl], [CompanyID], @TenantId
FROM [SunCity_Clinics].[Registration].[Technicians];
SET IDENTITY_INSERT [Registration].[Technicians] OFF;
GO

PRINT 'Migrating [Registration].[Titles]...';
SET IDENTITY_INSERT [Registration].[Titles] ON;
INSERT INTO [Registration].[Titles] ([Id], [ArabicTitleName], [EnglishTitleName], [AppliedTo], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IsDefault], [NameKa], [TenantId])
SELECT [Id], [ArabicTitleName], [EnglishTitleName], [AppliedTo], [Active], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [IsDefault], [NameKa], @TenantId
FROM [SunCity_Clinics].[Registration].[Titles];
SET IDENTITY_INSERT [Registration].[Titles] OFF;
GO

PRINT 'Migrating [Registration].[VaccineType]...';
SET IDENTITY_INSERT [Registration].[VaccineType] ON;
INSERT INTO [Registration].[VaccineType] ([Id], [NameEn], [NameAr], [CreatedDate], [CreatedBy], [LastModofiedDate], [ModifiedBy], [TenantId])
SELECT [Id], [NameEn], [NameAr], [CreatedDate], [CreatedBy], [LastModofiedDate], [ModifiedBy], @TenantId
FROM [SunCity_Clinics].[Registration].[VaccineType];
SET IDENTITY_INSERT [Registration].[VaccineType] OFF;
GO

PRINT 'Migrating [Registration].[XDoctorDutyRostersDetails]...';
SET IDENTITY_INSERT [Registration].[XDoctorDutyRostersDetails] ON;
INSERT INTO [Registration].[XDoctorDutyRostersDetails] ([Id], [DoctorDutyRostersHeaderId], [RegisterDate], [Day], [SessionDateFrom], [SessionDateTo], [SessionsID], [GenericClinicID], [TimeSlot], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TimeSlotFlowUp], [FlowUpNo], [TenantId])
SELECT [Id], [DoctorDutyRostersHeaderId], [RegisterDate], [Day], [SessionDateFrom], [SessionDateTo], [SessionsID], [GenericClinicID], [TimeSlot], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TimeSlotFlowUp], [FlowUpNo], @TenantId
FROM [SunCity_Clinics].[Registration].[XDoctorDutyRostersDetails];
SET IDENTITY_INSERT [Registration].[XDoctorDutyRostersDetails] OFF;
GO

PRINT 'Migrating [dbo].[AccTemp]...';
SET IDENTITY_INSERT [dbo].[AccTemp] ON;
INSERT INTO [dbo].[AccTemp] ([id], [AccountNo], [MainAccountNo], [AccountNameAr], [AccountNameEn], [AccountType], [AccountLevel], [CurrencyId], [CostCenterDistributionId], [FinalAccountId], [AccountNatureId], [JournalTypeId], [Notes], [PrevDebitCurrency], [PrevCreditCurrency], [CurrentDebitCurrency], [CurrentCreditCurrency], [CurrencyBalance], [PrevDebitLocal], [PrevCreditLocal], [CurrentDebitLocal], [CurrentCreditLocal], [LocalBalance], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [SupervisoryAccount], [IsDebit], [TenantId])
SELECT [id], [AccountNo], [MainAccountNo], [AccountNameAr], [AccountNameEn], [AccountType], [AccountLevel], [CurrencyId], [CostCenterDistributionId], [FinalAccountId], [AccountNatureId], [JournalTypeId], [Notes], [PrevDebitCurrency], [PrevCreditCurrency], [CurrentDebitCurrency], [CurrentCreditCurrency], [CurrencyBalance], [PrevDebitLocal], [PrevCreditLocal], [CurrentDebitLocal], [CurrentCreditLocal], [LocalBalance], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [SupervisoryAccount], [IsDebit], @TenantId
FROM [SunCity_Clinics].[dbo].[AccTemp];
SET IDENTITY_INSERT [dbo].[AccTemp] OFF;
GO

PRINT 'Migrating [dbo].[AspNetRoleClaims]...';
SET IDENTITY_INSERT [dbo].[AspNetRoleClaims] ON;
INSERT INTO [dbo].[AspNetRoleClaims] ([Id], [RoleId], [ClaimType], [ClaimValue], [TenantId])
SELECT [Id], [RoleId], [ClaimType], [ClaimValue], @TenantId
FROM [SunCity_Clinics].[dbo].[AspNetRoleClaims];
SET IDENTITY_INSERT [dbo].[AspNetRoleClaims] OFF;
GO

PRINT 'Migrating [dbo].[AspNetRoles]...';
SET IDENTITY_INSERT [dbo].[AspNetRoles] ON;
INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp], [Discriminator], [TenantId])
SELECT [Id], [Name], [NormalizedName], [ConcurrencyStamp], [Discriminator], @TenantId
FROM [SunCity_Clinics].[dbo].[AspNetRoles];
SET IDENTITY_INSERT [dbo].[AspNetRoles] OFF;
GO

PRINT 'Migrating [dbo].[AspNetUserClaims]...';
SET IDENTITY_INSERT [dbo].[AspNetUserClaims] ON;
INSERT INTO [dbo].[AspNetUserClaims] ([Id], [UserId], [ClaimType], [ClaimValue], [TenantId])
SELECT [Id], [UserId], [ClaimType], [ClaimValue], @TenantId
FROM [SunCity_Clinics].[dbo].[AspNetUserClaims];
SET IDENTITY_INSERT [dbo].[AspNetUserClaims] OFF;
GO

PRINT 'Migrating [dbo].[AspNetUserLogins]...';
INSERT INTO [dbo].[AspNetUserLogins] ([LoginProvider], [ProviderKey], [ProviderDisplayName], [UserId], [TenantId])
SELECT [LoginProvider], [ProviderKey], [ProviderDisplayName], [UserId], @TenantId
FROM [SunCity_Clinics].[dbo].[AspNetUserLogins];
GO

PRINT 'Migrating [dbo].[AspNetUserRoles]...';
INSERT INTO [dbo].[AspNetUserRoles] ([UserId], [RoleId], [TenantId])
SELECT [UserId], [RoleId], @TenantId
FROM [SunCity_Clinics].[dbo].[AspNetUserRoles];
GO

PRINT 'Migrating [dbo].[AspNetUserTokens]...';
INSERT INTO [dbo].[AspNetUserTokens] ([UserId], [LoginProvider], [Name], [Value], [TenantId])
SELECT [UserId], [LoginProvider], [Name], [Value], @TenantId
FROM [SunCity_Clinics].[dbo].[AspNetUserTokens];
GO

PRINT 'Migrating [dbo].[AspNetUsers]...';
SET IDENTITY_INSERT [dbo].[AspNetUsers] ON;
INSERT INTO [dbo].[AspNetUsers] ([Id], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnd], [LockoutEnabled], [AccessFailedCount], [Discriminator], [EmployeeId], [TenantId])
SELECT [Id], [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [EmailConfirmed], [PasswordHash], [SecurityStamp], [ConcurrencyStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [LockoutEnd], [LockoutEnabled], [AccessFailedCount], [Discriminator], [EmployeeId], @TenantId
FROM [SunCity_Clinics].[dbo].[AspNetUsers];
SET IDENTITY_INSERT [dbo].[AspNetUsers] OFF;
GO

PRINT 'Migrating [dbo].[BMoheb_Test]...';
SET IDENTITY_INSERT [dbo].[BMoheb_Test] ON;
INSERT INTO [dbo].[BMoheb_Test] ([ID_Code], [Name], [Name_Ar], [TenantId])
SELECT [ID_Code], [Name], [Name_Ar], @TenantId
FROM [SunCity_Clinics].[dbo].[BMoheb_Test];
SET IDENTITY_INSERT [dbo].[BMoheb_Test] OFF;
GO

PRINT 'Migrating [dbo].[BedStatus]...';
SET IDENTITY_INSERT [dbo].[BedStatus] ON;
INSERT INTO [dbo].[BedStatus] ([Id], [Name], [StatusImage], [CompanyID], [NameAr], [TenantId])
SELECT [Id], [Name], [StatusImage], [CompanyID], [NameAr], @TenantId
FROM [SunCity_Clinics].[dbo].[BedStatus];
SET IDENTITY_INSERT [dbo].[BedStatus] OFF;
GO

PRINT 'Migrating [dbo].[BedType]...';
SET IDENTITY_INSERT [dbo].[BedType] ON;
INSERT INTO [dbo].[BedType] ([Id], [TypeCode], [NameArabic], [NameEnglish], [Status], [HospitalCase], [TenantId])
SELECT [Id], [TypeCode], [NameArabic], [NameEnglish], [Status], [HospitalCase], @TenantId
FROM [SunCity_Clinics].[dbo].[BedType];
SET IDENTITY_INSERT [dbo].[BedType] OFF;
GO

PRINT 'Migrating [dbo].[CPT4]...';
INSERT INTO [dbo].[CPT4] ([PCPCCD], [PCPCSP], [PCPCNL], [PCPCNA], [PCPPTP], [PCACTV], [PCCRDT], [PCLMDT], [PCMTTP], [PCMTTM], [PCUSIP], [PCWSID], [PCFLAG], [PCPCCD1], [PCPPSM], [PCPPD1], [PCPPD2], [PCAVFLG], [TenantId])
SELECT [PCPCCD], [PCPCSP], [PCPCNL], [PCPCNA], [PCPPTP], [PCACTV], [PCCRDT], [PCLMDT], [PCMTTP], [PCMTTM], [PCUSIP], [PCWSID], [PCFLAG], [PCPCCD1], [PCPPSM], [PCPPD1], [PCPPD2], [PCAVFLG], @TenantId
FROM [SunCity_Clinics].[dbo].[CPT4];
GO

PRINT 'Migrating [dbo].[DevTest.htest1]...';
SET IDENTITY_INSERT [dbo].[DevTest.htest1] ON;
INSERT INTO [dbo].[DevTest.htest1] ([uid], [row_code], [row_name], [TenantId])
SELECT [uid], [row_code], [row_name], @TenantId
FROM [SunCity_Clinics].[dbo].[DevTest.htest1];
SET IDENTITY_INSERT [dbo].[DevTest.htest1] OFF;
GO

PRINT 'Migrating [dbo].[DischargeChecklist]...';
INSERT INTO [dbo].[DischargeChecklist] ([Id], [Diagnosis], [PatientId], [TenantId])
SELECT [Id], [Diagnosis], [PatientId], @TenantId
FROM [SunCity_Clinics].[dbo].[DischargeChecklist];
GO

PRINT 'Migrating [dbo].[EmployeeGomaa]...';
SET IDENTITY_INSERT [dbo].[EmployeeGomaa] ON;
INSERT INTO [dbo].[EmployeeGomaa] ([Id], [NameAr], [NameEn], [TenantId])
SELECT [Id], [NameAr], [NameEn], @TenantId
FROM [SunCity_Clinics].[dbo].[EmployeeGomaa];
SET IDENTITY_INSERT [dbo].[EmployeeGomaa] OFF;
GO

PRINT 'Migrating [dbo].[Employee]...';
SET IDENTITY_INSERT [dbo].[Employee] ON;
INSERT INTO [dbo].[Employee] ([Id], [EmployeeCode], [FirstName], [MiddleName], [BeforeLastName], [LastName], [Type], [IsActive], [SubDepartmentId], [JobId], [EmpDesignation], [EmpGrade], [EmergencyContactName], [EmergencyContactRelation], [EmergencyContactPhone], [LocalAddress1], [LocalAddress2], [LocalAddress3], [LocalAddress4], [PermanentAddress1], [PermanentAddress2], [PermanentAddress3], [PermanentAddress4], [BirthDate], [Gender], [Nationality], [MaritalStatus], [AppiontmentDate], [JoiningDate], [ConfermationDate], [ProbationPeriod], [Remark], [Image], [UserId], [CreateBy], [CreateDate], [ModifiedBy], [ModifiedDate], [NoticePeriod], [DistrictID], [GovernemenrID], [RelaginID], [BirthDatePlace], [NationalID], [NationalIDEnddate], [holidaysRoleID], [NoramalDaysVacation], [CasualDaysVacation], [Status], [MilitaryState], [MilitaryStateEndDate], [SessionID], [CardNumber], [CompanyID], [FirstNameEn], [MiddleNameEn], [BeforeLastNameEn], [LastNameEn], [FirstNameAr], [MiddleNameAr], [BeforeLastNameAr], [LastNameAr], [ResidencyExpire], [EmpImage], [hasInsurance], [InsuranceID], [TaxesID], [Classification], [durabilitytypes], [TypeofContractId], [WorkPhone], [PlaceIssuingIdentity], [passportnum], [Passportexpirationdate], [Contractstartingdate], [expirydateofcontract], [Drivinglicensenumber], [dateofissuanceoflicense], [Licenseexpirationdate], [SyndicateCard], [WorkPermit], [ExpirDateOfWorkPermit], [BloodType], [InsuranceDate], [InsuranceValue], [InsuranceJobTitle], [InsuranceNum], [IsCader], [BasicSalaryType], [BasicSalary], [BasicSalaryOf306], [ProbationPeriodType], [IsResearch], [IsSpecialNeeds], [JobDegreeID], [IdentificationTypeID], [ExcludeFromPayroll], [UnionID], [IsHalfTime], [TerminateID], [TerminateReason], [boxmembershipnumber], [IsCEO], [BoxMemberShipDate], [CountryId], [SalayFrom], [BankName], [BankBranch], [EmployeeAccount], [ClearanceDate], [EmployeeCodeInEPayment], [EmployeeType], [OtherPaymenyMethod], [ProbationPeriodEndDate], [NoticePeriodNumberN], [NoticePeriodN], [ISTravelling], [NumberOfTickets], [CountryTravelling], [EmployeeEmail], [EmployeeJobEmail], [transferallow], [housingAllow], [transportallowance], [housingallowance], [PensionAccount], [VacAccount], [TicketAccount], [SalaryAccount], [TerminateDate], [BranchId], [PatientID], [AttendanceMachineCode], [StaffType], [ClincalStaff], [TenantId])
SELECT [Id], [EmployeeCode], [FirstName], [MiddleName], [BeforeLastName], [LastName], [Type], [IsActive], [SubDepartmentId], [JobId], [EmpDesignation], [EmpGrade], [EmergencyContactName], [EmergencyContactRelation], [EmergencyContactPhone], [LocalAddress1], [LocalAddress2], [LocalAddress3], [LocalAddress4], [PermanentAddress1], [PermanentAddress2], [PermanentAddress3], [PermanentAddress4], [BirthDate], [Gender], [Nationality], [MaritalStatus], [AppiontmentDate], [JoiningDate], [ConfermationDate], [ProbationPeriod], [Remark], [Image], [UserId], [CreateBy], [CreateDate], [ModifiedBy], [ModifiedDate], [NoticePeriod], [DistrictID], [GovernemenrID], [RelaginID], [BirthDatePlace], [NationalID], [NationalIDEnddate], [holidaysRoleID], [NoramalDaysVacation], [CasualDaysVacation], [Status], [MilitaryState], [MilitaryStateEndDate], [SessionID], [CardNumber], [CompanyID], [FirstNameEn], [MiddleNameEn], [BeforeLastNameEn], [LastNameEn], [FirstNameAr], [MiddleNameAr], [BeforeLastNameAr], [LastNameAr], [ResidencyExpire], [EmpImage], [hasInsurance], [InsuranceID], [TaxesID], [Classification], [durabilitytypes], [TypeofContractId], [WorkPhone], [PlaceIssuingIdentity], [passportnum], [Passportexpirationdate], [Contractstartingdate], [expirydateofcontract], [Drivinglicensenumber], [dateofissuanceoflicense], [Licenseexpirationdate], [SyndicateCard], [WorkPermit], [ExpirDateOfWorkPermit], [BloodType], [InsuranceDate], [InsuranceValue], [InsuranceJobTitle], [InsuranceNum], [IsCader], [BasicSalaryType], [BasicSalary], [BasicSalaryOf306], [ProbationPeriodType], [IsResearch], [IsSpecialNeeds], [JobDegreeID], [IdentificationTypeID], [ExcludeFromPayroll], [UnionID], [IsHalfTime], [TerminateID], [TerminateReason], [boxmembershipnumber], [IsCEO], [BoxMemberShipDate], [CountryId], [SalayFrom], [BankName], [BankBranch], [EmployeeAccount], [ClearanceDate], [EmployeeCodeInEPayment], [EmployeeType], [OtherPaymenyMethod], [ProbationPeriodEndDate], [NoticePeriodNumberN], [NoticePeriodN], [ISTravelling], [NumberOfTickets], [CountryTravelling], [EmployeeEmail], [EmployeeJobEmail], [transferallow], [housingAllow], [transportallowance], [housingallowance], [PensionAccount], [VacAccount], [TicketAccount], [SalaryAccount], [TerminateDate], [BranchId], [PatientID], [AttendanceMachineCode], [StaffType], [ClincalStaff], @TenantId
FROM [SunCity_Clinics].[dbo].[Employee];
SET IDENTITY_INSERT [dbo].[Employee] OFF;
GO

PRINT 'Migrating [dbo].[ICD10]...';
INSERT INTO [dbo].[ICD10] ([Code], [Description], [F3], [TenantId])
SELECT [Code], [Description], [F3], @TenantId
FROM [SunCity_Clinics].[dbo].[ICD10];
GO

PRINT 'Migrating [dbo].[ICD10_New]...';
INSERT INTO [dbo].[ICD10_New] ([category], [block], [chapter], [description], [TenantId])
SELECT [category], [block], [chapter], [description], @TenantId
FROM [SunCity_Clinics].[dbo].[ICD10_New];
GO

PRINT 'Migrating [dbo].[ItemsDetails]...';
SET IDENTITY_INSERT [dbo].[ItemsDetails] ON;
INSERT INTO [dbo].[ItemsDetails] ([ItemDetailsId], [ItemIdcol], [Code], [TenantId])
SELECT [ItemDetailsId], [ItemIdcol], [Code], @TenantId
FROM [SunCity_Clinics].[dbo].[ItemsDetails];
SET IDENTITY_INSERT [dbo].[ItemsDetails] OFF;
GO

PRINT 'Migrating [dbo].[Items]...';
SET IDENTITY_INSERT [dbo].[Items] ON;
INSERT INTO [dbo].[Items] ([ItemId], [Code], [NameAr], [NameEn], [CreatedDate], [CreatedBy], [LastModifiedBy], [LastModifiedDate], [TenantId])
SELECT [ItemId], [Code], [NameAr], [NameEn], [CreatedDate], [CreatedBy], [LastModifiedBy], [LastModifiedDate], @TenantId
FROM [SunCity_Clinics].[dbo].[Items];
SET IDENTITY_INSERT [dbo].[Items] OFF;
GO

PRINT 'Migrating [dbo].[NewP]...';
SET IDENTITY_INSERT [dbo].[NewP] ON;
INSERT INTO [dbo].[NewP] ([ID], [firstname], [lastname], [mail], [Phone], [ChekinD], [TenantId])
SELECT [ID], [firstname], [lastname], [mail], [Phone], [ChekinD], @TenantId
FROM [SunCity_Clinics].[dbo].[NewP];
SET IDENTITY_INSERT [dbo].[NewP] OFF;
GO

PRINT 'Migrating [dbo].[OperationRoom]...';
SET IDENTITY_INSERT [dbo].[OperationRoom] ON;
INSERT INTO [dbo].[OperationRoom] ([ID], [RoomName], [CompanyID], [TenantId])
SELECT [ID], [RoomName], [CompanyID], @TenantId
FROM [SunCity_Clinics].[dbo].[OperationRoom];
SET IDENTITY_INSERT [dbo].[OperationRoom] OFF;
GO

PRINT 'Migrating [dbo].[Operations]...';
SET IDENTITY_INSERT [dbo].[Operations] ON;
INSERT INTO [dbo].[Operations] ([ID], [Start], [End], [OperationName], [OperationRoomID], [CompanyID], [TenantId])
SELECT [ID], [Start], [End], [OperationName], [OperationRoomID], [CompanyID], @TenantId
FROM [SunCity_Clinics].[dbo].[Operations];
SET IDENTITY_INSERT [dbo].[Operations] OFF;
GO

PRINT 'Migrating [dbo].[Pharmacy.Borrowing]...';
SET IDENTITY_INSERT [dbo].[Pharmacy.Borrowing] ON;
INSERT INTO [dbo].[Pharmacy.Borrowing] ([ID], [NameEN], [NameAR], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [BranchId], [TenantId])
SELECT [ID], [NameEN], [NameAR], [Code], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [BranchId], @TenantId
FROM [SunCity_Clinics].[dbo].[Pharmacy.Borrowing];
SET IDENTITY_INSERT [dbo].[Pharmacy.Borrowing] OFF;
GO

PRINT 'Migrating [dbo].[Query]...';
INSERT INTO [dbo].[Query] ([code], [description], [TenantId])
SELECT [code], [description], @TenantId
FROM [SunCity_Clinics].[dbo].[Query];
GO

PRINT 'Migrating [dbo].[TMP_Search]...';
SET IDENTITY_INSERT [dbo].[TMP_Search] ON;
INSERT INTO [dbo].[TMP_Search] ([Id_PK], [ID], [USR_C], [Description], [Field1_Nu], [Field2_Nu], [Field3_Nu], [Field4_Nu], [Field1_DateTime], [Field2_DateTime], [Field3_DateTime], [Field4_DateTime], [Field1_VarChar], [Field2_VarChar], [Field3_VarChar], [Field4_VarChar], [Field5_VarChar], [Field6_VarChar], [Field7_VarChar], [Field8_VarChar], [Field9_VarChar], [Field10_VarChar], [TenantId])
SELECT [Id_PK], [ID], [USR_C], [Description], [Field1_Nu], [Field2_Nu], [Field3_Nu], [Field4_Nu], [Field1_DateTime], [Field2_DateTime], [Field3_DateTime], [Field4_DateTime], [Field1_VarChar], [Field2_VarChar], [Field3_VarChar], [Field4_VarChar], [Field5_VarChar], [Field6_VarChar], [Field7_VarChar], [Field8_VarChar], [Field9_VarChar], [Field10_VarChar], @TenantId
FROM [SunCity_Clinics].[dbo].[TMP_Search];
SET IDENTITY_INSERT [dbo].[TMP_Search] OFF;
GO

PRINT 'Migrating [dbo].[Tbl_Details]...';
SET IDENTITY_INSERT [dbo].[Tbl_Details] ON;
INSERT INTO [dbo].[Tbl_Details] ([Education], [Degree], [MasterId], [Id], [TenantId])
SELECT [Education], [Degree], [MasterId], [Id], @TenantId
FROM [SunCity_Clinics].[dbo].[Tbl_Details];
SET IDENTITY_INSERT [dbo].[Tbl_Details] OFF;
GO

PRINT 'Migrating [dbo].[Tbl_Master]...';
SET IDENTITY_INSERT [dbo].[Tbl_Master] ON;
INSERT INTO [dbo].[Tbl_Master] ([Firstname], [Lastname], [Id], [TenantId])
SELECT [Firstname], [Lastname], [Id], @TenantId
FROM [SunCity_Clinics].[dbo].[Tbl_Master];
SET IDENTITY_INSERT [dbo].[Tbl_Master] OFF;
GO

PRINT 'Migrating [dbo].[Test002]...';
INSERT INTO [dbo].[Test002] ([ID], [BanksCode], [BanksDescription], [AccNoID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [BanksCode], [BanksDescription], [AccNoID], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[dbo].[Test002];
GO

PRINT 'Migrating [dbo].[Test1]...';
SET IDENTITY_INSERT [dbo].[Test1] ON;
INSERT INTO [dbo].[Test1] ([ID], [Code], [NameArabic], [NameEnglish], [TenantId])
SELECT [ID], [Code], [NameArabic], [NameEnglish], @TenantId
FROM [SunCity_Clinics].[dbo].[Test1];
SET IDENTITY_INSERT [dbo].[Test1] OFF;
GO

PRINT 'Migrating [dbo].[TestTable1]...';
SET IDENTITY_INSERT [dbo].[TestTable1] ON;
INSERT INTO [dbo].[TestTable1] ([ID], [Code], [Name], [NameAr], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [NameAr], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[dbo].[TestTable1];
SET IDENTITY_INSERT [dbo].[TestTable1] OFF;
GO

PRINT 'Migrating [dbo].[Test]...';
SET IDENTITY_INSERT [dbo].[Test] ON;
INSERT INTO [dbo].[Test] ([ID], [Code], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], [TenantId])
SELECT [ID], [Code], [Name], [NameEn], [CreatedBy], [CreatedDate], [LastModifiedBy], [LastModifiedDate], [CompanyID], @TenantId
FROM [SunCity_Clinics].[dbo].[Test];
SET IDENTITY_INSERT [dbo].[Test] OFF;
GO

PRINT 'Migrating [dbo].[Users$]...';
INSERT INTO [dbo].[Users$] ([RegisterNumber], [RegisterYear], [Product type], [DrugType], [Sub-Type], [Scientific Name], [Scientific Name Arabic], [Trade Name], [Trade Name Arabic], [Strength], [StrengthUnit], [PharmaceuticalForm], [AdministrationRoute], [AtcCode1], [AtcCode2], [Size], [SizeUnit], [PackageTypes], [PackageSize], [Legal Status], [Product Control], [Distribute area], [Public price], [shelfLife], [Storage conditions], [Marketing Company], [Marketing Country], [Manufacture Name], [Manufacture Country], [2Manufacture Name], [Manufacture Country1], [Secondry package  manufacture], [Main Agent], [Secosnd Agent], [Third agent], [Marketing Status], [Authorization Status], [TenantId])
SELECT [RegisterNumber], [RegisterYear], [Product type], [DrugType], [Sub-Type], [Scientific Name], [Scientific Name Arabic], [Trade Name], [Trade Name Arabic], [Strength], [StrengthUnit], [PharmaceuticalForm], [AdministrationRoute], [AtcCode1], [AtcCode2], [Size], [SizeUnit], [PackageTypes], [PackageSize], [Legal Status], [Product Control], [Distribute area], [Public price], [shelfLife], [Storage conditions], [Marketing Company], [Marketing Country], [Manufacture Name], [Manufacture Country], [2Manufacture Name], [Manufacture Country1], [Secondry package  manufacture], [Main Agent], [Secosnd Agent], [Third agent], [Marketing Status], [Authorization Status], @TenantId
FROM [SunCity_Clinics].[dbo].[Users$];
GO

PRINT 'Migrating [dbo].[Users$_FilterDatabase]...';
INSERT INTO [dbo].[Users$_FilterDatabase] ([Marketing Country], [TenantId])
SELECT [Marketing Country], @TenantId
FROM [SunCity_Clinics].[dbo].[Users$_FilterDatabase];
GO

PRINT 'Migrating [dbo].[UsersTask]...';
SET IDENTITY_INSERT [dbo].[UsersTask] ON;
INSERT INTO [dbo].[UsersTask] ([UserTaskID], [UserTaskName], [UserTaskAddress], [UserTaskPhone], [CompanyID], [TenantId])
SELECT [UserTaskID], [UserTaskName], [UserTaskAddress], [UserTaskPhone], [CompanyID], @TenantId
FROM [SunCity_Clinics].[dbo].[UsersTask];
SET IDENTITY_INSERT [dbo].[UsersTask] OFF;
GO

PRINT 'Migrating [dbo].[ZatcaCSR]...';
INSERT INTO [dbo].[ZatcaCSR] ([OTP], [privateKey], [TenantId])
SELECT [OTP], [privateKey], @TenantId
FROM [SunCity_Clinics].[dbo].[ZatcaCSR];
GO

PRINT 'Migrating [dbo].[__EFMigrationsHistory]...';
INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion], [TenantId])
SELECT [MigrationId], [ProductVersion], @TenantId
FROM [SunCity_Clinics].[dbo].[__EFMigrationsHistory];
GO

PRINT 'Migrating [dbo].[__MigrationHistory]...';
INSERT INTO [dbo].[__MigrationHistory] ([MigrationId], [ContextKey], [Model], [ProductVersion], [TenantId])
SELECT [MigrationId], [ContextKey], [Model], [ProductVersion], @TenantId
FROM [SunCity_Clinics].[dbo].[__MigrationHistory];
GO

PRINT 'Migrating [dbo].[__MigrationLog]...';
SET IDENTITY_INSERT [dbo].[__MigrationLog] ON;
INSERT INTO [dbo].[__MigrationLog] ([migration_id], [script_checksum], [script_filename], [complete_dt], [applied_by], [deployed], [version], [package_version], [release_version], [sequence_no], [TenantId])
SELECT [migration_id], [script_checksum], [script_filename], [complete_dt], [applied_by], [deployed], [version], [package_version], [release_version], [sequence_no], @TenantId
FROM [SunCity_Clinics].[dbo].[__MigrationLog];
SET IDENTITY_INSERT [dbo].[__MigrationLog] OFF;
GO

PRINT 'Migrating [dbo].[__SchemaSnapshot]...';
INSERT INTO [dbo].[__SchemaSnapshot] ([Snapshot], [LastUpdateDate], [TenantId])
SELECT [Snapshot], [LastUpdateDate], @TenantId
FROM [SunCity_Clinics].[dbo].[__SchemaSnapshot];
GO

PRINT 'Migrating [dbo].[data_patientradiologystudy]...';
SET IDENTITY_INSERT [dbo].[data_patientradiologystudy] ON;
INSERT INTO [dbo].[data_patientradiologystudy] ([uid], [patientname], [radiologyname], [radiotime], [TenantId])
SELECT [uid], [patientname], [radiologyname], [radiotime], @TenantId
FROM [SunCity_Clinics].[dbo].[data_patientradiologystudy];
SET IDENTITY_INSERT [dbo].[data_patientradiologystudy] OFF;
GO

PRINT 'Migrating [dbo].[data_radioimages]...';
SET IDENTITY_INSERT [dbo].[data_radioimages] ON;
INSERT INTO [dbo].[data_radioimages] ([id], [radiouid], [radioimage], [TenantId])
SELECT [id], [radiouid], [radioimage], @TenantId
FROM [SunCity_Clinics].[dbo].[data_radioimages];
SET IDENTITY_INSERT [dbo].[data_radioimages] OFF;
GO

PRINT 'Migrating [dbo].[htest1]...';
SET IDENTITY_INSERT [dbo].[htest1] ON;
INSERT INTO [dbo].[htest1] ([uid], [row_code], [row_name], [TenantId])
SELECT [uid], [row_code], [row_name], @TenantId
FROM [SunCity_Clinics].[dbo].[htest1];
SET IDENTITY_INSERT [dbo].[htest1] OFF;
GO

PRINT 'Migrating [dbo].[icd102019_chapters$]...';
INSERT INTO [dbo].[icd102019_chapters$] ([Chapter No#], [Chapter Title], [TenantId])
SELECT [Chapter No#], [Chapter Title], @TenantId
FROM [SunCity_Clinics].[dbo].[icd102019_chapters$];
GO

PRINT 'Migrating [dbo].[icd102019_codes$]...';
INSERT INTO [dbo].[icd102019_codes$] ([Code_Level ], [code_tye], [T-code_type], [chapter No#], [group_code], [code_W_dagger], [code_N_dag], [code], [Description], [parent_group], [TenantId])
SELECT [Code_Level ], [code_tye], [T-code_type], [chapter No#], [group_code], [code_W_dagger], [code_N_dag], [code], [Description], [parent_group], @TenantId
FROM [SunCity_Clinics].[dbo].[icd102019_codes$];
GO

PRINT 'Migrating [dbo].[icd102019_groups$]...';
INSERT INTO [dbo].[icd102019_groups$] ([start_code], [end_code], [Chapter No#], [Group Title], [TenantId])
SELECT [start_code], [end_code], [Chapter No#], [Group Title], @TenantId
FROM [SunCity_Clinics].[dbo].[icd102019_groups$];
GO

PRINT 'Migrating [dbo].[number]...';
INSERT INTO [dbo].[number] ([n], [TenantId])
SELECT [n], @TenantId
FROM [SunCity_Clinics].[dbo].[number];
GO

PRINT 'Migrating [dbo].[tb_data_icd]...';
INSERT INTO [dbo].[tb_data_icd] ([col1], [col2], [col3], [col4], [col5], [col6], [col7], [col8], [col9], [col10], [col11], [col12], [col13], [col14], [TenantId])
SELECT [col1], [col2], [col3], [col4], [col5], [col6], [col7], [col8], [col9], [col10], [col11], [col12], [col13], [col14], @TenantId
FROM [SunCity_Clinics].[dbo].[tb_data_icd];
GO

