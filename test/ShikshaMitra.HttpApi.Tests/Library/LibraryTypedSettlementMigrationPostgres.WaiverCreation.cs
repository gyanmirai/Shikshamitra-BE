using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryTypedSettlementMigrationPostgres_Tests
{
	[LibraryPostgresTheory]
	[InlineData(0, "'71000000-0000-0000-0000-000000000001'", "NULL")]
	[InlineData(1, "'71000000-0000-0000-0000-000000000001'", "'f4000000-0000-0000-0000-000000000001'")]
	[InlineData(2, "'71000000-0000-0000-0000-000000000001'", "NULL")]
	[InlineData(1, "NULL", "'f4000000-0000-0000-0000-000000000001'")]
	[InlineData(99, "'71000000-0000-0000-0000-000000000002'", "NULL")]
	public async Task Typed_Waiver_Creation_Should_Not_Bypass_The_Decision_State_And_Independent_Actor(int state, string actor, string application)
	{
		await using var database = await CreateM4Async();
		await UpgradeAsync(database);
		await database.ExecuteAsync(LibraryCirculationMigrationPostgres_Tests.ReservationAndFinanceEvidence + FinanceDecisions);
		var error = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(DecidedRequest(state, actor, application)));
		error.MessageText.ShouldContain("waiver request evidence");
		(await database.ScalarAsync("SELECT count(*) FROM \"Finance_ExternalWaiverRequests\";")).ShouldBe("0");
	}

	[LibraryPostgresTheory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Typed_Waiver_Creation_Should_Allow_A_Complete_Immediate_Or_Independent_Application(bool requiresApproval)
	{
		await using var database = await CreateM4Async();
		await UpgradeAsync(database);
		await database.ExecuteAsync(LibraryCirculationMigrationPostgres_Tests.ReservationAndFinanceEvidence + FinanceDecisions);
		var command = DecidedRequest(1, "'71000000-0000-0000-0000-000000000001'", "'f4000000-0000-0000-0000-000000000001'");
		command = requiresApproval ? IndependentRequester(command) : command.Replace("'INR',true,", "'INR',false,");
		await database.ExecuteAsync(command);
		(await database.ScalarAsync("SELECT \"State\"::text FROM \"Finance_ExternalWaiverRequests\";")).ShouldBe("1");
		(await database.ScalarAsync("SELECT \"Amount\"::text FROM \"Finance_ExternalWaiverRequests\";")).ShouldBe("10.25");
	}

	[LibraryPostgresTheory]
	[InlineData("TenantId", "'b0000000-0000-0000-0000-000000000002'")]
	[InlineData("ChargeId", "'96000000-0000-0000-0000-000000000099'")]
	[InlineData("EffectiveAmount", "11.25")]
	public async Task Typed_Waiver_Decision_Should_Require_Its_Exact_Tenant_Source_And_Applied_Amount(string column, string replacement)
	{
		await using var database = await CreateM4Async();
		await UpgradeAsync(database);
		await database.ExecuteAsync(LibraryCirculationMigrationPostgres_Tests.ReservationAndFinanceEvidence + FinanceDecisions);
		var tenant = column == "TenantId" ? replacement : "\"TenantId\"";
		var charge = column == "ChargeId" ? replacement : "\"ChargeId\"";
		var amount = column == "EffectiveAmount" ? replacement : "\"EffectiveAmount\"";
		await database.ExecuteAsync($"""
			INSERT INTO "Finance_ChargeWaiverApplications" ("Id","TenantId","StudentId","ChargeId","AcademicYearId","ChargeAmount","EffectiveAmount","Reason","AppliedByUserId","IdempotencyKey","ExtraProperties","ConcurrencyStamp","CreationTime")
			SELECT 'f4000000-0000-0000-0000-000000000099',{tenant},"StudentId",{charge},"AcademicYearId","ChargeAmount",{amount},"Reason","AppliedByUserId",'other-application',"ExtraProperties","ConcurrencyStamp","CreationTime"
			FROM "Finance_ChargeWaiverApplications";
			""");
		await database.ExecuteAsync(IndependentRequester(PendingWaiver));
		var otherApplication = $"SELECT \"{column}\"::text FROM \"Finance_ChargeWaiverApplications\" WHERE \"Id\"='f4000000-0000-0000-0000-000000000099';";
		(await database.ScalarAsync(otherApplication)).ShouldBe(replacement.Trim('\''));
		var error = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync("UPDATE \"Finance_ExternalWaiverRequests\" SET \"State\"=1,\"DecidedByUserId\"='71000000-0000-0000-0000-000000000001',\"DecidedAt\"=NOW(),\"DecisionReason\"='Independent decision',\"AppliedWaiverId\"='f4000000-0000-0000-0000-000000000099';"));
		error.MessageText.ShouldContain("waiver request evidence");
	}

	private static string IndependentRequester(string command) => command.Replace(
		",0,'71000000-0000-0000-0000-000000000001','Original request reason'",
		",0,'71000000-0000-0000-0000-000000000002','Original request reason'")
		.Replace(",1,'71000000-0000-0000-0000-000000000001','Original request reason'",
			",1,'71000000-0000-0000-0000-000000000002','Original request reason'");
	private static string DecidedRequest(int state, string actor, string application) => $$"""
		INSERT INTO "Finance_ExternalWaiverRequests" ("Id","TenantId","SourceLinkId","PolicyVersionId","Amount","Currency","RequiresApproval","State","RequestedByUserId","Reason","DecidedByUserId","DecidedAt","DecisionReason","AppliedWaiverId","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('f6000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','97000000-0000-0000-0000-000000000001','f2000000-0000-0000-0000-000000000001',10.25,'INR',true,{{state}},'71000000-0000-0000-0000-000000000001','Original request reason',{{actor}},NOW(),'Recorded decision',{{application}},'{}','waiver-request',NOW());
		""";
}
