using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using Volo.Abp.Validation;

namespace ShikshaMitra.TenantSettings.Reports;

internal static partial class InstituteReportHistoryQuery
{
	private sealed record Criterion(string Column, string Type, string Value);
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
	private static readonly HashSet<string> TextColumns = ["reportType", "format", "status", "fileName"];
	private static readonly HashSet<string> DateColumns = ["creationTime", "completedAt", "expiresAt"];
	public static IQueryable<InstituteReportHistoryRow> Apply(IQueryable<InstituteReportHistoryRow> query, InstituteReportHistoryInput input, Guid[] dateMatches)
	{
		if (input.Filter?.Length > 200 || input.ColumnFilters?.Length > 16384) throw Invalid();
		if (!string.IsNullOrWhiteSpace(input.Filter))
		{
			var term = input.Filter.Trim().ToLowerInvariant();
			var ids = InstituteReportCatalog.All.Where(x => x.Label.Contains(term, StringComparison.OrdinalIgnoreCase)).Select(x => x.Id).ToArray();
			var pending = "pending".Contains(term, StringComparison.Ordinal);
			query = query.Where(x => Enumerable.Contains(ids, x.Job.ReportTypeId) || x.Format.ToLower().Contains(term) || x.Status.ToLower().Contains(term)
				|| (x.FileName ?? "Not available").ToLower().Contains(term) || Enumerable.Contains(dateMatches, x.Job.Id)
				|| (pending && !x.Job.CompletedAt.HasValue));
		}
		List<Criterion> criteria;
		try { criteria = string.IsNullOrWhiteSpace(input.ColumnFilters) ? [] : JsonSerializer.Deserialize<List<Criterion>>(input.ColumnFilters, Json) ?? throw Invalid(); }
		catch (JsonException) { throw Invalid(); }
		if (criteria.Count > 10 || criteria.Any(x => x == null || x.Value == null || x.Value.Length > 200)) throw Invalid();
		foreach (var group in criteria.GroupBy(x => x.Column))
		{
			var row = Expression.Parameter(typeof(InstituteReportHistoryRow), "row");
			Expression combined = Expression.Constant(false);
			foreach (var criterion in group)
			{
				Expression match;
				if (TextColumns.Contains(criterion.Column))
				{
					if (criterion.Type is not ("equals" or "contains" or "startsWith" or "endsWith")) throw Invalid();
					var field = Text(row, criterion.Column);
					var text = Expression.Call(Expression.Coalesce(field, Expression.Constant("Not available")), nameof(string.ToLower), Type.EmptyTypes);
					var value = Expression.Constant(criterion.Value.ToLowerInvariant());
					match = criterion.Type == "equals" ? Expression.Equal(text, value) : Expression.Call(text,
						criterion.Type == "contains" ? nameof(string.Contains) : criterion.Type == "startsWith" ? nameof(string.StartsWith) : nameof(string.EndsWith), Type.EmptyTypes, value);
				}
				else if (DateColumns.Contains(criterion.Column))
				{
					if (criterion.Type is not ("equals" or "after" or "before") || !DateTimeOffset.TryParse(criterion.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)) throw Invalid();
					var start = new DateTime(date.UtcTicks - date.UtcTicks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);
					var field = Date(row, criterion.Column);
					var lower = Expression.GreaterThanOrEqual(field, Expression.Constant((DateTime?)start, typeof(DateTime?)));
					var upper = Expression.LessThan(field, Expression.Constant((DateTime?)start.AddMinutes(1), typeof(DateTime?)));
					match = criterion.Type == "after" ? lower : criterion.Type == "before" ? upper : Expression.AndAlso(lower, upper);
				}
				else throw Invalid();
				combined = Expression.OrElse(combined, match);
			}
			query = query.Where(Expression.Lambda<Func<InstituteReportHistoryRow, bool>>(combined, row));
		}
		return query;
	}
	private static Expression Text(Expression row, string column) => Expression.Property(row, column switch {
		"reportType" => nameof(InstituteReportHistoryRow.ReportType), "format" => nameof(InstituteReportHistoryRow.Format),
		"status" => nameof(InstituteReportHistoryRow.Status), "fileName" => nameof(InstituteReportHistoryRow.FileName), _ => throw Invalid() });
	private static Expression Date(Expression row, string column)
	{
		var value = Expression.Property(Expression.Property(row, nameof(InstituteReportHistoryRow.Job)), column switch {
			"creationTime" => nameof(InstituteReportJob.CreationTime), "completedAt" => nameof(InstituteReportJob.CompletedAt), "expiresAt" => nameof(InstituteReportJob.ExpiresAt), _ => throw Invalid() });
		return value.Type == typeof(DateTime?) ? value : Expression.Convert(value, typeof(DateTime?));
	}
	private static AbpValidationException Invalid() => new("Use supported report history fields, sorting and up to 10 valid column filters.");
}
