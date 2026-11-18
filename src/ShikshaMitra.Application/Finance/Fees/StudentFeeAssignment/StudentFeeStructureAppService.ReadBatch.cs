using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Domain.Entities;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
    // This scope exists only while constructing a bulk preview. Confirmation deliberately
    // starts with fresh authoritative reads inside each student's write transaction.
    private readonly AsyncLocal<AssignmentPreviewReads?> previewReadBatch = new();

    private sealed class AssignmentPreviewReads
    {
        public Dictionary<Guid, PaymentTerm> Terms { get; } = new();
        public AssignmentPolicyReadContext Policies { get; } = new();
        public Dictionary<Guid, FeeStructure> Structures { get; } = new();
        public Dictionary<Guid, FeeStructureMetadata> Metadata { get; } = new();
        public Dictionary<Guid, AcademicYear> Years { get; } = new();
        public Dictionary<Guid, List<FeeStructureDetail>> Details { get; } = new();
        public Dictionary<Guid, FeeHead> Heads { get; } = new();
    }

    private IDisposable BeginAssignmentPreviewReads()
    {
        var previous = previewReadBatch.Value;
        previewReadBatch.Value = new AssignmentPreviewReads();
        return new PreviewReadScope(() => previewReadBatch.Value = previous);
    }

    private sealed class PreviewReadScope(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

    private static async Task<TEntity> LoadPreviewEntityAsync<TEntity>(IRepository<TEntity, Guid> repository,
        Dictionary<Guid, TEntity>? cache, Guid id) where TEntity : class, IEntity<Guid>
    {
        if (cache != null && cache.TryGetValue(id, out var saved)) return saved;
        var entity = await repository.GetAsync(id);
        if (cache != null) cache[id] = entity;
        return entity;
    }

    private async Task<List<FeeStructureDetail>> LoadAssignmentDetailsAsync(Guid structureId)
    {
        var cache = previewReadBatch.Value?.Details;
        if (cache != null && cache.TryGetValue(structureId, out var saved)) return saved;
        var details = (await feeStructureDetailRepository.GetListAsync(detail => detail.FeeStructureId == structureId))
            .OrderBy(detail => detail.Id).ToList();
        if (cache != null) cache[structureId] = details;
        return details;
    }

    private async Task<IReadOnlyDictionary<Guid, FeeHead>> LoadAssignmentHeadsAsync(IEnumerable<Guid> headIds)
    {
        var heads = previewReadBatch.Value?.Heads ?? new Dictionary<Guid, FeeHead>();
        var missing = headIds.Distinct().Where(id => !heads.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
        {
            using var tracking = feeHeadRepository.DisableTracking();
            foreach (var head in await feeHeadRepository.GetListAsync(head => Enumerable.Contains(missing, head.Id)))
                heads[head.Id] = head;
        }
        return heads;
    }

    private async Task<IReadOnlyDictionary<Guid, PaymentTerm>> LoadAssignmentTermsAsync(IEnumerable<FeeStructureDetail> details)
    {
        var ids = details.Where(detail => detail.PaymentTermId.HasValue && detail.PaymentTermVersionId.HasValue)
            .Select(detail => detail.PaymentTermId!.Value).Distinct().ToArray();
        var terms = previewReadBatch.Value?.Terms ?? new Dictionary<Guid, PaymentTerm>();
        var missing = ids.Where(id => !terms.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
        {
            using var tracking = paymentTermRepository.DisableTracking();
            foreach (var term in await paymentTermRepository.GetListAsync(term => Enumerable.Contains(missing, term.Id)))
                terms[term.Id] = term;
        }
        return terms;
    }
}
