using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace TGAWarPlanetBot
{
	public class Player
	{
		public string Name { get; set; }
		// Game nickname
		public string GameName { get; set; }
		public ulong DiscordId { get; set; }
		public string GameId { get; set; }
		public string Faction { get; set; }
	}

	public class PlayerDatabase
	{
		public ulong Id { get; set; }
		public string Name { get; set; }
		public List<Player> Players { get; set; }

		public PlayerDatabase()
		{
			Players = new List<Player>();
		}
	}

	public class PlayerDatabaseService
	{
		private readonly string m_databaseDir = "db/player";
		private Dictionary<ulong, PlayerDatabase> m_databases;
		public PlayerDatabaseService()
		{
			Console.WriteLine(Directory.GetCurrentDirectory());
			m_databases = new Dictionary<ulong, PlayerDatabase>();

			// If db directory does not exist we create it
			if (!Directory.Exists(m_databaseDir))
			{
				Directory.CreateDirectory(m_databaseDir);
			}

			var jsonFiles = Directory.EnumerateFiles(m_databaseDir, "*.json", SearchOption.AllDirectories);

			foreach (string currentFile in jsonFiles)
			{
				var jsonString = File.ReadAllText(currentFile);
				PlayerDatabase database = JsonSerializer.Deserialize<PlayerDatabase>(jsonString);
				m_databases[database.Id] = database;
			}
		}

		public PlayerDatabase GetDatabase(SocketGuild guild)
		{
			PlayerDatabase database;
			ulong databaseId = guild.Id;
			if (!m_databases.ContainsKey(databaseId))
			{
				database = new PlayerDatabase();
				database.Id = databaseId;
				database.Name = guild.Name;
				m_databases[databaseId] = database;
			}
			else
			{
				database = m_databases[databaseId];
			}

			return database;
		}

		private void UpdateDatabase(PlayerDatabase database)
		{
			var options = new JsonSerializerOptions
			{
				WriteIndented = true
			};

			string jsonFile = m_databaseDir + "/" + database.Name + "_" + database.Id + ".json";
			string jsonText = JsonSerializer.Serialize<PlayerDatabase>(database, options);
			File.WriteAllText(jsonFile, jsonText);
		}

		public IEnumerable<Player> GetPlayers(SocketGuild guild)
		{
			PlayerDatabase database = GetDatabase(guild);

			return database.Players;
		}

		public Player AddPlayer(string name, PlayerDatabase database)
		{
			Player newPlayer = new Player() { Name = name };
			database.Players.Add(newPlayer);

			UpdateDatabase(database);
			return newPlayer;
		}

		public Player GetPlayer(string name, PlayerDatabase database)
		{
			int index = database.Players.FindIndex(x => x.Name == name);
			if (index >= 0)
			{
				return database.Players[index];
			}
			else
			{
				return null;
			}
		}

		public Player GetPlayerFromGameId(string gameId, PlayerDatabase database)
		{
			int index = database.Players.FindIndex(x => x.GameId == gameId);
			if (index >= 0)
			{
				return database.Players[index];
			}
			else
			{
				return null;
			}
		}

		public bool ConnectPlayer(Player player, SocketUser user, PlayerDatabase database)
		{
			player.DiscordId = user.Id;

			UpdateDatabase(database);
			return true;
		}

		public bool SetPlayerGameId(Player player, string gameId, PlayerDatabase database)
		{
			player.GameId = gameId;

			UpdateDatabase(database);
			return true;
		}

		public bool SetPlayerGameName(Player player, string gameName, PlayerDatabase database)
		{
			player.GameName = gameName;

			UpdateDatabase(database);
			return true;
		}

		public bool SetPlayerFaction(Player player, string faction, PlayerDatabase database)
		{
			player.Faction = faction;

			UpdateDatabase(database);
			return true;
		}
	}

	[Group("player")]
	public class PlayerModule : ModuleBase<SocketCommandContext>
	{
		private readonly PlayerDatabaseService m_databaseService;
		public PlayerModule(PlayerDatabaseService databaseService)
		{
			m_databaseService = databaseService;
		}

		// !player
		[Command]
		public async Task DefaultAsync()
		{
			await ReplyAsync("Awailable commands:\n\t!player add Name\n\t!player connect Name DiscordUser\n\t!player setid Name Gameid\n\t!player list\n\t!player whois Name/GameId");
		}

		// !player list
		[Command("list")]
		[Summary("List all players.")]
		public async Task ListAsync()
		{
			var sb = new System.Text.StringBuilder();
			sb.Append("```");
			sb.Append(String.Format("{0,-20} {1,-20} {2, -15} {3, -5}\n", "Name", "GameName", "GameId", "Faction"));
			sb.Append("-------------------------------------------------------------------\n");
			foreach (var player in m_databaseService.GetPlayers(Context.Guild))
			{
				string gameName = player.GameName != null ? player.GameName : "<N/A>";
				string gameId = player.GameId != null ? player.GameId : "<N/A>";
				string faction = player.Faction != null ? player.Faction : "<N/A>";
				sb.Append(String.Format("{0,-20} {1,-20} {2, -15} {3, -5}\n", player.Name, gameName, gameId, faction));
			}
			sb.Append("```");

			await ReplyAsync(sb.ToString());
		}

		// !player add Name
		[RequireUserPermission(GuildPermission.Administrator)]
		[Command("add")]
		[Summary("Add new player.")]
		public async Task AddAsync(string name = "")
		{
			if (name.Length == 0 || name.StartsWith("!"))
			{
				await ReplyAsync("Invalid player name!");
			}
			else
			{
				PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
				m_databaseService.AddPlayer(name, database);
				await ReplyAsync("Player " + name + " created!");
			}
		}

		// !player connect Name DiscordUser
		[RequireUserPermission(GuildPermission.Administrator)]
		[Command("connect")]
		[Summary("Connect player to discord id.")]
		public async Task ConnectAsync(string name, SocketUser user)
		{
			PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
			Player player = m_databaseService.GetPlayer(name, database);
			if (player != null)
			{
				m_databaseService.ConnectPlayer(player, user, database);
				await ReplyAsync($"Connected {name} -> {user.Username}#{user.Discriminator}");
			}
			else
			{
				await ReplyAsync($"Failed to find player with name {name}");
			}
		}

		// !player setid Name GameId
		[RequireUserPermission(GuildPermission.Administrator)]
		[Command("setid")]
		[Summary("Set game id for player.")]
		public async Task SetIdAsync(string name, string gameId)
		{
			PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
			Player player = m_databaseService.GetPlayer(name, database);
			if (player != null)
			{
				m_databaseService.SetPlayerGameId(player, gameId, database);
				await ReplyAsync($"Set game id of {name} -> {gameId}");
			}
			else
			{
				await ReplyAsync($"Failed to find player with name {name}");
			}
		}

		// !player setname Name GameName
		[RequireUserPermission(GuildPermission.Administrator)]
		[Command("setname")]
		[Summary("Set game name for player.")]
		public async Task SetNameAsync(string name, string gameName)
		{
			PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
			Player player = m_databaseService.GetPlayer(name, database);
			if (player != null)
			{
				m_databaseService.SetPlayerGameName(player, gameName, database);
				await ReplyAsync($"Set game name of {name} -> {gameName}");
			}
			else
			{
				await ReplyAsync($"Failed to find player with name {name}");
			}
		}

		// !player setfaction Name Faction
		[RequireUserPermission(GuildPermission.Administrator)]
		[Command("setfaction")]
		[Summary("Set faction for player.")]
		public async Task SetFactionAsync(string name, string faction)
		{
			PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
			Player player = m_databaseService.GetPlayer(name, database);
			if (player != null)
			{
				m_databaseService.SetPlayerFaction(player, faction, database);
				await ReplyAsync($"Set faction of {name} -> {faction}");
			}
			else
			{
				await ReplyAsync($"Failed to find player with name {name}");
			}
		}

		// !player whois Name/GameId
		[Command("whois")]
		[Summary("Get player matching given name or it.")]
		public async Task WhoIsAsync(string nameOrId = "")
		{
			if (nameOrId.Length == 0)
			{
				await ReplyAsync($"Usage: !player whois Name/GameId");
				return;
			}

			PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);

			// Try to find player with name and if that fails from game id
			Player player = m_databaseService.GetPlayer(nameOrId, database);
			if (player == null)
			{
				player = m_databaseService.GetPlayerFromGameId(nameOrId, database);
			}

			if (player != null)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append(String.Format("{0,-15} {1,-20}\n", "Name:", player.Name));
				string gameName = player.GameName != null ? player.GameName : "<N/A>";
				sb.Append(String.Format("{0,-15} {1,-20}\n", "GameName:", gameName));
				string gameId = player.GameId != null ? player.GameId : "<N/A>";
				sb.Append(String.Format("{0,-15} {1,-20}\n", "GameId:", gameId));
				string faction = player.Faction != null ? player.Faction : "<N/A>";
				sb.Append(String.Format("{0,-15} {1,-20}\n", "Faction:", faction));
				sb.Append("```");
				await ReplyAsync(sb.ToString());
			}
			else
			{
				await ReplyAsync($"Failed to find player matching {nameOrId}");
			}
		}
	}
}