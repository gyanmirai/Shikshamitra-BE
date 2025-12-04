using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_AppRooms_AppBuildings_BuildingId",
                table: "AppRooms",
                column: "BuildingId",
                principalTable: "AppBuildings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppRooms_AppRoomTypes_RoomTypeId",
                table: "AppRooms",
                column: "RoomTypeId",
                principalTable: "AppRoomTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppRooms_AppBuildings_BuildingId",
                table: "AppRooms");

            migrationBuilder.DropForeignKey(
                name: "FK_AppRooms_AppRoomTypes_RoomTypeId",
                table: "AppRooms");
        }
    }
}
