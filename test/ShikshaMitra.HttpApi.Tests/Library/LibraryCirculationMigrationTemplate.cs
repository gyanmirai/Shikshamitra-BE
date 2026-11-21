using System.Threading.Tasks;
using Xunit;

namespace ShikshaMitra.Library;

[CollectionDefinition(Name)]
public sealed class LibraryCirculationMigrationCollection : ICollectionFixture<LibraryCirculationMigrationTemplate>
{
	public const string Name = "Library circulation PostgreSQL migration";
}

public sealed class LibraryCirculationMigrationTemplate : IAsyncLifetime
{
	private LibraryMigrationPostgresFixture? template;

	public async Task InitializeAsync()
	{
		if (string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable(LibraryMigrationPostgresFixture.ConnectionVariable))) return;
		template = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.CopyEvidence);
		await template.ExecuteAsync(LegacyRows);
	}

	internal Task<LibraryMigrationPostgresFixture> CloneAsync() => template!.CloneAsync();
	public async Task DisposeAsync()
	{
		if (template != null) await template.DisposeAsync();
	}

	private const string LegacyRows = """
		INSERT INTO "academic_Years" ("Id","TenantId","Name","StartDate","EndDate","ExtraProperties","ConcurrencyStamp","CreationTime","IsDeleted")
		VALUES ('81000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','Legacy year','2026-06-01T00:00:00Z','2027-05-31T00:00:00Z','{}','legacy-year',NOW(),false);
		INSERT INTO "AppBooks" ("Id","TenantId","Title","Isbn","Author","Publisher","Edition","Subject","CategoryId","Language","ApplicableAge","Price","Pages","NumberOfCopies","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('10000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','Migration witness','','Asha','','','','82000000-0000-0000-0000-000000000001',2,'',50,20,1,'{}','legacy-book',NOW());
		INSERT INTO "AppBookCopies" ("Id","TenantId","BookId","CopyNumber","AccessionNumber","SectionId","ShelfId","Status","Remarks")
		VALUES ('20000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001',1,'Acc-001','61000000-0000-0000-0000-000000000001','62000000-0000-0000-0000-000000000001',0,'Historic condition');
		INSERT INTO "AppBookIssues" ("Id","TenantId","BookCopyId","AccessionNumber","MemberId","MemberRole","IssueDate","DueDate","ReturnDate","Status","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('83000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001','Acc-001','84000000-0000-0000-0000-000000000001',0,'2026-11-17T00:15:00+05:30','2026-11-24T00:15:00+05:30','2026-11-25T00:15:00+05:30',1,'{}','legacy-loan',NOW());
		INSERT INTO "AppBookReservations" ("Id","TenantId","BookId","MemberId","RequestedDate","ReserveDueDate","CreationTime")
		VALUES ('85000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','84000000-0000-0000-0000-000000000001','2026-11-17T00:15:00+05:30','2026-11-24T00:15:00+05:30',NOW());
		INSERT INTO "AppLibraryCharges" ("Id","TenantId","MemberId","MemberRole","ChargeCategoryId","BookIssueId","Amount","ChargeDate","DueDate","PaymentStatus","PaidDate","Remarks","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('86000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','84000000-0000-0000-0000-000000000001',0,'87000000-0000-0000-0000-000000000001','83000000-0000-0000-0000-000000000001',37.50,'2026-11-17T00:15:00+05:30','2026-11-24T00:15:00+05:30',1,'2026-11-23T12:45:00+05:30','Declared paid without a receipt','{}','legacy-charge',NOW());
		INSERT INTO "AppLibraryBorrowingPolicyVersions" ("Id","TenantId","OriginAcademicYearId","PolicyId","Version","Name","Role","EffectiveFrom","Priority","MaxBooksAtTime","ReservationLimit","LoanDays","DayCountingMode","MaxRenewals","RenewalDays","PickupWindowDays","MoveDueDateToNextOpenDay","Status","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('88000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','81000000-0000-0000-0000-000000000001','89000000-0000-0000-0000-000000000001',1,'Legacy automatic',0,'2026-01-01',0,3,2,7,0,1,7,2,true,0,'{}','legacy-policy',NOW());
		INSERT INTO "AppLibraryCopyEvents" ("Id","TenantId","BookId","CopyId","AccessionNumber","Sequence","EventType","OccurredAt","ActorId","Reason","PreviousStatus","NextStatus","FromSectionId","FromShelfId","ToSectionId","ToShelfId")
		VALUES ('90000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001','Acc-001',1,0,'2026-11-17T04:30:00Z','71000000-0000-0000-0000-000000000001','Original actor evidence',0,1,'61000000-0000-0000-0000-000000000001','62000000-0000-0000-0000-000000000001','61000000-0000-0000-0000-000000000001','62000000-0000-0000-0000-000000000001');
		""";
}
