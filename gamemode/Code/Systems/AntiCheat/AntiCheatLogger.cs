using Sandbox;
using OpenFramework.Systems.Pawn;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenFramework.Systems.AntiCheat;

/// <summary>
/// Track la session de chaque joueur (durée, argent gagné/perdu)
/// et envoie un résumé Discord à la déconnexion pour repérer les duplicateurs.
/// HOST only.
/// </summary>
public static class AntiCheatLogger
{
	private const string WebhookUrl =
		"https://discord.com/api/webhooks/1488196286757863436/Gjbwk1WC_lCm7ILmIDfo_kuxf8ZuMuBCuVgUL-ZhV2c7awBHXynW0os4hIhXGRpkF2UP";

	// Seuils pour flag "suspect" — argent gagné/min sur la session
	private const int SuspectEarnPerMinute = 5_000;
	private const int CriticalEarnPerMinute = 20_000;

	private sealed class SessionData
	{
		public string DisplayName;
		public ulong SteamId;
		public DateTime StartUtc;
		public int Earned;
		public int Lost;
		public int Transactions;
	}

	private static readonly Dictionary<ulong, SessionData> _sessions = new();

	// ─────────────────────────────────────────────
	//  HOOKS
	// ─────────────────────────────────────────────

	public static void OnPlayerJoin( Client client )
	{
		if ( !Networking.IsHost || client == null ) return;

		_sessions[client.SteamId] = new SessionData
		{
			DisplayName = client.SteamName ?? client.DisplayName,
			SteamId = client.SteamId,
			StartUtc = DateTime.UtcNow,
		};
	}

	public static void OnMoneyAdd( Client client, int amount )
	{
		if ( !Networking.IsHost || client == null || amount <= 0 ) return;
		if ( !_sessions.TryGetValue( client.SteamId, out var s ) ) return;

		s.Earned += amount;
		s.Transactions++;
	}

	public static void OnMoneyRemove( Client client, int amount )
	{
		if ( !Networking.IsHost || client == null || amount <= 0 ) return;
		if ( !_sessions.TryGetValue( client.SteamId, out var s ) ) return;

		s.Lost += amount;
		s.Transactions++;
	}

	public static void OnPlayerDisconnect( Client client )
	{
		if ( !Networking.IsHost || client == null ) return;
		if ( !_sessions.TryGetValue( client.SteamId, out var session ) ) return;

		_sessions.Remove( client.SteamId );

		var finalCash = MoneySystem.Get( client );
		_ = SendSummaryAsync( session, finalCash );
	}

	// ─────────────────────────────────────────────
	//  WEBHOOK
	// ─────────────────────────────────────────────

	private static async Task SendSummaryAsync( SessionData s, int finalCash )
	{
		try
		{
			var duration = DateTime.UtcNow - s.StartUtc;
			var minutes = Math.Max( duration.TotalMinutes, 0.01 );
			var earnPerMin = (int)(s.Earned / minutes);
			var delta = s.Earned - s.Lost;

			// Couleur Discord : vert / orange / rouge selon suspicion
			int color = 3066993; // vert
			string flag = "OK";
			if ( earnPerMin >= CriticalEarnPerMinute )
			{
				color = 15158332; // rouge
				flag = "CRITIQUE";
			}
			else if ( earnPerMin >= SuspectEarnPerMinute )
			{
				color = 15844367; // orange
				flag = "SUSPECT";
			}

			var deltaStr = $"{(delta >= 0 ? "+" : "")}{delta:N0}$";
			var line =
				$"**{s.DisplayName}** `{s.SteamId}` — {FormatDuration( duration )} · " +
				$"+{s.Earned:N0}$ / -{s.Lost:N0}$ (Δ {deltaStr}) · " +
				$"{earnPerMin:N0}$/min · final {finalCash:N0}$ · tx {s.Transactions}";

			var payload = new
			{
				username = "Anti-Cheat",
				embeds = new[]
				{
					new
					{
						description = $"`{flag}` {line}",
						color = color,
					}
				},
				allowed_mentions = new { parse = Array.Empty<string>() }
			};

			var response = await Http.RequestAsync(
				WebhookUrl, "POST", Http.CreateJsonContent( payload ) );

			if ( !response.IsSuccessStatusCode )
			{
				var body = await response.Content.ReadAsStringAsync();
				Log.Warning( $"[AntiCheat] Webhook echoue : {response.StatusCode} — {body}" );
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[AntiCheat] Erreur envoi webhook : {e.Message}" );
		}
	}

	private static string FormatDuration( TimeSpan d )
	{
		if ( d.TotalHours >= 1 )
			return $"{(int)d.TotalHours}h {d.Minutes}m {d.Seconds}s";
		if ( d.TotalMinutes >= 1 )
			return $"{d.Minutes}m {d.Seconds}s";
		return $"{d.Seconds}s";
	}
}
