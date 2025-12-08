using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ClassBenchesRows",
                table: "AppRooms",
                newName: "BenchesPerRow");

            migrationBuilder.RenameColumn(
                name: "ClassBenchesColumns",
                table: "AppRooms",
                newName: "BenchesPerColumn");

            // Convert Features from varchar to jsonb with proper type casting
            migrationBuilder.Sql(@"
                ALTER TABLE ""AppRooms"" 
                ALTER COLUMN ""Features"" 
                TYPE jsonb 
                USING CASE 
                    WHEN ""Features"" IS NULL OR ""Features"" = '' THEN NULL
                    ELSE ('""' || ""Features"" || '""')::jsonb
                END;
            ");

            migrationBuilder.CreateTable(
                name: "AppFloors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FloorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TotalRoom = table.Column<int>(type: "integer", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppFloors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppFloors_AppBuildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "AppBuildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppFloors_BuildingId",
                table: "AppFloors",
                column: "BuildingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppFloors");

            migrationBuilder.RenameColumn(
                name: "BenchesPerRow",
                table: "AppRooms",
                newName: "ClassBenchesRows");

            migrationBuilder.RenameColumn(
                name: "BenchesPerColumn",
                table: "AppRooms",
                newName: "ClassBenchesColumns");

            // Revert Features from jsonb to varchar
            migrationBuilder.Sql(@"
                ALTER TABLE ""AppRooms"" 
                ALTER COLUMN ""Features"" 
                TYPE character varying(500) 
                USING ""Features""::text;
            ");
        }
    }
}
