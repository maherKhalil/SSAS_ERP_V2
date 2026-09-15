# HIS Tables Specification

This document specifies the HIS (Health Information System) tables extracted from the legacy `SunCity_Clinics` database.
These tables are NOT part of the ERP schemas (Finance, HR, etc.) and represent the clinical side of the product.

## Tenancy Requirement
The legacy system was on-premises and did not include a `TenantId`. During migration and implementation in the new SSAS_ERP_V2, **every HIS table must have a `TenantId UNIQUEIDENTIFIER NOT NULL` column added** to support multi-tenancy.

## Tasks for Coder Subagent
1. **Entity Generation**: Scaffold EF Core entities for the schemas listed below.
2. **Tenancy Enforcement**: Implement `ITenantOwnedEntity` on all clinical entities so EF Core automatically filters by `TenantId`.
3. **Migration Integration**: Ensure the new DB context includes these `DbSet`s and generate the EF migrations.

## HIS Schemas and Tables

### Schema: `ApplicationSetup`
Total tables: 62

| Table Name | Notes |
|---|---|
| `CPTCode` | Requires `TenantId` |
| `City` | Requires `TenantId` |
| `ClassificationType` | Requires `TenantId` |
| `ClinicalCare` | Requires `TenantId` |
| `ClinicalCareDetails` | Requires `TenantId` |
| `ClinicalCareFunction` | Requires `TenantId` |
| `Company` | Requires `TenantId` |
| `CompanyContact` | Requires `TenantId` |
| `Components` | Requires `TenantId` |
| `ConfidentialityClassification` | Requires `TenantId` |
| `ContactType` | Requires `TenantId` |
| `CostCenter` | Requires `TenantId` |
| `CostCenterCompanies` | Requires `TenantId` |
| `CostCenterType` | Requires `TenantId` |
| `Country` | Requires `TenantId` |
| `CountryB` | Requires `TenantId` |
| `Currency` | Requires `TenantId` |
| `CurrencyRate` | Requires `TenantId` |
| `Customer` | Requires `TenantId` |
| `Dictionary` | Requires `TenantId` |
| `District` | Requires `TenantId` |
| `Doctor` | Requires `TenantId` |
| `DoctorFees` | Requires `TenantId` |
| `ExcludeDoctorsHolidays` | Requires `TenantId` |
| `Governorate` | Requires `TenantId` |
| `Holiday` | Requires `TenantId` |
| `HospitalNews` | Requires `TenantId` |
| `IdentificationTypes` | Requires `TenantId` |
| `InstallMentDetails` | Requires `TenantId` |
| `InsuranceServices` | Requires `TenantId` |
| `InternalCommunication` | Requires `TenantId` |
| `InvestigationGroup` | Requires `TenantId` |
| `InvestigationGroupDetail` | Requires `TenantId` |
| `Item` | Requires `TenantId` |
| `LinkedItem` | Requires `TenantId` |
| `MilitraryForce` | Requires `TenantId` |
| `MilitraryRanks` | Requires `TenantId` |
| `Nationality` | Requires `TenantId` |
| `NurseHead` | Requires `TenantId` |
| `PackageDefType` | Requires `TenantId` |
| `PackageDrags` | Requires `TenantId` |
| `PackageServices` | Requires `TenantId` |
| `PackageSupItem` | Requires `TenantId` |
| `PagePrefix` | Requires `TenantId` |
| `PaymentRole` | Requires `TenantId` |
| `QRImage` | Requires `TenantId` |
| `Report_Generator` | Requires `TenantId` |
| `Report_Generator_Columns` | Requires `TenantId` |
| `Report_Generator_Conditions` | Requires `TenantId` |
| `Report_Generator_Filters` | Requires `TenantId` |
| `Report_Generator_Tables` | Requires `TenantId` |
| `RevenueType` | Requires `TenantId` |
| `Service` | Requires `TenantId` |
| `ServicePackageDepartment` | Requires `TenantId` |
| `Setting` | Requires `TenantId` |
| `Supplier` | Requires `TenantId` |
| `SystemParameter` | Requires `TenantId` |
| `TypeOfWeapon` | Requires `TenantId` |
| `UserLog` | Requires `TenantId` |
| `UserType` | Requires `TenantId` |
| `allocationIncome` | Requires `TenantId` |
| `patientconsent` | Requires `TenantId` |

### Schema: `Billing`
Total tables: 95

| Table Name | Notes |
|---|---|
| `AdvanceReceipts` | Requires `TenantId` |
| `AdvanceReceiptsItems` | Requires `TenantId` |
| `AdvanceReceiptsItemsRefund` | Requires `TenantId` |
| `AdvanceReceiptsPayment` | Requires `TenantId` |
| `AdvanceReceiptsPaymentRefund` | Requires `TenantId` |
| `AssginCashierForPOS` | Requires `TenantId` |
| `AssginCostCenterForPOS` | Requires `TenantId` |
| `CashierBatches` | Requires `TenantId` |
| `CashierBox` | Requires `TenantId` |
| `CashierBoxCloseShift` | Requires `TenantId` |
| `CashierBoxCustody` | Requires `TenantId` |
| `CashierBoxDailyClose` | Requires `TenantId` |
| `CashierBoxDailyOpening` | Requires `TenantId` |
| `CashierBoxOpenSessions` | Requires `TenantId` |
| `CashierBoxTransfareToBank` | Requires `TenantId` |
| `CashierBoxUsers` | Requires `TenantId` |
| `CashierGroup` | Requires `TenantId` |
| `CashierOpenBalance` | Requires `TenantId` |
| `CashierWorkList` | Requires `TenantId` |
| `CreditCodeMaster` | Requires `TenantId` |
| `CustomerCategory` | Requires `TenantId` |
| `CustomerCategoryCostCenters` | Requires `TenantId` |
| `CustomerCategoryDrugType` | Requires `TenantId` |
| `CustomerCategoryDrugs` | Requires `TenantId` |
| `CustomerCategoryServices` | Requires `TenantId` |
| `CustomerCategorySupItems` | Requires `TenantId` |
| `CustomerCategorySupplies` | Requires `TenantId` |
| `CustomerMaster` | Requires `TenantId` |
| `CustomerMasterDetails` | Requires `TenantId` |
| `CustomerTypes` | Requires `TenantId` |
| `DepartmentConsumption` | Requires `TenantId` |
| `DiscountOFFers` | Requires `TenantId` |
| `DiscountPriceListDepartment` | Requires `TenantId` |
| `DiscountPriceListHeader` | Requires `TenantId` |
| `DiscountPriceListServices` | Requires `TenantId` |
| `DoctorFeesPayment` | Requires `TenantId` |
| `DoctorFeesPercentage` | Requires `TenantId` |
| `DoctorFees_Doctors` | Requires `TenantId` |
| `EclaimEdit` | Requires `TenantId` |
| `ICDCodes` | Requires `TenantId` |
| `ICDGroups` | Requires `TenantId` |
| `INS_Company_Contract_Discount` | Requires `TenantId` |
| `Ins_Company_Policy` | Requires `TenantId` |
| `InsuranceAdvice` | Requires `TenantId` |
| `InsuranceClaimDetail` | Requires `TenantId` |
| `InsuranceClaimMaster` | Requires `TenantId` |
| `InsuranceCompanies` | Requires `TenantId` |
| `InsuranceCompanyCodes` | Requires `TenantId` |
| `Insurance_Comp_Contract` | Requires `TenantId` |
| `Insurance_Setting` | Requires `TenantId` |
| `InvoiceReceipt` | Requires `TenantId` |
| `NphiesAPAItems` | Requires `TenantId` |
| `NphiesLinks` | Requires `TenantId` |
| `NphiesPatientInvRequest_claim` | Requires `TenantId` |
| `NphiesRequest` | Requires `TenantId` |
| `NphiesResponse` | Requires `TenantId` |
| `NphiesResponseExtension` | Requires `TenantId` |
| `NpiesPatientInvRequest` | Requires `TenantId` |
| `OutRersourceDeals` | Requires `TenantId` |
| `PatientBill` | Requires `TenantId` |
| `PatientBillservices` | Requires `TenantId` |
| `PatientBillservicesCanceled` | Requires `TenantId` |
| `PatientDeposit` | Requires `TenantId` |
| `PatientDepositHistory` | Requires `TenantId` |
| `PatientInsuranceLimitMaster` | Requires `TenantId` |
| `PatientInsuranceLimitdetail` | Requires `TenantId` |
| `Patient_InsuranceApproved` | Requires `TenantId` |
| `PayExaminationPrice` | Requires `TenantId` |
| `PaymentDetails` | Requires `TenantId` |
| `PaymentModes` | Requires `TenantId` |
| `PaymentReconciliation` | Requires `TenantId` |
| `PharmacyBillDetail` | Requires `TenantId` |
| `PharmacyBillHeader` | Requires `TenantId` |
| `PointOfSale` | Requires `TenantId` |
| `PriceListDepartment` | Requires `TenantId` |
| `PriceListHeader` | Requires `TenantId` |
| `PriceListServices` | Requires `TenantId` |
| `RCM_Settings` | Requires `TenantId` |
| `RefundReceipts` | Requires `TenantId` |
| `RegisterPackage` | Requires `TenantId` |
| `RegisterPackageInstallment` | Requires `TenantId` |
| `Rejected_Claim` | Requires `TenantId` |
| `RevenueTypes` | Requires `TenantId` |
| `SpecialUserDiscount` | Requires `TenantId` |
| `Sponsor` | Requires `TenantId` |
| `SupplierDues` | Requires `TenantId` |
| `TaxReturn` | Requires `TenantId` |
| `TicketingSetting` | Requires `TenantId` |
| `VisitorPayment` | Requires `TenantId` |
| `Voucher` | Requires `TenantId` |
| `VoucherDetails` | Requires `TenantId` |
| `Yearly_Tender` | Requires `TenantId` |
| `ZatcaEInvoice` | Requires `TenantId` |
| `ins_Company_Class` | Requires `TenantId` |
| `ins_Company_ClassCostCenter` | Requires `TenantId` |

