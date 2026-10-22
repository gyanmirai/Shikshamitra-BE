using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomColumnLayoutAndFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int[]>(
                name: "ColumnRows",
                table: "academic_Rooms",
                type: "integer[]",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "academic_RoomFeatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_academic_RoomFeatures", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_academic_RoomFeatures_TenantId_NormalizedName",
                table: "academic_RoomFeatures",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.Sql("""
                WITH tenants AS (
                    SELECT "Id" AS tenant_id FROM "AbpTenants" WHERE NOT "IsDeleted"
                    UNION SELECT "TenantId" FROM "academic_Rooms"
                    UNION SELECT NULL::uuid
                ), defaults(name) AS (
                    VALUES ('Air Conditioning'), ('Whiteboard'), ('CCTV'), ('Power Backup'),
                           ('Emergency Exit'), ('Projector'), ('WiFi'), ('Audio System'),
                           ('Wheelchair Access'), ('First Aid Kit')
                ), names AS (
                    SELECT tenant_id, name, 0 AS priority FROM tenants CROSS JOIN defaults
                    UNION ALL
                    SELECT r."TenantId", btrim(feature), 1
                    FROM "academic_Rooms" r
                    CROSS JOIN LATERAL jsonb_array_elements_text(
                        CASE WHEN jsonb_typeof(r."Features") = 'array' THEN r."Features" ELSE '[]'::jsonb END
                    ) AS feature
                    WHERE length(btrim(feature)) BETWEEN 1 AND 100
                ), unique_names AS (
                    SELECT DISTINCT ON (tenant_id, upper(name)) tenant_id, name, upper(name) AS normalized
                    FROM names ORDER BY tenant_id, upper(name), priority, name
                )
                INSERT INTO "academic_RoomFeatures"
                    ("Id", "TenantId", "Name", "NormalizedName", "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted")
                SELECT md5('room-feature:' || coalesce(tenant_id::text, 'host') || ':' || normalized)::uuid,
                       tenant_id, name, normalized, '{}', md5(random()::text), now(), false
                FROM unique_names;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "academic_RoomFeatures");

            migrationBuilder.DropColumn(
                name: "ColumnRows",
                table: "academic_Rooms");
        }
    }
}
