using System;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing.Smtp;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

internal sealed record NotificationEmailProvider(Guid Id, bool ConsentRequired);

[RemoteService(false)]
public sealed class NotificationEmailRoute(ICurrentTenant tenant, IRepository<ChannelProviderSetting, Guid> providers,
	IAsyncQueryableExecuter queries, ISmtpEmailSenderConfiguration smtp) : ITransientDependency
{
	internal async Task<NotificationEmailProvider?> FindAsync(CancellationToken cancellationToken)
	{
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty)
			throw new BusinessException(NotificationErrorCodes.TenantRequired);
		var rows = await queries.ToListAsync((await providers.GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && !x.IsDeleted && x.Channel == CommunicationChannelType.Email && x.IsEnabled &&
				x.Status == CommunicationProviderStatus.Enabled)
			.OrderByDescending(x => x.IsDefault).ThenByDescending(x => x.CreationTime).ThenBy(x => x.Id)
			.Select(x => new { x.Id, x.ProviderName, x.ConsentRequired }), cancellationToken);
		var provider = rows.FirstOrDefault(x => string.Equals(x.ProviderName.Trim(), "SMTP", StringComparison.OrdinalIgnoreCase));
		return provider != null && await HasSenderConfigurationAsync() ? new(provider.Id, provider.ConsentRequired) : null;
	}

	private async Task<bool> HasSenderConfigurationAsync()
	{
		try
		{
			if (string.IsNullOrWhiteSpace(await smtp.GetHostAsync()) ||
				await smtp.GetPortAsync() is < 0 or > 65535 ||
				!MailAddress.TryCreate(await smtp.GetDefaultFromAddressAsync(), out _) ||
				string.IsNullOrEmpty(await smtp.GetDefaultFromDisplayNameAsync()))
				return false;

			return await smtp.GetUseDefaultCredentialsAsync() ||
				(!string.IsNullOrEmpty(await smtp.GetUserNameAsync()) && !string.IsNullOrEmpty(await smtp.GetPasswordAsync()));
		}
		catch (Exception error) when (error is AbpException or ArgumentException or FormatException or OverflowException or CryptographicException)
		{
			return false;
		}
	}
}
