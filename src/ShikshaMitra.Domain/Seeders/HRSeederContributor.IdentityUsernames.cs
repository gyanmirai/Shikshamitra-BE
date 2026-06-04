using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task<string> GenerateUniqueUsernameAsync(string firstName, string lastName)
	{
		var firstInitial = !string.IsNullOrWhiteSpace(firstName)
			? new string(firstName.Where(char.IsLetterOrDigit).ToArray()).ToLower().FirstOrDefault().ToString()
			: "x";

		if (string.IsNullOrWhiteSpace(firstInitial))
		{
			firstInitial = "x";
		}

		var lastNamePart = !string.IsNullOrWhiteSpace(lastName)
			? new string(lastName.Where(char.IsLetterOrDigit).ToArray()).ToLower()
			: "user";

		if (string.IsNullOrWhiteSpace(lastNamePart))
		{
			lastNamePart = "user";
		}

		var baseUsername = $"{firstInitial}{lastNamePart}";
		var normalizedUsername = _lookupNormalizer.NormalizeName(baseUsername);
		var existingUser = await _userRepository.FindByNormalizedUserNameAsync(normalizedUsername);

		if (existingUser == null)
		{
			return baseUsername;
		}

		var sequence = 1;
		while (true)
		{
			var sequencedUsername = $"{baseUsername}{sequence:D2}";
			normalizedUsername = _lookupNormalizer.NormalizeName(sequencedUsername);
			existingUser = await _userRepository.FindByNormalizedUserNameAsync(normalizedUsername);

			if (existingUser == null)
			{
				return sequencedUsername;
			}

			sequence++;
			if (sequence > 99)
			{
				throw new Exception("Unable to generate unique username for seeding.");
			}
		}
	}
}
