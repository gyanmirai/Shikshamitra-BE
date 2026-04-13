using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.ConcessionRequests.Dtos;
using ShikshaMitra.Finance.Setting;
using ShikshaMitra.Finance.Setting.ConcessionType;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Students;

namespace ShikshaMitra.Finance.ConcessionRequests;

[RemoteService(false)]
public class ConcessionRequestAppService(
    IConcessionRequestRepository concessionRequestRepository,
    IRepository<ConcessionType, Guid> concessionTypeRepository,
    IRepository<Student, Guid> studentRepository,
    IRepository<FeeHead, Guid> feeHeadRepository,
    ConcessionRequestManager concessionRequestManager)
    : ShikshaMitraAppService, IConcessionRequestAppService
{
    // -------------------------------------------------------------------------
    // Queries
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the anchor request enriched with the student's enrollment context
    /// and all sibling requests (same student, same status) as
    /// <c>selectedConcessionTypes</c>.
    /// </summary>
    public virtual async Task<ApiDataResponseDto<ConcessionRequestDetailDto>> GetAsync(Guid id)
    {
        // 1. Load the anchor request
        var anchor = await concessionRequestRepository.GetAsync(id);

        // 2. Load the student to source the enrollment context fields
        var student = await studentRepository.GetAsync(anchor.StudentId);

        // 3. Load all requests for this student that share the anchor's status
        //    — they form the logical "batch" that was created / updated together.
        var siblingRequests = await concessionRequestRepository.GetListAsync(
            skipCount: 0,
            maxResultCount: int.MaxValue,
            studentId: anchor.StudentId,
            status: anchor.Status);

        // 4. Build the aggregated response
        var detail = new ConcessionRequestDetailDto
        {
            ConcessionRequestId   = anchor.Id,
            BoardId               = student.AcademicBoardId,
            EducationMediumId     = student.AcademicMediumId,
            StandardId            = student.StandardId,
            DivisionId            = student.DivisionId,
            StudentId             = anchor.StudentId,
            Status                = ConcessionRequestDetailDto.MapStatus(anchor.Status),
            CreatedAt             = anchor.CreationTime,
            UpdatedAt             = anchor.LastModificationTime ?? anchor.CreationTime,
            SelectedConcessionTypes = siblingRequests
                .Select(r => new SelectedConcessionTypeDto
                {
                    ConcessionTypeId                 = r.ConcessionTypeId,
                    ConcessionRequestConcessionTypeId = r.Id,
                })
                .ToList(),
        };

        return ApiDataResponseDto<ConcessionRequestDetailDto>.SuccessResult(detail);
    }

    public virtual async Task<PagedResultDto<ConcessionRequestDto>> GetListAsync(
        GetConcessionRequestListRequestDto input)
    {
        var totalCount = await concessionRequestRepository.GetCountAsync(
            studentId: input.StudentId,
            concessionTypeId: input.ConcessionTypeId,
            status: input.Status,
            filter: input.Filter);

        var list = await concessionRequestRepository.GetListAsync(
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount,
            sorting: input.Sorting,
            studentId: input.StudentId,
            concessionTypeId: input.ConcessionTypeId,
            status: input.Status,
            filter: input.Filter);

        return new PagedResultDto<ConcessionRequestDto>(
            totalCount,
            ObjectMapper.Map<List<ConcessionRequest>, List<ConcessionRequestDto>>(list));
    }

    /// <summary>
    /// Step 3 (Edit / Resume) — Returns saved fee-head selections for the full
    /// concession-type batch identified by the anchor request.
    ///
    /// <para>
    /// All requests sharing the anchor's student and lifecycle status are included so
    /// that the UI receives one entry per concession type regardless of whether fee
    /// heads have been saved yet. Types with no mappings return an empty list, which
    /// correctly leaves those checkboxes unchecked on resume.
    /// </para>
    /// </summary>
    public virtual async Task<ApiDataResponseDto<ConcessionRequestFeeHeadsDto>> GetFeeHeadsAsync(
        Guid concessionRequestId)
    {
        // 1. Load the anchor to get the student and status that define this batch
        var anchor = await concessionRequestRepository.GetAsync(concessionRequestId);

        // 2. Single query: all requests for this student at this status, with mappings
        var batchRequests = await concessionRequestRepository
            .GetBatchWithMappingsAsync(anchor.StudentId, anchor.Status);

        // 3. Bulk-load concession-type metadata so we can attach names and discount types
        var concessionTypeIds = batchRequests.Select(r => r.ConcessionTypeId).ToList();
        var concessionTypes = await concessionTypeRepository
            .GetListAsync(ct => concessionTypeIds.Contains(ct.Id));

        var concessionTypeById = concessionTypes.ToDictionary(ct => ct.Id);

        // 4. Build the per-type selection entries
        var typeEntries = batchRequests
            .Select(r =>
            {
                concessionTypeById.TryGetValue(r.ConcessionTypeId, out var ct);
                return new FeeHeadSelectionByTypeDto
                {
                    ConcessionTypeId = r.ConcessionTypeId,
                    ConcessionName   = ct?.Name ?? string.Empty,
                    DiscountType     = MapDiscountType(ct?.DiscountType),
                    FeeHeadIds       = r.ConcessionFeeHeadMappings
                                        .Select(m => m.FeeHeadId)
                                        .ToList(),
                };
            })
            .ToList();

        var dto = new ConcessionRequestFeeHeadsDto
        {
            ConcessionRequestId = anchor.Id,
            StudentId           = anchor.StudentId,
            ConcessionTypes     = typeEntries,
        };

        return ApiDataResponseDto<ConcessionRequestFeeHeadsDto>.SuccessResult(dto);
    }

    /// <summary>
    /// Step 4 (Edit / Resume) — Returns the previously saved discount values
    /// per concession type per fee head so the UI can pre-populate the input fields.
    ///
    /// <para>
    /// Only the stored <c>UserSpecifiedConcessionAmountValue</c>, <c>basedOn</c>, and
    /// <c>PostConcessionFeeHeadAmount</c> (netAmount) are returned. Fee head names and
    /// base amounts are loaded separately by the UI via <c>useGetStandardFeeStructureQuery</c>.
    /// </para>
    /// </summary>
    public virtual async Task<ApiDataResponseDto<ConcessionRequestValueEntriesDto>> GetValueEntriesAsync(
        Guid concessionRequestId)
    {
        // 1. Load the anchor to resolve studentId and status that identify the batch
        var anchor = await concessionRequestRepository.GetAsync(concessionRequestId);

        // 2. Single query: all requests for this student at this status, with mappings
        var batchRequests = await concessionRequestRepository
            .GetBatchWithMappingsAsync(anchor.StudentId, anchor.Status);

        // 3. Bulk-load concession-type metadata for display names
        var concessionTypeIds = batchRequests.Select(r => r.ConcessionTypeId).ToList();
        var concessionTypes = await concessionTypeRepository
            .GetListAsync(ct => concessionTypeIds.Contains(ct.Id));

        var concessionTypeById = concessionTypes.ToDictionary(ct => ct.Id);

        // 4. Build per-type value entries from stored mapping data
        var typeEntries = batchRequests
            .Select(r =>
            {
                concessionTypeById.TryGetValue(r.ConcessionTypeId, out var ct);

                return new SavedValueEntryByTypeDto
                {
                    ConcessionTypeId = r.ConcessionTypeId,
                    ConcessionName   = ct?.Name ?? string.Empty,
                    FeeHeadValues    = r.ConcessionFeeHeadMappings
                        .Select(m => new SavedFeeHeadValueDto
                        {
                            FeeHeadId = m.FeeHeadId,
                            BasedOn   = MapToBasedOnInt(m.UserSpecifiedConcessionAmountType),
                            Amount    = m.UserSpecifiedConcessionAmountValue,
                            NetAmount = m.PostConcessionFeeHeadAmount,
                        })
                        .ToList(),
                };
            })
            .ToList();

        var dto = new ConcessionRequestValueEntriesDto
        {
            ConcessionRequestId  = anchor.Id,
            StudentId            = anchor.StudentId,
            ConcessionTypeValues = typeEntries,
        };

        return ApiDataResponseDto<ConcessionRequestValueEntriesDto>.SuccessResult(dto);
    }

    // -------------------------------------------------------------------------
    // Commands
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates one <see cref="ConcessionRequest"/> per concession-type ID (Step 1 + Step 2).
    /// Validates that the student exists and is enrolled under the supplied
    /// board / medium / standard / division before persisting any records.
    /// </summary>
    public async Task CreateAsync(CreateConcessionRequestDto input)
    {
        var student = await studentRepository.GetAsync(input.StudentId);

        ValidateStudentEnrollmentContext(student,
            input.BoardId, input.EducationMediumId, input.StandardId, input.DivisionId);

        await EnsureAllConcessionTypesExistAsync(input.ConcessionTypeIds);

        await concessionRequestManager.CreateAsync(
            CurrentTenant.Id,
            input.StudentId,
            input.ConcessionTypeIds);
    }

    /// <summary>
    /// Replaces the student's Pending concession-type selection in full.
    ///
    /// <para>
    /// The <paramref name="id"/> (path parameter) is the ownership anchor — the
    /// concession request identified by it must be in <b>Pending</b> status and must
    /// belong to the student specified in <paramref name="input"/>.
    /// </para>
    /// <para>
    /// After validation passes, the manager performs a batch replace:
    /// Pending requests for types removed from the list are deleted; new requests are
    /// created for types added to the list.
    /// </para>
    /// </summary>
    public virtual async Task<ConcessionRequestDto> UpdateAsync(Guid id, UpdateConcessionRequestDto input)
    {
        // 1. Load the anchor and verify it is Pending
        var anchor = await concessionRequestRepository.GetAsync(id);

        if (anchor.Status != ConcessionRequestStatus.Pending)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.CannotUpdateNonPending)
                .WithData("RequestId", id)
                .WithData("Status", anchor.Status.ToString());
        }

        // 2. Verify the body's studentId matches the anchor's student (ownership check)
        if (anchor.StudentId != input.StudentId)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.StudentRequestOwnershipMismatch)
                .WithData("RequestId", id)
                .WithData("AnchorStudentId", anchor.StudentId)
                .WithData("InputStudentId", input.StudentId);
        }

        // 3. Validate student exists and enrollment context is consistent
        var student = await studentRepository.GetAsync(input.StudentId);

        ValidateStudentEnrollmentContext(student,
            input.BoardId, input.EducationMediumId, input.StandardId, input.DivisionId);

        // 4. Validate all requested concession types exist before touching any data
        await EnsureAllConcessionTypesExistAsync(input.ConcessionTypeIds);

        // 5. Delegate batch-replace to the domain manager
        var result = await concessionRequestManager.UpdateAsync(
            CurrentTenant.Id,
            input.StudentId,
            input.ConcessionTypeIds);

        return ObjectMapper.Map<ConcessionRequest, ConcessionRequestDto>(result);
    }

    public virtual async Task<ConcessionRequestDto> SubmitAsync(Guid id)
    {
        var entity = await concessionRequestRepository.GetWithMappingsAsync(id);
        entity = await concessionRequestManager.SubmitAsync(entity);
        return ObjectMapper.Map<ConcessionRequest, ConcessionRequestDto>(entity);
    }

    public virtual async Task<ConcessionRequestDto> ApproveAsync(Guid id)
    {
        var entity = await concessionRequestRepository.GetAsync(id);
        entity = await concessionRequestManager.ApproveAsync(entity);
        return ObjectMapper.Map<ConcessionRequest, ConcessionRequestDto>(entity);
    }

    public virtual async Task<ConcessionRequestDto> RejectAsync(Guid id, RejectConcessionRequestDto input)
    {
        var entity = await concessionRequestRepository.GetAsync(id);
        entity = await concessionRequestManager.RejectAsync(entity, input.RejectionReason);
        return ObjectMapper.Map<ConcessionRequest, ConcessionRequestDto>(entity);
    }

    /// <summary>
    /// Step 3 — Saves fee-head selections for all concession types in the student's
    /// Draft batch. Full replacement per concession type.
    /// </summary>
    public async Task SaveFeeHeadsDataAsync(Guid concessionRequestId, SaveFeeHeadsDataDto input)
    {
        // 1. Verify the anchor request exists and is still Draft
        var anchor = await concessionRequestRepository.GetAsync(concessionRequestId);

        if (anchor.Status != ConcessionRequestStatus.Draft)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.CannotUpdateNonDraft)
                .WithData("RequestId", concessionRequestId)
                .WithData("Status", anchor.Status.ToString());
        }

        // 2. Guard against duplicate fee heads within the same concession type
        foreach (var item in input.FeeHeadsData)
        {
            var duplicateId = item.SelectedFeeHeadIds
                .GroupBy(id => id)
                .FirstOrDefault(g => g.Count() > 1)?.Key;

            if (duplicateId.HasValue)
            {
                throw new BusinessException(ConcessionRequestErrorCodes.DuplicateFeeHeadInConcessionType)
                    .WithData("ConcessionTypeId", item.ConcessionTypeId)
                    .WithData("FeeHeadId", duplicateId.Value);
            }
        }

        // 3. Validate every referenced fee head exists (validate up-front so the
        //    operation is all-or-nothing before any data is written)
        var distinctFeeHeadIds = input.FeeHeadsData
            .SelectMany(i => i.SelectedFeeHeadIds)
            .Distinct();

        foreach (var feeHeadId in distinctFeeHeadIds)
        {
            if (!await feeHeadRepository.AnyAsync(f => f.Id == feeHeadId))
            {
                throw new BusinessException(ConcessionRequestErrorCodes.FeeHeadNotFound)
                    .WithData("FeeHeadId", feeHeadId);
            }
        }

        // 4. Delegate the multi-entity batch update to the domain manager
        var domainItems = input.FeeHeadsData
            .Select(dto => new ConcessionTypeFeeHeadsInput(
                dto.ConcessionTypeId,
                dto.SelectedFeeHeadIds))
            .ToList();

        await concessionRequestManager.SaveFeeHeadsDataAsync(
            CurrentTenant.Id,
            anchor.StudentId,
            domainItems);
    }

    /// <summary>
    /// Step 4 — Persists discount values for every fee head in the student's Draft batch.
    /// </summary>
    public async Task SaveValueEntryAsync(Guid concessionRequestId, ValueEntryDto input)
    {
        // 1. Load anchor — provides studentId and current status
        var anchor = await concessionRequestRepository.GetAsync(concessionRequestId);

        if (anchor.Status != ConcessionRequestStatus.Draft)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.CannotUpdateNonDraft)
                .WithData("RequestId", concessionRequestId)
                .WithData("Status", anchor.Status.ToString());
        }

        // 2. Validate: no duplicate fee heads within a single concession type block
        foreach (var block in input.ConcessionTypeValues)
        {
            var duplicateId = block.FeeHeadValues
                .GroupBy(fv => fv.FeeHeadId)
                .FirstOrDefault(g => g.Count() > 1)?.Key;

            if (duplicateId.HasValue)
            {
                throw new BusinessException(ConcessionRequestErrorCodes.DuplicateFeeHeadInConcessionType)
                    .WithData("ConcessionTypeId", block.ConcessionTypeId)
                    .WithData("FeeHeadId", duplicateId.Value);
            }
        }

        // 3. Validate: no duplicate concession types across blocks
        var duplicateConcessionTypeId = input.ConcessionTypeValues
            .GroupBy(b => b.ConcessionTypeId)
            .FirstOrDefault(g => g.Count() > 1)?.Key;

        if (duplicateConcessionTypeId.HasValue)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.DuplicateFeeHeadInConcessionType)
                .WithData("ConcessionTypeId", duplicateConcessionTypeId.Value);
        }

        // 4. Validate per-entry business rules (all-or-nothing before any writes)
        foreach (var block in input.ConcessionTypeValues)
        {
            foreach (var fv in block.FeeHeadValues)
            {
                var amountType = MapBasedOn(fv.BasedOn);

                if (amountType == AmountType.Percentage && fv.Amount > 100m)
                {
                    throw new BusinessException(ConcessionRequestErrorCodes.InvalidPercentageAmount)
                        .WithData("FeeHeadId", fv.FeeHeadId)
                        .WithData("ConcessionTypeId", block.ConcessionTypeId)
                        .WithData("Amount", fv.Amount);
                }

                if (amountType == AmountType.Flat && fv.Amount > fv.AnnualBaseAmount)
                {
                    throw new BusinessException(ConcessionRequestErrorCodes.ExcessiveDiscountAmount)
                        .WithData("FeeHeadId", fv.FeeHeadId)
                        .WithData("ConcessionTypeId", block.ConcessionTypeId)
                        .WithData("Amount", fv.Amount)
                        .WithData("AnnualBaseAmount", fv.AnnualBaseAmount);
                }
            }
        }

        // 5. Validate all concession types from the batch are present in the payload
        var batchRequests = await concessionRequestRepository
            .GetBatchWithMappingsAsync(anchor.StudentId, anchor.Status);

        var inputTypeIds = input.ConcessionTypeValues
            .Select(b => b.ConcessionTypeId)
            .ToHashSet();

        foreach (var request in batchRequests)
        {
            if (!inputTypeIds.Contains(request.ConcessionTypeId))
            {
                throw new BusinessException(ConcessionRequestErrorCodes.ConcessionTypeMissingFromValueEntry)
                    .WithData("ConcessionTypeId", request.ConcessionTypeId);
            }
        }

        // 6. Build domain input and delegate to manager
        var domainItems = input.ConcessionTypeValues
            .Select(block => new ConcessionTypeValueEntryInput(
                block.ConcessionTypeId,
                block.FeeHeadValues
                    .Select(fv => new FeeHeadValueEntryInput(
                        fv.FeeHeadId,
                        fv.AnnualBaseAmount,
                        fv.Amount,
                        MapBasedOn(fv.BasedOn)))
                    .ToList()))
            .ToList();

        await concessionRequestManager.SaveValueEntryAsync(
            anchor.StudentId,
            anchor.Status,
            domainItems);
    }

    /// <summary>
    /// Step 5 (Edit / Resume) — Returns all document template slots per concession type,
    /// merged with previously saved upload data.
    ///
    /// <para>
    /// Document template data (<c>documentName</c>, <c>acceptedFormat</c>) is sourced from
    /// <c>ConcessionType.ConcessionTypeDocuments</c>, which is auto-included by EF Core.
    /// Upload state (<c>url</c>, <c>status</c>, <c>uploadedAt</c>) is sourced from
    /// <c>ConcessionRequestDocument</c> records saved in Step 5 PUT.
    /// Slots with no saved upload return url=null, status=0, uploadedAt=null.
    /// </para>
    /// </summary>
    public virtual async Task<ApiDataResponseDto<ConcessionRequestDocumentsDto>> GetDocumentUploadsAsync(
        Guid concessionRequestId)
    {
        // 1. Load anchor → studentId + status to identify the batch
        var anchor = await concessionRequestRepository.GetAsync(concessionRequestId);

        // 2. Single query: all requests in the batch with saved document uploads
        var batchRequests = await concessionRequestRepository
            .GetBatchWithDocumentsAsync(anchor.StudentId, anchor.Status);

        // 3. Load concession-type templates (ConcessionTypeDocuments + AcceptedFormats
        //    are pulled automatically via AutoInclude in EF Core configuration)
        var concessionTypeIds = batchRequests.Select(r => r.ConcessionTypeId).ToList();
        var concessionTypes = await concessionTypeRepository
            .GetListAsync(ct => concessionTypeIds.Contains(ct.Id));

        var concessionTypeById = concessionTypes.ToDictionary(ct => ct.Id);

        // 4. Build the merged per-type document list
        var typeDocuments = batchRequests
            .Select(r =>
            {
                concessionTypeById.TryGetValue(r.ConcessionTypeId, out var ct);

                // Index uploaded documents by template slot for O(1) lookup
                var uploadByDocumentId = r.ConcessionRequestDocuments
                    .ToDictionary(d => d.DocumentId);

                // Merge template slots with saved upload state
                var slots = (ct?.ConcessionTypeDocuments ?? Enumerable.Empty<ConcessionTypeDocument>())
                    .Select(template =>
                    {
                        uploadByDocumentId.TryGetValue(template.Id, out var upload);

                        return new DocumentSlotDto
                        {
                            DocumentId     = template.Id,
                            DocumentName   = template.DocumentName,
                            AcceptedFormat = template.AcceptedFormats
                                                .Select(f => (int)f.DocumentType)
                                                .ToList(),
                            Url            = upload?.Url,
                            Status         = upload is not null ? (int)upload.Status : 0,
                            UploadedAt     = upload is not null
                                             ? upload.LastModificationTime ?? upload.CreationTime
                                             : null,
                        };
                    })
                    .ToList();

                return new DocumentsByTypeDto
                {
                    ConcessionTypeId   = r.ConcessionTypeId,
                    ConcessionTypeName = ct?.Name ?? string.Empty,
                    Documents          = slots,
                };
            })
            .ToList();

        var dto = new ConcessionRequestDocumentsDto
        {
            ConcessionRequestId      = anchor.Id,
            StudentId                = anchor.StudentId,
            ConcessionTypeDocuments  = typeDocuments,
        };

        return ApiDataResponseDto<ConcessionRequestDocumentsDto>.SuccessResult(dto);
    }

    /// <summary>
    /// Step 5 — Persists document uploads for every concession type in the student's
    /// Draft batch. Full replacement per concession type.
    /// </summary>
    public async Task SaveDocumentUploadAsync(Guid concessionRequestId, UploadDocumentsDto input)
    {
        // 1. Load anchor — establishes studentId, status, and Draft guard
        var anchor = await concessionRequestRepository.GetAsync(concessionRequestId);

        if (anchor.Status != ConcessionRequestStatus.Draft)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.CannotUploadDocumentsForNonDraft)
                .WithData("RequestId", concessionRequestId)
                .WithData("Status", anchor.Status.ToString());
        }

        // 2. Guard against duplicate documentIds within the same concession type block
        foreach (var block in input.ConcessionTypeDocuments)
        {
            var duplicateId = block.Documents
                .GroupBy(d => d.DocumentId)
                .FirstOrDefault(g => g.Count() > 1)?.Key;

            if (duplicateId.HasValue)
            {
                throw new BusinessException(ConcessionRequestErrorCodes.DuplicateDocumentInConcessionType)
                    .WithData("ConcessionTypeId", block.ConcessionTypeId)
                    .WithData("DocumentId", duplicateId.Value);
            }
        }

        // 3. Validate all concession types in the student's batch are present in the payload
        var batchRequests = await concessionRequestRepository
            .GetBatchWithDocumentsAsync(anchor.StudentId, anchor.Status);

        var inputTypeIds = input.ConcessionTypeDocuments
            .Select(b => b.ConcessionTypeId)
            .ToHashSet();

        foreach (var request in batchRequests)
        {
            if (!inputTypeIds.Contains(request.ConcessionTypeId))
            {
                throw new BusinessException(ConcessionRequestErrorCodes.ConcessionTypeMissingFromDocumentUpload)
                    .WithData("ConcessionTypeId", request.ConcessionTypeId);
            }
        }

        // 4. Build domain input and delegate to manager
        var domainItems = input.ConcessionTypeDocuments
            .Select(block => new ConcessionTypeDocumentUploadInput(
                block.ConcessionTypeId,
                block.Documents
                    .Select(d => new DocumentUploadInput(
                        d.DocumentId,
                        d.Url,
                        (DocumentUploadStatus)d.Status))
                    .ToList()))
            .ToList();

        await concessionRequestManager.SaveDocumentUploadAsync(
            CurrentTenant.Id,
            anchor.StudentId,
            anchor.Status,
            domainItems);
    }

    /// <summary>
    /// Step 6 — Saves an optional free-text description against a Draft request.
    /// </summary>
    public async Task SaveDescriptionAsync(Guid concessionRequestId, SaveDescriptionDto input)
    {
        var entity = await concessionRequestRepository.GetAsync(concessionRequestId);
        entity.SetDescription(input.Description);
        await concessionRequestRepository.UpdateAsync(entity);
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        var entity = await concessionRequestRepository.GetAsync(id);

        if (entity.Status != ConcessionRequestStatus.Draft)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.CannotDeleteNonDraft)
                .WithData("Status", entity.Status.ToString());
        }

        await concessionRequestRepository.DeleteAsync(id);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Guards against stale UI state: verifies that the four enrollment context
    /// identifiers supplied by the caller match what is on the student record.
    /// This is intentionally static — it has no side effects and no I/O.
    /// </summary>
    private static void ValidateStudentEnrollmentContext(
        Student student,
        Guid boardId,
        Guid educationMediumId,
        Guid standardId,
        Guid divisionId)
    {
        if (student.AcademicBoardId != boardId)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.StudentBoardMismatch)
                .WithData("StudentId", student.Id)
                .WithData("ExpectedBoardId", boardId)
                .WithData("ActualBoardId", student.AcademicBoardId);
        }

        if (student.AcademicMediumId != educationMediumId)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.StudentMediumMismatch)
                .WithData("StudentId", student.Id)
                .WithData("ExpectedMediumId", educationMediumId)
                .WithData("ActualMediumId", student.AcademicMediumId);
        }

        if (student.StandardId != standardId)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.StudentStandardMismatch)
                .WithData("StudentId", student.Id)
                .WithData("ExpectedStandardId", standardId)
                .WithData("ActualStandardId", student.StandardId);
        }

        if (student.DivisionId != divisionId)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.StudentDivisionMismatch)
                .WithData("StudentId", student.Id)
                .WithData("ExpectedDivisionId", divisionId)
                .WithData("ActualDivisionId", student.DivisionId);
        }
    }

    /// <summary>
    /// Validates all concession-type IDs up front so the operation either
    /// succeeds entirely or fails before any data is written.
    /// </summary>
    private async Task EnsureAllConcessionTypesExistAsync(IEnumerable<Guid> typeIds)
    {
        foreach (var typeId in typeIds)
        {
            var exists = await concessionTypeRepository.AnyAsync(t => t.Id == typeId);
            if (!exists)
            {
                throw new BusinessException(ConcessionRequestErrorCodes.ConcessionTypeNotFound)
                    .WithData("ConcessionTypeId", typeId);
            }
        }
    }

    /// <summary>
    /// Maps the domain <see cref="AmountType"/> enum to the uppercase string values
    /// defined by the API specification ("PERCENTAGE" / "FLAT" / <c>null</c>).
    /// </summary>
    private static string? MapDiscountType(AmountType? discountType) => discountType switch
    {
        AmountType.Percentage => "PERCENTAGE",
        AmountType.Flat       => "FLAT",
        _                     => null,
    };

    /// <summary>
    /// Maps the Step 4 <c>basedOn</c> integer discriminator to the domain
    /// <see cref="AmountType"/> enum. 0 = AMOUNT_BASED (Flat), 1 = PERCENTAGE.
    /// </summary>
    private static AmountType MapBasedOn(int basedOn) => basedOn switch
    {
        0 => AmountType.Flat,
        1 => AmountType.Percentage,
        _ => throw new BusinessException(ConcessionRequestErrorCodes.InvalidBasedOnValue)
                 .WithData("BasedOn", basedOn),
    };

    /// <summary>
    /// Inverse of <see cref="MapBasedOn"/> — converts the domain <see cref="AmountType"/>
    /// back to the integer discriminator used in the Step 4 GET response.
    /// </summary>
    private static int MapToBasedOnInt(AmountType amountType) => amountType switch
    {
        AmountType.Flat       => 0,
        AmountType.Percentage => 1,
        _                     => 0,
    };
}
