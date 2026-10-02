using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

/// <summary>Admissions executes a frozen Records return authorization; it does not own a second approval workflow.</summary>
[RemoteService(false)]
public class StudentRecordAdmissionReturnService(StudentRecordsAccessService access, StudentRecordsPolicyResolver policies,
	StudentRecordTransitionReadinessService readiness, StudentRecordTransitionAuthorityService authority,
	IStudentRepository students, IRepository<StudentEnrollment, Guid> enrollments,
	IRepository<StudentRecordReturnHandoff, Guid> handoffs, IRepository<StudentRecordTransition, Guid> transitions,
	IRepository<StudentRecordTransitionBatch, Guid> batches, IRepository<StudentRecordTransitionEvent, Guid> events,
	IRepository<StudentRecordLegacyOverride, Guid> overrides, IRepository<StudentGuardianAccessReview, Guid> guardianReviews,
	IRepository<StudentOnboardingCase, Guid> onboardingCases,
	IRepository<AdmissionApplicant, Guid> applicants, IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionFamily, Guid> families, IRepository<AdmissionGuardian, Guid> guardians,
	IRepository<AcademicYear, Guid> years,
	IRepository<AdmissionFormDefinition, Guid> forms, IRepository<AdmissionFormFieldDefinition, Guid> fields,
	IRepository<AdmissionFormSectionDefinition, Guid> sections, AdmissionApplicationFormValidator formValidator,
	AdmissionOfferingRuntimeService offeringRuntime, AdmissionInquiryPolicyResolver inquiryPolicies,
	AdmissionApplicationScopeAccessService admissionScope, StudentScopeAccessService scope)
	: ApplicationService, ITransientDependency
{
	public async Task<StudentRecordReturnDto?> ReplayAsync(StudentRecordReturnInput input)
	{
		var handoff = await handoffs.FindAsync(x => x.TenantId == CurrentTenant.Id && x.IdempotencyKey == input.IdempotencyKey);
		if (handoff == null) return null;
		await access.EnsureReadableAsync(input.StudentId);
		if (handoff.StudentId != input.StudentId || handoff.RequestJson != JsonSerializer.Serialize(input))
			throw new BusinessException("Students:Records:IdempotencyConflict");
		return await MapAsync(handoff);
	}

	public async Task<StudentRecordReturnDto> CreateAsync(StudentRecordReturnInput input, string route)
	{
		Guid transitionId;
		using (var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true))
		{
			var replay = await ReplayAsync(input);
			if (replay != null) { await work.CompleteAsync(); return replay; }
			var student = (await access.EnsureWritableAsync(input.StudentId, placement: true)).Student;
			if (student.Status != StudentLifeCycleStatusEnum.Left || !student.AcademicYearId.HasValue ||
				string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000 || string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 128)
				throw new BusinessException("Students:Records:InvalidTransition");
			if (await transitions.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == student.Id && x.Outcome == "Return" &&
				x.Status != "Applied" && x.Status != "Rejected" && x.Status != "Cancelled")) throw new BusinessException("Students:Records:InvalidChangeState");
			if (await batches.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.IdempotencyKey == input.IdempotencyKey)) throw new BusinessException("Students:Records:IdempotencyConflict");
			var policy = await policies.ResolveAsync(student.AcademicYearId);
			if (!policy.Policy.Transitions.AllowedReturnRoutes.Contains(route)) throw new BusinessException("Students:Records:ReturnRouteNotAllowed");
			var source = await enrollments.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == student.Id && x.AcademicYearId == student.AcademicYearId);
			if (source?.IsActive == true) throw new BusinessException("Students:Records:InvalidTransition");
			if (source?.StandardId.HasValue == true) await scope.EnsureCanAccessStandardAsync(source.StandardId.Value, "return source class");
			if (source?.DivisionId.HasValue == true) await scope.EnsureCanAccessDivisionAsync(source.DivisionId.Value, "return source division");
			if (source == null) await RequireLegacyAuthorityAsync(student.Id);
			await readiness.RequireAdmissionReturnOwnerChecksAsync(student, policy.Policy);
			var runtime = route == "ResumeStage" ? await GetResumeRuntimeAsync(input, student.Id) : await offeringRuntime.GetAvailableAsync(input.AdmissionOfferingId ?? throw new BusinessException("Students:Records:AdmissionOfferingRequired"));
			await ValidateOfferingAsync(input, student, runtime);
			var entry = route == "ResumeStage" ? (AdmissionEntryMode?)null : ResolveEntry(input.EntryPath, runtime.Offering.EntryMode);
			var details = await students.GetWithDetailsAsync(student.Id) ?? throw new BusinessException("Students:Records:StaleSnapshot");
			if (route == "FreshApplication")
			{
				await AuthorizationService.CheckAsync(entry == AdmissionEntryMode.Inquiry ? ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Create : ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Start);
				var registration = await ResolveRegistrationAsync(input, details);
				if (entry == AdmissionEntryMode.Inquiry) await ValidateInquiryAsync(await InquiryInputAsync(input, details, runtime.Offering, registration), runtime.Offering);
			}
			else await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Start);
			var date = input.EffectiveDate ?? AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now);
			var preview = new StudentRecordTransitionPreviewRowDto(); readiness.ValidateAdmissionReturnDate(preview, date, policy.Policy);
			if (preview.Issues.Count > 0) throw new BusinessException(preview.Issues[0].Code);
			var command = new StudentRecordTransitionInput { StudentId = student.Id, Outcome = "Return", Reason = input.Reason, EffectiveDate = date,
				TargetAcademicYearId = input.TargetAcademicYearId, TargetStandardId = runtime.Offering.StandardId };
			var batch = new StudentRecordTransitionBatch(GuidGenerator.Create(), CurrentTenant.Id!.Value, student.AcademicYearId.Value,
				input.IdempotencyKey, JsonSerializer.Serialize(input), "[]");
			var rule = policy.Policy.Changes["Status"];
			var days = (date.Date - AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now)).TotalDays;
			transitionId = GuidGenerator.Create(); var handoffId = GuidGenerator.Create();
			var snapshot = await CaptureSourceAsync(student, source);
			var transition = StudentRecordTransition.CreateAdmissionReturn(transitionId, CurrentTenant.Id.Value, batch.Id, student.Id, student.AcademicYearId.Value,
				source?.Id, input.Reason, date, JsonSerializer.Serialize(command), snapshot, policy.VersionId, policy.Policy.ToJson(), JsonSerializer.Serialize(rule),
				CurrentUser.Id ?? throw new AbpAuthorizationException(), rule.RequiresApproval(days < 0, days > 0, true) ? rule.Stages.Count : 0, handoffId);
			var handoff = new StudentRecordReturnHandoff(handoffId, CurrentTenant.Id.Value, student.Id, input.TargetAcademicYearId,
				transitionId, runtime.Offering.Id, entry, route, input.IdempotencyKey, JsonSerializer.Serialize(input), policy.Policy.ToJson(), snapshot);
			handoff.ExtraProperties["IntakeConfiguration"] = await CaptureIntakeAsync(runtime.Offering);
			if (route == "FreshApplication")
			{
				handoff.ExtraProperties["IntakeContact"] = JsonSerializer.Serialize(await ResolveRegistrationAsync(input, details));
				handoff.ExtraProperties["IntakeFamilyId"] = (await FindFamilyAsync(student.Id))?.ToString("D") ?? "";
			}
			await batches.InsertAsync(batch); await transitions.InsertAsync(transition); await handoffs.InsertAsync(handoff);
			await events.InsertAsync(new(GuidGenerator.Create(), CurrentTenant.Id.Value, transition.Id, CurrentUser.Id!.Value, "Submitted", 0, input.Reason));
			await work.CompleteAsync();
		}
		await LazyServiceProvider.LazyGetRequiredService<StudentRecordTransitionApplication>().ApplyAsRecordedActorAsync(transitionId);
		using var read = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
		var result = await MapAsync(await handoffs.GetAsync(x => x.TenantId == CurrentTenant.Id && x.TransitionId == transitionId)); await read.CompleteAsync(); return result;
	}

	public async Task StartApprovedAsync(StudentRecordTransition transition)
	{
		var handoff = await GetHandoffAsync(transition);
		var student = await ValidateOriginAsync(handoff, transition);
		var input = JsonSerializer.Deserialize<StudentRecordReturnInput>(handoff.RequestJson)!;
		var runtime = await offeringRuntime.GetAvailableAsync(handoff.AdmissionOfferingId!.Value);
		await ValidateOfferingAsync(input, student, runtime);
		if (!handoff.ExtraProperties.TryGetValue("IntakeConfiguration", out var frozen) || frozen?.ToString() != await CaptureIntakeAsync(runtime.Offering))
			throw new BusinessException("Students:Records:StaleSnapshot");
		if (handoff.Route == "ResumeStage")
		{
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Start);
			await GetResumeRuntimeAsync(input, student.Id); handoff.LinkApplication(input.ResumeApplicationId!.Value);
		}
		else
		{
			ResolveEntry(handoff.EntryPath, runtime.Offering.EntryMode);
			var details = await students.GetWithDetailsAsync(student.Id) ?? throw new BusinessException("Students:Records:StaleSnapshot");
			var registration = await ResolveRegistrationAsync(input, details);
			if (handoff.ExtraProperties["IntakeContact"]?.ToString() != JsonSerializer.Serialize(registration) ||
				handoff.ExtraProperties["IntakeFamilyId"]?.ToString() != ((await FindFamilyAsync(student.Id))?.ToString("D") ?? "")) throw new BusinessException("Students:Records:StaleSnapshot");
			if (handoff.EntryPath == AdmissionEntryMode.Inquiry)
			{
				await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Create);
				var inquiry = await LazyServiceProvider.LazyGetRequiredService<AdmissionInquiryAppService>().CreateForReturnAsync(await InquiryInputAsync(input, details, runtime.Offering, registration), details);
				handoff.LinkInquiry(inquiry.Id);
			}
			else
			{
				await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Start);
				var familyId = await FindFamilyAsync(student.Id);
				var application = await LazyServiceProvider.LazyGetRequiredService<AdmissionDirectRegistrationService>().StartForReturnAsync(new()
				{ RequestId = handoff.Id, AdmissionOfferingId = runtime.Offering.Id, ApplicantFirstName = details.FirstName!, ApplicantMiddleName = details.MiddleName,
					ApplicantLastName = details.LastName, ApplicantDateOfBirth = details.AdditionalDetail?.DateOfBirth?.DateTime, ApplicantGender = details.AdditionalDetail?.Gender?.ToString(),
					ExistingFamilyId = familyId, GuardianFirstName = registration.GuardianFirstName, GuardianLastName = registration.GuardianLastName,
					GuardianMobile = registration.GuardianMobile, GuardianEmail = registration.GuardianEmail, GuardianRelationship = registration.GuardianRelationship }, details);
				handoff.LinkApplication(application.Id);
			}
		}
		await handoffs.UpdateAsync(handoff); transition.WaitForAdmission();
	}

	public async Task<StudentRecordReturnHandoff?> GetForApplicationAsync(AdmissionApplication application)
	{
		var handoff = await handoffs.FindAsync(x => x.TenantId == CurrentTenant.Id && x.TransitionId.HasValue &&
			(x.AdmissionApplicationId == application.Id || application.InquiryId.HasValue && x.AdmissionInquiryId == application.InquiryId));
		if (handoff == null) return null;
		if (handoff.AdmissionOfferingId != application.AdmissionOfferingId) throw new BusinessException("Students:Records:InvalidReturnApplication");
		handoff.LinkApplication(application.Id); await handoffs.UpdateAsync(handoff); return handoff;
	}
	public async Task<StudentRecordReturnHandoff?> ValidateFinalizationAsync(AdmissionApplication application, AdmissionApplicant applicant)
	{
		var handoff = await GetForApplicationAsync(application);
		if (handoff == null)
		{
			if (applicant.ExistingStudentId.HasValue && (await students.GetAsync(applicant.ExistingStudentId.Value)).Status == StudentLifeCycleStatusEnum.Left)
				throw new BusinessException("Students:Records:InvalidReturnApplication");
			return null;
		}
		var transition = await transitions.GetAsync(x => x.Id == handoff.TransitionId && x.TenantId == CurrentTenant.Id);
		if (transition.Status != "AwaitingAdmission" || applicant.ExistingStudentId != handoff.StudentId) throw new BusinessException("Students:Records:InvalidReturnApplication");
		await ValidateOriginAsync(handoff, transition); await RequireNoGuardianReviewAsync(handoff.StudentId); return handoff;
	}
	public async Task ValidateOnboardingAsync(StudentOnboardingCase item, bool sourceRequired = true)
	{
		if (!ReturnTransitionId(item).HasValue) return;
		var transition = await transitions.GetAsync(x => x.Id == ReturnTransitionId(item) && x.TenantId == CurrentTenant.Id);
		var handoff = await GetHandoffAsync(transition);
		if (handoff.AdmissionApplicationId != item.ApplicationId || handoff.StudentId != item.StudentId || handoff.AcademicYearId != item.AcademicYearId || handoff.AdmissionOfferingId != item.OfferingId)
			throw new BusinessException("Students:Records:InvalidReturnApplication");
		if (transition.TargetStandardId != item.IntendedStandardId) throw new BusinessException("Students:Records:InvalidReturnApplication");
		if (!sourceRequired && item.Status == StudentOnboardingStatus.Completed && transition.Status == "Applied" && transition.TargetEnrollmentId == item.StudentEnrollmentId) return;
		if (transition.Status != "AwaitingAdmission") throw new BusinessException("Students:Records:InvalidReturnApplication");
		await authority.ValidateAsync(transition); await RequireNoGuardianReviewAsync(item.StudentId);
		if (sourceRequired) await ValidateOriginAsync(handoff, transition);
	}
	public async Task<StudentEnrollment> ActivateEnrollmentAsync(StudentOnboardingCase item)
	{
		await ValidateOnboardingAsync(item);
		var transition = await transitions.GetAsync(x => x.Id == ReturnTransitionId(item) && x.TenantId == CurrentTenant.Id);
		var rollRules = LazyServiceProvider.LazyGetRequiredService<StudentOnboardingRollRuleReader>();
		await rollRules.ValidateAsync(item.StudentId, item.AcademicYearId, item.IntendedStandardId, item.DivisionId!.Value, item.RollNumber, transition.Reason, await rollRules.CaptureForCaseAsync(item));
		await readiness.ValidateTargetAsync(item.StudentId, transition.AcademicYearId, item.AcademicYearId, item.IntendedStandardId,
			item.DivisionId!.Value, item.RollNumber, true, allowHistoricalFirstEnrollment: !transition.SourceEnrollmentId.HasValue, excludeOnboardingCaseId: item.Id);
		var enrollment = await enrollments.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == item.StudentId && x.AcademicYearId == item.AcademicYearId);
		if (enrollment == null)
			return await enrollments.InsertAsync(StudentEnrollment.Create(GuidGenerator.Create(), CurrentTenant.Id, item.StudentId, item.AcademicYearId, item.IntendedStandardId, item.DivisionId, item.RollNumber, item.Id));
		if (transition.AcademicYearId != item.AcademicYearId || transition.SourceEnrollmentId != enrollment.Id || enrollment.IsActive)
			throw new BusinessException("Students:Records:TargetEnrollmentExists");
		enrollment.ChangePlacement(item.IntendedStandardId, item.DivisionId!.Value, item.RollNumber, transition.EffectiveDate); enrollment.Activate(transition.EffectiveDate);
		return await enrollments.UpdateAsync(enrollment);
	}
	public async Task CompleteAsync(StudentOnboardingCase item)
	{
		if (!ReturnTransitionId(item).HasValue) return;
		await ValidateOnboardingAsync(item, false);
		var transition = await transitions.GetAsync(x => x.Id == ReturnTransitionId(item) && x.TenantId == CurrentTenant.Id);
		if (item.Status == StudentOnboardingStatus.Completed && transition.Status == "Applied" && transition.TargetEnrollmentId == item.StudentEnrollmentId) return;
		var enrollment = await enrollments.GetAsync(x => x.Id == item.StudentEnrollmentId && x.TenantId == CurrentTenant.Id && x.StudentId == item.StudentId && x.IsActive);
		var student = await students.GetAsync(item.StudentId);
		transition.Apply(enrollment.Id, StudentRecordTransitionReadinessService.Capture(student, enrollment), Clock.Now); await transitions.UpdateAsync(transition);
		await events.InsertAsync(new(GuidGenerator.Create(), CurrentTenant.Id!.Value, transition.Id, CurrentUser.Id ?? transition.LastApprovedBy ?? transition.RequestedBy, "Applied", transition.CurrentApprovalStage, "Reviewed return onboarding completed."));
	}
	public async Task RequireNoGuardianReviewAsync(Guid studentId)
	{
		if (await guardianReviews.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.Status == "Pending")) throw new BusinessException("Students:Records:RenewalChecklistRequired")
			.WithData("Reason", "Resolve the pending guardian access review before returning the student.");
	}
	public static Guid? ReturnTransitionId(StudentOnboardingCase item) => item.ExtraProperties.TryGetValue("RecordReturnTransitionId", out var raw) && Guid.TryParse(raw?.ToString(), out var id) ? id : null;
	public async Task<StudentRecordReturnHandoff> GetHandoffAsync(StudentRecordTransition transition)
	{
		var handoff = await handoffs.GetAsync(x => x.Id == transition.ReturnHandoffId && x.TenantId == CurrentTenant.Id);
		if (handoff.TransitionId != transition.Id || handoff.StudentId != transition.StudentId) throw new BusinessException("Students:Records:InvalidReturnApplication"); return handoff;
	}
	private async Task<Student> ValidateOriginAsync(StudentRecordReturnHandoff handoff, StudentRecordTransition transition)
	{
		await authority.ValidateAsync(transition);
		var student = (await access.EnsureWritableAsync(handoff.StudentId, placement: true)).Student;
		var enrollment = await enrollments.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == student.Id && x.AcademicYearId == transition.AcademicYearId);
		if (student.Status != StudentLifeCycleStatusEnum.Left || enrollment?.IsActive == true || transition.SourceEnrollmentId != enrollment?.Id || await CaptureSourceAsync(student, enrollment) != handoff.SourceSnapshotJson)
			throw new BusinessException("Students:Records:StaleSnapshot");
		if (enrollment?.StandardId.HasValue == true) await scope.EnsureCanAccessStandardAsync(enrollment.StandardId.Value, "return source class");
		if (enrollment?.DivisionId.HasValue == true) await scope.EnsureCanAccessDivisionAsync(enrollment.DivisionId.Value, "return source division");
		if (enrollment == null) await RequireLegacyAuthorityAsync(student.Id);
		await readiness.RequireAdmissionReturnOwnerChecksAsync(student, StudentRecordsPolicy.FromJson(transition.PolicyJson)); return student;
	}
	public async Task RequireLegacyAuthorityAsync(Guid studentId)
	{
		var approved = (await overrides.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.Status == "Approved"))
			.SingleOrDefault(x => x.DecidedByUserId.HasValue && x.DecidedByUserId != x.RequestedByUserId) ?? throw new BusinessException("Students:Records:LegacyStudentNotEligible");
		await authority.EnsureRecordedActorPermissionAsync(approved.DecidedByUserId!.Value, ShikshaMitraSubmodulePermissions.Students.Records_Overrides.Approve);
	}
	private async Task<AdmissionOfferingRuntimeContext> GetResumeRuntimeAsync(StudentRecordReturnInput input, Guid studentId)
	{
		if (!input.ResumeApplicationId.HasValue) throw new BusinessException("Students:Records:ResumeApplicationRequired");
		var application = await applications.GetAsync(x => x.Id == input.ResumeApplicationId && x.TenantId == CurrentTenant.Id);
		await admissionScope.EnsureCanAccessAsync(application);
		var applicant = await applicants.GetAsync(x => x.Id == application.ApplicantId && x.TenantId == CurrentTenant.Id);
		if (await onboardingCases.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.ApplicationId == application.Id)) throw new BusinessException("Students:Records:InvalidReturnApplication");
		if (applicant.ExistingStudentId != studentId || application.StudentId.HasValue || application.OnboardingCaseId.HasValue ||
			application.Status is AdmissionApplicationStatus.AdmissionFinalized or AdmissionApplicationStatus.Enrolled or AdmissionApplicationStatus.Rejected or AdmissionApplicationStatus.Withdrawn or AdmissionApplicationStatus.OfferDeclined)
			throw new BusinessException("Students:Records:InvalidReturnApplication");
		return await offeringRuntime.GetAvailableAsync(application.AdmissionOfferingId);
	}
	private async Task ValidateOfferingAsync(StudentRecordReturnInput input, Student student, AdmissionOfferingRuntimeContext runtime)
	{
		if (runtime.Cycle.AcademicYearId != input.TargetAcademicYearId || input.TargetStandardId.HasValue && input.TargetStandardId != runtime.Offering.StandardId)
			throw new BusinessException("Students:Records:InvalidTransitionYear");
		await scope.EnsureCanAccessStandardAsync(runtime.Offering.StandardId, "return admission class");
		var sourceYear = await years.GetAsync(x => x.Id == student.AcademicYearId && x.TenantId == CurrentTenant.Id);
		var targetYear = await years.GetAsync(x => x.Id == input.TargetAcademicYearId && x.TenantId == CurrentTenant.Id);
		if (sourceYear.Id != targetYear.Id && targetYear.StartDate <= sourceYear.StartDate) throw new BusinessException("Students:Records:InvalidTransitionYear");
	}
	internal static AdmissionEntryMode ResolveEntry(AdmissionEntryMode? chosen, AdmissionEntryMode configured)
	{
		if (!Enum.IsDefined(configured) || chosen.HasValue && chosen is not (AdmissionEntryMode.Inquiry or AdmissionEntryMode.DirectRegistration) || configured == AdmissionEntryMode.Both && !chosen.HasValue ||
			configured != AdmissionEntryMode.Both && chosen.HasValue && chosen != configured) throw new BusinessException("Students:Records:ReturnEntryPathRequired");
		return chosen ?? configured;
	}
	private async Task<Guid?> FindFamilyAsync(Guid studentId)
	{
		var linked = (await applicants.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.ExistingStudentId == studentId && x.Status == AdmissionApplicantStatus.Active)).OrderByDescending(x => x.CreationTime);
		foreach (var applicant in linked)
		{
			var family = await families.FindAsync(x => x.Id == applicant.FamilyId && x.TenantId == CurrentTenant.Id && x.Status == AdmissionFamilyStatus.Active);
			if (family?.PrimaryGuardianId.HasValue == true && await guardians.AnyAsync(x => x.Id == family.PrimaryGuardianId && x.FamilyId == family.Id && x.TenantId == CurrentTenant.Id)) return family.Id;
		}
		return null;
	}
	public async Task<bool> RequiresRegistrationAsync(Guid studentId) => !(await FindFamilyAsync(studentId)).HasValue;
	public async Task<StudentRecordReturnInquiryIntakeDto> GetIntakeAsync(Guid offeringId, Guid? studentId)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Create);
		var runtime = await offeringRuntime.GetAvailableAsync(offeringId);
		if (runtime.Offering.EntryMode == AdmissionEntryMode.DirectRegistration) throw new BusinessException(AdmissionErrorCodes.InquiryEntryNotAllowed);
		var policy = (await inquiryPolicies.ResolveAsync(offeringId))?.Policy;
		var dto = new StudentRecordReturnInquiryIntakeDto
		{ Form = runtime.Offering.InquiryFormDefinitionId.HasValue ? await LazyServiceProvider.LazyGetRequiredService<AdmissionFormAppService>().GetPublishedIntakeAsync(runtime.Offering.InquiryFormDefinitionId.Value) : null,
			AllowedSources = policy?.AllowedSources.Count > 0 ? policy.AllowedSources : Enum.GetValues<AdmissionInquirySource>().ToList(),
			RequireSourceDetailFor = (policy?.RequireSourceDetailFor ?? []).Concat(new[] { AdmissionInquirySource.QrCode, AdmissionInquirySource.SchoolFair }).Distinct().ToList(),
			RequireGuardianEmail = policy?.RequireGuardianEmail ?? false, RequireApplicantDateOfBirth = policy?.RequireApplicantDateOfBirth ?? false };
		foreach (var field in dto.Form?.Sections.SelectMany(s => s.Fields).Where(f => IsServerBound(f.BindingKey)) ?? [])
		{
			field.ReadOnly = true; dto.ServerBoundFieldCodes.Add(field.Code);
		}
		if (studentId.HasValue)
		{
			var record = await access.EnsureReadableAsync(studentId.Value);
			var roles = await access.GetDatabaseRoleNamesAsync();
			var student = await students.GetWithDetailsAsync(studentId.Value) ?? throw new BusinessException("Students:Records:StaleSnapshot");
			var registration = await FindFamilyAsync(student.Id) is { } ? await ResolveRegistrationAsync(new() { StudentId = student.Id }, student) : null;
			foreach (var value in await BoundValuesAsync(student, registration, runtime.Offering))
			{
				var definition = dto.Form?.Sections.SelectMany(s => s.Fields).SingleOrDefault(f => f.Code == value.FieldCode);
				var group = definition?.BindingKey?.StartsWith("Guardian.", StringComparison.OrdinalIgnoreCase) == true ? "Guardian" : "Identity";
				if (!record.Policy.IsMasked(group, roles) && !(group == "Guardian" && record.Policy.IsMasked("Contact", roles))) dto.BoundValues.Add(value);
			}
		}
		return dto;
	}
	private async Task<System.Collections.Generic.List<AdmissionFormValueDto>> BoundValuesAsync(Student student, StudentRecordReturnRegistrationDto? registration, AdmissionOffering offering)
	{
		if (!offering.InquiryFormDefinitionId.HasValue) return [];
		var applicant = new AdmissionApplicant(GuidGenerator.Create(), CurrentTenant.Id, GuidGenerator.Create(), student.FirstName ?? "", student.MiddleName, student.LastName,
			student.AdditionalDetail?.DateOfBirth?.DateTime, student.AdditionalDetail?.Gender?.ToString(), student.Id, student.Id);
		var guardian = registration == null ? null : new AdmissionGuardian(GuidGenerator.Create(), CurrentTenant.Id, applicant.FamilyId,
			registration.GuardianFirstName, registration.GuardianLastName, registration.GuardianMobile, registration.GuardianEmail, registration.GuardianRelationship, true);
		return (await fields.GetListAsync(x => x.FormDefinitionId == offering.InquiryFormDefinitionId && x.BindingType == AdmissionFormBindingType.System && x.BindingKey != null))
			.Select(field => new AdmissionFormValueDto { FieldCode = field.Code, ValueJson = JsonSerializer.Serialize(AdmissionApplicationMapper.GetBoundValue(field.BindingKey!, applicant, guardian)) })
			.Where(x => x.ValueJson != "null").ToList();
	}
	private async Task<StudentRecordReturnRegistrationDto> ResolveRegistrationAsync(StudentRecordReturnInput input, Student student)
	{
		var familyId = await FindFamilyAsync(student.Id);
		if (familyId.HasValue)
		{
			var family = await families.GetAsync(familyId.Value); var guardian = await guardians.GetAsync(family.PrimaryGuardianId!.Value);
			return new() { GuardianFirstName = guardian.FirstName, GuardianLastName = guardian.LastName, GuardianMobile = guardian.Mobile!, GuardianEmail = guardian.Email, GuardianRelationship = guardian.Relationship };
		}
		return input.Registration ?? throw new BusinessException("Students:Records:ReturnRegistrationRequired");
	}
	private async Task<CreateAdmissionInquiryDto> InquiryInputAsync(StudentRecordReturnInput input, Student student, AdmissionOffering offering, StudentRecordReturnRegistrationDto registration)
	{
		var evidence = input.Inquiry ?? throw new BusinessException("Students:Records:ReturnInquiryRequired");
		var dto = new CreateAdmissionInquiryDto { Source = evidence.Source ?? throw new BusinessException("Students:Records:ReturnInquiryRequired"), SourceDetail = evidence.SourceDetail, PreferredLanguageCode = evidence.PreferredLanguageCode,
			FormValues = JsonSerializer.Deserialize<System.Collections.Generic.List<AdmissionFormValueDto>>(JsonSerializer.Serialize(evidence.FormValues))! };
		if (offering.InquiryFormDefinitionId.HasValue)
		{
			var serverFields = (await fields.GetListAsync(x => x.FormDefinitionId == offering.InquiryFormDefinitionId)).Where(x => IsServerBound(x.BindingKey)).Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
			dto.FormValues.RemoveAll(x => serverFields.Contains(x.FieldCode));
		}
		dto.ExistingFamilyId = await FindFamilyAsync(student.Id); dto.CreateSeparateFamily = false;
		dto.AdmissionOfferingId = offering.Id; dto.AdmissionCycleId = offering.AdmissionCycleId; dto.BoardId = offering.BoardId; dto.MediumId = offering.MediumId; dto.StandardId = offering.StandardId;
		dto.ApplicantFirstName = student.FirstName!; dto.ApplicantLastName = student.LastName; dto.ApplicantDateOfBirth = student.AdditionalDetail?.DateOfBirth?.DateTime;
		dto.ApplicantName = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
		dto.GuardianFirstName = registration.GuardianFirstName; dto.GuardianLastName = registration.GuardianLastName; dto.GuardianPhone = registration.GuardianMobile; dto.GuardianEmail = registration.GuardianEmail;
		foreach (var value in await BoundValuesAsync(student, registration, offering))
			if (!dto.FormValues.Any(x => string.Equals(x.FieldCode, value.FieldCode, StringComparison.OrdinalIgnoreCase))) dto.FormValues.Add(value);
		return dto;
	}
	private static bool IsServerBound(string? key) => key?.StartsWith("Applicant.", StringComparison.OrdinalIgnoreCase) == true || key?.StartsWith("Guardian.", StringComparison.OrdinalIgnoreCase) == true;
	private async Task ValidateInquiryAsync(CreateAdmissionInquiryDto input, AdmissionOffering offering)
	{
		if (!Enum.IsDefined(input.Source)) throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput);
		if (input.FormValues.GroupBy(x => x.FieldCode, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput);
		var policy = (await inquiryPolicies.ResolveAsync(offering.Id))?.Policy;
		if (policy != null && policy.AllowedSources.Count > 0 && !policy.AllowedSources.Contains(input.Source)) throw new BusinessException(AdmissionInquiryManagementErrorCodes.SourceNotAllowed);
		if ((policy?.RequireSourceDetailFor.Contains(input.Source) == true || input.Source is AdmissionInquirySource.QrCode or AdmissionInquirySource.SchoolFair) && string.IsNullOrWhiteSpace(input.SourceDetail))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.SourceDetailRequired);
		if (policy?.RequireGuardianEmail == true && string.IsNullOrWhiteSpace(input.GuardianEmail) || policy?.RequireApplicantDateOfBirth == true && !input.ApplicantDateOfBirth.HasValue)
			throw new BusinessException(AdmissionErrorCodes.InquiryIdentityIncomplete);
		if (offering.InquiryFormDefinitionId.HasValue)
		{
			var result = formValidator.Validate(await fields.GetListAsync(x => x.FormDefinitionId == offering.InquiryFormDefinitionId), await sections.GetListAsync(x => x.FormDefinitionId == offering.InquiryFormDefinitionId),
				input.FormValues.ToDictionary(x => x.FieldCode, x => AdmissionApplicationValueConverter.Unwrap(x.ValueJson), StringComparer.OrdinalIgnoreCase));
			if (result.Errors.Count > 0) throw new BusinessException(AdmissionApplicationErrorCodes.ValidationFailed).WithData("Fields", string.Join(", ", result.FieldErrors.Keys));
		}
	}
	private async Task<string> CaptureIntakeAsync(AdmissionOffering offering)
	{
		var form = offering.InquiryFormDefinitionId.HasValue ? await forms.GetAsync(offering.InquiryFormDefinitionId.Value) : null;
		return JsonSerializer.Serialize(new { offering.EntryMode, offering.BoardId, offering.MediumId, offering.StageId, offering.StandardId, offering.InquiryFormDefinitionId, FormVersion = form?.Version, FormStamp = form?.ConcurrencyStamp, Policy = (await inquiryPolicies.ResolveAsync(offering.Id))?.SnapshotJson });
	}
	private async Task<string> CaptureSourceAsync(Student student, StudentEnrollment? enrollment)
	{
		var exits = await transitions.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == student.Id && x.Status == "Applied" && (x.Outcome == "Withdraw" || x.Outcome == "Transfer" || x.Outcome == "Graduate"));
		var exit = exits.OrderByDescending(x => x.AppliedAt).ThenByDescending(x => x.CreationTime).FirstOrDefault();
		return JsonSerializer.Serialize(new
		{ student.AcademicYearId, student.AcademicBoardId, student.AcademicMediumId, student.AcademicMediumStageId, student.StandardId, student.DivisionId, student.RollNo, student.Status,
			student.GrNumber, student.GrNo, EnrollmentId = enrollment?.Id, EnrollmentYear = enrollment?.AcademicYearId, EnrollmentStandard = enrollment?.StandardId,
			EnrollmentDivision = enrollment?.DivisionId, EnrollmentRoll = enrollment?.RollNumber, EnrollmentActive = enrollment?.IsActive, EnrollmentOriginCase = enrollment?.OnboardingCaseId,
			EnrollmentEffectiveHistory = enrollment?.AttendancePlacementHistoryJson, LastExitId = exit?.Id, LastExitEffectiveDate = exit?.EffectiveDate, LastExitAppliedAt = exit?.AppliedAt });
	}
	private async Task<StudentRecordReturnDto> MapAsync(StudentRecordReturnHandoff handoff)
	{
		var transition = handoff.TransitionId.HasValue ? await transitions.GetAsync(x => x.Id == handoff.TransitionId && x.TenantId == CurrentTenant.Id) : null;
		return new() { StudentId = handoff.StudentId, Route = handoff.Route, TransitionId = transition?.Id, Status = transition?.Status ?? "AdmissionHandoff",
			AdmissionApplicationId = await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View) ? handoff.AdmissionApplicationId : null,
			AdmissionInquiryId = await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View) ? handoff.AdmissionInquiryId : null };
	}
}
