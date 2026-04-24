using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Settings;

[RemoteService(false)]
[Authorize]
public class DayCalculationSettingAppService : ApplicationService, IDayCalculationSettingAppService
{
	private readonly IDayCalculationSettingRepository _repository;

	public DayCalculationSettingAppService(IDayCalculationSettingRepository repository)
	{
		_repository = repository;
	}

	public async Task<DayCalculationResponseDto> SaveAsync(DayCalculationRequestDto input)
	{
		var dayCalcType = ParseDayCalculationType(input.DayCalculationType);

		var existing = await _repository.FindByTenantAsync(CurrentTenant.Id);

		if (existing != null)
		{
			existing.Update(dayCalcType, input.ManualDays);
			await _repository.UpdateAsync(existing, autoSave: true);
		}
		else
		{
			var setting = new DayCalculationSetting(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				dayCalcType,
				input.ManualDays);
			await _repository.InsertAsync(setting, autoSave: true);
		}

		return new DayCalculationResponseDto
		{
			Success = true,
			Message = "Attendance configuration saved successfully"
		};
	}

	private static DayCalculationType ParseDayCalculationType(string value)
	{
		return (value ?? string.Empty).Trim().ToLowerInvariant() switch
		{
			"working-days" => DayCalculationType.WorkingDays,
			"total-month-days" => DayCalculationType.TotalMonthDays,
			"manual-days" => DayCalculationType.ManualDays,
			_ => throw new BusinessException("ShikshaMitra:InvalidDayCalculationType")
				.WithData("message", "dayCalculationType must be one of: working-days, total-month-days, manual-days.")
		};
	}
}
