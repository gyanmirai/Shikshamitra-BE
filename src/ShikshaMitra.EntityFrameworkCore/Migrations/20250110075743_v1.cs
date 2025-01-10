using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppStudentEnrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentEnrollmentState = table.Column<int>(type: "integer", nullable: false),
                    InquiryNumber = table.Column<string>(type: "text", nullable: false),
                    RegistrationCode = table.Column<string>(type: "text", nullable: false),
                    FormNumber = table.Column<string>(type: "text", nullable: true),
                    FirstName = table.Column<string>(type: "text", nullable: true),
                    MiddleName = table.Column<string>(type: "text", nullable: true),
                    LastName = table.Column<string>(type: "text", nullable: true),
                    PreviousSchoolName = table.Column<string>(type: "text", nullable: true),
                    DateOfBirth = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DateOfInquiry = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GenderType = table.Column<int>(type: "integer", nullable: false),
                    ParentContactNumber = table.Column<string>(type: "text", nullable: true),
                    AlternativeContactNumber = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<string>(type: "text", nullable: true),
                    PinCode = table.Column<int>(type: "integer", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    InquiryStatusType = table.Column<int>(type: "integer", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationBoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    InquiryPreferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProfilePicture = table.Column<byte[]>(type: "bytea", nullable: true),
                    AadhaarNo = table.Column<string>(type: "text", nullable: true),
                    Religion = table.Column<string>(type: "text", nullable: true),
                    FamilyCast = table.Column<string>(type: "text", nullable: true),
                    CastType = table.Column<int>(type: "integer", nullable: true),
                    MotherTongue = table.Column<string>(type: "text", nullable: true),
                    Nationality = table.Column<string>(type: "text", nullable: true),
                    BirthPlace = table.Column<string>(type: "text", nullable: true),
                    TemporaryGr = table.Column<string>(type: "text", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_AadhaarNo = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_Address = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_ContactNo = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_DateOfBirth = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FatherDetails_Designation = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_Email = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_Name = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_Occupation = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_OfficeAddress = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_OfficeNo = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_Organisation = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_PassOutYear = table.Column<int>(type: "integer", nullable: true),
                    FatherDetails_Qualification = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_Remarks = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_AadhaarNo = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_Address = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_ContactNo = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_DateOfBirth = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GuardianDetails_Designation = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_Email = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_Name = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_Occupation = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_OfficeAddress = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_OfficeNo = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_Organisation = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_PassOutYear = table.Column<int>(type: "integer", nullable: true),
                    GuardianDetails_Qualification = table.Column<string>(type: "text", nullable: true),
                    GuardianDetails_Remarks = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_AadhaarNo = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_Address = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_ContactNo = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_DateOfBirth = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MotherDetails_Designation = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_Email = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_Name = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_Occupation = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_OfficeAddress = table.Column<string>(type: "text", nullable: true),
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
                    StudentAdditionalDetails_BPLStudent = table.Column<int>(type: "integer", nullable: true),
                    StudentAdditionalDetails_Category = table.Column<int>(type: "integer", nullable: true),
                    StudentAdditionalDetails_EmergencyContactNo = table.Column<string>(type: "text", nullable: true),
                    StudentAdditionalDetails_PersonWithDisability = table.Column<int>(type: "integer", nullable: true),
                    StudentAdditionalDetails_RightToEducation = table.Column<int>(type: "integer", nullable: true),
                    StudentAdditionalDetails_SecondLanguage = table.Column<string>(type: "text", nullable: true),
                    StudentAdditionalDetails_SingleChild = table.Column<int>(type: "integer", nullable: true),
                    StudentAdditionalDetails_SingleParentChild = table.Column<int>(type: "integer", nullable: true),
                    StudentAdditionalDetails_SponsorName = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_EmergencyAddress = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_EmergencyCity = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_EmergencyPinCode = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_EmergencyState = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_LocalAddress = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_LocalCity = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_LocalPinCode = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_LocalState = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_ParentContactNo = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_ParentEmail = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_PermanentAddress = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_PermanentCity = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_PermanentPinCode = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_PermanentState = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_PrimaryContactNo = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_Remarks = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_StudentContactNo = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_StudentEmail = table.Column<string>(type: "text", nullable: true),
                    StudentCommunicationDetails_WhatsAppNo = table.Column<string>(type: "text", nullable: true),
                    StudentMedicalRecords_Allergies = table.Column<string>(type: "text", nullable: true),
                    StudentMedicalRecords_BodyMassIndex = table.Column<int>(type: "integer", nullable: true),
                    StudentMedicalRecords_ChildImmunisation = table.Column<string>(type: "text", nullable: true),
                    StudentMedicalRecords_CovidVaccination = table.Column<string>(type: "text", nullable: true),
                    StudentMedicalRecords_Haemoglobin = table.Column<string>(type: "text", nullable: true),
                    StudentMedicalRecords_Height = table.Column<float>(type: "real", nullable: true),
                    StudentMedicalRecords_ImmunisationRemarks = table.Column<string>(type: "text", nullable: true),
                    StudentMedicalRecords_Weight = table.Column<float>(type: "real", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_AppStudentEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentEnrollments_AbpRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AbpRoles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppStudentEnrollments_AppAcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AppAcademicYears",
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
                name: "AppStudentDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BlobName = table.Column<string>(type: "text", nullable: false),
                    StudentEnrollmentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentDocuments_AppStudentEnrollments_StudentEnrollment~",
                        column: x => x.StudentEnrollmentId,
                        principalTable: "AppStudentEnrollments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentDocuments_StudentEnrollmentId",
                table: "AppStudentDocuments",
                column: "StudentEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentEnrollments_AcademicYearId",
                table: "AppStudentEnrollments",
                column: "AcademicYearId");

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
                name: "IX_AppStudentEnrollments_RoleId",
                table: "AppStudentEnrollments",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentEnrollments_StandardId",
                table: "AppStudentEnrollments",
                column: "StandardId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppStudentDocuments");

            migrationBuilder.DropTable(
                name: "AppStudentEnrollments");
        }
    }
}
