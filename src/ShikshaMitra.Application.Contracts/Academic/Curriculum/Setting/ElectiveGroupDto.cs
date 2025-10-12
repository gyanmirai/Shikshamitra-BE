using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Curriculum.Setting;

/// <summary>
/// DTO for retrieving ElectiveGroup details.
/// </summary>
public class ElectiveGroupDto : EntityDto<Guid>
{
	public required string Name { get; set; }
	public required string Color { get; set; }
	public List<SubjectDto> Subjects { get; set; } = new();
}
