using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Curriculum.Setting;

/// <summary>
/// DTO for retrieving Subject details.
/// (Assumed to exist based on SubjectAppService usage)
/// </summary>
public class SubjectDto : EntityDto<Guid>
{
	public required string Name { get; set; }
	public required string Color { get; set; }
}
