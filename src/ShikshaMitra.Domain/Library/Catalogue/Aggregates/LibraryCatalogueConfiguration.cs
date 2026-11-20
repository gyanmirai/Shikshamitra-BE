using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Catalogue;

public class LibraryCatalogueConfiguration : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string DefinitionJson { get; private set; } = string.Empty;
	public string Revision { get; private set; } = Guid.NewGuid().ToString("N");

	protected LibraryCatalogueConfiguration() { }

	public LibraryCatalogueConfiguration(Guid id, Guid? tenantId, LibraryCatalogueDefinition definition) : base(id)
	{
		TenantId = tenantId;
		DefinitionJson = JsonSerializer.Serialize(LibraryCatalogueRules.ValidateDefinition(definition, LibraryCatalogueRules.Defaults()));
	}

	public LibraryCatalogueDefinition Read()
	{
		try
		{
			var definition = JsonSerializer.Deserialize<LibraryCatalogueDefinition>(DefinitionJson);
			if (definition == null) throw new JsonException();
			return LibraryCatalogueRules.ValidateDefinition(definition);
		}
		catch (JsonException)
		{
			throw new BusinessException(LibraryErrorCodes.CatalogueConfigurationInvalid, "The saved catalogue configuration requires review.");
		}
	}

	public void Revise(LibraryCatalogueDefinition definition, string expectedRevision)
	{
		if (!string.Equals(expectedRevision, Revision, StringComparison.Ordinal))
			throw new BusinessException(LibraryErrorCodes.CatalogueConfigurationChanged, "The catalogue configuration changed. Reload it before saving.");
		var accepted = LibraryCatalogueRules.ValidateDefinition(definition, Read());
		DefinitionJson = JsonSerializer.Serialize(accepted);
		Revision = Guid.NewGuid().ToString("N");
	}
}
