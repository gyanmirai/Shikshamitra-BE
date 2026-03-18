using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using ShikshaMitra.Finance.ConcessionRequests.Dtos;
using ShikshaMitra.Security.Permissions;

namespace ShikshaMitra.Finance.ConcessionRequests;

[RemoteService(false)]
public class ConcessionRequestAppService : ShikshaMitraAppService, IConcessionRequestAppService
{
    private readonly IConcessionRequestRepository _concessionRequestRepository;
    private readonly ConcessionRequestManager _concessionRequestManager;

    public ConcessionRequestAppService(
        IConcessionRequestRepository concessionRequestRepository,
        ConcessionRequestManager concessionRequestManager)
    {
        _concessionRequestRepository = concessionRequestRepository;
        _concessionRequestManager = concessionRequestManager;
    }

    public virtual async Task<ConcessionRequestDto> GetAsync(Guid id)
    {
        var entity = await _concessionRequestRepository.GetWithMappingsAsync(id);
        return ObjectMapper.Map<ConcessionRequest, ConcessionRequestDto>(entity);
    }

    public virtual async Task<PagedResultDto<ConcessionRequestDto>> GetListAsync(
        GetConcessionRequestListRequestDto input)
    {
        var totalCount = await _concessionRequestRepository.GetCountAsync(
            studentId: input.StudentId,
            concessionTypeId: input.ConcessionTypeId,
            status: input.Status,
            filter: input.Filter);

        var list = await _concessionRequestRepository.GetListAsync(
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

    public virtual async Task<ConcessionRequestDto> CreateAsync(CreateConcessionRequestDto input)
    {
        var mappingInputs = input.FeeHeadMappings.Select(m => new ConcessionFeeHeadMappingInput(
            m.FeeHeadId,
            m.PreConcessionFeeHeadAmount,
            m.UserSpecifiedConcessionAmountValue,
            m.UserSpecifiedConcessionAmountType)).ToList();

        var entity = await _concessionRequestManager.CreateAsync(
            input.StudentId,
            input.ConcessionTypeId,
            mappingInputs,
            input.Remarks);

        return ObjectMapper.Map<ConcessionRequest, ConcessionRequestDto>(entity);
    }

    public virtual async Task<ConcessionRequestDto> UpdateAsync(Guid id, UpdateConcessionRequestDto input)
    {
        var entity = await _concessionRequestRepository.GetWithMappingsAsync(id);

        var mappingInputs = input.FeeHeadMappings.Select(m => new ConcessionFeeHeadMappingInput(
            m.FeeHeadId,
            m.PreConcessionFeeHeadAmount,
            m.UserSpecifiedConcessionAmountValue,
            m.UserSpecifiedConcessionAmountType)).ToList();

        entity = await _concessionRequestManager.UpdateAsync(
            entity,
            input.ConcessionTypeId,
            mappingInputs,
            input.Remarks);

        return ObjectMapper.Map<ConcessionRequest, ConcessionRequestDto>(entity);
    }

    public virtual async Task<ConcessionRequestDto> SubmitAsync(Guid id)
    {
        var entity = await _concessionRequestRepository.GetWithMappingsAsync(id);
        entity = await _concessionRequestManager.SubmitAsync(entity);
        return ObjectMapper.Map<ConcessionRequest, ConcessionRequestDto>(entity);
    }

    public virtual async Task<ConcessionRequestDto> ApproveAsync(Guid id)
    {
        var entity = await _concessionRequestRepository.GetAsync(id);
        entity = await _concessionRequestManager.ApproveAsync(entity);
        return ObjectMapper.Map<ConcessionRequest, ConcessionRequestDto>(entity);
    }

    public virtual async Task<ConcessionRequestDto> RejectAsync(Guid id, RejectConcessionRequestDto input)
    {
        var entity = await _concessionRequestRepository.GetAsync(id);
        entity = await _concessionRequestManager.RejectAsync(entity, input.RejectionReason);
        return ObjectMapper.Map<ConcessionRequest, ConcessionRequestDto>(entity);
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        var entity = await _concessionRequestRepository.GetAsync(id);

        if (entity.Status != ConcessionRequestStatus.Draft)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.CannotDeleteNonDraft)
                .WithData("Status", entity.Status.ToString());
        }

        await _concessionRequestRepository.DeleteAsync(id);
    }
}

