using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Reads published Finance references; never writes Finance entities.</summary>
[RemoteService(false)]
public class AdmissionFeeStructureReader(
	IRepository<AdmissionOffering, Guid> offerings,
	IRepository<AdmissionCycle, Guid> cycles,
	IRepository<FeeStructureMetadata, Guid> metadata,
	IRepository<FeeStructure, Guid> structures,
	IRepository<FeeStructureDetail, Guid> details,
	IRepository<FeeHead, Guid> heads) : ApplicationService, ITransientDependency
{
	public static string ReferenceCurrency => new StudentFeeAssignmentPreviewDto().Currency;
	public async Task<List<AdmissionFeeStructureCandidateDto>> GetCandidatesAsync(AdmissionApplication application)
	{
		var offering = await offerings.GetAsync(application.AdmissionOfferingId);
		var cycle = await cycles.GetAsync(application.AdmissionCycleId);
		if (offering.TenantId != application.TenantId || cycle.TenantId != application.TenantId || offering.AdmissionCycleId != cycle.Id)
			throw new BusinessException(AdmissionFeeSelectionErrorCodes.InvalidSelection, "The fee selection does not belong to this admission scope.");
		var metadataIds = (await metadata.GetListAsync(m => m.TenantId == application.TenantId && m.AcademicYearId == cycle.AcademicYearId &&
			m.AcademicBoardId == offering.BoardId && m.AcademicMediumId == offering.MediumId)).Select(m => m.Id).ToArray();
		var rows = await structures.GetListAsync(s => s.TenantId == application.TenantId && s.IsLocked && s.StandardId == offering.StandardId &&
			Enumerable.Contains(metadataIds, s.FeeStructureMetadataId));
		var ids = rows.Select(s => s.Id).ToArray();
		var lines = await details.GetListAsync(d => d.TenantId == application.TenantId && Enumerable.Contains(ids, d.FeeStructureId));
		var headIds = lines.Select(d => d.FeeHeadId).Distinct().ToArray();
		var names = (await heads.GetListAsync(h => h.TenantId == application.TenantId && Enumerable.Contains(headIds, h.Id) && h.IsActive && h.AcademicYearId == cycle.AcademicYearId))
			.ToDictionary(h => h.Id, h => h.Name);
		return rows.Where(s => lines.Where(d => d.FeeStructureId == s.Id).All(d => names.ContainsKey(d.FeeHeadId)))
			.OrderBy(s => s.Name).ThenByDescending(s => s.RevisionNumber).Select(s => new AdmissionFeeStructureCandidateDto
		{
			StructureId = s.Id, StructureName = s.Name, RevisionNumber = s.RevisionNumber,
			AcademicYearId = cycle.AcademicYearId, StandardId = s.StandardId,
			Heads = lines.Where(d => d.FeeStructureId == s.Id).OrderBy(d => names.GetValueOrDefault(d.FeeHeadId)).Select(d =>
				new AdmissionFeeSelectionHeadDto { FeeHeadId = d.FeeHeadId, FeeHeadName = names.GetValueOrDefault(d.FeeHeadId, "Fee head"),
					ConfiguredAmount = d.Amount, IsVariable = d.IsVariable, IsOptional = d.IsOptional, IsOneTime = d.IsOneTime,
					PaymentTermId = d.PaymentTermId, PaymentTermVersionId = d.PaymentTermVersionId }).ToList()
		}).Where(s => s.Heads.Count > 0).ToList();
	}

	public async Task<AdmissionFeeSelectionSnapshot> CreateSnapshotAsync(AdmissionApplication application, Guid ownerStageId,
		string? currency, SaveAdmissionFeeSelectionDto input, Guid actorId)
	{
		var structure = (await GetCandidatesAsync(application)).SingleOrDefault(s => s.StructureId == input.StructureId)
			?? throw new BusinessException(AdmissionFeeSelectionErrorCodes.InvalidSelection,
				"Choose a published fee structure for this application's academic year and class.");
		if (input.OptionalHeadIds == null || input.OptionalHeadIds.Count > 100 ||
			input.OptionalHeadIds.Distinct().Count() != input.OptionalHeadIds.Count ||
			input.OptionalHeadIds.Any(id => !structure.Heads.Any(h => h.FeeHeadId == id && h.IsOptional)))
			throw new BusinessException(AdmissionFeeSelectionErrorCodes.InvalidSelection, "Choose valid optional fee heads from this structure.");
		return new(structure.StructureId, structure.StructureName, structure.RevisionNumber,
			structure.AcademicYearId, structure.StandardId, ownerStageId, currency,
			structure.Heads.Where(h => !h.IsOptional || input.OptionalHeadIds.Contains(h.FeeHeadId))
				.Select(h => new AdmissionFeeSelectionHead(h.FeeHeadId, h.FeeHeadName, h.ConfiguredAmount,
					h.IsOptional, h.IsOneTime, h.PaymentTermId, h.PaymentTermVersionId, h.IsVariable)).ToList(), actorId,
			new DateTimeOffset(Clock.Now.ToUniversalTime()));
	}
}
