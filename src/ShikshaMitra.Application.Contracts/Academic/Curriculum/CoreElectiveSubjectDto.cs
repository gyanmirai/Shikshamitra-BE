using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Curriculum;

public class CoreElectiveSubjectResponseDto : EntityDto<Guid>
{
	public required string Name { get; set; }
	public SubjectType SubjectType { get; set; }
}