### Schema: `BloodBank`
Total tables: 41

| Table Name | Notes |
|---|---|
| `BloodBankInventory` | Requires `TenantId` |
| `BloodBankInventoryBloodGroup` | Requires `TenantId` |
| `BloodBankLPODetail` | Requires `TenantId` |
| `BloodBankLPOHeader` | Requires `TenantId` |
| `BloodBankSettings` | Requires `TenantId` |
| `BloodBanks` | Requires `TenantId` |
| `BloodGroup` | Requires `TenantId` |
| `BloodGroupCompatable` | Requires `TenantId` |
| `BloodProduct` | Requires `TenantId` |
| `BloodServicePrice` | Requires `TenantId` |
| `BloodStream` | Requires `TenantId` |
| `BloodStreamDetails` | Requires `TenantId` |
| `BloodTesting` | Requires `TenantId` |
| `BloodTestingResultDetail` | Requires `TenantId` |
| `BloodTestingResultMaster` | Requires `TenantId` |
| `BloodTransferRequest` | Requires `TenantId` |
| `BloodTransfusion` | Requires `TenantId` |
| `BloodTransfustionActiontaken` | Requires `TenantId` |
| `Campaign` | Requires `TenantId` |
| `ClabsiBundle` | Requires `TenantId` |
| `DBloodBags` | Requires `TenantId` |
| `DispenseBloodBags` | Requires `TenantId` |
| `DonationInfo` | Requires `TenantId` |
| `DonationInvestgationMaster` | Requires `TenantId` |
| `DonationRestriction` | Requires `TenantId` |
| `DonorQuestionnaireDetails` | Requires `TenantId` |
| `DonorRegistration` | Requires `TenantId` |
| `DonorVitalSigns` | Requires `TenantId` |
| `ExternalFacilityDetails` | Requires `TenantId` |
| `ExternalFacilityHeader` | Requires `TenantId` |
| `FacilityMaster` | Requires `TenantId` |
| `InventoryAdjustment` | Requires `TenantId` |
| `InventoryAdjustmentEntry` | Requires `TenantId` |
| `IssuetoInventory` | Requires `TenantId` |
| `IssuetoInventoryEntry` | Requires `TenantId` |
| `OutRersourceDispenseBags` | Requires `TenantId` |
| `OutResources` | Requires `TenantId` |
| `ReturnDispenseBloodBags` | Requires `TenantId` |
| `TransferRequestDispense` | Requires `TenantId` |
| `TransfuionProcessing` | Requires `TenantId` |
| `clabsibundleDetails` | Requires `TenantId` |

### Schema: `CSSD`
Total tables: 10

| Table Name | Notes |
|---|---|
| `CSSDRequest` | Requires `TenantId` |
| `CSSDRequestDetails` | Requires `TenantId` |
| `LaundryActionType` | Requires `TenantId` |
| `LaundryRequest` | Requires `TenantId` |
| `Machine` | Requires `TenantId` |
| `SterilizationMachines` | Requires `TenantId` |
| `SterilizationMethod` | Requires `TenantId` |
| `TrayDetails` | Requires `TenantId` |
| `TrayTypes` | Requires `TenantId` |
| `TraysMaster` | Requires `TenantId` |

### Schema: `Chemotherapy`
Total tables: 9

| Table Name | Notes |
|---|---|
| `AdministrationRoutes` | Requires `TenantId` |
| `ChemotherapyGroups` | Requires `TenantId` |
| `ChemotherapyOrderDetails` | Requires `TenantId` |
| `ChemotherapyOrderDetailsStatus` | Requires `TenantId` |
| `ChemotherapyOrderHeader` | Requires `TenantId` |
| `ChemotherapyProtocol` | Requires `TenantId` |
| `ChemotherapySetupDetails` | Requires `TenantId` |
| `ChemotherapySetupHeader` | Requires `TenantId` |
| `RoutesofAdministration` | Requires `TenantId` |

### Schema: `ClinicalPharmacy`
Total tables: 5

| Table Name | Notes |
|---|---|
| `AdversDrugReaction` | Requires `TenantId` |
| `Categorized_MedicalErrors` | Requires `TenantId` |
| `ClinicalIntervention` | Requires `TenantId` |
| `ClinicalPharmacySetting` | Requires `TenantId` |
| `MedicationError` | Requires `TenantId` |

### Schema: `Emergency`
Total tables: 22

| Table Name | Notes |
|---|---|
| `ABGs` | Requires `TenantId` |
| `ArrivalType` | Requires `TenantId` |
| `CasePriority` | Requires `TenantId` |
| `ComplainSetup` | Requires `TenantId` |
| `DischargeType` | Requires `TenantId` |
| `DoctorSchedule` | Requires `TenantId` |
| `EmergencyDetails` | Requires `TenantId` |
| `EmergencyOrganization` | Requires `TenantId` |
| `EmergencySchedule` | Requires `TenantId` |
| `EmergencyUnit` | Requires `TenantId` |
| `EmergencyUnit_History` | Requires `TenantId` |
| `EmergencyVisit` | Requires `TenantId` |
| `FastEmergancy` | Requires `TenantId` |
| `NormalEmergancy` | Requires `TenantId` |
| `NursingAssessment` | Requires `TenantId` |
| `OnCallDoctors` | Requires `TenantId` |
| `PatientStatus` | Requires `TenantId` |
| `RiskType` | Requires `TenantId` |
| `ShiftType` | Requires `TenantId` |
| `TriagCategory` | Requires `TenantId` |
| `TriagCategoryItems` | Requires `TenantId` |
| `VitalParameter` | Requires `TenantId` |

### Schema: `HotelServices`
Total tables: 4

| Table Name | Notes |
|---|---|
| `ActionDefination` | Requires `TenantId` |
| `HotelServiceOrder` | Requires `TenantId` |
| `ItemAction` | Requires `TenantId` |
| `ScheduledActions` | Requires `TenantId` |

### Schema: `InPatient`
Total tables: 173

