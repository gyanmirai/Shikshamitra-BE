using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false)]
public sealed class NotificationSchoolTimeZone(ICurrentTenant tenant, ISettingManager settings) : ITransientDependency
{
	public async Task<TimeZoneInfo> GetAsync(CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		var value = await settings.GetOrNullForTenantAsync(NotificationSettingNames.SchoolTimeZone, tenantId, fallback: false);
		cancellationToken.ThrowIfCancellationRequested();
		if (value == null) return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
		if (string.IsNullOrWhiteSpace(value) || value.Length > 128) throw new BusinessException(NotificationErrorCodes.RuleConfigurationInvalid);
		try { return TimeZoneInfo.FindSystemTimeZoneById(value); }
		catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException)
		{
			throw new BusinessException(NotificationErrorCodes.RuleConfigurationInvalid);
		}
	}
}
