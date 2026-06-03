using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Finance_AuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubModuleCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentNameSnapshot = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    StudentGrNoSnapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    StudentRollNoSnapshot = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorNameSnapshot = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PerformedByRoleSnapshot = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Summary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ChangedFieldsJson = table.Column<string>(type: "text", nullable: true),
                    ContextJson = table.Column<string>(type: "text", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SourceAuditLogId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_AuditEvents_Tenant_AcademicYear_Time",
                table: "Finance_AuditEvents",
                columns: new[] { "TenantId", "AcademicYearId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_AuditEvents_Tenant_Action_Time",
                table: "Finance_AuditEvents",
                columns: new[] { "TenantId", "Action", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_AuditEvents_Tenant_Correlation",
                table: "Finance_AuditEvents",
                columns: new[] { "TenantId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_AuditEvents_Tenant_Entity_Time",
                table: "Finance_AuditEvents",
                columns: new[] { "TenantId", "EntityType", "EntityId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_AuditEvents_Tenant_Student_Time",
                table: "Finance_AuditEvents",
                columns: new[] { "TenantId", "StudentId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Finance_AuditEvents");
        }
    }
}
