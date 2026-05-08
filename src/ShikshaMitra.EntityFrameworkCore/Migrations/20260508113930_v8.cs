using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "FeeStructureMetadatas");

            migrationBuilder.AddColumn<bool>(
                name: "IsRefundable",
                table: "FeeStructureDetails",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PayableMonth",
                table: "FeeStructureDetails",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayableYear",
                table: "FeeStructureDetails",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRefundable",
                table: "FeeStructureDetails");

            migrationBuilder.DropColumn(
                name: "PayableMonth",
                table: "FeeStructureDetails");

            migrationBuilder.DropColumn(
                name: "PayableYear",
                table: "FeeStructureDetails");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "FeeStructureMetadatas",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
