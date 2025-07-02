using ShikshaMitra.Common;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;
public interface IHomeworkAssignmentTypeAppService : ICrudAppService<HomeworkAssignmentTypeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateHomeworkAssignmentTypeDto>
{
}

public interface IHomeworkAssignmentAppService :
		ICrudAppService<HomeworkAssignmentDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateHomeworkAssignmentDto>
{
	// Additional custom methods
	Task<HomeworkAssignmentDto> PublishAsync(Guid id, PublishHomeworkAssignmentDto input);
	Task<HomeworkAssignmentDto> UnpublishAsync(Guid id);
	Task<string> UploadDocumentAsync(Guid id, DocumentDto file);
	Task RemoveDocumentAsync(Guid id);
}