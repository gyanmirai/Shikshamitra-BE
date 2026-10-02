using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

public class StudentRecordReportGenerator(IStudentDirectoryAppService directory, IStudentRecordChangeAppService changes,
	IStudentRecordTransitionAppService transitions, IRepository<StudentRecordDocumentEvent, Guid> documents,
	IStudentRepository students, StudentRecordsEnrollmentScopeService scope, StudentRecordsAccessService access,
	StudentRecordsPolicyResolver policies, StudentRecordReportAuthority authority) : ApplicationService, ITransientDependency
{
	public async Task<StudentRecordReportPreviewDto> GeneratePageAsync(StudentRecordReportInput input)
	{
		await authority.EnsureAsync(input.ReportType);
		if (input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100) throw new BusinessException("Students:Records:InvalidReport");
		var result = new StudentRecordReportPreviewDto { AsOf = new DateTimeOffset(Clock.Now.ToUniversalTime()) };
		switch (input.ReportType)
		{
			case "Directory":
				var list = await directory.GetListAsync(new() { SkipCount = input.SkipCount, MaxResultCount = input.MaxResultCount,
					AcademicYearId = input.AcademicYearId, StudentId = input.StudentId, Population = input.Status ?? "All" });
				result.Columns = ["Student", "GR number", "Class", "Division", "Roll number", "Status", "Entry source"];
				result.TotalCount = list.TotalCount;
				result.Rows = list.Items.Select(x => Row(result.Columns, x.MaskedFields.Contains("Identity") ? "Restricted student" : $"{x.FirstName} {x.LastName}".Trim(),
					x.GrNumber ?? x.GrNo?.ToString(), x.StandardName, x.DivisionName, x.RollNo?.ToString(), x.Status.ToString(), x.EntrySource)).ToList();
				break;
			case "Changes":
				var requests = await changes.GetListAsync(new() { SkipCount = input.SkipCount, MaxResultCount = input.MaxResultCount,
					AcademicYearId = input.AcademicYearId, StudentId = input.StudentId, Status = input.Status });
				result.Columns = ["Student", "Change", "Status", "Requested", "Effective", "Approvals", "Reason"];
				result.TotalCount = requests.TotalCount;
				result.Rows = requests.Items.Select(x => Row(result.Columns, x.StudentName, x.Action, x.Status,
					x.CreationTime.ToString("O"), x.EffectiveAt.ToString("O"), $"{x.ApprovalCount}/{x.RequiredApprovals}", x.Reason)).ToList();
				break;
			case "Transitions":
				var moves = await transitions.GetListAsync(new() { SkipCount = input.SkipCount, MaxResultCount = input.MaxResultCount,
					AcademicYearId = input.AcademicYearId, StudentId = input.StudentId, Status = input.Status });
				result.Columns = ["Student", "Outcome", "Status", "Effective", "Approvals", "Reason"];
				result.TotalCount = moves.TotalCount;
				result.Rows = moves.Items.Select(x => Row(result.Columns, x.StudentName, x.Outcome, x.Status,
					x.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), $"{x.CurrentApprovalStage}/{x.ApprovalStageCount}", x.Reason)).ToList();
				break;
			case "Documents": await AddDocumentsAsync(result, input); break;
			default: throw new BusinessException("Students:Records:InvalidReport");
		}
		return result;
	}
	private async Task AddDocumentsAsync(StudentRecordReportPreviewDto result, StudentRecordReportInput input)
	{
		var studentQuery = await scope.ApplyStudentScopeAsync(await students.GetQueryableWithoutAutoIncludesAsync());
		var query = from d in await documents.GetQueryableAsync() join s in studentQuery on d.StudentId equals s.Id
			where d.TenantId == CurrentTenant.Id && (!input.StudentId.HasValue || d.StudentId == input.StudentId) &&
			(!input.AcademicYearId.HasValue || d.AcademicYearId == input.AcademicYearId) && (input.Status == null || d.Action == input.Status)
			select new { Document = d, Student = s };
		result.TotalCount = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.Document.CreationTime).ThenBy(x => x.Document.Id).Skip(input.SkipCount).Take(input.MaxResultCount));
		var roleNames = await access.GetDatabaseRoleNamesAsync();
		var yearPolicies = new Dictionary<Guid, StudentRecordsPolicy>();
		foreach (var year in rows.SelectMany(x => new[] { x.Student.AcademicYearId, x.Document.AcademicYearId }).Where(x => x.HasValue).Distinct())
			yearPolicies[year!.Value] = (await policies.ResolveAsync(year.Value)).Policy;
		var schoolPolicy = (await policies.ResolveAsync(null)).Policy;
		result.Columns = ["Student", "Document", "Action", "Date", "Reason"];
		foreach (var row in rows)
		{
			var current = row.Student.AcademicYearId.HasValue ? yearPolicies[row.Student.AcademicYearId.Value] : schoolPolicy;
			var issued = row.Document.AcademicYearId.HasValue ? yearPolicies[row.Document.AcademicYearId.Value] : schoolPolicy;
			var hidden = current.Access.Masks.Any(x => current.IsMasked(x.FieldGroup, roleNames)) || issued.Access.Masks.Any(x => issued.IsMasked(x.FieldGroup, roleNames));
			result.Rows.Add(Row(result.Columns, current.IsMasked("Identity", roleNames) || issued.IsMasked("Identity", roleNames)
				? "Restricted student" : $"{row.Student.FirstName} {row.Student.LastName}".Trim(), row.Document.DocumentType,
				row.Document.Action, row.Document.CreationTime.ToString("O"), hidden ? "Restricted" : row.Document.Reason));
		}
	}
	private static Dictionary<string, string?> Row(IReadOnlyList<string> columns, params string?[] values) =>
		columns.Select((name, index) => (name, values[index])).ToDictionary(x => x.name, x => x.Item2);
}
