using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.Settings;

public class DocumentSettingAppService(IDocumentSettingRepository repository)
	: CrudAppService<DocumentSetting, DocumentSettingDto, Guid, PagedAndSortedResultRequestDto,
			CreateUpdateDocumentSettingDto>(repository),
		IDocumentSettingAppService;
		