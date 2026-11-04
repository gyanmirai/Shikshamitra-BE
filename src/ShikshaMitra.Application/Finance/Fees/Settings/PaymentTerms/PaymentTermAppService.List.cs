using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings.PaymentTerms;

public partial class PaymentTermAppService
{
	private sealed class PaymentTermListRow
	{
		public Guid Id { get; init; }
		public Guid AcademicYearId { get; init; }
		public Guid BoardId { get; init; }
		public string Name { get; init; } = string.Empty;
		public string Code { get; init; } = string.Empty;
		public PaymentTermStatus Status { get; init; }
		public bool IsDefault { get; init; }
		public Guid? CurrentVersionId { get; init; }
		public List<PaymentTermListVersion> Versions { get; init; } = [];
	}

	private sealed class PaymentTermListVersion
	{
		public Guid Id { get; init; }
		public int VersionNumber { get; init; }
		public PaymentTermVersionStatus Status { get; init; }
		public int InstallmentCount { get; init; }
	}

	private Task<List<PaymentTermListRow>> ReadListRowsAsync(IQueryable<PaymentTerm> query) =>
		AsyncExecuter.ToListAsync(query.Select(term => new PaymentTermListRow
		{
			Id = term.Id, AcademicYearId = term.AcademicYearId, BoardId = term.BoardId,
			Name = term.Name, Code = term.Code, Status = term.Status, IsDefault = term.IsDefault,
			CurrentVersionId = term.CurrentVersionId,
			// Scalar projection bypasses Versions/Installments AutoInclude without changing aggregate reads.
			Versions = term.Versions.Select(version => new PaymentTermListVersion
			{
				Id = version.Id, VersionNumber = version.VersionNumber, Status = version.Status,
				InstallmentCount = version.Installments.Count
			}).ToList()
		}));

	private static PaymentTermListDto MapListDto(PaymentTermListRow term, int referencedCount)
	{
		var draft = term.Versions.FirstOrDefault(v => v.Status == PaymentTermVersionStatus.Draft);
		var published = term.Versions.Where(v => v.Status == PaymentTermVersionStatus.Published)
			.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
		var current = term.CurrentVersionId.HasValue
			? term.Versions.FirstOrDefault(v => v.Id == term.CurrentVersionId.Value)
			: term.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
		var latest = draft ?? published ?? current;
		return new PaymentTermListDto
		{
			Id = term.Id, Name = term.Name, Code = term.Code, AcademicYearId = term.AcademicYearId,
			BoardId = term.BoardId, IsDefault = term.IsDefault,
			Status = term.Status == PaymentTermStatus.Archived ? PaymentTermLifecycleStatus.Archived :
				draft != null ? PaymentTermLifecycleStatus.Draft : PaymentTermLifecycleStatus.Active,
			CurrentVersionNumber = latest?.VersionNumber ?? 0,
			PublishedVersionId = published?.Id, PublishedVersionNumber = published?.VersionNumber,
			HasDraft = draft != null, HasPublishedVersion = published != null,
			InstallmentCount = latest?.InstallmentCount ?? 0, ReferencedStructureCount = referencedCount,
			CanDirectEdit = term.Status == PaymentTermStatus.Active && referencedCount == 0
		};
	}
}
