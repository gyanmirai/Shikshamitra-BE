using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddSubstitutionSettingsAndNotificationLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAcademicSubstitutionNotificationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubstitutionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Channel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAcademicSubstitutionNotificationLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAcademicSubstitutionSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaxSubstitutionsPerTeacherPerDay = table.Column<int>(type: "integer", nullable: false),
                    MaxSubstitutionsPerTeacherPerWeek = table.Column<int>(type: "integer", nullable: false),
                    AcknowledgementRequiredByDefault = table.Column<bool>(type: "boolean", nullable: false),
                    AutoReminderEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ReminderBeforeMinutes = table.Column<int>(type: "integer", nullable: false),
                    NotifyLeadershipForUrgent = table.Column<bool>(type: "boolean", nullable: false),
                    AllowTeacherReject = table.Column<bool>(type: "boolean", nullable: false),
                    StudentVisibleDailySheet = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppAcademicSubstitutionSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicSubstitutionNotificationLogs_SubstitutionRequest~",
                table: "AppAcademicSubstitutionNotificationLogs",
                columns: new[] { "SubstitutionRequestId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicSubstitutionSettings_TenantId_AcademicYearId",
                table: "AppAcademicSubstitutionSettings",
                columns: new[] { "TenantId", "AcademicYearId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAcademicSubstitutionNotificationLogs");

            migrationBuilder.DropTable(
                name: "AppAcademicSubstitutionSettings");
        }
    }
}
