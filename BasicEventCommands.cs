using Discord;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using RoleyPoley.Core.Interfaces;
using RoleyPoley.Data;

namespace RoleyPoley
{
    [RequireContext(ContextType.Guild)]
    [RequireUserPermission(GuildPermission.ManageRoles)]
    public class BasicEventCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IServiceProvider _Services;

        public BasicEventCommands(IServiceProvider services)
        {
            _Services = services;
        }

        [SlashCommand("react_role", "Add a reaction role assignment to this message", runMode: RunMode.Async)]
        public async Task AddReactionRoleToMessage(string msgId, string emojiString, IRole role)
        {
            await DeferAsync(true);

            IMessage msg = null;
            var channel = (Context.Interaction as SocketSlashCommand).Channel;
            if (ulong.TryParse(msgId, out ulong messageId))
            {
                msg = await channel.GetMessageAsync(messageId);
                if (msg == null)
                {
                    await ModifyOriginalResponseAsync(properties =>
                    {
                        properties.Content = $"Cannot find message in channel <#{channel.Id}>";
                    });
                    return;
                }
            }

            bool addedReaction = false;
            bool customEmoji = emojiString.StartsWith("<");

            IEmote emoji = null;

            if (customEmoji)
            {
                try
                {
                    emoji = Context.Guild.Emotes.FirstOrDefault(e => e.Name == emojiString);
                    await msg.AddReactionAsync(emoji);
                    addedReaction = true;
                }
                catch (Exception exception)
                {
                    // ignored
                }
            }
            else
            {
                try
                {
                    emoji = new Emoji(emojiString);
                    await msg.AddReactionAsync(emoji);
                    addedReaction = true;
                }
                catch (Exception e)
                {
                    // ignored
                }
            }

            if (!addedReaction)
            {
                var content = customEmoji ? $"Custom Emoji's can only be used if its on the **owning** Discord Server." : $"{emojiString} is incompatible.";
                await ModifyOriginalResponseAsync(properties =>
                {
                    properties.Content = content;
                });
                return;
            }

            var db = _Services.GetRequiredService<IDatabase>();
            var roleData = await db.GetAsync<RoleData>($"RoleData/{msgId}");
            if (roleData == null)
            {
                roleData = new RoleData
                {
                    MessageId = messageId,
                    EmojiRoles = new Dictionary<string, ulong>()
                };
            }

            roleData.EmojiRoles.TryAdd(emojiString, role.Id);

            await db.PutAsync($"RoleData/{msgId}", roleData);
            
            await ModifyOriginalResponseAsync(properties =>
            {
                properties.Content = $"When a user reacts to {msg.GetJumpUrl()} with {emoji} they'll get <@&{role.Id}>";
            });
        }
    }
}
