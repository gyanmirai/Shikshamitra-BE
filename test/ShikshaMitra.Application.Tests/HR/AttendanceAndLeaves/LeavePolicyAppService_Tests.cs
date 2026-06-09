using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeavePolicyAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly ILeaveTypeAppService _leaveTypeAppService;
	private readonly ILeavePolicyAppService _leavePolicyAppService;
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly AcademicYearManager _academicYearManager;
	private readonly IUnitOfWorkManager _unitOfWorkManager;

	public LeavePolicyAppService_Tests()
	{
		_leaveTypeAppService = GetRequiredService<ILeaveTypeAppService>();
		_leavePolicyAppService = GetRequiredService<ILeavePolicyAppService>();
		_academicYearRepository = GetRequiredService<IAcademicYearRepository>();
		_academicYearManager = GetRequiredService<AcademicYearManager>();
		_unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();
	}

	[Fact]
	public async Task Should_Create_And_Get_Leave_Policy_By_Id()
	{
		var academicYearId = await GetAcademicYearIdAsync();
		var leaveType = await CreateLeaveTypeAsync($"LT-{Guid.NewGuid():N}");

		var created = await _leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
		{
			PolicyTitle = $"Policy-{Guid.NewGuid():N}",
			EffectiveFrom = DateTime.UtcNow.Date,
			Description = "Standard policy",
			Rules =
			[
				Rule(leaveType.Id, leaveCount: 12, accrualPolicyType: "Monthly", carryForward: true, encashmentAllowed: true, encashmentTiming: "end_of_year")
			]
		});

		created.Id.ShouldNotBe(Guid.Empty);
		created.RuleCount.ShouldBe(1);
		created.Rules[0].LeaveCount.ShouldBe(12);
		created.Rules[0].AccrualPolicyType.ShouldBe("Monthly");

		var detail = await _leavePolicyAppService.GetLeavePolicyByIdAsync(created.Id);
		detail.Id.ShouldBe(created.Id);
		detail.Rules[0].LeaveType.Id.ShouldBe(leaveType.Id);
		detail.Rules[0].LeaveType.Name.ShouldBe(leaveType.Name);
	}

	[Fact]
	public async Task Should_Search_Leave_Policy_List()
	{
		var academicYearId = await GetAcademicYearIdAsync();
		var leaveType = await CreateLeaveTypeAsync($"LT-{Guid.NewGuid():N}");
		var marker = $"SearchPolicy-{Guid.NewGuid():N}";

		await _leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
		{
			PolicyTitle = marker,
			EffectiveFrom = DateTime.UtcNow.Date,
			Rules = [Rule(leaveType.Id, leaveCount: 10)]
		});

		var list = await _leavePolicyAppService.GetListByAcademicYearIdAsync(
			academicYearId,
			page: 1,
			pageSize: 10,
			search: marker);

		list.Items.ShouldContain(x => x.PolicyTitle == marker);
	}

	[Fact]
	public async Task Should_Hide_Policy_When_Linked_Leave_Type_Is_Deleted()
	{
		var academicYearId = await GetAcademicYearIdAsync();
		var leaveType = await CreateLeaveTypeAsync($"LT-{Guid.NewGuid():N}");
		var policyTitle = $"DeletedTypePolicy-{Guid.NewGuid():N}";

		var created = await _leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
		{
			PolicyTitle = policyTitle,
			EffectiveFrom = DateTime.UtcNow.Date,
			Rules = [Rule(leaveType.Id, leaveCount: 10)]
		});

		await _leaveTypeAppService.DeleteLeaveTypeAsync(leaveType.Id);

		var list = await _leavePolicyAppService.GetListByAcademicYearIdAsync(
			academicYearId,
			page: 1,
			pageSize: 10,
			search: policyTitle);

		list.TotalCount.ShouldBe(0);
		list.Items.ShouldNotContain(x => x.Id == created.Id);
	}

	[Fact]
	public async Task Should_Update_Accrual_Policy_Type()
	{
		var academicYearId = await GetAcademicYearIdAsync();
		var leaveType = await CreateLeaveTypeAsync($"LT-{Guid.NewGuid():N}");

		var created = await _leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
		{
			PolicyTitle = $"Policy-{Guid.NewGuid():N}",
			EffectiveFrom = DateTime.UtcNow.Date,
			Rules = [Rule(leaveType.Id, accrualPolicyType: "Monthly", leaveCount: 12)]
		});

		var updated = await _leavePolicyAppService.UpdateLeavePolicyAsync(created.Id, new LeavePolicyUpdateRequest
		{
			PolicyTitle = created.PolicyTitle,
			EffectiveFrom = created.EffectiveFrom,
			Rules = [Rule(leaveType.Id, accrualPolicyType: "Yearly", leaveCount: created.Rules[0].LeaveCount)]
		});

		updated.Rules[0].AccrualPolicyType.ShouldBe("Yearly");
		var detail = await _leavePolicyAppService.GetLeavePolicyByIdAsync(created.Id);
		detail.Rules[0].AccrualPolicyType.ShouldBe("Yearly");
	}

	[Fact]
	public async Task Should_Require_Encashment_Timing_When_Encashment_Enabled()
	{
		var academicYearId = await GetAcademicYearIdAsync();
		var leaveType = await CreateLeaveTypeAsync($"LT-{Guid.NewGuid():N}");

		var ex = await Should.ThrowAsync<BusinessException>(() =>
			_leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
			{
				PolicyTitle = $"Policy-{Guid.NewGuid():N}",
				EffectiveFrom = DateTime.UtcNow.Date,
				Rules = [Rule(leaveType.Id, accrualPolicyType: "Monthly", leaveCount: 8, encashmentAllowed: true, encashmentTiming: null)]
			}));

		ex.Code.ShouldBe("ShikshaMitra:EncashmentTimingRequired");
	}

	[Fact]
	public async Task Should_Reject_Duplicate_Active_Rules_In_Same_Policy()
	{
		var academicYearId = await GetAcademicYearIdAsync();
		var leaveType = await CreateLeaveTypeAsync($"LT-{Guid.NewGuid():N}");

		var ex = await Should.ThrowAsync<BusinessException>(() =>
			_leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
			{
				PolicyTitle = $"Policy-{Guid.NewGuid():N}",
				EffectiveFrom = DateTime.UtcNow.Date,
				Rules =
				[
					Rule(leaveType.Id),
					Rule(leaveType.Id)
				]
			}));

		ex.Code.ShouldBe(HRErrorCodes.LeavePolicyDuplicateLeaveTypeRule);
	}

	private static LeavePolicyRuleRequest Rule(
		Guid leaveTypeId,
		string accrualPolicyType = "Yearly",
		int leaveCount = 10,
		string paidType = "Paid",
		bool carryForward = false,
		bool encashmentAllowed = false,
		string? encashmentTiming = null) =>
		new()
		{
			LeaveTypeId = leaveTypeId,
			AccrualPolicyType = accrualPolicyType,
			LeaveCount = leaveCount,
			PaidType = paidType,
			CarryForward = carryForward,
			EncashmentAllowed = encashmentAllowed,
			EncashmentTiming = encashmentTiming,
			RequiresApproval = true,
			MinNoticeDays = 2,
			MaxDaysPerRequest = 30,
			AllowHalfDay = true,
			IsActive = true
		};

	private async Task<LeaveTypeResponse> CreateLeaveTypeAsync(string name)
	{
		var academicYearId = await GetAcademicYearIdAsync();
		return await _leaveTypeAppService.CreateLeaveTypeAsync(academicYearId, new LeaveTypeCreateRequest
		{
			Name = name,
			Color = "#123456"
		});
	}

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
