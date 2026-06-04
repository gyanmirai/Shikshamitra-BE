using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveSubmitterResolver(
	IEmployeeRepository employeeRepository,
	IRepository<IdentityUser, Guid> identityUserRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	internal async Task<Dictionary<Guid, (string Name, string? EmployeeCode)>> ResolveForLeavesAsync(
		IReadOnlyCollection<EmployeeLeave> leaves)
	{
		var creatorIds = leaves
			.Where(x => x.CreatorId.HasValue)
			.Select(x => x.CreatorId!.Value)
			.Distinct()
			.ToList();

		var byCreatorId = await ResolveByCreatorIdsAsync(creatorIds);

		var result = new Dictionary<Guid, (string Name, string? EmployeeCode)>();
		foreach (var leave in leaves)
		{
			if (leave.CreatorId.HasValue && byCreatorId.TryGetValue(leave.CreatorId.Value, out var submitter))
			{
				result[leave.Id] = submitter;
			}
			else
			{
				result[leave.Id] = ("System", null);
			}
		}

		return result;
	}

	internal async Task<Dictionary<Guid, (string Name, string? EmployeeCode)>> ResolveByCreatorIdsAsync(
		IReadOnlyCollection<Guid> creatorIds)
	{
		if (creatorIds.Count == 0)
		{
			return new Dictionary<Guid, (string Name, string? EmployeeCode)>();
		}

		var employees = await asyncExecuter.ToListAsync(
			(await employeeRepository.GetQueryableAsync())
				.Where(x => x.UserId.HasValue && creatorIds.Contains(x.UserId.Value))
				.Select(x => new
				{
					UserId = x.UserId!.Value,
					Name = x.EmployeeNameWithFather,
					x.EmployeeCode
				}));
		var employeeByUserId = employees.ToDictionary(
			x => x.UserId,
			x => (Name: x.Name, EmployeeCode: (string?)x.EmployeeCode));

		var users = await identityUserRepository.GetListAsync(x => creatorIds.Contains(x.Id));
		var userDisplayById = users.ToDictionary(
			x => x.Id,
			x =>
				!string.IsNullOrWhiteSpace(x.Name)
					? x.Name!
					: !string.IsNullOrWhiteSpace(x.UserName)
						? x.UserName!
						: x.Email ?? "Unknown");

		var result = new Dictionary<Guid, (string Name, string? EmployeeCode)>();
		foreach (var creatorId in creatorIds)
		{
			if (employeeByUserId.TryGetValue(creatorId, out var employeeInfo))
			{
				result[creatorId] = employeeInfo;
				continue;
			}

			result[creatorId] = userDisplayById.TryGetValue(creatorId, out var userName)
				? (userName, null)
				: ("Unknown", null);
		}

		return result;
	}
}
