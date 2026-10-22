using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionActivityDto { public string? ActorName { get; set; } public Guid Id { get; set; } public Guid? ApplicantId { get; set; } public Guid? ApplicationId { get; set; } public Guid? InquiryId { get; set; } public string ActivityType { get; set; } = string.Empty; public string Description { get; set; } = string.Empty; public DateTime OccurredAt { get; set; } public Guid? CreatorId { get; set; } }
public interface IAdmissionActivityAppService : IApplicationService { Task<List<AdmissionActivityDto>> GetForApplicantAsync(Guid applicantId); Task<List<AdmissionActivityDto>> GetForApplicationAsync(Guid applicationId); }
