using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class PaymentTermPercentageOnlyAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FixedAmount (1) cannot be auto-converted for reusable terms attached to
            // fee heads of different annual amounts. Fail loudly if any remain.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "Finance_PaymentTermInstallments" WHERE "AllocationType" = 1
                    ) OR EXISTS (
                        SELECT 1 FROM "Finance_FeeStructureDetailScheduleOverrides"
                        WHERE "AllocationType" = 1
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot migrate to percentage-only allocation: FixedAmount rows still exist. Convert or delete them first.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropColumn(
                name: "AllocationType",
                table: "Finance_PaymentTermInstallments");

            migrationBuilder.DropColumn(
                name: "AllocationType",
                table: "Finance_FeeStructureDetailScheduleOverrides");

            migrationBuilder.RenameColumn(
                name: "AllocationValue",
                table: "Finance_PaymentTermInstallments",
                newName: "AllocationPercentage");

            migrationBuilder.RenameColumn(
                name: "AllocationValue",
                table: "Finance_FeeStructureDetailScheduleOverrides",
                newName: "AllocationPercentage");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AllocationPercentage",
                table: "Finance_PaymentTermInstallments",
                newName: "AllocationValue");

            migrationBuilder.RenameColumn(
                name: "AllocationPercentage",
                table: "Finance_FeeStructureDetailScheduleOverrides",
                newName: "AllocationValue");

            migrationBuilder.AddColumn<int>(
                name: "AllocationType",
                table: "Finance_PaymentTermInstallments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AllocationType",
                table: "Finance_FeeStructureDetailScheduleOverrides",
                type: "integer",
                nullable: true);
        }
    }
}
