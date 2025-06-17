using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.Settings;

public class DocumentSettingAppService(IDocumentSettingRepository repository, ILogger<DocumentSettingAppService> logger)
	: CrudAppService<DocumentSetting, DocumentSettingDto, Guid, PagedAndSortedResultRequestDto,
			CreateUpdateDocumentSettingDto>(repository),
		IDocumentSettingAppService
{
	public async override Task<DocumentSettingDto> CreateAsync(CreateUpdateDocumentSettingDto input)
	{
		try
		{
			var exists = await repository.FindAsync(x => x.DocumentName == input.DocumentName);
			if (exists != null)
			{
				throw new Exception($"Document setting with name '{input.DocumentName}' already exists.");
			}
			return await base.CreateAsync(input);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error creating document setting");
			throw;
		}
	}
}