| Table Name | Notes |
|---|---|
| `AccommodationType` | Requires `TenantId` |
| `AccommodationTypes` | Requires `TenantId` |
| `AdmissionCategory` | Requires `TenantId` |
| `AdmissionPurpose` | Requires `TenantId` |
| `AdmissionRequest` | Requires `TenantId` |
| `AdmitPatientToNewMedUnit` | Requires `TenantId` |
| `AdmitPatients` | Requires `TenantId` |
| `Bed` | Requires `TenantId` |
| `BedLockPurpose` | Requires `TenantId` |
| `BedRenewal` | Requires `TenantId` |
| `BedStatus` | Requires `TenantId` |
| `BedStatusChangeTracker` | Requires `TenantId` |
| `BedSwap` | Requires `TenantId` |
| `BedTracker` | Requires `TenantId` |
| `BedType` | Requires `TenantId` |
| `BedWardArrangement` | Requires `TenantId` |
| `Blacklist` | Requires `TenantId` |
| `Buildings` | Requires `TenantId` |
| `CRTPImplantationReport` | Requires `TenantId` |
| `CafeteriaCharges` | Requires `TenantId` |
| `CancelAdmission` | Requires `TenantId` |
| `CancelAdmitPatientInAnotherMedicalUnit` | Requires `TenantId` |
| `CancelDischarge` | Requires `TenantId` |
| `CancelDischargePatientInAnotherMedicalUnit` | Requires `TenantId` |
| `CancelDischargeReason` | Requires `TenantId` |
| `CancelIntialDischarge` | Requires `TenantId` |
| `CancelTypesMaster` | Requires `TenantId` |
| `CancellationOfPatientRequest` | Requires `TenantId` |
| `CardiacCatheterization` | Requires `TenantId` |
| `Cardiac_Electrphysiology` | Requires `TenantId` |
| `ChangePatientDoctor` | Requires `TenantId` |
| `ClottingTimeDetails` | Requires `TenantId` |
| `ClottingTimeMaster` | Requires `TenantId` |
| `ConsultaionEnum` | Requires `TenantId` |
| `Consultation_Request` | Requires `TenantId` |
| `CoronaryIntervention` | Requires `TenantId` |
| `CurrentMedication` | Requires `TenantId` |
| `DCAF` | Requires `TenantId` |
| `Delivery` | Requires `TenantId` |
| `DeliveryDetails` | Requires `TenantId` |
| `DeliveryType` | Requires `TenantId` |
| `DestinationOfPatient` | Requires `TenantId` |
| `DischargeReason` | Requires `TenantId` |
| `DischargeSummary` | Requires `TenantId` |
| `DischargeSummaryICD` | Requires `TenantId` |
| `DischargeType` | Requires `TenantId` |
| `Discharge_Order` | Requires `TenantId` |
| `Dobutamine_Stress_Echocardiography` | Requires `TenantId` |
| `Dobutamine_Stress_Echocardiography_Comments` | Requires `TenantId` |
| `Dobutamine_Stress_Echocardiography_MGM` | Requires `TenantId` |
| `DoctorInstructionClassification` | Requires `TenantId` |
| `DoctorNote` | Requires `TenantId` |
| `DoctorTransfer` | Requires `TenantId` |
| `DrugChart` | Requires `TenantId` |
| `ECGReport` | Requires `TenantId` |
| `Escort` | Requires `TenantId` |
| `EscortDetails` | Requires `TenantId` |
| `EscortEnterance` | Requires `TenantId` |
| `Estimatedmission` | Requires `TenantId` |
| `EstimatedmissionDetails` | Requires `TenantId` |
| `EyesightMeasurement` | Requires `TenantId` |
| `Finding` | Requires `TenantId` |
| `FindingFlag` | Requires `TenantId` |
| `Floors` | Requires `TenantId` |
| `FrequencyMaster` | Requires `TenantId` |
| `GeneralNursingCarePlanHeader` | Requires `TenantId` |
| `GeneralNursingCarePlan_TagsValues` | Requires `TenantId` |
| `GitImages` | Requires `TenantId` |
| `HeadUpTilt` | Requires `TenantId` |
| `HeadUpTiltDiagnosis` | Requires `TenantId` |
| `HeadUpTiltTable` | Requires `TenantId` |
| `HeadUpTiltTableICDCodes` | Requires `TenantId` |
| `HebaTest` | Requires `TenantId` |
| `Holter` | Requires `TenantId` |
| `InfectiousDiseaseScreening` | Requires `TenantId` |
| `InitialDischarge` | Requires `TenantId` |
| `InitiateDischargeTypes` | Requires `TenantId` |
| `InpatientSetting` | Requires `TenantId` |
| `LinkCostEstimation` | Requires `TenantId` |
| `MaintenanceTypes` | Requires `TenantId` |
| `MedicalObservation` | Requires `TenantId` |
| `MedicalObservationDental` | Requires `TenantId` |
| `MedicalObservationDepartmentDetails` | Requires `TenantId` |
| `MedicalObservationICDCodes` | Requires `TenantId` |
| `MedicalObservationOptical` | Requires `TenantId` |
| `MedicalObservation_CCU_MICU` | Requires `TenantId` |
| `MedicalObservation_Git` | Requires `TenantId` |
| `MedicalObservation_MedicationHistory` | Requires `TenantId` |
| `MedicalObservation_Obs_Gyn` | Requires `TenantId` |
| `MedicalObservation_Paediatrics` | Requires `TenantId` |
| `MedicalObservation_PastHistory` | Requires `TenantId` |
| `MedicalObservation_Plastic` | Requires `TenantId` |
| `MedicalObservation_TagsValues` | Requires `TenantId` |
| `MovingPatient` | Requires `TenantId` |
| `MyocardialPerfusionImaging` | Requires `TenantId` |
| `NewBorn` | Requires `TenantId` |
| `NurseAssessmentHeader` | Requires `TenantId` |
| `NurseAssessment_TagsValues` | Requires `TenantId` |
| `NurseStation_Drug` | Requires `TenantId` |
| `NursingAdmissionAssessment` | Requires `TenantId` |
| `NursingNote` | Requires `TenantId` |
| `OCAF` | Requires `TenantId` |
| `OperationRequest` | Requires `TenantId` |
| `OperationRoom` | Requires `TenantId` |
| `OperationTheatre` | Requires `TenantId` |
| `OperationTheatreDailyDuty` | Requires `TenantId` |
| `OperationWard` | Requires `TenantId` |
| `OrderCategoryDetail` | Requires `TenantId` |
| `OrderCategoryMaster` | Requires `TenantId` |
| `OrderMemberShip` | Requires `TenantId` |
| `OrderParameter` | Requires `TenantId` |
| `OrderType` | Requires `TenantId` |
| `OrdersEntery` | Requires `TenantId` |
| `OutResourceType` | Requires `TenantId` |
| `PageTags` | Requires `TenantId` |
| `PatientAllergyNew` | Requires `TenantId` |
| `PatientDiet` | Requires `TenantId` |
| `PatientDietManagement` | Requires `TenantId` |
| `PatientDischarge` | Requires `TenantId` |
| `PatientFamilyEducation` | Requires `TenantId` |
| `PatientOrderDetail` | Requires `TenantId` |
| `PatientOrderMaster` | Requires `TenantId` |
| `PatientType` | Requires `TenantId` |
| `Patient_Family` | Requires `TenantId` |
| `PermanentPacemakerImplantationReport` | Requires `TenantId` |
| `PostOPNursingCarePlanHeader` | Requires `TenantId` |
| `PostOPNursingCarePlan_TagsValues` | Requires `TenantId` |
| `PostRoomCharges` | Requires `TenantId` |
| `PreAnesthesiaEvaluation` | Requires `TenantId` |
| `PreOperative` | Requires `TenantId` |
| `PreOperativeMarking` | Requires `TenantId` |
| `Prescription` | Requires `TenantId` |
| `PrescriptionDetails` | Requires `TenantId` |
| `PrescriptionDispenseSetting` | Requires `TenantId` |
| `ProvisionalDiagnosis` | Requires `TenantId` |
| `ReferralType` | Requires `TenantId` |
| `RegisteringPackage` | Requires `TenantId` |
| `RegisteringPackageinstallment` | Requires `TenantId` |
| `RepetType` | Requires `TenantId` |
| `RequestProceduresDetails` | Requires `TenantId` |
| `RequestProceduresHeader` | Requires `TenantId` |
| `RequestSuppliesDetails` | Requires `TenantId` |
| `RequestSuppliesHeader` | Requires `TenantId` |
| `RequestSupplyReturnDetails` | Requires `TenantId` |
| `RequestSupplyReturnHeader` | Requires `TenantId` |
| `Room` | Requires `TenantId` |
| `RoomAccommodationTypes` | Requires `TenantId` |
| `RoomTransfer` | Requires `TenantId` |
| `RoomType` | Requires `TenantId` |
| `ScheduleOT` | Requires `TenantId` |
| `ScheduleSurgery` | Requires `TenantId` |
| `Surgery` | Requires `TenantId` |
| `SurgeryType` | Requires `TenantId` |
| `Tags` | Requires `TenantId` |
| `TelephoneCharges` | Requires `TenantId` |
| `TemporaryDischargeType` | Requires `TenantId` |
| `TemporaryExit` | Requires `TenantId` |
| `TimeBoundServiceRequestDetails` | Requires `TenantId` |
| `TimeBoundServiceRequestHeader` | Requires `TenantId` |
| `UCAF` | Requires `TenantId` |
| `VirtualClinic` | Requires `TenantId` |
| `VitalParameters` | Requires `TenantId` |
| `VitalSettings` | Requires `TenantId` |
| `VitalSigns` | Requires `TenantId` |
| `VitalTypeGroup` | Requires `TenantId` |
| `Ward` | Requires `TenantId` |
| `WardCategory` | Requires `TenantId` |
| `WardPatientPrescriptions` | Requires `TenantId` |
| `WardPharmacy` | Requires `TenantId` |
| `WardPharmacyDetails` | Requires `TenantId` |
| `WardPharmacyPaymentDetails` | Requires `TenantId` |
| `WardType` | Requires `TenantId` |
| `WishList` | Requires `TenantId` |

