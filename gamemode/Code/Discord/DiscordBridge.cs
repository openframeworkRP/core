using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OpenFramework.ChatSystem;
using OpenFramework.GameLoop;

namespace OpenFramework.Discord;

/// <summary>
/// Bridge bidirectionnel entre le chat in-game et un channel Discord.
/// - Jeu → Discord : envoie les messages via webhook
/// - Discord → Jeu : poll les messages via bot token et les affiche sur le canal HRP
/// Fonctionne uniquement côté Host.
/// </summary>
public sealed class DiscordBridge : Component
{
	public static DiscordBridge Instance { get; private set; }

	[Property] public string WebhookUrl { get; set; } = "";
	[Property] public string BotToken   { get; set; } = "";
	[Property] public string ChannelId  { get; set; } = "";

	[ConVar( "core-discord_debug", Help = "Activer les logs de debug du bridge Discord" )]
	public static bool DebugMode { get; set; } = false;

	/// <summary>
	/// Dernier message ID lu depuis Discord — pour ne pas re-traiter les anciens messages.
	/// </summary>
	private string _lastMessageId;

	private static readonly JsonSerializerOptions JsonOpts = new()
	{
		PropertyNameCaseInsensitive = true
	};

	protected override void OnAwake()
	{
		if ( !Networking.IsHost ) return;
		Instance = this;
	}

	protected override void OnStart()
	{
		if ( !Networking.IsHost ) return;

		if ( !IsConfigured() )
		{
			Log.Warning( "[Discord] Bridge non configuré — manque webhook_url, bot_token ou channel_id." );
			return;
		}

		_ = InitializeAndStartPolling();
	}

	private async Task InitializeAndStartPolling()
	{
		// Init le curseur AVANT de demarrer le poll, sinon le 1er poll avec
		// after=null rejoue les 10 derniers messages du channel comme neufs.
		await InitializeLastMessageId();

		var interval = Constants.Instance?.DiscordPollInterval ?? 5f;
		Timer.Host( "discord_poll", interval, () => _ = PollDiscordMessages(), true );

		Log.Info( "[Discord] Bridge initialisé." );
	}

	// ══════════════════════════════════════════════════════════════
	//  JEU → DISCORD (via Webhook)
	// ══════════════════════════════════════════════════════════════

	public async Task SendToDiscord( string authorName, string message )
	{
		try
		{
			var payload = new
			{
				username = $"[IG] {authorName}",
				content = message,
				allowed_mentions = new { parse = Array.Empty<string>() }
			};

			var response = await Http.RequestAsync(
				WebhookUrl, "POST", Http.CreateJsonContent( payload ) );

			if ( !response.IsSuccessStatusCode )
			{
				var body = await response.Content.ReadAsStringAsync();
				Log.Warning( $"[Discord] Webhook échoué : {response.StatusCode} — {body}" );
			}
			else if ( DebugMode )
			{
				Log.Info( $"[Discord] Webhook envoyé → {authorName}: {message}" );
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Discord] Erreur envoi webhook : {e.Message}" );
		}
	}

	// ══════════════════════════════════════════════════════════════
	//  DISCORD → JEU (via Bot Token polling)
	// ══════════════════════════════════════════════════════════════

