using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowStageDto
{
	public Guid Id { get; set; }
	public Guid WorkflowDefinitionId { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public AdmissionWorkflowStageType StageType { get; set; }
	public int DisplayOrder { get; set; }
	public bool IsRequired { get; set; }
	public bool AllowManualSkip { get; set; }
	public string? ConfigurationJson { get; set; }
}

public class AdmissionWorkflowTransitionDto
{
	public Guid Id { get; set; }
	public Guid WorkflowDefinitionId { get; set; }
	public Guid FromStageId { get; set; }
	public Guid ToStageId { get; set; }
	public int Priority { get; set; }
}

public class AdmissionWorkflowDto : FullAuditedEntityDto<Guid>
{
	public string Name { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public int Version { get; set; }
	public AdmissionWorkflowStatus Status { get; set; }
	public Guid? ParentVersionId { get; set; }
	public DateTime? PublishedAt { get; set; }
	public List<AdmissionWorkflowStageDto> Stages { get; set; } = [];
	public List<AdmissionWorkflowTransitionDto> Transitions { get; set; } = [];
}

public class CreateUpdateAdmissionWorkflowStageDto
{
	public Guid? Id { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public AdmissionWorkflowStageType StageType { get; set; }
	public int DisplayOrder { get; set; }
	public bool IsRequired { get; set; } = true;
	public bool AllowManualSkip { get; set; }
	public string? ConfigurationJson { get; set; }
}

public class CreateUpdateAdmissionWorkflowTransitionDto
{
	public Guid? Id { get; set; }
	public Guid FromStageId { get; set; }
	public Guid ToStageId { get; set; }
	public int Priority { get; set; }
}

public class CreateUpdateAdmissionWorkflowDto
{
	public string Name { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public List<CreateUpdateAdmissionWorkflowStageDto> Stages { get; set; } = [];
	public List<CreateUpdateAdmissionWorkflowTransitionDto> Transitions { get; set; } = [];
}

public class AdmissionWorkflowListInput : PagedAndSortedResultRequestDto
{
	public string? Search { get; set; }
	public AdmissionWorkflowStatus? Status { get; set; }
}

public class AdmissionWorkflowValidationResultDto
{
	public bool IsValid => Errors.Count == 0;
	public List<string> Errors { get; set; } = [];
}
