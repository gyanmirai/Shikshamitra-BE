using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations;

public partial class RemoveFeeStructureAdmissionCollection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CollectAtAdmission",
            table: "Finance_FeeStructureDetails");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "CollectAtAdmission",
            table: "Finance_FeeStructureDetails",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }
}
