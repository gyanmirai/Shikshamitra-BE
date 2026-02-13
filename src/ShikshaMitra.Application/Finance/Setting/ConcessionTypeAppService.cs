using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Application.Contracts.Finance.Setting.ConcessionType;
using ShikshaMitra.Application.Contracts.Finance.Setting.ConcessionType.Dtos;
using ShikshaMitra.Finance.Setting.ConcessionType;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Setting;

[Authorize]
[RemoteService(false)]
public class ConcessionTypeAppService : ApplicationService, IConcessionTypeAppService
{
    private readonly IRepository<ConcessionType.ConcessionType, Guid> _concessionTypeRepository;
    private readonly IRepository<ConcessionTypeFeeHead, Guid> _concessionTypeFeeHeadRepository; // May not need direct repo if using nav property, but good for specific queries
    private readonly IRepository<ConcessionTypeDocument, Guid> _concessionTypeDocumentRepository;
    private readonly IRepository<FeeHead, Guid> _feeHeadRepository;
    private readonly IRepository<AcademicYear, Guid> _academicYearRepository;

    public ConcessionTypeAppService(
        IRepository<ConcessionType.ConcessionType, Guid> concessionTypeRepository,
        IRepository<ConcessionTypeFeeHead, Guid> concessionTypeFeeHeadRepository,
        IRepository<ConcessionTypeDocument, Guid> concessionTypeDocumentRepository,
        IRepository<FeeHead, Guid> feeHeadRepository,
        IRepository<AcademicYear, Guid> academicYearRepository)
    {
        _concessionTypeRepository = concessionTypeRepository;
        _concessionTypeFeeHeadRepository = concessionTypeFeeHeadRepository;
        _concessionTypeDocumentRepository = concessionTypeDocumentRepository;
        _feeHeadRepository = feeHeadRepository;
        _academicYearRepository = academicYearRepository;
    }

    public async Task<MutationResponse> CreateAsync(Guid academicYearId, ConcessionTypeCreateRequest input)
    {
        // Validation
        var academicYear = await _academicYearRepository.GetAsync(academicYearId); // Ensure exists

        // Create Entity
        var concessionType = new ConcessionType.ConcessionType(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            academicYearId,
            input.Name,
            input.BasedOn,
            input.Amount,
            input.IsDocumentRequired,
            input.Description
        );

        // Add Fee Heads
        foreach (var feeHeadRef in input.ApplicableFeeHeads)
        {
            // Verify fee head exists?
            // Ignoring check for performance or do simple check?
            // Let's rely on FKs or simple assumption for now.
            concessionType.AddFeeHead(feeHeadRef.Id);
        }

        await _concessionTypeRepository.InsertAsync(concessionType);

        // handle documents separately as they are separate entities
        if (input.IsDocumentRequired && input.Documents != null)
        {
             foreach (var doc in input.Documents)
             {
                 var acceptedFormatsString = string.Join(",", doc.AcceptedFormat.Select(f => (int)f));
                 var document = new ConcessionTypeDocument(
                     GuidGenerator.Create(),
                     concessionType.Id,
                     doc.DocumentName,
                     doc.DocumentSize,
                     acceptedFormatsString
                 );
                 await _concessionTypeDocumentRepository.InsertAsync(document);
             }
        }

        return new MutationResponse
        {
            Id = concessionType.Id,
            Message = "Concession type created successfully"
        };
    }

    public async Task<ConcessionTypePaginatedResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null)
    {
        var query = await _concessionTypeRepository.WithDetailsAsync(x => x.ConcessionTypeFeeHeads);
        
        query = query.Where(x => x.AcademicYearId == academicYearId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Name.Contains(search));
        }

        var totalCount = await _concessionTypeRepository.AsyncExecuter.CountAsync(query);

        query = query.OrderByDescending(x => x.CreationTime)
                     .Skip((page - 1) * pageSize)
                     .Take(pageSize);

        var items = await _concessionTypeRepository.AsyncExecuter.ToListAsync(query);

        // Optimization: Batch load fee head names
        var feeHeadIds = items.SelectMany(x => x.ConcessionTypeFeeHeads).Select(fh => fh.FeeHeadId).Distinct().ToList();
        var feeHeads = await _feeHeadRepository.GetListAsync(x => feeHeadIds.Contains(x.Id));
        var feeHeadDict = feeHeads.ToDictionary(x => x.Id, x => x.Name);

        var dtos = items.Select(x => new ConcessionTypeListItem
        {
            Id = x.Id,
            Name = x.Name,
            BasedOn = x.BasedOn == ConcessionBasedOn.Fixed ? "FIXED" : "PERCENTAGE", // Enum to string logic
            Amount = x.Amount,
            IsDocumentRequired = x.IsDocumentRequired,
            ApplicableFeeHeads = x.ConcessionTypeFeeHeads.Select(fh => new FeeHeadRef 
            { 
                Id = fh.FeeHeadId,
                Name = feeHeadDict.ContainsKey(fh.FeeHeadId) ? feeHeadDict[fh.FeeHeadId] : null
            }).ToList()
        }).ToList();

        return new ConcessionTypePaginatedResponse
        {
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Items = dtos
        };
    }

    public async Task<ConcessionTypeResponse> GetAsync(Guid id)
    {
        var concessionType = await _concessionTypeRepository.GetAsync(id);
        
        // Load details manually or using WithDetailsAsync on single item?
        // Let's use repository methods to fetch related data
        
        var feeHeadLinks = await _concessionTypeFeeHeadRepository.GetListAsync(x => x.ConcessionTypeId == id);
        var feeHeadIds = feeHeadLinks.Select(x => x.FeeHeadId).ToList();
        var feeHeads = await _feeHeadRepository.GetListAsync(x => feeHeadIds.Contains(x.Id));
        
        var documents = await _concessionTypeDocumentRepository.GetListAsync(x => x.ConcessionTypeId == id);

        return new ConcessionTypeResponse
        {
            Id = concessionType.Id,
            Name = concessionType.Name,
            BasedOn = concessionType.BasedOn,
            Amount = concessionType.Amount,
            IsDocumentRequired = concessionType.IsDocumentRequired,
            Description = concessionType.Description,
            ApplicableFeeHeads = feeHeads.Select(fh => new FeeHeadRef { Id = fh.Id, Name = fh.Name }).ToList(),
            Documents = documents.Select(d => new ConcessionDocumentDto
            {
                DocumentName = d.DocumentName,
                DocumentSize = d.DocumentSize,
                AcceptedFormat = d.AcceptedFormats.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(f => (ConcessionDocumentFormat)int.Parse(f)).ToList()
            }).ToList()
        };
    }

    public async Task<MutationResponse> UpdateAsync(Guid id, ConcessionTypeUpdateRequest input)
    {
        var concessionType = await _concessionTypeRepository.GetAsync(id);
        
        concessionType.Update(
            input.Name,
            input.BasedOn,
            input.Amount,
            input.IsDocumentRequired,
            input.Description
        );

        // Update Fee Heads
        // Simplest strategy: delete all and re-add.
        var existingFeeHeads = await _concessionTypeFeeHeadRepository.GetListAsync(x => x.ConcessionTypeId == id);
        await _concessionTypeFeeHeadRepository.DeleteManyAsync(existingFeeHeads);

        var newFeeHeads = input.ApplicableFeeHeads.Select(fh => new ConcessionTypeFeeHead(id, fh.Id)).ToList();
        await _concessionTypeFeeHeadRepository.InsertManyAsync(newFeeHeads);

        // Update Documents
        if (input.IsDocumentRequired)
        {
            var existingDocs = await _concessionTypeDocumentRepository.GetListAsync(x => x.ConcessionTypeId == id);
            await _concessionTypeDocumentRepository.DeleteManyAsync(existingDocs);
            
            if (input.Documents != null)
            {
                var newDocs = input.Documents.Select(doc => new ConcessionTypeDocument(
                    GuidGenerator.Create(),
                    id,
                    doc.DocumentName,
                    doc.DocumentSize,
                    string.Join(",", doc.AcceptedFormat.Select(f => (int)f))
                )).ToList();
                
                await _concessionTypeDocumentRepository.InsertManyAsync(newDocs);
            }
        }
        else
        {
             // Remove documents if requirement disabled
             var existingDocs = await _concessionTypeDocumentRepository.GetListAsync(x => x.ConcessionTypeId == id);
             await _concessionTypeDocumentRepository.DeleteManyAsync(existingDocs);
        }

        await _concessionTypeRepository.UpdateAsync(concessionType);

        return new MutationResponse
        {
            Id = concessionType.Id,
            Message = "Concession type updated successfully"
        };
    }

    public async Task<MutationResponse> DeleteAsync(Guid id)
    {
        // Delete related data
        var feeHeads = await _concessionTypeFeeHeadRepository.GetListAsync(x => x.ConcessionTypeId == id);
        await _concessionTypeFeeHeadRepository.DeleteManyAsync(feeHeads);

        var docs = await _concessionTypeDocumentRepository.GetListAsync(x => x.ConcessionTypeId == id);
        await _concessionTypeDocumentRepository.DeleteManyAsync(docs);
        
        await _concessionTypeRepository.DeleteAsync(id);

        return new MutationResponse
        {
            Id = id,
            Message = "Concession type deleted successfully"
        };
    }
}
