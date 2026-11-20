using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Portal;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Authorization;

namespace ShikshaMitra.Library.Circulation;

public partial class LibraryLoanAppService
{
	private async Task<Guid> BeginCommandAsync(string action, string key)
	{
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue || CurrentUser.Id == Guid.Empty ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.View) ||
			!await permissions.IsGrantedAsync(action)) throw new AbpAuthorizationException();
		if (string.IsNullOrWhiteSpace(key) || key.Length > 100 || key.Contains('\0') || key != key.Trim())
			throw new BusinessException(LibraryErrorCodes.ContextInvalid, "Use a valid operation key to submit this action.");
		await access.GetContextAsync();
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		return CurrentUser.Id.Value;
	}

	private async Task<LibraryLoanCommandResultDto?> ReplayAsync(Guid actorId, LibraryLoanOperation operation,
		string key, string fingerprint, LibraryPortalOwner? owner = null)
	{
		var receipt = await AsyncExecuter.FirstOrDefaultAsync((await commands.GetQueryableAsync()).Where(row =>
			row.TenantId == CurrentTenant.Id && row.ActorId == actorId && row.Operation == operation && row.IdempotencyKey == key));
		if (receipt == null) return null;
		if (owner == null) await RequireReadableLoanAsync(receipt.LoanId);
		else await RequirePortalLoanAsync(owner, receipt.LoanId);
		if (!string.Equals(receipt.PayloadFingerprint, fingerprint, StringComparison.Ordinal))
			throw new BusinessException(LibraryCirculationErrorCodes.CommandKeyReused,
				"This operation key was already used for another submission.");
		var result = JsonSerializer.Deserialize<LibraryLoanCommandResultDto>(receipt.ResultJson, LoanJsonOptions)
			?? throw new BusinessException(LibraryErrorCodes.ContextInvalid);
		result.WasReplay = true;
		return result;
	}

	private async Task<LibraryLoanCommandResultDto> SaveResultAsync(Guid actorId, LibraryLoanOperation operation,
		string key, string fingerprint, BookIssue loan, System.Collections.Generic.List<Guid>? assessmentIds = null,
		LibraryPortalOwner? owner = null)
	{
		var result = new LibraryLoanCommandResultDto
		{
			Loan = owner == null ? await ReadLoanAsync(loan.Id) : await ReadLoanAsync(loan, owner),
			AssessmentIds = assessmentIds ?? []
		};
		await commands.InsertAsync(new LibraryLoanCommand(GuidGenerator.Create(), CurrentTenant.Id, actorId, operation,
			key, fingerprint, loan.Id, JsonSerializer.Serialize(result), new DateTimeOffset(Clock.Now.ToUniversalTime())), true);
		return result;
	}

	private async Task RecordLoanEventAsync(BookIssue loan, Guid actorId, LibraryLoanEventKind kind, string reason, string before,
		object? calculationEvidence = null)
	{
		var sequence = await AsyncExecuter.FirstOrDefaultAsync((await events.GetQueryableAsync())
			.Where(row => row.TenantId == CurrentTenant.Id && row.LoanId == loan.Id)
			.OrderByDescending(row => row.Sequence).Select(row => row.Sequence));
		var after = calculationEvidence == null ? JsonSerializer.Serialize(loan) : JsonSerializer.Serialize(new { Loan = loan, Evidence = calculationEvidence });
		await events.InsertAsync(new LibraryLoanEvent(GuidGenerator.Create(), CurrentTenant.Id, loan.Id, checked(sequence + 1),
			kind, actorId, reason, before, after, new DateTimeOffset(Clock.Now.ToUniversalTime()),
			LibraryBorrowerScopeCaptureEvidence.ReadCaptureId(before), loan.BorrowerScopeCaptureId), true);
	}

	private static string PayloadHash(object value) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
}
