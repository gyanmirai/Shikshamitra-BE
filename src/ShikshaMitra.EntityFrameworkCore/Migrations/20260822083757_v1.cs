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
            migrationBuilder.AddColumn<Guid>(
                name: "SeatingPlanId",
                table: "AppSeatingPlanAuditLogs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "AppAcademicCalendarAuditReviewNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuditLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedByRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_AppAcademicCalendarAuditReviewNotes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSeatingPlanAuditLogs_ExamId_SeatingPlanId",
                table: "AppSeatingPlanAuditLogs",
                columns: new[] { "ExamId", "SeatingPlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicCalendarAuditReviewNotes_TenantId_AuditLogId_Cre~",
                table: "AppAcademicCalendarAuditReviewNotes",
                columns: new[] { "TenantId", "AuditLogId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAcademicCalendarAuditReviewNotes");

            migrationBuilder.DropIndex(
                name: "IX_AppSeatingPlanAuditLogs_ExamId_SeatingPlanId",
                table: "AppSeatingPlanAuditLogs");

            migrationBuilder.DropColumn(
                name: "SeatingPlanId",
                table: "AppSeatingPlanAuditLogs");
        }
    }
}
