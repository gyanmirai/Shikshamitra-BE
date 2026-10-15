using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentRecords;

namespace ShikshaMitra.Students.Records;

public partial class StudentRecordChangeAppService
{
	internal async Task<IQueryable<StudentRecordChange>> FilterApprovableAsync(IQueryable<StudentRecordChange> query)
	{
		if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Approve)) return query.Where(_ => false);
		query = query.Where(c => c.Status == "Pending" && c.RequestedBy != CurrentUser.Id);
		var candidates = await AsyncExecuter.ToListAsync(query);
		var ids = candidates.Select(c => c.Id).ToArray(); var studentIds = candidates.Select(c => c.StudentId).Distinct().ToArray();
		var history = await decisions.GetListAsync(d => d.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, d.ChangeId));
		var identities = await AsyncExecuter.ToListAsync((await students.GetQueryableWithoutAutoIncludesAsync()).Where(s => Enumerable.Contains(studentIds, s.Id)));
		var roles = await access.GetDatabaseRoleNamesAsync(); var policyByYear = new Dictionary<Guid, StudentRecordsPolicy>();
		foreach (var year in candidates.Select(c => c.AcademicYearId).Concat(identities.Where(s => s.AcademicYearId.HasValue).Select(s => s.AcademicYearId!.Value)).Distinct())
			policyByYear[year] = (await policies.ResolveAsync(year)).Policy;
		var defaultPolicy = identities.Any(s => !s.AcademicYearId.HasValue) ? (await policies.ResolveAsync(null)).Policy : null;
		var allowed = new List<Guid>();
		foreach (var item in candidates)
		{
			if (!IsStageApprover(item, roles, history.Where(h => h.ChangeId == item.Id).ToList())) continue;
			var student = identities.Single(s => s.Id == item.StudentId);
			var currentPolicy = student.AcademicYearId.HasValue ? policyByYear[student.AcademicYearId.Value] : defaultPolicy!;
			if (StudentRecordVisibility.IsMasked(policyByYear[item.AcademicYearId], item.Section, roles) ||
				StudentRecordVisibility.IsMasked(currentPolicy, item.Section, roles)) continue;
			if (item.Section == "custom-details" && await payloads.IsCustomMaskedAsync(item.StudentId, item.BeforeJson)) continue;
			if (item.Section == "registration-form" && await LazyServiceProvider.LazyGetRequiredService<StudentRecordFormProjection>().IsAuditMaskedAsync(item.StudentId, item.BeforeJson)) continue;
			allowed.Add(item.Id);
		}
		var accepted = allowed.ToArray(); return query.Where(c => Enumerable.Contains(accepted, c.Id));
	}
}
