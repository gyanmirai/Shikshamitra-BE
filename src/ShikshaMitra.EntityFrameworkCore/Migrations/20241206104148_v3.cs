using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeDocuments_AppEmployees_EmployeeId",
                table: "EmployeeDocuments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EmployeeDocuments",
                table: "EmployeeDocuments");

            migrationBuilder.RenameTable(
                name: "EmployeeDocuments",
                newName: "AppEmployeeDocuments");

            migrationBuilder.RenameIndex(
                name: "IX_EmployeeDocuments_EmployeeId",
                table: "AppEmployeeDocuments",
                newName: "IX_AppEmployeeDocuments_EmployeeId");

            migrationBuilder.AlterColumn<string>(
                name: "EmployeeCode",
                table: "AppEmployees",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppEmployeeDocuments",
                table: "AppEmployeeDocuments",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "AppPayElements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsEmployeeSpecific = table.Column<bool>(type: "boolean", nullable: false),
                    IsDependent = table.Column<bool>(type: "boolean", nullable: false),
                    Dependent_ElementCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Dependent_Percentage = table.Column<float>(type: "real", nullable: true),
                    Dependent_Amount = table.Column<float>(type: "real", nullable: true),
                    IsVisibleInPaySlip = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPayElements", x => x.Id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_AppEmployeeDocuments_AppEmployees_EmployeeId",
                table: "AppEmployeeDocuments",
                column: "EmployeeId",
                principalTable: "AppEmployees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppEmployeeDocuments_AppEmployees_EmployeeId",
                table: "AppEmployeeDocuments");

            migrationBuilder.DropTable(
                name: "AppPayElements");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppEmployeeDocuments",
                table: "AppEmployeeDocuments");

            migrationBuilder.RenameTable(
                name: "AppEmployeeDocuments",
                newName: "EmployeeDocuments");

            migrationBuilder.RenameIndex(
                name: "IX_AppEmployeeDocuments_EmployeeId",
                table: "EmployeeDocuments",
                newName: "IX_EmployeeDocuments_EmployeeId");

            migrationBuilder.AlterColumn<string>(
                name: "EmployeeCode",
                table: "AppEmployees",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EmployeeDocuments",
                table: "EmployeeDocuments",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeDocuments_AppEmployees_EmployeeId",
                table: "EmployeeDocuments",
                column: "EmployeeId",
                principalTable: "AppEmployees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
