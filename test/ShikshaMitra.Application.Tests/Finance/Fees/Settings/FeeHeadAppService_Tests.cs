using Shouldly;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Domain.Finance.Fees.Settings.Managers;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public class FeeHeadAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IFeeHeadAppService _feeHeads;
	private readonly IRepository<FeeCategory, Guid> _feeCategoryRepository;
	private readonly IRepository<FeeHead, Guid> _feeHeadRepository;
	private readonly IRepository<TransactionCategory, Guid> _incomeCategoryRepository;
	private readonly IRepository<AccountHead, Guid> _accountHeadRepository;
	private readonly FeeHeadAccountingManager _accountingManager;

	public FeeHeadAppService_Tests()
	{
		_feeHeads = GetRequiredService<IFeeHeadAppService>();
		_feeCategoryRepository = GetRequiredService<IRepository<FeeCategory, Guid>>();
		_feeHeadRepository = GetRequiredService<IRepository<FeeHead, Guid>>();
		_incomeCategoryRepository = GetRequiredService<IRepository<TransactionCategory, Guid>>();
		_accountHeadRepository = GetRequiredService<IRepository<AccountHead, Guid>>();
		_accountingManager = GetRequiredService<FeeHeadAccountingManager>();
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
		var incomeCategory = await CreateIncomeCategoryAsync(academicYearId, "Tuition");
		var otherYearIncomeCategory = await CreateIncomeCategoryAsync(otherAcademicYearId, "Other Year");

		var selectable = new FeeHead(
			Guid.NewGuid(), null, academicYearId, activeCategory.Id, "Selectable Fee",
			FeeHeadModule.Finance, true, null, incomeCategory.Id);
		var inactiveHead = new FeeHead(
			Guid.NewGuid(), null, academicYearId, activeCategory.Id, "Inactive Fee",
			FeeHeadModule.Finance, false, null, incomeCategory.Id);
		inactiveHead.SetActiveStatus(false, "Not selectable");
		var inactiveCategoryHead = new FeeHead(
			Guid.NewGuid(), null, academicYearId, inactiveCategory.Id, "Hidden category Fee",
			FeeHeadModule.Finance, false, null, incomeCategory.Id);
		var otherYearHead = new FeeHead(
			Guid.NewGuid(), null, otherAcademicYearId, activeCategory.Id, "Other year Fee",
			FeeHeadModule.Finance, false, null, otherYearIncomeCategory.Id);
		await _feeHeadRepository.InsertManyAsync(
			[selectable, inactiveHead, inactiveCategoryHead, otherYearHead], autoSave: true);

		var result = await _feeHeads.GetActiveAsync(academicYearId);

		result.Select(x => x.Id).ShouldBe([selectable.Id]);
		result.Single().FeeCategoryName.ShouldBe(activeCategory.Name);
		result.Single().IncomeCategoryId.ShouldBe(incomeCategory.Id);
		result.Single().IncomeCategoryName.ShouldBe(incomeCategory.Name);
		result.Single().IncomeAccountHeadName.ShouldBe("Tuition Revenue");
	}

	[Fact]
	public async Task Accounting_Mapping_Rejects_An_Inactive_Income_Category()
	{
		var academicYearId = Guid.NewGuid();
		var incomeCategory = await CreateIncomeCategoryAsync(academicYearId, "Inactive");
		incomeCategory.Deactivate("No longer available for new fees");
		await _incomeCategoryRepository.UpdateAsync(incomeCategory, autoSave: true);

		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_accountingManager.GetValidMappingAsync(academicYearId, incomeCategory.Id));

		exception.Code.ShouldBe(FinanceFeeHeadErrorCodes.InvalidIncomeCategory);
	}

	private async Task<TransactionCategory> CreateIncomeCategoryAsync(Guid academicYearId, string prefix)
	{
		var accountHead = new AccountHead(
			Guid.NewGuid(), null, academicYearId,
			$"{prefix} Revenue", AccountHeadType.Income, null, null);
		await _accountHeadRepository.InsertAsync(accountHead, autoSave: true);
		var category = new TransactionCategory(
			Guid.NewGuid(), null, academicYearId, TransactionType.Income, $"{prefix} Income", null,
			[], true, $"{prefix.Replace(" ", "-").ToUpperInvariant()}-INC", accountHead.Id,
			DateTimeOffset.UtcNow.AddDays(-1), null);
		return await _incomeCategoryRepository.InsertAsync(category, autoSave: true);
	}
}
