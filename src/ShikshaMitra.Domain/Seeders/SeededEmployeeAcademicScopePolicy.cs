using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;

namespace ShikshaMitra.Seeders;

internal static class SeededEmployeeAcademicScopePolicy
{
	internal sealed record Stage(Guid Id, string Name, EducationBoardType BoardType);

	internal static EducationBoardType? GetRequiredBoard(string email) =>
		email.Trim().ToUpperInvariant() switch
		{
			"PRINCIPAL-GSEB@DDVS.COM" => EducationBoardType.GSEB,
			"PRINCIPAL-DEMO-GSEB@DDVS.COM" => EducationBoardType.GSEB,
			"TEACHER-GSEB@DDVS.COM" => EducationBoardType.GSEB,
			"PRINCIPAL-CBSE@DDVS.COM" => EducationBoardType.CBSE,
			_ => null
		};

	internal static IReadOnlyCollection<Guid> ResolveStageIds(
		string roleName,
		string email,
		IReadOnlyList<Stage> stages)
	{
		var requiredBoard = GetRequiredBoard(email);
		var eligibleStages = requiredBoard.HasValue
			? stages.Where(x => x.BoardType == requiredBoard.Value).ToList()
			: stages.ToList();
		if (eligibleStages.Count == 0)
		{
			return [];
		}

		var broadAccessRoles = new[] { "Director", "Principal", "Admin", "Administrative", "Manager", "Clerk", "Finance", "HR" };
		if (broadAccessRoles.Any(token => roleName.Contains(token, StringComparison.OrdinalIgnoreCase)))
		{
			return eligibleStages.Select(x => x.Id).ToList();
		}

		var primary = eligibleStages.FirstOrDefault(x => x.Name.Contains("Primary", StringComparison.OrdinalIgnoreCase));
		var secondary = eligibleStages.FirstOrDefault(x => x.Name.Contains("Secondary", StringComparison.OrdinalIgnoreCase));
		if (roleName.Contains("Teacher", StringComparison.OrdinalIgnoreCase))
		{
			var preferred = StableSeedHash(email) % 2 == 0
				? primary ?? eligibleStages[0]
				: secondary ?? eligibleStages[^1];
			return [preferred.Id];
		}

		return [eligibleStages[0].Id];
	}

	private static uint StableSeedHash(string value)
	{
		unchecked
		{
			var hash = 2166136261u;
			foreach (var character in value.ToUpperInvariant())
			{
				hash ^= character;
				hash *= 16777619u;
			}

			return hash;
		}
	}
}
