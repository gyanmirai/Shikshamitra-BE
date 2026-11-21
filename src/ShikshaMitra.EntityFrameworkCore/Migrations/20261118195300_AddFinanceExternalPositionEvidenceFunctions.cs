using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceExternalPositionEvidenceFunctions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(InstallEvidenceFunctions);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION public."AppFinanceTryJsonMoney"(text, text);
                DROP FUNCTION public."AppFinanceTryJsonScalar"(text, text, text);
                """);
        }
    }
}
