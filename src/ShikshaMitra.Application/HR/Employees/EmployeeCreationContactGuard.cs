using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Employees;

public class EmployeeCreationContactGuard(
	ILogger<EmployeeCreationContactGuard> logger) : ITransientDependency
{
	public EmployeeCreationContactDetails RequireContactDetails(string? email, string? mobileNo)
	{
		if (string.IsNullOrWhiteSpace(email))
		{
			logger.LogWarning("Employee creation failed: email is required.");
			throw new BusinessException(HRErrorCodes.EmployeeEmailRequired);
		}

		if (string.IsNullOrWhiteSpace(mobileNo))
		{
			logger.LogWarning("Employee creation failed: mobile number is required for email {Email}.", email);
			throw new BusinessException(HRErrorCodes.EmployeeMobileRequired);
		}

		if (mobileNo.Length < 4)
		{
			logger.LogWarning(
				"Staff creation failed: mobile number length {Length} is below the accepted minimum for email {Email}.",
				mobileNo.Length,
				email);
			throw new BusinessException(HRErrorCodes.EmployeeMobileTooShort);
		}

		return new EmployeeCreationContactDetails(email, mobileNo);
	}
}

public sealed record EmployeeCreationContactDetails(string Email, string MobileNo);
