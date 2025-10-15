using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Curriculum
{
	public class ChapterrDto : AuditedEntityDto<Guid>
	{
		public Guid StandardId { get; set; }
		public Guid SubjectId { get; set; }
		public int ChapterNumber { get; set; }
		public string Name { get; set; } = default!;
		public DateTimeOffset StartDate { get; set; }
		public DateTimeOffset EndDate { get; set; }
		public string? Description { get; set; }
		public List<ChapterDocumentDto> Documents { get; set; } = new();
	}
}
