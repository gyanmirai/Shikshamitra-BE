using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentRecords;

public interface IStudentGroupMemberAppService : IApplicationService
{
	Task<StudentGroupMemberDto> AssignStudentToGroupAsync(AssignStudentToGroupDto input);
	Task RemoveStudentFromGroupAsync(Guid studentId, Guid groupId);
	Task<StudentGroupMemberDto> SetGroupLeaderAsync(SetGroupLeaderDto input);
	Task RemoveGroupLeaderAsync(Guid groupId);
	Task<List<StudentDto>> GetGroupMembersAsync(Guid groupId);
	Task<StudentDto> GetGroupLeaderAsync(Guid groupId);
}
