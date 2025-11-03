using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v36 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppAcademicYears_AcademicYearId",
                table: "AppStudents");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppDivisions_DivisionId",
                table: "AppStudents");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppEducationBoards_EducationBoardId",
                table: "AppStudents");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppEducationMediumStages_EducationMediumStageId",
                table: "AppStudents");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppEducationMediums_EducationMediumId",
                table: "AppStudents");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppStandards_StandardId",
                table: "AppStudents");

            migrationBuilder.DropTable(
                name: "AppAttendanceTypes");

            migrationBuilder.DropTable(
                name: "AppDashboardLayouts");

            migrationBuilder.DropTable(
                name: "AppEntranceExams");

            migrationBuilder.DropTable(
                name: "AppFollowUpMessages");

            migrationBuilder.DropTable(
                name: "AppStudentAttendanceRules");

            migrationBuilder.DropTable(
                name: "AppStudentAttendances");

            migrationBuilder.DropTable(
                name: "AppStudentCertificates");

            migrationBuilder.DropTable(
                name: "AppStudentDocuments");

            migrationBuilder.DropTable(
                name: "AppStudentGroupMembers");

            migrationBuilder.DropTable(
                name: "AppStudentLeaves");

            migrationBuilder.DropTable(
                name: "AppStudentEnrollments");

            migrationBuilder.DropTable(
                name: "AppStudentGroups");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_AcademicYearId",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_DivisionId",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_EducationBoardId",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_EducationMediumId",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_EducationMediumStageId",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_StandardId",
                table: "AppStudents");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppEntranceExamSlots",
                table: "AppEntranceExamSlots");

            migrationBuilder.DropColumn(
                name: "AadhaarNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "AddressLine1",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "AddressLine3",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "AlternativeContactNumber",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "BirthPlace",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "CastType",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "City",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "CountryIsoCode",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "CountryName",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "EducationBoardId",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "EducationMediumStageId",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FamilyCast",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_AadhaarNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_Address1",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_Address2",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_Address3",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_ContactNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_DateOfBirth",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_Designation",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_Email",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_Name",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_Occupation",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_OfficeAddress1",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_OfficeAddress2",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_OfficeAddress3",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_OfficeNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_Organisation",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_PassOutYear",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_Qualification",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_Remarks",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FormNumber",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GenderType",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GrNoRecord",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_AadhaarNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_Address1",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_Address2",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_Address3",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_ContactNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_DateOfBirth",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_Designation",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_Email",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_Name",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_Occupation",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_OfficeAddress1",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_OfficeAddress2",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_OfficeAddress3",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_OfficeNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_Organisation",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_PassOutYear",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_Qualification",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_Remarks",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_AadhaarNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_Address1",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_Address2",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_Address3",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_ContactNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_DateOfBirth",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_Designation",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_Email",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_Name",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_Occupation",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_OfficeAddress1",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_OfficeAddress2",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_OfficeAddress3",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_OfficeNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_Organisation",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_PassOutYear",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_Qualification",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_Remarks",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherTongue",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "Nationality",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "ParentBankDetails_AccountHolderName",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "ParentBankDetails_AccountNumber",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "ParentBankDetails_BankName",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "ParentBankDetails_BranchName",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "ParentBankDetails_IfscCode",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "ParentContactNumber",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "PinCode",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "PreviousSchoolDetails_LastClassPassed",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "PreviousSchoolDetails_Percentage",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "PreviousSchoolDetails_PreviousSchoolAddress",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "PreviousSchoolDetails_PreviousSchoolBoard",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "PreviousSchoolDetails_PreviousSchoolMedium",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "PreviousSchoolDetails_PreviousSchoolName",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "PreviousSchoolDetails_TcNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "ProfilePicture",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "Religion",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StateIsoCode",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StateName",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentAdditionalDetails_BPLCardNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentAdditionalDetails_BPLStudent",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentAdditionalDetails_Category",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentAdditionalDetails_EmergencyContactNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentAdditionalDetails_PersonWithDisability",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentAdditionalDetails_RightToEducation",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentAdditionalDetails_SecondLanguage",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentAdditionalDetails_SingleChild",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentAdditionalDetails_SingleParentChild",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentAdditionalDetails_SponsorName",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_EmergencyAddressLine1",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_EmergencyAddressLine2",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_EmergencyAddressLine3",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_EmergencyCity",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_EmergencyCountry",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_EmergencyPinCode",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_EmergencyState",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_LocalAddressLine1",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_LocalAddressLine2",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_LocalAddressLine3",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_LocalCity",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_LocalCountryIsoCode",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_LocalCountryName",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_LocalPinCode",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_LocalStateIsoCode",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_LocalStateName",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_ParentContactNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_ParentEmail",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_PermanentAddressLine1",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_PermanentAddressLine2",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_PermanentAddressLine3",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_PermanentCity",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_PermanentCountryIsoCode",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_PermanentCountryName",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_PermanentPinCode",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_PermanentStateIsoCode",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_PermanentStateName",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_PrimaryContactNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_Remarks",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_StudentContactNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_StudentEmail",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_WhatsAppNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentMedicalRecords_Allergies",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentMedicalRecords_BodyMassIndex",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentMedicalRecords_ChildImmunisation",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentMedicalRecords_CovidVaccination",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentMedicalRecords_Haemoglobin",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentMedicalRecords_Height",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentMedicalRecords_ImmunisationRemarks",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentMedicalRecords_PulseRate",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentMedicalRecords_Weight",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "AppStudents");

            migrationBuilder.RenameTable(
                name: "AppEntranceExamSlots",
                newName: "EntranceExamSlots");

            migrationBuilder.RenameColumn(
                name: "TempGrNo",
                table: "AppStudents",
                newName: "GrNo");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EntranceExamSlots",
                table: "EntranceExamSlots",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_EntranceExamSlots",
                table: "EntranceExamSlots");

            migrationBuilder.RenameTable(
                name: "EntranceExamSlots",
                newName: "AppEntranceExamSlots");

            migrationBuilder.RenameColumn(
                name: "GrNo",
                table: "AppStudents",
                newName: "TempGrNo");

            migrationBuilder.AddColumn<string>(
                name: "AadhaarNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "AppStudents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine3",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AlternativeContactNumber",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BirthPlace",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CastType",
                table: "AppStudents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryIsoCode",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryName",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DateOfBirth",
                table: "AppStudents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EducationBoardId",
                table: "AppStudents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EducationMediumStageId",
                table: "AppStudents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FamilyCast",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_AadhaarNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_Address1",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_Address2",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_Address3",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_ContactNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FatherDetails_DateOfBirth",
                table: "AppStudents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_Designation",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_Email",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_Name",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_Occupation",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_OfficeAddress1",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_OfficeAddress2",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_OfficeAddress3",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_OfficeNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_Organisation",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FatherDetails_PassOutYear",
                table: "AppStudents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_Qualification",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FatherDetails_Remarks",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormNumber",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GenderType",
                table: "AppStudents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GrNoRecord",
                table: "AppStudents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_AadhaarNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_Address1",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_Address2",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_Address3",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_ContactNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GuardianDetails_DateOfBirth",
                table: "AppStudents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_Designation",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_Email",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_Name",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_Occupation",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_OfficeAddress1",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_OfficeAddress2",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_OfficeAddress3",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_OfficeNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_Organisation",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GuardianDetails_PassOutYear",
                table: "AppStudents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_Qualification",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_Remarks",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_AadhaarNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_Address1",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_Address2",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_Address3",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_ContactNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MotherDetails_DateOfBirth",
                table: "AppStudents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_Designation",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_Email",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_Name",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_Occupation",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_OfficeAddress1",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_OfficeAddress2",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_OfficeAddress3",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_OfficeNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_Organisation",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MotherDetails_PassOutYear",
                table: "AppStudents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_Qualification",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_Remarks",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherTongue",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nationality",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentBankDetails_AccountHolderName",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentBankDetails_AccountNumber",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentBankDetails_BankName",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentBankDetails_BranchName",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentBankDetails_IfscCode",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentContactNumber",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PinCode",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousSchoolDetails_LastClassPassed",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "PreviousSchoolDetails_Percentage",
                table: "AppStudents",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousSchoolDetails_PreviousSchoolAddress",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousSchoolDetails_PreviousSchoolBoard",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousSchoolDetails_PreviousSchoolMedium",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousSchoolDetails_PreviousSchoolName",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousSchoolDetails_TcNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ProfilePicture",
                table: "AppStudents",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Religion",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateIsoCode",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateName",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentAdditionalDetails_BPLCardNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StudentAdditionalDetails_BPLStudent",
                table: "AppStudents",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudentAdditionalDetails_Category",
                table: "AppStudents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentAdditionalDetails_EmergencyContactNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StudentAdditionalDetails_PersonWithDisability",
                table: "AppStudents",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StudentAdditionalDetails_RightToEducation",
                table: "AppStudents",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentAdditionalDetails_SecondLanguage",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StudentAdditionalDetails_SingleChild",
                table: "AppStudents",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StudentAdditionalDetails_SingleParentChild",
                table: "AppStudents",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentAdditionalDetails_SponsorName",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_EmergencyAddressLine1",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_EmergencyAddressLine2",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_EmergencyAddressLine3",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_EmergencyCity",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_EmergencyCountry",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_EmergencyPinCode",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_EmergencyState",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_LocalAddressLine1",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_LocalAddressLine2",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_LocalAddressLine3",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_LocalCity",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_LocalCountryIsoCode",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_LocalCountryName",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_LocalPinCode",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_LocalStateIsoCode",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_LocalStateName",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_ParentContactNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_ParentEmail",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_PermanentAddressLine1",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_PermanentAddressLine2",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_PermanentAddressLine3",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_PermanentCity",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_PermanentCountryIsoCode",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_PermanentCountryName",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_PermanentPinCode",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_PermanentStateIsoCode",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_PermanentStateName",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_PrimaryContactNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_Remarks",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_StudentContactNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_StudentEmail",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_WhatsAppNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentMedicalRecords_Allergies",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "StudentMedicalRecords_BodyMassIndex",
                table: "AppStudents",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentMedicalRecords_ChildImmunisation",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudentMedicalRecords_CovidVaccination",
                table: "AppStudents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "StudentMedicalRecords_Haemoglobin",
                table: "AppStudents",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "StudentMedicalRecords_Height",
                table: "AppStudents",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentMedicalRecords_ImmunisationRemarks",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "StudentMedicalRecords_PulseRate",
                table: "AppStudents",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "StudentMedicalRecords_Weight",
                table: "AppStudents",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "AppStudents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppEntranceExamSlots",
                table: "AppEntranceExamSlots",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "AppAttendanceTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAttendanceTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppDashboardLayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Layouts = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisibleWidgets = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDashboardLayouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentAttendanceRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EnableNotificationForUpperAuthority = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    HalfDayThreshold = table.Column<int>(type: "integer", nullable: false),
                    HalfDayThresholdUnit = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    LateThreshold = table.Column<int>(type: "integer", nullable: false),
                    LateThresholdUnit = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentAttendanceRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentAttendances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsPresent = table.Column<bool>(type: "boolean", nullable: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentAttendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentAttendances_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    PdfContent = table.Column<byte[]>(type: "bytea", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentCertificates_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentEnrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationBoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    InquiryPreferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    AadhaarNo = table.Column<string>(type: "text", nullable: true),
                    AddressLine1 = table.Column<string>(type: "text", nullable: true),
                    AddressLine2 = table.Column<string>(type: "text", nullable: true),
                    AddressLine3 = table.Column<string>(type: "text", nullable: true),
                    ApparId = table.Column<string>(type: "text", nullable: true),
                    BirthPlace = table.Column<string>(type: "text", nullable: true),
                    CastType = table.Column<int>(type: "integer", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DateOfBirth = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DateOfInquiry = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    FamilyCast = table.Column<string>(type: "text", nullable: true),
                    FeeStatusType = table.Column<int>(type: "integer", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    FollowUpStatusType = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    FormNumber = table.Column<string>(type: "text", nullable: true),
                    GenderType = table.Column<int>(type: "integer", nullable: true),
                    InquiryNumber = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastName = table.Column<string>(type: "text", nullable: true),
                    MiddleName = table.Column<string>(type: "text", nullable: true),
                    MotherTongue = table.Column<string>(type: "text", nullable: true),
                    Nationality = table.Column<string>(type: "text", nullable: true),
                    OnboardingProgressState = table.Column<int>(type: "integer", nullable: false),
                    PAN = table.Column<string>(type: "text", nullable: true),
                    PaidAmount = table.Column<float>(type: "real", nullable: false),
                    PinCode = table.Column<string>(type: "text", nullable: true),
                    PreviousSchoolName = table.Column<string>(type: "text", nullable: true),
                    PrimaryContactNumber = table.Column<string>(type: "text", nullable: true),
                    ProfilePicture = table.Column<byte[]>(type: "bytea", nullable: true),
                    Religion = table.Column<string>(type: "text", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    SecondaryContactNumber = table.Column<string>(type: "text", nullable: true),
                    StudentEnrollmentApprovalState = table.Column<int>(type: "integer", nullable: false),
                    StudentEnrollmentInquiryState = table.Column<int>(type: "integer", nullable: false),
                    StudentEnrollmentState = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TotalAmount = table.Column<float>(type: "real", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FatherDetails_AadhaarNo = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_Address1 = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_Address2 = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_Address3 = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_ContactNo = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_DateOfBirth = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FatherDetails_Designation = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_Email = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_Name = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_Occupation = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_OfficeAddress1 = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_OfficeAddress2 = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_OfficeAddress3 = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_OfficeNo = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_Organisation = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_PassOutYear = table.Column<int>(type: "integer", nullable: true),
                    FatherDetails_Qualification = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_Remarks = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_AadhaarNo = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_Address1 = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_Address2 = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_Address3 = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_ContactNo = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_DateOfBirth = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GuardianDetails_Designation = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_Email = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_Name = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_Occupation = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_OfficeAddress1 = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_OfficeAddress2 = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_OfficeAddress3 = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_OfficeNo = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_Organisation = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_PassOutYear = table.Column<int>(type: "integer", nullable: true),
                    GuardianDetails_Qualification = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_Remarks = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_AadhaarNo = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_Address1 = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_Address2 = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_Address3 = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_ContactNo = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_DateOfBirth = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MotherDetails_Designation = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_Email = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_Name = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_Occupation = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_OfficeAddress1 = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_OfficeAddress2 = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_OfficeAddress3 = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_OfficeNo = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_Organisation = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_PassOutYear = table.Column<int>(type: "integer", nullable: true),
                    MotherDetails_Qualification = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_Remarks = table.Column<string>(type: "text", nullable: true),
                    ParentBankDetails_AccountHolderName = table.Column<string>(type: "text", nullable: true),
                    ParentBankDetails_AccountNumber = table.Column<string>(type: "text", nullable: true),
                    ParentBankDetails_BankName = table.Column<string>(type: "text", nullable: true),
                    ParentBankDetails_BranchName = table.Column<string>(type: "text", nullable: true),
                    ParentBankDetails_IfscCode = table.Column<string>(type: "text", nullable: true),
                    PreviousSchoolDetails_LastClassPassed = table.Column<string>(type: "text", nullable: true),
                    PreviousSchoolDetails_Percentage = table.Column<float>(type: "real", nullable: true),
                    PreviousSchoolDetails_PreviousSchoolAddress = table.Column<string>(type: "text", nullable: true),
                    PreviousSchoolDetails_PreviousSchoolBoard = table.Column<string>(type: "text", nullable: true),
                    PreviousSchoolDetails_PreviousSchoolMedium = table.Column<string>(type: "text", nullable: true),
                    PreviousSchoolDetails_PreviousSchoolName = table.Column<string>(type: "text", nullable: true),
                    PreviousSchoolDetails_TcNo = table.Column<string>(type: "text", nullable: true),
                    StudentAdditionalDetails_BPLCardNo = table.Column<string>(type: "text", nullable: true),
                    StudentAdditionalDetails_BPLStudent = table.Column<bool>(type: "boolean", nullable: true),
                    StudentAdditionalDetails_Category = table.Column<int>(type: "integer", nullable: true),
                    StudentAdditionalDetails_EmergencyContactNo = table.Column<string>(type: "text", nullable: true),
                    StudentAdditionalDetails_PersonWithDisability = table.Column<bool>(type: "boolean", nullable: true),
                    StudentAdditionalDetails_RightToEducation = table.Column<bool>(type: "boolean", nullable: true),
                    StudentAdditionalDetails_SecondLanguage = table.Column<string>(type: "text", nullable: true),
                    StudentAdditionalDetails_SingleChild = table.Column<bool>(type: "boolean", nullable: true),
                    StudentAdditionalDetails_SingleParentChild = table.Column<bool>(type: "boolean", nullable: true),
                    StudentAdditionalDetails_SponsorName = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_EmergencyAddressLine1 = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_EmergencyAddressLine2 = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_EmergencyAddressLine3 = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_EmergencyCity = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_EmergencyCountry = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_EmergencyPinCode = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_EmergencyState = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_LocalAddressLine1 = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_LocalAddressLine2 = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_LocalAddressLine3 = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_LocalCity = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_LocalCountryIsoCode = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_LocalCountryName = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_LocalPinCode = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_LocalStateIsoCode = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_LocalStateName = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_ParentContactNo = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_ParentEmail = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_PermanentAddressLine1 = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_PermanentAddressLine2 = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_PermanentAddressLine3 = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_PermanentCity = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_PermanentCountryIsoCode = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_PermanentCountryName = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_PermanentPinCode = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_PermanentStateIsoCode = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_PermanentStateName = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_PrimaryContactNo = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_Remarks = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_StudentContactNo = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_StudentEmail = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_WhatsAppNo = table.Column<string>(type: "text", nullable: true),
                    StudentMedicalRecords_Allergies = table.Column<string>(type: "text", nullable: true),
                    StudentMedicalRecords_BodyMassIndex = table.Column<float>(type: "real", nullable: true),
                    StudentMedicalRecords_ChildImmunisation = table.Column<string>(type: "text", nullable: true),
                    StudentMedicalRecords_CovidVaccination = table.Column<int>(type: "integer", nullable: true),
                    StudentMedicalRecords_Haemoglobin = table.Column<float>(type: "real", nullable: true),
                    StudentMedicalRecords_Height = table.Column<float>(type: "real", nullable: true),
                    StudentMedicalRecords_ImmunisationRemarks = table.Column<string>(type: "text", nullable: true),
                    StudentMedicalRecords_PulseRate = table.Column<float>(type: "real", nullable: true),
                    StudentMedicalRecords_Weight = table.Column<float>(type: "real", nullable: true),
                    CountryIsoCode = table.Column<string>(type: "text", nullable: true),
                    CountryName = table.Column<string>(type: "text", nullable: true),
                    StateIsoCode = table.Column<string>(type: "text", nullable: true),
                    StateName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentEnrollments_AppAcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AppAcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentEnrollments_AppDivisions_DivisionId",
                        column: x => x.DivisionId,
                        principalTable: "AppDivisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentEnrollments_AppEducationBoards_EducationBoardId",
                        column: x => x.EducationBoardId,
                        principalTable: "AppEducationBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentEnrollments_AppEducationMediums_EducationMediumId",
                        column: x => x.EducationMediumId,
                        principalTable: "AppEducationMediums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentEnrollments_AppStandards_StandardId",
                        column: x => x.StandardId,
                        principalTable: "AppStandards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentEnrollments_InquiryPreferences_InquiryPreferenceId",
                        column: x => x.InquiryPreferenceId,
                        principalTable: "InquiryPreferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    Logo = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentLeaves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Attachment = table.Column<byte[]>(type: "bytea", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    EndDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    HalfDay = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentLeaves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentLeaves_AppLeavePolicies_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "AppLeavePolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentLeaves_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppEntranceExams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntranceExamSlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentEnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExamDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    MarksObtained = table.Column<float>(type: "real", nullable: true),
                    PassingMark = table.Column<float>(type: "real", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TotalMarks = table.Column<float>(type: "real", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEntranceExams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEntranceExams_AppEntranceExamSlots_EntranceExamSlotId",
                        column: x => x.EntranceExamSlotId,
                        principalTable: "AppEntranceExamSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppEntranceExams_AppStudentEnrollments_StudentEnrollmentId",
                        column: x => x.StudentEnrollmentId,
                        principalTable: "AppStudentEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppFollowUpMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentEnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Author = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppFollowUpMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppFollowUpMessages_AppStudentEnrollments_StudentEnrollment~",
                        column: x => x.StudentEnrollmentId,
                        principalTable: "AppStudentEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentEnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlobName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DocumentName = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    StudentEnrollmentId1 = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentDocuments_AppStudentEnrollments_StudentEnrollment~",
                        column: x => x.StudentEnrollmentId,
                        principalTable: "AppStudentEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentDocuments_AppStudentEnrollments_StudentEnrollmen~1",
                        column: x => x.StudentEnrollmentId1,
                        principalTable: "AppStudentEnrollments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppStudentDocuments_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppStudentGroupMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDivisionLeader = table.Column<bool>(type: "boolean", nullable: false),
                    IsLeader = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentGroupMembers_AppStudentGroups_StudentGroupId",
                        column: x => x.StudentGroupId,
                        principalTable: "AppStudentGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppStudentGroupMembers_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_AcademicYearId",
                table: "AppStudents",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_DivisionId",
                table: "AppStudents",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_EducationBoardId",
                table: "AppStudents",
                column: "EducationBoardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_EducationMediumId",
                table: "AppStudents",
                column: "EducationMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_EducationMediumStageId",
                table: "AppStudents",
                column: "EducationMediumStageId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_StandardId",
                table: "AppStudents",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEntranceExams_EntranceExamSlotId",
                table: "AppEntranceExams",
                column: "EntranceExamSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEntranceExams_StudentEnrollmentId",
                table: "AppEntranceExams",
                column: "StudentEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppFollowUpMessages_StudentEnrollmentId",
                table: "AppFollowUpMessages",
                column: "StudentEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAttendances_StudentId",
                table: "AppStudentAttendances",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentCertificates_StudentId",
                table: "AppStudentCertificates",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentDocuments_StudentEnrollmentId",
                table: "AppStudentDocuments",
                column: "StudentEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentDocuments_StudentEnrollmentId1",
                table: "AppStudentDocuments",
                column: "StudentEnrollmentId1");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentDocuments_StudentId",
                table: "AppStudentDocuments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentEnrollments_AcademicYearId",
                table: "AppStudentEnrollments",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentEnrollments_DivisionId",
                table: "AppStudentEnrollments",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentEnrollments_EducationBoardId",
                table: "AppStudentEnrollments",
                column: "EducationBoardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentEnrollments_EducationMediumId",
                table: "AppStudentEnrollments",
                column: "EducationMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentEnrollments_InquiryPreferenceId",
                table: "AppStudentEnrollments",
                column: "InquiryPreferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentEnrollments_StandardId",
                table: "AppStudentEnrollments",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentGroupMembers_StudentGroupId_IsDivisionLeader",
                table: "AppStudentGroupMembers",
                columns: new[] { "StudentGroupId", "IsDivisionLeader" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentGroupMembers_StudentGroupId_IsLeader",
                table: "AppStudentGroupMembers",
                columns: new[] { "StudentGroupId", "IsLeader" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentGroupMembers_StudentId_StudentGroupId",
                table: "AppStudentGroupMembers",
                columns: new[] { "StudentId", "StudentGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaves_LeaveTypeId",
                table: "AppStudentLeaves",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentLeaves_StudentId",
                table: "AppStudentLeaves",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppAcademicYears_AcademicYearId",
                table: "AppStudents",
                column: "AcademicYearId",
                principalTable: "AppAcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppDivisions_DivisionId",
                table: "AppStudents",
                column: "DivisionId",
                principalTable: "AppDivisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppEducationBoards_EducationBoardId",
                table: "AppStudents",
                column: "EducationBoardId",
                principalTable: "AppEducationBoards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppEducationMediumStages_EducationMediumStageId",
                table: "AppStudents",
                column: "EducationMediumStageId",
                principalTable: "AppEducationMediumStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppEducationMediums_EducationMediumId",
                table: "AppStudents",
                column: "EducationMediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppStandards_StandardId",
                table: "AppStudents",
                column: "StandardId",
                principalTable: "AppStandards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
