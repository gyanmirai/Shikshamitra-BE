using System;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

/// <summary>
/// Extension seam for the future "AI Seating Recommendation" feature (spec §26). No implementation
/// exists yet — the FE's "ai-suggested" allocation strategy option is disabled until an
/// implementation of this interface is registered. Intentionally mirrors <see cref="AutoAllocateRequestDto"/>
/// / <see cref="AutoAllocateResponseDto"/> so wiring it in later is a drop-in replacement for the
/// deterministic <c>ExamSeatAllocationAppService.AutoAllocatePreviewAsync</c> strategies.
/// </summary>
public interface ISeatingRecommendationProvider
{
    Task<AutoAllocateResponseDto> RecommendAsync(Guid examId, AutoAllocateRequestDto input);
}
