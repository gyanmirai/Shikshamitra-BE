using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Common;
using Volo.Abp.Validation;

namespace ShikshaMitra.Finance.Fees.Settings.PaymentTerms;

public partial class PaymentTermAppService
{
	private static readonly IReadOnlyDictionary<string, FinanceTableColumn> ListFilterColumns =
		new Dictionary<string, FinanceTableColumn>
		{
			["name"] = new(FinanceTableValueType.Text),
			["currentVersionNumber"] = new(FinanceTableValueType.Number),
			["usage"] = new(FinanceTableValueType.Text),
			// The shared enum parser accepts numeric enums. Payment Terms exposes named lifecycle values.
			["status"] = new(FinanceTableValueType.Text)
		};

	private async Task<IQueryable<PaymentTerm>> ApplyListFiltersAsync(IQueryable<PaymentTerm> source, string? filters)
	{
		FinanceTableQuery table;
		try
		{
			table = FinanceTableQuery.Parse(filters, null, null, null, ListFilterColumns);
		}
		catch (ArgumentNullException ex) when (ex.ParamName == "key")
		{
			// A JSON null column reaches the shared parser's dictionary lookup before its validation.
			throw new AbpValidationException("Invalid finance table query.",
				[new ValidationResult("Choose a supported column, operator and value.", ["Filters"])]);
		}
		if (table.Filters.Any(f => f.Column == "status" &&
			(f.Type != "equals" || f.Value.GetString() is not ("Active" or "Draft" or "Archived"))))
		{
			throw new AbpValidationException("Invalid finance table query.",
				[new ValidationResult("Choose a supported lifecycle status and equality operator.", ["Filters"])]);
		}

		var fields = new Dictionary<string, LambdaExpression>
		{
			["name"] = (Expression<Func<PaymentTerm, string>>)(t => t.Name),
			// Match MapListDto: draft, latest published, then current pointer (or latest without a pointer).
			["currentVersionNumber"] = (Expression<Func<PaymentTerm, int>>)(t =>
				(t.Versions.Where(v => v.Status == PaymentTermVersionStatus.Draft)
					.Select(v => (int?)v.VersionNumber).FirstOrDefault() ??
				 t.Versions.Where(v => v.Status == PaymentTermVersionStatus.Published)
					.OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.VersionNumber).FirstOrDefault() ??
				 (t.CurrentVersionId.HasValue
					 ? t.Versions.Where(v => v.Id == t.CurrentVersionId.Value).Select(v => (int?)v.VersionNumber).FirstOrDefault()
					 : t.Versions.OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.VersionNumber).FirstOrDefault())) ?? 0),
			["status"] = (Expression<Func<PaymentTerm, string>>)(t =>
				t.Status == PaymentTermStatus.Archived ? "Archived" :
				t.Versions.Any(v => v.Status == PaymentTermVersionStatus.Draft) ? "Draft" : "Active")
		};

		if (table.Filters.Any(f => f.Column == "usage"))
		{
			var structures = await feeStructureRepository.GetQueryableAsync();
			var standards = await scopeAccess.ApplyCurrentUserScopeAsync(
				(await standardRepository.GetQueryableAsync()).Select(c => new UsageClass
				{
					StandardId = c.Id, Name = c.Name, EducationMediumStageId = c.EducationMediumStageId,
					EducationMediumId = c.EducationMediumStage.EducationMediumId,
					EducationBoardId = c.EducationMediumStage.EducationMedium.EducationBoardId,
					AcademicYearId = c.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId
				}));
			// Same joins and distinct counts as GetUsageRowsAsync/SummarizeUsage, across all saved versions.
			var assignments = from structure in structures
				from detail in structure.FeeStructureDetails
				join standard in standards on structure.StandardId equals standard.StandardId
				// Hydrating the existing usage rows joins the required FeeHead navigation for its name.
				// Retain that join (including tenant/soft-delete filters) when counting assignments.
				where detail.PaymentTermId.HasValue && detail.FeeHead.Name != null
				select new { TermId = detail.PaymentTermId.Value, detail.FeeHeadId, ClassId = standard.StandardId };
			fields["usage"] = (Expression<Func<PaymentTerm, string>>)(t =>
				assignments.Where(a => a.TermId == t.Id).Select(a => a.FeeHeadId).Distinct().Count().ToString() +
				" fee heads · " +
				assignments.Where(a => a.TermId == t.Id).Select(a => a.ClassId).Distinct().Count().ToString() + " classes");
		}

		return table.Apply(source, fields);
	}
}
