using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentDto : AuditedEntityDto<Guid>
{
	public string Name { get; set; }
}
