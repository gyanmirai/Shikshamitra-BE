using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Validation;
using Volo.Abp.Uow;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeavePolicyAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly ILeavePolicyAppService _leavePolicyAppService;
	private readonly ILeavePolicyRepository _leavePolicyRepository;
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly AcademicYearManager _academicYearManager;
	private readonly IUnitOfWorkManager _unitOfWorkManager;

	public LeavePolicyAppService_Tests()
	{
		_leavePolicyAppService = GetRequiredService<ILeavePolicyAppService>();
		_leavePolicyRepository = GetRequiredService<ILeavePolicyRepository>();
		_academicYearRepository = GetRequiredService<IAcademicYearRepository>();
		_academicYearManager = GetRequiredService<AcademicYearManager>();
		_unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();
	}

	[Fact]
	public async Task Should_Create_And_Get_Leave_Policy_By_Id()
	{
		var academicYearId = await GetAcademicYearIdAsync();

		var created = await _leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
		{
			PolicyTitle = $"Policy-{Guid.NewGuid():N}",
			EffectiveFrom = DateTime.UtcNow.Date,
			Description = "Standard policy",
			Rules =
			[
				Rule(leaveCount: 12, accrualPolicyType: "Monthly", carryForward: true, carryForwardLimit: 6, encashmentAllowed: true, encashmentTiming: "end_of_year"),
				Rule(paidType: "Unpaid", entitlementMode: "Unlimited", leaveCount: null, allowNegativeBalance: true)
			]
		});

		created.Id.ShouldNotBe(Guid.Empty);
		created.RuleCount.ShouldBe(2);
		created.Rules.ShouldContain(rule =>
			rule.PaidType == "Paid" &&
			rule.LeaveCount == 12 &&
			rule.EntitlementMode == "Limited" &&
			rule.CarryForward &&
			rule.CarryForwardLimit == 6 &&
			rule.EncashmentAllowed &&
			rule.EncashmentTiming == "end_of_year");
		created.Rules.ShouldContain(rule => rule.PaidType == "Unpaid" && rule.LeaveCount == null && rule.IsUnlimited);

		var detail = await _leavePolicyAppService.GetLeavePolicyByIdAsync(created.Id);
		detail.Id.ShouldBe(created.Id);
		detail.Rules.Count.ShouldBe(2);
		detail.Rules.ShouldContain(rule =>
			rule.PaidType == "Paid" &&
			rule.CarryForward &&
			rule.CarryForwardLimit == 6 &&
			rule.EncashmentAllowed &&
			rule.EncashmentTiming == "end_of_year");
		detail.Rules.ShouldContain(rule => rule.PaidType == "Unpaid" && rule.EntitlementMode == "Unlimited");
	}

	[Fact]
	public async Task Should_Search_Leave_Policy_List()
	{
		var academicYearId = await GetAcademicYearIdAsync();
		var marker = $"SearchPolicy-{Guid.NewGuid():N}";

		await _leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
		{
			PolicyTitle = marker,
			EffectiveFrom = DateTime.UtcNow.Date,
			Rules = DefaultRules()
		});

		var list = await _leavePolicyAppService.GetListByAcademicYearIdAsync(
			academicYearId,
			page: 1,
			pageSize: 10,
			search: marker);

		list.Items.ShouldContain(x => x.PolicyTitle == marker);
	}

	[Fact]
	public async Task Should_List_Leave_Policies_With_Max_Page_Size()
	{
		var academicYearId = await GetAcademicYearIdAsync();
		var marker = $"MaxPagePolicy-{Guid.NewGuid():N}";

		await _leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
		{
			PolicyTitle = marker,
			EffectiveFrom = DateTime.UtcNow.Date,
			Rules = DefaultRules()
		});

		var list = await _leavePolicyAppService.GetListByAcademicYearIdAsync(
			academicYearId,
			page: 1,
			pageSize: 500,
			search: marker);

		list.PageSize.ShouldBe(500);
		list.Items.ShouldContain(x => x.PolicyTitle == marker);
	}

	[Fact]
	public async Task Should_Page_Current_And_Archived_Policies_Separately_And_Restore_As_Draft()
	{
		var academicYearId = await GetAcademicYearIdAsync();
		var marker = $"ArchivePolicy-{Guid.NewGuid():N}";
		var created = await _leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
		{
			PolicyTitle = marker,
			Status = "Draft",
			EffectiveFrom = DateTime.UtcNow.Date,
			Rules = DefaultRules()
		});

		var current = await _leavePolicyAppService.GetListByAcademicYearIdAsync(
			academicYearId,
			page: 1,
			pageSize: 10,
			search: marker,
			archivedOnly: false);
		current.Items.ShouldContain(x => x.Id == created.Id);

		await _leavePolicyAppService.DeleteLeavePolicyAsync(created.Id);

		current = await _leavePolicyAppService.GetListByAcademicYearIdAsync(
			academicYearId,
			page: 1,
			pageSize: 10,
			search: marker,
			archivedOnly: false);
		current.Items.ShouldNotContain(x => x.Id == created.Id);

		var archived = await _leavePolicyAppService.GetListByAcademicYearIdAsync(
			academicYearId,
			page: 1,
			pageSize: 10,
			search: marker,
			archivedOnly: true);
		archived.Items.ShouldContain(x => x.Id == created.Id && x.Status == "Archived");

		var restored = await _leavePolicyAppService.UnarchiveAsync(created.Id);
		restored.Status.ShouldBe("Draft");

		archived = await _leavePolicyAppService.GetListByAcademicYearIdAsync(
			academicYearId,
			page: 1,
			pageSize: 10,
			search: marker,
			archivedOnly: true);
		archived.Items.ShouldNotContain(x => x.Id == created.Id);
	}

	[Fact]
	public async Task Should_Reject_Case_Insensitive_Leave_Policy_Name_Duplicates()
	{
		var academicYearId = await GetAcademicYearIdAsync();
		var marker = $"CasePolicy-{Guid.NewGuid():N}";

		await _leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
		{
			PolicyTitle = marker,
			Status = "Draft",
			EffectiveFrom = DateTime.UtcNow.Date,
			Rules = DefaultRules()
		});

		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
			{
				PolicyTitle = marker.ToLowerInvariant(),
				Status = "Draft",
				EffectiveFrom = DateTime.UtcNow.Date,
				Rules = DefaultRules()
			}));

		exception.Code.ShouldBe(HRErrorCodes.LeavePolicyNameAlreadyExists);
	}

	[Fact]
	public async Task Normal_Create_And_Update_Should_Require_The_Dedicated_Archive_Workflow()
	{
		var academicYearId = await GetAcademicYearIdAsync();
		var createException = await Should.ThrowAsync<BusinessException>(() =>
			_leavePolicyAppService.CreateLeavePolicyAsync(
				academicYearId,
				new LeavePolicyCreateRequest
				{
					PolicyTitle = $"ArchivedCreate-{Guid.NewGuid():N}",
					Status = "Archived",
					EffectiveFrom = DateTime.UtcNow.Date,
					Rules = DefaultRules()
				}));
		createException.Code.ShouldBe(HRErrorCodes.PolicyArchiveRequiresArchiveWorkflow);

		var created = await _leavePolicyAppService.CreateLeavePolicyAsync(
			academicYearId,
			new LeavePolicyCreateRequest
			{
				PolicyTitle = $"ArchivedUpdate-{Guid.NewGuid():N}",
				Status = "Draft",
				EffectiveFrom = DateTime.UtcNow.Date,
				Rules = DefaultRules()
			});
		var updateException = await Should.ThrowAsync<BusinessException>(() =>
			_leavePolicyAppService.UpdateLeavePolicyAsync(
				created.Id,
				new LeavePolicyUpdateRequest
				{
					PolicyTitle = created.PolicyTitle,
					Status = "Archived",
					EffectiveFrom = created.EffectiveFrom,
					Rules = DefaultRules()
				}));

		updateException.Code.ShouldBe(HRErrorCodes.PolicyArchiveRequiresArchiveWorkflow);
		(await _leavePolicyAppService.GetLeavePolicyByIdAsync(created.Id))
			.Status.ShouldBe("Draft");
	}

	[Fact]
	public async Task Normal_Update_Should_Not_Bypass_The_Dedicated_Unarchive_Workflow()
	{
		var academicYearId = await GetAcademicYearIdAsync();
		var created = await _leavePolicyAppService.CreateLeavePolicyAsync(
			academicYearId,
			new LeavePolicyCreateRequest
			{
				PolicyTitle = $"UnarchiveBypass-{Guid.NewGuid():N}",
				Status = "Draft",
				EffectiveFrom = DateTime.UtcNow.Date,
				Rules = DefaultRules()
			});
		await _leavePolicyAppService.DeleteLeavePolicyAsync(created.Id);

		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_leavePolicyAppService.UpdateLeavePolicyAsync(
				created.Id,
				new LeavePolicyUpdateRequest
				{
					PolicyTitle = created.PolicyTitle,
					Status = "Draft",
					EffectiveFrom = created.EffectiveFrom,
					Rules = DefaultRules()
				}));

		exception.Code.ShouldBe(HRErrorCodes.ArchivedPolicyCannotBeEdited);
	}

	[Fact]
	public async Task Should_Reject_Unarchive_When_Current_Name_Is_Already_In_Use()
	{
		var academicYearId = await GetAcademicYearIdAsync();
		var marker = $"ReusePolicy-{Guid.NewGuid():N}";
		var archived = await _leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
		{
			PolicyTitle = marker,
			Status = "Draft",
			EffectiveFrom = DateTime.UtcNow.Date,
			Rules = DefaultRules()
		});
		await _leavePolicyAppService.DeleteLeavePolicyAsync(archived.Id);

		await _leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
		{
			PolicyTitle = marker.ToLowerInvariant(),
			Status = "Draft",
			EffectiveFrom = DateTime.UtcNow.Date,
			Rules = DefaultRules()
		});

		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_leavePolicyAppService.UnarchiveAsync(archived.Id));

		exception.Code.ShouldBe(HRErrorCodes.LeavePolicyNameAlreadyExists);
	}

	[Fact]
	public async Task Name_Lookup_For_Seed_Assignments_Should_Return_Only_The_Active_Current_Policy()
	{
		var academicYearId = await GetAcademicYearIdAsync();
		var marker = $"SeedLookupPolicy-{Guid.NewGuid():N}";
		var archived = await _leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
		{
			PolicyTitle = marker,
			Status = "Draft",
			EffectiveFrom = DateTime.UtcNow.Date,
			Rules = DefaultRules()
		});
		await _leavePolicyAppService.DeleteLeavePolicyAsync(archived.Id);

		var current = await _leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
		{
			PolicyTitle = marker.ToLowerInvariant(),
			Status = "Active",
			EffectiveFrom = DateTime.UtcNow.Date,
			Rules = DefaultRules()
		});

		var found = await _leavePolicyRepository.FindByPolicyNameAsync(academicYearId, marker);

		found.ShouldNotBeNull();
		found!.Id.ShouldBe(current.Id);
		found.Status.ShouldBe(HrPolicyStatus.Active);
		found.IsActive.ShouldBeTrue();
	}

	[Fact]
	public async Task Should_Update_Accrual_Policy_Type_Without_Replacing_Unreferenced_Rules()
	{
		var academicYearId = await GetAcademicYearIdAsync();

		var created = await _leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
		{
			PolicyTitle = $"Policy-{Guid.NewGuid():N}",
			EffectiveFrom = DateTime.UtcNow.Date,
			Rules = DefaultRules()
		});
		var originalPaidRuleId = created.Rules.Single(rule => rule.PaidType == "Paid").Id;

		var update = new LeavePolicyUpdateRequest
		{
			PolicyTitle = created.PolicyTitle,
			Status = created.Status,
			EffectiveFrom = created.EffectiveFrom,
			EffectiveTo = created.EffectiveTo,
			IsDefault = created.IsDefault,
			Description = created.Description,
			Rules =
			[
				Rule(accrualPolicyType: "Monthly", leaveCount: 12),
				Rule(paidType: "Unpaid", entitlementMode: "Unlimited", leaveCount: null, allowNegativeBalance: true)
			]
		};
		var accessor = GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
		var previous = accessor.HttpContext;
		accessor.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
		LeavePolicyDetailResponse updated;
		try
		{
			var preview = await GetRequiredService<ILeavePolicyImpactAppService>().PreviewAsync(new()
				{ AcademicYearId = academicYearId, PolicyId = created.Id, Action = "update", AsOfDate = GetRequiredService<IClock>().Now.Date, Proposal = update });
			accessor.HttpContext.Request.Headers[HrPolicyImpactConfirmationService.HeaderName] = preview.ConfirmationToken;
			updated = await _leavePolicyAppService.UpdateLeavePolicyAsync(created.Id, update);
		}
		finally { accessor.HttpContext = previous; }

		var updatedPaidRule = updated.Rules.Single(rule => rule.PaidType == "Paid");
		updatedPaidRule.AccrualPolicyType.ShouldBe("Monthly");
		updatedPaidRule.Id.ShouldBe(originalPaidRuleId);
	}

	[Fact]
	public async Task Should_Require_Encashment_Timing_When_Encashment_Enabled()
	{
		var academicYearId = await GetAcademicYearIdAsync();

		var ex = await Should.ThrowAsync<AbpValidationException>(() =>
			_leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
			{
				PolicyTitle = $"Policy-{Guid.NewGuid():N}",
				EffectiveFrom = DateTime.UtcNow.Date,
				Rules =
				[
					Rule(leaveCount: 8, encashmentAllowed: true, encashmentTiming: null),
					Rule(paidType: "Unpaid", entitlementMode: "Unlimited", leaveCount: null)
				]
			}));

		ex.ValidationErrors.ShouldContain(error =>
			error.MemberNames.Any(memberName => memberName.Contains(nameof(LeavePolicyRuleRequest.EncashmentTiming))));
	}

	[Fact]
	public async Task Should_Reject_Duplicate_Active_Paid_Type_Rules()
	{
		var academicYearId = await GetAcademicYearIdAsync();

		await Should.ThrowAsync<AbpValidationException>(() =>
			_leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
			{
				PolicyTitle = $"Policy-{Guid.NewGuid():N}",
				EffectiveFrom = DateTime.UtcNow.Date,
				Rules =
				[
					Rule(leaveCount: 10),
					Rule(leaveCount: 8)
				]
			}));
	}

	[Fact]
	public async Task Should_Reject_Paid_Only_Controls_On_Unpaid_Rule()
	{
		var academicYearId = await GetAcademicYearIdAsync();

		await Should.ThrowAsync<AbpValidationException>(() =>
			_leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
			{
				PolicyTitle = $"Policy-{Guid.NewGuid():N}",
				EffectiveFrom = DateTime.UtcNow.Date,
				Rules =
				[
					Rule(leaveCount: 10),
					Rule(paidType: "Unpaid", leaveCount: 5, carryForward: true)
				]
			}));
	}

	[Theory]
	[InlineData(-5, 0, true, true, false)]
	[InlineData(1, 10, true, true, false)]
	[InlineData(-10, -1, true, false, false)]
	[InlineData(-5, 5, false, false, false)]
	[InlineData(-5, 5, true, false, true)]
	public async Task Archive_Should_Block_Only_Current_Or_Future_Active_Assignments(
		int startOffset, int endOffset, bool active, bool shouldBlock, bool foreignTenant)
	{
		// Use the same configured clock as archive validation, including across UTC/local midnight.
		var today = GetRequiredService<IClock>().Now.Date;
		var academicYearId = await GetAcademicYearIdAsync();
		await _leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
		{
			PolicyTitle = $"Default-{Guid.NewGuid():N}",
			Status = "Active",
			IsDefault = true,
			EffectiveFrom = DateTime.UtcNow.Date,
			Rules = DefaultRules()
		});
		var policy = await _leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
		{
			PolicyTitle = $"Assigned-{Guid.NewGuid():N}",
			Status = "Active",
			EffectiveFrom = DateTime.UtcNow.Date,
			Rules = DefaultRules()
		});
		var assignments = GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var tenant = GetRequiredService<ICurrentTenant>();
		var assignment = new HrPolicyAssignment(Guid.NewGuid(), foreignTenant ? Guid.NewGuid() : tenant.Id, academicYearId,
			HrPolicyAssignmentTargetType.Employee, Guid.NewGuid().ToString(), "Synthetic staff",
			Guid.NewGuid(), Guid.NewGuid(), policy.Id, today.AddDays(startOffset),
			today.AddDays(endOffset), active);
		using (var uow = _unitOfWorkManager.Begin(requiresNew: true))
		{
			await assignments.InsertAsync(assignment, autoSave: true);
			await uow.CompleteAsync();
		}
		if (shouldBlock)
		{
			var error = await Should.ThrowAsync<BusinessException>(() => _leavePolicyAppService.DeleteLeavePolicyAsync(policy.Id));
			error.Code.ShouldBe(HRErrorCodes.LeavePolicyHasActiveAssignments);
			error.Data["assignmentCount"].ShouldBe(1);
			(await _leavePolicyAppService.GetLeavePolicyByIdAsync(policy.Id)).Status.ShouldBe("Active");
		}
		else
		{
			var accessor = GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
			var previous = accessor.HttpContext;
			accessor.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
			try
			{
				var preview = await GetRequiredService<ILeavePolicyImpactAppService>().PreviewAsync(new()
					{ AcademicYearId = academicYearId, PolicyId = policy.Id, Action = "archive", AsOfDate = today });
				accessor.HttpContext.Request.Headers[HrPolicyImpactConfirmationService.HeaderName] = preview.ConfirmationToken;
				await _leavePolicyAppService.DeleteLeavePolicyAsync(policy.Id);
			}
			finally { accessor.HttpContext = previous; }
			(await _leavePolicyAppService.GetLeavePolicyByIdAsync(policy.Id)).Status.ShouldBe("Archived");
		}
		var after = await _leavePolicyAppService.GetLeavePolicyByIdAsync(policy.Id);
		after.Rules.Select(rule => rule.Id).OrderBy(id => id).ShouldBe(policy.Rules.Select(rule => rule.Id).OrderBy(id => id));
		using (var uow = _unitOfWorkManager.Begin(requiresNew: true))
		{
			using var referenceTenant = tenant.Change(assignment.TenantId);
			var preserved = await assignments.GetAsync(assignment.Id);
			preserved.LeavePolicyId.ShouldBe(policy.Id);
			preserved.IsActive.ShouldBe(active);
			await uow.CompleteAsync();
		}
	}

	private static List<LeavePolicyRuleRequest> DefaultRules() =>
	[
		Rule(leaveCount: 12),
		Rule(paidType: "Unpaid", entitlementMode: "Unlimited", leaveCount: null, allowNegativeBalance: true)
	];

	private static LeavePolicyRuleRequest Rule(
		string paidType = "Paid",
		string entitlementMode = "Limited",
		string accrualPolicyType = "Yearly",
		int? leaveCount = 10,
		bool carryForward = false,
		int? carryForwardLimit = null,
		bool encashmentAllowed = false,
		string? encashmentTiming = null,
		bool allowNegativeBalance = false) =>
		new()
		{
			PaidType = paidType,
			EntitlementMode = entitlementMode,
			AccrualPolicyType = accrualPolicyType,
			LeaveCount = leaveCount,
			CarryForward = carryForward,
			CarryForwardLimit = carryForwardLimit,
			EncashmentAllowed = encashmentAllowed,
			EncashmentTiming = encashmentTiming,
			RequiresApproval = true,
			MinNoticeDays = 2,
			MaxDaysPerRequest = 30,
			AllowHalfDay = true,
			AllowNegativeBalance = allowNegativeBalance,
			IsActive = true
		};

	private async Task<Guid> GetAcademicYearIdAsync()
	{
		using var uow = _unitOfWorkManager.Begin(requiresNew: true);
		var query = await _academicYearRepository.GetQueryableAsync();
		var academicYearId = query.Select(x => x.Id).FirstOrDefault();
		if (academicYearId == Guid.Empty)
		{
			var now = DateTime.UtcNow;
			var created = await _academicYearManager.CreateAsync(
				$"AY-{Guid.NewGuid():N}",
				new DateTime(now.Year, 4, 1),
				new DateTime(now.Year + 1, 3, 31));
			await _academicYearRepository.InsertAsync(created, autoSave: true);
			academicYearId = created.Id;
		}
		await uow.CompleteAsync();
		academicYearId.ShouldNotBe(Guid.Empty);
		return academicYearId;
	}
}
