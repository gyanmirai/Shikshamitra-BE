using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionApplicationListInput : PagedAndSortedResultRequestDto { public string? Search { get; set; } public AdmissionApplicationStatus? Status { get; set; } public Guid? AdmissionCycleId { get; set; } public Guid? AdmissionOfferingId { get; set; } public Guid? CurrentStageInstanceId { get; set; } public string? CurrentStageSearch { get; set; } }
public class AdmissionApplicationDto : FullAuditedEntityDto<Guid> { public string? ConcurrencyStamp { get; set; } public string ApplicationNumber { get; set; } = string.Empty; public Guid ApplicantId { get; set; } public string ApplicantName { get; set; } = string.Empty; public Guid AdmissionCycleId { get; set; } public Guid AdmissionOfferingId { get; set; } public string OfferingCode { get; set; } = string.Empty; public string CurrentStageName { get; set; } = string.Empty; public AdmissionApplicationStatus Status { get; set; } public Guid? CurrentStageInstanceId { get; set; } public DateTime? LastSavedAt { get; set; } public DateTime? SubmittedAt { get; set; } }
public class AdmissionApplicationStageDto { public Guid Id { get; set; } public string Code { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public AdmissionWorkflowStageType StageType { get; set; } public int DisplayOrder { get; set; } public AdmissionRuntimeStageStatus Status { get; set; } }
public class AdmissionApplicationWorkflowDto { public Guid WorkflowDefinitionId { get; set; } public List<AdmissionApplicationStageDto> Stages { get; set; } = []; }
public class AdmissionFormValueDto { public string FieldCode { get; set; } = string.Empty; public string? ValueJson { get; set; } }
public class AdmissionApplicationFormDto { public Guid FormDefinitionId { get; set; } public int FormVersion { get; set; } public AdmissionFormDto Definition { get; set; } = new(); public List<AdmissionFormValueDto> Values { get; set; } = []; }
public class UpdateAdmissionApplicationFormValuesDto { public string? ConcurrencyStamp { get; set; } public List<AdmissionFormValueDto> Values { get; set; } = []; }
public class AdmissionApplicationSubmitResultDto { public AdmissionApplicationDto Application { get; set; } = new(); public List<string> ValidationErrors { get; set; } = []; public Dictionary<string, string> FieldErrors { get; set; } = []; }
