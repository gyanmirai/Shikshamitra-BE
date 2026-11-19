using System;
using System.Linq;
using System.Linq.Expressions;

namespace ShikshaMitra.TenantSettings.Reports;

internal sealed class InstituteReportHistoryRow
{
	public InstituteReportJob Job { get; set; } = null!;
	public string ReportType { get; set; } = "";
	public string Format { get; set; } = "";
	public string Status { get; set; } = "";
	public string? FileName { get; set; }
}

internal static partial class InstituteReportHistoryQuery
{
	public static IQueryable<InstituteReportHistoryRow> Project(IQueryable<InstituteReportJob> query, DateTime now)
	{
		var job = Expression.Parameter(typeof(InstituteReportJob), "job");
		Expression title = Expression.Constant("");
		foreach (var definition in InstituteReportCatalog.All.Reverse())
			title = Expression.Condition(Expression.Equal(Expression.Property(job, nameof(InstituteReportJob.ReportTypeId)), Expression.Constant(definition.Id)), Expression.Constant(definition.Label), title);
		var json = Expression.Property(job, nameof(InstituteReportJob.RequestJson));
		Expression HasFormat(string format) => Expression.OrElse(
			Expression.Call(json, nameof(string.Contains), Type.EmptyTypes, Expression.Constant($"\"Format\":\"{format}\"")),
			Expression.Call(json, nameof(string.Contains), Type.EmptyTypes, Expression.Constant($"\"format\":\"{format}\"")));
		var format = Expression.Condition(HasFormat("csv"), Expression.Constant("csv"), Expression.Condition(HasFormat("excel"), Expression.Constant("excel"), Expression.Constant("pdf")));
		var created = Expression.Property(job, nameof(InstituteReportJob.CreationTime));
		var timestamp = Join(Part(created, nameof(DateTime.Year), true), Part(created, nameof(DateTime.Month)), Part(created, nameof(DateTime.Day)), Expression.Constant("_"),
			Part(created, nameof(DateTime.Hour)), Part(created, nameof(DateTime.Minute)), Part(created, nameof(DateTime.Second)));
		var extension = Expression.Condition(Expression.Equal(format, Expression.Constant("excel")), Expression.Constant("xlsx"), format);
		var filename = Expression.Condition(Expression.Equal(Expression.Property(job, nameof(InstituteReportJob.Status)), Expression.Constant("Completed")),
			Join(Expression.Call(title, nameof(string.Replace), Type.EmptyTypes, Expression.Constant(" "), Expression.Constant("_")), Expression.Constant("_"), timestamp, Expression.Constant("."), extension), Expression.Constant(null, typeof(string)));
		var status = Expression.Condition(Expression.LessThanOrEqual(Expression.Property(job, nameof(InstituteReportJob.ExpiresAt)), Expression.Constant(now)), Expression.Constant("Expired"), Expression.Property(job, nameof(InstituteReportJob.Status)));
		var body = Expression.MemberInit(Expression.New(typeof(InstituteReportHistoryRow)),
			Expression.Bind(typeof(InstituteReportHistoryRow).GetProperty(nameof(InstituteReportHistoryRow.Job))!, job),
			Expression.Bind(typeof(InstituteReportHistoryRow).GetProperty(nameof(InstituteReportHistoryRow.ReportType))!, title),
			Expression.Bind(typeof(InstituteReportHistoryRow).GetProperty(nameof(InstituteReportHistoryRow.Format))!, format),
			Expression.Bind(typeof(InstituteReportHistoryRow).GetProperty(nameof(InstituteReportHistoryRow.Status))!, status),
			Expression.Bind(typeof(InstituteReportHistoryRow).GetProperty(nameof(InstituteReportHistoryRow.FileName))!, filename));
		return query.Select(Expression.Lambda<Func<InstituteReportJob, InstituteReportHistoryRow>>(body, job));
	}
	private static Expression Part(Expression date, string member, bool year = false)
	{
		var value = Expression.Property(date, member);
		var raw = Expression.Call(value, nameof(int.ToString), Type.EmptyTypes);
		Expression result = Expression.Condition(Expression.LessThan(value, Expression.Constant(10)), Join(Expression.Constant("0"), raw), raw);
		if (year) result = Expression.Condition(Expression.LessThan(value, Expression.Constant(10)), Join(Expression.Constant("000"), raw),
			Expression.Condition(Expression.LessThan(value, Expression.Constant(100)), Join(Expression.Constant("00"), raw),
				Expression.Condition(Expression.LessThan(value, Expression.Constant(1000)), Join(Expression.Constant("0"), raw), raw)));
		return result;
	}
	private static Expression Join(params Expression[] values) => values.Aggregate((left, right) => Expression.Add(left, right, typeof(string).GetMethod(nameof(string.Concat), [typeof(string), typeof(string)])!));
}
