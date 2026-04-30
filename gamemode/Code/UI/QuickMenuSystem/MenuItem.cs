using System.Threading.Tasks;

namespace OpenFramework.UI.QuickMenuSystem;

// 🧠 MenuItem.cs
public record MenuItem(
	string Label,
	Delegate OnSelect = null,
	List<MenuItem> Children = null,
	bool Enabled = true,
	bool CloseMenuOnSelect = false,
	bool GoBackOnSelect = false,
	string InputPrompt = null,
	Action<string> OnInputConfirm = null
)
{
	public bool IsSubMenu => Children != null && Children.Count > 0;
	public async Task Invoke()
	{
		if ( OnSelect is Func<Task> asyncAction )
			await asyncAction();
		else if ( OnSelect is Action syncAction )
			syncAction();
	}
}
