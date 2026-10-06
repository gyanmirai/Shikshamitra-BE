using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Portal;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

[Authorize]
[RemoteService(false)]
public class StudentCredentialEmailAppService(IRepository<StudentCredentialEmailRequest, Guid> requests,
	IStudentCredentialEmailEligibilityResolver eligibility, IRepository<StudentOnboardingCase, Guid> cases) : ApplicationService, IStudentCredentialEmailAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View)]
	public async Task<IReadOnlyList<StudentCredentialEmailDto>> GetAsync(Guid caseId)
	{
		var result = new List<StudentCredentialEmailDto>();
		foreach (var type in new[] { "student", "guardian" })
		{
			var state = await eligibility.ResolveAsync(caseId, type);
			var row = state.LinkId.HasValue ? await requests.FindAsync(x => x.CaseId == caseId && x.AccountType == type && x.LinkId == state.LinkId) : null;
			var dto = Map(type, state, row);
			dto.CanSend &= await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit);
			result.Add(dto);
		}
		return result;
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[UnitOfWork(true)]
	public async Task<StudentCredentialEmailDto> RequestAsync(Guid caseId, RequestStudentCredentialEmailDto input)
	{
		ValidateType(input.AccountType);
		var state = await eligibility.ResolveAsync(caseId, input.AccountType);
		EnsureReady(state);
		var item = await cases.GetAsync(caseId);
		if (!string.IsNullOrWhiteSpace(input.ConcurrencyStamp) && input.ConcurrencyStamp != item.ConcurrencyStamp)
			throw new BusinessException(StudentOnboardingErrorCodes.ConcurrencyConflict);
		var row = await FindAsync(caseId, input.AccountType, state.LinkId!.Value);
		if (row == null) row = await InsertAsync(state, input.AccountType);
		else if (row.Status is not ("Pending" or "Processing"))
		{
			if (!row.CanRequest(DateTime.UtcNow)) throw Invalid("Wait at least two minutes between email requests.");
			row.Requeue(CurrentUser.Id, DateTime.UtcNow);
			await requests.UpdateAsync(row, false);
		}
		return Map(input.AccountType, state, row);
	}
	// Called by the onboarding transaction after provisioning. Repeated readiness refreshes are harmless.
	[UnitOfWork(true)]
	public virtual async Task QueueAsync(Guid caseId, string accountType)
	{
		ValidateType(accountType);
		var state = await eligibility.ResolveAsync(caseId, accountType);
		if (!state.EmailEnabled || !state.IsReady || !state.LinkId.HasValue || !state.UserId.HasValue) return;
		if (await FindAsync(caseId, accountType, state.LinkId.Value) != null) return;
		await InsertAsync(state, accountType);
	}
	private Task<StudentCredentialEmailRequest?> FindAsync(Guid caseId, string type, Guid linkId) =>
		requests.FindAsync(x => x.CaseId == caseId && x.AccountType == type && x.LinkId == linkId);
	private async Task<StudentCredentialEmailRequest> InsertAsync(StudentCredentialEmailEligibility state, string type)
	{
		if (!CurrentTenant.Id.HasValue) throw Invalid("A school tenant is required.");
		var row = new StudentCredentialEmailRequest(GuidGenerator.Create(), CurrentTenant.Id.Value, state.CaseId,
			state.StudentId, state.LinkId!.Value, state.UserId!.Value, type, state.Email, CurrentUser.Id, DateTime.UtcNow);
		return await requests.InsertAsync(row, false);
	}
	private static void ValidateType(string type)
	{
		if (type is not ("student" or "guardian")) throw Invalid("Select a student or guardian account.");
	}
	private static void EnsureReady(StudentCredentialEmailEligibility state)
	{
		if (!state.EmailEnabled || !state.IsReady || !state.LinkId.HasValue || !state.UserId.HasValue)
			throw Invalid(state.BlockedReason ?? "The account email is disabled or onboarding prerequisites are incomplete.");
	}
	private static BusinessException Invalid(string reason) => new BusinessException(StudentOnboardingErrorCodes.PortalAccountInvalid).WithData("Reason", reason);
	private static StudentCredentialEmailDto Map(string type, StudentCredentialEmailEligibility state, StudentCredentialEmailRequest? row) => new()
	{
		Id = row?.Id, AccountType = type, Email = state.Email, EmailEnabled = state.EmailEnabled,
		Status = row?.Status == "Processing" ? "Pending" : row?.Status ?? (state.EmailEnabled && !state.IsReady ? "Blocked" : "NotRequested"),
		Message = row?.Message ?? state.BlockedReason, SentAt = row?.SentAt, AttemptCount = row?.AttemptCount ?? 0,
		CanSend = state.EmailEnabled && state.IsReady && state.LinkId.HasValue && (row == null || row.CanRequest(DateTime.UtcNow))
	};
}