### Schema: `InfectionControl`
Total tables: 33

| Table Name | Notes |
|---|---|
| `AIDS_Survey` | Requires `TenantId` |
| `AIDS_investigation` | Requires `TenantId` |
| `AreaGroups` | Requires `TenantId` |
| `Areas` | Requires `TenantId` |
| `BloodExposure` | Requires `TenantId` |
| `Element` | Requires `TenantId` |
| `Emp_OtherVaccination_Details` | Requires `TenantId` |
| `Emp_OtherVaccination_master` | Requires `TenantId` |
| `EmployeesCovid_19Detail` | Requires `TenantId` |
| `EmployeesCovid_19Master` | Requires `TenantId` |
| `EmployeesVaccinations` | Requires `TenantId` |
| `Group` | Requires `TenantId` |
| `GroupElements` | Requires `TenantId` |
| `HBV_Vaccination` | Requires `TenantId` |
| `HandHigine` | Requires `TenantId` |
| `HandHigineDet` | Requires `TenantId` |
| `ICRelatedSvsEvaluation` | Requires `TenantId` |
| `ICUServices` | Requires `TenantId` |
| `IncidentReportReview` | Requires `TenantId` |
| `InfectionEvaluationDetail` | Requires `TenantId` |
| `InfectionEvaluationHeader` | Requires `TenantId` |
| `KPIs` | Requires `TenantId` |
| `MARSA` | Requires `TenantId` |
| `NotificationsAcupuncture` | Requires `TenantId` |
| `OVRForm` | Requires `TenantId` |
| `PersonInvolved` | Requires `TenantId` |
| `PreventiveAction` | Requires `TenantId` |
| `PreventiveMedicine` | Requires `TenantId` |
| `StaffHealthCareHepatitisB` | Requires `TenantId` |
| `StaffHealthCareProgram` | Requires `TenantId` |
| `StaffHealthCareProgramDetails` | Requires `TenantId` |
| `StaffHealthCareProgramDetails_Vac` | Requires `TenantId` |
| `Vap` | Requires `TenantId` |

### Schema: `Laboratory`
Total tables: 49

| Table Name | Notes |
|---|---|
| `AllowUsersToShowReports` | Requires `TenantId` |
| `Antibiotics` | Requires `TenantId` |
| `ContainerType` | Requires `TenantId` |
| `ExternalAgencies` | Requires `TenantId` |
| `FarmType` | Requires `TenantId` |
| `Farms` | Requires `TenantId` |
| `LabAnalyzers` | Requires `TenantId` |
| `LabServiceItemLinkMaster` | Requires `TenantId` |
| `LabServicesPeriod` | Requires `TenantId` |
| `LabTestItem` | Requires `TenantId` |
| `LabUnit` | Requires `TenantId` |
| `LaboratorySetting` | Requires `TenantId` |
| `Labs` | Requires `TenantId` |
| `LabsDevices` | Requires `TenantId` |
| `LabsStores` | Requires `TenantId` |
| `LabsTechnicans` | Requires `TenantId` |
| `MergeSamples` | Requires `TenantId` |
| `OrganismAntibioticSensitivity` | Requires `TenantId` |
| `Organisms` | Requires `TenantId` |
| `PatientFarm_Details` | Requires `TenantId` |
| `Patient_Farms` | Requires `TenantId` |
| `PermittedStaff` | Requires `TenantId` |
| `RadResultImages` | Requires `TenantId` |
| `RejectSample` | Requires `TenantId` |
| `ResultEntry` | Requires `TenantId` |
| `ResultEntryDetail` | Requires `TenantId` |
| `ResultEntryDetails_Findings` | Requires `TenantId` |
| `ResultRanges` | Requires `TenantId` |
| `ResultRangesDetails` | Requires `TenantId` |
| `ResultValueDetails` | Requires `TenantId` |
| `ResultValueHeader` | Requires `TenantId` |
| `Results` | Requires `TenantId` |
| `SampleCollectionMedia` | Requires `TenantId` |
| `SampleEntry` | Requires `TenantId` |
| `SampleType` | Requires `TenantId` |
| `SamplesDispatchedDetails` | Requires `TenantId` |
| `SamplesDispatchedHeader` | Requires `TenantId` |
| `SamplesReceivedDetails` | Requires `TenantId` |
| `SamplesReceivedHeader` | Requires `TenantId` |
| `SamplesTransfere` | Requires `TenantId` |
| `Sections` | Requires `TenantId` |
| `SelectionType` | Requires `TenantId` |
| `Service_ResultValueHeader` | Requires `TenantId` |
| `SpecimenTypeAssociation` | Requires `TenantId` |
| `SpecimenTypes` | Requires `TenantId` |
| `TestDetails` | Requires `TenantId` |
| `TestResultLinking` | Requires `TenantId` |
| `TestResultLinkingEntry` | Requires `TenantId` |
| `Tests` | Requires `TenantId` |

### Schema: `Laundry`
Total tables: 1

| Table Name | Notes |
|---|---|
| `LaundryActions` | Requires `TenantId` |

### Schema: `LegalAffairs`
Total tables: 23

| Table Name | Notes |
|---|---|
| `Affair` | Requires `TenantId` |
| `AffairDetails` | Requires `TenantId` |
| `Complaint` | Requires `TenantId` |
| `ComplaintDetails` | Requires `TenantId` |
| `CourtDetails` | Requires `TenantId` |
| `CourtMaster` | Requires `TenantId` |
| `ExecutionAndReservations` | Requires `TenantId` |
| `ExecutionAndReservationsDetails` | Requires `TenantId` |
| `Fatwa` | Requires `TenantId` |
| `GeneralSaving` | Requires `TenantId` |
| `Incoming` | Requires `TenantId` |
| `Investigation` | Requires `TenantId` |
| `LegalContracts` | Requires `TenantId` |
| `LegalSetting` | Requires `TenantId` |
| `LowyersTasks` | Requires `TenantId` |
| `OtherIssue` | Requires `TenantId` |
| `OutgoingIssues` | Requires `TenantId` |
| `Stab` | Requires `TenantId` |
| `StabDetails` | Requires `TenantId` |
| `StoppedCanceledAffair` | Requires `TenantId` |
| `WorkDistributionAmongTechnicalMembers` | Requires `TenantId` |
| `lawyer` | Requires `TenantId` |
| `succinctness` | Requires `TenantId` |

### Schema: `Maintenance`
Total tables: 23

