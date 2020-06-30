using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace TGAWarPlanetBot
{
	class Program
	{
		private DiscordSocketClient m_client;
		// Keep the CommandService and DI container around for use with commands.
		// These two types require you install the Discord.Net.Commands package.
		private readonly CommandService m_commands;
		private readonly IServiceProvider m_services;

		static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

		private Program()
		{
			m_client = new DiscordSocketClient(new DiscordSocketConfig
			{
				// How much logging do you want to see?
				LogLevel = LogSeverity.Info,

				// If you or another service needs to do anything with messages
				// (eg. checking Reactions, checking the content of edited/deleted messages),
				// you must set the MessageCacheSize. You may adjust the number as needed.
				//MessageCacheSize = 50,

				// If your platform doesn't have native WebSockets,
				// add Discord.Net.Providers.WS4Net from NuGet,
				// add the `using` at the top, and uncomment this line:
				//WebSocketProvider = WS4NetProvider.Instance
			});

			m_commands = new CommandService(new CommandServiceConfig
			{
				// Again, log level:
				LogLevel = LogSeverity.Info,
				
				// There's a few more properties you can set,
				// for example, case-insensitive commands.
				CaseSensitiveCommands = false,
			});

			// Subscribe the logging handler to both the client and the CommandService.
			m_client.Log += Log;
			m_commands.Log += Log;

			// Setup your DI container.
			m_services = ConfigureServices();
		}

		// If any services require the client, or the CommandService, or something else you keep on hand,
		// pass them as parameters into this method as needed.
		// If this method is getting pretty long, you can seperate it out into another file using partials.
		private static IServiceProvider ConfigureServices()
		{
			var map = new ServiceCollection()
				// Repeat this for all the service classes
				// and other dependencies that your commands might need.
				.AddSingleton(new PlayerDatabaseService());

			// When all your required services are in the collection, build the container.
			// Tip: There's an overload taking in a 'validateScopes' bool to make sure
			// you haven't made any mistakes in your dependency graph.
			return map.BuildServiceProvider();
		}

		// Example of a logging handler. This can be re-used by addons
		// that ask for a Func<LogMessage, Task>.
		private static Task Log(LogMessage message)
		{
			switch (message.Severity)
			{
				case LogSeverity.Critical:
				case LogSeverity.Error:
					Console.ForegroundColor = ConsoleColor.Red;
					break;
				case LogSeverity.Warning:
					Console.ForegroundColor = ConsoleColor.Yellow;
					break;
				case LogSeverity.Info:
					Console.ForegroundColor = ConsoleColor.White;
					break;
				case LogSeverity.Verbose:
				case LogSeverity.Debug:
					Console.ForegroundColor = ConsoleColor.DarkGray;
					break;
			}
			Console.WriteLine($"{DateTime.Now,-19} [{message.Severity,8}] {message.Source}: {message.Message} {message.Exception}");
			Console.ResetColor();
			
			// If you get an error saying 'CompletedTask' doesn't exist,
			// your project is targeting .NET 4.5.2 or lower. You'll need
			// to adjust your project's target framework to 4.6 or higher
			// (instructions for this are easily Googled).
			// If you *need* to run on .NET 4.5 for compat/other reasons,
			// the alternative is to 'return Task.Delay(0);' instead.
			return Task.CompletedTask;
		}

		private async Task InitCommands()
		{
			// Either search the program and add all Module classes that can be found.
			// Module classes MUST be marked 'public' or they will be ignored.
			// You also need to pass your 'IServiceProvider' instance now,
			// so make sure that's done before you get here.
			await m_commands.AddModulesAsync(Assembly.GetEntryAssembly(), m_services);
			// Or add Modules manually if you prefer to be a little more explicit:
			//await m_commands.AddModuleAsync<SomeModule>(m_services);
			// Note that the first one is 'Modules' (plural) and the second is 'Module' (singular).

			// Subscribe a handler to see if a message invokes a command.
			m_client.MessageReceived += HandleCommandAsync;
		}

		private async Task HandleCommandAsync(SocketMessage arg)
		{
			// Bail out if it's a System Message.
			var msg = arg as SocketUserMessage;
			if (msg == null) return;

			// We don't want the bot to respond to itself or other bots.
			if (msg.Author.Id == m_client.CurrentUser.Id || msg.Author.IsBot) return;
			
			// Create a number to track where the prefix ends and the command begins
			int pos = 0;
			// Replace the '!' with whatever character
			// you want to prefix your commands with.
			// Uncomment the second half if you also want
			// commands to be invoked by mentioning the bot instead.
			if (msg.HasCharPrefix('!', ref pos) /* || msg.HasMentionPrefix(_client.CurrentUser, ref pos) */)
			{
				// Create a Command Context.
				var context = new SocketCommandContext(m_client, msg);
				
				// Execute the command. (result does not indicate a return value, 
				// rather an object stating if the command executed successfully).
				var result = await m_commands.ExecuteAsync(context, pos, m_services);

				// Uncomment the following lines if you want the bot
				// to send a message if it failed.
				// This does not catch errors from commands with 'RunMode.Async',
				// subscribe a handler for '_commands.CommandExecuted' to see those.
				//if (!result.IsSuccess && result.Error != CommandError.UnknownCommand)
				//    await msg.Channel.SendMessageAsync(result.ErrorReason);
			}
		}

		private string GetAuthToken()
		{
			var jsonString = File.ReadAllText("auth.json");

			var options = new JsonDocumentOptions
			{
				AllowTrailingCommas = true
			};

			using (JsonDocument document = JsonDocument.Parse(jsonString, options))
			{
				JsonElement tokenElem;
				if (document.RootElement.TryGetProperty("token", out tokenElem))
				{
					return tokenElem.GetString();
				}

				Console.WriteLine("Failed to read auth token!");
				return "";
			}
		}

		public async Task MainAsync()
		{
			// Centralize the logic for commands into a separate method.
			await InitCommands();

			string authToken = GetAuthToken();

			// Login and connect.
			await m_client.LoginAsync(TokenType.Bot, authToken);
			await m_client.StartAsync();

			// Wait infinitely so your bot actually stays connected.
			await Task.Delay(Timeout.Infinite);
		}
	}
}
