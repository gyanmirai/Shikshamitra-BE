using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Accounts.Settings;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public class FeeSettingsReadPerformance_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Category_page_uses_bounded_reads_and_preserves_head_accounting_and_dependency_details()
	{
		var yearId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var incomeId = await FeeHeadAccountingTestData.CreateIncomeCategoryAsync(
				GetRequiredService<IRepository<AccountHead, Guid>>(),
				GetRequiredService<IRepository<TransactionCategory, Guid>>(), yearId, null, "Performance");
			for (var index = 0; index < 10; index++)
			{
				var category = new FeeCategory(Guid.NewGuid(), null, yearId, $"Category {index}", "Preserved description");
				await GetRequiredService<IRepository<FeeCategory, Guid>>().InsertAsync(category, true);
				await GetRequiredService<IRepository<FeeHead, Guid>>().InsertAsync(new FeeHead(
					Guid.NewGuid(), null, yearId, category.Id, $"Head {index}", null, true, "Preserved head", incomeId), true);
			}
		});

		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
			var result = await GetRequiredService<IFeeCategoryAppService>().GetListAsync(yearId, pageSize: 10, sortBy: "name");
			result.TotalCount.ShouldBe(10);
			result.Items.Count.ShouldBe(10);
			foreach (var category in result.Items)
			{
				category.Description.ShouldBe("Preserved description");
				category.IsUsedInDownstream.ShouldBeFalse();
				var head = category.FeeHeads.Single();
				head.FeeCategoryName.ShouldBe(category.Name);
				head.IncomeCategoryName.ShouldStartWith("Performance Income");
				head.IncomeAccountHeadName.ShouldBe("Performance Revenue");
				head.Dependencies.StudentCount.ShouldBe(0);
				head.IsUsedInDownstream.ShouldBeFalse();
			}
			reads.Count.ShouldBeLessThanOrEqualTo(10, "a page must batch dependency and accounting reads, not repeat them per category");
		});
	}
}
