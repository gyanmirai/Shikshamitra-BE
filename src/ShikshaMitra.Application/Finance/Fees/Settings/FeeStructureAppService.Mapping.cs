using System;

using System.Collections.Generic;

using System.Linq;

using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;

using ShikshaMitra.Constants.Permissions;

using ShikshaMitra.Domain.Finance.Fees.Settings;

using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;

using ShikshaMitra.Domain.Finance.Policies;

using ShikshaMitra.Finance.Fees.Settings.FeeStructures;

using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;

using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;

using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;

using ShikshaMitra.Security.UserScope;

using Volo.Abp;

using Volo.Abp.Domain.Repositories;

using Volo.Abp.Data;

using System.Text.Json;



namespace ShikshaMitra.Finance.Fees.Settings;



public partial class FeeStructureAppService

{

	private sealed record StructureScope(Guid? AcademicYearId, Guid? EducationBoardId, Guid? EducationMediumId,

		Guid? EducationMediumStageId, Guid? StandardId, Guid? DivisionId = null) : IUserScopedEntity;



	private async Task<StructureScope> EnsureStandardScopeAsync(Guid id)

	{

		// Scalar projection avoids the academic hierarchy's circular auto-includes.

		var scope = await AsyncExecuter.SingleOrDefaultAsync((await standardRepository.GetQueryableAsync())

			.Where(x => x.Id == id).Select(x => new StructureScope(

				x.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId,

				x.EducationMediumStage.EducationMedium.EducationBoardId,

				x.EducationMediumStage.EducationMediumId, x.EducationMediumStageId, x.Id, null)))

			?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(ShikshaMitra.TenantSettings.Standards.Standard), id);

		await scopeAccess.EnsureCurrentUserCanAccessAsync(scope);

