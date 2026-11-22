using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using ClassTeacherRow = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class AcademicWeeklySummaryStaff(IConfiguration configuration, IRepository<ClassTeacherRow, Guid> assignments,
	AcademicNotificationStaffAccess access, TimetableNotificationAudience staff, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal const string View = ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.View;

	internal async Task<IReadOnlyList<NotificationRecipientRefDto>> ResolveAsync(Guid school, NotificationSourceScopeDto scope, CancellationToken token)
	{
		var teacherIds = await queries.ToListAsync((await assignments.GetQueryableAsync()).Where(x =>
			x.TenantId == school && !x.IsDeleted && x.Status == ClassTeacherAssignmentStatus.Active &&
			x.AcademicMediumId == scope.MediumId && x.StandardId == scope.ClassId && x.DivisionId == scope.DivisionId)
			.Select(x => x.TeacherId).Distinct(), token);
		var candidates = (await staff.FindTeachersAsync(school, teacherIds.ToArray(), token))
			.Select(x => x with { Kind = "ClassTeacher" }).ToList();
		if (Guid.TryParse(configuration[$"Notifications:Academic:Tenants:{school:N}:WeeklySummaryReviewerUserId"], out var reviewer) &&
			reviewer != Guid.Empty)
			candidates.Add(new("ConfiguredReviewer", UserId: reviewer));

		return await access.FilterAsync(school, scope, candidates, View, token);
	}
}
