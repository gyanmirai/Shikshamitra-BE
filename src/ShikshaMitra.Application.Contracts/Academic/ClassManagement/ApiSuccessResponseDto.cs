namespace ShikshaMitra.Academic.ClassManagement;

public class ApiSuccessResponseDto
{
	public bool Success { get; set; } = true;
	public string Message { get; set; } = null!; // The response mapper populates these members after construction.
}
