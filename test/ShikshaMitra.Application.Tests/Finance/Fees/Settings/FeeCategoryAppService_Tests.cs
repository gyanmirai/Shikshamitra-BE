using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public class FeeCategoryAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IFeeCategoryAppService _feeCategories;
	private readonly IRepository<FeeCategory, Guid> _feeCategoryRepository;

	public FeeCategoryAppService_Tests()
	{
		_feeCategories = GetRequiredService<IFeeCategoryAppService>();
		_feeCategoryRepository = GetRequiredService<IRepository<FeeCategory, Guid>>();
	}

	[Fact]
	public async Task GetList_Searches_Names_Case_Insensitively()
	{
		var academicYearId = Guid.NewGuid();
		var matchingCategory = new FeeCategory(
			Guid.NewGuid(), null, academicYearId, "Seed - School Fees", null);
		var nonMatchingCategory = new FeeCategory(
			Guid.NewGuid(), null, academicYearId, "Transport Fees", null);

		await _feeCategoryRepository.InsertManyAsync(
			[matchingCategory, nonMatchingCategory], autoSave: true);

		foreach (var search in new[] { "seed", "SEED", "SeEd" })
		{
			var result = await _feeCategories.GetListAsync(academicYearId, search);

			result.TotalCount.ShouldBe(1);
			result.Items.Select(x => x.Id).ShouldBe([matchingCategory.Id]);
		}
	}
}
