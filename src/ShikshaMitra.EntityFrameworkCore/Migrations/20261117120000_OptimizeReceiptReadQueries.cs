using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20261117120000_OptimizeReceiptReadQueries")]
public partial class OptimizeReceiptReadQueries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_FeePaymentReceipts_ListOrder",
            table: "Finance_FeePaymentReceipts",
            columns: new[] { "TenantId", "AcademicYearId", "BoardId", "EducationMediumId", "CreationTime", "ReceiptNumber" },
            descending: new[] { false, false, false, false, true, true });

        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
        migrationBuilder.Sql("""
            CREATE INDEX "IX_FeePaymentReceipts_ReceiptNumberTrgm"
            ON "Finance_FeePaymentReceipts" USING gin (lower("ReceiptNumber") gin_trgm_ops);
            """);
        migrationBuilder.Sql("""
            CREATE INDEX "IX_FinanceFeeAccounts_ApplicantNameTrgm"
            ON "Finance_FeeAccounts" USING gin (lower("ApplicantName") gin_trgm_ops);
            """);
        migrationBuilder.Sql("""
            CREATE INDEX "IX_AppStudents_ReceiptSearchNameTrgm"
            ON "AppStudents" USING gin (lower("FirstName" || ' ' || "LastName") gin_trgm_ops);
            """);
        migrationBuilder.Sql("""
            CREATE INDEX "IX_AppStudents_ReceiptSearchFullNameTrgm"
            ON "AppStudents" USING gin (lower("FirstName" || ' ' || coalesce("MiddleName", '') || ' ' || "LastName") gin_trgm_ops);
            """);
        migrationBuilder.Sql("""
            CREATE INDEX "IX_FeePaymentReceipts_ListMonth"
            ON "Finance_FeePaymentReceipts" ("TenantId", "AcademicYearId", "BoardId", "EducationMediumId",
                ((date_part('month', "ReceiptDate"))::integer), "CreationTime" DESC, "ReceiptNumber" DESC);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_FeePaymentReceipts_ListMonth\";");
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_AppStudents_ReceiptSearchFullNameTrgm\";");
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_AppStudents_ReceiptSearchNameTrgm\";");
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_FinanceFeeAccounts_ApplicantNameTrgm\";");
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_FeePaymentReceipts_ReceiptNumberTrgm\";");
        migrationBuilder.DropIndex(
            name: "IX_FeePaymentReceipts_ListOrder",
            table: "Finance_FeePaymentReceipts");
    }
}
