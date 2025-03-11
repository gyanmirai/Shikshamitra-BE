namespace ShikshaMitra.Common;

public class Country
{
	public string Name { get; set; }

	public string IsoCode { get; set; }


	private Country()
	{
	}

	public Country(string name, string isoCode)
	{
		Name = name;
		IsoCode = isoCode;
	}
}
