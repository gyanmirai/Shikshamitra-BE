using System;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class StandardAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

	private readonly IStandardAppService _standardAppService;
	private readonly IShiftManagementAppService _shiftManagementAppService;
	private readonly IRepository<Standard, Guid> _standardRepository;
	private readonly ICurrentTenant _currentTenant;

	public StandardAppService_Tests()
	{
		_standardAppService = GetRequiredService<IStandardAppService>();
		_shiftManagementAppService = GetRequiredService<IShiftManagementAppService>();
		_standardRepository = GetRequiredService<IRepository<Standard, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task Create_and_update_should_reject_another_tenants_shift_without_changing_the_standard()
	{
		ShiftManagementDto foreign;
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			foreign = await _shiftManagementAppService.CreateAsync(new CreateUpdateShiftManagementDto {
				ShiftName = "Foreign shift", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(16, 0) });
		}
		var own = await CreateShiftAsync("Own");
		var standard = await CreateStandardAsync(own.Id);
		using (_currentTenant.Change(TestTenantId))
		{
			var input = new CreateUpdateStandardDto {
				Name = "Attempted foreign assignment", AgeLowerLimit = 6,
				EducationMediumStageId = standard.EducationMediumStageId, ShiftId = foreign.Id };
			(await Should.ThrowAsync<BusinessException>(() => _standardAppService.CreateAsync(input)))
				.Code.ShouldBe(HRErrorCodes.AttendancePolicyEvaluationShiftRequired);
			(await Should.ThrowAsync<BusinessException>(() => _standardAppService.UpdateAsync(standard.Id, input)))
				.Code.ShouldBe(HRErrorCodes.AttendancePolicyEvaluationShiftRequired);
			var reloaded = await _standardRepository.GetAsync(standard.Id);
			reloaded.ShiftId.ShouldBe(own.Id);
			reloaded.Name.ShouldBe(standard.Name);
		}
	}

	[Fact]
	public async Task CreateAsync_Should_Reject_Nonexistent_ShiftId()
	{
		var stageId = await EnsureStageExistsAsync();

		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_standardAppService.CreateAsync(new CreateUpdateStandardDto
			{
				Name = $"MissingShiftStd{Guid.NewGuid():N}"[..30],
				AgeLowerLimit = 6,
				EducationMediumStageId = stageId,
				ShiftId = Guid.NewGuid()
			}));

		exception.Code.ShouldBe(HRErrorCodes.AttendancePolicyEvaluationShiftRequired);
	}

	[Fact]
	public async Task UpdateAsync_Should_Reject_Nonexistent_ShiftId()
	{
		var standard = await CreateStandardAsync((await CreateShiftAsync("Valid")).Id);

		var exception = await Should.ThrowAsync<BusinessException>(() =>
			_standardAppService.UpdateAsync(standard.Id, new CreateUpdateStandardDto
			{
				Name = standard.Name,
				AgeLowerLimit = standard.AgeLowerLimit,
				EducationMediumStageId = standard.EducationMediumStageId,
				ShiftId = Guid.NewGuid()
			}));

		exception.Code.ShouldBe(HRErrorCodes.AttendancePolicyEvaluationShiftRequired);
	}

	[Fact]
	public async Task UpdateAsync_Should_Clear_Shift_When_ShiftId_Is_Null()
	{
		var shift = await CreateShiftAsync("Clear");
		var standard = await CreateStandardAsync(shift.Id);

		using (_currentTenant.Change(TestTenantId))
		{
			await _standardAppService.UpdateAsync(standard.Id, new CreateUpdateStandardDto
			{
				Name = standard.Name,
				AgeLowerLimit = standard.AgeLowerLimit,
				EducationMediumStageId = standard.EducationMediumStageId,
				ShiftId = null
			});

			var reloaded = await _standardRepository.GetAsync(standard.Id);
			reloaded.ShiftId.ShouldBeNull();
		}
	}

	private async Task<Standard> CreateStandardAsync(Guid shiftId)
	{
		using (_currentTenant.Change(TestTenantId))
		{
			var created = await _standardAppService.CreateAsync(new CreateUpdateStandardDto
			{
				Name = $"Std{Guid.NewGuid():N}"[..30],
				AgeLowerLimit = 6,
				EducationMediumStageId = await EnsureStageExistsAsync(),
				ShiftId = shiftId
			});

			return await _standardRepository.GetAsync(created.Id);
		}
	}

	private async Task<ShiftManagementDto> CreateShiftAsync(string prefix)
	{
		using (_currentTenant.Change(TestTenantId))
		{
			return await _shiftManagementAppService.CreateAsync(new CreateUpdateShiftManagementDto
			{
				ShiftName = $"{prefix} Shift {Guid.NewGuid():N}",
				StartTime = new TimeOnly(8, 0),
				EndTime = new TimeOnly(16, 0)
			});
		}
	}

	private Task<Guid> EnsureStageExistsAsync() =>
		EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
}
