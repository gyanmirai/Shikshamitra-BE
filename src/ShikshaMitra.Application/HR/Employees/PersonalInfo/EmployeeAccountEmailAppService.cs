using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using OpenIddict.Abstractions;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.OpenIddict.Tokens;
using Volo.Abp.OpenIddict.Authorizations;
using Volo.Abp.Uow;
using Volo.Abp.Validation;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

[RemoteService(false)]
public class EmployeeAccountEmailAppService(IEmployeeRepository employees, IdentityUserManager users,
	IEmployeeAuthorizationService access, IPayrollInputRevisionStore fence, HrAuditWriter audit,
	IRepository<HrAuditEvent, Guid> auditEvents, IOpenIddictTokenManager tokens,
	IOpenIddictAuthorizationManager authorizations, IOpenIddictTokenCache<OpenIddictTokenModel> tokenCache,
	IOpenIddictAuthorizationCache<OpenIddictAuthorizationModel> authorizationCache) : ApplicationService, IEmployeeAccountEmailAppService
{
	private const string AuditAction = "account_email_changed";

	public async Task<EmployeeAccountEmailDto> GetAsync(Guid employeeId)
	{
		await EnsurePermissionsAsync();
		await access.CheckEmployeeModifyAccessAsync(employeeId);
		var employee = await employees.GetAsync(employeeId);
		return Map(employee, await GetLinkedUserAsync(employee));
	}

	[UnitOfWork(true)]
	public virtual async Task<EmployeeAccountEmailDto> ChangeAsync(Guid employeeId, ChangeEmployeeAccountEmailDto input)
	{
		await EnsurePermissionsAsync();
		Validate(input);
		// Delivery checks and recipient selection use this same tenant transaction fence.
		await fence.LockAsync();
		await access.CheckEmployeeModifyAccessAsync(employeeId);
		var employee = await employees.GetAsync(employeeId);
		var user = await GetLinkedUserAsync(employee);
		var email = input.Email.Trim();
		var normalizedEmail = email.ToUpperInvariant();
		var reason = input.Reason.Trim();
		var fingerprint = Hash(JsonSerializer.Serialize(new
		{
			Email = email.ToUpperInvariant(),
			Reason = reason,
			input.ConfirmEmployeeVerified,
			input.ConcurrencyToken
		}));
		var requestId = input.IdempotencyKey.ToString("D");
		var previous = await auditEvents.FindAsync(x => x.EntityId == employeeId && x.Action == AuditAction && x.CorrelationId == requestId);
		if (previous != null)
		{
			using var context = JsonDocument.Parse(previous.ContextJson!);
			if (!string.Equals(context.RootElement.GetProperty("requestFingerprint").GetString(), fingerprint, StringComparison.Ordinal))
				throw new BusinessException(HRErrorCodes.EmployeeAccountEmailConflict);
			// A replay acknowledges the original write without undoing any later account change.
			return Map(employee, user);
		}
		if (!string.Equals(Map(employee, user).ConcurrencyToken, input.ConcurrencyToken, StringComparison.Ordinal))
			throw new BusinessException(HRErrorCodes.EmployeeAccountEmailConflict);
		var duplicate = await users.FindByEmailAsync(email);
		var usesEmailAsUserName = string.Equals(user.UserName, user.Email, StringComparison.OrdinalIgnoreCase);
		var duplicateName = usesEmailAsUserName ? await users.FindByNameAsync(email) : null;
		var employeeQuery = await employees.GetQueryableAsync();
		if (duplicate != null && duplicate.Id != user.Id || duplicateName != null && duplicateName.Id != user.Id ||
			await AsyncExecuter.AnyAsync(employeeQuery.Where(x => x.Id != employeeId && x.Email != null && x.Email.ToUpper() == normalizedEmail)))
			throw new BusinessException(HRErrorCodes.EmployeeAccountEmailInUse).WithData("field", nameof(input.Email));
		var before = new { employee.Email, AccountEmail = user.Email, user.UserName };
		// Identity's linked-employee invariant reads this transaction's persisted address.
		// Any subsequent Identity/audit failure rolls this write back with the account change.
		employee.ChangeAccountEmail(email);
		await employees.UpdateAsync(employee, autoSave: true);
		if (!(await users.SetEmailAsync(user, email)).Succeeded ||
			usesEmailAsUserName && !(await users.SetUserNameAsync(user, email)).Succeeded)
			throw new BusinessException(HRErrorCodes.EmployeeAccountEmailRejected).WithData("field", nameof(input.Email));
		// This privileged workflow requires HR's explicit verification, like the existing managed account creation path.
		user.SetEmailConfirmed(true);
		if (!(await users.UpdateSecurityStampAsync(user)).Succeeded)
			throw new BusinessException(HRErrorCodes.EmployeeAccountEmailRejected);
		await RevokeCredentialsAsync(user.Id);
		await audit.WriteAsync(null, "employee", employeeId, employee.EmployeeName, AuditAction,
			"Changed the linked account email and payslip recipient", reason,
			changedFields: HrAuditWriter.Diff(before, new { employee.Email, AccountEmail = user.Email, user.UserName }),
			context: new { RequestFingerprint = fingerprint, user.Id, input.ConfirmEmployeeVerified }, correlationId: requestId);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return Map(employee, user);
	}

	private async Task EnsurePermissionsAsync()
	{
		if (!CurrentUser.IsAuthenticated || !CurrentUser.Id.HasValue || !CurrentTenant.Id.HasValue) throw new AbpAuthorizationException();
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit);
		await AuthorizationService.CheckAsync(IdentityPermissions.Users.Update);
	}

	private async Task<IdentityUser> GetLinkedUserAsync(Employee employee)
	{
		if (employee.TenantId != CurrentTenant.Id || !employee.UserId.HasValue)
			throw new BusinessException(HRErrorCodes.EmployeeAccountLinkInvalid);
		var user = await users.FindByIdAsync(employee.UserId.Value.ToString());
		var query = await employees.GetQueryableAsync();
		if (user == null || user.TenantId != CurrentTenant.Id ||
			await AsyncExecuter.AnyAsync(query.Where(x => x.UserId == user.Id && x.Id != employee.Id)))
			throw new BusinessException(HRErrorCodes.EmployeeAccountLinkInvalid);
		return user;
	}

	private async Task RevokeCredentialsAsync(Guid userId)
	{
		await foreach (var token in tokens.FindBySubjectAsync(userId.ToString()))
		{
			if (await tokens.GetStatusAsync(token) != OpenIddictConstants.Statuses.Valid) continue;
			var model = (OpenIddictTokenModel) token;
			// The manager caches mutable models before the owning transaction commits.
			// Evict now so rollback cannot retain revoked state, then after commit to discard concurrent old reads.
			CurrentUnitOfWork!.OnCompleted(async () => await tokenCache.RemoveAsync(model, CancellationToken.None));
			try
			{
				if (!await tokens.TryRevokeAsync(token)) throw new BusinessException(HRErrorCodes.EmployeeAccountEmailRejected);
			}
			finally { await tokenCache.RemoveAsync(model, CancellationToken.None); }
		}
		await foreach (var authorization in authorizations.FindBySubjectAsync(userId.ToString()))
		{
			if (await authorizations.GetStatusAsync(authorization) != OpenIddictConstants.Statuses.Valid) continue;
			var model = (OpenIddictAuthorizationModel) authorization;
			CurrentUnitOfWork!.OnCompleted(async () => await authorizationCache.RemoveAsync(model, CancellationToken.None));
			try
			{
				if (!await authorizations.TryRevokeAsync(authorization)) throw new BusinessException(HRErrorCodes.EmployeeAccountEmailRejected);
			}
			finally { await authorizationCache.RemoveAsync(model, CancellationToken.None); }
		}
	}

	private static void Validate(ChangeEmployeeAccountEmailDto input)
	{
		var errors = new List<ValidationResult>();
		Validator.TryValidateObject(input, new ValidationContext(input), errors, validateAllProperties: true);
		if (!MailAddress.TryCreate(input.Email?.Trim(), out var address) || address.Address != input.Email?.Trim() ||
			input.Email!.Contains('\r') || input.Email.Contains('\n')) errors.Add(new("Enter a valid email address.", [nameof(input.Email)]));
		if (!input.ConfirmEmployeeVerified) errors.Add(new("HR must verify the address with the employee.", [nameof(input.ConfirmEmployeeVerified)]));
		if (input.Reason?.Trim().Length < 5) errors.Add(new("Enter a reason with at least five characters.", [nameof(input.Reason)]));
		if (input.IdempotencyKey == Guid.Empty) errors.Add(new("A request identifier is required.", [nameof(input.IdempotencyKey)]));
		if (errors.Count != 0) throw new AbpValidationException("Review the account email change.", errors);
	}

	private static EmployeeAccountEmailDto Map(Employee employee, IdentityUser user) => new()
	{
		UserId = user.Id,
		EmployeeEmail = employee.Email,
		AccountEmail = user.Email,
		UserName = user.UserName,
		ConcurrencyToken = Hash(JsonSerializer.Serialize(new
		{
			employee.Id,
			employee.UserId,
			employee.Email,
			employee.ConcurrencyStamp,
			AccountEmail = user.Email,
			user.UserName,
			AccountStamp = user.ConcurrencyStamp,
			user.SecurityStamp,
			user.EmailConfirmed
		}))
	};
	private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}