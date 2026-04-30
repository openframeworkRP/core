using Facepunch;

namespace OpenFramework.UI.QuickMenuSystem;

public interface IQuickMenuInterface
{
	string Title { get; }
	string SubTitle { get; }

	QuickMenuStyle Style { get; }
	List<MenuItem> BuildMenu();
	int GetRebuildHash() => 0;
}

public struct QuickMenuStyle
{
	public string Style { get; set; }
	public string BackgroundImage { get; set; }
	public string HoverItemStyle { get; set; }
}
