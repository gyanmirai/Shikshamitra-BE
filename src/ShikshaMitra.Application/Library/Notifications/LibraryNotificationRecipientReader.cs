using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Notifications;

[RemoteService(false)]
public class LibraryNotificationRecipientReader(ILibraryAccessService access,
	IRepository<LibraryMember, Guid> memberships, LibraryNotificationRecipientRecoveryReader recovery)
	: ApplicationService, ITransientDependency
{
	public async Task<PaginatedResponse<LibraryNotificationRecipientOptionDto>> SearchAsync(LibraryNotificationRecipientSearchRequest input, UserScopeContext scope)
	{
		var term = (input.Search ?? string.Empty).Trim().ToLowerInvariant();
		if (term.Length > 100 || input.Page < 1 || input.PageSize is < 1 or > 100)
			throw new BusinessException(LibraryNotificationErrorCodes.ConfigurationInvalid, "Review the search and page size.");
		var students = await access.GetStudentsAsync(scope);
		var employees = await access.GetEmployeesAsync(scope);
		if (term.Length > 0)
		{
			var isGuid = Guid.TryParse(term, out var id);
			var matchingMembers = (await memberships.GetQueryableAsync()).Where(member => member.TenantId == CurrentTenant.Id &&
				(member.MemberIdentifier.ToLower().Contains(term) || isGuid && member.Id == id)).Select(member => member.StudentId);
			students = students.Where(person => isGuid && person.Id == id || matchingMembers.Contains(person.Id) ||
				person.FirstName != null && person.FirstName.ToLower().Contains(term) ||
				person.MiddleName != null && person.MiddleName.ToLower().Contains(term) || person.LastName != null && person.LastName.ToLower().Contains(term));
			employees = employees.Where(person => isGuid && person.Id == id || person.EmployeeCode != null && person.EmployeeCode.ToLower().Contains(term) ||
				person.FirstName != null && person.FirstName.ToLower().Contains(term) ||
				person.MiddleName != null && person.MiddleName.ToLower().Contains(term) || person.LastName != null && person.LastName.ToLower().Contains(term));
		}
		var studentCount = await AsyncExecuter.CountAsync(students);
		var count = studentCount + await AsyncExecuter.CountAsync(employees);
		var skip = (long)(input.Page - 1) * input.PageSize;
		var rows = skip < studentCount ? await AsyncExecuter.ToListAsync(students.OrderBy(person => person.FirstName).ThenBy(person => person.LastName).ThenBy(person => person.Id)
			.Skip((int)skip).Take((int)Math.Min(input.PageSize, studentCount - skip)).Select(person => new RecipientRow(person.Id, person.FirstName, person.MiddleName, person.LastName, null, 0,
				person.Status != StudentLifeCycleStatusEnum.Left && person.Status != StudentLifeCycleStatusEnum.InActive && person.AdditionalDetail != null && person.AdditionalDetail.IsLibraryMember == true))) : new List<RecipientRow>();
		if (rows.Count < input.PageSize && skip < count)
			rows.AddRange(await AsyncExecuter.ToListAsync(employees.OrderBy(person => person.FirstName).ThenBy(person => person.LastName).ThenBy(person => person.Id)
				.Skip((int)Math.Max(0, skip - studentCount)).Take(input.PageSize - rows.Count).Select(person => new RecipientRow(person.Id, person.FirstName, person.MiddleName, person.LastName, person.EmployeeCode, 1,
					person.EmployeeStatusType == EmployeeStatusType.Active && person.AdditionalDetails != null && person.AdditionalDetails.LibraryMember == true))));
		var ids = rows.Select(row => row.Id).ToArray();
		var latest = (await AsyncExecuter.ToListAsync((await memberships.GetQueryableAsync()).Where(member => member.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, member.StudentId))
			.Select(member => new { member.Id, member.StudentId, member.MemberIdentifier, member.IsActive, member.CreationTime })))
			.GroupBy(member => member.StudentId).ToDictionary(group => group.Key, group => group.OrderByDescending(member => member.CreationTime).ThenBy(member => member.Id).First());
		var recoverable = await recovery.ReadAsync(ids);
		return new() { TotalCount = count, CurrentPage = input.Page, PageSize = input.PageSize, TotalPages = (int)Math.Ceiling(count / (double)input.PageSize),
			Items = rows.Select(row => {
				latest.TryGetValue(row.Id, out var membership);
				var active = row.IsMember && (row.Role != 0 || membership?.IsActive != false);
				var obligation = recoverable.Contains(row.Id);
				return new LibraryNotificationRecipientOptionDto { Id = row.Id, Role = row.Role,
					MemberIdentifier = row.Role == 0 ? membership?.MemberIdentifier : row.Identifier,
					MemberName = string.Join(" ", new[] { row.FirstName, row.MiddleName, row.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))),
					IsActiveMembership = active, HasRecoverableObligation = obligation, IsEligibleForNotification = active || obligation,
					EligibilityReason = active ? null : obligation ? "Inactive membership; outstanding obligations may allow record-specific recovery notifications. Delivery is checked separately." : "Membership is inactive and no recoverable obligation was found." };
			}).ToList() };
	}
	private sealed record RecipientRow(Guid Id, string? FirstName, string? MiddleName, string? LastName, string? Identifier, int Role, bool IsMember);
}