| Table Name | Notes |
|---|---|
| `AsstesWarenty` | Requires `TenantId` |
| `CustomMaintenanceRequest` | Requires `TenantId` |
| `CustomMaintenanceRequestDetails` | Requires `TenantId` |
| `DirectMaintenanceRequestsToMaintenanceManager` | Requires `TenantId` |
| `EmergencyMaintenance` | Requires `TenantId` |
| `Employee_GasBon` | Requires `TenantId` |
| `Machine_Maintenance_Schedule` | Requires `TenantId` |
| `Machines` | Requires `TenantId` |
| `MaintenanceRecorde` | Requires `TenantId` |
| `MaintenanceRequest` | Requires `TenantId` |
| `MaintenanceRequestsToWorkshop` | Requires `TenantId` |
| `MaintenanceSetting` | Requires `TenantId` |
| `MaintenanceType` | Requires `TenantId` |
| `Maintenance_SpareParts` | Requires `TenantId` |
| `OrginalMaintenanceRequest` | Requires `TenantId` |
| `VehicleReq_Persons` | Requires `TenantId` |
| `VehicleRequest` | Requires `TenantId` |
| `Vehicles` | Requires `TenantId` |
| `WarentyAlarmRequest` | Requires `TenantId` |
| `WorkOrders` | Requires `TenantId` |
| `WorkShop` | Requires `TenantId` |
| `WorkShop_Authority` | Requires `TenantId` |
| `WorkShop_Employee` | Requires `TenantId` |

### Schema: `Marketing`
Total tables: 92

| Table Name | Notes |
|---|---|
| `ActivityPhases` | Requires `TenantId` |
| `ActivityPlans` | Requires `TenantId` |
| `ActivityProcessDetails` | Requires `TenantId` |
| `ActivityProcessHeader` | Requires `TenantId` |
| `ActivityTypes` | Requires `TenantId` |
| `BusinessClassification` | Requires `TenantId` |
| `CallLists` | Requires `TenantId` |
| `CallListsTarget` | Requires `TenantId` |
| `Campaign` | Requires `TenantId` |
| `CampaignGroup` | Requires `TenantId` |
| `CampaignTarget` | Requires `TenantId` |
| `CampaignTypes` | Requires `TenantId` |
| `Carrier` | Requires `TenantId` |
| `CarrierCompany` | Requires `TenantId` |
| `CaseCategory` | Requires `TenantId` |
| `Character` | Requires `TenantId` |
| `CommissionCalculation` | Requires `TenantId` |
| `CompanyChains` | Requires `TenantId` |
| `Contact` | Requires `TenantId` |
| `CustomerPriceDiscountGroup` | Requires `TenantId` |
| `CustomerRebateGroup` | Requires `TenantId` |
| `CustomerTMAGroup` | Requires `TenantId` |
| `Customers` | Requires `TenantId` |
| `DeliveryModeItems` | Requires `TenantId` |
| `DeliveryModesAddress` | Requires `TenantId` |
| `DeliveryModesHeader` | Requires `TenantId` |
| `DeliveryReasons` | Requires `TenantId` |
| `Destination` | Requires `TenantId` |
| `EmailCategories` | Requires `TenantId` |
| `EmailGroups` | Requires `TenantId` |
| `Expedite` | Requires `TenantId` |
| `ItemDiscountGroup` | Requires `TenantId` |
| `ItemFreightGroups` | Requires `TenantId` |
| `ItemListDetails` | Requires `TenantId` |
| `ItemListHeader` | Requires `TenantId` |
| `ItemRebateGroups` | Requires `TenantId` |
| `ItemSalesControl` | Requires `TenantId` |
| `Leads` | Requires `TenantId` |
| `LeadsTransAction` | Requires `TenantId` |
| `LeadsTransActionAddress` | Requires `TenantId` |
| `LeadsTransActionContactInformation` | Requires `TenantId` |
| `LineOFBusiness` | Requires `TenantId` |
| `MailingCategories` | Requires `TenantId` |
| `MailingItems` | Requires `TenantId` |
| `Opportunities` | Requires `TenantId` |
| `OpportunitiesCompetitors` | Requires `TenantId` |
| `OpportunitiesContact` | Requires `TenantId` |
| `OpportunitiesContactInformation` | Requires `TenantId` |
| `PackageAppearance` | Requires `TenantId` |
| `Phases` | Requires `TenantId` |
| `Probability` | Requires `TenantId` |
| `Prognosis` | Requires `TenantId` |
| `Prospect` | Requires `TenantId` |
| `ProspectRelation` | Requires `TenantId` |
| `QualifyingProcess` | Requires `TenantId` |
| `Questionnaire` | Requires `TenantId` |
| `QuotationsDocumentConclusions` | Requires `TenantId` |
| `QuotationsDocumentTitles` | Requires `TenantId` |
| `QuotationsDocumentintroDuctions` | Requires `TenantId` |
| `QuotationsTemplateGroups` | Requires `TenantId` |
| `QuotationsType` | Requires `TenantId` |
| `Rating` | Requires `TenantId` |
| `ReasonsCanceld` | Requires `TenantId` |
| `RebateProgramType` | Requires `TenantId` |
| `Responsibilties` | Requires `TenantId` |
| `ReturnSalesOrder` | Requires `TenantId` |
| `ReturnSalesOrderDetails` | Requires `TenantId` |
| `ReturnSalesReasonCode` | Requires `TenantId` |
| `SalesAgreementClassifications` | Requires `TenantId` |
| `SalesDistricts` | Requires `TenantId` |
| `SalesOrder` | Requires `TenantId` |
| `SalesOrderDetails` | Requires `TenantId` |
| `SalesOrderInvoice` | Requires `TenantId` |
| `SalesOrderPools` | Requires `TenantId` |
| `SalesOrigin` | Requires `TenantId` |
| `SalesQuotations` | Requires `TenantId` |
| `SalesUnit` | Requires `TenantId` |
| `Salutation` | Requires `TenantId` |
| `Segments` | Requires `TenantId` |
| `StageOptions` | Requires `TenantId` |
| `StatisticsGroup` | Requires `TenantId` |
| `Status` | Requires `TenantId` |
| `SupplementaryItemCustomer` | Requires `TenantId` |
| `SupplemetaryItemItemCroup` | Requires `TenantId` |
| `TeleMarketing` | Requires `TenantId` |
| `TeleMarketingReasonCanceled` | Requires `TenantId` |
| `TermsOFDelivery` | Requires `TenantId` |
| `complinentery` | Requires `TenantId` |
| `decision` | Requires `TenantId` |
| `functions` | Requires `TenantId` |
| `interest` | Requires `TenantId` |
| `loyalty` | Requires `TenantId` |

### Schema: `Nuitration`
Total tables: 15

| Table Name | Notes |
|---|---|
| `AssginDiteMeal` | Requires `TenantId` |
| `AssignItemMeals` | Requires `TenantId` |
| `DietRequest` | Requires `TenantId` |
| `DietTypes` | Requires `TenantId` |
| `ItemGroup` | Requires `TenantId` |
| `Items` | Requires `TenantId` |
| `MealRequest` | Requires `TenantId` |
| `MealTimes` | Requires `TenantId` |
| `MealTypes` | Requires `TenantId` |
| `TPNCentralLineFormulas` | Requires `TenantId` |
| `TPNOrderCentralLineFormulas` | Requires `TenantId` |
| `TPNOrderHeader` | Requires `TenantId` |
| `TPNOrderPeripheralLineFormulas` | Requires `TenantId` |
| `TPNPeripheralLineFormulas` | Requires `TenantId` |
| `TPNTemplate` | Requires `TenantId` |

### Schema: `Nursing`
Total tables: 60

