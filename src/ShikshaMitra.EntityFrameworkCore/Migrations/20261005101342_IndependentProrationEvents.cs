using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class IndependentProrationEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ApplyOnAdmission",
                table: "Finance_ProrationPolicyRules",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ApplyOnEarlyWithdrawal",
                table: "Finance_ProrationPolicyRules",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "EarlyWithdrawalBeforeCutoffAction",
                table: "Finance_ProrationPolicyRules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EarlyWithdrawalCutoffDay",
                table: "Finance_ProrationPolicyRules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EarlyWithdrawalMinimumChargeDays",
                table: "Finance_ProrationPolicyRules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EarlyWithdrawalRuleType",
                table: "Finance_ProrationPolicyRules",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplyOnAdmission",
                table: "Finance_ProrationPolicyRules");

            migrationBuilder.DropColumn(
                name: "ApplyOnEarlyWithdrawal",
                table: "Finance_ProrationPolicyRules");

            migrationBuilder.DropColumn(
                name: "EarlyWithdrawalBeforeCutoffAction",
                table: "Finance_ProrationPolicyRules");

            migrationBuilder.DropColumn(
                name: "EarlyWithdrawalCutoffDay",
                table: "Finance_ProrationPolicyRules");

            migrationBuilder.DropColumn(
                name: "EarlyWithdrawalMinimumChargeDays",
                table: "Finance_ProrationPolicyRules");

            migrationBuilder.DropColumn(
                name: "EarlyWithdrawalRuleType",
                table: "Finance_ProrationPolicyRules");
        }
    }
}
