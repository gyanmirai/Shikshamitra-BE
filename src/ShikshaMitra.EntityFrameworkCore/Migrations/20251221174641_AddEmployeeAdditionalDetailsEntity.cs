using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeAdditionalDetailsEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdditionalDetails_BplCardNo",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "AdditionalDetails_LibraryMember",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "AdditionalDetails_PersonWithDisability",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "AdditionalDetails_SecondLanguage",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "AdditionalDetails_TransportMember",
                table: "AppEmployees");

            migrationBuilder.AddColumn<Guid>(
                name: "ReportingManagerId",
                table: "AppEmployees",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppEmployeeAdditionalDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    BplCardNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PersonWithDisability = table.Column<bool>(type: "boolean", nullable: false),
                    SecondLanguage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LibraryMember = table.Column<bool>(type: "boolean", nullable: false),
                    TransportMember = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppEmployeeAdditionalDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppEmployeeAdditionalDetails_AppEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "AppEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ReportingManagerId",
                table: "AppEmployees",
                column: "ReportingManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAdditionalDetails_EmployeeId_Unique",
                table: "AppEmployeeAdditionalDetails",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAdditionalDetails_TenantId",
                table: "AppEmployeeAdditionalDetails",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppEmployees_AppEmployees_ReportingManagerId",
                table: "AppEmployees",
                column: "ReportingManagerId",
                principalTable: "AppEmployees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppEmployees_AppEmployees_ReportingManagerId",
                table: "AppEmployees");

            migrationBuilder.DropTable(
                name: "AppEmployeeAdditionalDetails");

            migrationBuilder.DropIndex(
                name: "IX_Employees_ReportingManagerId",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "ReportingManagerId",
                table: "AppEmployees");

            migrationBuilder.AddColumn<string>(
                name: "AdditionalDetails_BplCardNo",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AdditionalDetails_LibraryMember",
                table: "AppEmployees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AdditionalDetails_PersonWithDisability",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdditionalDetails_SecondLanguage",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AdditionalDetails_TransportMember",
                table: "AppEmployees",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
