using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCollectionPolicyGracePeriodType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GracePeriodType",
                table: "Finance_CollectionPolicies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GracePeriodType",
                table: "Finance_CollectionPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