		return scope;

	}



	private sealed record MetadataReference(Guid? AcademicYearId, Guid? AcademicBoardId, Guid? AcademicMediumId);

	private async Task<MetadataReference> GetMetadataReferenceAsync(Guid id) =>

		await AsyncExecuter.SingleOrDefaultAsync((await feeStructureMetadataRepository.GetQueryableAsync())

			.Where(x => x.Id == id).Select(x => new MetadataReference(x.AcademicYearId, x.AcademicBoardId, x.AcademicMediumId)))

		?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(FeeStructureMetadata), id);



	private sealed record BoardCalendar(Guid Id, DateTime StartDate, DateTime EndDate);

	private async Task<BoardCalendar> GetBoardCalendarAsync(Guid id) =>

		await AsyncExecuter.SingleOrDefaultAsync((await boardRepository.GetQueryableAsync())

			.Where(x => x.Id == id).Select(x => new BoardCalendar(x.Id, x.StartDate, x.EndDate)))

		?? throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(ShikshaMitra.TenantSettings.EducationBoards.EducationBoard), id);



	private async Task<FeeStructure> GetScopedAsync(Guid id)

	{

		var entity = await feeStructureRepository.GetAsync(id);

		await EnsureStandardScopeAsync(entity.StandardId);

		return entity;

	}



	private async Task EnsureUniqueNameAsync(Guid standardId, string name, Guid? exceptId, Guid? rootId)

	{

		var normalized = name.Trim().ToUpperInvariant();

		if (await feeStructureRepository.AnyAsync(x => x.StandardId == standardId && x.Id != exceptId &&

			(!rootId.HasValue || x.RevisionRootId != rootId.Value) && x.Name.ToUpper() == normalized))

			throw new BusinessException(FeeStructureErrorCodes.DuplicateName);

	}



	private async Task CheckAttachmentsAsync(StandardFeeStructureRequest input, FeeStructure? old)

	{

		if (input.CollectionPolicyId != old?.CollectionPolicyId || input.CollectionPolicyVersionId != old?.CollectionPolicyVersionId)

			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Add);

		if (input.RefundPolicyId != old?.RefundPolicyId || input.RefundPolicyVersionId != old?.RefundPolicyVersionId)

			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.Add);


	}



	private void ApplyCandidate(FeeStructure entity, StandardFeeStructureRequest input)

	{

		entity.ChangeName(input.Name);

		entity.AttachCollectionPolicy(input.CollectionPolicyId, input.CollectionPolicyVersionId);

		// Concession policy is selected and version-pinned by the concession request,
		// never by the fee structure.
		entity.AttachConcessionPolicy(
			null, null, ConcessionFeeHeadScope.EntireFeeStructure, System.Array.Empty<Guid>());

		entity.AttachRefundPolicy(input.RefundPolicyId, input.RefundPolicyVersionId);


		foreach (var deleted in entity.FeeStructureDetails.Where(x => input.FeeHeads.All(y => y.FeeHeadId != x.FeeHeadId)).ToList())

			entity.RemoveDetail(deleted);

		foreach (var line in input.FeeHeads)

		{

			var detail = entity.FeeStructureDetails.SingleOrDefault(x => x.FeeHeadId == line.FeeHeadId);

			if (detail == null)

			{

				detail = new FeeStructureDetail(GuidGenerator.Create(), CurrentTenant.Id, entity.Id, line.FeeHeadId,

					line.Amount, line.IsOneTime, line.IsOptional, line.PayableMonth, line.PayableYear, line.IsVariable);

				entity.AddDetail(detail);

			}

			else detail.UpdateConfiguration(line.Amount, line.IsOneTime, line.IsOptional, line.PayableMonth, line.PayableYear, line.IsVariable);

			AttachSchedule(detail, line);

		}

	}



	private void AttachSchedule(FeeStructureDetail detail, FeeHeadAssignmentRequest line)

	{

		if (detail.PaymentTermId == line.PaymentTermId && detail.PaymentTermVersionId == line.PaymentTermVersionId &&

			detail.ScheduleMode == line.ScheduleMode && JsonSerializer.Serialize(detail.ScheduleOverrides.OrderBy(x => x.Sequence).Select(MapOverride)) ==

			JsonSerializer.Serialize(line.InstallmentOverrides.OrderBy(x => x.Sequence))) return;

		if (!line.PaymentTermId.HasValue || !line.PaymentTermVersionId.HasValue) { detail.DetachPaymentTerm(); return; }

		if (line.ScheduleMode == ScheduleMode.Inherited)

			detail.AttachPaymentTerm(line.PaymentTermId.Value, line.PaymentTermVersionId.Value);

		else detail.AttachPaymentTermWithOverrides(line.PaymentTermId.Value, line.PaymentTermVersionId.Value,

			line.InstallmentOverrides.Select(x => new FeeStructureDetailScheduleOverride(GuidGenerator.Create(), CurrentTenant.Id,

				detail.Id, x.SourcePaymentTermInstallmentId, x.Sequence, x.Name, x.CoverageStartDate, x.CoverageEndDate,

				x.DemandDate, x.DueDate, x.AllocationPercentage)).ToList());

	}



	private static ScheduleOverrideDto MapOverride(FeeStructureDetailScheduleOverride x) => new()

	{

		SourcePaymentTermInstallmentId = x.SourcePaymentTermInstallmentId, Sequence = x.Sequence, Name = x.Name,

		CoverageStartDate = x.CoverageStartDate, CoverageEndDate = x.CoverageEndDate, DemandDate = x.DemandDate,

		DueDate = x.DueDate, AllocationPercentage = x.AllocationPercentage

	};



	private static StandardFeeStructureRequest ToRequest(FeeStructure entity) => new()

	{

		Name = entity.Name, ConcurrencyStamp = entity.ConcurrencyStamp, ReceiptBookId = entity.ReceiptBookId,

		CollectionPolicyId = entity.CollectionPolicyId, CollectionPolicyVersionId = entity.CollectionPolicyVersionId,

		ConcessionPolicyId = entity.ConcessionPolicyId, ConcessionPolicyVersionId = entity.ConcessionPolicyVersionId,

		RefundPolicyId = entity.RefundPolicyId, RefundPolicyVersionId = entity.RefundPolicyVersionId,


		ConcessionFeeHeadScope = entity.ConcessionFeeHeadScope, ConcessionApplicableFeeHeadIds = entity.GetConcessionApplicableFeeHeadIds().ToList(),

		FeeHeads = entity.FeeStructureDetails.Select(x => new FeeHeadAssignmentRequest

		{

			Id = x.Id, FeeHeadId = x.FeeHeadId, Amount = x.Amount, IsVariable = x.IsVariable, IsOneTime = x.IsOneTime, IsOptional = x.IsOptional,

			PayableMonth = x.PayableMonth ?? 0, PayableYear = x.PayableYear ?? 0, PaymentTermId = x.PaymentTermId,

			PaymentTermVersionId = x.PaymentTermVersionId, ScheduleMode = x.ScheduleMode,

			InstallmentOverrides = x.ScheduleOverrides.Select(MapOverride).ToList()

		}).ToList()

	};



	private async Task<FeeStructureCreateResponse> MapAsync(FeeStructure entity)

	{

		var snapshotJson = entity.GetProperty<string>("PublicationReview");

		var frozen = string.IsNullOrEmpty(snapshotJson) ? null : JsonSerializer.Deserialize<FeeStructurePreviewDto>(snapshotJson);

		// Project retired setup fields out of the response without rewriting stored publication evidence.
		if (frozen != null)
		{
			var hadProration = frozen.PolicyLabels.Remove("proration");
			frozen.Warnings.RemoveAll(x => x.Contains("proration", StringComparison.OrdinalIgnoreCase) ||
				(hadProration && x.Contains("policy Fee Head(s) are absent", StringComparison.Ordinal)));
		}
		var result = new FeeStructureCreateResponse

		{

			PublicationReview = frozen,

			Id = entity.Id, Name = entity.Name, Status = entity.IsLocked ? FeeStructureStatus.Locked : FeeStructureStatus.Unlocked,

			ConcurrencyStamp = entity.ConcurrencyStamp, RevisionRootId = entity.RevisionRootId, PreviousRevisionId = entity.PreviousRevisionId,

			RevisionNumber = entity.RevisionNumber, ReceiptBookId = entity.ReceiptBookId, CreationTime = entity.CreationTime, LastModificationTime = entity.LastModificationTime,

			CollectionPolicyId = entity.CollectionPolicyId, CollectionPolicyVersionId = entity.CollectionPolicyVersionId,

			ConcessionPolicyId = entity.ConcessionPolicyId, ConcessionPolicyVersionId = entity.ConcessionPolicyVersionId,

			RefundPolicyId = entity.RefundPolicyId, RefundPolicyVersionId = entity.RefundPolicyVersionId,


			ConcessionFeeHeadScope = entity.ConcessionFeeHeadScope, ConcessionApplicableFeeHeadIds = entity.GetConcessionApplicableFeeHeadIds().ToList(), FeeHeads = []

		};

		var policyIds = new[] { entity.CollectionPolicyVersionId, entity.ConcessionPolicyVersionId, entity.RefundPolicyVersionId }.Where(x => x.HasValue).Select(x => x!.Value).ToList();

		result.PolicyLabels = frozen?.PolicyLabels ?? (await financePolicyVersionRepository.GetListAsync(x => policyIds.Contains(x.Id)))

			.ToDictionary(x => x.PolicyType, x => $"{x.PolicyName} · v{x.VersionNumber}");

		var ids = entity.FeeStructureDetails.Select(x => x.FeeHeadId).ToList();

		var heads = (await feeHeadRepository.GetListAsync(x => ids.Contains(x.Id))).ToDictionary(x => x.Id);

		var termIds = entity.FeeStructureDetails.Where(x => x.PaymentTermId.HasValue).Select(x => x.PaymentTermId!.Value).Distinct().ToList();

		var terms = (await paymentTermRepository.GetListAsync(x => termIds.Contains(x.Id))).ToDictionary(x => x.Id);

		foreach (var line in entity.FeeStructureDetails)

		{

			heads.TryGetValue(line.FeeHeadId, out var head);

			var term = line.PaymentTermId.HasValue ? terms.GetValueOrDefault(line.PaymentTermId.Value) : null;

			var version = term?.Versions.SingleOrDefault(x => x.Id == line.PaymentTermVersionId);

			result.FeeHeads.Add(new FeeHeadAssignmentResponse

			{

				Id = line.Id, FeeHeadId = line.FeeHeadId, FeeHeadName = frozen?.Coverage.SingleOrDefault(x => x.FeeHeadId == line.FeeHeadId)?.Name ?? head?.Name ?? "Unavailable fee head", CategoryName = head?.FeeCategory?.Name ?? "",

				Amount = line.Amount, IsVariable = line.IsVariable, IsOneTime = line.IsOneTime, IsOptional = line.IsOptional, PayableMonth = line.PayableMonth, PayableYear = line.PayableYear,

				PaymentTermId = line.PaymentTermId, PaymentTermVersionId = line.PaymentTermVersionId, PaymentTermName = frozen?.Schedules.SingleOrDefault(x => x.FeeHeadId == line.FeeHeadId)?.PaymentTermName ?? term?.Name,

				PaymentTermVersionNumber = version?.VersionNumber, HasNewVersionAvailable = term?.GetPublishedVersion() is { } latest && version != null && latest.VersionNumber > version.VersionNumber,

				ScheduleMode = line.ScheduleMode, IsScheduleLocked = entity.IsLocked, InstallmentOverrides = line.ScheduleOverrides.Select(MapOverride).ToList()

			});

		}

		return result;

	}

}

