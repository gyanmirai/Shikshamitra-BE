using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddFinanceExternalSourceFingerprintFunctions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(InstallFingerprintFunctions);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION public."AppFinanceTrySourceFingerprintV1"(text);
                DROP FUNCTION public."AppFinanceJsonQuoteV1"(text);
                """);
        }
    }
}
