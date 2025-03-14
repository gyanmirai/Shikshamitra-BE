using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
			migrationBuilder.Sql(
  "ALTER TABLE \"AppStudentEnrollments\" ALTER COLUMN \"TemporaryGr\" TYPE integer USING (\"TemporaryGr\"::integer);"
);


			migrationBuilder.RenameColumn(
                name: "Name",
                table: "AppStudents",
                newName: "StateName");

            migrationBuilder.AlterColumn<Guid>(
                name: "StandardId",
                table: "AppStudents",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "GrNoRecord",
                table: "AppStudents",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<Guid>(
                name: "EducationMediumStageId",
                table: "AppStudents",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "DivisionId",
                table: "AppStudents",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "StateName",
                table: "AppStudents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "AadhaarNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

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
                name: "FatherDetails_Address",
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
                name: "FatherDetails_OfficeAddress",
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
                name: "FirstName",
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

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_AadhaarNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianDetails_Address",
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
                name: "GuardianDetails_OfficeAddress",
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
                name: "LastName",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MiddleName",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_AadhaarNo",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotherDetails_Address",
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
                name: "MotherDetails_OfficeAddress",
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
                name: "StudentCommunicationDetails_LocalCountry",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_LocalPinCode",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_LocalState",
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
                name: "StudentCommunicationDetails_PermanentCountry",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_PermanentPinCode",
                table: "AppStudents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentCommunicationDetails_PermanentState",
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

            migrationBuilder.AddColumn<int>(
                name: "TempGrNo",
                table: "AppStudents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "AppStudents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TemporaryGr",
                table: "AppStudentEnrollments",
                type: "integer",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentDocuments_StudentId",
                table: "AppStudentDocuments",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudentDocuments_AppStudents_StudentId",
                table: "AppStudentDocuments",
                column: "StudentId",
                principalTable: "AppStudents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudentDocuments_AppStudents_StudentId",
                table: "AppStudentDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentDocuments_StudentId",
                table: "AppStudentDocuments");

            migrationBuilder.DropColumn(
                name: "AadhaarNo",
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
                name: "Email",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FamilyCast",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_AadhaarNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FatherDetails_Address",
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
                name: "FatherDetails_OfficeAddress",
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
                name: "FirstName",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FormNumber",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GenderType",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_AadhaarNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "GuardianDetails_Address",
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
                name: "GuardianDetails_OfficeAddress",
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
                name: "LastName",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MiddleName",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_AadhaarNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MotherDetails_Address",
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
                name: "MotherDetails_OfficeAddress",
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
                name: "StudentCommunicationDetails_LocalCountry",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_LocalPinCode",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_LocalState",
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
                name: "StudentCommunicationDetails_PermanentCountry",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_PermanentPinCode",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentCommunicationDetails_PermanentState",
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
                name: "TempGrNo",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "AppStudents");

            migrationBuilder.RenameColumn(
                name: "StateName",
                table: "AppStudents",
                newName: "Name");

            migrationBuilder.AlterColumn<Guid>(
                name: "StandardId",
                table: "AppStudents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "GrNoRecord",
                table: "AppStudents",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "EducationMediumStageId",
                table: "AppStudents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "DivisionId",
                table: "AppStudents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "AppStudents",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TemporaryGr",
                table: "AppStudentEnrollments",
                type: "text",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
