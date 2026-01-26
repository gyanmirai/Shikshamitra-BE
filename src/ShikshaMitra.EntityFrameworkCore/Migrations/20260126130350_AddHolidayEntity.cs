using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddHolidayEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppHolidays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true, comment: "Tenant ID for multi-tenancy"),
                    HolidayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false, comment: "Name of the holiday"),
                    HolidayTypeId = table.Column<Guid>(type: "uuid", nullable: false, comment: "Reference to EventType"),
                    StartDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Start date of the holiday"),
                    EndDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "End date of the holiday"),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, comment: "Optional description"),
                    AudienceIds = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, comment: "Comma-separated audience IDs"),
                    Attachments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true, comment: "Comma-separated attachment file paths"),
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
                    table.PrimaryKey("PK_AppHolidays", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "AppHolidays_HolidayNameTenant",
                table: "AppHolidays",
                columns: new[] { "HolidayName", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "AppHolidays_TypeId",
                table: "AppHolidays",
                column: "HolidayTypeId");

            migrationBuilder.CreateIndex(
                name: "AppHolidays_DateRange",
                table: "AppHolidays",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "AppHolidays_Tenant",
                table: "AppHolidays",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppHolidays");
        }
    }
}
