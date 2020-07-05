using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

		public void UpdateDatabase(PlayerDatabase database)
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

			return database.Players.OrderBy(p => p.Name);
		}

		public Player AddPlayer(PlayerDatabase database, string name, string id = "<N/A>", string faction = "<N/A>")
		{
			Player newPlayer = new Player() { Name = name, GameId = id, Faction = faction };
			database.Players.Add(newPlayer);

			UpdateDatabase(database);
			return newPlayer;
		}

		public void RemovePlayer(PlayerDatabase database, Player player)
		{
			database.Players.Remove(player);

			UpdateDatabase(database);
		}

		public Player GetPlayer(PlayerDatabase database, string name)
		{
			int index = database.Players.FindIndex(x => x.Name.ToLower() == name.ToLower());
			if (index >= 0)
			{
				return database.Players[index];
			}
			else
			{
				return null;
			}
		}

		public Player GetPlayerFromGameId(PlayerDatabase database, string gameId)
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

		public bool ConnectPlayer(PlayerDatabase database, Player player, SocketUser user)
		{
			player.DiscordId = user.Id;

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
			var sb = new System.Text.StringBuilder();
			sb.Append("```");
			sb.Append("usage: !player <command> [<args>]\n\n");
			sb.Append(String.Format("\t{0,-15} {1}\n", "add", "Add player with given name and properties."));
			sb.Append(String.Format("\t{0,-15} {1}\n", "connect", "Connect a player to a discord user."));
			sb.Append(String.Format("\t{0,-15} {1}\n", "connect", "Set game id and faction for given player."));
			sb.Append(String.Format("\t{0,-15} {1}\n", "setid", "Set the game id for given player."));
			sb.Append(String.Format("\t{0,-15} {1}\n", "setname", "Set the game name for given player."));
			sb.Append(String.Format("\t{0,-15} {1}\n", "setfaction", "Set faction for the given player."));
			sb.Append(String.Format("\t{0,-15} {1}\n", "list", "List all players."));
			sb.Append(String.Format("\t{0,-15} {1}\n", "whois", "Display information for the given player."));
			sb.Append(String.Format("\t{0,-15} {1}\n", "search", "Same as whois."));
			sb.Append("```");
			await ReplyAsync(sb.ToString());
		}

		// !player details
		[Command("details")]
		[Summary("List all players with full details.")]
		public async Task DetailsAsync()
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

		private string GetPlayerList(IEnumerable<Player> players)
		{
			var sb = new System.Text.StringBuilder();
			sb.Append("```");
			sb.Append(String.Format("{0,-20} {1, -5}\n", "Name", "Faction"));
			sb.Append("----------------------------\n");
			foreach (var player in players)
			{
				string faction = player.Faction != null ? player.Faction : "<N/A>";
				sb.Append(String.Format("{0,-20} {1, -5}\n", player.Name, faction));
			}
			sb.Append("```");
			return sb.ToString();
		}

		// !player list
		[Command("list")]
		[Summary("List all players.")]
		public async Task ListAsync(string faction = "")
		{
			string playerList = "";
			if (faction.Length == 0)
			{
				playerList = GetPlayerList(m_databaseService.GetPlayers(Context.Guild));
			}
			else
			{
				playerList = GetPlayerList(m_databaseService.GetPlayers(Context.Guild).Where(p => p.Faction == faction));
			}

			await ReplyAsync(playerList);
		}

		// !player add Name
		[RequireUserPermission(GuildPermission.ManageNicknames)]
		[Command("add")]
		[Summary("Add new player.")]
		public async Task AddAsync(string name = "", string id = "<N/A>", string faction = "<N/A>")
		{
			if (name.Length == 0 || name.StartsWith("!"))
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append("usage: !player add <name> [<game-id>] [<faction>]");
				sb.Append("```");
				await ReplyAsync(sb.ToString());
			}
			else
			{
				PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
				// Make sure we do not add players that already exist
				Player player = m_databaseService.GetPlayer(database, name);
				if (player == null)
				{
					m_databaseService.AddPlayer(database, name, id, faction);
					await ReplyAsync("Player " + name + " created!");
				}
				else
				{
					await ReplyAsync("Player " + name + " already exist!");
				}
			}
		}

		// !player add Name
		[RequireUserPermission(GuildPermission.Administrator)]
		[Command("remove")]
		[Summary("Remove existing player.")]
		public async Task RemoveAsync(string name = "")
		{
			if (name.Length == 0)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append("usage: !player remove <name>");
				sb.Append("```");
				await ReplyAsync(sb.ToString());
			}
			else
			{
				PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
				Player player = m_databaseService.GetPlayer(database, name);
				if (player != null)
				{
					m_databaseService.RemovePlayer(database, player);
					await ReplyAsync($"Removed player {name}");
				}
				else
				{
					await ReplyAsync($"Failed to find player with name {name}");
				}
			}
		}

		// !player connect Name DiscordUser
		[RequireUserPermission(GuildPermission.ManageNicknames)]
		[Command("connect")]
		[Summary("Connect player to discord id.")]
		public async Task ConnectAsync(string name = "", SocketUser user = null)
		{
			if (name.Length == 0 || user == null)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append("usage: !player connect <name> <@discord-user>");
				sb.Append("```");
				await ReplyAsync(sb.ToString());
				return;
			}

			PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
			Player player = m_databaseService.GetPlayer(database, name);
			if (player != null)
			{
				m_databaseService.ConnectPlayer(database, player, user);
				await ReplyAsync($"Connected {name} -> {user.Username}#{user.Discriminator}");
			}
			else
			{
				await ReplyAsync($"Failed to find player with name {name}");
			}
		}

		// !player set Name GameId Faction
		[RequireUserPermission(GuildPermission.ManageNicknames)]
		[Command("set")]
		[Summary("Set game id and faction for player.")]
		public async Task SetAsync(string name = "", string gameId = "", string faction = "")
		{
			if (name.Length == 0 || gameId.Length == 0 || faction.Length == 0)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append("usage: !player set <name> <game-id> <faction>");
				sb.Append("```");
				await ReplyAsync(sb.ToString());
				return;
			}

			PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
			Player player = m_databaseService.GetPlayer(database, name);
			if (player != null)
			{
				player.GameId = gameId;
				player.Faction = faction;
				m_databaseService.UpdateDatabase(database);
				await ReplyAsync($"Set game id of {name} -> {gameId}");
			}
			else
			{
				await ReplyAsync($"Failed to find player with name {name}");
			}
		}

		// !player setid Name GameId
		[RequireUserPermission(GuildPermission.ManageNicknames)]
		[Command("setid")]
		[Summary("Set game id for player.")]
		public async Task SetIdAsync(string name = "", string gameId = "")
		{
			if (name.Length == 0 || gameId.Length == 0)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append("usage: !player setid <name> <game-id>");
				sb.Append("```");
				await ReplyAsync(sb.ToString());
				return;
			}

			PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
			Player player = m_databaseService.GetPlayer(database, name);
			if (player != null)
			{
				player.GameId = gameId;
				m_databaseService.UpdateDatabase(database);
				await ReplyAsync($"Set game id of {name} -> {gameId}");
			}
			else
			{
				await ReplyAsync($"Failed to find player with name {name}");
			}
		}

		// !player setname Name GameName
		[RequireUserPermission(GuildPermission.ManageNicknames)]
		[Command("setname")]
		[Summary("Set game name for player.")]
		public async Task SetNameAsync(string name = "", string gameName = "")
		{
			if (name.Length == 0 || gameName.Length == 0)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append("usage: !player setname <name> <game-name>");
				sb.Append("```");
				await ReplyAsync(sb.ToString());
				return;
			}

			PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
			Player player = m_databaseService.GetPlayer(database, name);
			if (player != null)
			{
				player.GameName = gameName;
				m_databaseService.UpdateDatabase(database);
				await ReplyAsync($"Set game name of {name} -> {gameName}");
			}
			else
			{
				await ReplyAsync($"Failed to find player with name {name}");
			}
		}

		// !player setfaction Name Faction
		[RequireUserPermission(GuildPermission.ManageNicknames)]
		[Command("setfaction")]
		[Summary("Set faction for player.")]
		public async Task SetFactionAsync(string name = "", string faction = "")
		{
			if (name.Length == 0 || faction.Length == 0)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append("usage: !player setfaction <name> <faction>");
				sb.Append("```");
				await ReplyAsync(sb.ToString());
				return;
			}

			PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);
			Player player = m_databaseService.GetPlayer(database, name);
			if (player != null)
			{
				player.Faction = faction;
				m_databaseService.UpdateDatabase(database);
				await ReplyAsync($"Set faction of {name} -> {faction}");
			}
			else
			{
				await ReplyAsync($"Failed to find player with name {name}");
			}
		}

		// !player whois Name/GameId
		[Command("whois")]
		[Alias("search")]
		[Summary("Get player matching given name or it.")]
		public async Task WhoIsAsync(string nameOrId = "")
		{
			if (nameOrId.Length == 0)
			{
				var sb = new System.Text.StringBuilder();
				sb.Append("```");
				sb.Append("usage: !player whois <name>\n");
				sb.Append("usage: !player whois <game-id>\n");
				sb.Append("usage: !player search <name>\n");
				sb.Append("usage: !player search <game-id>");
				sb.Append("```");
				await ReplyAsync(sb.ToString());
				return;
			}

			PlayerDatabase database = m_databaseService.GetDatabase(Context.Guild);

			// Try to find player with name and if that fails from game id
			Player player = m_databaseService.GetPlayer(database, nameOrId);
			if (player == null)
			{
				player = m_databaseService.GetPlayerFromGameId(database, nameOrId);
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