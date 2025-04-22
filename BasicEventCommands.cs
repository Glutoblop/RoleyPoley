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
        [SlashCommand("allow_grant_role", "Attempt to grant someone a new role, if you are allowed.", runMode: RunMode.Async)]
        public async Task SetGrantRole(
            [Summary(name: "Granter", description: "The role someone has to grant the target role")] IRole granter,
            [Summary(name: "Target", description: "The role they can be given.")] IRole target)
        {
            await DeferAsync(true);

            IDatabase db = _Services.GetRequiredService<IDatabase>();
            RoleGrant roleGrant = await db.GetAsync<RoleGrant>($"Grants/{Context.Interaction.GuildId}");
            if (roleGrant?.Grants == null)
            {
                roleGrant = new RoleGrant()
                {
                    Grants = new()
                };
            }

            if (!roleGrant.Grants.ContainsKey(granter.Id))
            {
                roleGrant.Grants.Add(granter.Id, new());
            }
            if (!roleGrant.Grants[granter.Id].Contains(target.Id))
            {
                roleGrant.Grants[granter.Id].Add(target.Id);
            }

            await db.PutAsync<RoleGrant>($"Grants/{Context.Interaction.GuildId}", roleGrant);

            await ModifyOriginalResponseAsync(properties =>
            {
                properties.Content = $"{granter.Mention} can assign people {target.Mention}";
            });
        }

        [SlashCommand("grant_role", "Attempt to grant someone a new role, if you are allowed.", runMode: RunMode.Async)]
        public async Task GrantRolePlease(
        [Summary(name: "Role", description: "The role you want this person to have")] IRole targetRole,
        [Summary(name: "TargetUser", description: "The user to gain the role")] IGuildUser targetUser)
        {
            await DeferAsync(true);

            IDatabase db = _Services.GetRequiredService<IDatabase>();
            RoleGrant roleGrant = await db.GetAsync<RoleGrant>($"Grants/{Context.Interaction.GuildId}");
            if (roleGrant == null)
            {
                roleGrant = new RoleGrant()
                {
                    Grants = new()
                };
                await db.PutAsync<RoleGrant>($"Grants/{Context.Interaction.GuildId}", roleGrant);
            }

            DiscordSocketClient socketUser = _Services.GetRequiredService<DiscordSocketClient>();
            SocketGuild guild = socketUser.GetGuild(Context.Interaction.GuildId.Value);
            if (guild is IGuild iGuild && iGuild != null)
            {
                IGuildUser granterUser = await iGuild.GetUserAsync(Context.Interaction.User.Id);
                if (granterUser != null)
                {
                    foreach (ulong granterRoleId in granterUser.RoleIds)
                    {
                        //Is any of your roles allowed to grant the target role to a user?
                        if (roleGrant.Grants.ContainsKey(granterRoleId) && roleGrant.Grants[granterRoleId].Contains(targetRole.Id))
                        {
                            //Grant the role
                            try
                            {
                                await targetUser.AddRoleAsync(targetRole.Id);
                                await ModifyOriginalResponseAsync(properties =>
                                {
                                    properties.Content = $"{targetUser.Mention} now has the role {targetRole.Mention}";
                                });
                            }
                            catch
                            {
                                await ModifyOriginalResponseAsync(properties =>
                                {
                                    properties.Content = $"Error, failed to grant {targetUser.Mention} the role {targetRole.Mention}";
                                });
                            }
                            return;
                        }
                    }
                }
            }

            //The role cannot be granted
            await ModifyOriginalResponseAsync(properties =>
            {
                properties.Content = $"You don't have permission to grant {targetRole.Mention}";
            });
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
                    var iconString = emojiString.Remove(0, 2);
                    iconString = iconString.Remove(iconString.Length - 1, 1);
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
