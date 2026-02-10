using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeStructure;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeStructure.Dtos;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeCategory.Dtos; // For MessageResponse
using ShikshaMitra.Finance.Setting.FeeStructure;
using ShikshaMitra.Finance.Setting.FeeHead;
using ShikshaMitra.Finance.Setting.FeeCategory;
using ShikshaMitra.Finance.Setting.ReceiptBook;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;

namespace ShikshaMitra.Application.Finance.Setting.FeeStructure;

[Authorize]
[RemoteService(false)]
public class FeeStructureAppService : ApplicationService, IFeeStructureAppService
{
    private readonly IRepository<ShikshaMitra.Finance.Setting.FeeStructure.FeeStructure, Guid> _feeStructureRepository;
    private readonly IRepository<FeeStructureStandard, Guid> _feeStructureStandardRepository;
    private readonly IRepository<FeeStructureDetail, Guid> _feeStructureDetailRepository;
    private readonly IRepository<Standard, Guid> _standardRepository;
    private readonly IRepository<ShikshaMitra.Finance.Setting.FeeHead.FeeHead, Guid> _feeHeadRepository;
    private readonly IRepository<ShikshaMitra.Finance.Setting.FeeCategory.FeeCategory, Guid> _feeCategoryRepository;
    private readonly IRepository<EducationBoard, Guid> _educationBoardRepository;
    private readonly IRepository<EducationMedium, Guid> _educationMediumRepository;
    private readonly IRepository<ShikshaMitra.Finance.Setting.ReceiptBook.ReceiptBook, Guid> _receiptBookRepository;
    private readonly IRepository<AcademicYear, Guid> _academicYearRepository;

    public FeeStructureAppService(
        IRepository<ShikshaMitra.Finance.Setting.FeeStructure.FeeStructure, Guid> feeStructureRepository,
        IRepository<FeeStructureStandard, Guid> feeStructureStandardRepository,
        IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
        IRepository<Standard, Guid> standardRepository,
        IRepository<ShikshaMitra.Finance.Setting.FeeHead.FeeHead, Guid> feeHeadRepository,
        IRepository<ShikshaMitra.Finance.Setting.FeeCategory.FeeCategory, Guid> feeCategoryRepository,
        IRepository<EducationBoard, Guid> educationBoardRepository,
        IRepository<EducationMedium, Guid> educationMediumRepository,
        IRepository<ShikshaMitra.Finance.Setting.ReceiptBook.ReceiptBook, Guid> receiptBookRepository,
        IRepository<AcademicYear, Guid> academicYearRepository)
    {
        _feeStructureRepository = feeStructureRepository;
        _feeStructureStandardRepository = feeStructureStandardRepository;
        _feeStructureDetailRepository = feeStructureDetailRepository;
        _standardRepository = standardRepository;
        _feeHeadRepository = feeHeadRepository;
        _feeCategoryRepository = feeCategoryRepository;
        _educationBoardRepository = educationBoardRepository;
        _educationMediumRepository = educationMediumRepository;
        _receiptBookRepository = receiptBookRepository;
        _academicYearRepository = academicYearRepository;
    }

    public async Task<FeeStructureCreateResponse> CreateAsync(Guid academicYearId, FeeStructureCreateRequest input)
    {
        var feeStructure = new ShikshaMitra.Finance.Setting.FeeStructure.FeeStructure(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            academicYearId,
            input.BoardId,
            input.MediumId,
            input.ReceiptBookId,
            input.Name
        );

        await _feeStructureRepository.InsertAsync(feeStructure);

        return new FeeStructureCreateResponse
        {
            Id = feeStructure.Id,
            Name = feeStructure.Name
        };
    }

