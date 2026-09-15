using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentRecords;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Reports;

public class StudentRecordsDynamicReportRowReader(
	IAsyncQueryableExecuter asyncExecuter,
	IStudentRepository studentRepository,
	StudentScopeAccessService studentScopeAccessService) : ITransientDependency
{
	public async Task<List<Dictionary<string, string?>>> ReadAsync(
		AcademicDynamicReportSpecDto plan,
		AcademicAiReportCatalogScope scope,
		AcademicDynamicReportCatalogSnapshot snapshot,
		int maximumRows)
	{
		if (!string.Equals(plan.Dataset, AcademicDynamicDatasets.StudentRecordsStudents,
			StringComparison.OrdinalIgnoreCase))
		{
			throw AcademicDynamicReportGuard.DatasetNotAllowed(plan.Dataset);
		}

		var query = (await studentRepository.GetQueryableWithoutAutoIncludesAsync())
			.Where(student =>
				student.AcademicYearId == scope.AcademicYearId &&
				student.AcademicBoardId == scope.EducationBoardId &&
				student.AcademicMediumId == scope.EducationMediumId);
		query = await studentScopeAccessService.ApplyCurrentUserScopeAsync(query);

		var classIds = Resolve(plan.Filters.ClassTokens, snapshot.Classes, item => item.Token, item => item.Id)
			.Concat(Resolve(plan.Filters.DivisionTokens, snapshot.Divisions, item => item.Token, item => item.StandardId))
			.Distinct().ToList();
		var divisionIds = Resolve(
			plan.Filters.DivisionTokens, snapshot.Divisions, item => item.Token, item => item.Id).ToList();

		if (scope.ClassIds is { Count: > 0 })
		{
			query = query.Where(student => student.StandardId.HasValue && scope.ClassIds.Contains(student.StandardId.Value));
		}
		if (scope.DivisionIds is { Count: > 0 })
		{
			query = query.Where(student => student.DivisionId.HasValue && scope.DivisionIds.Contains(student.DivisionId.Value));
		}
		if (classIds.Count > 0)
		{
			query = query.Where(student => student.StandardId.HasValue && classIds.Contains(student.StandardId.Value));
		}
		if (divisionIds.Count > 0)
		{
			query = query.Where(student => student.DivisionId.HasValue && divisionIds.Contains(student.DivisionId.Value));
		}

		query = ApplyStatusPushdown(query, plan.Where);
		var records = await asyncExecuter.ToListAsync(query
			.OrderBy(student => student.StandardId)
			.ThenBy(student => student.DivisionId)
			.ThenBy(student => student.RollNo)
			.Select(student => new StudentRecordProjection
			{
				StandardId = student.StandardId,
				DivisionId = student.DivisionId,
				GrNumber = student.GrNumber,
				GrNo = student.GrNo,
				RollNo = student.RollNo,
				FirstName = student.FirstName,
				MiddleName = student.MiddleName,
				LastName = student.LastName,
				LifecycleStatus = student.Status,
				AdmissionApprovalStatus = student.AdmissionApprovalStatus
			})
			.Take(maximumRows + 1));

		var classNames = snapshot.Classes.ToDictionary(item => item.Id, item => item.Name);
		var divisionNames = snapshot.Divisions.ToDictionary(item => item.Id, item => item.Name);
		return records.Select(record => Row(
			("class", record.StandardId.HasValue
				? classNames.GetValueOrDefault(record.StandardId.Value, "Unassigned") : "Unassigned"),
			("division", record.DivisionId.HasValue
				? divisionNames.GetValueOrDefault(record.DivisionId.Value, "Unassigned") : "Unassigned"),
			("grNumber", record.GrNumber ?? record.GrNo?.ToString(CultureInfo.InvariantCulture)),
			("rollNumber", record.RollNo?.ToString(CultureInfo.InvariantCulture)),
			("studentName", FullName(record.FirstName, record.MiddleName, record.LastName)),
			("lifecycleStatus", record.LifecycleStatus.ToString()),
			("admissionApprovalStatus", record.AdmissionApprovalStatus.ToString()))).ToList();
	}

	internal static IQueryable<Student> ApplyStatusPushdown(
		IQueryable<Student> query,
		AcademicDynamicReportWhereDto where)
	{
		if (!string.Equals(where.Match, AcademicDynamicMatchModes.All, StringComparison.OrdinalIgnoreCase)) return query;
		foreach (var condition in where.Conditions)
		{
			if (string.Equals(condition.Field, "lifecycleStatus", StringComparison.OrdinalIgnoreCase) &&
				TryParseEnums<StudentLifeCycleStatusEnum>(condition.Values, out var lifecycleStatuses))
			{
				query = condition.Operator switch
				{
					AcademicDynamicFilterOperators.Equal => query.Where(student => student.Status == lifecycleStatuses[0]),
					AcademicDynamicFilterOperators.NotEquals => query.Where(student => student.Status != lifecycleStatuses[0]),
					AcademicDynamicFilterOperators.In => query.Where(student => lifecycleStatuses.Contains(student.Status)),
					_ => query
				};
			}
			else if (string.Equals(condition.Field, "admissionApprovalStatus", StringComparison.OrdinalIgnoreCase) &&
				TryParseEnums<StudentEnrollmentApprovalState>(condition.Values, out var approvalStatuses))
			{
				query = condition.Operator switch
				{
					AcademicDynamicFilterOperators.Equal => query.Where(student => student.AdmissionApprovalStatus == approvalStatuses[0]),
					AcademicDynamicFilterOperators.NotEquals => query.Where(student => student.AdmissionApprovalStatus != approvalStatuses[0]),
					AcademicDynamicFilterOperators.In => query.Where(student => approvalStatuses.Contains(student.AdmissionApprovalStatus)),
					_ => query
				};
			}
		}
		return query;
	}

	private static bool TryParseEnums<TEnum>(IReadOnlyCollection<string> values, out List<TEnum> parsed)
		where TEnum : struct, Enum
	{
		parsed = [];
		foreach (var value in values)
		{
			if (!Enum.TryParse<TEnum>(value, true, out var result)) return false;
			parsed.Add(result);
		}
		return parsed.Count > 0;
	}

	private static IEnumerable<TResult> Resolve<T, TResult>(IEnumerable<string> tokens, IEnumerable<T> catalog,
		Func<T, string> tokenSelector, Func<T, TResult> valueSelector)
	{
		var tokenSet = tokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
		return catalog.Where(item => tokenSet.Contains(tokenSelector(item))).Select(valueSelector);
	}

	private static Dictionary<string, string?> Row(params (string Key, string? Value)[] values) =>
		values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

	private static string FullName(params string?[] parts) =>
		string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));

	private sealed class StudentRecordProjection
	{
		public Guid? StandardId { get; set; }
		public Guid? DivisionId { get; set; }
		public string? GrNumber { get; set; }
		public int? GrNo { get; set; }
		public int? RollNo { get; set; }
		public string? FirstName { get; set; }
		public string? MiddleName { get; set; }
		public string? LastName { get; set; }
		public StudentLifeCycleStatusEnum LifecycleStatus { get; set; }
		public StudentEnrollmentApprovalState AdmissionApprovalStatus { get; set; }
	}
}
