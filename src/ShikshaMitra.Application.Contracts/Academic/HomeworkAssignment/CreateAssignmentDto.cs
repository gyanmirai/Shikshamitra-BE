using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// DTO for creating a new assignment
/// </summary>
public class CreateAssignmentDto
{
	[Required(ErrorMessage = "ShikshaMitra.Assignment:ClassRequired")]
	public Guid ClassId { get; set; } // StandardId

	[Required(ErrorMessage = "ShikshaMitra.Assignment:SubjectRequired")]
	public Guid SubjectId { get; set; }

	public List<Guid>? Chapters { get; set; }

	[Required(ErrorMessage = "ShikshaMitra.Assignment:TitleRequired")]
	[StringLength(200, MinimumLength = 3, ErrorMessage = "ShikshaMitra.Assignment:TitleLength")]
	public string Title { get; set; } = string.Empty;

	[Required(ErrorMessage = "ShikshaMitra.Assignment:DescriptionRequired")]
	public string Description { get; set; } = string.Empty;

	[Required(ErrorMessage = "ShikshaMitra.Assignment:AssignmentTypeRequired")]
	public Guid AssignmentTypeId { get; set; }

	[Required(ErrorMessage = "ShikshaMitra.Assignment:SubmissionTypeRequired")]
	[Range(0, 2, ErrorMessage = "ShikshaMitra.Assignment:SubmissionTypeInvalid")]
	// 0=OnlineText, 1=FileUpload, 2=InPerson
	public int SubmissionType { get; set; }

	public List<AttachmentDto>? Attachments { get; set; }

	[Required]
	[MinLength(1, ErrorMessage = "ShikshaMitra.Assignment:AtLeastOneDivision")]
	public List<AssignmentDivisionScheduleInputDto> Divisions { get; set; } = new();
}

/// <summary>
/// DTO for assignment division scheduling input
/// </summary>
public class AssignmentDivisionScheduleInputDto
{
	[Required(ErrorMessage = "ShikshaMitra.Assignment:DivisionRequired")]
	public Guid DivisionId { get; set; }

	public DateTime? PublishDate { get; set; } // null = manual publish

	[Required(ErrorMessage = "ShikshaMitra.Assignment:DueDateRequired")]
	public DateTime DueDate { get; set; }
}
