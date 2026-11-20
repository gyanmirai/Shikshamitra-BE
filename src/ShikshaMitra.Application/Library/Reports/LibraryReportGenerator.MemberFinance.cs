using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Charges;
using Volo.Abp;

namespace ShikshaMitra.Library.Reports;

public partial class LibraryReportGenerator
{
	private async Task<Dictionary<(int Role, Guid MemberId), List<FinanceExternalSourcePosition>>> ReadMemberFinanceAsync(
		IReadOnlyCollection<LibraryMemberReportInfo> members, Guid? academicYearId)
	{
		var results = new Dictionary<(int, Guid), List<FinanceExternalSourcePosition>>();
		if (members.Count == 0) return results;
		var students = members.Where(member => member.RoleValue == 0).Select(member => member.Id).ToArray();
		var staff = members.Where(member => member.RoleValue == 1).Select(member => member.Id).ToArray();
		var input = new LibraryChargePositionReadInput(LibraryChargePositionReadPurpose.Reports, academicYearId);
		var readableIds = await chargePositionReader.GetReadableSourceIdsAsync(input);
		var aliases = (await membershipRepository.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id);
		var charges = from charge in await chargeRepository.GetQueryableAsync()
			join id in readableIds on charge.Id equals id
			where charge.TenantId == tenant.Id && (charge.MemberRole == 0 || charge.MemberRole == 1 || charge.MemberRole == 2)
			select new
			{
				charge.Id, Role = charge.MemberRole == 0 ? 0 : 1,
				MemberId = charge.MemberRole == 0 ? aliases.Where(alias => alias.Id == charge.MemberId)
					.Select(alias => (Guid?)alias.StudentId).FirstOrDefault() ?? charge.MemberId : charge.MemberId
			};
		var sources = await asyncExecuter.ToListAsync(charges.Where(row => row.Role == 0 && Enumerable.Contains(students, row.MemberId) ||
			row.Role == 1 && Enumerable.Contains(staff, row.MemberId)).OrderBy(row => row.Id));
		foreach (var batch in sources.Chunk(100))
		{
			var ids = batch.Select(row => row.Id).ToArray();
			var positions = (await chargePositionReader.ReadAsync(input with { AssessmentIds = ids })).ToDictionary(row => row.SourceId);
			foreach (var source in batch)
			{
				if (!positions.TryGetValue(source.Id, out var position)) throw new BusinessException(LibraryFinanceErrorCodes.SourceChanged);
				var key = (source.Role, source.MemberId);
				if (!results.TryGetValue(key, out var entries)) results.Add(key, entries = []);
				entries.Add(position);
			}
		}
		return results;
	}

	private sealed record MemberFinanceColumns(string Evidence, string? Currency, decimal? Collectible)
	{
		public static MemberFinanceColumns From(IReadOnlyList<FinanceExternalSourcePosition> positions)
		{
			if (positions.Count == 0) return new("No assessments", null, null);
			var evidence = positions.All(row => row.Evidence == positions[0].Evidence) ? positions[0].Evidence.ToString() : "Mixed";
			var verified = positions.Where(row => row.Evidence == FinanceExternalPositionEvidence.Verified && row.Amounts != null).ToArray();
			var currencies = verified.Select(row => row.Currency).Distinct(StringComparer.Ordinal).ToArray();
			if (currencies.Length != 1) return new(evidence, null, null);
			return new(evidence, currencies[0], verified.Sum(row => row.Amounts!.Collectible));
		}
	}
}
