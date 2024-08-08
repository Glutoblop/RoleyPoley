using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using RoleyPoley.Core.Interfaces;
using RoleyPoley.Data;

namespace RoleyPoley
{
    [RequireContext(ContextType.Guild)]
    public class BasicEventCommands : InteractionModuleBase<InteractionContext>
    {
        private readonly IServiceProvider _Services;

        public BasicEventCommands(IServiceProvider services)
        {
            _Services = services;
        }

        [EnabledInDm(false)]
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("react_role", "Add a reaction role assignment to this message", runMode: RunMode.Async)]
        public async Task AddReactionRoleToMessage(
            [Summary(name: "MessageLink", description: "The Message Link of the message you want to add a reaction role.")] string messageLink,
            [Summary(name: "EmojiString", description: "The emoji to use as the reaction")] string emojiString,
            [Summary(name: "Role", description: "The role this reaction will add.")] IRole role)
        {
            await DeferAsync(true);

            IMessage? msg = null;
            ulong msgId = 0;
            var channel = (Context.Interaction as SocketSlashCommand).Channel;

            try
            {
                string myMessageId = messageLink.Split("/")[^1];
                if (ulong.TryParse(myMessageId, out msgId))
                {
                    msg = await channel.GetMessageAsync(msgId);
                }
            }
            catch (Exception e)
            {
                //ignored
            }

            
            msg = await channel.GetMessageAsync(msgId);
            if (msg == null)
            {
                await ModifyOriginalResponseAsync(properties =>
                {
                    properties.Content = $"Cannot find message in channel <#{channel.Id}>";
                });
                return;
            }

            bool addedReaction = false;
            bool customEmoji = emojiString.StartsWith("<");

            IEmote emoji = null;

            if (customEmoji)
            {
                //eg: <:test:1271108294609735701>
                try
                {
                    var iconString = emojiString.Remove(0,2);
                    iconString = iconString.Remove(iconString.Length - 1,1);
                    var iconData = iconString.Split(":".ToCharArray());
                    iconString = iconData[1];

                    ulong iconId = ulong.Parse(iconString);

                    emoji = Context.Guild.Emotes.FirstOrDefault(e => e.Id == iconId);
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
                    MessageId = msgId,
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
