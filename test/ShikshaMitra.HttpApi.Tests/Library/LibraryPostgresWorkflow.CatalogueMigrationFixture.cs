using ShikshaMitra.Infrastructure.Auth;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	private const string CatalogueOldBook = "ca100000-0000-0000-0000-000000000000";
	private const string CatalogueOldCopy = "ca200000-0000-0000-0000-000000000001";
	private static readonly string CatalogueLegacyRows = $$"""
		INSERT INTO "AppBooks" ("Id","TenantId","Title","Isbn","Author","Publisher","Edition","Subject","CategoryId","Language","ApplicableAge","Price","Pages","NumberOfCopies","ExtraProperties","ConcurrencyStamp","CreationTime")
		SELECT ('ca100000-0000-0000-0000-00000000000'||n)::uuid,'{{TestAuthHandler.TestTenantId}}',
		'Legacy catalogue '||n,'','  অজানা  ','','1','Reading','ca700000-0000-0000-0000-000000000001',
		n,'5-7',12.50+n,20,1,'{"opaque":"legacy +/=: bytes"}','old-catalogue-'||n,'2025-04-01T00:00:00Z'
		FROM generate_series(0,4) AS n;
		INSERT INTO "AppLibrarySections" ("Id","TenantId","SectionName","NumberOfShelf","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('ca400000-0000-0000-0000-000000000001','{{TestAuthHandler.TestTenantId}}','Old reading room',1,'{}','old-section','2025-04-01T00:00:00Z');
		INSERT INTO "AppLibraryShelves" ("Id","SectionId","ShelfName")
		VALUES ('ca300000-0000-0000-0000-000000000001','ca400000-0000-0000-0000-000000000001','A1');
		INSERT INTO "AppBookCopies" ("Id","TenantId","BookId","CopyNumber","AccessionNumber","SectionId","ShelfId","Status","Remarks")
		VALUES ('{{CatalogueOldCopy}}','{{TestAuthHandler.TestTenantId}}','{{CatalogueOldBook}}',1,'CAT-OLD-1',
		'ca400000-0000-0000-0000-000000000001','ca300000-0000-0000-0000-000000000001',0,'Old cover note');
		INSERT INTO "AppBookIssues" ("Id","TenantId","BookCopyId","AccessionNumber","MemberId","MemberRole","IssueDate","DueDate","ReturnDate","Status","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('ca500000-0000-0000-0000-000000000001','{{TestAuthHandler.TestTenantId}}','{{CatalogueOldCopy}}','CAT-OLD-1',
		'ca600000-0000-0000-0000-000000000001',0,'2025-04-01T00:15:00+05:30','2025-04-08T00:15:00+05:30',
		'2025-04-07T00:15:00+05:30',1,'{"opaque":"do not rewrite"}','old-loan','2025-04-01T00:00:00Z');
		""";

	private const string CataloguePreservedState = """
		SELECT jsonb_build_object(
		'books',(SELECT jsonb_agg(to_jsonb(b)-'LanguageCode'-'MaterialCode' ORDER BY b."Id") FROM "AppBooks" b),
		'copies',(SELECT jsonb_agg(to_jsonb(c) ORDER BY c."Id") FROM "AppBookCopies" c),
		'loans',(SELECT jsonb_agg(to_jsonb(l) ORDER BY l."Id") FROM "AppBookIssues" l),
		'receipts',(SELECT jsonb_agg(to_jsonb(h) ORDER BY h."Id") FROM "AppLibraryStockReceipts" h),
		'lines',(SELECT jsonb_agg(to_jsonb(r) ORDER BY r."Id") FROM "AppLibraryStockReceiptLines" r),
		'links',(SELECT jsonb_agg(to_jsonb(r) ORDER BY r."Id") FROM "AppLibraryReceivedCopies" r),
		'commands',(SELECT jsonb_agg(to_jsonb(r) ORDER BY r."Id") FROM "AppLibraryStockReceiptCommands" r)
		)::text;
		""";
}
