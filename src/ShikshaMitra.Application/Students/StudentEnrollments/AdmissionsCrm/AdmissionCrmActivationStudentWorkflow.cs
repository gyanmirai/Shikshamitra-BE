using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Admissions;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmActivationStudentWorkflow(
	IStudentRepository studentRepository,
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionCrmRegistrationApplication, Guid> registrationRepository,
	IRepository<AdmissionCrmIdentifierAllocation, Guid> identifierRepository,
	AdmissionCrmIdentifierWorkflow identifierWorkflow,
	AdmissionCrmVerifiedRegistrationStudentProjector registrationProjector,
	AdmissionCrmStudentActivationOrchestrator orchestrator) : ApplicationService, ITransientDependency
{
	public async Task<(Guid StudentId, string? Failure)> ActivateAsync(
		AdmissionCrmActivation activation,
		AdmissionInquiryLead lead,
		List<AdmissionCrmActivationStepDto> steps)
	{
		var student = lead.StudentId.HasValue ? await studentRepository.GetAsync(lead.StudentId.Value) : null;
		if (student == null)
		{
			var identifier = await identifierWorkflow.GetOrAllocateAsync(activation, lead, steps);
			if (identifier.Sequence > int.MaxValue) throw new BusinessException("AdmissionCrm:GrSequenceExceedsStudentLimit");
			if (!lead.DateOfBirth.HasValue) throw new BusinessException("AdmissionCrm:ActivationDateOfBirthRequired");
			student = Student.CreateDirectRegistration(GuidGenerator.Create(), CurrentTenant.Id,
				lead.FirstName, lead.MiddleName, lead.LastName, lead.AcademicYearId, activation.EducationBoardId,
				activation.AcademicMediumId, activation.AcademicMediumStageId, activation.StandardId,
				activation.DivisionId, lead.DateOfBirth.Value, activation.Gender,
				lead.PrimaryContactNo ?? string.Empty, email: lead.Email);
			student.MoveToAdmission();
			student.AssignGrNo((int)identifier.Sequence);
			if (activation.RollNumber.HasValue) student.AssignRollNo(activation.RollNumber.Value);
			await studentRepository.InsertAsync(student, autoSave: true);
			lead.LinkStudent(student.Id);
			await leadRepository.UpdateAsync(lead, autoSave: true);
			activation.LinkStudent(student.Id);
			identifier.Commit(student.Id, Clock.Now);
			await identifierRepository.UpdateAsync(identifier, autoSave: true);
			AdmissionCrmActivationMapper.UpsertStep(steps, "student_conversion",
				AdmissionCrmActivationStepStatus.Completed, $"Student {student.Id} created.");
		}
		else
		{
			activation.LinkStudent(student.Id);
			AdmissionCrmActivationMapper.UpsertStep(steps, "student_conversion",
				AdmissionCrmActivationStepStatus.Completed, $"Existing student {student.Id} reused.");
		}

		if (student.Status != StudentLifeCycleStatusEnum.Enrolled)
		{
			var verifiedRegistration = (await registrationRepository.GetListAsync(item =>
				item.LeadId == lead.Id &&
				(item.Status == AdmissionCrmRegistrationStatus.Verified ||
				 item.Status == AdmissionCrmRegistrationStatus.AdmissionReady)))
				.OrderByDescending(item => item.VerifiedAt)
				.FirstOrDefault();
			if (verifiedRegistration != null)
			{
				registrationProjector.Project(student, verifiedRegistration);
				await studentRepository.UpdateAsync(student, autoSave: true);
			}
			var outcome = await orchestrator.ActivateAsync(student.Id, activation.RequireStudentAccount,
				activation.RequireGuardianAccount, $"Activated from {activation.ActivationReference}.");
			if (!outcome.Success) return (student.Id, outcome.Message);
		}
		return (student.Id, null);
	}
}
