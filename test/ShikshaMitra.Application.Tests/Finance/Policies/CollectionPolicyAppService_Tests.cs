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

public class CollectionPolicyAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly ICollectionPolicyAppService _collectionPolicies;
	private readonly IStudentFeePolicyOverrideAppService _overrides;
	private readonly IGuidGenerator _guidGenerator;
	private readonly IRepository<AcademicYear, Guid> _academicYearRepository;
	private readonly IRepository<FeeCategory, Guid> _feeCategoryRepository;
	private readonly IRepository<FeeHead, Guid> _feeHeadRepository;
	private readonly IRepository<CollectionPolicy, Guid> _collectionPolicyRepository;

	public CollectionPolicyAppService_Tests()
	{
		_collectionPolicies = GetRequiredService<ICollectionPolicyAppService>();
		_overrides = GetRequiredService<IStudentFeePolicyOverrideAppService>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
		_academicYearRepository = GetRequiredService<IRepository<AcademicYear, Guid>>();
		_feeCategoryRepository = GetRequiredService<IRepository<FeeCategory, Guid>>();
		_feeHeadRepository = GetRequiredService<IRepository<FeeHead, Guid>>();
		_collectionPolicyRepository = GetRequiredService<IRepository<CollectionPolicy, Guid>>();
	}

	private async Task<CreateUpdateCollectionPolicyDto> BuildInputAsync(
		Guid academicYearId, string name, bool isDefault = false)
	{
		if (await _academicYearRepository.FindAsync(academicYearId) is null)
		{
			await _academicYearRepository.InsertAsync(new AcademicYear(
				academicYearId, null, $"Test {academicYearId:N}",
				new DateTime(2025, 4, 1), new DateTime(2026, 3, 31)), autoSave: true);
		}

		var categoryId = _guidGenerator.Create();
		var lateFeeHeadId = _guidGenerator.Create();
		var waiverFeeHeadId = _guidGenerator.Create();
		await _feeCategoryRepository.InsertAsync(new FeeCategory(
			categoryId, null, academicYearId, $"Category {categoryId:N}", null), autoSave: true);
		var incomeCategoryId = await ShikshaMitra.Finance.Fees.FeeHeadAccountingTestData.CreateIncomeCategoryAsync(
			GetRequiredService<IRepository<ShikshaMitra.Domain.Finance.Accounts.Settings.AccountHead, Guid>>(),
			GetRequiredService<IRepository<ShikshaMitra.Domain.Finance.Accounts.Settings.TransactionCategory, Guid>>(),
			academicYearId, null, "Policy Fee");
		await _feeHeadRepository.InsertAsync(new FeeHead(
			lateFeeHeadId, null, academicYearId, categoryId, "Tuition Fee",
			FeeHeadModule.Finance, false, null, incomeCategoryId), autoSave: true);
		await _feeHeadRepository.InsertAsync(new FeeHead(
			waiverFeeHeadId, null, academicYearId, categoryId, "Transport Fee",
			FeeHeadModule.Finance, false, null, incomeCategoryId), autoSave: true);

		return new CreateUpdateCollectionPolicyDto
		{
			AcademicYearId = academicYearId,
			PolicyName = name,
			PolicyCode = $"CP-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
			Description = "Standard late fee policy for grades 1-5",
			Status = "Active",
			IsDefault = isDefault,
			EnableLateFee = true,
			GracePeriodDays = 7,
			LateFeeApplyOnFeeHeads = new() { lateFeeHeadId, waiverFeeHeadId },
			LateFeeType = "PerDay",
			AmountPerDay = 20,
			PercentageBase = "OutstandingAmount",
			CompoundFrequency = "Daily",
			RoundingMethod = "RoundUp",
			MaximumLateFeeAmount = 2000,
			EnableWaiver = true,
			AllowOnlineCollection = true
		};
	}

	[Fact]
	public async Task Create_Then_Get_Should_Round_Trip_All_Fields()
	{
		var academicYearId = _guidGenerator.Create();
		var input = await BuildInputAsync(academicYearId, "Standard Late Fee Policy");

		var created = await _collectionPolicies.CreateAsync(input);

		created.Id.ShouldNotBe(Guid.Empty);
		created.PolicyName.ShouldBe("Standard Late Fee Policy");
		created.GracePeriodDays.ShouldBe(7);
		created.LateFeeType.ShouldBe("PerDay");
		created.AmountPerDay.ShouldBe(20);
		created.MaximumLateFeeAmount.ShouldBe(2000);
		created.LateFeeApplyOnFeeHeads.Count.ShouldBe(1);
		created.Version.ShouldBe(1); // ctor calls Update() once, same as AttendancePolicy's pattern
		var stored = await _collectionPolicyRepository.GetAsync(created.Id);
		stored.EffectiveFrom.ShouldBe(new DateTime(2025, 4, 1));
		stored.EffectiveTo.ShouldBe(new DateTime(2026, 3, 31));

		var fetched = await _collectionPolicies.GetAsync(created.Id);
		fetched.PolicyName.ShouldBe(created.PolicyName);
		fetched.AmountPerDay.ShouldBe(20);
		fetched.LateFeeApplyOnFeeHeads.ShouldBe(created.LateFeeApplyOnFeeHeads);
	}

	[Fact]
	public async Task List_Should_Separate_Current_And_Archived_Policies()
	{
		var academicYearId = _guidGenerator.Create();
		var current = await _collectionPolicies.CreateAsync(
			await BuildInputAsync(academicYearId, "Current Collection Policy"));
		var archived = await _collectionPolicies.CreateAsync(
			await BuildInputAsync(academicYearId, "Archived Collection Policy"));
		await _collectionPolicies.ArchiveAsync(archived.Id);

		var currentPage = await _collectionPolicies.GetListAsync(new CollectionPolicyListRequestDto
		{
			AcademicYearId = academicYearId,
			MaxResultCount = 20
		});
		var archivedPage = await _collectionPolicies.GetListAsync(new CollectionPolicyListRequestDto
		{
			AcademicYearId = academicYearId,
			Status = "Archived",
			IncludeArchived = true,
			MaxResultCount = 20
		});

		currentPage.Items.ShouldContain(x => x.Id == current.Id);
		currentPage.Items.ShouldNotContain(x => x.Id == archived.Id);
		archivedPage.Items.ShouldContain(x => x.Id == archived.Id);
		archivedPage.Items.ShouldNotContain(x => x.Id == current.Id);
	}

	[Fact]
	public async Task Update_Should_Increment_Version_And_Write_A_Version_Snapshot()
	{
		var academicYearId = _guidGenerator.Create();
		var created = await _collectionPolicies.CreateAsync(await BuildInputAsync(academicYearId, "Grace Period Policy"));

		var updateInput = await BuildInputAsync(academicYearId, "Grace Period Policy");
		updateInput.GracePeriodDays = 14;
		updateInput.ChangeReason = "Extended the grace period after fee committee review.";
		var updated = await _collectionPolicies.UpdateAsync(created.Id, updateInput);

		updated.GracePeriodDays.ShouldBe(14);
		updated.Version.ShouldBe(2);

		var versions = await _collectionPolicies.GetVersionsAsync(created.Id, new FinancePolicyVersionListRequestDto());
		versions.TotalCount.ShouldBe(2);
		versions.Items.OrderBy(v => v.VersionNumber).Select(v => v.ChangeAction).ShouldBe(new[] { "created", "edited" });
		versions.Items.Single(v => v.VersionNumber == 2).ChangeReason.ShouldBe(updateInput.ChangeReason);
	}

	[Fact]
	public async Task Version_History_Should_Record_Archive_And_Unarchive_Actions()
	{
		var academicYearId = _guidGenerator.Create();
		var created = await _collectionPolicies.CreateAsync(
			await BuildInputAsync(academicYearId, "Lifecycle History Policy"));

		await _collectionPolicies.ArchiveAsync(created.Id);
		await _collectionPolicies.UnarchiveAsync(created.Id);

		var history = await _collectionPolicies.GetVersionsAsync(
			created.Id,
			new FinancePolicyVersionListRequestDto { MaxResultCount = 20 });

		history.TotalCount.ShouldBe(3);
		var ordered = history.Items.OrderBy(x => x.VersionNumber).ToList();
		ordered.Select(x => x.VersionNumber).ShouldBe(new[] { 1, 2, 3 });
		ordered.Select(x => x.ChangeAction).ShouldBe(new[] { "created", "archived", "unarchived" });
		ordered.Select(x => x.Status).ShouldBe(new[] { "Active", "Archived", "Draft" });
	}

	[Fact]
	public async Task Version_History_For_Missing_Policy_Should_Return_Not_Found()
	{
		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_collectionPolicies.GetVersionsAsync(
				_guidGenerator.Create(),
				new FinancePolicyVersionListRequestDto()));

		exception.Code.ShouldBe(CollectionPolicyErrorCodes.PolicyNotFound);
	}

	[Fact]
	public async Task Selecting_A_New_Default_Replaces_The_Previous_Default()
	{
		var academicYearId = _guidGenerator.Create();
		var first = await _collectionPolicies.CreateAsync(
			await BuildInputAsync(academicYearId, "First Default Policy", isDefault: true));

		var secondInput = await BuildInputAsync(academicYearId, "Second Default Policy", isDefault: true);
		var second = await _collectionPolicies.CreateAsync(secondInput);

		(await _collectionPolicies.GetAsync(first.Id)).IsDefault.ShouldBeFalse();
		(await _collectionPolicies.GetAsync(second.Id)).IsDefault.ShouldBeTrue();
	}

	[Fact]
	public async Task Slab_Overlap_Is_Rejected_Server_Side()
	{
		var academicYearId = _guidGenerator.Create();
		var input = await BuildInputAsync(academicYearId, "Slab Policy");
		input.LateFeeType = "Slab";
		input.EscalationRules = new()
		{
			new CollectionPolicyEscalationRuleDto { StartDay = 1, EndDay = 10, CalculationType = "Flat", Value = 10 },
			new CollectionPolicyEscalationRuleDto { StartDay = 5, EndDay = 20, CalculationType = "Flat", Value = 20 }
		};

		var ex = await Should.ThrowAsync<BusinessException>(() => _collectionPolicies.CreateAsync(input));
		ex.Code.ShouldBe(CollectionPolicyErrorCodes.InvalidEscalationSlabs);
	}

	[Fact]
	public async Task Active_Policy_Rejects_A_Fee_Head_From_An_Inactive_Category()
	{
		var academicYearId = _guidGenerator.Create();
		var input = await BuildInputAsync(academicYearId, "Inactive category policy");
		var head = await _feeHeadRepository.GetAsync(input.LateFeeApplyOnFeeHeads.First());
		var category = await _feeCategoryRepository.GetAsync(head.FeeCategoryId);
		category.SetStatus(false, "Not selectable");
		await _feeCategoryRepository.UpdateAsync(category, autoSave: true);

		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_collectionPolicies.CreateAsync(input));

		exception.Code.ShouldBe(CollectionPolicyErrorCodes.InvalidFeeHeadReference);
		exception.Data["reason"].ShouldBe("inactive_fee_category");
	}

	[Fact]
	public async Task Archive_Then_Unarchive_Round_Trips_Status()
	{
		var academicYearId = _guidGenerator.Create();
		var created = await _collectionPolicies.CreateAsync(await BuildInputAsync(academicYearId, "Archivable Policy"));

		await _collectionPolicies.ArchiveAsync(created.Id);
		var archived = await _collectionPolicies.GetAsync(created.Id);
		archived.Status.ShouldBe("Archived");

		var restored = await _collectionPolicies.UnarchiveAsync(created.Id);
		restored.Status.ShouldBe("Draft");
	}

	[Fact]
	public async Task Clone_Copies_Fields_And_Forces_Non_Default()
	{
		var academicYearId = _guidGenerator.Create();
		var source = await _collectionPolicies.CreateAsync(await BuildInputAsync(academicYearId, "Source Policy", isDefault: true));

		var cloned = await _collectionPolicies.CloneAsync(source.Id, new CollectionPolicyCloneRequestDto());

		cloned.Id.ShouldNotBe(source.Id);
		cloned.PolicyName.ShouldBe("Source Policy Copy");
		cloned.IsDefault.ShouldBeFalse();
		cloned.GracePeriodDays.ShouldBe(source.GracePeriodDays);
		cloned.AmountPerDay.ShouldBe(source.AmountPerDay);
	}

	[Fact]
	public async Task Student_Override_Requires_A_Reason_When_Waiver_Is_Pre_Approved()
	{
		var studentId = _guidGenerator.Create();
		var feeStructureId = _guidGenerator.Create();

		var ex = await Should.ThrowAsync<BusinessException>(() => _overrides.SetOverrideAsync(
			studentId,
			feeStructureId,
			new CreateUpdateStudentFeePolicyOverrideDto { WaiverPreApproved = true }));

		ex.Code.ShouldBe(CollectionPolicyErrorCodes.WaiverPreApprovalReasonRequired);
	}

	[Fact]
	public async Task Student_Override_Persists_And_Is_Idempotent_On_Second_Set()
	{
		var studentId = _guidGenerator.Create();
		var feeStructureId = _guidGenerator.Create();

		var first = await _overrides.SetOverrideAsync(studentId, feeStructureId, new CreateUpdateStudentFeePolicyOverrideDto
		{
			IsLateFeeExempt = true,
			CustomGracePeriodDays = 20
		});

		var second = await _overrides.SetOverrideAsync(studentId, feeStructureId, new CreateUpdateStudentFeePolicyOverrideDto
		{
			IsLateFeeExempt = false,
			CustomGracePeriodDays = 5
		});

		first.Id.ShouldBe(second.Id);
		second.IsLateFeeExempt.ShouldBeFalse();
		second.CustomGracePeriodDays.ShouldBe(5);

		var fetched = await _overrides.GetOverrideAsync(studentId, feeStructureId);
		fetched.ShouldNotBeNull();
		fetched!.CustomGracePeriodDays.ShouldBe(5);
	}

	[Fact]
	public async Task Simulate_Computes_Late_Fee_Using_The_Same_Service_As_A_Real_Collection()
	{
		var policy = await _collectionPolicies.CreateAsync(await BuildInputAsync(_guidGenerator.Create(), "Simulate PerDay Policy"));

		var result = await _collectionPolicies.SimulateAsync(policy.Id, new CollectionPolicySimulateRequestDto
		{
			DaysLate = 15,
			OutstandingAmount = 5000
		});

		// BuildInput's fixture: PerDay, 20/day, 7-day grace -> (15-7) * 20 = 160
		result.LateFee.ShouldBe(160);
		result.IsExempt.ShouldBeFalse();
		result.Explanation.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Simulate_Reports_Which_Cap_Bound_The_Result()
	{
		var policy = await _collectionPolicies.CreateAsync(await BuildInputAsync(_guidGenerator.Create(), "Simulate Capped Policy"));

		var result = await _collectionPolicies.SimulateAsync(policy.Id, new CollectionPolicySimulateRequestDto
		{
			DaysLate = 200,
			OutstandingAmount = 5000
		});

		// (200-7) * 20 = 3860, capped by BuildInput's MaximumLateFeeAmount = 2000
		result.LateFee.ShouldBe(2000);
		result.CappedBy.ShouldBe("maximumLateFeeAmount");
	}

	[Fact]
	public async Task Simulate_Resolves_Student_Level_Exemption_When_StudentId_Is_Provided()
	{
		var policy = await _collectionPolicies.CreateAsync(await BuildInputAsync(_guidGenerator.Create(), "Simulate Exemption Policy"));
		var studentId = _guidGenerator.Create();
		var feeStructureId = _guidGenerator.Create();

		await _overrides.SetOverrideAsync(studentId, feeStructureId, new CreateUpdateStudentFeePolicyOverrideDto
		{
			IsLateFeeExempt = true
		});

		var result = await _collectionPolicies.SimulateAsync(policy.Id, new CollectionPolicySimulateRequestDto
		{
			DaysLate = 30,
			OutstandingAmount = 5000,
			StudentId = studentId,
			FeeStructureId = feeStructureId
		});

		result.LateFee.ShouldBe(0);
		result.IsExempt.ShouldBeTrue();
	}

	[Fact]
	public async Task Simulate_Includes_A_Waiver_Breakdown_When_A_Requested_Amount_Is_Given()
	{
		var policy = await _collectionPolicies.CreateAsync(await BuildInputAsync(_guidGenerator.Create(), "Simulate Waiver Policy"));

		var result = await _collectionPolicies.SimulateAsync(policy.Id, new CollectionPolicySimulateRequestDto
		{
			DaysLate = 15,
			OutstandingAmount = 5000,
			// Late fee here is 160 (see the PerDay fixture above). The remaining late fee is
			// the only limit applied to the requested waiver.
			RequestedWaiverAmount = 1000
		});

		result.EffectiveWaiverAmount.ShouldBe(160);
		result.WaiverExplanation.ShouldNotBeNullOrWhiteSpace();
	}

	// ── Per-fee-head overrides (Phase 4a) ────────────────────────────────────

	[Fact]
	public async Task FeeHeadOverrides_Round_Trip_Through_Create_And_Get()
	{
		var academicYearId = _guidGenerator.Create();
		var input = await BuildInputAsync(academicYearId, "Policy With Overrides");
		var transportFeeHeadId = input.LateFeeApplyOnFeeHeads.First();
		var libraryFeeHeadId = input.LateFeeApplyOnFeeHeads.Last();
		input.FeeHeadOverrides = new()
		{
			new CollectionPolicyFeeHeadOverrideDto
			{
				FeeHeadId = transportFeeHeadId,
				OverrideMode = "NoLateFee"
			},
			new CollectionPolicyFeeHeadOverrideDto
			{
				FeeHeadId = libraryFeeHeadId,
				OverrideMode = "CustomRule",
				LateFeeRule = new LateFeeRuleOverrideDto
				{
					LateFeeType = "Flat",
					FlatAmount = 50
				}
			}
		};

		var created = await _collectionPolicies.CreateAsync(input);

		created.FeeHeadOverrides.Count.ShouldBe(2);
		var transportOverride = created.FeeHeadOverrides.Single(o => o.FeeHeadId == transportFeeHeadId);
		transportOverride.OverrideMode.ShouldBe("NoLateFee");
		transportOverride.LateFeeRule.ShouldBeNull();

		var libraryOverride = created.FeeHeadOverrides.Single(o => o.FeeHeadId == libraryFeeHeadId);
		libraryOverride.OverrideMode.ShouldBe("CustomRule");
		libraryOverride.LateFeeRule.ShouldNotBeNull();
		libraryOverride.LateFeeRule!.LateFeeType.ShouldBe("Flat");
		libraryOverride.LateFeeRule!.FlatAmount.ShouldBe(50);

		var fetched = await _collectionPolicies.GetAsync(created.Id);
		fetched.FeeHeadOverrides.Count.ShouldBe(2);
	}

	[Fact]
	public async Task Duplicate_FeeHeadId_In_Overrides_Is_Rejected()
	{
		var input = await BuildInputAsync(_guidGenerator.Create(), "Duplicate Override Policy");
		var feeHeadId = input.LateFeeApplyOnFeeHeads.First();
		input.FeeHeadOverrides = new()
		{
			new CollectionPolicyFeeHeadOverrideDto { FeeHeadId = feeHeadId, OverrideMode = "NoLateFee" },
			new CollectionPolicyFeeHeadOverrideDto { FeeHeadId = feeHeadId, OverrideMode = "Inherit" }
		};

		var ex = await Should.ThrowAsync<BusinessException>(() => _collectionPolicies.CreateAsync(input));

		ex.Code.ShouldBe(CollectionPolicyErrorCodes.DuplicateFeeHeadOverride);
	}

	[Fact]
	public async Task CustomRule_Override_Without_A_LateFeeRule_Is_Rejected()
	{
		var input = await BuildInputAsync(_guidGenerator.Create(), "Missing Custom Rule Policy");
		input.FeeHeadOverrides = new()
		{
			new CollectionPolicyFeeHeadOverrideDto
			{
				FeeHeadId = input.LateFeeApplyOnFeeHeads.First(),
				OverrideMode = "CustomRule",
				LateFeeRule = null
			}
		};

		var ex = await Should.ThrowAsync<BusinessException>(() => _collectionPolicies.CreateAsync(input));

		ex.Code.ShouldBe(CollectionPolicyErrorCodes.CustomRuleRequiresLateFeeRule);
	}

	[Fact]
	public async Task Acceptance_Transport_NoLateFee_And_Tuition_Normal_Rule_Both_Resolve_Correctly_Via_Simulate()
	{
		// implementation plan §4a acceptance criteria: tuition on the default rule and transport
		// set to NoLateFee produces zero late fee on transport dues and the normal calculated
		// amount on tuition dues.
		var input = await BuildInputAsync(_guidGenerator.Create(), "Acceptance Scenario Policy");
		var tuitionFeeHeadId = input.LateFeeApplyOnFeeHeads.First();
		var transportFeeHeadId = input.LateFeeApplyOnFeeHeads.Last();
		input.FeeHeadOverrides = new()
		{
			new CollectionPolicyFeeHeadOverrideDto { FeeHeadId = transportFeeHeadId, OverrideMode = "NoLateFee" }
		};
		var policy = await _collectionPolicies.CreateAsync(input);

		var tuitionResult = await _collectionPolicies.SimulateAsync(policy.Id, new CollectionPolicySimulateRequestDto
		{
			DaysLate = 15,
			OutstandingAmount = 5000,
			FeeHeadId = tuitionFeeHeadId
		});
		var transportResult = await _collectionPolicies.SimulateAsync(policy.Id, new CollectionPolicySimulateRequestDto
		{
			DaysLate = 15,
			OutstandingAmount = 5000,
			FeeHeadId = transportFeeHeadId
		});

		// BuildInput's fixture: PerDay, 20/day, 7-day grace -> (15-7) * 20 = 160
		tuitionResult.LateFee.ShouldBe(160);
		tuitionResult.FeeHeadOverrideApplied.ShouldBe("Inherit");

		transportResult.LateFee.ShouldBe(0);
		transportResult.FeeHeadOverrideApplied.ShouldBe("NoLateFee");
	}
}
