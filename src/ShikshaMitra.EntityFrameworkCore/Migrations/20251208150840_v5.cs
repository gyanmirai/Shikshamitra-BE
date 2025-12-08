using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_InquiryPreferences",
                table: "InquiryPreferences");

            migrationBuilder.DropColumn(
                name: "DocumentTypes",
                table: "StudentDocumentSettings");

            migrationBuilder.RenameTable(
                name: "InquiryPreferences",
                newName: "AppInquiryPreferences");

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "AppStudents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdmissionApprovalRemarks",
                table: "AppStudents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AdmissionApprovalStatus",
                table: "AppStudents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "BankDetailId",
                table: "AppStudents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommunicationDetailId",
                table: "AppStudents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FamilyDetailId",
                table: "AppStudents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MedicalRecordId",
                table: "AppStudents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousSchoolDetailId",
                table: "AppStudents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StudentDocumentSettingId",
                table: "AppDocumentTypes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PreferenceName",
                table: "AppInquiryPreferences",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppInquiryPreferences",
                table: "AppInquiryPreferences",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "AppStudentDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlobName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentDocuments_AppDocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "AppDocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentDocuments_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentBankDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccountNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IFSCCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BranchName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccountHolderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccountType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BankAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MICRCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SwiftCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentBankDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentCommunicationDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocalAddress_BuildingNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LocalAddress_AreaLocalityRoadName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LocalAddress_Landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LocalAddress_State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LocalAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LocalAddress_Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    LocalAddress_Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PermanentAddress_BuildingNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PermanentAddress_AreaLocalityRoadName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PermanentAddress_Landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PermanentAddress_State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PermanentAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PermanentAddress_Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    PermanentAddress_Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmergencyAddress_BuildingNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmergencyAddress_AreaLocalityRoadName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmergencyAddress_Landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmergencyAddress_State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmergencyAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmergencyAddress_Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    EmergencyAddress_Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsPermanentSameAsLocal = table.Column<bool>(type: "boolean", nullable: false),
                    IsEmergencySameAsLocal = table.Column<bool>(type: "boolean", nullable: false),
                    EmergencyContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EmergencyContactRelation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentCommunicationDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentFamilyDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FatherDetails_FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FatherDetails_DateOfBirth = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FatherDetails_Occupation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FatherDetails_Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FatherDetails_ContactNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FatherDetails_OfficeNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FatherDetails_Qualification = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FatherDetails_YearOfPassing = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    FatherDetails_Designation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FatherDetails_Organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FatherDetails_AadharNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Father_OfficeAddress_BuildingNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Father_OfficeAddress_AreaLocalityRoadName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Father_OfficeAddress_Landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Father_OfficeAddress_State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Father_OfficeAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Father_OfficeAddress_Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Father_OfficeAddress_Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Father_LocalAddress_BuildingNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Father_LocalAddress_AreaLocalityRoadName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Father_LocalAddress_Landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Father_LocalAddress_State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Father_LocalAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Father_LocalAddress_Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Father_LocalAddress_Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MotherDetails_FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MotherDetails_DateOfBirth = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MotherDetails_Occupation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MotherDetails_Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    MotherDetails_ContactNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MotherDetails_OfficeNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MotherDetails_Qualification = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MotherDetails_YearOfPassing = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    MotherDetails_Designation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MotherDetails_Organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MotherDetails_AadharNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Mother_OfficeAddress_BuildingNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Mother_OfficeAddress_AreaLocalityRoadName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Mother_OfficeAddress_Landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Mother_OfficeAddress_State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Mother_OfficeAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Mother_OfficeAddress_Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Mother_OfficeAddress_Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Mother_LocalAddress_BuildingNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Mother_LocalAddress_AreaLocalityRoadName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Mother_LocalAddress_Landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Mother_LocalAddress_State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Mother_LocalAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Mother_LocalAddress_Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Mother_LocalAddress_Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GuardianDetails_FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GuardianDetails_DateOfBirth = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GuardianDetails_Occupation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GuardianDetails_Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    GuardianDetails_ContactNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    GuardianDetails_OfficeNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    GuardianDetails_Qualification = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GuardianDetails_YearOfPassing = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    GuardianDetails_Designation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GuardianDetails_Organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GuardianDetails_AadharNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Guardian_OfficeAddress_BuildingNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Guardian_OfficeAddress_AreaLocalityRoadName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Guardian_OfficeAddress_Landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Guardian_OfficeAddress_State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Guardian_OfficeAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Guardian_OfficeAddress_Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Guardian_OfficeAddress_Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Guardian_LocalAddress_BuildingNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Guardian_LocalAddress_AreaLocalityRoadName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Guardian_LocalAddress_Landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Guardian_LocalAddress_State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Guardian_LocalAddress_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Guardian_LocalAddress_Pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Guardian_LocalAddress_Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SiblingStudentIds = table.Column<string>(type: "text", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentFamilyDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentMedicalRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    WeightKg = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    HeightCm = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    BodyMassIndex = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    PulseRate = table.Column<int>(type: "integer", nullable: true),
                    Hemoglobin = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    Allergies = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChildImmunization = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CovidVaccinationDoses = table.Column<int>(type: "integer", nullable: true),
                    ImmunizationRemarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BloodGroup = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ChronicConditions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CurrentMedications = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentMedicalRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentPreviousSchoolDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SchoolName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SchoolAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Board = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Medium = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastClassAttended = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TCNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DateOfLeaving = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PercentageObtained = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    AcademicYear = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ReasonForLeaving = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentPreviousSchoolDetails", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_AcademicYearId_Status",
                table: "AppStudents",
                columns: new[] { "AcademicYearId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_AdmissionApprovalStatus_DivisionId",
                table: "AppStudents",
                columns: new[] { "AdmissionApprovalStatus", "DivisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_BankDetailId",
                table: "AppStudents",
                column: "BankDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_CommunicationDetailId",
                table: "AppStudents",
                column: "CommunicationDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_FamilyDetailId",
                table: "AppStudents",
                column: "FamilyDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_MedicalRecordId",
                table: "AppStudents",
                column: "MedicalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_PreviousSchoolDetailId",
                table: "AppStudents",
                column: "PreviousSchoolDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDocumentTypes_StudentDocumentSettingId",
                table: "AppDocumentTypes",
                column: "StudentDocumentSettingId");

            migrationBuilder.CreateIndex(
                name: "IX_AppInquiryPreferences_TenantId_PreferenceName",
                table: "AppInquiryPreferences",
                columns: new[] { "TenantId", "PreferenceName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentDocuments_DocumentTypeId",
                table: "AppStudentDocuments",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentDocuments_StudentId",
                table: "AppStudentDocuments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentDocuments_StudentId_DocumentTypeId",
                table: "AppStudentDocuments",
                columns: new[] { "StudentId", "DocumentTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentBankDetails_AccountNo",
                table: "StudentBankDetails",
                column: "AccountNo");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBankDetails_IFSCCode",
                table: "StudentBankDetails",
                column: "IFSCCode");

            migrationBuilder.AddForeignKey(
                name: "FK_AppDocumentTypes_StudentDocumentSettings_StudentDocumentSet~",
                table: "AppDocumentTypes",
                column: "StudentDocumentSettingId",
                principalTable: "StudentDocumentSettings",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_StudentBankDetails_BankDetailId",
                table: "AppStudents",
                column: "BankDetailId",
                principalTable: "StudentBankDetails",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_StudentCommunicationDetails_CommunicationDetail~",
                table: "AppStudents",
                column: "CommunicationDetailId",
                principalTable: "StudentCommunicationDetails",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_StudentFamilyDetails_FamilyDetailId",
                table: "AppStudents",
                column: "FamilyDetailId",
                principalTable: "StudentFamilyDetails",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_StudentMedicalRecords_MedicalRecordId",
                table: "AppStudents",
                column: "MedicalRecordId",
                principalTable: "StudentMedicalRecords",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_StudentPreviousSchoolDetails_PreviousSchoolDeta~",
                table: "AppStudents",
                column: "PreviousSchoolDetailId",
                principalTable: "StudentPreviousSchoolDetails",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppDocumentTypes_StudentDocumentSettings_StudentDocumentSet~",
                table: "AppDocumentTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_StudentBankDetails_BankDetailId",
                table: "AppStudents");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_StudentCommunicationDetails_CommunicationDetail~",
                table: "AppStudents");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_StudentFamilyDetails_FamilyDetailId",
                table: "AppStudents");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_StudentMedicalRecords_MedicalRecordId",
                table: "AppStudents");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_StudentPreviousSchoolDetails_PreviousSchoolDeta~",
                table: "AppStudents");

            migrationBuilder.DropTable(
                name: "AppStudentDocuments");

            migrationBuilder.DropTable(
                name: "StudentBankDetails");

            migrationBuilder.DropTable(
                name: "StudentCommunicationDetails");

            migrationBuilder.DropTable(
                name: "StudentFamilyDetails");

            migrationBuilder.DropTable(
                name: "StudentMedicalRecords");

            migrationBuilder.DropTable(
                name: "StudentPreviousSchoolDetails");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_AcademicYearId_Status",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_AdmissionApprovalStatus_DivisionId",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_BankDetailId",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_CommunicationDetailId",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_FamilyDetailId",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_MedicalRecordId",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_PreviousSchoolDetailId",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppDocumentTypes_StudentDocumentSettingId",
                table: "AppDocumentTypes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppInquiryPreferences",
                table: "AppInquiryPreferences");

            migrationBuilder.DropIndex(
                name: "IX_AppInquiryPreferences_TenantId_PreferenceName",
                table: "AppInquiryPreferences");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "AdmissionApprovalRemarks",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "AdmissionApprovalStatus",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "BankDetailId",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "CommunicationDetailId",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "FamilyDetailId",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "MedicalRecordId",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "PreviousSchoolDetailId",
                table: "AppStudents");

            migrationBuilder.DropColumn(
                name: "StudentDocumentSettingId",
                table: "AppDocumentTypes");

            migrationBuilder.RenameTable(
                name: "AppInquiryPreferences",
                newName: "InquiryPreferences");

            migrationBuilder.AddColumn<int[]>(
                name: "DocumentTypes",
                table: "StudentDocumentSettings",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.AlterColumn<string>(
                name: "PreferenceName",
                table: "InquiryPreferences",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddPrimaryKey(
                name: "PK_InquiryPreferences",
                table: "InquiryPreferences",
                column: "Id");
        }
    }
}