    public async Task<FeeStructureListResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null)
    {
        var query = await _feeStructureRepository.GetQueryableAsync();
        
        query = query.Where(x => x.AcademicYearId == academicYearId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Name.Contains(search));
        }

        var totalCount = await _feeStructureRepository.AsyncExecuter.CountAsync(query);

        query = query.OrderByDescending(x => x.CreationTime)
                     .Skip((page - 1) * pageSize)
                     .Take(pageSize);

        var items = await _feeStructureRepository.AsyncExecuter.ToListAsync(query);

        // Fetch related data for names
        var academicYears = await _academicYearRepository.GetListAsync(x => x.Id == academicYearId);
        var academicYearName = academicYears.FirstOrDefault()?.Name;

        var boardIds = items.Select(x => x.BoardId).Distinct().ToList();
        var boards = await _educationBoardRepository.GetListAsync(x => boardIds.Contains(x.Id));
        var boardDict = boards.ToDictionary(x => x.Id, x => x.Type.ToString());
        
        var mediumIds = items.Select(x => x.MediumId).Distinct().ToList();
        var mediums = await _educationMediumRepository.GetListAsync(x => mediumIds.Contains(x.Id));
        var mediumDict = mediums.ToDictionary(x => x.Id, x => x.Name);

        var receiptBookIds = items.Select(x => x.ReceiptBookId).Distinct().ToList();
        var receiptBooks = await _receiptBookRepository.GetListAsync(x => receiptBookIds.Contains(x.Id));
        var receiptBookDict = receiptBooks.ToDictionary(x => x.Id, x => x.Name);

        var dtos = items.Select(x => new FeeStructureDetailsResponse
        {
            Id = x.Id,
            Name = x.Name,
            AcademicYearId = x.AcademicYearId,
            AcademicYearName = academicYearName,
            BoardId = x.BoardId,
            BoardName = boardDict.ContainsKey(x.BoardId) ? boardDict[x.BoardId] : null,
            MediumId = x.MediumId,
            MediumName = mediumDict.ContainsKey(x.MediumId) ? mediumDict[x.MediumId] : null,
            ReceiptBookId = x.ReceiptBookId,
            ReceiptBookName = receiptBookDict.ContainsKey(x.ReceiptBookId) ? receiptBookDict[x.ReceiptBookId] : null,
            Status = x.Status
        }).ToList();

        return new FeeStructureListResponse
        {
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = dtos
        };
    }

    public async Task<FeeStructureDetailsResponse> GetAsync(Guid feeStructureId)
    {
        var feeStructure = await _feeStructureRepository.GetAsync(feeStructureId);
        
        var academicYear = await _academicYearRepository.GetAsync(feeStructure.AcademicYearId);
        var board = await _educationBoardRepository.GetAsync(feeStructure.BoardId);
        var medium = await _educationMediumRepository.GetAsync(feeStructure.MediumId);
        var receiptBook = await _receiptBookRepository.GetAsync(feeStructure.ReceiptBookId);

        return new FeeStructureDetailsResponse
        {
            Id = feeStructure.Id,
            Name = feeStructure.Name,
            AcademicYearId = feeStructure.AcademicYearId,
            AcademicYearName = academicYear.Name,
            BoardId = feeStructure.BoardId,
            BoardName = board.Type.ToString(),
            MediumId = feeStructure.MediumId,
            MediumName = medium.Name,
            ReceiptBookId = feeStructure.ReceiptBookId,
            ReceiptBookName = receiptBook.Name,
            Status = feeStructure.Status
        };
    }

    public async Task<MessageResponse> UpdateStatusAsync(Guid feeStructureId, FeeStructureStatusUpdateRequest input)
    {
        var feeStructure = await _feeStructureRepository.GetAsync(feeStructureId);
        feeStructure.SetStatus(input.Status);
        await _feeStructureRepository.UpdateAsync(feeStructure);
        
        return new MessageResponse { Message = "Status updated successfully" };
    }

    public async Task<MessageResponse> SaveStandardFeeStructuresAsync(Guid feeStructureId, List<StandardFeeStructureSaveRequest> input)
    {
        var feeStructure = await _feeStructureRepository.GetAsync(feeStructureId);

        foreach (var standardRequest in input)
        {
            // 1. Check if FeeStructureStandard exists for this Standard in this FeeStructure
            // 1. Check if FeeStructureStandard exists for this Standard in this FeeStructure
            var query = await _feeStructureStandardRepository.WithDetailsAsync(x => x.FeeStructureDetails);
            var feeStructureStandards = await AsyncExecuter.ToListAsync(
                query.Where(x => x.FeeStructureId == feeStructureId && x.StandardId == standardRequest.StandardId)
            );

            // Logic: The UI sends a list of structures for a standard. We probably replace existing ones or add new ones?
            // "standardFeeStructures": [ { "name": "general", "feeHeads": [...] } ]
            // This suggests one Standard can have multiple named structures (e.g. "General", "RTE").
            // My entity FeeStructureStandard has a 'Name' property. So for a given StandardId, we can have multiple entries with different Names.

            // Strategy: For the given StandardId, identify existing ones by Name. Update if exists, create if not. 
            // Better strategy for bulk save: 
            // Remove existing configurations for this standard and recreate them? Or smart update? 
            // Documentation implies "Save", suggesting replacement or update. 
            // Let's assume we replace the configuration for this StandardId entirely or merge. 
            // Given the nested complexity, clearing and re-adding for the specific StandardId might be safest if 'StandardFeeStructures' list is comprehensive.
            // However, usually we should be careful with Ids. But here requests don't have Ids, just Names. 
            // So we can assume re-creation or update by Name.

            // Let's try matching by Name.
            var inputStructureNames = standardRequest.StandardFeeStructures.Select(x => x.Name).ToList();
            
            // Delete those not in input
            var toDelete = feeStructureStandards.Where(x => !inputStructureNames.Contains(x.Name)).ToList();
            await _feeStructureStandardRepository.DeleteManyAsync(toDelete);

            foreach (var structReq in standardRequest.StandardFeeStructures)
            {
                var existingStandardStruct = feeStructureStandards.FirstOrDefault(x => x.Name == structReq.Name);
                
                if (existingStandardStruct == null)
                {
                    existingStandardStruct = new FeeStructureStandard(
                        GuidGenerator.Create(),
                        CurrentTenant.Id,
                        feeStructureId,
                        standardRequest.StandardId,
                        structReq.Name
                    );
                    await _feeStructureStandardRepository.InsertAsync(existingStandardStruct);
                }

                // Handle Details (Fee Heads)
                // We need to manage the details for this structure.
                // existingStandardStruct.FeeStructureDetails might be empty if we just created it.
                // If it existed, we need to load them (I used Include above).

                var existingDetails = existingStandardStruct.FeeStructureDetails.ToList();
                var inputHeadIds = structReq.FeeHeads.Select(x => x.FeeHeadId).ToList();

                // Delete removed heads
                var detailsToDelete = existingDetails.Where(x => !inputHeadIds.Contains(x.FeeHeadId)).ToList();
                // We can't remove from the collection directly if we want EF to track it properly sometimes, but directly deleting via repository is fine.
                // Or better, clear and re-add?
                
                if (detailsToDelete.Any())
                {
                    await _feeStructureDetailRepository.DeleteManyAsync(detailsToDelete);
                }

                foreach (var headReq in structReq.FeeHeads)
                {
                    var existingDetail = existingDetails.FirstOrDefault(x => x.FeeHeadId == headReq.FeeHeadId);
                    if (existingDetail != null)
                    {
                        // Update
                        // Properties are private set, need a method strictly speaking, or use reflection/private access if allowed, 
                        // but here I should probably delete and recreate if I didn't add Update method.
                        // Wait, I didn't add Update method to FeeStructureDetail.
                        // I'll delete and recreate for simplicity and correctness with immutable-style entities.
                        await _feeStructureDetailRepository.DeleteAsync(existingDetail);
                    }
                    
                    var newDetail = new FeeStructureDetail(
                        GuidGenerator.Create(),
                        CurrentTenant.Id,
                        existingStandardStruct.Id,
                        headReq.FeeHeadId,
                        headReq.Amount,
                        headReq.IsOneTime,
                        headReq.IsOptional
                    );
                    await _feeStructureDetailRepository.InsertAsync(newDetail);
                }
            }
        }

        return new MessageResponse { Message = "Standard fee structures saved successfully" };
    }

    public async Task<StandardFeeStructureResponse> GetStandardFeeStructureAsync(Guid feeStructureId, Guid standardId)
    {
        var standard = await _standardRepository.GetAsync(standardId);

        var query = await _feeStructureStandardRepository.WithDetailsAsync(x => x.FeeStructureDetails);
        var structures = await AsyncExecuter.ToListAsync(
            query.Where(x => x.FeeStructureId == feeStructureId && x.StandardId == standardId)
        );

        var response = new StandardFeeStructureResponse
        {
            StandardId = standard.Id,
            StandardName = standard.Name,
            StandardFeeStructures = new List<StandardFeeStructureResponseItem>()
        };

        // Bulk load fee head names and category names
        var allHeadIds = structures.SelectMany(x => x.FeeStructureDetails).Select(d => d.FeeHeadId).Distinct().ToList();
        var feeHeads = await _feeHeadRepository.GetListAsync(x => allHeadIds.Contains(x.Id));
        var feeHeadDict = feeHeads.ToDictionary(x => x.Id, x => x);

        var allCategoryIds = feeHeads.Select(x => x.FeeCategoryId).Distinct().ToList();
        var categories = await _feeCategoryRepository.GetListAsync(x => allCategoryIds.Contains(x.Id));
        var categoryDict = categories.ToDictionary(x => x.Id, x => x.Name);

        foreach (var structure in structures)
        {
            var item = new StandardFeeStructureResponseItem
            {
                Name = structure.Name,
                FeeHeads = new List<FeeHeadAssignmentResponse>()
            };

            foreach (var detail in structure.FeeStructureDetails)
            {
                var feeHead = feeHeadDict.ContainsKey(detail.FeeHeadId) ? feeHeadDict[detail.FeeHeadId] : null;
                var categoryName = (feeHead != null && categoryDict.ContainsKey(feeHead.FeeCategoryId)) 
                    ? categoryDict[feeHead.FeeCategoryId] 
                    : null;

                item.FeeHeads.Add(new FeeHeadAssignmentResponse
                {
                    FeeHeadId = detail.FeeHeadId,
                    FeeHeadName = feeHead?.Name,
                    CategoryName = categoryName,
                    Amount = detail.Amount,
                    IsOneTime = detail.IsOneTime,
                    IsOptional = detail.IsOptional
                });
            }

            response.StandardFeeStructures.Add(item);
        }

        return response;
    }
}
