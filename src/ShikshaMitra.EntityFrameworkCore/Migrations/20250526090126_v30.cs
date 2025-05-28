using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v30 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppLoanPolicies_Role",
                table: "AppLoanPolicies");

            migrationBuilder.DropColumn(
                name: "DeleterId",
                table: "AppReturnBooks");

            migrationBuilder.DropColumn(
                name: "DeletionTime",
                table: "AppReturnBooks");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AppReturnBooks");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "AppLoanPolicies");

            migrationBuilder.DropColumn(
                name: "DeleterId",
                table: "AppIssueBooks");

            migrationBuilder.DropColumn(
                name: "DeletionTime",
                table: "AppIssueBooks");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AppIssueBooks");

            migrationBuilder.DropColumn(
                name: "DeleterId",
                table: "AppAddNewBooks");

            migrationBuilder.DropColumn(
                name: "DeletionTime",
                table: "AppAddNewBooks");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AppAddNewBooks");

            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                table: "AppLoanPolicies",
                type: "uuid",
                maxLength: 64,
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "AppBookOverviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ISBN = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Borrowed = table.Column<int>(type: "integer", nullable: false),
                    Overdue = table.Column<int>(type: "integer", nullable: false),
                    Reserved = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBookOverviews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppBookOverviews_ISBN",
                table: "AppBookOverviews",
                column: "ISBN");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookOverviews_TenantId",
                table: "AppBookOverviews",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppBookOverviews");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "AppLoanPolicies");

            migrationBuilder.AddColumn<Guid>(
                name: "DeleterId",
                table: "AppReturnBooks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionTime",
                table: "AppReturnBooks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AppReturnBooks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "AppLoanPolicies",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "DeleterId",
                table: "AppIssueBooks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionTime",
                table: "AppIssueBooks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AppIssueBooks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "DeleterId",
                table: "AppAddNewBooks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionTime",
                table: "AppAddNewBooks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AppAddNewBooks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_AppLoanPolicies_Role",
                table: "AppLoanPolicies",
                column: "Role",
                unique: true);
        }
    }
}
