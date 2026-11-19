using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ShikshaMitra.Migrations;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

public class CashManagementMigration_Tests
{
	private static readonly string[] CashTables =
	[
		"Finance_CashCollections", "Finance_CashCommands", "Finance_CashDepositCredits",
		"Finance_CashDeposits", "Finance_CashExpenses"
	];

	[Fact]
	public void Upgrade_should_only_add_cash_schema_without_replaying_or_mutating_existing_financial_history()
	{
		var operations = new AddSimpleCashManagement().UpOperations;
		// An allow-list also rejects SQL/data backfill, rename, alter and drop operations.
		operations.All(operation => operation is CreateTableOperation or AddColumnOperation
			or CreateIndexOperation or AddForeignKeyOperation).ShouldBeTrue();
		operations.All(operation => !operation.IsDestructiveChange).ShouldBeTrue();
		var tables = operations.OfType<CreateTableOperation>().ToArray();
		tables.Select(table => table.Name).OrderBy(name => name).ToArray().ShouldBe(CashTables.OrderBy(name => name).ToArray());
		var link = operations.OfType<AddColumnOperation>().Single();
		link.Table.ShouldBe("Finance_Transactions");
		link.Name.ShouldBe("CashCollectionId");
		link.ClrType.ShouldBe(typeof(Guid));
		link.IsNullable.ShouldBeTrue();
		link.DefaultValue.ShouldBeNull();
		link.DefaultValueSql.ShouldBeNull();
		operations.OfType<CreateIndexOperation>().All(index => CashTables.Contains(index.Table)
			|| (index.Table == "Finance_Transactions" && index.Columns.SequenceEqual(["CashCollectionId"]))).ShouldBeTrue();
		var sourceIndex = operations.OfType<CreateIndexOperation>().Single(index => index.Table == "Finance_CashCollections"
			&& index.Columns.SequenceEqual(["TenantId", "IncomeSourceKeyPrefix"]));
		sourceIndex.IsUnique.ShouldBeTrue();
		var transactionLink = operations.OfType<AddForeignKeyOperation>().Single();
		transactionLink.Table.ShouldBe("Finance_Transactions");
		transactionLink.Columns.ShouldBe(["CashCollectionId"]);
		transactionLink.PrincipalTable.ShouldBe("Finance_CashCollections");
		transactionLink.OnDelete.ShouldBe(ReferentialAction.Restrict);
		var collection = tables.Single(table => table.Name == "Finance_CashCollections");
		collection.ForeignKeys.Count.ShouldBe(2);
		collection.ForeignKeys.SelectMany(key => key.Columns).OrderBy(name => name).ToArray()
			.ShouldBe(["BankAccountId", "FinanceFeeAccountId"]);
		collection.ForeignKeys.All(key => key.OnDelete == ReferentialAction.Restrict).ShouldBeTrue();
		var money = tables.SelectMany(table => table.Columns).Where(column => column.ClrType == typeof(decimal)).ToArray();
		money.Length.ShouldBe(8);
		foreach (var column in money)
		{
			column.ColumnType.ShouldBe("numeric(18,2)");
			column.Precision.ShouldBe(18);
			column.Scale.ShouldBe(2);
		}
		var actualDates = tables.SelectMany(table => table.Columns).Where(column => column.ClrType == typeof(DateTimeOffset)).ToArray();
		actualDates.Length.ShouldBe(6);
		actualDates.All(column => column.ColumnType == "timestamp with time zone").ShouldBeTrue();
	}
}
