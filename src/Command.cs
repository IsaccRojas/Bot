/* Command.cs
Command class for wrapping an executable command with relevant information.
Has multiple "built-in" static command methods.
*/

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Rest;

//to be thrown when error with parameter list
[Serializable]
class ParameterException : Exception {
    public ParameterException() : base() {}
    public ParameterException(string message) : base(message) {}
    public ParameterException(string message, Exception inner) : base(message, inner) {}

    protected ParameterException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
        : base(info, context) {}
}

//to be thrown when error with syntax
[Serializable]
class SyntaxException : Exception {
    public SyntaxException() : base() {}
    public SyntaxException(string message) : base(message) {}
    public SyntaxException(string message, Exception inner) : base(message, inner) {}

    protected SyntaxException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
        : base(info, context) {}
}

//to be thrown when error with command routine
[Serializable]
class CommandException : Exception {
    public CommandException() : base() {}
    public CommandException(string message) : base(message) {}
    public CommandException(string message, Exception inner) : base(message, inner) {}

    protected CommandException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
        : base(info, context) {}
}

//class encapsulating command information
delegate Task CommandRoutine(SocketCommandContext context, SocketUserMessage msg);
class Command {
    public String Name { get; }
    public String Description { get; }
    public String Syntax { get; }
    protected CommandRoutine _commandroutine;
    public Command(String commandname, String commanddesc, String commandsyntax, CommandRoutine commandroutine = null) {
        Name = commandname;
        Description = commanddesc;
        Syntax = commandsyntax;
        _commandroutine = commandroutine;
    }
    public Task Exec(SocketCommandContext context, SocketUserMessage msg) {
        if (_commandroutine != null)
            return _commandroutine(context, msg);
        else
            return Task.Delay(0);
    }

    /* built-in command methods */

    //spawn "poll"-formatted message
    public static async Task RoutinePoll(SocketCommandContext context, SocketUserMessage msg) {
        //get message elements
        String[] msg_array = msg.ToString().Split(' ');
        if (msg_array.Length < 4) {
            throw new ParameterException("insufficient parameters");
        }

        //get quote substrings
        List<String> quotes = null;
        try {
            quotes = new List<String>(Util.GetQuoteSubstrings(msg.ToString()));
        } catch (QuoteException) {
            throw new SyntaxException("open quotation");
        }

        //error if not enough quotation items
        if (quotes.Count < 2) {
            if (quotes.Count < 1) {
                throw new SyntaxException("no quotation strings found");
            } else {
                throw new SyntaxException("only title quotation string found");
            }
        }

        //begin building embed using last element of msg_array as image url
        var embed = new EmbedBuilder{ Title = quotes[0] };
        String desc = new String("");
        for (int i = 1; i < quotes.Count && i < 26; i += 1)
            desc += Util.GetUnicodeLetterI(i - 1) + " **" + quotes[i] + "**\n";
        embed.WithDescription(desc);
        
        try {
            embed.WithImageUrl(msg_array[msg_array.Length - 1]);
        } catch {
            Console.WriteLine("WARN: _poll(): invalid URL.");
        }
        embed.WithFooter("React with the corresponding emote to vote.");

        //send message
        var pollmsg = (RestUserMessage)(await context.Channel.SendMessageAsync(embed: embed.Build()));

        //add initial reactions
        for (int i = 1; i < quotes.Count && i < 26; i += 1)
            await pollmsg.AddReactionAsync(new Emoji(Util.GetUnicodeLetterI(i - 1)));

        //delete message that invoked command
        await context.Channel.DeleteMessageAsync(msg);
    }

