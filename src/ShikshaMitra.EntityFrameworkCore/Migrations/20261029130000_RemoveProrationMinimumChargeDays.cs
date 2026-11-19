using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20261029130000_RemoveProrationMinimumChargeDays")]
public class RemoveProrationMinimumChargeDays : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn("MinimumChargeDays", "Finance_ProrationPolicyRules");
		migrationBuilder.DropColumn("EarlyWithdrawalMinimumChargeDays", "Finance_ProrationPolicyRules");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<int>("MinimumChargeDays", "Finance_ProrationPolicyRules", type: "integer", nullable: true);
		migrationBuilder.AddColumn<int>("EarlyWithdrawalMinimumChargeDays", "Finance_ProrationPolicyRules", type: "integer", nullable: true);
	}
}
