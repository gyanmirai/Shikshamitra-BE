using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSubstitute;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Accounts.BankActivity;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;
namespace ShikshaMitra.Finance.BankActivity;
public sealed class BankActivityPostgresFactAttribute : FactAttribute
{
	public BankActivityPostgresFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BANK_ACTIVITY_TEST_POSTGRES"))) Skip = "Requires dedicated loopback PostgreSQL bank-activity cluster on port 55443."; }
}
public class BankActivityPostgres_Tests
{
	[BankActivityPostgresFact]
	public async Task Canonical_projection_preserves_instrument_grain_signed_corrections_dates_and_historic_bank()
	{
		await using var connection = Connection(); await connection.OpenAsync();
		await using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection).Options);
		await Schema(db);
		var tenant = Guid.NewGuid(); var year = Guid.NewGuid(); var bank = Guid.NewGuid(); var otherBank = Guid.NewGuid(); var student = Guid.NewGuid(); var source = Guid.NewGuid(); var cash = Guid.NewGuid(); var deposit = Guid.NewGuid(); var later = Guid.NewGuid(); var category = Guid.NewGuid();
		async Task Tx(string key, decimal amount, int method, int type, bool confirmed, Guid? bankId = null, Guid? owner = null) =>
		 await db.Database.ExecuteSqlInterpolatedAsync($$"""
INSERT INTO "Finance_Transactions" ("Id","TenantId","AcademicYearId","BankAccountId","CategoryId","Name","SourceKey","SourceId","StudentId","Amount","PaymentMethod","TransactionType","IsConfirmed","PaymentDate","ExtraProperties")
VALUES ({{Guid.NewGuid()}},{{tenant}},{{year}},{{bankId ?? bank}},{{category}},'REF',{{key}},{{source}},{{owner ?? student}},{{amount}},{{method}},{{type}},{{confirmed}},{{new DateTimeOffset(2025, 1, 10, 0, 0, 0, TimeSpan.Zero)}},'{}')
""");
		await Tx($"collection:{source:N}:0:0", 3000.01m, 3, 0, true);
		await Tx($"collection:{source:N}:0:1", 999.99m, 3, 0, true);
		await Tx($"collection:{source:N}:1:0", 2000m, 6, 0, true, otherBank);
		await Tx($"collection:{source:N}:0:0:return", -3000.01m, 3, 0, true);
		await Tx($"collection:{source:N}:0:1:return", -999.99m, 3, 0, true);
		await Tx($"collection:{source:N}:2:0", 500.02m, 1, 0, false);
		await Tx($"collection:{source:N}:3:0", 8000m, 0, 0, true); // cash Income is explanatory only
		await Tx($"refund:{Guid.NewGuid():N}", 200m, 0, 1, true); // bank tag is not bank-funded cash evidence
		await Tx($"refund:{Guid.NewGuid():N}", -20.03m, 3, 1, true);
		var evidence = System.Text.Json.JsonSerializer.Serialize(new System.Collections.Generic.Dictionary<string, object> { ["DepositBank:" + deposit.ToString("N")] = System.Text.Json.JsonSerializer.Serialize(new { Id = bank, Label = "Old bank" }), ["DepositBank:" + later.ToString("N")] = System.Text.Json.JsonSerializer.Serialize(new { Id = otherBank, Label = "New bank" }) });
		await db.Database.ExecuteSqlInterpolatedAsync($$"""
INSERT INTO "Finance_CashCollections" ("Id","TenantId","AcademicYearId","SourceId","StudentId","CollectedAt","PayerName","BankAccountId","ExtraProperties") VALUES ({{cash}},{{tenant}},{{year}},{{source}},{{student}},{{new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)}},'Visible payer',{{otherBank}},{{evidence}});
INSERT INTO "Finance_CashDeposits" ("Id","CashCollectionId","Amount","CreditedAmount","DepositDate","Reference") VALUES ({{deposit}},{{cash}},8000.01,7000.01,{{new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero)}},'D1'),({{later}},{{cash}},500,500,{{new DateTimeOffset(2025, 4, 1, 0, 0, 0, TimeSpan.Zero)}},'D2');
INSERT INTO "Finance_CashDepositCredits" ("Id","CashDepositId","Amount","CreditDate","Reference") VALUES ({{Guid.NewGuid()}},{{deposit}},6000.01,{{new DateTimeOffset(2025, 3, 2, 0, 0, 0, TimeSpan.Zero)}},'BANK-CREDIT-SECRET-1234'),({{Guid.NewGuid()}},{{deposit}},1000,{{new DateTimeOffset(2025, 3, 3, 0, 0, 0, TimeSpan.Zero)}},'C2'),({{Guid.NewGuid()}},{{later}},500,{{new DateTimeOffset(2025, 4, 2, 0, 0, 0, TimeSpan.Zero)}},'C3')
""");
		var provider = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>(); provider.GetDbContextAsync().Returns(db);
		var facts = await new EfCoreBankActivityQuerySource(provider).QueryAsync(tenant, year, bank);
		var activity = facts.Where(x => x.Panel == "activity");
		var rows = await activity.ToListAsync();
		rows.Single(x => x.Key == $"collection:{source:N}:0").Incoming.ShouldBe(4000m);
		rows.Single(x => x.Key == $"collection:{source:N}:0:return").Outgoing.ShouldBe(4000m);
		rows.Single(x => x.Source == "refund" && x.PaymentMethod == 0).RecognitionStatus.ShouldBe((int) BankActivityRecognitionStatus.Unverified);
		rows.Single(x => x.Source == "refund" && x.PaymentMethod == 0).Outgoing.ShouldBe(0m);
		rows.Where(x => x.Source == "cash-deposit").Sum(x => x.Incoming).ShouldBe(7000.01m);
		var maskedCredit = rows.Single(x => x.Incoming == 6000.01m);
		maskedCredit.Reference.ShouldEndWith("1234"); maskedCredit.Reference.ShouldNotContain("SECRET");
		maskedCredit.PaymentReference.ShouldBe(maskedCredit.Reference);
		(await activity.CountAsync(x => x.Reference.Contains("BANK-CREDIT-SECRET") || x.PaymentReference!.Contains("BANK-CREDIT-SECRET"))).ShouldBe(0);
		rows.Where(x => x.BankConfirmationStatus == 1).Min(x => x.OccurredAt).ShouldBe(new DateTimeOffset(2025, 3, 2, 0, 0, 0, TimeSpan.Zero));
		var summary = await BankActivityQueryRules.Aggregates(activity).SingleAsync(); summary.Net.ShouldBe(7020.04m); summary.PendingIncoming.ShouldBe(500.02m);
		var onlyAfterCredit = await activity.Where(x => x.OccurredAt >= new DateTimeOffset(2025, 3, 3, 0, 0, 0, TimeSpan.Zero) && x.OccurredAt < new DateTimeOffset(2025, 3, 4, 0, 0, 0, TimeSpan.Zero)).ToListAsync(); onlyAfterCredit.ShouldHaveSingleItem().Incoming.ShouldBe(1000m);
		var offsetQuery = new BankActivityQuery { AcademicYearId = year, From = new DateTimeOffset(2025, 3, 3, 5, 30, 0, TimeSpan.FromMinutes(330)), ToExclusive = new DateTimeOffset(2025, 3, 4, 5, 30, 0, TimeSpan.FromMinutes(330)) };
		(await BankActivityQueryRules.Filter(facts, offsetQuery, BankActivityQueryRules.Validate(offsetQuery)).SingleAsync()).Incoming.ShouldBe(1000m);
		(await (await new EfCoreBankActivityQuerySource(provider).QueryAsync(Guid.NewGuid(), year, bank)).CountAsync()).ShouldBe(0);
		(await (await new EfCoreBankActivityQuerySource(provider).QueryAsync(tenant, Guid.NewGuid(), bank)).CountAsync()).ShouldBe(0);
		var table = BankActivityQueryRules.Validate(new() { AcademicYearId = year, SortBy = "incoming", SortOrder = "desc" });
		(await BankActivityQueryRules.Order(activity, table).Take(1).SingleAsync()).Incoming.ShouldBe(6000.01m);
	}
	[BankActivityPostgresFact]
	public async Task Bulk_only_evidence_is_resolved_and_unmapped_header_bank_is_never_used()
	{
		await using var connection = Connection(); await connection.OpenAsync(); await using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection).Options); await Schema(db);
		var tenant = Guid.NewGuid(); var year = Guid.NewGuid(); var bank = Guid.NewGuid(); var cash = Guid.NewGuid(); var deposit = Guid.NewGuid(); var unassigned = Guid.NewGuid();
		var evidence = System.Text.Json.JsonSerializer.Serialize(new System.Collections.Generic.Dictionary<string, object> { ["BulkDeposit:" + Guid.NewGuid().ToString("N")] = System.Text.Json.JsonSerializer.Serialize(new { depositId = deposit, bankAccountId = bank, amount = 100.01m }) });
		await db.Database.ExecuteSqlInterpolatedAsync($$"""
INSERT INTO "Finance_CashCollections" ("Id","TenantId","AcademicYearId","SourceId","CollectedAt","PayerName","BankAccountId","ExtraProperties") VALUES ({{cash}},{{tenant}},{{year}},{{Guid.NewGuid()}},{{DateTimeOffset.UtcNow}},'Payer',{{bank}},{{evidence}});
INSERT INTO "Finance_CashDeposits" ("Id","CashCollectionId","Amount","CreditedAmount","DepositDate","Reference") VALUES ({{deposit}},{{cash}},100.01,0,{{DateTimeOffset.UtcNow}},'mapped'),({{unassigned}},{{cash}},999999,0,{{DateTimeOffset.UtcNow}},'unmapped')
""");
		var provider = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>(); provider.GetDbContextAsync().Returns(db);
		var facts = await new EfCoreBankActivityQuerySource(provider).QueryAsync(tenant, year, bank);
		var rows = await facts.Where(x => x.Panel == "deposits").ToListAsync(); rows.ShouldHaveSingleItem().DepositAmount.ShouldBe(100.01m);
		var emptyStudents = db.Database.SqlQuery<Guid>($"SELECT {Guid.NewGuid()}::uuid AS \"Value\" WHERE false");
		var scoped = BankActivityQueryRules.Scope(facts, emptyStudents, emptyStudents, false); (await scoped.CountAsync()).ShouldBe(0);
	}
	[BankActivityPostgresFact]
	public async Task Childless_legacy_credit_keeps_its_actual_date_in_activity_and_deposit_panel()
	{
		await using var connection = Connection(); await connection.OpenAsync(); await using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection).Options); await Schema(db);
		var tenant = Guid.NewGuid(); var year = Guid.NewGuid(); var bank = Guid.NewGuid(); var cash = Guid.NewGuid(); var deposit = Guid.NewGuid(); var creditedAt = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
		var evidence = System.Text.Json.JsonSerializer.Serialize(new System.Collections.Generic.Dictionary<string, object> { ["DepositBank:" + deposit.ToString("N")] = System.Text.Json.JsonSerializer.Serialize(new { Id = bank }) });
		await db.Database.ExecuteSqlInterpolatedAsync($$"""
INSERT INTO "Finance_CashCollections" ("Id","TenantId","AcademicYearId","SourceId","CollectedAt","PayerName","ExtraProperties") VALUES ({{cash}},{{tenant}},{{year}},{{Guid.NewGuid()}},{{new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)}},'Legacy',{{evidence}});
INSERT INTO "Finance_CashDeposits" ("Id","CashCollectionId","Amount","CreditedAmount","DepositDate","CreditDate","Reference") VALUES ({{deposit}},{{cash}},55.67,55.67,{{new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero)}},{{creditedAt}},'Legacy')
""");
		var provider = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>(); provider.GetDbContextAsync().Returns(db);
		var facts = await new EfCoreBankActivityQuerySource(provider).QueryAsync(tenant, year, bank);
		var movement = await facts.SingleAsync(x => x.Panel == "activity"); movement.Incoming.ShouldBe(55.67m); movement.OccurredAt.ShouldBe(creditedAt);
		(await facts.SingleAsync(x => x.Panel == "deposits")).BankCreditAt.ShouldBe(creditedAt);
	}
	[BankActivityPostgresFact]
	public async Task Mixed_batch_scope_filters_before_sums_and_account_scope_takes_precedence_over_student()
	{
		await using var connection = Connection(); await connection.OpenAsync(); await using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection).Options); await Schema(db);
		var tenant = Guid.NewGuid(); var year = Guid.NewGuid(); var bank = Guid.NewGuid(); var student = Guid.NewGuid(); var hiddenStudent = Guid.NewGuid(); var hiddenAccount = Guid.NewGuid(); var batch = Guid.NewGuid();
		async Task Add(Guid payer, Guid? account, decimal amount)
		{
			var cash = Guid.NewGuid(); var deposit = Guid.NewGuid();
			var evidence = System.Text.Json.JsonSerializer.Serialize(new System.Collections.Generic.Dictionary<string, object> { ["BulkDeposit:" + batch.ToString("N")] = System.Text.Json.JsonSerializer.Serialize(new { depositId = deposit, bankAccountId = bank, amount }) });
			await db.Database.ExecuteSqlInterpolatedAsync($$"""
INSERT INTO "Finance_CashCollections" ("Id","TenantId","AcademicYearId","SourceId","StudentId","FinanceFeeAccountId","CollectedAt","PayerName","ExtraProperties") VALUES ({{cash}},{{tenant}},{{year}},{{Guid.NewGuid()}},{{payer}},{{account}},{{new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)}},'Payer',{{evidence}});
INSERT INTO "Finance_CashDeposits" ("Id","CashCollectionId","Amount","CreditedAmount","DepositDate","Reference") VALUES ({{deposit}},{{cash}},{{amount}},{{amount}},{{new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)}},'Mixed batch');
INSERT INTO "Finance_CashDepositCredits" ("Id","CashDepositId","Amount","CreditDate","Reference") VALUES ({{Guid.NewGuid()}},{{deposit}},{{amount}},{{new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero)}},'Mixed batch')
""");
		}
		await Add(student, null, 100.01m); await Add(hiddenStudent, null, 900m); await Add(student, hiddenAccount, 500m);
		var provider = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>(); provider.GetDbContextAsync().Returns(db);
		var facts = await new EfCoreBankActivityQuerySource(provider).QueryAsync(tenant, year, bank);
		var students = db.Database.SqlQuery<Guid>($"SELECT {student}::uuid AS \"Value\"");
		var noAccounts = db.Database.SqlQuery<Guid>($"SELECT {Guid.Empty}::uuid AS \"Value\" WHERE false");
		var scoped = BankActivityQueryRules.Scope(facts, students, noAccounts, false);
		(await scoped.CountAsync(x => x.Panel == "activity")).ShouldBe(1);
		(await BankActivityQueryRules.Aggregates(scoped.Where(x => x.Panel == "activity")).SingleAsync()).Incoming.ShouldBe(100.01m);
		(await scoped.SingleAsync(x => x.Panel == "deposits")).DepositAmount.ShouldBe(100.01m);
	}
	private static NpgsqlConnection Connection()
	{
		var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("BANK_ACTIVITY_TEST_POSTGRES"));
		if (builder.Host != "127.0.0.1" || builder.Port != 55443 || builder.Username != "bank_activity_tests" || builder.Database != "postgres") throw new InvalidOperationException("Only dedicated bank-activity test cluster is allowed.");
		return new NpgsqlConnection(builder.ConnectionString);
	}
	private static Task Schema(ShikshaMitraDbContext db) => db.Database.ExecuteSqlRawAsync("""
CREATE TEMP TABLE "Finance_Transactions" ("Id" uuid,"TenantId" uuid,"AcademicYearId" uuid,"BankAccountId" uuid,"CategoryId" uuid,"Name" text,"SourceKey" text,"SourceId" uuid,"StudentId" uuid,"FinanceFeeAccountId" uuid,"Amount" numeric,"PaymentMethod" int,"TransactionType" int,"IsConfirmed" bool,"PaymentDate" timestamptz,"ExtraProperties" text,"CategoryNameSnapshot" text DEFAULT 'Category',"Notes" text,"IsDeleted" bool DEFAULT false);
CREATE TEMP TABLE "Finance_FeeRecordMetadatas" ("Id" uuid,"TenantId" uuid,"AcademicYearId" uuid,"Status" int,"ReceiptNumber" text,"IsDeleted" bool DEFAULT false);
CREATE TEMP TABLE "Finance_PaymentMethodRecords" ("Id" uuid,"TenantId" uuid,"BankAccountId" uuid,"StudentId" uuid,"FinanceFeeAccountId" uuid,"FeeRecordMetadataId" uuid,"PaymentMethod" int,"PaymentDate" timestamptz,"Amount" numeric,"Notes" text,"IsDeleted" bool DEFAULT false);
CREATE TEMP TABLE "Finance_FeeRefundAccountingEntries" ("Id" uuid,"TenantId" uuid,"RefundRequestId" uuid,"BankAccountId" uuid,"Method" text,"Amount" numeric,"IsCorrection" bool,"CorrectsEntryId" uuid,"PostedAt" timestamptz,"ExternalReference" text);
CREATE TEMP TABLE "Finance_FeeRefundRequests" ("Id" uuid,"TenantId" uuid,"AcademicYearId" uuid,"StudentId" uuid,"FinanceFeeAccountId" uuid,"VoucherNumber" text,"IsDeleted" bool DEFAULT false);
CREATE TEMP TABLE "Finance_CashCollections" ("Id" uuid,"TenantId" uuid,"AcademicYearId" uuid,"SourceId" uuid,"StudentId" uuid,"FinanceFeeAccountId" uuid,"CollectedAt" timestamptz,"PayerName" text,"BankAccountId" uuid,"ExtraProperties" text,"IsDeleted" bool DEFAULT false);
CREATE TEMP TABLE "Finance_CashDeposits" ("Id" uuid,"CashCollectionId" uuid,"Amount" numeric,"CreditedAmount" numeric,"DepositDate" timestamptz,"CreditDate" timestamptz,"Reference" text,"Notes" text);
CREATE TEMP TABLE "Finance_CashDepositCredits" ("Id" uuid,"CashDepositId" uuid,"Amount" numeric,"CreditDate" timestamptz,"Reference" text,"Notes" text)
""");
}
