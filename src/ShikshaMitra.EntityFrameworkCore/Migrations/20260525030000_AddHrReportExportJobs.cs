using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    public partial class AddHrReportExportJobs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HR_ReportExportJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportType = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    Format = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FilterJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BlobName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_ReportExportJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HR_ReportExportJobs_Tenant_Status_Expires",
                table: "HR_ReportExportJobs",
                columns: new[] { "TenantId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_ReportExportJobs_Tenant_User_Created",
                table: "HR_ReportExportJobs",
                columns: new[] { "TenantId", "RequestedByUserId", "CreationTime" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HR_ReportExportJobs");
        }
    }
}
