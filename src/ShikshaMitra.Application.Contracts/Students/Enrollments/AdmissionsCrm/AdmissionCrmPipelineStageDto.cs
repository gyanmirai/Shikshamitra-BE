using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPipelineStageDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public AdmissionInquiryStage SystemStage { get; set; }
	public string StageName { get; set; } = string.Empty;
	public int DisplayOrder { get; set; }
	public bool IsEnabled { get; set; }
	public bool IsSystem { get; set; }
	public string? ColorHex { get; set; }
	public int? SlaTargetHours { get; set; }
	public string? OwnerResponsibility { get; set; }
	public List<AdmissionInquiryStage> AllowedNextStages { get; set; } = [];
}
