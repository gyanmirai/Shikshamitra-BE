using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class VariableFeeStructurePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Finance_FeeStructureDetails",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<bool>(
                name: "IsVariable",
                table: "Finance_FeeStructureDetails",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_FeeStructureDetail_Pricing",
                table: "Finance_FeeStructureDetails",
                sql: "(\"IsVariable\" AND \"Amount\" IS NULL) OR (NOT \"IsVariable\" AND \"Amount\" IS NOT NULL AND \"Amount\" >= 0 AND \"Amount\" <= 9999999999.99)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "Finance_FeeStructureDetails" WHERE "IsVariable" OR "Amount" IS NULL) THEN
                        RAISE EXCEPTION 'Variable fee pricing cannot be downgraded after use; use a forward fix.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_FeeStructureDetail_Pricing",
                table: "Finance_FeeStructureDetails");

            migrationBuilder.DropColumn(
                name: "IsVariable",
                table: "Finance_FeeStructureDetails");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Finance_FeeStructureDetails",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);
        }
    }
}
