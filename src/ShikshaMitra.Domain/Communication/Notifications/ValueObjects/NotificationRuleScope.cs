using System;
using System.Linq;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationRuleScope
{
	public Guid? AcademicYearId { get; private init; }
	public Guid? BoardId { get; private init; }
	public Guid? MediumId { get; private init; }
	public Guid? ClassId { get; private init; }
	public Guid? DivisionId { get; private init; }
	public string Key { get; private init; } = "school";
	public int Depth { get; private init; }
	private NotificationRuleScope() { }

	public static NotificationRuleScope Create(Guid? yearId = null, Guid? boardId = null,
		Guid? mediumId = null, Guid? classId = null, Guid? divisionId = null)
	{
		Guid?[] values = [yearId, boardId, mediumId, classId, divisionId];
		if (values.Any(value => value == Guid.Empty)) throw new ArgumentException("Scope identifiers cannot be empty.");
		var depth = values.TakeWhile(value => value.HasValue).Count();
		if (values.Skip(depth).Any(value => value.HasValue))
			throw new ArgumentException("A scope requires all of its parent identifiers.");
		string[] labels = ["year", "board", "medium", "class", "division"];
		return new()
		{
			AcademicYearId = yearId, BoardId = boardId, MediumId = mediumId, ClassId = classId, DivisionId = divisionId,
			Depth = depth,
			Key = depth == 0 ? "school" : "school/" + string.Join("/", values.Take(depth)
				.Select((value, index) => $"{labels[index]}:{value!.Value:N}"))
		};
	}

	public bool Contains(NotificationRuleScope other) => other != null &&
		(other.Key == Key || other.Key.StartsWith(Key + "/", StringComparison.Ordinal));
}
