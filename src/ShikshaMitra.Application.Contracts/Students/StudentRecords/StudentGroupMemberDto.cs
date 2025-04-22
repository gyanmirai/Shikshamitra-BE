using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentGroupMemberDto : EntityDto<Guid>
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; }
	public Guid StudentGroupId { get; set; }
	public string StudentGroupName { get; set; }
	public bool IsLeader { get; set; }
	public bool IsDivisionLeader { get; set; }
}
