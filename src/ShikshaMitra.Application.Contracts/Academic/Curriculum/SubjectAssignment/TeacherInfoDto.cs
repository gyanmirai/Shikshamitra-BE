namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Teacher information for display
/// </summary>
public class TeacherInfoDto
{
	/// <summary>
	/// Teacher ID (as string for FE)
	/// </summary>
	public string Id { get; set; }

	/// <summary>
	/// Teacher full name
	/// </summary>
	public string Name { get; set; }

	/// <summary>
	/// Teacher email address from the linked employee or identity user.
	/// </summary>
	public string? Email { get; set; }

	/// <summary>
	/// Human-readable total professional experience.
	/// </summary>
	public string? Experience { get; set; }
}
