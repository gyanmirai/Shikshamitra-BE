using System;
using System.Globalization;
using System.Threading.Tasks;
using ShikshaMitra.HR;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Security.Claims;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
public class CustomAttendanceRuleAppService(
	ICustomAttendanceRuleRepository customAttendanceRuleRepository)
	: CrudAppService<CustomAttendanceRule, CustomAttendanceRuleDto, Guid, PagedAndSortedResultRequestDto,
		CreateUpdateCustomAttendanceRuleDto>(customAttendanceRuleRepository), ICustomAttendanceRuleAppService
{
	private Guid? ResolveTenantId()
	{
		if (CurrentTenant.Id.HasValue)
		{
			return CurrentTenant.Id;
		}

		var claim = CurrentUser.FindClaim(AbpClaimTypes.TenantId)?.Value;
		return Guid.TryParse(claim, CultureInfo.InvariantCulture, out var id) ? id : null;
	}

	protected override async Task<CustomAttendanceRule> MapToEntityAsync(CreateUpdateCustomAttendanceRuleDto createInput)
	{
		var tenantId = ResolveTenantId();
		if (!tenantId.HasValue)
		{
			throw new BusinessException(HRErrorCodes.AttendanceRuleTenantRequired);
		}

		var entity = CustomAttendanceRule.Create(
			GuidGenerator.Create(),
			tenantId.Value,
			createInput.Name,
			createInput.Type,
			createInput.LateThreshold,
			createInput.LateThresholdUnit,
			createInput.HalfDayThreshold,
			createInput.HalfDayThresholdUnit,
			createInput.EnableNotificationForUpperAuthority);

		return await Task.FromResult(entity);
	}

	protected override async Task MapToEntityAsync(CreateUpdateCustomAttendanceRuleDto updateInput, CustomAttendanceRule entity)
	{
		entity.UpdateConfiguration(
			updateInput.Name,
			updateInput.Type,
			updateInput.LateThreshold,
			updateInput.LateThresholdUnit,
			updateInput.HalfDayThreshold,
			updateInput.HalfDayThresholdUnit,
			updateInput.EnableNotificationForUpperAuthority);
		await Task.CompletedTask;
	}
}
