using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using RoleyPoley.Core.Interfaces;
using RoleyPoley.Data;

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
            _client.ModalSubmitted += HandleModalSubmitted;
            _client.ReactionAdded += HandleReactionAdded;
            _client.ReactionRemoved += HandleReactionRemoved;
            _client.SelectMenuExecuted += HandleMenuSelection;
            _client.ButtonExecuted += HandleButtonPressed;

            _client.MessageReceived += HandleMessageReceived;
            _client.MessageUpdated += HandleMessageUpdated;
            _client.MessageDeleted += HandleMessageDeleted;

            _client.ChannelDestroyed += HandleChannelDestroyed;
            _client.UserVoiceStateUpdated += HandleVoiceChannelUpdated;
        }

        private async Task HandleMessageReceived(SocketMessage msg)
        {
            _Logger?.Log($"[HandleMessageReceived]", ELogType.Log);
        }

        private async Task HandleVoiceChannelUpdated(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
        {
            _Logger?.Log($"[HandleVoiceChannelUpdated]", ELogType.Log);
        }

        private async Task HandleChannelDestroyed(SocketChannel socketChannel)
        {
            _Logger?.Log($"[HandleChannelDestroyed]", ELogType.Log);
        }

        private async Task HandleMessageUpdated(Cacheable<IMessage, ulong> msgCache, SocketMessage message, ISocketMessageChannel channel)
        {
            _Logger?.Log($"[HandleMessageUpdated] Message {msgCache.Id} updated: '{message.Content}' in channel: {channel.Id}", ELogType.VeryVerbose);
        }

        private async Task HandleMessageDeleted(Cacheable<IMessage, ulong> msgCache, Cacheable<IMessageChannel, ulong> channelCache)
        {
            _Logger?.Log($"[HandleMessageDeleted]", ELogType.Log);

            var db = _Services.GetRequiredService<IDatabase>();
            var roleData = await db.GetAsync<RoleData>($"RoleData/{msgCache.Id}");
            if (roleData != null)
            {
                await db.DeleteAsync($"RoleData/{msgCache.Id}");
            }
        }

        private async Task HandleButtonPressed(SocketMessageComponent arg)
        {
            _Logger?.Log($"[HandleButtonPressed]", ELogType.Log);
        }

        private async Task HandleMenuSelection(SocketMessageComponent arg)
        {
            _Logger?.Log($"[HandleMenuSelection]", ELogType.Log);
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
                if (!roleData.EmojiRoles.ContainsKey(reaction.Emote.Name)) return;

                var guildUser = (IGuildUser)reaction.User.Value;
                if (guildUser == null) return;

                await guildUser.AddRoleAsync(roleData.EmojiRoles[reaction.Emote.Name]);
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
                if (!roleData.EmojiRoles.ContainsKey(reaction.Emote.Name)) return;

                var guildUser = (IGuildUser)reaction.User.Value;
                if (guildUser == null) return;

                await guildUser.RemoveRoleAsync(roleData.EmojiRoles[reaction.Emote.Name]);
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

        private async Task HandleModalSubmitted(SocketModal arg)
        {
            _Logger?.Log($"[HandleModalSubmitted]", ELogType.Log);
        }
    }
}