| Table Name | Notes |
|---|---|
| `AssignNurseToPatients` | Requires `TenantId` |
| `Bed` | Requires `TenantId` |
| `CareCategory` | Requires `TenantId` |
| `Clothing` | Requires `TenantId` |
| `Designation` | Requires `TenantId` |
| `DischargNurseData` | Requires `TenantId` |
| `DischargNurseInstruction` | Requires `TenantId` |
| `DischargeChecklist` | Requires `TenantId` |
| `DoctorInstructions` | Requires `TenantId` |
| `DrugChart` | Requires `TenantId` |
| `Employee` | Requires `TenantId` |
| `EmployeeHaifTimeDays` | Requires `TenantId` |
| `EmployeeJobHistory` | Requires `TenantId` |
| `EmployeeSubDepartmentHistory` | Requires `TenantId` |
| `EmployeeTypeofContractHistory` | Requires `TenantId` |
| `FollowUpPatientRestraint` | Requires `TenantId` |
| `Grade` | Requires `TenantId` |
| `GradingType` | Requires `TenantId` |
| `HabitMaster` | Requires `TenantId` |
| `ICDCodes` | Requires `TenantId` |
| `ICDGroups` | Requires `TenantId` |
| `InitialNursingAssessment` | Requires `TenantId` |
| `InitialNursingAssessmentDet` | Requires `TenantId` |
| `InitiateDischarge` | Requires `TenantId` |
| `InstructionsMaster` | Requires `TenantId` |
| `InternalTransferHandover` | Requires `TenantId` |
| `InterventionsBasedonAssessedRisk` | Requires `TenantId` |
| `NurseActionNotes` | Requires `TenantId` |
| `NurseAdmissionAssessment` | Requires `TenantId` |
| `NurseCarePlan` | Requires `TenantId` |
| `NurseCategory` | Requires `TenantId` |
| `NurseDischargeAssessment` | Requires `TenantId` |
| `NurseDischargeTransfer` | Requires `TenantId` |
| `NurseInstruction` | Requires `TenantId` |
| `NurseMaster` | Requires `TenantId` |
| `NurseQulification` | Requires `TenantId` |
| `NurseSchedule` | Requires `TenantId` |
| `NurseSpecialization` | Requires `TenantId` |
| `NursingInitialAssessment` | Requires `TenantId` |
| `NursingNotes` | Requires `TenantId` |
| `NursingParameters` | Requires `TenantId` |
| `PatientEquipment` | Requires `TenantId` |
| `PatientPackages` | Requires `TenantId` |
| `PatientRestraint` | Requires `TenantId` |
| `PatientValuable` | Requires `TenantId` |
| `PatientValuableClothing` | Requires `TenantId` |
| `PatientValuablePersonalItem` | Requires `TenantId` |
| `PatientValuableProsthetic` | Requires `TenantId` |
| `Patient_HandOver` | Requires `TenantId` |
| `PediatricAssessment` | Requires `TenantId` |
| `PersonalItem` | Requires `TenantId` |
| `Prosthetic` | Requires `TenantId` |
| `ReceivedMealsDetails` | Requires `TenantId` |
| `ReceivedMealsMaster` | Requires `TenantId` |
| `Room` | Requires `TenantId` |
| `TPNOrderNurseStation` | Requires `TenantId` |
| `TreatmentCycle` | Requires `TenantId` |
| `ValuablesMaster` | Requires `TenantId` |
| `Ward` | Requires `TenantId` |
| `patientacuity` | Requires `TenantId` |

### Schema: `Operations`
Total tables: 48

| Table Name | Notes |
|---|---|
| `AnesthesiaMedication` | Requires `TenantId` |
| `AnesthesiaPatientConsent` | Requires `TenantId` |
| `AnesthesiaRecord` | Requires `TenantId` |
| `AnesthesiaType` | Requires `TenantId` |
| `AnesthesiaVitalSigns` | Requires `TenantId` |
| `BodyFluidBalanceChart` | Requires `TenantId` |
| `BodyFluidBalanceDetails` | Requires `TenantId` |
| `CancelOperationMaster` | Requires `TenantId` |
| `CathForm` | Requires `TenantId` |
| `ComplicationMaster` | Requires `TenantId` |
| `InstrumentCategoryMaster` | Requires `TenantId` |
| `InvestigationGroup` | Requires `TenantId` |
| `OperationClassification` | Requires `TenantId` |
| `OperationJobMaster` | Requires `TenantId` |
| `OperationLinkedItems` | Requires `TenantId` |
| `OperationPostureMaster` | Requires `TenantId` |
| `OperationPriorityMaster` | Requires `TenantId` |
| `OperationReservation` | Requires `TenantId` |
| `OperationRoom` | Requires `TenantId` |
| `OperationRoomScedule` | Requires `TenantId` |
| `OperationSetupMaster` | Requires `TenantId` |
| `OperationSignOut` | Requires `TenantId` |
| `OperationTypeMaster` | Requires `TenantId` |
| `OperationWardMaster` | Requires `TenantId` |
| `OperationWardRooms` | Requires `TenantId` |
| `OperationWardSpecialities` | Requires `TenantId` |
| `OperationWardWorkDay` | Requires `TenantId` |
| `OperationsManage` | Requires `TenantId` |
| `OperationsSetting` | Requires `TenantId` |
| `Operations_Ward` | Requires `TenantId` |
| `Operative` | Requires `TenantId` |
| `PatientBloodTransfusionConsent` | Requires `TenantId` |
| `PatientRiskConsent` | Requires `TenantId` |
| `PositionMaster` | Requires `TenantId` |
| `PostAnesthesiaCare` | Requires `TenantId` |
| `PostAnesthesiaVitalSigns` | Requires `TenantId` |
| `PreOperative` | Requires `TenantId` |
| `PreOperativeHandOver` | Requires `TenantId` |
| `PreOperative_PreProceduralRecord` | Requires `TenantId` |
| `PreProceduralRecord` | Requires `TenantId` |
| `PreprationGroup` | Requires `TenantId` |
| `Prroperativeverification` | Requires `TenantId` |
| `SurgicalInstrumentsMaster` | Requires `TenantId` |
| `SurgicalProceduralConsent` | Requires `TenantId` |
| `SurgicalSpecimens` | Requires `TenantId` |
| `SurgicalSuppliesMaster` | Requires `TenantId` |
| `TimeOut` | Requires `TenantId` |
| `VerificationProcess` | Requires `TenantId` |

### Schema: `OutPatient`
Total tables: 66

| Table Name | Notes |
|---|---|
| `AdmittingPatient` | Requires `TenantId` |
| `AllergyDetails` | Requires `TenantId` |
| `AllergyMaster` | Requires `TenantId` |
| `ClinicLocation` | Requires `TenantId` |
| `ClinicProcedures` | Requires `TenantId` |
| `ClinicSchedule` | Requires `TenantId` |
| `ClinicSetup` | Requires `TenantId` |
| `ClinicType` | Requires `TenantId` |
| `ClinicalServices` | Requires `TenantId` |
| `ClinicalSnomed` | Requires `TenantId` |
| `ConsultationSetting` | Requires `TenantId` |
| `DailyCloseDate` | Requires `TenantId` |
| `DiagnosisAnswers` | Requires `TenantId` |
| `DiagnosisGroups` | Requires `TenantId` |
| `DiagnosisMain` | Requires `TenantId` |
| `DiagnosisQuestion` | Requires `TenantId` |
| `DiagnosisSub` | Requires `TenantId` |
| `DiseaseCategory` | Requires `TenantId` |
| `DiseaseMaster` | Requires `TenantId` |
| `Disease_InfectionTypes` | Requires `TenantId` |
| `DoctorGeneralSchedule` | Requires `TenantId` |
| `DoctorRemarksAndComments` | Requires `TenantId` |
| `DoctorsPersonalList` | Requires `TenantId` |
| `DrugsWithInfusionRateDetails` | Requires `TenantId` |
| `DrugsWithInfusionRateDetailsDetails` | Requires `TenantId` |
| `DrugsWithInfusionRateDetails_StatusHistory` | Requires `TenantId` |
| `DrugsWithInfusionRateHeader` | Requires `TenantId` |
| `GeneralSetup` | Requires `TenantId` |
| `ICDCode` | Requires `TenantId` |
| `InfusionRate_Order` | Requires `TenantId` |
| `InvestigationRequestDetails` | Requires `TenantId` |
| `InvestigationRequestHeader` | Requires `TenantId` |
| `ManualGroupTransfer` | Requires `TenantId` |
| `MedicalConsumablesDetails` | Requires `TenantId` |
| `MedicalConsumablesHeader` | Requires `TenantId` |
| `NationalVacation` | Requires `TenantId` |
| `OPDInternalTransfer` | Requires `TenantId` |
| `PackagesRequestDetails` | Requires `TenantId` |
| `PatientAllergy` | Requires `TenantId` |
| `PatientAllergyUpdates` | Requires `TenantId` |
| `PatientVitals` | Requires `TenantId` |
| `PayPolicy` | Requires `TenantId` |
| `PrepareVisitSlip` | Requires `TenantId` |
| `PrescriptionsDetails` | Requires `TenantId` |
| `PrescriptionsHeader` | Requires `TenantId` |
| `ProceduresDetails` | Requires `TenantId` |
| `ProceduresMaster` | Requires `TenantId` |
| `ReFillPrescriptions` | Requires `TenantId` |
| `ScheduleAppointment` | Requires `TenantId` |
| `ServicesRequestDetails` | Requires `TenantId` |
| `SpecialityGroupDetails` | Requires `TenantId` |
| `SpecialityGroupMaster` | Requires `TenantId` |
| `SponsorshipCondition` | Requires `TenantId` |
| `SponsorshipConditionCostCenters` | Requires `TenantId` |
| `SponsorshipConditionDrugTypes` | Requires `TenantId` |
| `SponsorshipConditionDrugs` | Requires `TenantId` |
| `SponsorshipConditionServices` | Requires `TenantId` |
| `SponsorshipConditionSupplierGroups` | Requires `TenantId` |
| `SponsorshipConditionSupplierGroupsItems` | Requires `TenantId` |
| `Vac-Clinic` | Requires `TenantId` |
| `Vac_Clinic` | Requires `TenantId` |
| `VaccinationChart` | Requires `TenantId` |
| `VaccinationDetails` | Requires `TenantId` |
| `VaccinationMaster` | Requires `TenantId` |
| `VaccinationSchedule` | Requires `TenantId` |
| `VitalParametersDeptWise` | Requires `TenantId` |

