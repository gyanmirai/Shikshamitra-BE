using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class WhatsAppProviderRoute_Tests
{
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
	private readonly List<ChannelProviderSetting> _providers = [];
	private readonly WhatsAppProviderRoute _route;

	public WhatsAppProviderRoute_Tests()
	{
		_tenant.Id.Returns(_tenantId);
		var provider = Substitute.For<IRepository<ChannelProviderSetting, Guid>>();
		provider.GetQueryableAsync().Returns(_ => _providers.AsQueryable());
		var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["Notifications:WhatsApp:GraphApiVersion"] = "v26.0",
			[$"Notifications:WhatsApp:Tenants:{_tenantId:N}:AccessTokens:school-main"] = "synthetic-token"
		}).Build();
		_route = new(_tenant, provider, new AsyncQueryableExecuter([]), config);
	}

	[Fact]
	public async Task The_default_enabled_provider_and_exact_pinned_provider_are_resolved_in_school_scope()
	{
		var primary = Add(_tenantId, "1000001", isDefault: true);
		var secondary = Add(_tenantId, "1000002");
		var foreign = Add(Guid.NewGuid(), "1000003", isDefault: true);

		var selected = await _route.FindAsync(default);
		selected.ShouldNotBeNull();
		selected.Id.ShouldBe(primary.Id);
		selected.Connection.GraphApiVersion.ShouldBe("v26.0");
		selected.Connection.PhoneNumberId.ShouldBe("1000001");
		selected.Connection.AccessToken.ShouldBe("synthetic-token");
		selected.ToString().ShouldNotContain("synthetic-token");
		(await _route.FindAsync(default, secondary.Id))!.Id.ShouldBe(secondary.Id);
		(await _route.FindAsync(default, foreign.Id)).ShouldBeNull();
		_tenant.Id.Returns(foreign.TenantId);
		(await _route.FindAsync(default, foreign.Id)).ShouldBeNull();
	}

	[Fact]
	public async Task An_invalid_selected_account_or_missing_secret_does_not_switch_the_schools_sender()
	{
		var primary = Add(_tenantId, "1000001", isDefault: true);
		Add(_tenantId, "1000002");
		primary.SecretReference = "missing";

		(await _route.FindAsync(default)).ShouldBeNull();
		primary.SecretReference = "school-main";
		primary.ConfigurationJson = """{"schemaVersion":1,"phoneNumberId":"../invalid","businessAccountId":"1000000"}""";
		(await _route.FindAsync(default)).ShouldBeNull();
	}

	[Fact]
	public async Task Other_providers_disabled_settings_and_host_scope_cannot_supply_the_connection()
	{
		var setting = Add(_tenantId, "1000001", isDefault: true);
		setting.IsEnabled = false;
		(await _route.FindAsync(default, setting.Id)).ShouldBeNull();
		setting.IsEnabled = true;
		setting.ProviderName = "Unrecognized provider";
		(await _route.FindAsync(default, setting.Id)).ShouldBeNull();
		_tenant.Id.Returns((Guid?)null);
		var error = await Should.ThrowAsync<BusinessException>(() => _route.FindAsync(default));
		error.Code.ShouldBe(NotificationErrorCodes.TenantRequired);
	}

	private ChannelProviderSetting Add(Guid tenantId, string phoneId, bool isDefault = false)
	{
		var row = new ChannelProviderSetting(Guid.NewGuid(), tenantId) {
			Channel = CommunicationChannelType.WhatsApp, ProviderName = WhatsAppProviderRoute.ProviderName,
			IsEnabled = true, Status = CommunicationProviderStatus.Enabled, IsDefault = isDefault, SecretReference = "school-main",
			ConfigurationJson = $$"""{"schemaVersion":1,"phoneNumberId":"{{phoneId}}","businessAccountId":"1000000"}"""
		};
		_providers.Add(row); return row;
	}
}
