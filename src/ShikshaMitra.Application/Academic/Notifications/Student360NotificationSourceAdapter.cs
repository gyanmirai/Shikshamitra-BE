using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;
using ClassTeacherRecord = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationSourceAdapter), typeof(IAcademicNotificationSourceAuthorizer))]
public sealed class Student360NotificationSourceAdapter(IStudentRepository students, StudentScopeAccessService studentAccess,
	AcademicNotificationDivisionReader divisions, AcademicNotificationEnrollmentRoster roster,
	IRepository<ClassTeacherRecord, Guid> classTeachers, TimetableNotificationAudience staff, IAsyncQueryableExecuter queries,
	ICurrentTenant tenant, ITenantStore tenants, IPermissionChecker permissions)
	: INotificationSourceAdapter, IAcademicNotificationSourceAuthorizer, ITransientDependency
{
	internal const string Type = "Academic.Student360";
	internal const string GuardianMessage = AcademicNotificationEventKeys.Student360GuardianMessage;
	internal const string MeetingRequested = AcademicNotificationEventKeys.Student360MeetingRequested;
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { GuardianMessage, MeetingRequested });

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		var school = context.TenantId;
		if (school == Guid.Empty || tenant.Id != school || context.SourceType != Type || !EventKeys.Contains(context.EventKey))
			return Unavailable();

		var eligible = (await students.GetQueryableWithoutAutoIncludesAsync()).Where(x => x.TenantId == school &&
			x.Id == context.SourceId && !x.IsDeleted && x.Status == StudentLifeCycleStatusEnum.Enrolled);
		var student = await queries.SingleOrDefaultAsync(eligible, token);
		if (student?.DivisionId is not Guid divisionId || divisionId == Guid.Empty ||
			await divisions.FindAsync(school, divisionId, token) is not { } division)
			return Unavailable();

		var scope = division.Scope;
		if (student.AcademicYearId != scope.AcademicYearId || student.AcademicBoardId != scope.BoardId ||
			student.AcademicMediumId != scope.MediumId || student.StandardId != scope.ClassId ||
			(context.SourceScope != null && context.SourceScope != scope))
			return Unavailable();

		var learners = await roster.FindAsync(school, scope.AcademicYearId!.Value, scope.MediumId!.Value,
			scope.ClassId!.Value, [divisionId], eligible, token);
		if (learners?.Count != 1) return Unavailable();

		var schoolName = tenant.Name ?? (await tenants.FindAsync(school))?.Name;
		if (string.IsNullOrWhiteSpace(schoolName)) return Unavailable();
		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, schoolName),
			["class_name"] = new(NotificationVariableType.Text, division.Name),
			["student_name"] = new(NotificationVariableType.Text,
				AcademicNotificationFamilyRecipients.DisplayName(student.FirstName, student.MiddleName, student.LastName))
		};
		var recipients = new List<NotificationRecipientRefDto> { new("Student", StudentId: student.Id) };
		if (context.EventKey == MeetingRequested)
		{
			var teacherIds = await queries.ToListAsync((await classTeachers.GetQueryableAsync()).Where(x =>
				x.TenantId == school && !x.IsDeleted && x.Status == ClassTeacherAssignmentStatus.Active &&
				x.AcademicMediumId == scope.MediumId && x.StandardId == scope.ClassId && x.DivisionId == divisionId)
				.Select(x => x.TeacherId).Distinct().Take(2), token);
			if (teacherIds.Count == 1)
				recipients.AddRange((await staff.FindTeachersAsync(school, teacherIds.ToArray(), token))
					.Select(x => x with { Kind = "ClassTeacher" }));
		}

		var revision = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
			new { student.ConcurrencyStamp, scope, variables, recipients })));
		if (!string.IsNullOrEmpty(context.SourceRevision) && revision != context.SourceRevision)
			return Unavailable();

		return new(true, null, revision, Link, variables, recipients, AudienceSlices: [new(scope, recipients)]);
	}

	public async Task EnsureCanViewAsync(string eventKey, Guid sourceId, CancellationToken token)
	{
		if (!EventKeys.Contains(eventKey) || tenant.Id is not Guid school ||
			!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Academics.Dashboard.View))
			throw new AbpAuthorizationException();

		var query = (await students.GetQueryableWithoutAutoIncludesAsync()).Where(x =>
			x.TenantId == school && x.Id == sourceId && !x.IsDeleted);
		var student = await queries.SingleOrDefaultAsync(query, token) ?? throw new AbpAuthorizationException();
		await studentAccess.EnsureCanAccessStudentAsync(student, nameof(Student360NotificationSourceAdapter));
	}

	public Task EnsureCanSendAsync(string eventKey, Guid sourceId, CancellationToken token) =>
		EnsureCanViewAsync(eventKey, sourceId, token);
}
