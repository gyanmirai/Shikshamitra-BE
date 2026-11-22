using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using static ShikshaMitra.Academic.Notifications.LearningTaskNotificationContent;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record ResultNotificationRecord(ExamResultHeader Header, Guid YearId, Guid BoardId, Guid MediumId,
	Guid ClassId, string ExamName, ExamStatus ExamStatus)
{
	internal NotificationSourceScopeDto Scope => new(YearId, BoardId, MediumId, ClassId, Header.DivisionId);
}

[RemoteService(false), ExposeServices(typeof(ResultNotificationSource), typeof(INotificationSourceAdapter))]
public sealed class ResultNotificationSource(IRepository<ExamResultHeader, Guid> headers, IRepository<Exam, Guid> exams,
	IRepository<ExamResultDetail, Guid> details, IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions,
	IRepository<Student, Guid> students, AcademicNotificationEnrollmentRoster enrollments, ICurrentTenant tenant,
	ITenantStore tenants, IAsyncQueryableExecuter queries) : INotificationSourceAdapter, ITransientDependency
{
	internal const string Type = "Academic.Result";
	internal const string PublicationRevisionProperty = "AcademicResultPublicationNoticeRevision";
	public IReadOnlyCollection<string> EventKeys { get; } = Array.AsReadOnly(new[] { AcademicNotificationEventKeys.ResultPublished });

	internal async Task<ResultNotificationRecord?> FindAsync(Guid tenantId, Guid headerId, CancellationToken token) =>
		await queries.SingleOrDefaultAsync(
			from header in await headers.GetQueryableAsync()
			join exam in await exams.GetQueryableAsync() on header.ExamId equals exam.Id
			join division in await divisions.GetQueryableAsync() on header.DivisionId equals division.Id
			join standard in await standards.GetQueryableAsync() on exam.StandardId equals standard.Id
			let stage = standard.EducationMediumStage
			let medium = stage.EducationMedium
			let board = medium.EducationBoard
			where header.Id == headerId && header.TenantId == tenantId && !header.IsDeleted && exam.TenantId == tenantId && !exam.IsDeleted &&
				division.TenantId == tenantId && !division.IsDeleted && division.StandardId == standard.Id && standard.TenantId == tenantId &&
				stage.TenantId == tenantId && medium.TenantId == tenantId && board.TenantId == tenantId &&
				board.AcademicYear.TenantId == tenantId && exam.ExamDivisions.Any(x => x.DivisionId == division.Id)
			select new ResultNotificationRecord(header, board.AcademicYearId, board.Id, medium.Id, standard.Id, exam.ExamName, exam.Status), token);

	public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken token)
	{
		if (context.TenantId == Guid.Empty || context.TenantId != tenant.Id || context.SourceType != Type ||
			context.SourceId == Guid.Empty || !EventKeys.Contains(context.EventKey)) return Unavailable();
		var record = await FindAsync(context.TenantId, context.SourceId, token);
		if (record == null || record.ExamStatus is not (ExamStatus.Published or ExamStatus.Completed) ||
			record.Header.Status is not (ExamResultStatus.Published or ExamResultStatus.Frozen) || !record.Header.PublishedOn.HasValue ||
			(context.SourceScope != null && context.SourceScope != record.Scope)) return Unavailable();
		var revision = record.Header.GetProperty<string>(PublicationRevisionProperty);
		if (string.IsNullOrWhiteSpace(revision) || (!string.IsNullOrEmpty(context.SourceRevision) && context.SourceRevision != revision))
			return Unavailable();
		var released = (await details.GetQueryableAsync()).Where(x => x.TenantId == context.TenantId && !x.IsDeleted &&
			x.ResultHeaderId == record.Header.Id && x.ExamId == record.Header.ExamId && x.DivisionId == record.Header.DivisionId &&
			x.ResultStatus != ExamStudentResultStatus.Withheld);
		var eligible = (await students.GetQueryableAsync()).Where(x => x.TenantId == context.TenantId && !x.IsDeleted &&
			x.Status == StudentLifeCycleStatusEnum.Enrolled && released.Any(detail => detail.StudentId == x.Id));
		var learners = await enrollments.FindAsync(context.TenantId, record.YearId, record.MediumId, record.ClassId,
			[record.Header.DivisionId], eligible, token);
		if (learners is not { Count: > 0 }) return Unavailable();
		var school = tenant.Name ?? (await tenants.FindAsync(context.TenantId))?.Name;
		if (string.IsNullOrWhiteSpace(school)) return Unavailable();
		var recipients = learners.Select(x => new NotificationRecipientRefDto("Student", StudentId: x.StudentId)).ToArray();
		var variables = new Dictionary<string, NotificationVariableValueDto>(StringComparer.Ordinal)
		{
			["school_name"] = new(NotificationVariableType.Text, school),
			["exam_name"] = new(NotificationVariableType.Text, record.ExamName)
		};
		return new(true, null, revision, Link, variables, recipients, AudienceSlices: [new(record.Scope, recipients)]);
	}
}
