using QuestPDF.Fluent;
using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.Settings;

public interface
	IDocumentSettingAppService : ICrudAppService<DocumentSettingDto, Guid, PagedAndSortedResultRequestDto,
	CreateUpdateDocumentSettingDto>
{
}