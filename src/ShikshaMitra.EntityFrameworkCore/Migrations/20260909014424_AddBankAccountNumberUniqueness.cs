using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddBankAccountNumberUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Finance_BankAccounts"
                        WHERE NOT "IsDeleted"
                        GROUP BY "TenantId", "AcademicYearId", "AccountNumber"
                        HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Duplicate bank account numbers exist for the same tenant and academic year. Merge or retire duplicates before applying this migration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_BankAccounts_TenantId_AcademicYearId_AccountNumber",
                table: "Finance_BankAccounts",
                columns: new[] { "TenantId", "AcademicYearId", "AccountNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Finance_BankAccounts_TenantId_AcademicYearId_AccountNumber",
                table: "Finance_BankAccounts");
        }
    }
}
