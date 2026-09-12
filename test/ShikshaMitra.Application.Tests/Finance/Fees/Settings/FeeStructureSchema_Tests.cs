using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Migrations;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public class FeeStructureSchema_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
    [Fact]
    public async Task Revision_Model_Snapshot_And_Migration_Preserve_The_Same_Constraints()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
            var current = db.Model.FindEntityType(typeof(FeeStructure))!;
            var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model.FindEntityType(typeof(FeeStructure).FullName!)!;
            foreach (var name in new[] { "RevisionRootId", "RevisionNumber", "PreviousRevisionId", "ReceiptBookId", "NormalizedName" })
            {
                current.FindProperty(name).ShouldNotBeNull();
                current.FindProperty(name)!.ClrType.ShouldBe(snapshot.FindProperty(name)!.ClrType);
                current.FindProperty(name)!.IsNullable.ShouldBe(snapshot.FindProperty(name)!.IsNullable);
            }
            foreach (var keys in new[] { new[] { "RevisionRootId", "RevisionNumber" }, new[] { "StandardId", "NormalizedName" } })
            {
                var liveIndex = current.GetIndexes().Single(x => x.Properties.Select(p => p.Name).SequenceEqual(keys));
                var savedIndex = snapshot.GetIndexes().Single(x => x.Properties.Select(p => p.Name).SequenceEqual(keys));
                liveIndex.IsUnique.ShouldBeTrue(); savedIndex.IsUnique.ShouldBeTrue();
                liveIndex.GetFilter().ShouldBe(savedIndex.GetFilter());
            }
            var migration = new AddFeeStructureRevisions();
            migration.UpOperations.OfType<AddColumnOperation>().Count().ShouldBe(5);
            migration.UpOperations.OfType<CreateIndexOperation>().Count(x => x.IsUnique).ShouldBe(2);
            migration.UpOperations.OfType<SqlOperation>().ShouldContain(x => x.Sql.Contains("SET \"RevisionRootId\" = \"Id\""));
            migration.UpOperations.OfType<AddForeignKeyOperation>().All(x => x.OnDelete == Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Restrict).ShouldBeTrue();
        });
    }
}
