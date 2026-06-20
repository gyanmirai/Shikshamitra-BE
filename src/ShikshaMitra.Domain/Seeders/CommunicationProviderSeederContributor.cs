using System;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class CommunicationProviderSeederContributor(
	ICurrentTenant currentTenant,
	IGuidGenerator guidGenerator,
	IUnitOfWork currentUnitOfWork,
	IRepository<ChannelProviderSetting, Guid> providerSettingRepository)
	: BaseSeederContributor(currentTenant, guidGenerator, currentUnitOfWork)
{
	public override async Task SeedAsync(DataSeedContext context)
	{
		var exists = await providerSettingRepository.AnyAsync(x =>
			x.Channel == CommunicationChannelType.InApp
			&& x.ProviderName == "Internal");

		if (exists)
		{
			return;
		}

		await providerSettingRepository.InsertAsync(new ChannelProviderSetting(GuidGenerator.Create(), CurrentTenant.Id)
		{
			Channel = CommunicationChannelType.InApp,
			ProviderName = "Internal",
			SenderId = "INAPP",
			IsEnabled = true,
			IsDefault = true,
			ConfigurationJson = "{}",
			FallbackOrderJson = "[]",
			ConsentRequired = false,
			TemplateApprovalRequired = false,
			Status = CommunicationProviderStatus.Enabled
		}, autoSave: true);
	}
}
