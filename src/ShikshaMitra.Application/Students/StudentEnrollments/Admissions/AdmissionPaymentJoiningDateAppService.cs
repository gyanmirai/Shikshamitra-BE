using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
public class AdmissionPaymentJoiningDateAppService(
	AdmissionFeeSelectionAccessService access,
	AdmissionJoiningDateReader reader,
	AdmissionJoiningDateCapture capture,
	IAdmissionFinanceContextReader financeContext,
	StudentFeeStructureAppService calculator,
	IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<FinanceFeeAccount, Guid> accounts,
	IReadOnlyRepository<AcademicYear, Guid> years,
	IRepository<AdmissionActivity, Guid> activities,
	IUnitOfWorkManager units) : ApplicationService, IAdmissionPaymentJoiningDateAppService
{
	public async Task<AdmissionPaymentJoiningDateDto> GetAsync(Guid applicationId)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View);
		var application = await access.GetApplicationAsync(applicationId);
		return await ReadAsync(application, await financeContext.GetAsync(applicationId));
	}

	public async Task<AdmissionPaymentJoiningDatePreviewDto> PreviewAsync(Guid applicationId, PreviewAdmissionPaymentJoiningDateDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View);
		var application = await access.GetApplicationAsync(applicationId);
		var source = await financeContext.GetAsync(applicationId);
		var state = await ReadAsync(application, source);
		if (!state.CanPreview) throw new BusinessException(AdmissionFeeSelectionErrorCodes.InvalidState, state.LockReason);
		var date = ValidateDate(input.JoiningDate, state.AcademicYearEndDate);
		ValidateVariableAmounts(input);
		var account = await accounts.FindAsync(a => a.ApplicationId == applicationId && a.TenantId == CurrentTenant.Id);
		var calculated = await calculator.PrepareAdmissionAsync(source with { JoiningDate = date }, account?.Id ?? GuidGenerator.Create(), input.VariableFeeHeads);
		return new()
		{
			JoiningDate = date, TotalAmount = calculated.Preview.ProratedTotal,
			ScheduledAmount = calculated.Preview.ScheduledTotal, ReductionAmount = calculated.Preview.ReductionTotal,
			Proration = AdmissionFeeCollectionAppService.ProrationResult(calculated.Preview, calculated.Dues),
			Warnings = calculated.Preview.Warnings
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add)]
	public async Task<AdmissionPaymentJoiningDateDto> SaveAsync(Guid applicationId, SaveAdmissionPaymentJoiningDateDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View);
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add);
		// Serializable reads of the source and account prevent racing Finance preparation
		// from freezing a different date while this transaction confirms the admission fact.
		using var unit = units.Begin(new AbpUnitOfWorkOptions
		{ IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
		var application = await access.GetApplicationAsync(applicationId);
		await EnsureEditPermissionAsync(application);
		if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp) || application.ConcurrencyStamp != input.ConcurrencyStamp)
			throw new BusinessException(AdmissionFeeSelectionErrorCodes.ConcurrencyConflict, "The application changed. Refresh before confirming the joining date.");
		var source = await financeContext.GetAsync(applicationId);
		var state = await ReadAsync(application, source);
		var date = ValidateDate(input.JoiningDate, state.AcademicYearEndDate);
		if (state.IsLocked) AdmissionJoiningDateCapture.ThrowLocked();
		if (!state.CanEdit) throw new BusinessException(AdmissionFeeSelectionErrorCodes.InvalidState, state.LockReason);
		var previous = await reader.ReadAsync(application);
		await capture.SynchronizeAsync(application, date);
		await applications.UpdateAsync(application, false);
		await activities.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity(
			"AdmissionJoiningDateConfirmed", "Actual joining date confirmed for fee preparation.", application.ApplicantId, application.Id,
			metadataJson: JsonSerializer.Serialize(new { Previous = previous, JoiningDate = date })), false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		var result = await ReadAsync(application, source with { JoiningDate = date });
		await unit.CompleteAsync();
		return result;
	}

	private async Task<AdmissionPaymentJoiningDateDto> ReadAsync(AdmissionApplication application, AdmissionFinanceContext source)
	{
		var year = await years.GetAsync(source.AcademicYearId);
		var locked = await capture.IsLockedAsync(application);
		var ownerEditable = await HasEditPermissionAsync(application);
		var active = false;
		if (application.Status == AdmissionApplicationStatus.AdmissionFinalized)
		{
			var item = ownerEditable ? await access.FindOnboardingCaseAsync(application) : null;
			active = item != null && item.Status is not (StudentOnboardingStatus.Completed or StudentOnboardingStatus.Cancelled);
		}
		else if (application.CurrentStageInstanceId.HasValue && application.CanEndJourney)
		{
			var stage = await stages.FindAsync(application.CurrentStageInstanceId.Value);
			active = stage?.StageType == AdmissionWorkflowStageType.Payment && stage.Status == AdmissionRuntimeStageStatus.Active;
		}
		var canEdit = !locked && ownerEditable && active && source.CanCollect &&
			await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add);
		return new()
		{
			JoiningDate = await reader.ReadAsync(application), ConcurrencyStamp = application.ConcurrencyStamp,
			CanEdit = canEdit, CanPreview = canEdit && source.StructureId.HasValue, IsLocked = locked,
			LockReason = locked ? "Fees have already been prepared. Ask Finance to review a joining date correction." :
				!source.CanCollect ? source.Blocker : !active ? "Confirm the joining date during an open Payment or Onboarding step." :
				!canEdit ? "You do not have permission to confirm the joining date." : null,
			AcademicYearStartDate = DateTime.SpecifyKind(year.StartDate.Date, DateTimeKind.Utc),
			AcademicYearEndDate = DateTime.SpecifyKind(year.EndDate.Date, DateTimeKind.Utc)
		};
	}

	private Task<bool> HasEditPermissionAsync(AdmissionApplication application) => AuthorizationService.IsGrantedAsync(
		application.Status == AdmissionApplicationStatus.AdmissionFinalized
			? ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit
			: ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Edit);

	private async Task EnsureEditPermissionAsync(AdmissionApplication application)
	{
		if (!await HasEditPermissionAsync(application)) throw new AbpAuthorizationException();
	}

	private static DateTime ValidateDate(string value, DateTime end)
	{
		if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date.Date > end.Date)
			throw new BusinessException(ShikshaMitra.Domain.Finance.Policies.ProrationPolicyErrorCodes.JoiningDateInvalid,
				"Enter a valid actual joining date on or before the academic year end.");
		return DateTime.SpecifyKind(date, DateTimeKind.Utc);
	}

	private static void ValidateVariableAmounts(PreviewAdmissionPaymentJoiningDateDto input)
	{
		if (input.VariableFeeHeads == null || input.VariableFeeHeads.Count > 200 ||
			input.VariableFeeHeads.Any(v => v == null || v.FeeHeadId == Guid.Empty || !v.Amount.HasValue || v.Amount < 0m ||
				v.Amount > 9_999_999_999.99m || decimal.Round(v.Amount.Value, 2) != v.Amount || v.ZeroAmountReason?.Length > 500 ||
				v.Amount == 0m && string.IsNullOrWhiteSpace(v.ZeroAmountReason)) ||
			input.VariableFeeHeads.Select(v => v.FeeHeadId).Distinct().Count() != input.VariableFeeHeads.Count)
			throw new Volo.Abp.Validation.AbpValidationException("Invalid variable fee amounts.");
	}
}
