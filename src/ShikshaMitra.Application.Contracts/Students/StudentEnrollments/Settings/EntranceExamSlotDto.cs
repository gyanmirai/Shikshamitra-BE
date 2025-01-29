using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class EntranceExamSlotDto : AuditedEntityDto<Guid>
{
	public DateTimeOffset StartDate { get; set; }
	public DateTimeOffset EndDate { get; set; }
}