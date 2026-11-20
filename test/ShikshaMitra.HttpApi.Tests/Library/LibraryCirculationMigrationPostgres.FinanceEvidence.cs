using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCirculationMigrationPostgres_Tests
{
	[LibraryPostgresTheory]
	[InlineData("AppLibraryReservationEvents", "Reason", "Original queue review")]
	[InlineData("Finance_ExternalReceivableSourceLinks", "SourceSnapshotJson", "{\"amount\":37.50}")]
	[InlineData("Finance_ExternalReceivableCommands", "CommandFingerprint", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
	public async Task Reservation_And_Finance_Evidence_Should_Reject_Every_Destructive_Sql_Operation(string table, string evidenceColumn, string originalEvidence)
	{
		await using var database = await template.CloneAsync();
		await UpgradeAsync(database);
		await database.ExecuteAsync(ReservationAndFinanceEvidence);
		string[] commands = [$"UPDATE \"{table}\" SET \"{evidenceColumn}\"='changed'", $"DELETE FROM \"{table}\"", $"TRUNCATE \"{table}\" CASCADE"];
		foreach (var command in commands)
		{
			var error = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(command));
			error.MessageText.ShouldContain("append-only");
			(await database.ScalarAsync($"SELECT count(*) FROM \"{table}\";")).ShouldBe("1");
			(await database.ScalarAsync($"SELECT \"{evidenceColumn}\" FROM \"{table}\";")).ShouldBe(originalEvidence);
		}
		(await database.ScalarAsync("SELECT count(*) FROM \"Finance_ExternalReceivableCommands\";")).ShouldBe("1");
		(await database.ScalarAsync("SELECT count(*) FROM \"Finance_ExternalReceivableSourceLinks\";")).ShouldBe("1");
	}

	[LibraryPostgresFact]
	public async Task Rollback_Should_Retain_Reservation_Review_And_Linked_Pending_Finance_Evidence()
	{
		await using var database = await template.CloneAsync();
		await UpgradeAsync(database);
		await database.ExecuteAsync(ReservationAndFinanceEvidence);
		var error = await Should.ThrowAsync<PostgresException>(() => RollbackAsync(database));
		error.MessageText.ShouldContain("cannot preserve");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryReservationEvents\";")).ShouldBe("1");
		(await database.ScalarAsync("SELECT count(*) FROM \"Finance_ExternalReceivableSourceLinks\";")).ShouldBe("1");
		(await database.ScalarAsync("SELECT count(*) FROM \"Finance_ExternalReceivableCommands\";")).ShouldBe("1");
		(await database.ScalarAsync("SELECT \"Amount\"::text FROM \"Finance_Charges\";")).ShouldBe("37.50");
		(await database.ScalarAsync("SELECT \"PaymentStatus\" FROM \"AppLibraryCharges\" WHERE \"Id\"='86000000-0000-0000-0000-000000000001';")).ShouldBe("1");
	}

	// These are fresh, unpaid SQL boundary witnesses. The legacy declared-paid assessment remains unlinked.
	internal const string ReservationAndFinanceEvidence = """
		INSERT INTO "AppLibraryReservationEvents" ("Id","TenantId","ReservationId","Sequence","EventType","PreviousState","NextState","ActorKind","ActorId","Reason","OccurredAt","BeforeSnapshotJson","AfterSnapshotJson")
		VALUES ('93000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','85000000-0000-0000-0000-000000000001',1,'Review',5,5,1,'71000000-0000-0000-0000-000000000001','Original queue review',NOW(),'{}','{}');
		INSERT INTO "AppLibraryCharges" ("Id","TenantId","MemberId","MemberRole","ChargeCategoryId","Amount","PaymentStatus","SchoolChargeDate","ChargeDateProvenance","AcademicYearId","AcademicYearProvenance","Currency","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('94000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','84000000-0000-0000-0000-000000000001',0,'87000000-0000-0000-0000-000000000001',37.50,0,'2026-11-17',2,'81000000-0000-0000-0000-000000000001',2,'INR','{}','new-assessment',NOW());
		INSERT INTO "Finance_ChargeRequests" ("Id","TenantId","AcademicYearId","ChargeName","ChargeCategoryId","PurposeDescription","Amount","Priority","RequestorId","AppliedOn","AudienceSnapshot","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('95000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','81000000-0000-0000-0000-000000000001','Pending library assessment','87000000-0000-0000-0000-000000000001','SQL immutability witness',37.50,0,'71000000-0000-0000-0000-000000000001',NOW(),'{}','{}','pending-request',NOW());
		INSERT INTO "Finance_Charges" ("Id","TenantId","AcademicYearId","ChargeRequestId","ChargeName","ChargeCategoryId","StudentId","PurposeDescription","Amount","AppliedOn","Priority","Status","RequestorId","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('96000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','81000000-0000-0000-0000-000000000001','95000000-0000-0000-0000-000000000001','Pending library assessment','87000000-0000-0000-0000-000000000001','84000000-0000-0000-0000-000000000001','SQL immutability witness',37.50,NOW(),0,0,'71000000-0000-0000-0000-000000000001','{}','pending-charge',NOW());
		INSERT INTO "Finance_ExternalReceivableSourceLinks" ("Id","TenantId","SourceKind","SourceId","SourceVersion","SourceFingerprint","SourceSnapshotJson","FinanceChargeRequestId","FinanceChargeId","AcademicYearId","Currency","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('97000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001',1,'94000000-0000-0000-0000-000000000001','new-assessment',repeat('a',64),'{"amount":37.50}','95000000-0000-0000-0000-000000000001','96000000-0000-0000-0000-000000000001','81000000-0000-0000-0000-000000000001','INR','{}','original-link',NOW());
		INSERT INTO "Finance_ExternalReceivableCommands" ("Id","TenantId","Operation","IdempotencyKey","CommandFingerprint","SourceLinkId","ActorId","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('98000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','Post','original-post',repeat('b',64),'97000000-0000-0000-0000-000000000001','71000000-0000-0000-0000-000000000001','{}','original-command',NOW());
		""";
}
