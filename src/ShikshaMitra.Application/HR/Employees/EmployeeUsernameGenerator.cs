using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace ShikshaMitra.HR.Employees;

public class EmployeeUsernameGenerator(
	IIdentityUserRepository userRepository,
	ILookupNormalizer lookupNormalizer)
	: ITransientDependency
{
	public async Task<string> GenerateUniqueAsync(string? firstName, string? lastName)
	{
		var firstNamePart = NormalizeUsernamePart(firstName, "x", maxLength: 16);
		var lastNamePart = NormalizeUsernamePart(lastName, "user", maxLength: 24);
		var baseUsername = $"{firstNamePart}.{lastNamePart}";

		var normalizedUsername = lookupNormalizer.NormalizeName(baseUsername);
		var existingUser = await userRepository.FindByNormalizedUserNameAsync(normalizedUsername);

		if (existingUser == null)
		{
			return baseUsername;
		}

		for (var sequence = 1; sequence <= 9999; sequence++)
		{
			var sequencedUsername = $"{baseUsername}{sequence:D4}";
			normalizedUsername = lookupNormalizer.NormalizeName(sequencedUsername);
			existingUser = await userRepository.FindByNormalizedUserNameAsync(normalizedUsername);

			if (existingUser == null)
			{
				return sequencedUsername;
			}
		}

		throw new BusinessException(HRErrorCodes.EmployeeUsernameGenerationFailed);
	}

	private static string NormalizeUsernamePart(string? value, string fallback, int maxLength)
	{
		var normalized = string.IsNullOrWhiteSpace(value)
			? string.Empty
			: new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

		if (string.IsNullOrWhiteSpace(normalized))
		{
			normalized = fallback;
		}

		return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
	}
}
