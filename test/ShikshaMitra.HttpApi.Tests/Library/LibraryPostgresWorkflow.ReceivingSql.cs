using System;
using ShikshaMitra.Infrastructure.Auth;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	private const string ReceivingHeader = "fae00000-0000-0000-0000-000000000001";
	private const string ReceivingLine = "fae00000-0000-0000-0000-000000000002";
	private const string ReceivingCopy = "fae00000-0000-0000-0000-000000000003";
	private const string ReceivingLink = "fae00000-0000-0000-0000-000000000004";
	private const string ReceivingCommand = "fae00000-0000-0000-0000-000000000005";

	private static string ReceivingSql(Guid sourceCopy, string? defect = null, string cost = "NULL")
	{
		var tenant = defect == "ForeignSourceTenant" ? LibraryMigrationPostgresFixture.TenantB : TestAuthHandler.TestTenantId;
		var actor = defect == "WrongCommandActor" ? Guid.NewGuid().ToString("D") : TestAuthHandler.TestUserId;
		var expectedLines = defect == "MissingLine" ? 2 : 1;
		var expectedCopies = defect == "MissingCopy" ? 2 : 1;
		var currency = cost == "NULL" ? "NULL" : "'INR'";
		var command = defect == "MissingCommand" ? string.Empty : $$"""
			INSERT INTO "AppLibraryStockReceiptCommands" ("Id","TenantId","ActorId","ReceiptId","IdempotencyKey","PayloadFingerprint","RecordedAt")
			VALUES ('{{ReceivingCommand}}','{{tenant}}','{{actor}}','{{ReceivingHeader}}','sql-receiving-witness',repeat('A',64),'2026-11-18T00:00:00+00');
			""";
		return $$"""
			BEGIN;
			INSERT INTO "AppBookCopies" ("Id","TenantId","BookId","CopyNumber","AccessionNumber","SectionId","ShelfId","Status","Remarks")
			SELECT '{{ReceivingCopy}}',"TenantId","BookId","CopyNumber"+1,'RECV-SQL-1',"SectionId","ShelfId",0,NULL FROM "AppBookCopies" WHERE "Id"='{{sourceCopy:D}}';
			INSERT INTO "AppLibraryStockReceipts" ("Id","TenantId","ReceivedOn","Source","SourceName","Reference","RecordedBy","RecordedAt","ExpectedLineCount","ExpectedCopyCount")
			VALUES ('{{ReceivingHeader}}','{{tenant}}','2026-11-18',1,'Community donor',NULL,'{{TestAuthHandler.TestUserId}}','2026-11-18T00:00:00+00',{{expectedLines}},1);
			INSERT INTO "AppLibraryStockReceiptLines" ("Id","TenantId","ReceiptId","LineNumber","BookId","SectionId","ShelfId","ExpectedCopyCount","UnitCost","Currency")
			SELECT '{{ReceivingLine}}','{{tenant}}','{{ReceivingHeader}}',1,"BookId","SectionId","ShelfId",{{expectedCopies}},{{cost}},{{currency}}
			FROM "AppBookCopies" WHERE "Id"='{{ReceivingCopy}}';
			INSERT INTO "AppLibraryReceivedCopies" ("Id","TenantId","ReceiptLineId","CopyId","AccessionNumber","CopyNumber")
			SELECT '{{ReceivingLink}}','{{tenant}}','{{ReceivingLine}}',"Id","AccessionNumber","CopyNumber" FROM "AppBookCopies" WHERE "Id"='{{ReceivingCopy}}';
			{{command}}
			COMMIT;
			""";
	}
}
