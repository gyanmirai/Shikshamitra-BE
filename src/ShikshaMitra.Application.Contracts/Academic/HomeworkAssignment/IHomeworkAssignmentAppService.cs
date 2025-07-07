using System;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public interface IHomeworkAssignmentAppService
	: ICrudAppService<
		HomeworkAssignmentDto,
		Guid,
		PagedAndSortedResultRequestDto,
		CreateUpdateHomeworkAssignmentDto
	>
{
	// Additional custom methods
	Task<HomeworkAssignmentDto> PublishAsync(Guid id, PublishHomeworkAssignmentDto input);
	Task<HomeworkAssignmentDto> UnpublishAsync(Guid id);
	Task<string> UploadDocumentAsync(Guid id, DocumentDto file);
	Task RemoveDocumentAsync(Guid id);
}
