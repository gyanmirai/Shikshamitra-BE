using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class HostTenantManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstituteName",
                table: "AbpTenants",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AbpTenants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Preserve the existing institute name when moving this extension into a queryable column.
            migrationBuilder.Sql("""
                UPDATE "AbpTenants"
                SET "InstituteName" = "ExtraProperties"::jsonb ->> 'InstituteName';
                """);

            migrationBuilder.CreateTable(
                name: "TenantStatusChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStatus = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantStatusChanges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantStatusChanges_TargetTenantId_CreationTime",
                table: "TenantStatusChanges",
                columns: new[] { "TargetTenantId", "CreationTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "AbpTenants"
                SET "ExtraProperties" = jsonb_set(COALESCE("ExtraProperties"::jsonb, '{}'::jsonb),
                    '{InstituteName}', to_jsonb("InstituteName"), true)::text
                WHERE "InstituteName" IS NOT NULL;
                """);
            migrationBuilder.DropTable(
                name: "TenantStatusChanges");

            migrationBuilder.DropColumn(
                name: "InstituteName",
                table: "AbpTenants");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AbpTenants");
        }
    }
}
