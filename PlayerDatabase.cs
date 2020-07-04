using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
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
			// if (!Directory.Exists("db"))
			// {
			// 	Directory.CreateDirectory("db");
			// }

			// All player databases are in a player folder to allow us to find them easily
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

		private PlayerDatabase GetDatabase(SocketGuild guild)
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
			using (var fileStream = File.Open(jsonFile, FileMode.OpenOrCreate))
			{
				JsonSerializer.SerializeAsync<PlayerDatabase>(fileStream, database, options);
			}
		}

		public IEnumerable<Player> GetPlayers(SocketGuild guild)
		{
			PlayerDatabase database = GetDatabase(guild);

			return database.Players;
		}

		public Player AddPlayer(string name, SocketGuild guild)
		{
			PlayerDatabase database = GetDatabase(guild);

			Player newPlayer = new Player() { Name = name };
			database.Players.Add(newPlayer);

			UpdateDatabase(database);
			return newPlayer;
		}

		public Player GetPlayer(string name, SocketGuild guild)
		{
			PlayerDatabase database = GetDatabase(guild);

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

		public bool ConnectPlayer(Player player, SocketUser user, SocketGuild guild)
		{
			PlayerDatabase database = GetDatabase(guild);

			player.DiscordId = user.Id;
			return true;
		}

		public bool SetPlayerGameId(Player player, string gameId, SocketGuild guild)
		{
			PlayerDatabase database = GetDatabase(guild);

			player.GameId = gameId;
			return true;
		}
	}

	[Group("player")]
	public class PlayerModule : ModuleBase<SocketCommandContext>
	{
		private readonly PlayerDatabaseService m_database;
		public PlayerModule(PlayerDatabaseService database)
		{
			m_database = database;
		}

		// !player
		[Command]
		public async Task DefaultAsync()
		{
			await ReplyAsync("Awailable commands:\n\t!player add Name\n\t!player list");
		}

		// !player list
		[Command("list")]
		[Summary("List all players.")]
		public async Task ListAsync()
		{
			var sb = new System.Text.StringBuilder();
			sb.Append("```");
			foreach (var player in m_database.GetPlayers(Context.Guild))
			{
				string userString = "<N/A>";
				if (player.DiscordId > 0)
				{
					SocketGuildUser user = Context.Guild.GetUser(player.DiscordId);
					userString = $"{user.Username}#{user.Discriminator}";
				}
				string gameId = player.GameId != null ? player.GameId : "<N/A>";
				sb.Append(String.Format("{0,-20} {1,-20} {2, -10}\n", player.Name, userString, gameId));
			}
			sb.Append("```");

			await ReplyAsync(sb.ToString());
		}

		// !player add Name
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
				m_database.AddPlayer(name, Context.Guild);
				await ReplyAsync("Player " + name + " created!");
			}
		}

		// !player connect Name DiscordUser
		[Command("connect")]
		[Summary("Connect player to discord id.")]
		public async Task ConnectAsync(string name, SocketUser user)
		{
			Player player = m_database.GetPlayer(name, Context.Guild);
			if (player != null)
			{
				m_database.ConnectPlayer(player, user, Context.Guild);
				await ReplyAsync($"Connected {name} -> {user.Username}#{user.Discriminator}");
			}
			else
			{
				await ReplyAsync($"Failed to find player with name {name}");
			}
		}

		// !player setid Name GameId
		[Command("setid")]
		[Summary("Set id for player.")]
		public async Task SetIdAsync(string name, string gameId)
		{
			Player player = m_database.GetPlayer(name, Context.Guild);
			if (player != null)
			{
				m_database.SetPlayerGameId(player, gameId, Context.Guild);
				await ReplyAsync($"Set game id of {name} -> {gameId}");
			}
			else
			{
				await ReplyAsync($"Failed to find player with name {name}");
			}
		}
	}
}