using Shouldly;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Guids;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class CollectionPolicyAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly ICollectionPolicyAppService _collectionPolicies;
	private readonly IStudentFeePolicyOverrideAppService _overrides;
	private readonly IGuidGenerator _guidGenerator;

	public CollectionPolicyAppService_Tests()
	{
		_collectionPolicies = GetRequiredService<ICollectionPolicyAppService>();
		_overrides = GetRequiredService<IStudentFeePolicyOverrideAppService>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
	}

	private CreateUpdateCollectionPolicyDto BuildInput(Guid academicYearId, string name, bool isDefault = false) => new()
	{
		AcademicYearId = academicYearId,
		PolicyName = name,
		PolicyCode = $"CP-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
		Description = "Standard late fee policy for grades 1-5",
		Status = "Active",
		IsDefault = isDefault,
		EnableLateFee = true,
		GracePeriodDays = 7,
		LateFeeApplyOnFeeHeads = new() { Guid.NewGuid() },
		LateFeeType = "PerDay",
		AmountPerDay = 20,
		PercentageBase = "OutstandingAmount",
		CompoundFrequency = "Daily",
		RoundingMethod = "RoundUp",
		MaximumLateFeeAmount = 2000,
		EnableWaiver = true,
		WaiverApplyOnFeeHeads = new() { Guid.NewGuid() },
		MaximumWaiverAmount = 500,
		MaximumWaiverPercentage = 50,
		AllowOnlineCollection = true
	};

	[Fact]
	public async Task Create_Then_Get_Should_Round_Trip_All_Fields()
	{
		var academicYearId = _guidGenerator.Create();
		var input = BuildInput(academicYearId, "Standard Late Fee Policy");

		var created = await _collectionPolicies.CreateAsync(input);

		created.Id.ShouldNotBe(Guid.Empty);
		created.PolicyName.ShouldBe("Standard Late Fee Policy");
		created.GracePeriodDays.ShouldBe(7);
		created.LateFeeType.ShouldBe("PerDay");
		created.AmountPerDay.ShouldBe(20);
		created.MaximumLateFeeAmount.ShouldBe(2000);
		created.LateFeeApplyOnFeeHeads.Count.ShouldBe(1);
		created.Version.ShouldBe(1); // ctor calls Update() once, same as AttendancePolicy's pattern

		var fetched = await _collectionPolicies.GetAsync(created.Id);
		fetched.PolicyName.ShouldBe(created.PolicyName);
		fetched.AmountPerDay.ShouldBe(20);
		fetched.LateFeeApplyOnFeeHeads.ShouldBe(created.LateFeeApplyOnFeeHeads);
	}

	[Fact]
	public async Task Update_Should_Increment_Version_And_Write_A_Version_Snapshot()
	{
		var academicYearId = _guidGenerator.Create();
		var created = await _collectionPolicies.CreateAsync(BuildInput(academicYearId, "Grace Period Policy"));

		var updateInput = BuildInput(academicYearId, "Grace Period Policy");
		updateInput.GracePeriodDays = 14;
		var updated = await _collectionPolicies.UpdateAsync(created.Id, updateInput);

		updated.GracePeriodDays.ShouldBe(14);
		updated.Version.ShouldBe(2);

		var versions = await _collectionPolicies.GetVersionsAsync(created.Id, new FinancePolicyVersionListRequestDto());
		versions.TotalCount.ShouldBe(2);
		versions.Items.OrderBy(v => v.VersionNumber).Select(v => v.ChangeAction).ShouldBe(new[] { "created", "edited" });
	}

	[Fact]
	public async Task Only_One_Default_Policy_Is_Allowed_Per_Academic_Year()
	{
		var academicYearId = _guidGenerator.Create();
		await _collectionPolicies.CreateAsync(BuildInput(academicYearId, "First Default Policy", isDefault: true));

		var ex = await Should.ThrowAsync<BusinessException>(() =>
			_collectionPolicies.CreateAsync(BuildInput(academicYearId, "Second Default Policy", isDefault: true)));

		ex.Code.ShouldBe(CollectionPolicyErrorCodes.DuplicateDefaultForAcademicYear);
	}

	[Fact]
	public async Task Slab_Overlap_Is_Rejected_Server_Side()
	{
		var academicYearId = _guidGenerator.Create();
		var input = BuildInput(academicYearId, "Slab Policy");
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
	public async Task Archive_Then_Unarchive_Round_Trips_Status()
	{
		var academicYearId = _guidGenerator.Create();
		var created = await _collectionPolicies.CreateAsync(BuildInput(academicYearId, "Archivable Policy"));

		await _collectionPolicies.ArchiveAsync(created.Id);
		var archived = await _collectionPolicies.GetAsync(created.Id);
		archived.Status.ShouldBe("Archived");

		var restored = await _collectionPolicies.UnarchiveAsync(created.Id);
		restored.Status.ShouldBe("Active");
	}

	[Fact]
	public async Task Clone_Copies_Fields_And_Forces_Non_Default()
	{
		var academicYearId = _guidGenerator.Create();
		var source = await _collectionPolicies.CreateAsync(BuildInput(academicYearId, "Source Policy", isDefault: true));

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
		var policy = await _collectionPolicies.CreateAsync(BuildInput(_guidGenerator.Create(), "Simulate PerDay Policy"));

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
		var policy = await _collectionPolicies.CreateAsync(BuildInput(_guidGenerator.Create(), "Simulate Capped Policy"));

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
		var policy = await _collectionPolicies.CreateAsync(BuildInput(_guidGenerator.Create(), "Simulate Exemption Policy"));
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
		var policy = await _collectionPolicies.CreateAsync(BuildInput(_guidGenerator.Create(), "Simulate Waiver Policy"));

		var result = await _collectionPolicies.SimulateAsync(policy.Id, new CollectionPolicySimulateRequestDto
		{
			DaysLate = 15,
			OutstandingAmount = 5000,
			// Late fee here is 160 (see the PerDay fixture above). Requesting 1000 exceeds both
			// BuildInput's MaximumWaiverAmount (500) and MaximumWaiverPercentage (50% of 160 = 80)
			// — the tighter of the two, 80, is the one that actually binds.
			RequestedWaiverAmount = 1000
		});

		result.EffectiveWaiverAmount.ShouldBe(80);
		result.WaiverExplanation.ShouldNotBeNullOrWhiteSpace();
	}

	// ── Per-fee-head overrides (Phase 4a) ────────────────────────────────────

	[Fact]
	public async Task FeeHeadOverrides_Round_Trip_Through_Create_And_Get()
	{
		var academicYearId = _guidGenerator.Create();
		var transportFeeHeadId = _guidGenerator.Create();
		var libraryFeeHeadId = _guidGenerator.Create();

		var input = BuildInput(academicYearId, "Policy With Overrides");
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
		var feeHeadId = _guidGenerator.Create();
		var input = BuildInput(_guidGenerator.Create(), "Duplicate Override Policy");
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
		var input = BuildInput(_guidGenerator.Create(), "Missing Custom Rule Policy");
		input.FeeHeadOverrides = new()
		{
			new CollectionPolicyFeeHeadOverrideDto
			{
				FeeHeadId = _guidGenerator.Create(),
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
		var tuitionFeeHeadId = _guidGenerator.Create();
		var transportFeeHeadId = _guidGenerator.Create();

		var input = BuildInput(_guidGenerator.Create(), "Acceptance Scenario Policy");
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
