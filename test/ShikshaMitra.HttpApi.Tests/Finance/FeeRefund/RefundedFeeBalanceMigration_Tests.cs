using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.FeeRefunds;

public class RefundedFeeBalanceMigration_Tests
{
	[Theory]
	[InlineData(1000, 300, 0, 300, 1, 0, 2)]
	[InlineData(1000, 1000, 0, 1000, 0, 0, 2)]
	[InlineData(400, 400, 0, 1000, 0, 600, 1)]
	[InlineData(400, 400, 100, 900, 1, 500, 1)]
	[InlineData(1000, 400, 400, 0, 2, 0, 2)]
	[InlineData(0, 0, 0, 1000, 0, 1000, 0)]
	public void Backfill_repairs_only_derived_balances_and_is_idempotent(
		int paid, int refunded, int credit, int oldOutstanding, int oldStatus, int expectedOutstanding, int expectedStatus)
	{
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		using var context = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql("Host=localhost;Database=unused;Username=validation;Password=unused").Options);
		var assembly = context.GetService<IMigrationsAssembly>();
		assembly.Migrations.ContainsKey("20261029120000_PreserveFeeSettlementAfterRefund").ShouldBeTrue();
		var migration = assembly.CreateMigration(assembly.Migrations["20261029120000_PreserveFeeSettlementAfterRefund"], "Npgsql.EntityFrameworkCore.PostgreSQL");
		using var connection = new SqliteConnection("Data Source=:memory:");
		connection.Open();
		using var command = connection.CreateCommand();
		command.CommandText = """
			CREATE TABLE "Finance_DueFees" (
			  "Id" INTEGER PRIMARY KEY, "IsDeleted" BOOLEAN, "OriginalAmount" NUMERIC,
			  "PaidAmount" NUMERIC, "RefundedAmount" NUMERIC, "NetAmount" NUMERIC,
			  "OutstandingAmount" NUMERIC, "Status" INTEGER);
			INSERT INTO "Finance_DueFees" VALUES (1, FALSE, 1000, $paid, $refunded, $credit, $outstanding, $status);
			INSERT INTO "Finance_DueFees" VALUES (2, TRUE, 1000, 1000, 300, 0, 300, 1);
			""";
		command.Parameters.AddWithValue("$paid", paid);
		command.Parameters.AddWithValue("$refunded", refunded);
		command.Parameters.AddWithValue("$credit", credit);
		command.Parameters.AddWithValue("$outstanding", oldOutstanding);
		command.Parameters.AddWithValue("$status", oldStatus);
		command.ExecuteNonQuery();
		command.Parameters.Clear();
		foreach (var operation in migration.UpOperations.Cast<SqlOperation>())
		{
			command.CommandText = operation.Sql;
			command.ExecuteNonQuery();
			command.ExecuteNonQuery().ShouldBe(0);
		}
		command.CommandText = """SELECT "OutstandingAmount", "Status", "PaidAmount", "RefundedAmount", "NetAmount" FROM "Finance_DueFees" WHERE "Id" = 1""";
		using (var reader = command.ExecuteReader())
		{
			reader.Read().ShouldBeTrue();
			reader.GetDecimal(0).ShouldBe(expectedOutstanding);
			reader.GetInt32(1).ShouldBe(expectedStatus);
			reader.GetDecimal(2).ShouldBe(paid);
			reader.GetDecimal(3).ShouldBe(refunded);
			reader.GetDecimal(4).ShouldBe(credit);
		}
		command.CommandText = """SELECT "OutstandingAmount" FROM "Finance_DueFees" WHERE "Id" = 2""";
		command.ExecuteScalar().ShouldBe(300L);
	}
}
