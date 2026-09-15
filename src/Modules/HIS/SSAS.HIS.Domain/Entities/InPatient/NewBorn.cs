using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class NewBorn : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public string MRN { get; set; }
        public string BabyNameEn { get; set; }
        public string BabyNameAr { get; set; }
        public int MotherID { get; set; }
        public int FatherID { get; set; }
        public string FatherFullNameArabic { get; set; }
        public string FatherFullNameEnglish { get; set; }
        public int FatherReligionID { get; set; }
        public string FatherNationalNumber { get; set; }
        public int FatherIDTypeID { get; set; }
        public int FatherNationalityID { get; set; }
        public int FatherJobID { get; set; }
        public string FatherAddress { get; set; }
        public bool IssueDate { get; set; }
        public bool IssueAddress { get; set; }
        public DateTime TimeOfBirth { get; set; }
        public int BabyGenderID { get; set; }
        public int BloodGroupID { get; set; }
        public int BloodGroupTypeID { get; set; }
        public int AttendingDoctorID { get; set; }
        public int BabyDoctorID { get; set; }
        public string BabyHeight { get; set; }
        public int BabyHeightUnitID { get; set; }
        public string BabyWeight { get; set; }
        public int BabyWeightUnitID { get; set; }
        public string BabyAlive { get; set; }
        public string Twins { get; set; }
        public string NumberOfTwins { get; set; }
        public DateTime BabyBirthDate { get; set; }
        public string SquenceNumber { get; set; }
        public int BabyAccommTypeID { get; set; }
        public int BabyWardID { get; set; }
        public int BabyRoomID { get; set; }
        public int BabyBedID { get; set; }
        public int CompanyID { get; set; }
        public string FatherJob { get; set; }
        public int PatID { get; set; }
        public Guid TenantId { get; set; }
    }
}
