using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarEventDateRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndDate",
                table: "AppEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartDate",
                table: "AppEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "AppEvents"
                SET "StartDate" = "MeetingDate",
                    "EndDate" = "MeetingDate"
                WHERE "MeetingDate" IS NOT NULL
                  AND "StartDate" IS NULL
                  AND "EndDate" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AppEvents_StartDate_EndDate",
                table: "AppEvents",
                columns: new[] { "StartDate", "EndDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppEvents_StartDate_EndDate",
                table: "AppEvents");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "AppEvents");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "AppEvents");
        }
    }
}
