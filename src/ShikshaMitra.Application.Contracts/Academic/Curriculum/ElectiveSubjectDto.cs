using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// DTO for retrieving ElectiveSubject details.
/// </summary>
public class ElectiveSubjectDto : EntityDto<Guid>
{
	public required string Name { get; set; }

	public required string Color { get; set; }

	public Guid EducationMediumId { get; set; }

	/// <summary>
	/// The subjects included in this elective subject.
	/// </summary>
	public List<SubjectDto> Subjects { get; set; } = new List<SubjectDto>();
}
