using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppEmployeeApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    MiddleName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    MobileNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SecondaryMobileNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PositionAppliedFor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DepartmentAppliedFor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<int>(type: "integer", nullable: true),
                    Religion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CasteType = table.Column<int>(type: "integer", nullable: true),
                    FamilyCaste = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MaritalStatus = table.Column<int>(type: "integer", nullable: true),
                    BloodGroup = table.Column<int>(type: "integer", nullable: true),
                    QualificationSummary = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    SkillsSummary = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Address1 = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Address2 = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Address3 = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CountryName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CountryIsoCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    StateName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StateIsoCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    City = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PinCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LinkLastIssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalizedEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResumeBlobName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ResumeFileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ResumeContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ResumeSize = table.Column<long>(type: "bigint", nullable: true),
                    PhotoBlobName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PhotoFileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PhotoContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PhotoSize = table.Column<long>(type: "bigint", nullable: true),
                    StatusReason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    InternalNotes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WorkExperiences = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEmployeeApplications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeApplications_TenantId_ApplicationNumber",
                table: "AppEmployeeApplications",
                columns: new[] { "TenantId", "ApplicationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeApplications_TenantId_Email",
                table: "AppEmployeeApplications",
                columns: new[] { "TenantId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeApplications_TenantId_Status",
                table: "AppEmployeeApplications",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeApplications_TokenHash",
                table: "AppEmployeeApplications",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppEmployeeApplications");
        }
    }
}
