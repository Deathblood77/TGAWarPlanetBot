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
		public string Nick { get; set; }
		public ulong DiscordId { get; set; }
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

		public void AddPlayer(string name, SocketGuild guild)
		{
			PlayerDatabase database = GetDatabase(guild);

			database.Players.Add(new Player() { Name = name });

			UpdateDatabase(database);
		}

		public bool ConnectPlayer(string name, SocketUser user, SocketGuild guild)
		{
			PlayerDatabase database = GetDatabase(guild);

			int index = database.Players.FindIndex(x => x.Name == name);
			if (index >= 0)
			{
				database.Players[index].DiscordId = user.Id;

				UpdateDatabase(database);
				return true;
			}
			else
			{
				return false;
			}
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

		// !player add Name
		[Command("connect")]
		[Summary("Connect player to discord id.")]
		public async Task ConnectAsync(string name, SocketUser user)
		{
			if (m_database.ConnectPlayer(name, user, Context.Guild))
			{
				await ReplyAsync($"Connected {name} -> {user.Username}#{user.Discriminator}");
			}
			else
			{
				await ReplyAsync($"Failed to connect {name} -> {user.Username}#{user.Discriminator}");
			}
		}
	}
}