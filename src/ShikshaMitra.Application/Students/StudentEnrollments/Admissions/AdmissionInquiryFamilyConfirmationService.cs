using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Staff confirmation of one pending child's family; never a family or account merge.</summary>
[RemoteService(false)]
public class AdmissionInquiryFamilyConfirmationService(
	IAdmissionInquiryRepository inquiries,
	IRepository<AdmissionApplicant, Guid> applicants,
	IAdmissionApplicantRepository applicantLocks,
	IRepository<AdmissionFamily, Guid> families,
	IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionActivity, Guid> activities,
	AdmissionInquiryScopeAccessService inquiryScope,
	AdmissionApplicantScopeAccessService applicantScope,
	AdmissionDirectFamilyMatchService parentMatches,
	IPermissionChecker permissions,
	IUnitOfWorkManager units,
	IDataFilter filters) : ApplicationService, ITransientDependency
{
	public async Task ConfirmAsync(Guid inquiryId, ConfirmAdmissionInquiryFamilyDto input)
	{
		var actorId = CurrentUser.Id;
		if (!actorId.HasValue || !await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage))
			throw new AbpAuthorizationException();
		if (input.ExistingFamilyId == Guid.Empty || input.ExistingParentApplicantId == Guid.Empty || !AdmissionSiblingParentSelection.IsRelationship(input.ParentRelationship))
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput, "Choose a saved Father or Mother and their family.");
		using var unit = units.Begin(requiresNew: units.Current?.Options.IsTransactional != true, isTransactional: true);
		var initial = await inquiryScope.GetAsync(inquiryId);
		if (!initial.ApplicantId.HasValue) throw InvalidState();
		var targetApplicantId = initial.ApplicantId.Value;
		await applicantLocks.LockForSiblingParentSelectionAsync([targetApplicantId, input.ExistingParentApplicantId]);
		var inquiry = await inquiries.FindForApplicationStartAsync(inquiryId) ?? throw new EntityNotFoundException(typeof(AdmissionInquiry), inquiryId);
		if (inquiry.ApplicantId != targetApplicantId) throw new BusinessException(AdmissionInquiryManagementErrorCodes.ConcurrencyConflict);
		await inquiryScope.GetAsync(inquiryId); // Scope may have changed while waiting for the row lock.
		var target = inquiry.ApplicantId.HasValue ? await applicants.FindAsync(inquiry.ApplicantId.Value) : null;
		if (target == null || target.IsDeleted || target.TenantId != CurrentTenant.Id || inquiry.TenantId != CurrentTenant.Id || inquiry.FamilyId != target.FamilyId || target.Id == input.ExistingParentApplicantId)
			throw InvalidState();
		var source = await ReadSourceAsync(input.ExistingParentApplicantId, input.ExistingFamilyId);
		var confirmation = AdmissionSiblingParentSelection.ReadConfirmation(target);
		if (confirmation != null)
		{
			if (confirmation.ConfirmedBy == Guid.Empty) throw InvalidState();
			if (confirmation.FamilyId != input.ExistingFamilyId || confirmation.SourceApplicantId != source.Id || confirmation.Relationship != input.ParentRelationship)
				throw new BusinessException(AdmissionInquiryManagementErrorCodes.ConcurrencyConflict);
			await unit.CompleteAsync();
			return; // A retry never recopies a parent or appends a second confirmation event.
		}
		if (target.ExtraProperties.ContainsKey("SiblingParentConfirmation")) throw InvalidState();
		if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp) || inquiry.ConcurrencyStamp != input.ConcurrencyStamp)
			throw new BusinessException(AdmissionInquiryManagementErrorCodes.ConcurrencyConflict);
		await EnsurePendingGraphAsync(inquiry, target, input.ExistingFamilyId);
		var parent = await parentMatches.ResolveParentAsync(source.FamilyId, source.Id, input.ParentRelationship,
			inquiry.NormalizedPhone, inquiry.Email, AdmissionErrorCodes.InquiryFamilyMismatch);
		var oldFamilyId = target.FamilyId;
		var now = Clock.Now.ToUniversalTime();
		inquiry.ConfirmPendingSiblingFamily(target, oldFamilyId, source.FamilyId, now);
		AdmissionPrimaryContactSelection.Capture(target, input.ParentRelationship, parent.Name, parent.Mobile ?? string.Empty,
			parent.Email, false, actorId, now, false);
		AdmissionSiblingParentSelection.Confirm(target, parent, actorId.Value, now, inquiry.NormalizedPhone, inquiry.Email);
		AdmissionFamilyContactCapture.CopyToInquiry(target, inquiry);
		CaptureProof(inquiry, target);
		await applicants.UpdateAsync(target, autoSave: false);
		await inquiries.UpdateAsync(inquiry, autoSave: false);
		await activities.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity(
			"SiblingFamilyConfirmed", "Staff confirmed this pending child's sibling family.", target.Id, null, inquiry.Id,
			JsonSerializer.Serialize(new { PreviousFamilyId = oldFamilyId, FamilyId = source.FamilyId, SourceApplicantId = source.Id,
				Relationship = input.ParentRelationship, ConfirmedBy = actorId, ConfirmedAt = now })), autoSave: true);
		await unit.CompleteAsync();
	}

	private async Task<AdmissionApplicant> ReadSourceAsync(Guid sourceId, Guid familyId)
	{
		var canRead = false;
		var hasAction = false;
		if (await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View))
		{
			hasAction = true;
			canRead = await AsyncExecuter.AnyAsync(await applicantScope.GetApplicationsAsync(sourceId));
		}
		if (!canRead && await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View))
		{
			hasAction = true;
			canRead = await AsyncExecuter.AnyAsync((await inquiryScope.GetScopedQueryAsync()).Where(item => item.ApplicantId == sourceId));
		}
		if (!hasAction) throw new AbpAuthorizationException();
		if (!canRead) throw new EntityNotFoundException(typeof(AdmissionApplicant), sourceId);
		var source = await applicantScope.GetAsync(sourceId);
		var family = await families.FindAsync(familyId);
		if (source.IsDeleted || source.TenantId != CurrentTenant.Id || source.FamilyId != familyId || source.Status != AdmissionApplicantStatus.Active ||
			family == null || family.IsDeleted || family.TenantId != CurrentTenant.Id || family.Status != AdmissionFamilyStatus.Active)
			throw new BusinessException(AdmissionErrorCodes.InquiryFamilyMismatch);
		var students = LazyServiceProvider.LazyGetRequiredService<IStudentRepository>();
		var studentId = source.ExistingStudentId ?? source.ReservedStudentId;
		var linked = (await students.GetQueryableWithoutAutoIncludesAsync()).Where(student => student.TenantId == CurrentTenant.Id && student.Id == studentId);
		if (await AsyncExecuter.AnyAsync(linked))
		{
			if (!await AsyncExecuter.AnyAsync(await LazyServiceProvider.LazyGetRequiredService<StudentScopeAccessService>().ApplyCurrentUserScopeAsync(linked)))
				throw new EntityNotFoundException(typeof(AdmissionApplicant), sourceId);
		}
		else if (source.ExistingStudentId.HasValue) throw new EntityNotFoundException(typeof(AdmissionApplicant), sourceId);
		return source;
	}

	private async Task EnsurePendingGraphAsync(AdmissionInquiry inquiry, AdmissionApplicant target, Guid sourceFamilyId)
	{
		if (inquiry.Status is not (AdmissionInquiryStatus.New or AdmissionInquiryStatus.Contacted or AdmissionInquiryStatus.FollowUp) ||
			inquiry.DuplicateOfId.HasValue || target.Status != AdmissionApplicantStatus.Active || target.ExistingStudentId.HasValue || target.FamilyId == sourceFamilyId)
			throw InvalidState();
		var oldFamily = await families.FindAsync(target.FamilyId);
		if (oldFamily == null || oldFamily.IsDeleted || oldFamily.Status != AdmissionFamilyStatus.Active || oldFamily.TenantId != CurrentTenant.Id) throw InvalidState();
		using (filters.Disable<ISoftDelete>())
		{
			if (await applicants.AnyAsync(item => item.TenantId == CurrentTenant.Id && item.FamilyId == target.FamilyId && item.Id != target.Id) ||
				await inquiries.AnyAsync(item => item.TenantId == CurrentTenant.Id && item.Id != inquiry.Id && (item.ApplicantId == target.Id || item.FamilyId == target.FamilyId)) ||
				await applications.AnyAsync(item => item.TenantId == CurrentTenant.Id && (item.ApplicantId == target.Id || item.InquiryId == inquiry.Id)) ||
				await LazyServiceProvider.LazyGetRequiredService<IStudentRepository>().AnyAsync(item => item.TenantId == CurrentTenant.Id && item.Id == target.ReservedStudentId) ||
				await Repository<StudentOnboardingCase>().AnyAsync(item => item.TenantId == CurrentTenant.Id && (item.ApplicantId == target.Id || item.StudentId == target.ReservedStudentId)) ||
				await Repository<StudentPortalAccountLink>().AnyAsync(item => item.TenantId == CurrentTenant.Id && item.StudentId == target.ReservedStudentId) ||
				await Repository<StudentCredentialEmailRequest>().AnyAsync(item => item.TenantId == CurrentTenant.Id && item.StudentId == target.ReservedStudentId) ||
				await Repository<FinanceFeeAccount>().AnyAsync(item => item.TenantId == CurrentTenant.Id && (item.ApplicantId == target.Id || item.StudentId == target.ReservedStudentId)) ||
				await Repository<Charge>().AnyAsync(item => item.TenantId == CurrentTenant.Id && item.StudentId == target.ReservedStudentId) ||
				await Repository<FeeRecordMetadata>().AnyAsync(item => item.TenantId == CurrentTenant.Id && item.StudentId == target.ReservedStudentId) ||
				await Repository<StudentFeeStructure>().AnyAsync(item => item.TenantId == CurrentTenant.Id && item.StudentId == target.ReservedStudentId))
				throw InvalidState();
		}
	}

	private IRepository<T, Guid> Repository<T>() where T : class, IEntity<Guid> => LazyServiceProvider.LazyGetRequiredService<IRepository<T, Guid>>();
	private static BusinessException InvalidState() => new(AdmissionInquiryManagementErrorCodes.InvalidState,
		"Only a pending inquiry with its own untouched admission identity can be confirmed as a sibling. Reload the inquiry or contact the admission administrator.");

	private const string ProofKey = "AdmissionInquirySiblingFamilyConfirmation";
	internal static void CaptureProof(AdmissionInquiry inquiry, AdmissionApplicant applicant)
	{
		var proof = AdmissionSiblingParentSelection.ReadConfirmation(applicant);
		if (proof == null) return;
		inquiry.SetProperty(ProofKey, JsonSerializer.Serialize(new InquiryProof(1, applicant.Id, proof.FamilyId, proof.SourceApplicantId, proof.Relationship, proof.ConfirmedBy)));
	}
	internal static bool IsConfirmed(AdmissionInquiry inquiry)
	{
		try
		{
			var json = inquiry.GetProperty<string>(ProofKey);
			if (string.IsNullOrWhiteSpace(json)) return false;
			var proof = JsonSerializer.Deserialize<InquiryProof>(json);
			return proof is { Version: 1, ConfirmedBy: not null } && proof.ConfirmedBy != Guid.Empty && proof.ApplicantId == inquiry.ApplicantId &&
				proof.FamilyId == inquiry.FamilyId && proof.SourceApplicantId != Guid.Empty && proof.SourceApplicantId != inquiry.ApplicantId && AdmissionSiblingParentSelection.IsRelationship(proof.Relationship);
		}
		catch (JsonException) { return false; }
	}
	private sealed record InquiryProof(int Version, Guid ApplicantId, Guid FamilyId, Guid SourceApplicantId, string Relationship, Guid? ConfirmedBy);
}
