using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Mobile;

/// <summary>Scalar reads only. Callers must first authorize placement through ResolveAsync.</summary>
public class MobileHomeworkCountReader(MobileAcademicContextAppService context,
	IRepository<HomeworkAssignmentEntity, Guid> homework, IRepository<AssignmentEntity, Guid> assignments,
	IRepository<StudentSubmissionEntity, Guid> submissions, ICurrentTenant tenant,
	IAsyncQueryableExecuter executer) : ITransientDependency
{
	public async Task<MobileHomeworkCountsDto> ReadAsync(MobilePlacement p, DateTime now)
	{
		var selected = (await context.GetSubjectsAsync(p)).SelectMany(x => new[] { x.Id, x.SubjectId }).Distinct().ToArray();
		var blocked = (await submissions.GetQueryableAsync())
			.Where(x => x.TenantId == tenant.Id && x.StudentId == p.Student.Id)
			.Where(MobileSubmissionRules.BlocksFirstSubmission);
		var supported = new[] { SubmissionType.FileUpload, SubmissionType.OnlineText,
			SubmissionType.InPersonSubmission, SubmissionType.NoSubmissionRequired };
		var hq = MobileHomeworkVisibility.Homework(await homework.GetQueryableAsync(), p, tenant.Id, selected, now)
			.Where(x => x.Status != HomeworkStatus.Closed && Enumerable.Contains(supported, x.SubmissionType) &&
				!blocked.Any(s => s.HomeworkAssignmentId == x.Id));
		var aq = MobileHomeworkVisibility.Assignments(await assignments.GetQueryableAsync(), p, tenant.Id, selected, now)
			.Where(x => x.Status != AssignmentStatus.Closed && Enumerable.Contains(supported, x.SubmissionType) &&
				!blocked.Any(s => s.AssignmentId == x.Id));
		// One UNION ALL aggregate gives both task kinds one database snapshot and
		// one round trip, without loading either list or merging distinct task IDs.
		var total = await executer.CountAsync(hq.Select(x => x.Id).Concat(aq.Select(x => x.Id)));
		return new(p.Student.Id, p.Year.Id, total, new DateTimeOffset(now.ToUniversalTime()));
	}
}
