using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ShikshaMitra.Migrations;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.HR;

public class LeavePolicyDeploymentModel_Tests
{
	[Fact]
	[Trait("Category", "OfflineMigrationModel")]
	public void Leave_target_preserves_current_cash_schema_and_explicit_zero_configuration()
	{
		// Metadata only: no host, database connection, migration execution or credentials.
		var migration = new AddLeavePolicyVersion();
		var model = migration.TargetModel;
		var leave = model.GetEntityTypes().Single(x => x.GetTableName() == "AppEmployeeLeavePolicies");
		var version = leave.FindProperty("Version")!;
		version.IsConcurrencyToken.ShouldBeTrue();
		version.GetDefaultValue().ShouldBe(1);
		var rule = model.GetEntityTypes().Single(x => x.GetTableName() == "AppEmployeeLeavePolicyRules");
		rule.FindProperty("MinNoticeDays")!.ValueGenerated.ShouldBe(ValueGenerated.Never);
		var cashTables = new[] { "Finance_CashCollections", "Finance_CashCommands", "Finance_CashDepositCredits", "Finance_CashDeposits", "Finance_CashExpenses" };
		foreach (var table in cashTables) model.GetEntityTypes().ShouldContain(x => x.GetTableName() == table);
		var column = migration.UpOperations.ShouldHaveSingleItem().ShouldBeOfType<AddColumnOperation>();
		column.Table.ShouldBe("AppEmployeeLeavePolicies"); column.Name.ShouldBe("Version");
		column.DefaultValue.ShouldBe(1); column.IsNullable.ShouldBeFalse();
	}
}
