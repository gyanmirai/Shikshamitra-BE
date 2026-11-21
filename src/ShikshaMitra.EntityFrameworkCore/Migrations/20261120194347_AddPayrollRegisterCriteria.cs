using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollRegisterCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "RegisterIssueCodes",
                table: "HR_PayrollRunItems",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<decimal>(
                name: "NetPay",
                table: "HR_PayrollPreviewEmployees",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetPayDelta",
                table: "HR_PayrollPreviewEmployees",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PayableDays",
                table: "HR_PayrollPreviewEmployees",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "RegisterIssueCodes",
                table: "HR_PayrollPreviewEmployees",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.Sql("""
                WITH rows AS (
                    SELECT "Id", "ItemJson"::jsonb AS item
                    FROM "HR_PayrollPreviewEmployees"
                )
                UPDATE "HR_PayrollPreviewEmployees" AS employee
                SET "NetPay" = CASE WHEN jsonb_typeof(rows.item -> 'NetPay') = 'number' THEN (rows.item ->> 'NetPay')::numeric END,
                    "NetPayDelta" = CASE WHEN jsonb_typeof(rows.item -> 'NetPayDelta') = 'number' THEN (rows.item ->> 'NetPayDelta')::numeric END,
                    "PayableDays" = CASE WHEN jsonb_typeof(rows.item -> 'PayableDays') = 'number' THEN (rows.item ->> 'PayableDays')::numeric END,
                    "RegisterIssueCodes" = ARRAY(
                        SELECT jsonb_array_elements_text(
                            CASE WHEN jsonb_typeof(rows.item -> 'IssueCodes') = 'array' THEN rows.item -> 'IssueCodes' ELSE '[]'::jsonb END
                        )
                    )
                FROM rows
                WHERE employee."Id" = rows."Id";

                UPDATE "HR_PayrollRunItems"
                SET "RegisterIssueCodes" = ARRAY(
                    SELECT jsonb_array_elements_text(
                        CASE WHEN jsonb_typeof("IssueCodesJson") = 'array' THEN "IssueCodesJson" ELSE '[]'::jsonb END
                    )
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollPreviewEmployees_RunId_NetPay_EmployeeId",
                table: "HR_PayrollPreviewEmployees",
                columns: new[] { "RunId", "NetPay", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollPreviewEmployees_RunId_NetPayDelta_EmployeeId",
                table: "HR_PayrollPreviewEmployees",
                columns: new[] { "RunId", "NetPayDelta", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_HR_PayrollPreviewEmployees_RunId_PayableDays_EmployeeId",
                table: "HR_PayrollPreviewEmployees",
                columns: new[] { "RunId", "PayableDays", "EmployeeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HR_PayrollPreviewEmployees_RunId_NetPay_EmployeeId",
                table: "HR_PayrollPreviewEmployees");

            migrationBuilder.DropIndex(
                name: "IX_HR_PayrollPreviewEmployees_RunId_NetPayDelta_EmployeeId",
                table: "HR_PayrollPreviewEmployees");

            migrationBuilder.DropIndex(
                name: "IX_HR_PayrollPreviewEmployees_RunId_PayableDays_EmployeeId",
                table: "HR_PayrollPreviewEmployees");

            migrationBuilder.DropColumn(
                name: "RegisterIssueCodes",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "NetPay",
                table: "HR_PayrollPreviewEmployees");

            migrationBuilder.DropColumn(
                name: "NetPayDelta",
                table: "HR_PayrollPreviewEmployees");

            migrationBuilder.DropColumn(
                name: "PayableDays",
                table: "HR_PayrollPreviewEmployees");

            migrationBuilder.DropColumn(
                name: "RegisterIssueCodes",
                table: "HR_PayrollPreviewEmployees");
        }
    }
}