	private async Task InitializeLastMessageId()
	{
		try
		{
			var messages = await FetchMessages( limit: 1 );
			if ( messages != null && messages.Count > 0 )
				_lastMessageId = messages[0].Id;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Discord] Erreur init curseur : {e.Message}" );
		}
	}

	private async Task PollDiscordMessages()
	{
		if ( !Networking.IsHost ) return;
		if ( !IsConfigured() ) return;
		// Defense en profondeur : sans curseur, on refuse de fetch (eviterait
		// de rejouer l'historique si l'init echoue ou se fait doubler).
		if ( string.IsNullOrEmpty( _lastMessageId ) ) return;

		try
		{
			if ( DebugMode )
				Log.Info( $"[Discord] Poll en cours... (lastId={_lastMessageId})" );

			var messages = await FetchMessages( after: _lastMessageId );
			if ( messages == null || messages.Count == 0 ) return;

			messages.Sort( ( a, b ) => string.Compare( a.Id, b.Id, StringComparison.Ordinal ) );

			foreach ( var msg in messages )
			{
				if ( msg.Author?.Bot == true ) continue;
				if ( string.IsNullOrWhiteSpace( msg.Content ) ) continue;

				if ( DebugMode )
					Log.Info( $"[Discord] Message reçu de {msg.Author?.Username}: {msg.Content}" );

				ChatUI.Receive( new ChatUI.ChatMessage
				{
					AuthorId = Guid.Empty,
					AuthorName = msg.Author?.Username ?? "Discord",
					DiscordAuthor = msg.Author?.Username ?? "Discord",
					Message = SanitizeDiscordMessage( msg.Content ),
					IsFromDiscord = true,
					Time = DateTime.Now
				} );

				_lastMessageId = msg.Id;
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Discord] Erreur poll : {e.Message}" );
		}
	}

	private async Task<List<DiscordMessage>> FetchMessages( int limit = 10, string after = null )
	{
		var url = $"https://discord.com/api/v10/channels/{ChannelId}/messages?limit={limit}";
		if ( !string.IsNullOrEmpty( after ) )
			url += $"&after={after}";

		var headers = new Dictionary<string, string>
		{
			{ "Authorization", $"Bot {BotToken}" }
		};

		var response = await Http.RequestAsync( url, "GET", null, headers );

		if ( !response.IsSuccessStatusCode )
		{
			Log.Warning( $"[Discord] Fetch échoué : {response.StatusCode}" );
			return null;
		}

		var json = await response.Content.ReadAsStringAsync();

		if ( DebugMode )
			Log.Info( $"[Discord] Fetch OK — {json[..Math.Min( json.Length, 200 )]}" );

		return JsonSerializer.Deserialize<List<DiscordMessage>>( json, JsonOpts );
	}

	// ══════════════════════════════════════════════════════════════
	//  HELPERS
	// ══════════════════════════════════════════════════════════════

	private bool IsConfigured()
		=> !string.IsNullOrEmpty( WebhookUrl )
		   && !string.IsNullOrEmpty( BotToken )
		   && !string.IsNullOrEmpty( ChannelId );

	private static string SanitizeDiscordMessage( string content )
	{
		if ( string.IsNullOrEmpty( content ) ) return content;

		// Supprimer @everyone et @here
		content = content.Replace( "@everyone", "" ).Replace( "@here", "" );

		// Supprimer les mentions utilisateur <@123456> et <@!123456>
		content = Regex.Replace( content, @"<@!?\d+>", "" );

		// Supprimer les mentions de rôle <@&123456>
		content = Regex.Replace( content, @"<@&\d+>", "" );

		// Supprimer les mentions de channel <#123456>
		content = Regex.Replace( content, @"<#\d+>", "" );

		// Supprimer les commandes slash </command:123456>
		content = Regex.Replace( content, @"</[\w]+:\d+>", "" );

		// Supprimer les emojis custom <:nom:123456> et <a:nom:123456>
		content = Regex.Replace( content, @"<a?:\w+:\d+>", "" );

		// Nettoyer les espaces multiples
		content = Regex.Replace( content.Trim(), @"\s{2,}", " " );

		if ( content.Length > 128 )
			content = content[..128] + "...";

		return content;
	}
}

// ── DTOs Discord API ──────────────────────────────────────────────

public class DiscordMessage
{
	[JsonPropertyName( "id" )]
	public string Id { get; set; }

	[JsonPropertyName( "content" )]
	public string Content { get; set; }

	[JsonPropertyName( "author" )]
	public DiscordAuthor Author { get; set; }
}

public class DiscordAuthor
{
	[JsonPropertyName( "id" )]
	public string Id { get; set; }

	[JsonPropertyName( "username" )]
	public string Username { get; set; }

	[JsonPropertyName( "bot" )]
	public bool Bot { get; set; }
}
