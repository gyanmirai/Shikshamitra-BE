using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationTemplateTransaction_Tests : ShikshaMitraApplicationTestBase<NotificationTemplateTransactionTestModule>
{
	[Fact]
	public async Task Direct_container_command_enters_a_transaction_before_authorization_or_any_save()
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] {
			new Claim(AbpClaimTypes.UserId, Guid.NewGuid().ToString()), new Claim(AbpClaimTypes.TenantId, tenantId.ToString())
		}, "transaction-test"));
		using var identity = GetRequiredService<ICurrentPrincipalAccessor>().Change(principal);
		var command = new CreateNotificationTemplateDto("Academic.Homework.Published", "Guardian", CommunicationChannelType.Email,
			"en", "Transaction probe", "Homework", "{{source_link}}");
		var error = await Should.ThrowAsync<BusinessException>(() => GetRequiredService<NotificationTemplateRevisionService>().CreateAsync(command));
		error.Code.ShouldBe("Notification:Test:TransactionProbe");
		GetRequiredService<NotificationTemplateTransactionProbe>().WasTransactional.ShouldBeTrue();
	}
}

[DependsOn(typeof(ShikshaMitraApplicationTestModule))]
public class NotificationTemplateTransactionTestModule : AbpModule
{
	public override void PostConfigureServices(ServiceConfigurationContext context)
	{
		context.Services.Replace(ServiceDescriptor.Transient<IUnitOfWorkManager, UnitOfWorkManager>());
		context.Services.RemoveAll<INotificationTemplateAccessPolicy>();
		context.Services.AddSingleton<NotificationTemplateTransactionProbe>();
		context.Services.AddSingleton<INotificationTemplateAccessPolicy>(services => services.GetRequiredService<NotificationTemplateTransactionProbe>());
	}
}

public class NotificationTemplateTransactionProbe(IUnitOfWorkManager units) : INotificationTemplateAccessPolicy
{
	public bool WasTransactional { get; private set; }
	public bool SupportsEvent(string eventKey) => eventKey.StartsWith("Academic.", StringComparison.Ordinal);
	public Task EnsureAsync(string eventKey, Guid? academicMediumId, NotificationTemplateAction action, CancellationToken cancellationToken)
	{
		WasTransactional = units.Current?.Options.IsTransactional == true;
		throw new BusinessException("Notification:Test:TransactionProbe");
	}
}
