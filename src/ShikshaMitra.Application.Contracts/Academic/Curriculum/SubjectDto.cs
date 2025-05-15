using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectDto : EntityDto<Guid>
{
	public Guid MediumId { get; set; }
	public string Color { get; set; }
}
