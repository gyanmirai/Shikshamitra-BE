using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollment.Settings;

public class InquiryPreferenceDto : AuditedEntityDto<Guid>
{
	public string PreferenceName { get; set; }
}