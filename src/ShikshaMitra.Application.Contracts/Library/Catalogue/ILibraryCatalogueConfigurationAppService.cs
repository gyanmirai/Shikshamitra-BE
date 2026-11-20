using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Catalogue;

public interface ILibraryCatalogueConfigurationAppService : IApplicationService
{
	Task<LibraryCatalogueConfigurationDto> GetAsync();
	Task<LibraryCatalogueConfigurationDto> UpdateAsync(LibraryCatalogueConfigurationRequest input);
}

public class LibraryCatalogueConfigurationDto : LibraryCatalogueDefinition
{
	public string Revision { get; set; } = string.Empty;
	public string Source { get; set; } = string.Empty;
	public bool CanManageConfiguration { get; set; }
}

public class LibraryCatalogueConfigurationRequest
{
	public string ExpectedRevision { get; set; } = string.Empty;
	public LibraryCatalogueDefinition Definition { get; set; } = new();
}
