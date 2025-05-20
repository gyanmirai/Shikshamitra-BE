using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v27 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AppLibraryTimings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "AppBookCategories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AppBookCategories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppLoanPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LoanDurationInDays = table.Column<int>(type: "integer", nullable: false),
                    MaxBooksAllowed = table.Column<int>(type: "integer", nullable: false),
                    LateReturnFinePerDay = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLoanPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppLoanPolicies_Role",
                table: "AppLoanPolicies",
                column: "Role",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppLoanPolicies");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AppLibraryTimings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AppBookCategories");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "AppBookCategories",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