### Schema: `PFEducation`
Total tables: 16

| Table Name | Notes |
|---|---|
| `EducationalFiles` | Requires `TenantId` |
| `InstructionComments` | Requires `TenantId` |
| `InstructionList` | Requires `TenantId` |
| `Need_NeedExecution` | Requires `TenantId` |
| `Needs` | Requires `TenantId` |
| `NeedsExecution` | Requires `TenantId` |
| `PFEBarriers` | Requires `TenantId` |
| `PFEBarriersReduce` | Requires `TenantId` |
| `PFEDiagnosis` | Requires `TenantId` |
| `PFEGroupExecution` | Requires `TenantId` |
| `PFENeeds` | Requires `TenantId` |
| `PFENeedsExecution` | Requires `TenantId` |
| `PatientDocuments` | Requires `TenantId` |
| `PatientEduFiles` | Requires `TenantId` |
| `PatientFamilyEducation` | Requires `TenantId` |
| `PatientInstructionList` | Requires `TenantId` |

### Schema: `PatientPortal`
Total tables: 1

| Table Name | Notes |
|---|---|
| `PatientComplaint` | Requires `TenantId` |

### Schema: `Pharmacy`
Total tables: 117

| Table Name | Notes |
|---|---|
| `Additives` | Requires `TenantId` |
| `AdministrationSite` | Requires `TenantId` |
| `AdminstrationMaster` | Requires `TenantId` |
| `Batches` | Requires `TenantId` |
| `Borrowing` | Requires `TenantId` |
| `Brands` | Requires `TenantId` |
| `ContraDrugs` | Requires `TenantId` |
| `DeliveryTerm` | Requires `TenantId` |
| `DespatchingDetails` | Requires `TenantId` |
| `DestroyingExpiryItemsDetails` | Requires `TenantId` |
| `DestroyingExpiryItemsHeader` | Requires `TenantId` |
| `DirectAddationDetails` | Requires `TenantId` |
| `DirectAddationHeader` | Requires `TenantId` |
| `DirectSubstractDetails` | Requires `TenantId` |
| `DirectSubstractHeader` | Requires `TenantId` |
| `DispenseDrugsDetails` | Requires `TenantId` |
| `DispenseDrugsHeader` | Requires `TenantId` |
| `DosageFrequencyLink` | Requires `TenantId` |
| `DosageSession` | Requires `TenantId` |
| `DosageUniteForm` | Requires `TenantId` |
| `DrugAdminMode` | Requires `TenantId` |
| `DrugAlternative` | Requires `TenantId` |
| `DrugClass` | Requires `TenantId` |
| `DrugClassDet` | Requires `TenantId` |
| `DrugClassification` | Requires `TenantId` |
| `DrugClassificationPeroid` | Requires `TenantId` |
| `DrugForms` | Requires `TenantId` |
| `DrugGroupMaster` | Requires `TenantId` |
| `DrugPreparationTemplate` | Requires `TenantId` |
| `DrugTemplate` | Requires `TenantId` |
| `DrugTypes` | Requires `TenantId` |
| `Drug_manufacturers` | Requires `TenantId` |
| `Drugs` | Requires `TenantId` |
| `ExpenseMaster` | Requires `TenantId` |
| `Frequencies` | Requires `TenantId` |
| `GRNDetailsBatchesTransactions` | Requires `TenantId` |
| `GRN_LPOs` | Requires `TenantId` |
| `GenericNames` | Requires `TenantId` |
| `GenericNamesReplacement` | Requires `TenantId` |
| `GoodsReceivedNoteDetails` | Requires `TenantId` |
| `GoodsReceivedNoteExpenses` | Requires `TenantId` |
| `GoodsReceivedNoteHeader` | Requires `TenantId` |
| `InternalConsumption` | Requires `TenantId` |
| `InternalConsumptionEntry` | Requires `TenantId` |
| `IssueRequest` | Requires `TenantId` |
| `IssueRequestdetails` | Requires `TenantId` |
| `IssuetoDepartment` | Requires `TenantId` |
| `IssuetoDepartmentEntry` | Requires `TenantId` |
| `IssuetoDepartmentReturn` | Requires `TenantId` |
| `IssuetoDepartmentReturnEntry` | Requires `TenantId` |
| `LPOApprovingAuthority` | Requires `TenantId` |
| `LPOApprovingAuthorityHeader` | Requires `TenantId` |
| `LegalStatusMaster` | Requires `TenantId` |
| `LocalPurchaseCancelation` | Requires `TenantId` |
| `LocalPurchaseOrderDetails` | Requires `TenantId` |
| `LocalPurchaseOrderExpenses` | Requires `TenantId` |
| `LocalPurchaseOrderHeader` | Requires `TenantId` |
| `MainSolution` | Requires `TenantId` |
| `MedicationDispensingPeriod` | Requires `TenantId` |
| `MedicationDispensingPeriod_GenericNames` | Requires `TenantId` |
| `OpeningPharmacyDetails` | Requires `TenantId` |
| `OpeningPharmacyHeader` | Requires `TenantId` |
| `OpeningStockDetails` | Requires `TenantId` |
| `OpeningStockHeader` | Requires `TenantId` |
| `OtherHospitals` | Requires `TenantId` |
| `PackageUnitMaster` | Requires `TenantId` |
| `PaymentGroup` | Requires `TenantId` |
| `PaymentSupplier` | Requires `TenantId` |
| `PaymentTermsMaster` | Requires `TenantId` |
| `PaymentTermsSchedule` | Requires `TenantId` |
| `PharmInstallation` | Requires `TenantId` |
| `PharmInstallationDoctorDegree` | Requires `TenantId` |
| `PharmacyPayment` | Requires `TenantId` |
| `PharmacySettings` | Requires `TenantId` |
| `PharmacySupplierInvoicePayment` | Requires `TenantId` |
| `PhysicalStockAdjustment` | Requires `TenantId` |
| `PhysicalStockAdjustmentEntry` | Requires `TenantId` |
| `PrescriptionAbbreviation` | Requires `TenantId` |
| `ProhibitedDrugDocs` | Requires `TenantId` |
| `PurchaseReturnDetails` | Requires `TenantId` |
| `PurchaseReturnHeader` | Requires `TenantId` |
| `ReOrderHistoryDetails` | Requires `TenantId` |
| `ReOrderHistoryMaster` | Requires `TenantId` |
| `RefundDrugsDetails` | Requires `TenantId` |
| `RefundDrugsHeader` | Requires `TenantId` |
| `RequestSuppliesDetails` | Requires `TenantId` |
| `RequestSuppliesHeader` | Requires `TenantId` |
| `RequestSupplyReturnDetails` | Requires `TenantId` |
| `RequestSupplyReturnHeader` | Requires `TenantId` |
| `ReturningExpiryItemsDetails` | Requires `TenantId` |
| `ReturningExpiryItemsHeader` | Requires `TenantId` |
| `Route` | Requires `TenantId` |
| `ScrapDetail` | Requires `TenantId` |
| `ScrapHeader` | Requires `TenantId` |
| `ScrapReturnDetail` | Requires `TenantId` |
| `ScrapReturnHeader` | Requires `TenantId` |
| `StdDosage` | Requires `TenantId` |
| `StockAdjustmentReasons` | Requires `TenantId` |
| `StockControl` | Requires `TenantId` |
| `StockControlDetail` | Requires `TenantId` |
| `StockTransfer` | Requires `TenantId` |
| `StockTransferEntry` | Requires `TenantId` |
| `StrenghtUnitMaster` | Requires `TenantId` |
| `SubStoreBatches` | Requires `TenantId` |
| `SubStoresClassifications` | Requires `TenantId` |
| `SubstoreAuthority` | Requires `TenantId` |
| `Substore_Items` | Requires `TenantId` |
| `Substores` | Requires `TenantId` |
| `SupplierContacts` | Requires `TenantId` |
| `SupplierRelatedCompanies` | Requires `TenantId` |
| `SupplierType` | Requires `TenantId` |
| `SupplierforDrug` | Requires `TenantId` |
| `Suppliers` | Requires `TenantId` |
| `Template` | Requires `TenantId` |
| `UnitConversionFactor` | Requires `TenantId` |
| `UnitTemplate` | Requires `TenantId` |
| `Units` | Requires `TenantId` |

