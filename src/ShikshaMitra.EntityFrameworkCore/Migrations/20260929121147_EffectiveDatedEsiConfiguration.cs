using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class EffectiveDatedEsiConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EsiConfigurationId",
                table: "HR_SalarySlips",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EsiConfigurationId",
                table: "HR_PayrollRunItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployeeShareDailyWageExemption",
                table: "AppStatutoryRuleVersions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 176m);

            migrationBuilder.CreateTable(
                name: "AppEsiPayrollConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectiveMonth = table.Column<DateTime>(type: "date", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CoveredSince = table.Column<DateTime>(type: "date", nullable: true),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReplacesId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ComponentTreatmentsJson = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_AppEsiPayrollConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppEsiPayrollConfigurations_TenantId_EffectiveMonth",
                table: "AppEsiPayrollConfigurations",
                columns: new[] { "TenantId", "EffectiveMonth" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"IsCancelled\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppEsiPayrollConfigurations_TenantId_RequestFingerprint",
                table: "AppEsiPayrollConfigurations",
                columns: new[] { "TenantId", "RequestFingerprint" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppEsiPayrollConfigurations");

            migrationBuilder.DropColumn(
                name: "EsiConfigurationId",
                table: "HR_SalarySlips");

            migrationBuilder.DropColumn(
                name: "EsiConfigurationId",
                table: "HR_PayrollRunItems");

            migrationBuilder.DropColumn(
                name: "EmployeeShareDailyWageExemption",
                table: "AppStatutoryRuleVersions");
        }
    }
}
