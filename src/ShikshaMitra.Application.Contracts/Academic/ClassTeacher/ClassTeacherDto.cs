using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.ClassTeacher;

public class ClassTeacherDto : EntityDto<Guid>
{
	public Guid TeacherId { get; set; }
	public Guid ClassId { get; set; }
	public Guid DivisionId { get; set; }
}
