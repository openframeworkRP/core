using Facepunch;
using OpenFramework.UI.QuickMenuSystem;

namespace OpenFramework.Models;

public partial class RealityPanel : Panel
{
	public RealityPanel()
	{
		StyleSheet.Load( "Models/RealityPanel.cs.scss" );
	}

	private bool _isTriggered;

	public bool IsTriggered
	{
		get => _isTriggered;
		set
		{
			var triggeable = this as ITriggeable;
			if ( triggeable == null )
				return;

			_isTriggered = value;

			if ( value )
				triggeable.OnOpen();
			else
				triggeable.OnClose();

			SetClass( "triggered", value );
		}
	}

	public interface ITriggeable
	{
		/// <summary>
		/// True if the panel is currently triggered.
		/// </summary>
		bool IsTriggered { get; protected set; }

		/// <summary>
		/// The input action that will trigger this panel.
		/// </summary>
		string InputAction { get; }

		/// <summary>
		/// Called when the panel is opened.
		/// </summary>
		void OnOpen();

		/// <summary>
		/// Called when the panel is closed.
		/// </summary>
		void OnClose();
	}

	public override void Tick()
	{
		base.Tick();

		var triggeable = this as ITriggeable;
		if ( triggeable != null && !string.IsNullOrEmpty( triggeable.InputAction ) && Input.Pressed(triggeable.InputAction) && Client.Local.PlayerPawn.IsValid() && (QuickMenu.Instance == null || !QuickMenu.Instance.IsOpen) )
		{
			Log.Info( "RealityPanel: Tick" );
			IsTriggered = !IsTriggered;
		}
	}
}
