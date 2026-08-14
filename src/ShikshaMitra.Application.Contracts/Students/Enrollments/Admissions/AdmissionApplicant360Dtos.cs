using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionNoteDto { public Guid Id { get; set; } public Guid ApplicantId { get; set; } public string Text { get; set; } = string.Empty; public DateTime CreationTime { get; set; } public DateTime? LastEditedAt { get; set; } public Guid? CreatorId { get; set; } }
public class CreateAdmissionNoteDto { public string Text { get; set; } = string.Empty; }
public class UpdateAdmissionNoteDto { public string Text { get; set; } = string.Empty; }
public class AdmissionApplicantApplicationDto : AdmissionApplicationDto { public List<AdmissionApplicantStageProgressDto> Stages { get; set; } = []; }
public class AdmissionApplicantStageProgressDto { public string Code { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public AdmissionWorkflowStageType StageType { get; set; } public AdmissionRuntimeStageStatus Status { get; set; } public int DisplayOrder { get; set; } }
public class AdmissionApplicant360Dto { public AdmissionApplicantDto Applicant { get; set; } = new(); public AdmissionFamilyDto? Family { get; set; } public AdmissionGuardianDto? PrimaryGuardian { get; set; } public List<AdmissionApplicantApplicationDto> Applications { get; set; } = []; public List<AdmissionActivityDto> Activities { get; set; } = []; public List<AdmissionNoteDto> Notes { get; set; } = []; }
public interface IAdmissionApplicantAppService : IApplicationService { Task<AdmissionApplicant360Dto> GetAsync(Guid id); Task<List<AdmissionActivityDto>> GetActivitiesAsync(Guid id); Task<List<AdmissionNoteDto>> GetNotesAsync(Guid id); Task<AdmissionNoteDto> CreateNoteAsync(Guid id, CreateAdmissionNoteDto input); Task<AdmissionNoteDto> UpdateNoteAsync(Guid id, Guid noteId, UpdateAdmissionNoteDto input); }
