using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CustomComponentId",
                table: "AppEarningComponents",
                newName: "SelectedEarningComponentId");

            migrationBuilder.RenameColumn(
                name: "CustomComponentId",
                table: "AppDeductionComponents",
                newName: "SelectedDeductionComponentId");

            migrationBuilder.Sql(
                "ALTER TABLE \"AppEarningComponents\" ALTER COLUMN \"Amount\" DROP NOT NULL;");
            migrationBuilder.Sql(
                "ALTER TABLE \"AppEarningComponents\" ALTER COLUMN \"Amount\" DROP DEFAULT;");
            migrationBuilder.Sql(
                "ALTER TABLE \"AppEarningComponents\" ALTER COLUMN \"Amount\" TYPE numeric(18,2) USING \"Amount\"::numeric(18,2);");

            migrationBuilder.AddColumn<int>(
                name: "CalculationType",
                table: "AppEarningComponents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Percentage",
                table: "AppEarningComponents",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.Sql(
                "ALTER TABLE \"AppDeductionComponents\" ALTER COLUMN \"Amount\" DROP NOT NULL;");
            migrationBuilder.Sql(
                "ALTER TABLE \"AppDeductionComponents\" ALTER COLUMN \"Amount\" DROP DEFAULT;");
            migrationBuilder.Sql(
                "ALTER TABLE \"AppDeductionComponents\" ALTER COLUMN \"Amount\" TYPE numeric(18,2) USING \"Amount\"::numeric(18,2);");

            migrationBuilder.AddColumn<int>(
                name: "CalculationType",
                table: "AppDeductionComponents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Percentage",
                table: "AppDeductionComponents",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppDayCalculationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    DayCalculationType = table.Column<int>(type: "integer", nullable: false),
                    ManualDays = table.Column<int>(type: "integer", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDayCalculationSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DayCalculationSettings_TenantId",
                table: "AppDayCalculationSettings",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppDayCalculationSettings");

            migrationBuilder.DropColumn(
                name: "CalculationType",
                table: "AppEarningComponents");

            migrationBuilder.DropColumn(
                name: "Percentage",
                table: "AppEarningComponents");

            migrationBuilder.DropColumn(
                name: "CalculationType",
                table: "AppDeductionComponents");

            migrationBuilder.DropColumn(
                name: "Percentage",
                table: "AppDeductionComponents");

            migrationBuilder.RenameColumn(
                name: "SelectedEarningComponentId",
                table: "AppEarningComponents",
                newName: "CustomComponentId");

            migrationBuilder.RenameColumn(
                name: "SelectedDeductionComponentId",
                table: "AppDeductionComponents",
                newName: "CustomComponentId");

            migrationBuilder.Sql(
                "ALTER TABLE \"AppEarningComponents\" ALTER COLUMN \"Amount\" TYPE character varying(20) USING \"Amount\"::character varying(20);");
            migrationBuilder.Sql(
                "ALTER TABLE \"AppEarningComponents\" ALTER COLUMN \"Amount\" SET NOT NULL;");
            migrationBuilder.Sql(
                "ALTER TABLE \"AppEarningComponents\" ALTER COLUMN \"Amount\" SET DEFAULT '';");

            migrationBuilder.Sql(
                "ALTER TABLE \"AppDeductionComponents\" ALTER COLUMN \"Amount\" TYPE character varying(20) USING \"Amount\"::character varying(20);");
            migrationBuilder.Sql(
                "ALTER TABLE \"AppDeductionComponents\" ALTER COLUMN \"Amount\" SET NOT NULL;");
            migrationBuilder.Sql(
                "ALTER TABLE \"AppDeductionComponents\" ALTER COLUMN \"Amount\" SET DEFAULT '';");
        }
    }
}
