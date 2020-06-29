using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace TGAWarPlanetBot
{
	public class InfoModule : ModuleBase<SocketCommandContext>
	{
		// !say hello world -> hello world
		[Command("say")]
		[Summary("Echoes a message.")]
		public Task SayAsync([Remainder] [Summary("The text to echo")] string echo)
			=> ReplyAsync(echo);
			
		// ReplyAsync is a method on ModuleBase 
	}
}