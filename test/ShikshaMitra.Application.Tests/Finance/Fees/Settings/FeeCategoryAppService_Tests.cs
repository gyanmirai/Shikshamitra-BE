using ShikshaMitra.Domain.Finance.Fees.Settings.Managers;
using Volo.Abp;
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
	public async Task Uniqueness_Normalizes_Whitespace_And_Identifies_Archived_Conflicts()
	{
		var year = Guid.NewGuid();
		var archived = new FeeCategory(Guid.NewGuid(), null, year, " 8th   FEE CATEGORY ", null);
		archived.Name.ShouldBe("8th FEE CATEGORY");
		archived.SetStatus(false, "Retired");
		await _feeCategoryRepository.InsertAsync(archived, autoSave: true);
		var manager = GetRequiredService<FeeCategoryManager>();
		var error = await Should.ThrowAsync<BusinessException>(() => manager.EnsureNameIsUniqueAsync(year, "8th fee\t category"));
		error.Code.ShouldBe(FinanceFeeCategoryErrorCodes.DuplicateName);
		error.Data["Location"].ShouldBe("Archived");
		await manager.EnsureNameIsUniqueAsync(year, "8th fee category", archived.Id);
		await manager.EnsureNameIsUniqueAsync(Guid.NewGuid(), "8th fee category");
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

	[Fact]
	public async Task GetList_Filters_And_Sorts_Before_Paging()
	{
		var academicYearId = Guid.NewGuid();
		var otherYearId = Guid.NewGuid();
		await _feeCategoryRepository.InsertManyAsync(
		[
			new FeeCategory(Guid.NewGuid(), null, academicYearId, "Alpha", "School fees"),
			new FeeCategory(Guid.NewGuid(), null, academicYearId, "Beta", "School fees"),
			new FeeCategory(Guid.NewGuid(), null, academicYearId, "Gamma", "Transport"),
			new FeeCategory(Guid.NewGuid(), null, otherYearId, "Delta", "School fees")
		], autoSave: true);

		const string filters = "[{\"column\":\"description\",\"type\":\"contains\",\"value\":\"SCHOOL\"}]";
		var pageOne = await _feeCategories.GetListAsync(academicYearId, pageSize: 1, filters: filters, sortBy: "name", sortDirection: "desc");
		var pageTwo = await _feeCategories.GetListAsync(academicYearId, page: 2, pageSize: 1, filters: filters, sortBy: "name", sortDirection: "desc");

		pageOne.TotalCount.ShouldBe(2);
		pageTwo.TotalCount.ShouldBe(2);
		pageOne.Items.Single().Name.ShouldBe("Beta");
		pageTwo.Items.Single().Name.ShouldBe("Alpha");

		var zeroHeads = await _feeCategories.GetListAsync(academicYearId,
			filters: "[{\"column\":\"totalFeeHeads\",\"type\":\"equals\",\"value\":0}]",
			sortBy: "totalFeeHeads", sortDirection: "asc");
		var oneHead = await _feeCategories.GetListAsync(academicYearId,
			filters: "[{\"column\":\"totalFeeHeads\",\"type\":\"equals\",\"value\":1}]");
		var countRange = await _feeCategories.GetListAsync(academicYearId,
			filters: "[{\"column\":\"totalFeeHeads\",\"type\":\"between\",\"value\":{\"min\":0,\"max\":0}}]");
		zeroHeads.TotalCount.ShouldBe(3);
		oneHead.TotalCount.ShouldBe(0);
		countRange.TotalCount.ShouldBe(3);
	}

	[Fact]
	public async Task GetList_Status_Filter_Uses_Both_Boolean_Choices_Within_Tab_Scope()
	{
		var academicYearId = Guid.NewGuid();
		var active = new FeeCategory(Guid.NewGuid(), null, academicYearId, "Active category", null);
		var archived = new FeeCategory(Guid.NewGuid(), null, academicYearId, "Archived category", null);
		archived.SetStatus(false, "Archived for test");
		await _feeCategoryRepository.InsertManyAsync([active, archived], autoSave: true);

		const string activeFilter = "[{\"column\":\"isActive\",\"type\":\"equals\",\"value\":\"true\"}]";
		const string archivedFilter = "[{\"column\":\"isActive\",\"type\":\"equals\",\"value\":\"false\"}]";
		var activeResult = await _feeCategories.GetListAsync(academicYearId, filters: activeFilter);
		var archivedResult = await _feeCategories.GetListAsync(academicYearId, filters: archivedFilter);
		var conflictingResult = await _feeCategories.GetListAsync(academicYearId, isActive: true, filters: archivedFilter);

		activeResult.Items.Single().Id.ShouldBe(active.Id);
		archivedResult.Items.Single().Id.ShouldBe(archived.Id);
		conflictingResult.TotalCount.ShouldBe(0);
	}
}
