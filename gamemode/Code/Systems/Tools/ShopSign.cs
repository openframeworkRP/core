using OpenFramework.Extension;
using OpenFramework.UI.World.ShopSigns;

namespace OpenFramework.Systems.Tools;

public sealed class ShopSign : Component
{
	public static readonly Color DefaultBackground = new( 20f / 255f, 12f / 255f, 4f / 255f, 0.85f );
	public const float DefaultFontSize = 96f;
	public const float MinFontSize = 32f;
	public const float MaxFontSize = 200f;

	/// <summary>
	/// Distance d'interaction maximale (touche E) pour ouvrir le panneau d'edition.
	/// Plus longue que la distance standard (Constants.InteractionDistance) pour
	/// les enseignes posees en hauteur sur la facade des shops. Lue par la trace
	/// dediee dans PlayerPawn.Using.cs.
	/// </summary>
	public const float InteractionRange = 300f;
	public const float InteractionTraceSize = 10f;

	/// <summary>
	/// Active les logs de diag du flow shop sign (trace E, radial menu, panel
	/// d'edition, RPC sync). Bascule via la console : <c>shopsign_debug 1</c>
	/// pour activer, <c>shopsign_debug 0</c> pour desactiver. Off par defaut.
	/// </summary>
	[ConVar( "shopsign_debug" )]
	public static bool DebugLogs { get; set; } = false;

	// Autorite host-only : seul l'host ecrit (via RpcUpdateSign), tous les clients
	// recoivent la valeur a jour. Sans FromHost, l'autorite suit l'owner du GameObject
	// et l'host ne peut plus propager si le meuble appartient a un autre client.
	[Property, Sync( SyncFlags.FromHost )] public string SignText { get; set; } = "Mon Shop";
	[Property, Sync( SyncFlags.FromHost )] public float FontSize { get; set; } = DefaultFontSize;
	[Property, Sync( SyncFlags.FromHost )] public Color BackgroundColor { get; set; } = DefaultBackground;

	private string _lastText;
	private float _lastFontSize;
	private Color _lastBackground;
	private ShopSignUI _ui;

	protected override void OnStart()
	{
		_lastText = SignText;
		_lastFontSize = FontSize;
		_lastBackground = BackgroundColor;
		WireUI();
		if ( DebugLogs ) Log.Info( $"[ShopSign] OnStart sur '{GameObject.Name}' (proxy={Network.IsProxy}, host={Networking.IsHost}) : text='{SignText}', size={FontSize:0}, color={BackgroundColor.Hex}, ui={_ui.IsValid()}" );
	}

	protected override void OnUpdate()
	{
		var changed = SignText != _lastText
			|| !FontSize.AlmostEqual( _lastFontSize, 0.01f )
			|| BackgroundColor != _lastBackground;

		if ( !changed ) return;

		if ( DebugLogs ) Log.Info( $"[ShopSign] OnUpdate detecte un changement sur '{GameObject.Name}' : text='{SignText}', size={FontSize:0}, color={BackgroundColor.Hex}" );

		_lastText = SignText;
		_lastFontSize = FontSize;
		_lastBackground = BackgroundColor;

		if ( _ui.IsValid() )
			_ui.StateHasChanged();
	}

	private void WireUI()
	{
		_ui = GameObject.Components.Get<ShopSignUI>( FindMode.EverythingInSelfAndDescendants );
		if ( _ui.IsValid() )
			_ui.Sign = this;
		else
			Log.Warning( $"[ShopSign] WireUI : aucun ShopSignUI trouve sur '{GameObject.Name}' (le panel ne s'affichera pas)" );
	}

	[Rpc.Host]
	public void RpcUpdateSign( string text, float fontSize, Color backgroundColor )
	{
		if ( DebugLogs ) Log.Info( $"[ShopSign] RpcUpdateSign recu sur host : text='{text}', size={fontSize:0}, color={backgroundColor.Hex}" );

		var caller = Rpc.Caller.GetClient();
		if ( caller == null )
		{
			Log.Warning( "[ShopSign] RpcUpdateSign : caller introuvable, abandon." );
			return;
		}

		// Texte permissif : si vide on conserve le texte courant pour autoriser des
		// updates live qui ne touchent que la taille/couleur (le texte n'est ecrit
		// qu'a la validation finale ou des qu'il a ete modifie).
		text = text?.Trim() ?? "";
		if ( !string.IsNullOrEmpty( text ) )
		{
			if ( text.Length > 40 ) text = text[..40];
			SignText = text;
		}

		FontSize = fontSize.Clamp( MinFontSize, MaxFontSize );
		BackgroundColor = new Color(
			backgroundColor.r.Clamp( 0f, 1f ),
			backgroundColor.g.Clamp( 0f, 1f ),
			backgroundColor.b.Clamp( 0f, 1f ),
			backgroundColor.a.Clamp( 0f, 1f )
		);

		if ( DebugLogs ) Log.Info( $"[ShopSign] '{caller.DisplayName}' a mis a jour l'enseigne : '{SignText}' (taille={FontSize:0}, fond={BackgroundColor.Hex})" );
	}
}
