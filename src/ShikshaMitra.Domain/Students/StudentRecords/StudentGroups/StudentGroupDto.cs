using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentRecords.StudentGroups;

public class StudentGroupDto : EntityDto<Guid>
{
	public string Name { get; set; }
}
