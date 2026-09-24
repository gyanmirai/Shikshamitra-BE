using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionApplicantAppService(IRepository<AdmissionApplicant, Guid> applicantRepository, IRepository<AdmissionFamily, Guid> familyRepository, IRepository<AdmissionGuardian, Guid> guardianRepository, IRepository<AdmissionApplication, Guid> applicationRepository, IRepository<AdmissionApplicationWorkflowInstance, Guid> workflowInstanceRepository, IRepository<AdmissionApplicationStageInstance, Guid> stageInstanceRepository, IRepository<AdmissionActivity, Guid> activityRepository, IRepository<AdmissionNote, Guid> noteRepository) : ApplicationService, IAdmissionApplicantAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplicants.View)] public async Task<AdmissionApplicant360Dto> GetAsync(Guid id) { var applicant = await GetApplicantAsync(id); var family = await familyRepository.FindAsync(applicant.FamilyId); var guardian = family?.PrimaryGuardianId.HasValue == true ? await guardianRepository.FindAsync(family.PrimaryGuardianId.Value) : null; var applications = (await applicationRepository.GetListAsync(x => x.ApplicantId == id)).OrderByDescending(x => x.CreationTime).ToList(); var workflows = await workflowInstanceRepository.GetListAsync(x => applications.Select(a => a.Id).Contains(x.ApplicationId)); var stages = await stageInstanceRepository.GetListAsync(x => workflows.Select(w => w.Id).Contains(x.WorkflowInstanceId)); return new AdmissionApplicant360Dto { Applicant = Map(applicant), Family = family == null ? null : new AdmissionFamilyDto { Id = family.Id, FamilyNumber = family.FamilyNumber, PrimaryGuardianId = family.PrimaryGuardianId, Status = family.Status }, PrimaryGuardian = guardian == null ? null : Map(guardian), Applications = applications.Select(a => Map(a, stages.Where(s => workflows.FirstOrDefault(w => w.ApplicationId == a.Id)?.Id == s.WorkflowInstanceId))).ToList(), Activities = (await GetActivitiesAsync(id)), Notes = (await GetNotesAsync(id)) }; }
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplicants.Edit)]
	public async Task<AdmissionApplicantDto> UpdateProfileAsync(Guid id, UpdateAdmissionApplicantProfileDto input)
	{
		var applicant = await GetApplicantAsync(id);
		var applications = await applicationRepository.GetListAsync(x => x.ApplicantId == id);
		if (applicant.ExistingStudentId.HasValue || applications.Any(x => x.Status is AdmissionApplicationStatus.Enrolled or AdmissionApplicationStatus.AdmissionFinalized))
			throw new BusinessException(AdmissionErrorCodes.ApplicationDecisionInvalidState,
				"A finalized applicant profile must be updated from the Student record.");
		applicant.UpdateProfile(input.FirstName, input.MiddleName, input.LastName, input.DateOfBirth, input.Gender,
			applicant.AddressLine1, applicant.AddressLine2, applicant.City, applicant.State, applicant.Country,
			applicant.PostalCode);
		if (input.GuardianMobile != null || input.GuardianEmail != null)
		{
			var family = await familyRepository.FindAsync(applicant.FamilyId);
			var guardian = family?.PrimaryGuardianId.HasValue == true
				? await guardianRepository.FindAsync(family.PrimaryGuardianId.Value)
				: null;
			if (guardian == null)
				throw new BusinessException(AdmissionErrorCodes.InquiryIdentityIncomplete,
					"A primary guardian is required before updating guardian contact details.");
			guardian.UpdateContact(guardian.FirstName, guardian.LastName,
				input.GuardianMobile ?? guardian.Mobile, input.GuardianEmail);
			await guardianRepository.UpdateAsync(guardian, false);
		}
		await applicantRepository.UpdateAsync(applicant, false);
		await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id,
			"ApplicantProfileUpdated", "Applicant identity and guardian contact updated", id, null, null), true);
		return Map(applicant);
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplicants.View)]
	public async Task<List<AdmissionActivityDto>> GetActivitiesAsync(Guid id)
	{
		var query = await activityRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(query.Where(x => x.ApplicantId == id)
			.OrderByDescending(x => x.OccurredAt).Take(200));
		return rows.Select(x => new AdmissionActivityDto
		{
			Id = x.Id, ApplicantId = x.ApplicantId, ApplicationId = x.ApplicationId,
			InquiryId = x.InquiryId, ActivityType = x.ActivityType, Description = x.Description,
			OccurredAt = x.OccurredAt, CreatorId = x.CreatorId
		}).ToList();
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplicants.NotesView)] public async Task<List<AdmissionNoteDto>> GetNotesAsync(Guid id) => (await noteRepository.GetListAsync(x => x.ApplicantId == id)).OrderByDescending(x => x.CreationTime).Select(Map).ToList();
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplicants.NotesCreate)] public async Task<AdmissionNoteDto> CreateNoteAsync(Guid id, CreateAdmissionNoteDto input) { await GetApplicantAsync(id); var note = new AdmissionNote(GuidGenerator.Create(), CurrentTenant.Id, id, input.Text); await noteRepository.InsertAsync(note, false); await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id, "NoteAdded", "Applicant note added", id, null, null), true); return Map(note); }
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionApplicants.NotesEdit)] public async Task<AdmissionNoteDto> UpdateNoteAsync(Guid id, Guid noteId, UpdateAdmissionNoteDto input) { await GetApplicantAsync(id); var note = await noteRepository.FindAsync(x => x.Id == noteId && x.ApplicantId == id) ?? throw new EntityNotFoundException(typeof(AdmissionNote), noteId); note.UpdateText(input.Text); await noteRepository.UpdateAsync(note, false); await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id, "NoteModified", "Applicant note modified", id), true); return Map(note); }
	private async Task<AdmissionApplicant> GetApplicantAsync(Guid id) => await applicantRepository.FindAsync(id) ?? throw new EntityNotFoundException(typeof(AdmissionApplicant), id);
	private static AdmissionApplicantDto Map(AdmissionApplicant x) => new() { Id = x.Id, FamilyId = x.FamilyId, FirstName = x.FirstName, MiddleName = x.MiddleName, LastName = x.LastName, DateOfBirth = x.DateOfBirth, Gender = x.Gender, AddressLine1 = x.AddressLine1, AddressLine2 = x.AddressLine2, City = x.City, State = x.State, Country = x.Country, PostalCode = x.PostalCode, Status = x.Status, CreationTime = x.CreationTime, CreatorId = x.CreatorId, LastModificationTime = x.LastModificationTime, LastModifierId = x.LastModifierId };
	private static AdmissionGuardianDto Map(AdmissionGuardian x) => new() { Id = x.Id, FamilyId = x.FamilyId, FirstName = x.FirstName, LastName = x.LastName, Mobile = x.Mobile, Email = x.Email, Relationship = x.Relationship, IsPrimaryContact = x.IsPrimaryContact, CreationTime = x.CreationTime, CreatorId = x.CreatorId, LastModificationTime = x.LastModificationTime, LastModifierId = x.LastModifierId };
	private static AdmissionApplicationDto Map(AdmissionApplication x) => new() { Id = x.Id, ApplicationNumber = x.ApplicationNumber, ApplicantId = x.ApplicantId, AdmissionCycleId = x.AdmissionCycleId, AdmissionOfferingId = x.AdmissionOfferingId, Status = x.Status, CurrentStageInstanceId = x.CurrentStageInstanceId, LastSavedAt = x.LastSavedAt, SubmittedAt = x.SubmittedAt, CreationTime = x.CreationTime, CreatorId = x.CreatorId, LastModificationTime = x.LastModificationTime, LastModifierId = x.LastModifierId };
	private static AdmissionApplicantApplicationDto Map(AdmissionApplication x, IEnumerable<AdmissionApplicationStageInstance> stages) => new() { Id = x.Id, ApplicationNumber = x.ApplicationNumber, ApplicantId = x.ApplicantId, AdmissionCycleId = x.AdmissionCycleId, AdmissionOfferingId = x.AdmissionOfferingId, Status = x.Status, CurrentStageInstanceId = x.CurrentStageInstanceId, LastSavedAt = x.LastSavedAt, SubmittedAt = x.SubmittedAt, CreationTime = x.CreationTime, CreatorId = x.CreatorId, LastModificationTime = x.LastModificationTime, LastModifierId = x.LastModifierId, Stages = stages.OrderBy(s => s.DisplayOrder).Select(s => new AdmissionApplicantStageProgressDto { Code = s.Code, Name = s.Name, StageType = s.StageType, Status = s.Status, DisplayOrder = s.DisplayOrder, EnteredAt = s.EnteredAt, CompletedAt = s.CompletedAt }).ToList() };
	private static AdmissionNoteDto Map(AdmissionNote x) => new() { Id = x.Id, ApplicantId = x.ApplicantId, Text = x.Text, CreationTime = x.CreationTime, LastEditedAt = x.LastEditedAt, CreatorId = x.CreatorId };
}
