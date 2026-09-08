using Shouldly;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public class FeeHeadAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IFeeHeadAppService _feeHeads;
	private readonly IRepository<FeeCategory, Guid> _feeCategoryRepository;
	private readonly IRepository<FeeHead, Guid> _feeHeadRepository;

	public FeeHeadAppService_Tests()
	{
		_feeHeads = GetRequiredService<IFeeHeadAppService>();
		_feeCategoryRepository = GetRequiredService<IRepository<FeeCategory, Guid>>();
		_feeHeadRepository = GetRequiredService<IRepository<FeeHead, Guid>>();
	}

	[Fact]
	public async Task GetActive_Returns_All_And_Only_Selectable_Heads_For_The_Academic_Year()
	{
		var academicYearId = Guid.NewGuid();
		var otherAcademicYearId = Guid.NewGuid();
		var activeCategory = new FeeCategory(
			Guid.NewGuid(), null, academicYearId, "Active category", null);
		var inactiveCategory = new FeeCategory(
			Guid.NewGuid(), null, academicYearId, "Inactive category", null);
		inactiveCategory.SetStatus(false, "Not selectable");
		await _feeCategoryRepository.InsertManyAsync([activeCategory, inactiveCategory], autoSave: true);

		var selectable = new FeeHead(
			Guid.NewGuid(), null, academicYearId, activeCategory.Id, "Selectable Fee",
			FeeHeadModule.Finance, true, null);
		var inactiveHead = new FeeHead(
			Guid.NewGuid(), null, academicYearId, activeCategory.Id, "Inactive Fee",
			FeeHeadModule.Finance, false, null);
		inactiveHead.SetActiveStatus(false, "Not selectable");
		var inactiveCategoryHead = new FeeHead(
			Guid.NewGuid(), null, academicYearId, inactiveCategory.Id, "Hidden category Fee",
			FeeHeadModule.Finance, false, null);
		var otherYearHead = new FeeHead(
			Guid.NewGuid(), null, otherAcademicYearId, activeCategory.Id, "Other year Fee",
			FeeHeadModule.Finance, false, null);
		await _feeHeadRepository.InsertManyAsync(
			[selectable, inactiveHead, inactiveCategoryHead, otherYearHead], autoSave: true);

		var result = await _feeHeads.GetActiveAsync(academicYearId);

		result.Select(x => x.Id).ShouldBe([selectable.Id]);
		result.Single().FeeCategoryName.ShouldBe(activeCategory.Name);
	}
}
