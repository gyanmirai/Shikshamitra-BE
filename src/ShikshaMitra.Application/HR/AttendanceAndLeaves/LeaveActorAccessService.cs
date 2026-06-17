using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class LeaveActorAccessService(
	IEmployeeRepository employeeRepository,
	LeaveValidationService leaveValidationService,
	ICurrentUser currentUser)
	: ITransientDependency
{
	private const int MaxHierarchyDepth = 64;
	private static readonly IReadOnlyDictionary<string, string[]> SeededLoginEmployeeEmailAliases =
		new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
		{
			["admin-ddvs"] = ["admin-ddvs@ddvs.com", "admin1@gmail.com", "ddvs@gmail.com"],
			["admin-ops"] = ["admin-ops@ddvs.com", "admin2@ddvs.com"],
			["principal-demo-gseb"] = ["principal-demo-gseb@ddvs.com", "principal@school.com"],
			["principal-gseb"] = ["principal-gseb@ddvs.com", "principal1@ddvs.com", "rajesh.kumar@school.edu", "hiren.vadher@ddvs.com"],
			["principal-cbse"] = ["principal-cbse@ddvs.com", "principle1@ddvs.com"],
			["teacher-demo-1"] = ["teacher-demo-1@ddvs.com", "teacher1@school.com"],
			["teacher-demo-2"] = ["teacher-demo-2@ddvs.com", "teacher2@school.com"],
			["teacher-gseb"] = ["teacher-gseb@ddvs.com", "teacher1@ddvs.com", "vikram.singh@school.edu", "sweta.badgujar@ddvs.com"],
			["hr-manager"] = ["hr-manager@ddvs.com", "hr@school.com"],
			["clerk-office"] = ["clerk-office@ddvs.com", "clerk1@ddvs.com", "clerk@school.edu", "raj.baku@ddvs.com"],
			["finance-officer"] = ["finance-officer@ddvs.com", "finance1@ddvs.com", "suresh.patil@school.edu", "punam.badgujar@ddvs.com"]
		};

	public async Task<LeaveActorContext> ResolveContextAsync()
	{
		var currentUserId = currentUser.Id
			?? throw new BusinessException(HRErrorCodes.LeaveUserNotAuthenticated);

		var actorEmployee = await ResolveCurrentUserEmployeeAsync(currentUserId);
		var isAdminBypass = IsAdminBypassUser();

		if (actorEmployee is null && !isAdminBypass)
		{
			throw new BusinessException(HRErrorCodes.LeaveEmployeeNotFound);
		}

		return new LeaveActorContext(isAdminBypass, actorEmployee?.Id);
	}

	public async Task<Guid> ResolveCurrentActorEmployeeIdStrictAsync()
	{
		var actorContext = await ResolveContextAsync();
		return actorContext.ActorEmployeeId
			?? throw new BusinessException(HRErrorCodes.LeaveEmployeeNotFound);
	}

	public async Task EnsureMayCreateLeaveOnBehalfAsync(Guid targetEmployeeId, LeaveActorContext actorContext)
	{
		if (actorContext.IsAdminBypass || targetEmployeeId == actorContext.ActorEmployeeId)
		{
			return;
		}

		await leaveValidationService.EnsureActorMayChangeLeaveStatusAsync(
			targetEmployeeId,
			actorContext.ActorEmployeeId!.Value);
	}

	public async Task EnsureActorCanAccessEmployeeAsync(Guid targetEmployeeId)
	{
		var actorContext = await ResolveContextAsync();
		if (actorContext.IsAdminBypass)
		{
			return;
		}

		if (!actorContext.ActorEmployeeId.HasValue)
		{
			throw new AbpAuthorizationException("Access denied: You do not have leave access.");
		}

		var scopedEmployeeIds = await ResolveScopedEmployeeIdsAsync(actorContext);
		if (scopedEmployeeIds is { Count: > 0 } && scopedEmployeeIds.Contains(targetEmployeeId))
		{
			return;
		}

		throw new AbpAuthorizationException("Access denied: You can only access your own or reporting employees' leave requests.");
	}

	public async Task<IReadOnlyCollection<Guid>?> ResolveScopedEmployeeIdsAsync(LeaveActorContext actorContext)
	{
		if (actorContext.IsAdminBypass)
		{
			return null;
		}

		if (!actorContext.ActorEmployeeId.HasValue)
		{
			return [];
		}

		var scoped = new HashSet<Guid> { actorContext.ActorEmployeeId.Value };
		var queue = new Queue<Guid>();
		queue.Enqueue(actorContext.ActorEmployeeId.Value);
		var depth = 0;

		while (queue.Count > 0 && depth < MaxHierarchyDepth)
		{
			depth++;
			var currentManagerId = queue.Dequeue();
			var children = await employeeRepository.GetEmployeesByManagerAsync(currentManagerId);

			foreach (var child in children)
			{
				if (scoped.Add(child.Id))
				{
					queue.Enqueue(child.Id);
				}
			}
		}

		return scoped;
	}

	public static void EnsureLeaveBelongsToActor(Guid leaveEmployeeId, Guid actorEmployeeId)
	{
		if (leaveEmployeeId != actorEmployeeId)
		{
			throw new AbpAuthorizationException("Access denied: You can only access your own leave requests.");
		}
	}

	private async Task<Employee?> ResolveCurrentUserEmployeeAsync(Guid currentUserId)
	{
		var actorEmployee = await employeeRepository.GetEmployeeByUserIdAsync(currentUserId);
		if (actorEmployee is not null)
		{
			return actorEmployee;
		}

		var (candidateEmails, canRelinkSeededAlias) = ResolveCurrentUserEmployeeCandidateEmails();
		if (candidateEmails.Count == 0)
		{
			return null;
		}

		var employees = await employeeRepository.GetListAsync();
		var employee = employees.FirstOrDefault(x =>
			!string.IsNullOrWhiteSpace(x.Email) &&
			candidateEmails.Contains(x.Email, StringComparer.OrdinalIgnoreCase));
		if (employee is null)
		{
			return null;
		}

		if (employee.UserId.HasValue && employee.UserId.Value != currentUserId && !canRelinkSeededAlias)
		{
			return null;
		}

		return employee;
	}

	private (List<string> Emails, bool CanRelinkSeededAlias) ResolveCurrentUserEmployeeCandidateEmails()
	{
		var emails = new List<string>();
		if (!string.IsNullOrWhiteSpace(currentUser.Email))
		{
			emails.Add(currentUser.Email);
		}

		var canRelinkSeededAlias = false;
		var userName = currentUser.UserName;
		if (!string.IsNullOrWhiteSpace(userName) &&
		    SeededLoginEmployeeEmailAliases.TryGetValue(userName, out var aliases))
		{
			emails.AddRange(aliases);
			canRelinkSeededAlias = true;
		}

		return (emails
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList(), canRelinkSeededAlias);
	}

	private bool IsAdminBypassUser()
	{
		return currentUser.IsInRole("admin")
			|| currentUser.IsInRole("Administrator");
	}
}

public readonly record struct LeaveActorContext(bool IsAdminBypass, Guid? ActorEmployeeId);
