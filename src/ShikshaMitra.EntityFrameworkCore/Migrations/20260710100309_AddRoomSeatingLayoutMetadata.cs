using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomSeatingLayoutMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessibleSeatNumbersCsv",
                table: "AppRooms",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AisleAfterColumnsCsv",
                table: "AppRooms",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoardPosition",
                table: "AppRooms",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisabledSeatNumbersCsv",
                table: "AppRooms",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntrancePosition",
                table: "AppRooms",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReservedSeatNumbersCsv",
                table: "AppRooms",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessibleSeatNumbersCsv",
                table: "AppRooms");

            migrationBuilder.DropColumn(
                name: "AisleAfterColumnsCsv",
                table: "AppRooms");

            migrationBuilder.DropColumn(
                name: "BoardPosition",
                table: "AppRooms");

            migrationBuilder.DropColumn(
                name: "DisabledSeatNumbersCsv",
                table: "AppRooms");

            migrationBuilder.DropColumn(
                name: "EntrancePosition",
                table: "AppRooms");

            migrationBuilder.DropColumn(
                name: "ReservedSeatNumbersCsv",
                table: "AppRooms");
        }
    }
}
