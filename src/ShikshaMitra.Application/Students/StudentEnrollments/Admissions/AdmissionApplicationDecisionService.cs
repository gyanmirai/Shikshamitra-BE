using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.ValueObjects;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionApplicationDecisionService(
	IRepository<AdmissionApplication, Guid> applicationRepository,
	IRepository<AdmissionApplicant, Guid> applicantRepository,
	IRepository<AdmissionFamily, Guid> familyRepository,
	IRepository<AdmissionGuardian, Guid> guardianRepository,
	IRepository<AdmissionCycle, Guid> cycleRepository,
	IRepository<AdmissionOffering, Guid> offeringRepository,
	IRepository<AdmissionApplicationDecisionHistory, Guid> historyRepository,
	IRepository<AdmissionActivity, Guid> activityRepository,
	IRepository<Division, Guid> divisionRepository,
	IStudentRepository studentRepository,
	IRepository<StudentEnrollment, Guid> enrollmentRepository)
	: ApplicationService, ITransientDependency
{
	public Task<AdmissionApplicationDto> MarkInReviewAsync(Guid id, AdmissionApplicationDecisionDto input) =>
		ChangeAsync(id, input, application => application.MarkInReview(CurrentUser.Id, input.Reason));

	public Task<AdmissionApplicationDto> ApproveAsync(Guid id, AdmissionApplicationDecisionDto input) =>
		ChangeAsync(id, input, application => application.Approve(CurrentUser.Id, input.Reason));

	public Task<AdmissionApplicationDto> RejectAsync(Guid id, AdmissionApplicationDecisionDto input) =>
		ChangeAsync(id, input, application => application.Reject(CurrentUser.Id, input.Reason ?? string.Empty));

	public Task<AdmissionApplicationDto> WaitlistAsync(Guid id, AdmissionApplicationDecisionDto input) =>
		ChangeAsync(id, input, application => application.Waitlist(CurrentUser.Id, input.Reason ?? string.Empty));

	public Task<AdmissionApplicationDto> IssueOfferAsync(Guid id, AdmissionApplicationDecisionDto input) =>
		ChangeAsync(id, input, application => application.IssueOffer(
			input.OfferExpiresAt?.ToUniversalTime() ?? throw new BusinessException(AdmissionErrorCodes.ApplicationOfferExpiryInvalid),
			CurrentUser.Id,
			input.Reason));

	public Task<AdmissionApplicationDto> AcceptOfferAsync(Guid id, AdmissionApplicationDecisionDto input) =>
		ChangeAsync(id, input, application => application.AcceptOffer(CurrentUser.Id, input.Reason));

	public Task<AdmissionApplicationDto> DeclineOfferAsync(Guid id, AdmissionApplicationDecisionDto input) =>
		ChangeAsync(id, input, application => application.DeclineOffer(CurrentUser.Id, input.Reason ?? string.Empty));

	public async Task<AdmissionEnrollmentResultDto> EnrollAsync(Guid id, EnrollAdmissionApplicationDto input)
	{
		var application = await GetApplicationAsync(id);
		EnsureConcurrency(application, input.ConcurrencyStamp);
		if (application.Status != AdmissionApplicationStatus.OfferAccepted)
			throw new BusinessException(AdmissionErrorCodes.ApplicationEnrollmentNotReady);

		var offering = await offeringRepository.FindAsync(application.AdmissionOfferingId) ??
			throw new EntityNotFoundException(typeof(AdmissionOffering), application.AdmissionOfferingId);
		var cycle = await cycleRepository.FindAsync(application.AdmissionCycleId) ??
			throw new EntityNotFoundException(typeof(AdmissionCycle), application.AdmissionCycleId);
		var division = await ResolveDivisionAsync(input.DivisionId, offering.StandardId);

		var applicant = await applicantRepository.FindAsync(application.ApplicantId) ??
			throw new EntityNotFoundException(typeof(AdmissionApplicant), application.ApplicantId);
		var student = applicant.ExistingStudentId.HasValue
			? await studentRepository.FindAsync(applicant.ExistingStudentId.Value)
			: await CreateStudentAsync(applicant, offering, cycle);
		if (student == null)
			throw new BusinessException(AdmissionErrorCodes.ApplicationEnrollmentDataMissing);

		var existingEnrollment = await enrollmentRepository.FirstOrDefaultAsync(enrollment =>
			enrollment.AcademicYearId == cycle.AcademicYearId && enrollment.StudentId == student.Id);
		if (existingEnrollment != null)
			throw new BusinessException(AdmissionErrorCodes.ApplicationEnrollmentDuplicate);
		if (division != null)
			await EnsureDivisionCapacityAsync(division, cycle.AcademicYearId);

		if (student.StandardId.HasValue && student.StandardId != offering.StandardId)
			throw new BusinessException(AdmissionErrorCodes.ApplicationEnrollmentDataMissing);
		student.UpdateAcademicPlacement(cycle.AcademicYearId, offering.BoardId, offering.MediumId,
			offering.StageId, offering.StandardId, input.DivisionId);
		student.ChangeStatus(StudentLifeCycleStatusEnum.Enrolled);
		var enrollment = StudentEnrollment.Create(GuidGenerator.Create(), CurrentTenant.Id, student.Id,
			cycle.AcademicYearId, offering.StandardId, division?.Id);
		await studentRepository.UpdateAsync(student, false);
		await enrollmentRepository.InsertAsync(enrollment, false);
		application.MarkEnrolled(student.Id, enrollment.Id);
		await applicationRepository.UpdateAsync(application, false);
		await historyRepository.InsertAsync(new AdmissionApplicationDecisionHistory(GuidGenerator.Create(),
			CurrentTenant.Id, application.Id, AdmissionApplicationStatus.Enrolled, "Applicant enrolled", CurrentUser.Id), false);
		await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id,
			"ApplicantEnrolled", "Applicant converted to Student enrollment", application.ApplicantId, application.Id), false);
		await CurrentUnitOfWork!.SaveChangesAsync();

		return new AdmissionEnrollmentResultDto
		{
			Application = AdmissionApplicationMapper.ToDto(application),
			StudentId = student.Id,
			StudentEnrollmentId = enrollment.Id,
			AcademicYearId = cycle.AcademicYearId,
			StandardId = offering.StandardId,
			DivisionId = input.DivisionId
		};
	}

	public async Task<AdmissionEnrollmentOptionsDto> GetEnrollmentOptionsAsync(Guid id)
	{
		var application = await GetApplicationAsync(id);
		if (application.Status != AdmissionApplicationStatus.OfferAccepted)
			throw new BusinessException(AdmissionErrorCodes.ApplicationEnrollmentNotReady);
		var offering = await offeringRepository.FindAsync(application.AdmissionOfferingId) ??
			throw new EntityNotFoundException(typeof(AdmissionOffering), application.AdmissionOfferingId);
		var cycle = await cycleRepository.FindAsync(application.AdmissionCycleId) ??
			throw new EntityNotFoundException(typeof(AdmissionCycle), application.AdmissionCycleId);
		var divisions = await divisionRepository.GetListAsync(item => item.StandardId == offering.StandardId);
		var divisionIds = divisions.Select(item => item.Id).ToHashSet();
		var activeEnrollments = await enrollmentRepository.GetListAsync(item =>
			item.AcademicYearId == cycle.AcademicYearId && item.IsActive && item.DivisionId.HasValue &&
			divisionIds.Contains(item.DivisionId.Value));
		var occupancy = activeEnrollments.GroupBy(item => item.DivisionId!.Value)
			.ToDictionary(group => group.Key, group => group.Count());

		return new AdmissionEnrollmentOptionsDto
		{
			StandardId = offering.StandardId,
			Divisions = divisions.OrderBy(item => item.Name).Select(item =>
			{
				var occupied = occupancy.GetValueOrDefault(item.Id);
				return new AdmissionEnrollmentDivisionOptionDto
				{
					Id = item.Id,
					Name = item.Name,
					Strength = item.Strength,
					OccupiedSeats = occupied,
					AvailableSeats = Math.Max(item.Strength - occupied, 0),
					IsAvailable = occupied < item.Strength
				};
			}).ToList()
		};
	}

	public async Task<List<AdmissionApplicationDecisionHistoryDto>> GetDecisionHistoryAsync(Guid id)
	{
		await GetApplicationAsync(id);
		var rows = await historyRepository.GetListAsync(item => item.ApplicationId == id);
		return rows.OrderByDescending(item => item.OccurredAt).Select(item => new AdmissionApplicationDecisionHistoryDto
		{
			Id = item.Id, Status = item.Status, Reason = item.Reason, ActorId = item.ActorId, OccurredAt = item.OccurredAt
		}).ToList();
	}

	private async Task<AdmissionApplicationDto> ChangeAsync(Guid id, AdmissionApplicationDecisionDto input, Action<AdmissionApplication> change)
	{
		var application = await GetApplicationAsync(id);
		EnsureConcurrency(application, input.ConcurrencyStamp);
		change(application);
		await applicationRepository.UpdateAsync(application, false);
		await historyRepository.InsertAsync(new AdmissionApplicationDecisionHistory(GuidGenerator.Create(), CurrentTenant.Id,
			application.Id, application.Status, input.Reason, CurrentUser.Id), false);
		await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id,
			"ApplicationDecisionChanged", $"Application status changed to {application.Status}", application.ApplicantId, application.Id), false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return AdmissionApplicationMapper.ToDto(application);
	}

	private async Task<AdmissionApplication> GetApplicationAsync(Guid id) => await applicationRepository.FindAsync(id) ??
		throw new EntityNotFoundException(typeof(AdmissionApplication), id);

	private async Task<Division?> ResolveDivisionAsync(Guid? divisionId, Guid standardId)
	{
		if (!divisionId.HasValue)
			return null;

		var division = await divisionRepository.FindAsync(divisionId.Value);
		if (division == null || division.StandardId != standardId)
			throw new BusinessException(AdmissionErrorCodes.ApplicationEnrollmentDivisionInvalid);

		return division;
	}

	private async Task EnsureDivisionCapacityAsync(Division division, Guid academicYearId)
	{
		var occupied = (await enrollmentRepository.GetListAsync(item =>
			item.AcademicYearId == academicYearId && item.DivisionId == division.Id && item.IsActive)).Count;
		if (occupied >= division.Strength)
			throw new BusinessException(AdmissionErrorCodes.ApplicationEnrollmentDivisionFull);
	}

	private static void EnsureConcurrency(AdmissionApplication application, string? stamp)
	{
		if (!string.IsNullOrWhiteSpace(stamp) && !string.Equals(stamp, application.ConcurrencyStamp, StringComparison.Ordinal))
			throw new BusinessException(AdmissionApplicationErrorCodes.ConcurrencyConflict);
	}

	private async Task<Student> CreateStudentAsync(AdmissionApplicant applicant, AdmissionOffering offering, AdmissionCycle cycle)
	{
		if (!applicant.DateOfBirth.HasValue || !Enum.TryParse<GenderEnum>(applicant.Gender, true, out var gender))
			throw new BusinessException(AdmissionErrorCodes.ApplicationEnrollmentDataMissing);
		var family = applicant.FamilyId == Guid.Empty ? null : await familyRepository.FindAsync(applicant.FamilyId);
		var guardian = family?.PrimaryGuardianId.HasValue == true
			? await guardianRepository.FindAsync(family.PrimaryGuardianId.Value) : null;
		if (guardian == null)
			throw new BusinessException(AdmissionErrorCodes.ApplicationEnrollmentDataMissing);
		var student = Student.CreateDirectRegistration(GuidGenerator.Create(), CurrentTenant.Id,
			applicant.FirstName, applicant.MiddleName, applicant.LastName, cycle.AcademicYearId,
			offering.BoardId, offering.MediumId, offering.StageId, offering.StandardId, null,
			applicant.DateOfBirth.Value, gender, guardian.Mobile, email: guardian.Email,
			address1: applicant.AddressLine1, address2: applicant.AddressLine2, city: applicant.City,
			state: string.IsNullOrWhiteSpace(applicant.State) ? null : new State(applicant.State, string.Empty),
			country: string.IsNullOrWhiteSpace(applicant.Country) ? null : new Country(applicant.Country, string.Empty),
			pincode: applicant.PostalCode);
		student.UpdateFamilyInfo(null, null, new ParentGuardianDetails(
			$"{guardian.FirstName} {guardian.LastName}".Trim(), null, null, guardian.Email, guardian.Mobile,
			null, null, null, null, null, null, null, null), null);
		return student;
	}
}
