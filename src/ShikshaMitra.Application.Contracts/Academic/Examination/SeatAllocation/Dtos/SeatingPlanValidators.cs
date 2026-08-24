using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FluentValidation;

namespace ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;

public sealed class SaveSeatStudentDtoValidator : AbstractValidator<SaveSeatStudentDto>
{
    public SaveSeatStudentDtoValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.SeatNumber).GreaterThan(0);
        // Guid.Empty is treated as the legacy "no bench" sentinel and is
        // normalized to null by the application service before persistence.
    }
}

public sealed class SaveSeatAllocationRequestDtoValidator : AbstractValidator<SaveSeatAllocationRequestDto>
{
    public SaveSeatAllocationRequestDtoValidator()
    {
        RuleFor(x => x.RoomId).NotEmpty();
        RuleFor(x => x.Students).NotNull().Must(items => items.Count <= 2000)
            .WithMessage("A room cannot contain more than 2000 submitted allocations.");
        RuleForEach(x => x.Students).SetValidator(new SaveSeatStudentDtoValidator());
        RuleFor(x => x.Students).Must(items => items.Select(i => i.StudentId).Distinct().Count() == items.Count)
            .WithMessage("A student may appear only once in a seating-plan save.");
        RuleFor(x => x.Students).Must(items => items.Select(i => i.SeatNumber).Distinct().Count() == items.Count)
            .WithMessage("A seat may appear only once in a room save.");
    }
}

public sealed class SaveSeatingPlanScopeInputDtoValidator : AbstractValidator<SaveSeatingPlanScopeInputDto>
{
    public SaveSeatingPlanScopeInputDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ExamSubjectIds).NotEmpty().Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("ExamSubjectIds must be unique.");
        RuleFor(x => x.ExamSubjectIds).Must((input, ids) => input.ScopeType != SeatingPlanScopeType.SingleSubject || ids.Count == 1)
            .WithMessage("A single-subject seating plan must contain exactly one subject.");
    }
}

public sealed class SaveSeatingPlanDraftInputDtoValidator : AbstractValidator<SaveSeatingPlanDraftInputDto>
{
    public SaveSeatingPlanDraftInputDtoValidator()
    {
        RuleFor(x => x.Name).MaximumLength(128).When(x => x.Name != null);
        RuleFor(x => x.RoomIds).NotNull().Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("RoomIds must be unique.");
        RuleFor(x => x.ExcludedStudentIds).Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("ExcludedStudentIds must be unique.");
        RuleFor(x => x.SpecialRequirementStudentIds).Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("SpecialRequirementStudentIds must be unique.");
        RuleFor(x => x.AllocationRulesJson).NotEmpty().Must(IsJsonObject)
            .WithMessage("AllocationRulesJson must contain a valid JSON object.");
        RuleFor(x => x.ExamSubjectIds).Must(ids => ids == null || ids.Distinct().Count() == ids.Count)
            .WithMessage("ExamSubjectIds must be unique.");
    }

    private static bool IsJsonObject(string value)
    {
        try { return JsonDocument.Parse(value).RootElement.ValueKind == JsonValueKind.Object; }
        catch (JsonException) { return false; }
    }
}

public sealed class SaveSeatingPlanBulkInputDtoValidator : AbstractValidator<SaveSeatingPlanBulkInputDto>
{
    public SaveSeatingPlanBulkInputDtoValidator()
    {
        Include(new SaveSeatingPlanDraftInputDtoValidator());
        RuleFor(x => x.Rooms).NotNull().Must(rooms => rooms.Select(r => r.RoomId).Distinct().Count() == rooms.Count)
            .WithMessage("Rooms must contain unique room identifiers.");
        RuleFor(x => x.Rooms).Must(rooms => rooms.Count <= 200)
            .WithMessage("A bulk save cannot contain more than 200 rooms.");
        RuleForEach(x => x.Rooms).SetValidator(new SaveSeatingPlanBulkRoomDtoValidator());
    }
}

public sealed class SaveSeatingPlanBulkRoomDtoValidator : AbstractValidator<SaveSeatingPlanBulkRoomDto>
{
    public SaveSeatingPlanBulkRoomDtoValidator()
    {
        RuleFor(x => x.RoomId).NotEmpty();
        RuleFor(x => x.Students).NotNull().Must(items => items.Count <= 2000);
        RuleForEach(x => x.Students).SetValidator(new SaveSeatStudentDtoValidator());
        RuleFor(x => x.Students).Must(items => items.Select(i => i.StudentId).Distinct().Count() == items.Count);
        RuleFor(x => x.Students).Must(items => items.Select(i => i.SeatNumber).Distinct().Count() == items.Count);
    }
}

public sealed class AutoAllocateRequestDtoValidator : AbstractValidator<AutoAllocateRequestDto>
{
    private static readonly HashSet<string> Strategies = ["roll-number", "alphabetical", "random", "rank", "alternate-division", "alternate-class"];
    private static readonly HashSet<string> Directions = ["front-to-back", "back-to-front", "row-wise", "column-wise", "aisle-wise"];

    public AutoAllocateRequestDtoValidator()
    {
        RuleFor(x => x.RoomIds).NotEmpty().Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Select at least one unique room.");
        RuleFor(x => x.Strategy).Must(Strategies.Contains).WithMessage("Unsupported allocation strategy.");
        RuleFor(x => x.FillDirection).Must(Directions.Contains).WithMessage("Unsupported fill direction.");
        RuleFor(x => x.ExcludedStudentIds).Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("ExcludedStudentIds must be unique.");
    }
}

public sealed class UnlockSeatingPlanInputDtoValidator : AbstractValidator<UnlockSeatingPlanInputDto>
{
    public UnlockSeatingPlanInputDtoValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
}
