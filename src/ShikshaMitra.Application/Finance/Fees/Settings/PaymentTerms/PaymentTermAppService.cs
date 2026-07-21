using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.Services;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Settings.PaymentTerms;

[Authorize]
[RemoteService(false)]
public class PaymentTermAppService(
	IRepository<PaymentTerm, Guid> paymentTermRepository,
	IRepository<FeeStructure, Guid> feeStructureRepository,
	IRepository<EducationBoard, Guid> boardRepository,
	IAcademicYearRepository academicYearRepository,
	PaymentTermScheduleResolver scheduleResolver
) : ApplicationService, IPaymentTermAppService
{
	public async Task<PagedResultDto<PaymentTermListDto>> GetListAsync(PaymentTermGetListInput input)
	{
		var query = await paymentTermRepository.GetQueryableAsync();
		query = query.Where(t => t.AcademicYearId == input.AcademicYearId);

		if (input.BoardId.HasValue)
		{
			query = query.Where(t => t.BoardId == input.BoardId.Value);
		}

		if (!input.IncludeArchived)
		{
			query = query.Where(t => t.Status != PaymentTermStatus.Archived);
		}

		if (input.Status.HasValue)
		{
			query = query.Where(t => t.Status == input.Status.Value);
		}

		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim();
			query = query.Where(t => t.Name.Contains(search) || t.Code.Contains(search));
		}

		var totalCount = await AsyncExecuter.CountAsync(query);
		query = query.OrderByDescending(t => t.CreationTime)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount);

		var items = await AsyncExecuter.ToListAsync(query);
		var termIds = items.Select(t => t.Id).ToList();
		var referenceCounts = await GetReferenceCountsAsync(termIds);

		return new PagedResultDto<PaymentTermListDto>(
			totalCount,
			items.Select(t => MapListDto(t, referenceCounts.GetValueOrDefault(t.Id))).ToList());
	}

	public async Task<PaymentTermDto> GetAsync(Guid paymentTermId)
	{
		var term = await GetTermWithVersionsAsync(paymentTermId);
		var referenceCount = (await GetReferenceCountsAsync(new[] { term.Id })).GetValueOrDefault(term.Id);
		return await MapDetailDtoAsync(term, referenceCount);
	}

	public async Task<PaymentTermDto> CreateAsync(CreatePaymentTermDto input)
	{
		PaymentTermInstallmentValidator.Validate(input.Installments);
		await EnsureUniqueCodeAsync(input.AcademicYearId, input.Code, excludeId: null);

		if (input.DemandGenerationRule == DemandGenerationRule.XDaysBeforeDue &&
			(!input.DemandOffsetDays.HasValue || input.DemandOffsetDays.Value < 0))
		{
			throw new BusinessException(PaymentTermErrorCodes.DemandOffsetDaysMustBeNonNegative);
		}

		var term = new PaymentTerm(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			input.AcademicYearId,
			input.BoardId,
			input.Name,
			input.Code,
			input.Description,
			input.IsDefault,
			input.HolidayShiftingMethod,
			input.DemandGenerationRule,
			input.DemandOffsetDays,
			input.InitialDemandStatus);

		if (input.IsDefault)
		{
			await ClearDefaultForYearBoardAsync(input.AcademicYearId, input.BoardId, excludeId: null);
		}

		var version = new PaymentTermVersion(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			term.Id,
			versionNumber: 1,
			PaymentTermVersionStatus.Draft,
			effectiveFrom: null,
			effectiveTo: null);

		version.SetInstallments(BuildInstallments(version.Id, input.Installments));
		// First version is published immediately so the term can be attached to fee structures.
		version.Publish(Clock.Now, CurrentUser.Id ?? Guid.Empty);
		term.AddVersion(version);
		term.SetCurrentVersion(version.Id);

		await paymentTermRepository.InsertAsync(term, autoSave: true);
		return await GetAsync(term.Id);
	}

	public async Task<PaymentTermDto> UpdateDraftAsync(Guid paymentTermId, UpdatePaymentTermDraftDto input)
	{
		PaymentTermInstallmentValidator.Validate(input.Installments);

		var term = await GetTermWithVersionsAsync(paymentTermId);
		if (term.Status == PaymentTermStatus.Archived)
		{
			throw new BusinessException(PaymentTermErrorCodes.ArchiveInUse)
				.WithData("PaymentTermId", paymentTermId);
		}

		await EnsureUniqueCodeAsync(term.AcademicYearId, input.Code, paymentTermId);

		var draft = term.GetDraftVersion();
		if (draft == null)
		{
			// Editing a published-only term creates an implicit revision draft.
			var source = term.GetPublishedVersion() ?? term.GetCurrentVersion()
				?? throw new BusinessException(PaymentTermErrorCodes.NoDraftVersion)
					.WithData("PaymentTermId", paymentTermId);

			var nextVersionNumber = term.Versions.Max(v => v.VersionNumber) + 1;
			draft = new PaymentTermVersion(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				term.Id,
				nextVersionNumber,
				PaymentTermVersionStatus.Draft,
				effectiveFrom: null,
				effectiveTo: null,
				changeReason: "Edited published schedule");
			term.AddVersion(draft);
		}

		term.UpdateMetadata(
			input.Name,
			input.Code,
			input.Description,
			input.HolidayShiftingMethod,
			input.DemandGenerationRule,
			input.DemandOffsetDays,
			input.InitialDemandStatus);

		draft.SetInstallments(BuildInstallments(draft.Id, input.Installments));

		// If the term is not yet referenced, publishing the draft keeps assignment simple.
		var referenceCount = (await GetReferenceCountsAsync(new[] { term.Id })).GetValueOrDefault(term.Id);
		if (referenceCount == 0 && draft.Status == PaymentTermVersionStatus.Draft)
		{
			var previousPublished = term.GetPublishedVersion();
			draft.Publish(Clock.Now, CurrentUser.Id ?? Guid.Empty);
			previousPublished?.Supersede(Clock.Now.Date.AddDays(-1));
			term.SetCurrentVersion(draft.Id);
		}

		await paymentTermRepository.UpdateAsync(term, autoSave: true);
		return await GetAsync(term.Id);
	}

	public async Task DeleteAsync(Guid paymentTermId)
	{
		var term = await GetTermWithVersionsAsync(paymentTermId);
		var referenceCount = (await GetReferenceCountsAsync(new[] { term.Id })).GetValueOrDefault(term.Id);
		if (referenceCount > 0)
		{
			throw new BusinessException(PaymentTermErrorCodes.DeleteWithReferences)
				.WithData("ReferencedStructureCount", referenceCount);
		}

		await paymentTermRepository.DeleteAsync(term);
	}

	public async Task<PaymentTermDto> CreateRevisionAsync(Guid paymentTermId, CreateRevisionDto input)
	{
		if (string.IsNullOrWhiteSpace(input.ChangeReason))
		{
			throw new BusinessException(PaymentTermErrorCodes.ChangeReasonRequired);
		}

		var term = await GetTermWithVersionsAsync(paymentTermId);
		if (term.GetDraftVersion() != null)
		{
			return await GetAsync(term.Id);
		}

		var source = input.SourceVersionId.HasValue
			? term.Versions.FirstOrDefault(v => v.Id == input.SourceVersionId.Value)
			: term.GetPublishedVersion() ?? term.GetCurrentVersion();

		if (source == null)
		{
			throw new BusinessException(PaymentTermErrorCodes.NotFound)
				.WithData("PaymentTermId", paymentTermId);
		}

		var nextVersionNumber = term.Versions.Max(v => v.VersionNumber) + 1;
		var draft = new PaymentTermVersion(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			term.Id,
			nextVersionNumber,
			PaymentTermVersionStatus.Draft,
			effectiveFrom: null,
			effectiveTo: null,
			changeReason: input.ChangeReason);

		var clonedInstallments = source.Installments
			.OrderBy(i => i.Sequence)
			.Select(i => new PaymentTermInstallment(
				GuidGenerator.Create(),
				draft.Id,
				i.Sequence,
				i.Name,
				i.CoverageStartDate,
				i.CoverageEndDate,
				i.DemandDate,
				i.DueDate,
				i.AllocationPercentage,
				i.DemandEnabled,
				i.ReminderEnabled,
				i.ReminderDate,
				i.AllowPartialOverride,
				i.GracePeriodDaysOverride))
			.ToList();

		draft.SetInstallments(clonedInstallments);
		term.AddVersion(draft);
		await paymentTermRepository.UpdateAsync(term, autoSave: true);
		return await GetAsync(term.Id);
	}

	public async Task<PublishResultDto> PublishVersionAsync(Guid paymentTermId, Guid versionId, PublishVersionDto input)
	{
		var term = await GetTermWithVersionsAsync(paymentTermId);
		var version = term.Versions.FirstOrDefault(v => v.Id == versionId)
			?? throw new BusinessException(PaymentTermErrorCodes.NotFound)
				.WithData("VersionId", versionId);

		var installmentDtos = version.Installments
			.OrderBy(i => i.Sequence)
			.Select(MapInstallmentToCreateDto)
			.ToList();
		PaymentTermInstallmentValidator.Validate(installmentDtos);

		var previousPublished = term.GetPublishedVersion();
		version.Publish(input.EffectiveFrom, CurrentUser.Id ?? Guid.Empty);
		previousPublished?.Supersede(input.EffectiveFrom.Date.AddDays(-1));
		term.SetCurrentVersion(version.Id);

		await paymentTermRepository.UpdateAsync(term, autoSave: true);

		return new PublishResultDto
		{
			VersionId = version.Id,
			VersionNumber = version.VersionNumber,
			ValidationWarnings = new List<string>(),
			ImpactPreview = await GetImpactPreviewAsync(paymentTermId)
		};
	}

	public async Task ArchiveAsync(Guid paymentTermId, ArchivePaymentTermDto input)
	{
		var term = await GetTermWithVersionsAsync(paymentTermId);
		term.Archive(input.Reason);
		await paymentTermRepository.UpdateAsync(term, autoSave: true);
	}

	public async Task<PaymentTermDto> RestoreAsync(Guid paymentTermId)
	{
		var term = await GetTermWithVersionsAsync(paymentTermId);
		term.Restore();
		await paymentTermRepository.UpdateAsync(term, autoSave: true);
		return await GetAsync(term.Id);
	}

	public async Task<ClonePaymentTermsResultDto> CloneAsync(ClonePaymentTermsDto input)
	{
		if (input.SourceAcademicYearId == input.TargetAcademicYearId)
		{
			throw new BusinessException("Source and target academic years must be different.");
		}

		_ = await academicYearRepository.GetAsync(input.TargetAcademicYearId);

		var query = await paymentTermRepository.GetQueryableAsync();
		query = query.Where(t => t.AcademicYearId == input.SourceAcademicYearId);

		if (input.PaymentTermIds is { Count: > 0 })
		{
			query = query.Where(t => input.PaymentTermIds.Contains(t.Id));
		}

		var sourceTerms = await AsyncExecuter.ToListAsync(query);
		var result = new ClonePaymentTermsResultDto();

		foreach (var source in sourceTerms)
		{
			var sourceVersion = source.GetPublishedVersion() ?? source.GetCurrentVersion();
			if (sourceVersion == null)
			{
				continue;
			}

			var existing = await paymentTermRepository.FirstOrDefaultAsync(t =>
				t.AcademicYearId == input.TargetAcademicYearId &&
				t.BoardId == source.BoardId &&
				t.Code == source.Code);

			if (existing != null)
			{
				result.ConflictedCodes.Add(source.Code);
				continue;
			}

			var clone = new PaymentTerm(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				input.TargetAcademicYearId,
				source.BoardId,
				source.Name,
				source.Code,
				source.Description,
				isDefault: false,
				source.HolidayShiftingMethod,
				source.DemandGenerationRule,
				source.DemandOffsetDays,
				source.InitialDemandStatus);

			var version = new PaymentTermVersion(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				clone.Id,
				1,
				PaymentTermVersionStatus.Draft,
				null,
				null,
				changeReason: $"Cloned from academic year {input.SourceAcademicYearId}");

			version.SetInstallments(
				sourceVersion.Installments
					.OrderBy(i => i.Sequence)
					.Select(i => new PaymentTermInstallment(
						GuidGenerator.Create(),
						version.Id,
						i.Sequence,
						i.Name,
						i.CoverageStartDate,
						i.CoverageEndDate,
						i.DemandDate,
						i.DueDate,
						i.AllocationPercentage,
						i.DemandEnabled,
						i.ReminderEnabled,
						i.ReminderDate,
						i.AllowPartialOverride,
						i.GracePeriodDaysOverride))
					.ToList());

			clone.AddVersion(version);
			await paymentTermRepository.InsertAsync(clone, autoSave: true);
			result.ClonedTermIds.Add(clone.Id);
			result.DateRangesRequiringReview.Add($"{clone.Code}: review installment dates for target year");
		}

		return result;
	}

	public async Task<ImpactPreviewDto> GetImpactPreviewAsync(Guid paymentTermId)
	{
		var structureQuery = await feeStructureRepository.GetQueryableAsync();
		var details = await AsyncExecuter.ToListAsync(
			structureQuery
				.SelectMany(fs => fs.FeeStructureDetails)
				.Where(d => d.PaymentTermId == paymentTermId)
				.Select(d => new { d.FeeStructureId, StructureName = d.FeeStructureId }));

		var structureIds = details.Select(d => d.FeeStructureId).Distinct().ToList();
		var structures = structureIds.Count == 0
			? new List<FeeStructure>()
			: await feeStructureRepository.GetListAsync(fs => structureIds.Contains(fs.Id));

		return new ImpactPreviewDto
		{
			AffectedStructures = structures.Count,
			AffectedStudents = 0,
			ExistingIssuedDueCount = 0,
			FutureUngeneratedInstanceCount = 0,
			ProjectedTotalAmount = 0,
			Structures = structures.Select(fs => new ImpactPreviewStructureDto
			{
				FeeStructureId = fs.Id,
				FeeStructureName = fs.Name,
				StudentCount = 0,
				IssuedDueCount = 0,
				FutureInstanceCount = 0,
				ProjectedAmount = 0
			}).ToList()
		};
	}

	public async Task<FeeStructureSchedulePreviewDto> GetFeeStructureSchedulePreviewAsync(Guid feeStructureId)
	{
		var structure = await feeStructureRepository.GetAsync(feeStructureId);
		var schedules = await scheduleResolver.ResolveAllAsync(structure);
		var mapped = schedules.Select(s => new ResolvedFeeHeadScheduleDto
		{
			FeeStructureDetailId = s.FeeStructureDetailId,
			FeeHeadId = s.FeeHeadId,
			FeeHeadName = s.FeeHeadName ?? string.Empty,
			AnnualAmount = s.AnnualAmount,
			PaymentTermId = s.PaymentTermId,
			PaymentTermName = s.PaymentTermName,
			PaymentTermVersionId = s.PaymentTermVersionId,
			VersionNumber = s.VersionNumber,
			ScheduleMode = s.ScheduleMode,
			IsValid = s.IsValid,
			Installments = s.Installments.Select(i => new ResolvedInstallmentDto
			{
				Sequence = i.Sequence,
				Name = i.Name,
				CoverageStartDate = i.CoverageStartDate,
				CoverageEndDate = i.CoverageEndDate,
				DemandDate = i.DemandDate,
				DueDate = i.DueDate,
				AllocationPercentage = i.AllocationPercentage,
				CalculatedAmount = i.CalculatedAmount,
				AllowPartialOverride = i.AllowPartialOverride,
				GracePeriodDaysOverride = i.GracePeriodDaysOverride
			}).ToList()
		}).ToList();

		var errors = mapped
			.Where(m => !m.IsValid && m.PaymentTermId == null)
			.Select(m => $"Fee head '{m.FeeHeadName}' has no Payment Term attached.")
			.ToList();

		return new FeeStructureSchedulePreviewDto
		{
			FeeStructureId = structure.Id,
			FeeStructureName = structure.Name,
			FeeHeadSchedules = mapped,
			IsValid = errors.Count == 0,
			ValidationErrors = errors,
			ValidationWarnings = new List<string>()
		};
	}

	private async Task<PaymentTerm> GetTermWithVersionsAsync(Guid paymentTermId)
	{
		// Versions + Installments are AutoIncluded via EF configuration.
		var term = await paymentTermRepository.FindAsync(paymentTermId);
		if (term == null)
		{
			throw new BusinessException(PaymentTermErrorCodes.NotFound)
				.WithData("PaymentTermId", paymentTermId);
		}

		return term;
	}

	private async Task EnsureUniqueCodeAsync(Guid academicYearId, string code, Guid? excludeId)
	{
		var normalized = code.Trim().ToUpperInvariant();
		var exists = await paymentTermRepository.AnyAsync(t =>
			t.AcademicYearId == academicYearId &&
			t.Code == normalized &&
			(!excludeId.HasValue || t.Id != excludeId.Value));

		if (exists)
		{
			throw new BusinessException(PaymentTermErrorCodes.DuplicateCode)
				.WithData("Code", normalized);
		}
	}

	private async Task ClearDefaultForYearBoardAsync(Guid academicYearId, Guid boardId, Guid? excludeId)
	{
		var defaults = await paymentTermRepository.GetListAsync(t =>
			t.AcademicYearId == academicYearId &&
			t.BoardId == boardId &&
			t.IsDefault &&
			(!excludeId.HasValue || t.Id != excludeId.Value));

		foreach (var item in defaults)
		{
			item.SetDefault(false);
			await paymentTermRepository.UpdateAsync(item);
		}
	}

	private async Task<Dictionary<Guid, int>> GetReferenceCountsAsync(IEnumerable<Guid> termIds)
	{
		var ids = termIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return new Dictionary<Guid, int>();
		}

		var structureQuery = await feeStructureRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(
			structureQuery
				.SelectMany(fs => fs.FeeStructureDetails)
				.Where(d => d.PaymentTermId.HasValue && ids.Contains(d.PaymentTermId.Value))
				.GroupBy(d => d.PaymentTermId!.Value)
				.Select(g => new { TermId = g.Key, Count = g.Count() }));

		return rows.ToDictionary(r => r.TermId, r => r.Count);
	}

	private List<PaymentTermInstallment> BuildInstallments(
		Guid versionId,
		IEnumerable<CreatePaymentTermInstallmentDto> installments)
	{
		return installments
			.OrderBy(i => i.Sequence)
			.Select(i => new PaymentTermInstallment(
				GuidGenerator.Create(),
				versionId,
				i.Sequence,
				i.Name.Trim(),
				i.CoverageStartDate,
				i.CoverageEndDate,
				i.DemandDate,
				i.DueDate,
				i.AllocationPercentage,
				i.DemandEnabled,
				i.ReminderEnabled,
				i.ReminderDate,
				i.AllowPartialOverride,
				i.GracePeriodDaysOverride))
			.ToList();
	}

	private static CreatePaymentTermInstallmentDto MapInstallmentToCreateDto(PaymentTermInstallment i) => new()
	{
		Sequence = i.Sequence,
		Name = i.Name,
		CoverageStartDate = i.CoverageStartDate,
		CoverageEndDate = i.CoverageEndDate,
		DemandDate = i.DemandDate,
		DueDate = i.DueDate,
		AllocationPercentage = i.AllocationPercentage,
		DemandEnabled = i.DemandEnabled,
		ReminderEnabled = i.ReminderEnabled,
		ReminderDate = i.ReminderDate,
		AllowPartialOverride = i.AllowPartialOverride,
		GracePeriodDaysOverride = i.GracePeriodDaysOverride
	};

	private static PaymentTermListDto MapListDto(PaymentTerm term, int referencedCount)
	{
		var current = term.GetCurrentVersion();
		return new PaymentTermListDto
		{
			Id = term.Id,
			Name = term.Name,
			Code = term.Code,
			AcademicYearId = term.AcademicYearId,
			BoardId = term.BoardId,
			Status = term.Status,
			IsDefault = term.IsDefault,
			CurrentVersionNumber = current?.VersionNumber ?? 0,
			InstallmentCount = current?.Installments.Count ?? 0,
			ReferencedStructureCount = referencedCount,
			CanDirectEdit = term.Status == PaymentTermStatus.Active && referencedCount == 0
		};
	}

	private async Task<PaymentTermDto> MapDetailDtoAsync(PaymentTerm term, int referencedCount)
	{
		string? boardName = null;
		var board = await boardRepository.FindAsync(term.BoardId);
		boardName = board?.Type.ToString();

		string? academicYearName = null;
		try
		{
			var year = await academicYearRepository.FindAsync(term.AcademicYearId);
			academicYearName = year?.Name;
		}
		catch
		{
			// optional display enrichment
		}

		return new PaymentTermDto
		{
			Id = term.Id,
			AcademicYearId = term.AcademicYearId,
			AcademicYearName = academicYearName,
			BoardId = term.BoardId,
			BoardName = boardName,
			Name = term.Name,
			Code = term.Code,
			Description = term.Description,
			Status = term.Status,
			IsDefault = term.IsDefault,
			HolidayShiftingMethod = term.HolidayShiftingMethod,
			DemandGenerationRule = term.DemandGenerationRule,
			DemandOffsetDays = term.DemandOffsetDays,
			InitialDemandStatus = term.InitialDemandStatus,
			CurrentVersionId = term.CurrentVersionId,
			ArchivedAt = term.ArchivedAt,
			TotalInstallments = term.GetCurrentVersion()?.Installments.Count ?? 0,
			VersionCount = term.Versions.Count,
			IsUsedInFeeStructures = referencedCount > 0,
			CanDirectEdit = term.Status == PaymentTermStatus.Active && referencedCount == 0,
			Versions = term.Versions
				.OrderByDescending(v => v.VersionNumber)
				.Select(v => new PaymentTermVersionDto
				{
					Id = v.Id,
					VersionNumber = v.VersionNumber,
					Status = v.Status,
					EffectiveFrom = v.EffectiveFrom,
					EffectiveTo = v.EffectiveTo,
					PublishedAt = v.PublishedAt,
					ChangeReason = v.ChangeReason,
					Installments = v.Installments
						.OrderBy(i => i.Sequence)
						.Select(i => new PaymentTermInstallmentDto
						{
							Id = i.Id,
							Sequence = i.Sequence,
							Name = i.Name,
							CoverageStartDate = i.CoverageStartDate,
							CoverageEndDate = i.CoverageEndDate,
							DemandDate = i.DemandDate,
							DueDate = i.DueDate,
							AllocationPercentage = i.AllocationPercentage,
							DemandEnabled = i.DemandEnabled,
							ReminderEnabled = i.ReminderEnabled,
							ReminderDate = i.ReminderDate,
							AllowPartialOverride = i.AllowPartialOverride,
							GracePeriodDaysOverride = i.GracePeriodDaysOverride
						}).ToList()
				}).ToList()
		};
	}
}
