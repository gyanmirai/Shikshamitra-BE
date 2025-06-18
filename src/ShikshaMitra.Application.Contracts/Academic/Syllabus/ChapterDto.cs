using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Syllabus;

public class ChapterDto : AuditedEntityDto<Guid>
{
	public Guid StandardId { get; set; }
	public Guid SubjectId { get; set; }
	public int ChapterNumber { get; set; }
	public string ChapterName { get; set; }
	public DateTimeOffset ChapterStartTime { get; set; }
	public DateTimeOffset ChapterEndTime { get; set; }
	public string? Description { get; set; }
}
