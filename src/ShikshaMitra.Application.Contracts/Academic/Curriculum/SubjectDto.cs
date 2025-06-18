using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectDto : EntityDto<Guid>
{
	public string Name { get; set; }
	public string Color { get; set; }
	public Guid EducationMediumId { get; set; }
}
