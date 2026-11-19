using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;
using ClassTeacherRecord = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record ClassTeacherNoticePlacement(Division Division, NotificationSourceScopeDto Scope, string ClassName);

[RemoteService(false), ExposeServices(typeof(ClassTeacherNotificationSource), typeof(INotificationSourceAdapter))]
public sealed class ClassTeacherNotificationSource(IRepository<Division, Guid> divisions, IRepository<Standard, Guid> standards,
	IRepository<ClassTeacherRecord, Guid> assignments, IRepository<Employee, Guid> employees, IRepository<IdentityUser, Guid> users,
	ClassTeacherNotificationAccess access, IAsyncQueryableExecuter queries, ICurrentTenant tenant, ITenantStore tenants)
	: INotificationSourceAdapter, ITransientDependency
{
	internal const string Type = "Academic.ClassTeacher";
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[]
	{
		AcademicNotificationEventKeys.ClassTeacherAssigned, AcademicNotificationEventKeys.ClassTeacherReplaced,
		AcademicNotificationEventKeys.ClassTeacherRemoved
	});

	internal async Task<ClassTeacherNoticePlacement?> FindPlacementAsync(Guid school, Guid divisionId, CancellationToken token)
	{
		var context = from division in await divisions.GetQueryableAsync()
			join standard in await standards.GetQueryableAsync() on division.StandardId equals standard.Id
			let stage = standard.EducationMediumStage
			let medium = stage.EducationMedium
			let board = medium.EducationBoard
			where division.Id == divisionId && division.TenantId == school && !division.IsDeleted &&
				standard.TenantId == school && !standard.IsDeleted && stage.TenantId == school && medium.TenantId == school &&
				board.TenantId == school && board.AcademicYear.TenantId == school
			select new ClassTeacherNoticePlacement(division, new(board.AcademicYearId, board.Id, medium.Id, standard.Id, division.Id),
				standard.Name + " - " + division.Name);
		return await queries.SingleOrDefaultAsync(context, token);
	}

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		var school = context.TenantId;
		if (school == Guid.Empty || school != tenant.Id || context.SourceId == Guid.Empty ||
			context.SourceType != Type || !EventKeys.Contains(context.EventKey)) return Unavailable();

		var placement = await FindPlacementAsync(school, context.SourceId, token);
		if (placement == null || (context.SourceScope != null && context.SourceScope != placement.Scope)) return Unavailable();
		var state = ClassTeacherNotificationPublication.Read(placement.Division);
		if (state?.EventKey != context.EventKey || string.IsNullOrWhiteSpace(state.Revision) ||
			(!string.IsNullOrEmpty(context.SourceRevision) && state.Revision != context.SourceRevision)) return Unavailable();

		var active = await queries.ToListAsync((await assignments.GetQueryableAsync()).Where(x => x.TenantId == school &&
			!x.IsDeleted && x.StandardId == placement.Division.StandardId && x.DivisionId == placement.Division.Id &&
			x.AcademicMediumId == placement.Scope.MediumId && x.Status == ClassTeacherAssignmentStatus.Active)
			.Select(x => x.TeacherId).Distinct().Take(2), token);
		if (state.ConfirmedEmployeeId is Guid confirmed ? active.Count != 1 || active[0] != confirmed : active.Count != 0)
			return Unavailable() with { SourceRevision = state.Revision };
		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable() with { SourceRevision = state.Revision };

		var recipients = new List<NotificationRecipientRefDto>();
		foreach (var candidate in new[] { state.AssignedTeacher, state.PreviousTeacher }.OfType<NotificationRecipientRefDto>())
		{
			var eligibleStaff = from employee in await employees.GetQueryableAsync()
				join user in await users.GetQueryableAsync() on employee.UserId equals (Guid?)user.Id
				where employee.Id == candidate.EmployeeId && employee.TenantId == school && !employee.IsDeleted &&
					employee.EmployeeStatusType == EmployeeStatusType.Active && employee.EmployeeGroup == EmployeeGroupType.Teaching &&
					user.Id == candidate.UserId && user.TenantId == school && !user.IsDeleted && user.IsActive
				select employee.Id;
			if (await queries.AnyAsync(eligibleStaff, token)) recipients.Add(candidate);
		}
		if (await access.FindReviewerAsync(school, placement.Scope, state.ActorId, token) is { } reviewer &&
			recipients.All(x => x.UserId != reviewer.UserId)) recipients.Add(reviewer);

		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, schoolName),
			["class_name"] = new(NotificationVariableType.Text, placement.ClassName)
		};
		var former = recipients.Where(x => x.Kind == "PreviousTeacher").Select(x => new NotificationSourceRecipientContentDto(
			x, new Dictionary<string, NotificationVariableValueDto> { ["school_name"] = variables["school_name"] }, Link)).ToArray();
		return new(true, null, state.Revision, Link, variables, recipients, AudienceSlices: [new(placement.Scope, recipients)], RecipientContent: former);
	}
}
