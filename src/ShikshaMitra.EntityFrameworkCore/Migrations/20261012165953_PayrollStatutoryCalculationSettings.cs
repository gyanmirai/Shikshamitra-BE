using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class PayrollStatutoryCalculationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EsiCalculationDetailsJson",
                table: "HR_PayrollRunItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PfEligibilityDetailsJson",
                table: "HR_PayrollRunItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalculationSettingsJson",
                table: "AppEsiPayrollConfigurations",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.CreateTable(
                name: "AppPfCoverageConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectiveMonth = table.Column<DateTime>(type: "date", nullable: false),
                    IsCovered = table.Column<bool>(type: "boolean", nullable: false),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    RequestKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPfCoverageConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppPfCoverageConfigurations_TenantId_EffectiveMonth",
                table: "AppPfCoverageConfigurations",
                columns: new[] { "TenantId", "EffectiveMonth" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"IsCancelled\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppPfCoverageConfigurations_TenantId_RequestKey",
                table: "AppPfCoverageConfigurations",
                columns: new[] { "TenantId", "RequestKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppPfCoverageConfigurations");

            migrationBuilder.DropColumn(
                name: "EsiCalculationDetailsJson",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "PfEligibilityDetailsJson",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "CalculationSettingsJson",
                table: "AppEsiPayrollConfigurations");
        }
    }
}