    //delete number of messages
    public static async Task RoutineDelete(SocketCommandContext context, SocketUserMessage msg) {
        //determine if sufficient permissions to execute command
        var user = context.Guild.GetUser(msg.Author.Id);
        if (user.GuildPermissions.ManageMessages) {

            //get number of messages to delete
            int del_num = 0;
            String[] msg_array = msg.ToString().Split(' ');
            if (msg_array.Length < 3) {
                //use default number
                del_num = 1;
            } else if (!(int.TryParse(msg_array[2], out del_num) && del_num >= 0 && del_num <= 1000)) {
                //failed to get specified number
                throw new CommandException("invalid number of messages");
            }

            var messages_pages = context.Channel.GetMessagesAsync(msg, Direction.Before, del_num);
            var messages = await AsyncEnumerableExtensions.FlattenAsync<IMessage>(messages_pages);
            var i = 0;
            foreach (var message in messages) {
                await context.Channel.DeleteMessageAsync(message);
                i += 1;
            }
            
            await context.Channel.SendMessageAsync(i.ToString() + " message(s) deleted.");
        }
        else
            await context.Channel.SendMessageAsync("Insufficient permissions to execute this command.");
    }

    //kick user
    public static async Task RoutineKick(SocketCommandContext context, SocketUserMessage msg) {
        //determine if sufficient permissions to execute command
        var user = context.Guild.GetUser(msg.Author.Id);
        if (user.GuildPermissions.KickMembers || user.GuildPermissions.Administrator) {

            //determine if correct number of parameters
            String[] msg_array = msg.ToString().Split(' ');
            if (msg_array.Length < 3) {
                throw new ParameterException("insufficient parameters");
            }

            //determine if user is valid
            ulong id = Util.GetSubstringUInt64(msg_array[2]);
            if (id == 0)
                throw new CommandException("invalid user");

            //kick user
            await context.Guild.GetUser(id).KickAsync(reason: "Bot command");
            await context.Channel.SendMessageAsync("User kicked.");
        }
        else
            await context.Channel.SendMessageAsync("Insufficient permissions to execute this command.");
    }

    //ban user
    public static async Task RoutineBan(SocketCommandContext context, SocketUserMessage msg) {
        //determine if sufficient permissions to execute command
        var user = context.Guild.GetUser(msg.Author.Id);
        if (user.GuildPermissions.BanMembers || user.GuildPermissions.Administrator) {

            //determine if correct number of parameters
            String[] msg_array = msg.ToString().Split(' ');
            if (msg_array.Length < 3) {
                throw new ParameterException("insufficient parameters");
            }

            //determine if user is valid
            ulong id = Util.GetSubstringUInt64(msg_array[2]);
            if (id == 0)
                throw new CommandException("invalid user");

            //determine if number of days is specified and valid
            if (msg_array.Length == 4) {
                int days = 0;
                if (int.TryParse(msg_array[3], out days) && days >= 0 && days <= 7) {
                    //ban user and delete number of days worth of message history
                    await context.Guild.AddBanAsync(userId: id, pruneDays: days, reason: "Bot command");
                    await context.Channel.SendMessageAsync("User banned, deleted " + msg_array[3] + " days of their message history.");
                } else {
                    throw new CommandException("invalid number of days");
                }
            } else {
                //ban user with default number of days (0)
                await context.Guild.AddBanAsync(userId: id, reason: "Bot command");
                await context.Channel.SendMessageAsync("User banned.");
            }
        }
        else
            await context.Channel.SendMessageAsync("Insufficient permissions to execute this command.");
    }

    //unban user
    public static async Task RoutineUnban(SocketCommandContext context, SocketUserMessage msg) {
        //determine if sufficient permissions to execute command
        var user = context.Guild.GetUser(msg.Author.Id);
        if (user.GuildPermissions.BanMembers || user.GuildPermissions.Administrator) {

            //determine if correct number of parameters
            String[] msg_array = msg.ToString().Split(' ');
            if (msg_array.Length < 3)
                throw new ParameterException("insufficient parameters");

            //determine if user is valid
            ulong id = 0;
            id = Util.GetSubstringUInt64(msg_array[2]);
            if (id == 0)
                throw new CommandException("invalid user");

            //unban user
            await context.Guild.RemoveBanAsync(userId: id);
            await context.Channel.SendMessageAsync("User unbanned.");
        }
        else
            await context.Channel.SendMessageAsync("Insufficient permissions to execute this command.");
    }
}