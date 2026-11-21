using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Catalogue;

public class LibraryCatalogueConfigurationReader(IRepository<LibraryCatalogueConfiguration, Guid> configurations,
	ICurrentTenant tenant) : ITransientDependency
{
	public async Task<LibraryCatalogueConfigurationDto> ReadAsync()
	{
		var saved = await configurations.FindAsync(row => row.TenantId == tenant.Id);
		var definition = saved?.Read() ?? LibraryCatalogueRules.Defaults();
		return new()
		{
			Revision = saved?.Revision ?? LibraryCatalogueRules.DefaultsRevision,
			Source = saved == null ? "Defaults" : "School",
			RequiredFields = definition.RequiredFields, Languages = definition.Languages, Materials = definition.Materials
		};
	}

	public async Task<LibraryCatalogueMetadata> ValidateAsync(string? expectedRevision,
		LibraryCatalogueMetadata input, LibraryCatalogueMetadata? previous = null)
	{
		var configuration = await ForWriteAsync(expectedRevision);
		return LibraryCatalogueRules.ValidateMetadata(configuration, input, previous);
	}

	public async Task<LibraryCatalogueConfigurationDto> ForWriteAsync(string? expectedRevision)
	{
		var configuration = await ReadAsync();
		if ((configuration.Source == "School" || expectedRevision != null) &&
			!string.Equals(configuration.Revision, expectedRevision, StringComparison.Ordinal))
			throw new BusinessException(LibraryErrorCodes.CatalogueConfigurationChanged,
				"The school catalogue rules changed. Review the current rules before saving.");
		return configuration;
	}
}
