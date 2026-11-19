using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.FeeRefund;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.FeeRefunds;

public class PaidChargeRefundMigration_Tests
{
    [Fact]
    public void Refund_Source_Model_Should_Match_Migrations_And_Generate_Postgres_Upgrade()
    {
        ShikshaMitraEfCoreEntityExtensionMappings.Configure();
        using var context = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
            .UseNpgsql("Host=localhost;Database=refund_model_validation;Username=validation;Password=unused")
            .Options);
        var currentModel = context.GetService<IDesignTimeModel>().Model;
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        // Host test fixtures add global ABP extensions to unrelated entities. Compare
        // the complete refund source shapes rather than those unrelated host changes.
        foreach (var source in new[] { typeof(FeeRefundLine), typeof(FeeRefundReservation) })
        {
            var current = currentModel.GetEntityTypes().Single(x => x.ClrType == source);
            var saved = snapshot.GetEntityTypes().Single(x => x.GetTableName() == current.GetTableName());
            current.GetProperties().Select(x => new { x.Name, ProviderType = ProviderType(x), x.IsNullable, StoreType = x.GetColumnType() })
                .OrderBy(x => x.Name).ToArray().ShouldBe(saved.GetProperties()
                    .Select(x => new { x.Name, ProviderType = ProviderType(x), x.IsNullable, StoreType = x.GetColumnType() }).OrderBy(x => x.Name).ToArray());
            current.GetIndexes().Select(x => string.Join(",", x.Properties.Select(p => p.Name)))
                .OrderBy(x => x).ToArray().ShouldBe(saved.GetIndexes()
                    .Select(x => string.Join(",", x.Properties.Select(p => p.Name))).OrderBy(x => x).ToArray());
            current.GetCheckConstraints().Select(x => new { x.Name, x.Sql }).OrderBy(x => x.Name).ToArray()
                .ShouldBe(saved.GetCheckConstraints().Select(x => new { x.Name, x.Sql }).OrderBy(x => x.Name).ToArray());
        }
        var migrations = context.Database.GetMigrations().ToList();
        const string target = "20261023120000_EnablePaidChargeRefunds";
        var previous = migrations[migrations.IndexOf(target) - 1];
        var sql = context.GetService<IMigrator>().GenerateScript(previous, target);
        sql.ShouldContain("CK_FeeRefundLines_Source");
        sql.ShouldContain("ChargeId");
        sql.ShouldNotContain("DROP TABLE");
    }

    private static Type ProviderType(IProperty property)
    {
        // Generated snapshots describe enum columns by their underlying number type.
        var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        return type.IsEnum ? Enum.GetUnderlyingType(type) : type;
    }
}
