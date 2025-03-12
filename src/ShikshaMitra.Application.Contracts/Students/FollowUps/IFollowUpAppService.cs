using ShikshaMitra.Students.StudentEnrollments;
using System.Threading.Tasks;
using System;

namespace ShikshaMitra.Students.FollowUps;

public interface IFollowUpAppService
{

	Task<FollowUpWithStatusAndDateDto> AddFollowUpAsync(Guid studentEnrollmentId, AddFollowUpDto input);
	Task<FollowUpWithStatusAndDateDto> GetFollowUpDetailsAsync(Guid studentEnrollmentId);
	Task UpdateStatusAndDueDateAsync(Guid studentEnrollmentId, FollowUpStatusType status, DateTimeOffset? dueDate);
}
