using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class CreateAppCustomAttendanceRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppEmployeeLeaves_AbpUsers_UserId",
                table: "AppEmployeeLeaves");

            migrationBuilder.DropIndex(
                name: "IX_AppEmployeeLeaves_UserId",
                table: "AppEmployeeLeaves");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "AppEmployeeLeaves");

            migrationBuilder.DropColumn(
                name: "TotalHoursWorked",
                table: "AppEmployeeAttendances");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "AppEmployeeAttendances",
                newName: "StatusType");

            migrationBuilder.CreateTable(
                name: "AppAutomatedNotificationsSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeAttendanceStatusType = table.Column<int>(type: "integer", nullable: false),
                    NotificationMethods = table.Column<int[]>(type: "integer[]", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAutomatedNotificationsSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppCustomAttendanceRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    LateThreshold = table.Column<int>(type: "integer", nullable: false),
                    LateThresholdUnit = table.Column<int>(type: "integer", nullable: false),
                    HalfDayThreshold = table.Column<int>(type: "integer", nullable: false),
                    HalfDayThresholdUnit = table.Column<int>(type: "integer", nullable: false),
                    EnableNotificationForUpperAuthority = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppCustomAttendanceRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppNotificationInfos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiverName = table.Column<string>(type: "text", nullable: false),
                    NotificationTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceiverRole = table.Column<string>(type: "text", nullable: false),
                    NotificationType = table.Column<int>(type: "integer", nullable: false),
                    IsDelivered = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppNotificationInfos", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAutomatedNotificationsSettings");

            migrationBuilder.DropTable(
                name: "AppCustomAttendanceRules");

            migrationBuilder.DropTable(
                name: "AppNotificationInfos");

            migrationBuilder.RenameColumn(
                name: "StatusType",
                table: "AppEmployeeAttendances",
                newName: "Status");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "AppEmployeeLeaves",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<double>(
                name: "TotalHoursWorked",
                table: "AppEmployeeAttendances",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployeeLeaves_UserId",
                table: "AppEmployeeLeaves",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppEmployeeLeaves_AbpUsers_UserId",
                table: "AppEmployeeLeaves",
                column: "UserId",
                principalTable: "AbpUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
