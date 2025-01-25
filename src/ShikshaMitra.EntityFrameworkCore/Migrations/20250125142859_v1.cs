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
            migrationBuilder.DropTable(
                name: "AppStudentInquiries");

            migrationBuilder.DropTable(
                name: "AppStudents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppStudentInquiries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationBoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    InquiryPreferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    AlternativeContactNumber = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DateOfBirth = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DateOfInquiry = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    FormNumber = table.Column<string>(type: "text", nullable: false),
                    GenderType = table.Column<int>(type: "integer", nullable: false),
                    InquiryNumber = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    MiddleName = table.Column<string>(type: "text", nullable: false),
                    ParentContactNumber = table.Column<string>(type: "text", nullable: false),
                    PinCode = table.Column<int>(type: "integer", nullable: false),
                    PreviousSchoolName = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentInquiries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentInquiries_AppAcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AppAcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentInquiries_AppEducationBoards_EducationBoardId",
                        column: x => x.EducationBoardId,
                        principalTable: "AppEducationBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentInquiries_AppEducationMediums_EducationMediumId",
                        column: x => x.EducationMediumId,
                        principalTable: "AppEducationMediums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentInquiries_AppStandards_StandardId",
                        column: x => x.StandardId,
                        principalTable: "AppStandards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudentInquiries_InquiryPreferences_InquiryPreferenceId",
                        column: x => x.InquiryPreferenceId,
                        principalTable: "InquiryPreferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppStudents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    GenderType = table.Column<int>(type: "integer", nullable: false),
                    GrNo = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    MiddleName = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ProfilePicture = table.Column<byte[]>(type: "bytea", nullable: true),
                    Surname = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Address_BirthPlace = table.Column<string>(type: "text", nullable: false),
                    Address_CasteCategory = table.Column<string>(type: "text", nullable: false),
                    Address_EmergencyAddress = table.Column<string>(type: "text", nullable: false),
                    Address_FamilyCaste = table.Column<string>(type: "text", nullable: false),
                    Address_LocalAddress = table.Column<string>(type: "text", nullable: false),
                    Address_MotherTongue = table.Column<string>(type: "text", nullable: false),
                    Address_Nationality = table.Column<string>(type: "text", nullable: false),
                    Address_PermanentAddress = table.Column<string>(type: "text", nullable: false),
                    Address_Religion = table.Column<string>(type: "text", nullable: false),
                    AdmissionDetails_AdmissionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AdmissionDetails_AdmissionFormNumber = table.Column<string>(type: "text", nullable: true),
                    AdmissionDetails_AdmissionQuota = table.Column<string>(type: "text", nullable: true),
                    AdmissionDetails_AdmissionStandard = table.Column<string>(type: "text", nullable: true),
                    AdmissionDetails_PreviousLcDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AdmissionDetails_PreviousLcNumber = table.Column<string>(type: "text", nullable: true),
                    AdmissionDetails_PreviousSchool = table.Column<string>(type: "text", nullable: true),
                    AdmissionDetails_PreviousSchoolLeftDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CommunicationDetails_ParentContactNumber = table.Column<string>(type: "text", nullable: true),
                    CommunicationDetails_ParentEmail = table.Column<string>(type: "text", nullable: true),
                    CommunicationDetails_StudentContactNumber = table.Column<string>(type: "text", nullable: true),
                    CommunicationDetails_StudentEmail = table.Column<string>(type: "text", nullable: true),
                    CommunicationDetails_WhatsAppContactNumber = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_AadharNo = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_AnnualIncome = table.Column<decimal>(type: "numeric", nullable: true),
                    FatherDetails_ContactNumber = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    FatherDetails_Designation = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_Email = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_ExStudent = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_Name = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_Occupation = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_OfficeAddress = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_OfficeContactNumber = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_Organization = table.Column<string>(type: "text", nullable: true),
                    FatherDetails_PassOutYear = table.Column<int>(type: "integer", nullable: true),
                    FatherDetails_Qualification = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_AadharNo = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_AnnualIncome = table.Column<decimal>(type: "numeric", nullable: true),
                    MotherDetails_ContactNumber = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    MotherDetails_Designation = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_Email = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_ExStudent = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_Name = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_Occupation = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_OfficeAddress = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_OfficeContactNumber = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_Organization = table.Column<string>(type: "text", nullable: true),
                    MotherDetails_PassOutYear = table.Column<int>(type: "integer", nullable: true),
                    MotherDetails_Qualification = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudents_AbpUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudents_AppDivisions_DivisionId",
                        column: x => x.DivisionId,
                        principalTable: "AppDivisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppStudents_AppStandards_StandardId",
                        column: x => x.StandardId,
                        principalTable: "AppStandards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentInquiries_AcademicYearId",
                table: "AppStudentInquiries",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentInquiries_EducationBoardId",
                table: "AppStudentInquiries",
                column: "EducationBoardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentInquiries_EducationMediumId",
                table: "AppStudentInquiries",
                column: "EducationMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentInquiries_InquiryPreferenceId",
                table: "AppStudentInquiries",
                column: "InquiryPreferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentInquiries_StandardId",
                table: "AppStudentInquiries",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_DivisionId",
                table: "AppStudents",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_StandardId",
                table: "AppStudents",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_UserId",
                table: "AppStudents",
                column: "UserId");
        }
    }
}
