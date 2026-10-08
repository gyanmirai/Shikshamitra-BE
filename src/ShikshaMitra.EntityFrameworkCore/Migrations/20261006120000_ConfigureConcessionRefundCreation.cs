using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20261006120000_ConfigureConcessionRefundCreation")]
public class ConfigureConcessionRefundCreation : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<bool>(name: "AutoCreateRefund", table: "Finance_ConcessionPolicies",
			type: "boolean", nullable: false, defaultValue: false);
		// Preserve existing behavior without changing any historical policy-version JSON.
		migrationBuilder.Sql("UPDATE \"Finance_ConcessionPolicies\" SET \"AutoCreateRefund\" = TRUE WHERE \"ApplyOn\" = 3;");
		migrationBuilder.Sql("ALTER TABLE \"Finance_ConcessionPolicies\" ADD CONSTRAINT \"CK_ConcessionPolicies_RefundPaidScope\" CHECK (NOT \"AutoCreateRefund\" OR \"ApplyOn\" = 3);");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("ALTER TABLE \"Finance_ConcessionPolicies\" DROP CONSTRAINT \"CK_ConcessionPolicies_RefundPaidScope\";");
		migrationBuilder.DropColumn(name: "AutoCreateRefund", table: "Finance_ConcessionPolicies");
	}
}
