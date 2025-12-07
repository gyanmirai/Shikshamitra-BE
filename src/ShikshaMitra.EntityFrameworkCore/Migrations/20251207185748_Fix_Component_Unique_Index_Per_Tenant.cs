using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class Fix_Component_Unique_Index_Per_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EarningComponents_Name",
                table: "AppEarningComponents");

            migrationBuilder.DropIndex(
                name: "IX_DeductionComponents_Name",
                table: "AppDeductionComponents");

            migrationBuilder.CreateIndex(
                name: "IX_EarningComponents_TenantId_Name",
                table: "AppEarningComponents",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeductionComponents_TenantId_Name",
                table: "AppDeductionComponents",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EarningComponents_TenantId_Name",
                table: "AppEarningComponents");

            migrationBuilder.DropIndex(
                name: "IX_DeductionComponents_TenantId_Name",
                table: "AppDeductionComponents");

            migrationBuilder.CreateIndex(
                name: "IX_EarningComponents_Name",
                table: "AppEarningComponents",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeductionComponents_Name",
                table: "AppDeductionComponents",
                column: "Name",
                unique: true);
        }
    }
}
