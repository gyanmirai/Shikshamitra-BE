using System.Threading.Tasks;
using Npgsql;
using Shouldly;

namespace ShikshaMitra.Library;

public class LibraryMigrationPostgres_Tests
{
    [LibraryPostgresFact]
    public async Task Upgrade_Backfills_Ownership_And_Counts_Without_Replacing_Historical_Copies()
    {
        await using var fixture = await LibraryMigrationPostgresFixture.CreateAsync();
        await fixture.ExecuteAsync(fixture.Script());

        (await fixture.ScalarAsync("SELECT \"TenantId\" FROM \"AppBookCopies\" WHERE \"AccessionNumber\" = 'Acc-001'"))
            .ShouldBe(LibraryMigrationPostgresFixture.TenantA);
        (await fixture.ScalarAsync("SELECT count(*) FROM \"AppBooks\" WHERE \"NumberOfCopies\" = 99")).ShouldBe("0");
        (await fixture.ScalarAsync("SELECT \"NumberOfCopies\" FROM \"AppBooks\" WHERE \"Id\" = '10000000-0000-0000-0000-000000000004'"))
            .ShouldBe("0");
        (await fixture.ScalarAsync("SELECT \"Remarks\" FROM \"AppBookCopies\" WHERE \"Id\" = '20000000-0000-0000-0000-000000000001' AND \"Status\" = 4"))
            .ShouldBe("Existing damage evidence");
        (await fixture.ScalarAsync("SELECT \"AttemptCount\" FROM \"AppLibraryNotificationHistories\" WHERE \"Status\" = 'Sent'"))
            .ShouldBe("0");
    }

    [LibraryPostgresFact]
    public async Task Database_Enforces_Normalized_Accession_Uniqueness_Per_School_And_For_Host()
    {
        await using var fixture = await LibraryMigrationPostgresFixture.CreateAsync();
        await fixture.ExecuteAsync(fixture.Script());
        (await fixture.ScalarAsync("SELECT count(*) FROM \"AppBookCopies\" WHERE \"NormalizedAccessionNumber\" = 'ACC-001'"))
            .ShouldBe("2");

        var school = await Should.ThrowAsync<PostgresException>(() => fixture.ExecuteAsync("""
            INSERT INTO "AppBookCopies" ("Id", "BookId", "AccessionNumber", "Status", "TenantId") VALUES
            (gen_random_uuid(), '10000000-0000-0000-0000-000000000001', ' ACC-001 ', 0, 'a0000000-0000-0000-0000-000000000001');
            """));
        school.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        var host = await Should.ThrowAsync<PostgresException>(() => fixture.ExecuteAsync("""
            INSERT INTO "AppBookCopies" ("Id", "BookId", "AccessionNumber", "Status", "TenantId") VALUES
            (gen_random_uuid(), '10000000-0000-0000-0000-000000000003', 'host-001', 0, NULL);
            """));
        host.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [LibraryPostgresFact]
    public async Task Duplicate_Preflight_Rejects_Upgrade_Without_Changing_Legacy_Schema()
    {
        await using var fixture = await LibraryMigrationPostgresFixture.CreateAsync();
        await fixture.ExecuteAsync("""
            UPDATE "AppBooks" SET "TenantId" = 'a0000000-0000-0000-0000-000000000001'
            WHERE "Id" = '10000000-0000-0000-0000-000000000002';
            """);
        var error = await Should.ThrowAsync<PostgresException>(() => fixture.ExecuteAsync(fixture.Script()));
        error.MessageText.ShouldContain("duplicate normalized accessions");
        (await fixture.ScalarAsync(HasOwnershipColumn)).ShouldBe("0");
        (await fixture.ScalarAsync("SELECT count(*) FROM \"AppBookCopies\"")).ShouldBe("3");
    }

    [LibraryPostgresFact]
    public async Task Rollback_Preserves_Records_And_Rejects_Restoring_Unsafe_Global_Uniqueness()
    {
        await using var fixture = await LibraryMigrationPostgresFixture.CreateAsync();
        await fixture.ExecuteAsync(fixture.Script());
        await fixture.ExecuteAsync("""
            INSERT INTO "AppLibraryNotificationSettings" VALUES
            (gen_random_uuid(), 'b0000000-0000-0000-0000-000000000002', 'dueDateReminder', 'Member', 'Email');
            """);
        var error = await Should.ThrowAsync<PostgresException>(() => fixture.ExecuteAsync(fixture.Script(rollback: true)));
        error.MessageText.ShouldContain("cannot preserve current school records");
        (await fixture.ScalarAsync(HasOwnershipColumn)).ShouldBe("1");
        (await fixture.ScalarAsync("SELECT count(*) FROM \"AppLibraryNotificationSettings\"")).ShouldBe("2");

        await using var reversible = await LibraryMigrationPostgresFixture.CreateAsync();
        await reversible.ExecuteAsync(reversible.Script());
        await reversible.ExecuteAsync(reversible.Script(rollback: true));
        (await reversible.ScalarAsync(HasOwnershipColumn)).ShouldBe("0");
        (await reversible.ScalarAsync("SELECT count(*) FROM \"AppBookCopies\"")).ShouldBe("3");
    }

    private const string HasOwnershipColumn = """
        SELECT count(*) FROM information_schema.columns WHERE table_name = 'AppBookCopies' AND column_name = 'TenantId'
        """;
}
