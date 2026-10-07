using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Employees;

/// <summary>Prevent generic Identity/profile updates from disconnecting an employee's managed address.</summary>
public class EmployeeAccountEmailUserValidator(IRepository<IdentityUser, Guid> users, IEmployeeRepository employees,
	IAsyncQueryableExecuter queries) : IUserValidator<IdentityUser>
{
	public async Task<IdentityResult> ValidateAsync(UserManager<IdentityUser> manager, IdentityUser user)
	{
		// Project the persisted value, not a tracked user whose email has already been assigned by UserManager.
		var savedEmail = await queries.FirstOrDefaultAsync((await users.GetQueryableAsync()).Where(x => x.Id == user.Id).Select(x => x.Email));
		if (savedEmail == null || string.Equals(savedEmail, user.Email, StringComparison.OrdinalIgnoreCase)) return IdentityResult.Success;
		var address = (user.Email ?? string.Empty).ToUpperInvariant();
		var mismatch = await queries.AnyAsync((await employees.GetQueryableAsync()).Where(x => x.UserId == user.Id &&
			(x.Email == null || x.Email.ToUpper() != address)));
		return mismatch ? IdentityResult.Failed(new IdentityError
		{
			Code = HRErrorCodes.EmployeeEmailCannotBeChanged,
			Description = "Use the controlled employee account email change."
		}) : IdentityResult.Success;
	}
}