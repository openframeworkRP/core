using Facepunch.UI;
using Sandbox;

namespace OpenFramework;

public sealed class PlayerManagerHudComponent : Component
{
	[Property] public MainHUD mainHud {  get; set; }
	[Property] public MainMenuCreatorCharacter menuCreatorHud {  get; set; }
	[Property] public MainMenuComponent mainMenuHud {  get; set; }

	protected override void OnStart()
	{
		mainHud.Enabled = true;
		menuCreatorHud.Enabled = false;
	}

	public void ShowCreator()
	{
		menuCreatorHud.Enabled = true;
		mainMenuHud.Enabled = false;
	}

	public void ShowMainMenu()
	{
		menuCreatorHud.Enabled = false;
		mainMenuHud.Enabled = true;
	}

	public void SwitchToGame()
	{
		CreatorDebug.Info( "[PlayerManager] SwitchToGame" );
		menuCreatorHud.Enabled = false;
		mainMenuHud.Enabled = false;
		CreatorDebug.Info( $"[PlayerManager] menuCreator.Enabled={menuCreatorHud.Enabled}, mainMenu.Enabled={mainMenuHud.Enabled}" );
	}

	protected override void OnUpdate()
	{

	}
}
