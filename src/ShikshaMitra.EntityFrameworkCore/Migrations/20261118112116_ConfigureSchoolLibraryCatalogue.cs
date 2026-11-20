using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureSchoolLibraryCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Language",
                table: "AppBooks",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                table: "AppBooks",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaterialCode",
                table: "AppBooks",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppLibraryCatalogueConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionJson = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("PK_AppLibraryCatalogueConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCatalogueConfigurations_TenantId",
                table: "AppLibraryCatalogueConfigurations",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
			migrationBuilder.Sql("""
				LOCK TABLE "AppBooks", "AppLibraryCatalogueConfigurations" IN ACCESS EXCLUSIVE MODE;
				DO $catalogue_rollback$
				BEGIN
					IF EXISTS (SELECT 1 FROM "AppLibraryCatalogueConfigurations")
						OR EXISTS (SELECT 1 FROM "AppBooks"
							WHERE "Language" IS NULL OR "LanguageCode" IS NOT NULL OR "MaterialCode" IS NOT NULL) THEN
						RAISE EXCEPTION 'Catalogue rollback refused: school configuration or recorded language/material metadata would be discarded.'
							USING ERRCODE = 'P0001';
					END IF;
				END;
				$catalogue_rollback$;
				""");

            migrationBuilder.DropTable(
                name: "AppLibraryCatalogueConfigurations");

            migrationBuilder.DropColumn(
                name: "LanguageCode",
                table: "AppBooks");

            migrationBuilder.DropColumn(
                name: "MaterialCode",
                table: "AppBooks");

            migrationBuilder.AlterColumn<int>(
                name: "Language",
                table: "AppBooks",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
