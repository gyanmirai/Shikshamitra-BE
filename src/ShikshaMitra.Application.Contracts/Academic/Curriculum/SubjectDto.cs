using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectDto : EntityDto<Guid>
{
	public Guid ClassId { get; set; }
	public string? SubjectCode { get; set; }
	public string? SubjectName { get; set; }
	//public List<TeacherDto> Teachers { get; set; } = new();
	public DateTimeOffset ScheduledDate { get; set; }
	public string? SubjectColor { get; set; }
	public string? LearningGoals { get; set; }
	public string? Prerequisites { get; set; }
	public string? Description { get; set; }
}
