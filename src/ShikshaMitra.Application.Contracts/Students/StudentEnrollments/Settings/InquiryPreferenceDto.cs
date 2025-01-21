using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

public class InquiryPreferenceDto : AuditedEntityDto<Guid>
{
	public string PreferenceName { get; set; }
}