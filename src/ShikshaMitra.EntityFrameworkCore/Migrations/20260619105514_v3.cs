using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdjustmentType",
                table: "Finance_FeeAdjustmentRequests");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "Finance_FeeAdjustmentRequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdjustmentType",
                table: "Finance_FeeAdjustmentRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "Finance_FeeAdjustmentRequests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }
    }
}
