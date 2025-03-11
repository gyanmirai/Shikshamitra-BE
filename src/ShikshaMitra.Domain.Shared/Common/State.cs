namespace ShikshaMitra.Common;

public class State
{
	public string Name { get; set; }

	public string IsoCode { get; set; }

	private State()
	{
	}
	public State(string name, string isoCode)
	{
		Name = name;
		IsoCode = isoCode;
	}
}
