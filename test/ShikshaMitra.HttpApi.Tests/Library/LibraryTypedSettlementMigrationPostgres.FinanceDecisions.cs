using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryTypedSettlementMigrationPostgres_Tests
{
	[LibraryPostgresTheory]
	[InlineData("Finance_ExternalSettlementPolicies")]
	[InlineData("Finance_ChargeWaiverApplications")]
	[InlineData("Finance_ChargeWaiverReversals")]
	public async Task Typed_Finance_Decision_Evidence_Should_Reject_Update_Delete_And_Truncate(string table)
	{
		await using var database = await CreateM4Async();
		await UpgradeAsync(database);
		await database.ExecuteAsync(LibraryCirculationMigrationPostgres_Tests.ReservationAndFinanceEvidence + FinanceDecisions);
		string[] commands = [$"UPDATE \"{table}\" SET \"Reason\"='Rewritten'", $"DELETE FROM \"{table}\"", $"TRUNCATE \"{table}\" CASCADE"];
		foreach (var command in commands)
		{
			var error = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(command));
			error.MessageText.ShouldContain("append-only");
			(await database.ScalarAsync($"SELECT count(*) FROM \"{table}\";")).ShouldBe("1");
		}
		(await database.ScalarAsync("SELECT \"ExtraProperties\" FROM \"Finance_ChargeWaiverApplications\";")).ShouldBe("{ \"legacy\" : true }");
		(await database.ScalarAsync("SELECT \"ConcurrencyStamp\" FROM \"Finance_ChargeWaiverApplications\";")).ShouldBe("original-waiver-stamp");
	}

	[LibraryPostgresTheory]
	[InlineData("UPDATE \"Finance_ExternalWaiverRequests\" SET \"Amount\"=9.25;")]
	[InlineData("UPDATE \"Finance_ExternalWaiverRequests\" SET \"Reason\"='Changed request';")]
	[InlineData("DELETE FROM \"Finance_ExternalWaiverRequests\";")]
	[InlineData("TRUNCATE \"Finance_ExternalWaiverRequests\" CASCADE;")]
	public async Task Typed_Finance_Waiver_Request_Should_Retain_The_Original_Request(string command)
	{
		await using var database = await CreateM4Async();
		await UpgradeAsync(database);
		await database.ExecuteAsync(LibraryCirculationMigrationPostgres_Tests.ReservationAndFinanceEvidence + FinanceDecisions + PendingWaiver);
		var error = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(command));
		error.MessageText.ShouldContain("waiver request evidence");
		(await database.ScalarAsync("SELECT \"Amount\"::text FROM \"Finance_ExternalWaiverRequests\";")).ShouldBe("10.25");
		(await database.ScalarAsync("SELECT \"Reason\" FROM \"Finance_ExternalWaiverRequests\";")).ShouldBe("Original request reason");
	}

	[LibraryPostgresFact]
	public async Task Typed_Finance_Waiver_Request_Should_Record_One_Independent_Final_Decision()
	{
		await using var database = await CreateM4Async();
		await UpgradeAsync(database);
		await database.ExecuteAsync(LibraryCirculationMigrationPostgres_Tests.ReservationAndFinanceEvidence + FinanceDecisions + PendingWaiver);
		await database.ExecuteAsync("UPDATE \"Finance_ExternalWaiverRequests\" SET \"State\"=2,\"DecidedByUserId\"='71000000-0000-0000-0000-000000000002',\"DecidedAt\"='2026-11-17T04:00:00Z',\"DecisionReason\"='Independent rejection';");
		(await database.ScalarAsync("SELECT \"State\"::text FROM \"Finance_ExternalWaiverRequests\";")).ShouldBe("2");
		var error = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync("UPDATE \"Finance_ExternalWaiverRequests\" SET \"DecisionReason\"='Rewritten final decision';"));
		error.MessageText.ShouldContain("waiver request evidence");
		(await database.ScalarAsync("SELECT \"DecisionReason\" FROM \"Finance_ExternalWaiverRequests\";")).ShouldBe("Independent rejection");
	}

	private const string FinanceDecisions = """
		INSERT INTO "Finance_PolicyVersions" ("Id","TenantId","PolicyType","PolicyId","AcademicYearId","VersionNumber","PolicyName","Status","SnapshotJson","ChangeAction","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('f2000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','collection','f2000000-0000-0000-0000-000000000002','81000000-0000-0000-0000-000000000001',1,'Recorded collection policy','Active','{}','created','{}','policy-version',NOW());
		INSERT INTO "Finance_ExternalSettlementPolicies" ("Id","TenantId","SourceLinkId","CollectionPolicyVersionId","BoundByUserId","Reason","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('f3000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','97000000-0000-0000-0000-000000000001','f2000000-0000-0000-0000-000000000001','71000000-0000-0000-0000-000000000001','Original policy binding','{}','binding',NOW());
		INSERT INTO "Finance_ChargeWaiverApplications" ("Id","TenantId","StudentId","ChargeId","AcademicYearId","ChargeAmount","EffectiveAmount","Reason","AppliedByUserId","IdempotencyKey","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('f4000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','84000000-0000-0000-0000-000000000001','96000000-0000-0000-0000-000000000001','81000000-0000-0000-0000-000000000001',37.50,10.25,'Original waiver decision','71000000-0000-0000-0000-000000000001','original-waiver','{ "legacy" : true }','original-waiver-stamp',NOW());
		INSERT INTO "Finance_ChargeWaiverReversals" ("Id","TenantId","WaiverApplicationId","Amount","Currency","ReversedByUserId","Reason","CreationTime")
		VALUES ('f5000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','f4000000-0000-0000-0000-000000000001',10.25,'INR','71000000-0000-0000-0000-000000000002','Original reversal',NOW());
		""";
	private const string PendingWaiver = """
		INSERT INTO "Finance_ExternalWaiverRequests" ("Id","TenantId","SourceLinkId","PolicyVersionId","Amount","Currency","RequiresApproval","State","RequestedByUserId","Reason","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('f6000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','97000000-0000-0000-0000-000000000001','f2000000-0000-0000-0000-000000000001',10.25,'INR',true,0,'71000000-0000-0000-0000-000000000001','Original request reason','{}','waiver-request',NOW());
		""";
}