### Schema: `Radiology`
Total tables: 23

| Table Name | Notes |
|---|---|
| `DeviceServices` | Requires `TenantId` |
| `DeviceTechnician` | Requires `TenantId` |
| `DevicesDefination` | Requires `TenantId` |
| `DevicsSchedule` | Requires `TenantId` |
| `DirectRad_Diagnosis` | Requires `TenantId` |
| `ExamDelivery` | Requires `TenantId` |
| `ExamRequest` | Requires `TenantId` |
| `Exams` | Requires `TenantId` |
| `FlagSettings` | Requires `TenantId` |
| `LocationDefination` | Requires `TenantId` |
| `MostCommenInvestigations` | Requires `TenantId` |
| `PatientRadReception` | Requires `TenantId` |
| `RadReceptionProcedures` | Requires `TenantId` |
| `RadReceptioniestSchedule` | Requires `TenantId` |
| `RadResultImages` | Requires `TenantId` |
| `RadiologyReceptions` | Requires `TenantId` |
| `RayBodyLoaction` | Requires `TenantId` |
| `ReceptionDevicsSchedule` | Requires `TenantId` |
| `ReceptionStocks` | Requires `TenantId` |
| `ResultEntryDetails` | Requires `TenantId` |
| `ResultEntryDetails_Findings` | Requires `TenantId` |
| `ResultEntryHeader` | Requires `TenantId` |
| `Tests` | Requires `TenantId` |

### Schema: `Registration`
Total tables: 60

| Table Name | Notes |
|---|---|
| `AdmissionRequest` | Requires `TenantId` |
| `Age` | Requires `TenantId` |
| `Areas` | Requires `TenantId` |
| `CallDoctor` | Requires `TenantId` |
| `ConfidentialityCode` | Requires `TenantId` |
| `ConsultationCharges` | Requires `TenantId` |
| `DeficiencySetup` | Requires `TenantId` |
| `DoctorDegree` | Requires `TenantId` |
| `DoctorDutyRosters` | Requires `TenantId` |
| `DoctorDutyRostersDetails` | Requires `TenantId` |
| `DoctorType` | Requires `TenantId` |
| `Doctors` | Requires `TenantId` |
| `DoctorsDailySchedule` | Requires `TenantId` |
| `DoctorsDailyScheduleDetails` | Requires `TenantId` |
| `Equipments` | Requires `TenantId` |
| `FavouriteDiagnosis` | Requires `TenantId` |
| `FavouriteDisease` | Requires `TenantId` |
| `FavouriteDrugs` | Requires `TenantId` |
| `GeneralConsent` | Requires `TenantId` |
| `GenerateDeficiencySetup` | Requires `TenantId` |
| `GenericClinics` | Requires `TenantId` |
| `LockDoctorDutyRoster` | Requires `TenantId` |
| `MarketingMedia` | Requires `TenantId` |
| `MedicalAlerts` | Requires `TenantId` |
| `MedicalRecordDepartments` | Requires `TenantId` |
| `MedicalServices` | Requires `TenantId` |
| `MergePatient` | Requires `TenantId` |
| `MergePatientDetail` | Requires `TenantId` |
| `Military` | Requires `TenantId` |
| `MinistryAlerts` | Requires `TenantId` |
| `Nationalities` | Requires `TenantId` |
| `NationalityGroups` | Requires `TenantId` |
| `PatientAllergy` | Requires `TenantId` |
| `PatientComment` | Requires `TenantId` |
| `PatientFamilyMembers` | Requires `TenantId` |
| `PatientInsurance` | Requires `TenantId` |
| `PatientMedicalAlerts` | Requires `TenantId` |
| `PatientPaymentTypeHistory` | Requires `TenantId` |
| `PatientSurvey` | Requires `TenantId` |
| `PatientSurveyDetail` | Requires `TenantId` |
| `Patients` | Requires `TenantId` |
| `PostCashCompanies` | Requires `TenantId` |
| `Registration.Companies` | Requires `TenantId` |
| `RegistrationFields` | Requires `TenantId` |
| `RegistrationParameters` | Requires `TenantId` |
| `Relation` | Requires `TenantId` |
| `Religions` | Requires `TenantId` |
| `RequestMedicalRecordTransfer` | Requires `TenantId` |
| `ScheduleStatus` | Requires `TenantId` |
| `Sessions` | Requires `TenantId` |
| `Sex` | Requires `TenantId` |
| `Specialty` | Requires `TenantId` |
| `Sponsor` | Requires `TenantId` |
| `SponsorItem` | Requires `TenantId` |
| `SurveyAnswers` | Requires `TenantId` |
| `SurveyQuestions` | Requires `TenantId` |
| `Technicians` | Requires `TenantId` |
| `Titles` | Requires `TenantId` |
| `VaccineType` | Requires `TenantId` |
| `XDoctorDutyRostersDetails` | Requires `TenantId` |

### Schema: `dbo`
Total tables: 48

| Table Name | Notes |
|---|---|
| `AccTemp` | Requires `TenantId` |
| `AspNetRoleClaims` | Requires `TenantId` |
| `AspNetRoles` | Requires `TenantId` |
| `AspNetUserClaims` | Requires `TenantId` |
| `AspNetUserLogins` | Requires `TenantId` |
| `AspNetUserRoles` | Requires `TenantId` |
| `AspNetUserTokens` | Requires `TenantId` |
| `AspNetUsers` | Requires `TenantId` |
| `BMoheb_Test` | Requires `TenantId` |
| `BedStatus` | Requires `TenantId` |
| `BedType` | Requires `TenantId` |
| `CPT4` | Requires `TenantId` |
| `DevTest.htest1` | Requires `TenantId` |
| `DischargeChecklist` | Requires `TenantId` |
| `Employee` | Requires `TenantId` |
| `EmployeeGomaa` | Requires `TenantId` |
| `ICD10` | Requires `TenantId` |
| `ICD10_New` | Requires `TenantId` |
| `Items` | Requires `TenantId` |
| `ItemsDetails` | Requires `TenantId` |
| `NewP` | Requires `TenantId` |
| `OperationRoom` | Requires `TenantId` |
| `Operations` | Requires `TenantId` |
| `Pharmacy.Borrowing` | Requires `TenantId` |
| `Query` | Requires `TenantId` |
| `TMP_Search` | Requires `TenantId` |
| `Tbl_Details` | Requires `TenantId` |
| `Tbl_Master` | Requires `TenantId` |
| `Test` | Requires `TenantId` |
| `Test002` | Requires `TenantId` |
| `Test1` | Requires `TenantId` |
| `TestTable1` | Requires `TenantId` |
| `Users$` | Requires `TenantId` |
| `Users$_FilterDatabase` | Requires `TenantId` |
| `UsersTask` | Requires `TenantId` |
| `ZatcaCSR` | Requires `TenantId` |
| `__EFMigrationsHistory` | Requires `TenantId` |
| `__MigrationHistory` | Requires `TenantId` |
| `__MigrationLog` | Requires `TenantId` |
| `__SchemaSnapshot` | Requires `TenantId` |
| `data_patientradiologystudy` | Requires `TenantId` |
| `data_radioimages` | Requires `TenantId` |
| `htest1` | Requires `TenantId` |
| `icd102019_chapters$` | Requires `TenantId` |
| `icd102019_codes$` | Requires `TenantId` |
| `icd102019_groups$` | Requires `TenantId` |
| `number` | Requires `TenantId` |
| `tb_data_icd` | Requires `TenantId` |

