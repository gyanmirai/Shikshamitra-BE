using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionApplicationNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAdmissionApplicationNumberCounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoredTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeKey = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    NextValue = table.Column<long>(type: "bigint", nullable: false),
                    LastRuleVersion = table.Column<int>(type: "integer", nullable: false),
                    LastYearText = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAdmissionApplicationNumberCounters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionApplicationNumberingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoredTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Separator = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    Suffix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    YearFormat = table.Column<int>(type: "integer", nullable: false),
                    ResetPolicy = table.Column<int>(type: "integer", nullable: false),
                    Digits = table.Column<int>(type: "integer", nullable: false),
                    StartingNumber = table.Column<long>(type: "bigint", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAdmissionApplicationNumberingRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionApplicationNumberCounters_StoredTenantId_ScopeK~",
                table: "AppAdmissionApplicationNumberCounters",
                columns: new[] { "StoredTenantId", "ScopeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionApplicationNumberingRules_StoredTenantId_Version",
                table: "AppAdmissionApplicationNumberingRules",
                columns: new[] { "StoredTenantId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAdmissionApplicationNumberCounters");

            migrationBuilder.DropTable(
                name: "AppAdmissionApplicationNumberingRules");
        }
    }
}
