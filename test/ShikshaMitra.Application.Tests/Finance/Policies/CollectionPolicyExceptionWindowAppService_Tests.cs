using Shouldly;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>
/// Phase 4j (collection-policy-implementation-plan.md §4j) — CRUD + the calc-service
/// integration's acceptance criterion, exercised end-to-end via /simulate.
/// </summary>
public class CollectionPolicyExceptionWindowAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly ICollectionPolicyExceptionWindowAppService _windows;
	private readonly ICollectionPolicyAppService _collectionPolicies;
	private readonly IGuidGenerator _guidGenerator;
	private readonly IRepository<AcademicYear, Guid> _academicYearRepository;
	private readonly IRepository<FeeCategory, Guid> _feeCategoryRepository;
	private readonly IRepository<FeeHead, Guid> _feeHeadRepository;

	public CollectionPolicyExceptionWindowAppService_Tests()
	{
		_windows = GetRequiredService<ICollectionPolicyExceptionWindowAppService>();
		_collectionPolicies = GetRequiredService<ICollectionPolicyAppService>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
		_academicYearRepository = GetRequiredService<IRepository<AcademicYear, Guid>>();
		_feeCategoryRepository = GetRequiredService<IRepository<FeeCategory, Guid>>();
		_feeHeadRepository = GetRequiredService<IRepository<FeeHead, Guid>>();
	}

	private static CreateCollectionPolicyExceptionWindowDto SchoolWideWindow(
		DateTime startDate, DateTime endDate, string effect = "SuspendLateFeeAccrual", int? extensionDays = null) => new()
	{
		Scope = "SchoolWide",
		StartDate = startDate,
		EndDate = endDate,
		Effect = effect,
		ExtensionDays = extensionDays,
		ReasonCode = "GATEWAY_OUTAGE",
		Reason = "Payment gateway was down for several days.",
	};

	private async Task<CollectionPolicyDto> CreateActivePolicyAsync(string name)
	{
		var academicYearId = _guidGenerator.Create();
		var categoryId = _guidGenerator.Create();
		var feeHeadId = _guidGenerator.Create();
		await _academicYearRepository.InsertAsync(new AcademicYear(
			academicYearId, null, $"Test {academicYearId:N}",
			new DateTime(2025, 4, 1), new DateTime(2026, 3, 31)), autoSave: true);
		await _feeCategoryRepository.InsertAsync(new FeeCategory(
			categoryId, null, academicYearId, $"Category {categoryId:N}", null), autoSave: true);
		await _feeHeadRepository.InsertAsync(new FeeHead(
			feeHeadId, null, academicYearId, categoryId, "Tuition Fee",
			FeeHeadModule.Finance, false, null), autoSave: true);

		return await _collectionPolicies.CreateAsync(new CreateUpdateCollectionPolicyDto
		{
			AcademicYearId = academicYearId,
			PolicyName = name,
			PolicyCode = $"CP-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
			Status = "Active",
			EnableLateFee = true,
			GracePeriodDays = 0,
			LateFeeApplyOnFeeHeads = new() { feeHeadId },
			LateFeeType = "PerDay",
			AmountPerDay = 20,
			PercentageBase = "OutstandingAmount",
			CompoundFrequency = "Daily",
			RoundingMethod = "None",
			AllowOnlineCollection = true,
		});
	}

	[Fact]
	public async Task Create_Then_List_Round_Trips_A_SchoolWide_Window()
	{
		var today = DateTime.UtcNow.Date;
		var created = await _windows.CreateAsync(SchoolWideWindow(today.AddDays(-5), today.AddDays(5)));

		created.Id.ShouldNotBe(Guid.Empty);
		created.Scope.ShouldBe("SchoolWide");
		created.Effect.ShouldBe("SuspendLateFeeAccrual");
		created.IsActive.ShouldBeTrue();

		var list = await _windows.GetListAsync(new CollectionPolicyExceptionWindowListRequestDto { ActiveOn = today });
		list.ShouldContain(w => w.Id == created.Id);
	}

	[Fact]
	public async Task AcademicYear_Scope_Requires_A_ScopeId()
	{
		var input = SchoolWideWindow(DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(1));
		input.Scope = "AcademicYear";
		input.ScopeId = null;

		var ex = await Should.ThrowAsync<BusinessException>(() => _windows.CreateAsync(input));
		ex.Code.ShouldBe(CollectionPolicyErrorCodes.ExceptionWindowScopeIdRequired);
	}

	[Fact]
	public async Task Delete_Before_Start_Date_Succeeds()
	{
		var future = DateTime.UtcNow.Date.AddDays(30);
		var created = await _windows.CreateAsync(SchoolWideWindow(future, future.AddDays(5)));

		await _windows.DeleteAsync(created.Id);

		var list = await _windows.GetListAsync(new CollectionPolicyExceptionWindowListRequestDto());
		list.ShouldNotContain(w => w.Id == created.Id);
	}

	[Fact]
	public async Task Delete_After_Start_Date_Is_Rejected()
	{
		var today = DateTime.UtcNow.Date;
		var created = await _windows.CreateAsync(SchoolWideWindow(today, today.AddDays(5)));

		var ex = await Should.ThrowAsync<BusinessException>(() => _windows.DeleteAsync(created.Id));
		ex.Code.ShouldBe(CollectionPolicyErrorCodes.ExceptionWindowAlreadyActive);
	}

	[Fact]
	public async Task Acceptance_SuspendLateFeeAccrual_Window_Zeroes_Late_Fee_Via_Simulate()
	{
		var policy = await CreateActivePolicyAsync("Exception Window Acceptance Policy");

		var today = DateTime.UtcNow.Date;
		await _windows.CreateAsync(SchoolWideWindow(today.AddDays(-5), today.AddDays(5)));

		var result = await _collectionPolicies.SimulateAsync(policy.Id, new CollectionPolicySimulateRequestDto
		{
			DaysLate = 15,
			OutstandingAmount = 5000,
		});

		// Without the window this would be 15 * 20 = 300 — the active SuspendLateFeeAccrual
		// window zeroes it out with no manual waiver action required.
		result.LateFee.ShouldBe(0);
		result.ExceptionWindowApplied.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task No_Active_Window_Simulates_Normally()
	{
		var policy = await CreateActivePolicyAsync("No Window Policy");

		var result = await _collectionPolicies.SimulateAsync(policy.Id, new CollectionPolicySimulateRequestDto
		{
			DaysLate = 15,
			OutstandingAmount = 5000,
		});

		result.LateFee.ShouldBe(300);
	}
}
