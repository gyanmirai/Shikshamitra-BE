using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.Portal;

/// <summary>Resolves persisted portal and legacy student-self associations within the current tenant.</summary>
[Authorize]
[RemoteService(false)]
public class StudentPortalAccessService(
	IRepository<StudentPortalAccountLink, Guid> links,
	IRepository<IdentityUser, Guid> users,
	IRepository<Student, Guid> students,
	IDataFilter dataFilter) : ApplicationService, ITransientDependency
{
	public async Task<IReadOnlyList<StudentPortalAccountDto>> GetMyStudentsAsync()
	{
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
		var user = await users.FindAsync(x => x.Id == CurrentUser.Id.Value && x.TenantId == CurrentTenant.Id);
		if (user == null || !user.IsActive || !user.EmailConfirmed || string.IsNullOrWhiteSpace(user.Email) || user.ShouldChangePasswordOnNextLogin) throw new AbpAuthorizationException();

		var activeLinks = await links.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.UserId == user.Id &&
			(x.AccountType == "guardian" || (x.AccountType == "student" && x.Relationship == "self")));
		var studentQuery = await students.GetQueryableAsync();
		// Association lists need names, not the Student aggregate's auto-included relationships.
		var selfStudents = await AsyncExecuter.ToListAsync(studentQuery.Where(x => x.TenantId == CurrentTenant.Id && x.UserId == user.Id && x.Status != StudentLifeCycleStatusEnum.Left)
			.Select(x => new PersonalStudent { Id = x.Id, FirstName = x.FirstName, LastName = x.LastName }));
		List<StudentPortalAccountLink> selfLinkHistory;
		// A soft-deleted explicit self link must not regain access through Student.UserId.
		// Tenant filtering remains enabled while inspecting revocations.
		using (dataFilter.Disable<ISoftDelete>())
			selfLinkHistory = await links.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.UserId == user.Id &&
				x.AccountType == "student" && x.Relationship == "self");

		var linkedIds = activeLinks.Select(x => x.StudentId).Distinct().ToArray();
		var linkedStudents = linkedIds.Length == 0 ? [] :
			await AsyncExecuter.ToListAsync(studentQuery.Where(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(linkedIds, x.Id) && x.Status != StudentLifeCycleStatusEnum.Left)
				.Select(x => new PersonalStudent { Id = x.Id, FirstName = x.FirstName, LastName = x.LastName }));
		var result = new List<StudentPortalAccountDto>();
		foreach (var link in activeLinks)
		{
			var student = linkedStudents.FirstOrDefault(x => x.Id == link.StudentId);
			if (student != null) result.Add(Map(student, user, link.AccountType, link.Relationship));
		}
		foreach (var student in selfStudents)
			if (!selfLinkHistory.Any(x => x.StudentId == student.Id))
				result.Add(Map(student, user, "student", "self"));
		return result;
	}

	public async Task<Student> ResolveStudentAsync(Guid? studentId)
	{
		var owned = (await GetMyStudentsAsync()).Where(x => !studentId.HasValue || x.StudentId == studentId.Value).ToList();
		if (owned.Count == 0) throw new AbpAuthorizationException();
		if (owned.Select(x => x.StudentId).Distinct().Count() != 1)
			throw new BusinessException(StudentOnboardingErrorCodes.PortalAccountInvalid).WithData("Reason", "Select one linked student.");
		return await students.FindAsync(x => x.Id == owned[0].StudentId && x.TenantId == CurrentTenant.Id)
			?? throw new AbpAuthorizationException();
	}

	private sealed class PersonalStudent
	{
		public Guid Id { get; init; }
		public string? FirstName { get; init; }
		public string? LastName { get; init; }
	}

	private static StudentPortalAccountDto Map(PersonalStudent student, IdentityUser user, string accountType, string relationship) => new()
	{
		StudentId = student.Id, UserId = user.Id,
		StudentName = string.Join(" ", new[] { student.FirstName, student.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
		AccountType = accountType, Relationship = relationship, Email = user.Email,
		EmailConfirmed = user.EmailConfirmed, IsActive = user.IsActive
	};
}
