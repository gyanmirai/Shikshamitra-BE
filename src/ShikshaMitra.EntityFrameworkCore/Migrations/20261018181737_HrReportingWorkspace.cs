using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class HrReportingWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HR_ReportSchedules");

            migrationBuilder.AddColumn<int>(
                name: "DefinitionVersion",
                table: "HR_ReportTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PublishedDefinitionJson",
                table: "HR_ReportTemplates",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishedDescription",
                table: "HR_ReportTemplates",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishedName",
                table: "HR_ReportTemplates",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PublishedRevision",
                table: "HR_ReportTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "HR_ReportTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "HR_ReportRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TemplateId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    DataSource = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DefinitionJson = table.Column<string>(type: "jsonb", nullable: false),
                    ParametersJson = table.Column<string>(type: "jsonb", nullable: false),
                    ScopeJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExcelReady = table.Column<bool>(type: "boolean", nullable: false),
                    PdfReady = table.Column<bool>(type: "boolean", nullable: false),
                    ExcelStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PdfStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("PK_HR_ReportRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HR_ReportRunPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: false),
                    RowsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_ReportRunPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HR_ReportRunPages_HR_ReportRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "HR_ReportRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HR_ReportRunPages_ExpiresAt",
                table: "HR_ReportRunPages",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_HR_ReportRunPages_RunId",
                table: "HR_ReportRunPages",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_HR_ReportRunPages_TenantId_RunId_PageNumber",
                table: "HR_ReportRunPages",
                columns: new[] { "TenantId", "RunId", "PageNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HR_ReportRuns_TenantId_OwnerUserId_CreationTime",
                table: "HR_ReportRuns",
                columns: new[] { "TenantId", "OwnerUserId", "CreationTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HR_ReportRunPages");

            migrationBuilder.DropTable(
                name: "HR_ReportRuns");

            migrationBuilder.DropColumn(
                name: "DefinitionVersion",
                table: "HR_ReportTemplates");

            migrationBuilder.DropColumn(
                name: "PublishedDefinitionJson",
                table: "HR_ReportTemplates");

            migrationBuilder.DropColumn(
                name: "PublishedDescription",
                table: "HR_ReportTemplates");

            migrationBuilder.DropColumn(
                name: "PublishedName",
                table: "HR_ReportTemplates");

            migrationBuilder.DropColumn(
                name: "PublishedRevision",
                table: "HR_ReportTemplates");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "HR_ReportTemplates");

            migrationBuilder.CreateTable(
                name: "HR_ReportSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefinitionJson = table.Column<string>(type: "jsonb", nullable: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveryMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    Frequency = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NextRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HR_ReportSchedules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HR_ReportSchedules_Tenant_Owner_Status_NextRun",
                table: "HR_ReportSchedules",
                columns: new[] { "TenantId", "OwnerUserId", "Status", "NextRunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_ReportSchedules_Tenant_Status_NextRun",
                table: "HR_ReportSchedules",
                columns: new[] { "TenantId", "Status", "NextRunAt" });
        }
    }
}
