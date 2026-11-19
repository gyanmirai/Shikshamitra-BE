using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Migrations;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class FinancePolicyDefaultPersistence_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Host_Default_Index_Blocks_An_Independent_Second_Promotion()
	{
		var service = GetRequiredService<IConcessionPolicyAppService>();
		var year = Guid.NewGuid();
		CreateUpdateConcessionPolicyDto Input(string name) => new()
		{
			AcademicYearId = year, PolicyName = name, Status = "Active",
			ValueSource = "PolicyFixed", CalculationMethod = "Percentage", PercentageValue = 10,
			ApplyOn = "AllOutstanding", RoundingMethod = "None"
		};
		var first = await service.CreateAsync(Input("Host default"));
		var second = await service.CreateAsync(Input("Host alternative"));
		first.IsDefault.ShouldBeTrue();
		second.IsDefault.ShouldBeFalse();
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			var error = await Should.ThrowAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync(
				$"UPDATE \"Finance_ConcessionPolicies\" SET \"IsDefault\" = true WHERE \"Id\" = {second.Id}"));
			error.SqliteErrorCode.ShouldBe(19); // Database constraint, independent of application guards.
			error.Message.ShouldContain("Finance_ConcessionPolicies.AcademicYearId");
		});
		(await service.GetAsync(first.Id)).IsDefault.ShouldBeTrue();
		(await service.GetAsync(second.Id)).IsDefault.ShouldBeFalse();
	}

	[Fact]
	public async Task All_Default_Indexes_Agree_Between_Runtime_Snapshot_And_Forward_Migration()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
			var indexes = new EnforceHostFinancePolicyDefaults().UpOperations.OfType<CreateIndexOperation>().ToList();
			indexes.Count.ShouldBe(4);
			foreach (var type in new[] { typeof(ConcessionPolicy), typeof(RefundPolicy), typeof(CollectionPolicy), typeof(ProrationPolicy) })
			{
				var current = db.Model.FindEntityType(type)!;
				var saved = snapshot.FindEntityType(type.FullName!)!;
				var liveIndex = current.GetIndexes().Single(x => x.GetDatabaseName()!.EndsWith("Host_ActiveDefault"));
				var savedIndex = saved.GetIndexes().Single(x => x.GetDatabaseName() == liveIndex.GetDatabaseName());
				var migration = indexes.Single(x => x.Name == liveIndex.GetDatabaseName());
				liveIndex.IsUnique.ShouldBeTrue();
				savedIndex.IsUnique.ShouldBeTrue();
				liveIndex.GetFilter().ShouldBe(savedIndex.GetFilter());
				migration.Filter.ShouldBe(liveIndex.GetFilter());
				migration.Columns.ShouldBe(new[] { nameof(ConcessionPolicy.AcademicYearId) });
			}
		});
	}
}
