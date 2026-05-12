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
			LeaveType = leaveType.Id,
			AccrualPolicyType = "Monthly",
			LeaveCount = 12,
			PaidType = "Paid",
			CarryForward = true,
			EncashmentAllowed = true,
			EncashmentTiming = "end_of_year",
			RequiresApproval = true,
			Description = "Standard policy"
		});

		created.Id.ShouldNotBe(Guid.Empty);
		created.LeaveCount.ShouldBe(12);
		created.AccrualPolicyType.ShouldBe("Monthly");

		var detail = await _leavePolicyAppService.GetLeavePolicyByIdAsync(created.Id);
		detail.Id.ShouldBe(created.Id);
		detail.LeaveType.Id.ShouldBe(leaveType.Id);
		detail.LeaveType.Name.ShouldBe(leaveType.Name);
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
			LeaveType = leaveType.Id,
			AccrualPolicyType = "Yearly",
			LeaveCount = 10,
			PaidType = "Paid",
			CarryForward = false,
			EncashmentAllowed = false,
			RequiresApproval = true
		});

		var list = await _leavePolicyAppService.GetListByAcademicYearIdAsync(
			academicYearId,
			page: 1,
			pageSize: 10,
			search: marker);

		list.Items.ShouldContain(x => x.PolicyTitle == marker);
	}

	[Fact]
	public async Task Should_Update_Accrual_Policy_Type()
	{
		var academicYearId = await GetAcademicYearIdAsync();
		var leaveType = await CreateLeaveTypeAsync($"LT-{Guid.NewGuid():N}");

		var created = await _leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, new LeavePolicyCreateRequest
		{
			PolicyTitle = $"Policy-{Guid.NewGuid():N}",
			LeaveType = leaveType.Id,
			AccrualPolicyType = "Monthly",
			LeaveCount = 12,
			PaidType = "Paid",
			CarryForward = false,
			EncashmentAllowed = false,
			RequiresApproval = true
		});

		var updated = await _leavePolicyAppService.UpdateLeavePolicyAsync(created.Id, new LeavePolicyUpdateRequest
		{
			PolicyTitle = created.PolicyTitle,
			LeaveType = leaveType.Id,
			AccrualPolicyType = "Yearly",
			LeaveCount = created.LeaveCount,
			PaidType = created.PaidType,
			CarryForward = created.CarryForward,
			EncashmentAllowed = false,
			RequiresApproval = created.RequiresApproval
		});

		updated.AccrualPolicyType.ShouldBe("Yearly");
		var detail = await _leavePolicyAppService.GetLeavePolicyByIdAsync(created.Id);
		detail.AccrualPolicyType.ShouldBe("Yearly");
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
				LeaveType = leaveType.Id,
				AccrualPolicyType = "Monthly",
				LeaveCount = 8,
				PaidType = "Paid",
				CarryForward = false,
				EncashmentAllowed = true,
				EncashmentTiming = null,
				RequiresApproval = true
			}));

		ex.Code.ShouldBe("ShikshaMitra:EncashmentTimingRequired");
	}

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
