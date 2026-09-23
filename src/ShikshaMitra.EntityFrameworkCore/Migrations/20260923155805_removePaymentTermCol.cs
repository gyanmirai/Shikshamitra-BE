using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class removePaymentTermCol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Finance_RefundPolicies"
                DROP COLUMN IF EXISTS "EligiblePaymentState",
                DROP COLUMN IF EXISTS "RequestDeadlineDays";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Finance_RefundPolicies"
                ADD COLUMN IF NOT EXISTS "EligiblePaymentState" integer NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS "RequestDeadlineDays" integer NULL;
                """);
        }
    }
}
