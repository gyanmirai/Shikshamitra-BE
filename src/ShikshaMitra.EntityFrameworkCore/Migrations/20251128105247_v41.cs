using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v41 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FormNo",
                table: "AppStudentAdditionalDetails",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppDocumentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaxSizeMB = table.Column<double>(type: "numeric(10,2)", nullable: false),
                    AcceptedFormats = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
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
                    table.PrimaryKey("PK_AppDocumentTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_TenantId_DisplayOrder",
                table: "AppDocumentTypes",
                columns: new[] { "TenantId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_TenantId_Name",
                table: "AppDocumentTypes",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudentAdditionalDetails_AppStudents_Id",
                table: "AppStudentAdditionalDetails",
                column: "Id",
                principalTable: "AppStudents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudentContactDetails_AppStudents_Id",
                table: "AppStudentContactDetails",
                column: "Id",
                principalTable: "AppStudents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudentAdditionalDetails_AppStudents_Id",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudentContactDetails_AppStudents_Id",
                table: "AppStudentContactDetails");

            migrationBuilder.DropTable(
                name: "AppDocumentTypes");

            migrationBuilder.DropColumn(
                name: "FormNo",
                table: "AppStudentAdditionalDetails");
        }
    }
}
