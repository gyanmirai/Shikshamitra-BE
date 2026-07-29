using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionYearSetupVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AdmissionYearSetupId",
                table: "AppAdmissionInquiryLeads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdmissionYearSetupSnapshotJson",
                table: "AppAdmissionInquiryLeads",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AdmissionYearSetupVersion",
                table: "AppAdmissionInquiryLeads",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppAdmissionCrmAdmissionYearSetups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InquiryOpenDate = table.Column<DateTime>(type: "date", nullable: false),
                    InquiryCloseDate = table.Column<DateTime>(type: "date", nullable: false),
                    RegistrationCloseDate = table.Column<DateTime>(type: "date", nullable: false),
                    ActivationTargetDate = table.Column<DateTime>(type: "date", nullable: false),
                    EntranceExamRequired = table.Column<bool>(type: "boolean", nullable: false),
                    FinanceVerificationRequired = table.Column<bool>(type: "boolean", nullable: false),
                    PlacementTiming = table.Column<int>(type: "integer", nullable: false),
                    DefaultOwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetStandardIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    RouteTypesJson = table.Column<string>(type: "jsonb", nullable: false),
                    StepOwnersJson = table.Column<string>(type: "jsonb", nullable: false),
                    PublishedSnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CloseReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_AppAdmissionCrmAdmissionYearSetups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionInquiryLeads_AdmissionYearSetupId",
                table: "AppAdmissionInquiryLeads",
                column: "AdmissionYearSetupId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionInquiryLeads_TenantId_AdmissionYearSetupId",
                table: "AppAdmissionInquiryLeads",
                columns: new[] { "TenantId", "AdmissionYearSetupId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmAdmissionYearSetups_TenantId_AcademicYearId_~",
                table: "AppAdmissionCrmAdmissionYearSetups",
                columns: new[] { "TenantId", "AcademicYearId", "Status" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionCrmAdmissionYearSetups_TenantId_AcademicYearId~1",
                table: "AppAdmissionCrmAdmissionYearSetups",
                columns: new[] { "TenantId", "AcademicYearId", "Version" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_AppAdmissionInquiryLeads_AppAdmissionCrmAdmissionYearSetups~",
                table: "AppAdmissionInquiryLeads",
                column: "AdmissionYearSetupId",
                principalTable: "AppAdmissionCrmAdmissionYearSetups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppAdmissionInquiryLeads_AppAdmissionCrmAdmissionYearSetups~",
                table: "AppAdmissionInquiryLeads");

            migrationBuilder.DropTable(
                name: "AppAdmissionCrmAdmissionYearSetups");

            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionInquiryLeads_AdmissionYearSetupId",
                table: "AppAdmissionInquiryLeads");

            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionInquiryLeads_TenantId_AdmissionYearSetupId",
                table: "AppAdmissionInquiryLeads");

            migrationBuilder.DropColumn(
                name: "AdmissionYearSetupId",
                table: "AppAdmissionInquiryLeads");

            migrationBuilder.DropColumn(
                name: "AdmissionYearSetupSnapshotJson",
                table: "AppAdmissionInquiryLeads");

            migrationBuilder.DropColumn(
                name: "AdmissionYearSetupVersion",
                table: "AppAdmissionInquiryLeads");

        }
    }
}
