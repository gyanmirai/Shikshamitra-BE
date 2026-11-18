using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationSchoolTimeZone_Tests
{
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
	private readonly ISettingManager _settings = Substitute.For<ISettingManager>();
	private readonly NotificationSchoolTimeZone _zone;
	public NotificationSchoolTimeZone_Tests()
	{
		_tenant.Id.Returns(_tenantId);
		_settings.GetOrNullAsync(NotificationSettingNames.SchoolTimeZone, "T", _tenantId.ToString(), false).Returns((string?)null);
		_zone = new(_tenant, _settings);
	}
	[Fact]
	public async Task Only_an_absent_school_setting_uses_the_existing_India_fallback()
	{
		(await _zone.GetAsync()).Id.ShouldBe("Asia/Kolkata");
		await _settings.Received(1).GetOrNullAsync(NotificationSettingNames.SchoolTimeZone, "T", _tenantId.ToString(), false);
	}
	[Fact]
	public async Task A_saved_school_zone_is_used_without_user_global_or_reporting_overrides()
	{
		_settings.GetOrNullAsync(NotificationSettingNames.SchoolTimeZone, "T", _tenantId.ToString(), false).Returns("Pacific/Honolulu");
		(await _zone.GetAsync()).Id.ShouldBe("Pacific/Honolulu");
		_settings.ReceivedCalls().ShouldHaveSingleItem();
	}
	[Theory]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData("Not/A_Real_Zone")]
	public async Task An_invalid_saved_zone_never_silently_falls_back(string value)
	{
		_settings.GetOrNullAsync(NotificationSettingNames.SchoolTimeZone, "T", _tenantId.ToString(), false).Returns(value);
		var error = await Should.ThrowAsync<BusinessException>(() => _zone.GetAsync());
		error.Code.ShouldBe(NotificationErrorCodes.RuleConfigurationInvalid); error.InnerException.ShouldBeNull();
	}
	[Fact]
	public async Task A_host_or_cancelled_call_cannot_resolve_a_school_schedule()
	{
		_tenant.Id.Returns((Guid?)null);
		var error = await Should.ThrowAsync<BusinessException>(() => _zone.GetAsync()); error.Code.ShouldBe(NotificationErrorCodes.TenantRequired);
		_tenant.Id.Returns(_tenantId);
		await Should.ThrowAsync<OperationCanceledException>(() => _zone.GetAsync(new CancellationToken(true)));
		_settings.ReceivedCalls().ShouldBeEmpty();
	}
}
