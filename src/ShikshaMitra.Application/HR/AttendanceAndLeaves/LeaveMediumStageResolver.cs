using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveMediumStageResolver(
	IRepository<UserScopeAssignment, Guid> userScopeAssignmentRepository,
	IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
	IRepository<IdentityUser, Guid> identityUserRepository,
	IRepository<IdentityRole, Guid> identityRoleRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	internal async Task<Dictionary<Guid, (Guid MediumStageId, string MediumStageName)>> ResolveAsync(
		IReadOnlyCollection<EmployeeLeave> leaves,
		Guid academicYearId)
	{
		var userIds = leaves
			.Select(x => x.Employee?.UserId)
			.Where(x => x.HasValue)
			.Select(x => x!.Value)
			.Distinct()
			.ToList();

		if (userIds.Count == 0)
		{
			return new Dictionary<Guid, (Guid MediumStageId, string MediumStageName)>();
		}

		var scopeAssignments = await asyncExecuter.ToListAsync(
			(await userScopeAssignmentRepository.GetQueryableAsync())
				.Where(x =>
					x.IsActive &&
					!x.IsDeleted &&
					x.UserId.HasValue &&
					userIds.Contains(x.UserId.Value) &&
					x.EducationMediumStageId.HasValue &&
					(x.AcademicYearId == academicYearId || !x.AcademicYearId.HasValue))
				.Select(x => new
				{
					UserId = x.UserId!.Value,
					MediumStageId = x.EducationMediumStageId!.Value,
					x.AcademicYearId,
					x.CreationTime
				}));

		var selectedByUser = scopeAssignments
			.GroupBy(x => x.UserId)
			.Select(g => g
				.OrderByDescending(x => x.AcademicYearId == academicYearId)
				.ThenByDescending(x => x.CreationTime)
				.First())
			.ToList();

		var selectedByUserMap = selectedByUser.ToDictionary(x => x.UserId, x => x.MediumStageId);
		var missingUserIds = userIds.Where(x => !selectedByUserMap.ContainsKey(x)).ToList();
		if (missingUserIds.Count > 0)
		{
			var users = await identityUserRepository.GetListAsync(
				x => missingUserIds.Contains(x.Id),
				includeDetails: true);

			var roleIds = users
				.SelectMany(x => x.Roles)
				.Select(x => x.RoleId)
				.Distinct()
				.ToList();

			if (roleIds.Count > 0)
			{
				var roles = await identityRoleRepository.GetListAsync(x => roleIds.Contains(x.Id));
				var roleNamesById = roles
					.Where(x => roleIds.Contains(x.Id))
					.ToDictionary(x => x.Id, x => x.Name);
				var relevantRoleNames = roleNamesById.Values
					.Where(x => !string.IsNullOrWhiteSpace(x))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();

				if (relevantRoleNames.Count > 0)
				{
					var roleAssignments = await asyncExecuter.ToListAsync(
						(await userScopeAssignmentRepository.GetQueryableAsync())
							.Where(x =>
								x.IsActive &&
								!x.IsDeleted &&
								x.RoleName != null &&
								relevantRoleNames.Contains(x.RoleName) &&
								x.EducationMediumStageId.HasValue &&
								(x.AcademicYearId == academicYearId || !x.AcademicYearId.HasValue))
							.Select(x => new
							{
								x.RoleName,
								MediumStageId = x.EducationMediumStageId!.Value,
								x.AcademicYearId,
								x.CreationTime
							}));

					var roleAssignmentByRoleName = roleAssignments
						.GroupBy(x => x.RoleName!, StringComparer.OrdinalIgnoreCase)
						.ToDictionary(
							g => g.Key,
							g => g
								.OrderByDescending(x => x.AcademicYearId == academicYearId)
								.ThenByDescending(x => x.CreationTime)
								.First(),
							StringComparer.OrdinalIgnoreCase);

					foreach (var user in users)
					{
						var mappedRoleNames = user.Roles
							.Select(x => roleNamesById.TryGetValue(x.RoleId, out var roleName) ? roleName : null)
							.Where(x => !string.IsNullOrWhiteSpace(x))
							.Cast<string>()
							.ToList();

						var resolvedStageIds = mappedRoleNames
							.Where(roleAssignmentByRoleName.ContainsKey)
							.Select(x => roleAssignmentByRoleName[x].MediumStageId)
							.Distinct()
							.ToList();

						// If multiple stage IDs resolve for the same user, keep it unset
						// to avoid arbitrary, order-dependent stage attribution.
						if (resolvedStageIds.Count == 1)
						{
							selectedByUserMap[user.Id] = resolvedStageIds[0];
						}
					}
				}
			}
		}

		if (selectedByUserMap.Count == 0)
		{
			return new Dictionary<Guid, (Guid MediumStageId, string MediumStageName)>();
		}

		var stageIds = selectedByUserMap.Values.Distinct().ToList();
		var stages = await asyncExecuter.ToListAsync(
			(await educationMediumStageRepository.GetQueryableAsync())
				.Where(x => stageIds.Contains(x.Id))
				.Select(x => new { x.Id, x.Name }));
		var stageNames = stages.ToDictionary(x => x.Id, x => x.Name);

		var lookup = new Dictionary<Guid, (Guid MediumStageId, string MediumStageName)>();
		foreach (var item in selectedByUserMap)
		{
			if (stageNames.TryGetValue(item.Value, out var name))
			{
				lookup[item.Key] = (item.Value, name);
			}
		}

		return lookup;
	}
}
