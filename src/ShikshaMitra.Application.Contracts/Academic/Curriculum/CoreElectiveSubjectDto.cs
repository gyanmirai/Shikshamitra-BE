using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Curriculum;

public class CoreElectiveSubjectResponseDto : EntityDto<Guid>
{
	public required string Name { get; set; }
	public SubjectType SubjectType { get; set; }
	public string? Color { get; set; }
	public List<CoreElectiveSubjectDetailDto> Subjects { get; set; } = new List<CoreElectiveSubjectDetailDto>();
}