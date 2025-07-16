using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v37 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppStudentAdditionalDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FormNo = table.Column<string>(type: "text", nullable: true),
                    DateOfBirthday = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Gender = table.Column<int>(type: "integer", nullable: true),
                    ProfilePictureUrl = table.Column<string>(type: "text", nullable: true),
                    DateOfInquiry = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InquiryNo = table.Column<string>(type: "text", nullable: false),
                    InquiryPreferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RegistrationFormNo = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: true),
                    IsBPLStudent = table.Column<bool>(type: "boolean", nullable: true),
                    BPLCardNo = table.Column<string>(type: "text", nullable: true),
                    HaveDisability = table.Column<bool>(type: "boolean", nullable: true),
                    SecondLanguage = table.Column<string>(type: "text", nullable: true),
                    EmergencyContactNo = table.Column<string>(type: "text", nullable: true),
                    EmergencyRelationName = table.Column<string>(type: "text", nullable: true),
                    EmergencyRelationType = table.Column<string>(type: "text", nullable: true),
                    IsSingleParentChild = table.Column<bool>(type: "boolean", nullable: true),
                    IsSingleChild = table.Column<bool>(type: "boolean", nullable: true),
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
                    table.PrimaryKey("PK_AppStudentAdditionalDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentContactDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrimaryContactNo = table.Column<string>(type: "text", nullable: false),
                    SecondaryContactNo = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Address1 = table.Column<string>(type: "text", nullable: true),
                    Address2 = table.Column<string>(type: "text", nullable: true),
                    Address3 = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    Pincode = table.Column<string>(type: "text", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AppStudentContactDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentContactDetails_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentContactDetails_StudentId",
                table: "AppStudentContactDetails",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppStudentAdditionalDetails");

            migrationBuilder.DropTable(
                name: "AppStudentContactDetails");
        }
    }
}
