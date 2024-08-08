using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using RoleyPoley.Core.Interfaces;
using RoleyPoley.Data;
using System.Reflection;

namespace RoleyPoley
{
    public class InteractionHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _commands;
        private readonly IServiceProvider _Services;

        private ILogger _Logger;

        public InteractionHandler(DiscordSocketClient client, InteractionService commands, IServiceProvider services)
        {
            _client = client;
            _commands = commands;
            _Services = services;

            _Logger = services.GetRequiredService<ILogger>();
        }

        public async Task InitialiseAsync()
        {
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _Services);
            _client.InteractionCreated += HandleInteraction;
            _client.ReactionAdded += HandleReactionAdded;
            _client.ReactionRemoved += HandleReactionRemoved;
        }

        private async Task HandleReactionAdded(Cacheable<IUserMessage, ulong> msgCache,
            Cacheable<IMessageChannel, ulong> channelCache, SocketReaction reaction)
        {
            _Logger?.Log($"[HandleReactionAdded]", ELogType.Log);

            try
            {
                var db = _Services.GetRequiredService<IDatabase>();
                var roleData = await db.GetAsync<RoleData>($"RoleData/{msgCache.Id}");
                if (roleData == null) return;

                ulong roleId = 0;

                if (!roleData.EmojiRoles.ContainsKey(reaction.Emote.Name))
                {
                    bool found = false;
                    foreach (var data in roleData.EmojiRoles)
                    {
                        if (data.Key.StartsWith($"<:{reaction.Emote.Name}:"))
                        {
                            roleId = data.Value;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        return;
                    }
                }
                else
                {
                    roleId = roleData.EmojiRoles[reaction.Emote.Name];
                }

                var guildUser = (IGuildUser)reaction.User.Value;
                if (guildUser == null) return;

                await guildUser.AddRoleAsync(roleId);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private async Task HandleReactionRemoved(Cacheable<IUserMessage, ulong> msgCache, Cacheable<IMessageChannel, ulong> channelCache, SocketReaction reaction)
        {
            _Logger?.Log($"[HandleReactionRemoved]", ELogType.Log);

            try
            {
                var db = _Services.GetRequiredService<IDatabase>();
                var roleData = await db.GetAsync<RoleData>($"RoleData/{msgCache.Id}");
                if (roleData == null) return;

                ulong roleId = 0;

                if (!roleData.EmojiRoles.ContainsKey(reaction.Emote.Name))
                {
                    bool found = false;
                    foreach (var data in roleData.EmojiRoles)
                    {
                        if (data.Key.StartsWith($"<:{reaction.Emote.Name}:"))
                        {
                            roleId = data.Value;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        return;
                    }
                }
                else
                {
                    roleId = roleData.EmojiRoles[reaction.Emote.Name];
                }

                var guildUser = (IGuildUser)reaction.User.Value;
                if (guildUser == null) return;

                await guildUser.RemoveRoleAsync(roleId);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private async Task HandleInteraction(SocketInteraction arg)
        {
            _Logger?.Log($"[HandleInteraction]", ELogType.Log);
            var dialogueContext = new InteractionContext(_client, arg);
            await _commands.ExecuteCommandAsync(dialogueContext, _Services);
        }
    }
}
