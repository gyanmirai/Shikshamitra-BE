using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Validation;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeavePolicyAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly ILeavePolicyAppService _leavePolicyAppService;
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly AcademicYearManager _academicYearManager;
	private readonly IUnitOfWorkManager _unitOfWorkManager;

	public LeavePolicyAppService_Tests()
	{
		_leavePolicyAppService = GetRequiredService<ILeavePolicyAppService>();
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

		var updated = await _leavePolicyAppService.UpdateLeavePolicyAsync(created.Id, new LeavePolicyUpdateRequest
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
		});

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
