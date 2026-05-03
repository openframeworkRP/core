using Editor;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenFramework.Editor;

public class SetupForkDialog : Widget
{
	LineEdit _orgField;
	LineEdit _identField;
	Label _statusLabel;

	const string SbprojFile = "core.sbproj";

	public SetupForkDialog() : base( null )
	{
		WindowTitle = "Configurer ce fork — OpenFramework";
		SetWindowIcon( "settings" );
		MinimumSize = new Vector2( 420, 240 );

		Layout = Layout.Column();
		Layout.Margin = 16;
		Layout.Spacing = 8;

		Layout.Add( new Label( "Renseignez votre org et ident s&box pour ce fork.\nCes valeurs seront écrites dans core.sbproj.", this ) );
		Layout.AddSpacingCell( 4 );

		Layout.Add( new Label( "Org :", this ) );
		_orgField = Layout.Add( new LineEdit( this ) );
		_orgField.PlaceholderText = "monorg";

		Layout.Add( new Label( "Ident :", this ) );
		_identField = Layout.Add( new LineEdit( this ) );
		_identField.PlaceholderText = "core";

		Layout.AddSpacingCell( 4 );
		_statusLabel = Layout.Add( new Label( "", this ) );

		Layout.AddStretchCell();

		var btn = Layout.Add( new Button( "Appliquer", "check", this ) );
		btn.Clicked = OnApply;

		LoadCurrentValues();
	}

	void LoadCurrentValues()
	{
		try
		{
			var json = FileSystem.Root.ReadAllText( SbprojFile );
			var node = JsonNode.Parse( json );
			_orgField.Text = node["Org"]?.GetValue<string>() ?? "";
			_identField.Text = node["Ident"]?.GetValue<string>() ?? "";
		}
		catch ( Exception e )
		{
			SetStatus( $"Lecture impossible : {e.Message}", "#f66" );
		}
	}

	void OnApply()
	{
		var org = _orgField.Text.Trim();
		var ident = _identField.Text.Trim();

		if ( string.IsNullOrEmpty( org ) || string.IsNullOrEmpty( ident ) )
		{
			SetStatus( "Org et Ident sont obligatoires.", "#f66" );
			return;
		}

		try
		{
			var json = FileSystem.Root.ReadAllText( SbprojFile );
			var node = JsonNode.Parse( json );
			node["Org"] = org;
			node["Ident"] = ident;
			FileSystem.Root.WriteAllText( SbprojFile, node.ToJsonString( new JsonSerializerOptions { WriteIndented = true } ) );
			SetStatus( $"Sauvegardé — {org}.{ident}", "#4f4" );
		}
		catch ( Exception e )
		{
			SetStatus( $"Erreur : {e.Message}", "#f66" );
		}
	}

	void SetStatus( string text, string color )
	{
		_statusLabel.Text = text;
		_statusLabel.SetStyles( $"color: {color};" );
	}
}

public static class SetupForkMenu
{
	[Menu( "Editor", "OpenFramework/Configurer ce fork...", "settings" )]
	public static void Open()
	{
		var dialog = new SetupForkDialog();
		dialog.Show();
	}
}
