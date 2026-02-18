using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppLibraryTimings_Day",
                table: "AppLibraryTimings");

            migrationBuilder.DropColumn(
                name: "ClosingTime",
                table: "AppLibraryTimings");

            migrationBuilder.DropColumn(
                name: "OpeningTime",
                table: "AppLibraryTimings");

            migrationBuilder.AlterColumn<string>(
                name: "Day",
                table: "AppLibraryTimings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "AppLibraryTimings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndTime",
                table: "AppLibraryTimings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StartTime",
                table: "AppLibraryTimings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "AppLibraryTimings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AppLibraryTimings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "AppBookCategories",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "AppBookCategories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AppBookCategories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppBorrowingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    MaxBooksAtTime = table.Column<int>(type: "integer", nullable: false),
                    ReservationLimit = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBorrowingRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryChargeCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargeType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsFixed = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryChargeCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppLibrarySections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: true),
                    SectionName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NumberOfShelf = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibrarySections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryShelves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShelfName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryShelves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLibraryShelves_AppLibrarySections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "AppLibrarySections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppBorrowingRules_AcademicYearId_Role",
                table: "AppBorrowingRules",
                columns: new[] { "AcademicYearId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryShelves_SectionId",
                table: "AppLibraryShelves",
                column: "SectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppBorrowingRules");

            migrationBuilder.DropTable(
                name: "AppLibraryChargeCategories");

            migrationBuilder.DropTable(
                name: "AppLibraryShelves");

            migrationBuilder.DropTable(
                name: "AppLibrarySections");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "AppLibraryTimings");

            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "AppLibraryTimings");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "AppLibraryTimings");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AppLibraryTimings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AppLibraryTimings");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "AppBookCategories");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AppBookCategories");

            migrationBuilder.AlterColumn<int>(
                name: "Day",
                table: "AppLibraryTimings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "ClosingTime",
                table: "AppLibraryTimings",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "OpeningTime",
                table: "AppLibraryTimings",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "AppBookCategories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryTimings_Day",
                table: "AppLibraryTimings",
                column: "Day",
                unique: true);
        }
    }
}
