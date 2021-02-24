/* CommandHandler.cs
Class for receiving and processing special command messages. Uses Command
class and static methods to execute received command messages from bot's
message listener. Reads data/commands.txt for user-defined custom commands.
*/

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Rest;

//child of command encapsulating custom information used for special execution
class CustomCommand : Command {
    public String Text { get; }
    public int NumParams { get; }
    public String[] Imgs { get; }

    public CustomCommand(String commandname, String commanddesc, String commandsyntax, String text, int num_params, String[] images, bool admin) : base(commandname, commanddesc, commandsyntax, admin, null) {
        Text = text;
        NumParams = num_params;
        Imgs = images;
    }
}

//command handler class
delegate Task<int> LoadRolesFn();
class CommandHandler {
    private Config _config = null;
    //reference to bot's role handler's load function
    private LoadRolesFn _loadrolesfn = null;

    private Command[] command_arr = null;
    private List<CustomCommand> customcommand_list = new List<CustomCommand>();

    public CommandHandler(Config config) {
        _config = config;
    }

    public void SetLoadRolesFn(LoadRolesFn loadrolesfn) {
        _loadrolesfn = loadrolesfn;
    }

    public int LoadCommands() {
        //load default commands
        if (command_arr == null) {
            command_arr = new Command[] {
                new Command("help", "Gets information of available commands.", "``" + _config.BotTrigger + " help``", false, RoutineHelp),
                new Command("poll", "Spawns a message in a poll format.", "``" + _config.BotTrigger + " poll \"[title]\" \"[item]\"(...) [Optional: URL]`` e.g. ``" + _config.BotTrigger +" poll \"Which are better?\" \"Apples\" \"Oranges\" \"Pears\" https://i.imgur.com/85wyR2x.jpg``", false, Command.RoutinePoll),
                new Command("delete", "Deletes messages.", "``" + _config.BotTrigger + " delete [Optional: number of messages, between 0-1000]`` e.g. ``" + _config.BotTrigger + " delete 10``", true, Command.RoutineDelete),
                new Command("kick", "Kicks user.", "``" + _config.BotTrigger + " kick @[Username]`` e.g. ``" + _config.BotTrigger + " kick @Bot``", true, Command.RoutineKick),
                new Command("ban", "Bans user.", "``" + _config.BotTrigger + " ban @[Username] [Optional: number of days to remove messages of from user, between 0-7]`` e.g. ``" + _config.BotTrigger + " ban @Bot 5``", true, Command.RoutineBan),
                new Command("unban", "Unbans user", "``" + _config.BotTrigger + " unban @[Username]`` e.g. ``" + _config.BotTrigger + " unban @Bot``", true, Command.RoutineUnban),
                new Command("reloadcommands", "Reloads command handler.", "``" + _config.BotTrigger + " reloadcommands``", true, RoutineReloadCommands),
                new Command("reloadroles", "Reloads role handler.", "``" + _config.BotTrigger + " reloadroles``", true, RoutineReloadRoles),
            };
        }

        //load custom commands
        List<CustomCommand> customcommand_list_tmp = new List<CustomCommand>();
        try {
            //read commands.txt for name;text;imgs triplets
            StreamReader fr = File.OpenText("data/commands.txt");
            String line;
            int i = 1;
            while ((line = fr.ReadLine()) != null) {
                //get name;text;imgs triplet in file
                String[] line_arr = line.Split(';');
                
                //ensure right size
                if (line_arr.Length != 4) {
                    Console.WriteLine("WARN: data/commands.txt: invalid number of semi-colon separated items (line " + i + ").");
                    i += 1;
                    continue;
                }

                //get string fields
                String name = line_arr[0];
                String text = line_arr[1];
                String[] img_arr = line_arr[2].Split(',');
                bool admin = true;
                if (!(Boolean.TryParse(line_arr[3], out admin)))
                    admin = true;
                
                if (name == "") {
                    Console.WriteLine("WARN: data/commands.txt: command name must not be empty (line " + i + ").");
                    i += 1;
                    continue;
                }

                //look for parameter syntax in order
                int num_params = 0;
                int j = 1;
                do {
                    if (text.Contains("\\" + j.ToString())) {
                        num_params += 1;
                        j += 1;
                        if (j < 10)
                            continue;
                    }
                } while (false);

                //create syntax
                String syntax = "``" + _config.BotTrigger + " " + name;
                //add syntax for each parameter
                for (j = 0; j < num_params; j += 1)
                    syntax += " [name " + (j + 1).ToString() + "]";
                syntax += "``";

                customcommand_list_tmp.Add(
                    new CustomCommand(
                        name,
                        "Custom command.",
                        syntax,
                        text,
                        num_params,
                        img_arr,
                        admin
                    )
                );

                i += 1;
            }
            fr.Close();
        } catch (FileNotFoundException) {
            Console.WriteLine("WARN: data/commands.txt not found. Creating new empty commands.txt file.");
            File.CreateText("data/commands.txt");
        }
        if (customcommand_list_tmp.Count == 0)
            Console.WriteLine("WARN: data/commands.txt: could not find any valid 'name;text' or 'name;text;imgs' triplets.");
        else
            Console.WriteLine("LoadCommands(): " + customcommand_list_tmp.Count + " custom commands found.");
        
        customcommand_list = customcommand_list_tmp;
        return 0;
    }

    private Command FindCommand(String name) {
        //search in default command array
        for (uint i = 0; i < command_arr.Length; i += 1)
            if (command_arr[i].Name == name)
                return command_arr[i];
        //search in custom command list
        for (int i = 0; i < customcommand_list.Count; i += 1)
            if (customcommand_list[i].Name == name)
                return customcommand_list[i];
        return null;
    }
    
    //attempt to run given command message
    public async Task Execute(SocketCommandContext context, SocketUserMessage msg) {
        String msg_str = msg.ToString();

        //attempt to execute "help" command if empty command
        if (msg_str == _config.BotTrigger) {
            Command help = FindCommand("help");
            if (help != null)
                await help.Exec(context, msg);
            return;
        }

        //check formatting
        String[] msg_array = msg.ToString().Split(' ');
        if (msg_array[0] != _config.BotTrigger)
            return;

        //look for command and execute if found
        Command command = FindCommand(msg_array[1]);
        try {
            if (command != null) {

                //check permissions for command
                if (command.Admin) {
                    var user = context.Guild.GetUser(msg.Author.Id);
                    if (!(user.GuildPermissions.Administrator)) {
                        await context.Channel.SendMessageAsync("Insufficient permissions to execute this command.");
                        return;
                    }
                }

                if (command is CustomCommand) {
                    await CustomExecute((CustomCommand)(command), context, msg);
                } else {
                    await command.Exec(context, msg);
                }

            } else {
                    await context.Channel.SendMessageAsync("Command not found. Please use ``" + _config.BotTrigger + "`` or ``" + _config.BotTrigger + " help`` to see a list of commands.");
            }

        } catch (Exception e) {
            String err_msg;
            if (e is ParameterException) {
                err_msg = "Parameter error: ";
            } else if (e is SyntaxException) {
                err_msg = "Syntax error: ";
            } else if (e is CommandException) {
                err_msg = "Command error: ";
            } else {
                throw;
            }
            err_msg += e.Message + ". Syntax: " + command.Syntax;
            await context.Channel.SendMessageAsync(err_msg);
        }
    }

    //attempt to run given custom command
    public async Task CustomExecute(CustomCommand customcommand, SocketCommandContext context, SocketUserMessage msg) {
        String[] msg_array = msg.ToString().Split(' ');

        if (msg_array.Length - 2 != customcommand.NumParams) {
            throw new ParameterException("incorrect number of parameters");
        }

        String text = customcommand.Text;

        //replace special username syntax with users
        text = text.Replace("\\0", msg.Author.Username);
        for (int i = 2; i < msg_array.Length; i += 1) {
            text = text.Replace("\\" + (i - 1).ToString(), msg_array[i]);
        }
        
        //create embed
        var embed = new EmbedBuilder();
        embed.WithDescription(text);
        try {
            var rand = new Random();
            embed.WithImageUrl(customcommand.Imgs[rand.Next(customcommand.Imgs.Length)]);
        } catch {
            Console.WriteLine("WARN: CustomExecute(): invalid URL.");
        }

        await context.Channel.SendMessageAsync(embed: embed.Build());
    }

    /* built-in command methods */

    //get list of available commands
    private async Task RoutineHelp(SocketCommandContext context, SocketUserMessage msg) {
        var user = context.Guild.GetUser(msg.Author.Id);
        if (user == null) {
            await context.Channel.SendMessageAsync("User is invalid. Could not send DM.");
            return;
        }

        String help_str = "Available commands:\n";
        for (int i = 0; i < command_arr.Length; i += 1) {
            help_str += command_arr[i].Name + "\n\t" + command_arr[i].Description;

            //check permissions for command
            if (command_arr[i].Admin && user.GuildPermissions.Administrator)
                help_str += " (admin only)";
            
            help_str += "\n\tSyntax: " + command_arr[i].Syntax + "\n";
        }
            
        for (int i = 0; i < customcommand_list.Count; i += 1) {
            help_str += customcommand_list[i].Name + "\n\t" + customcommand_list[i].Description;

            //check permissions for command
            if (customcommand_list[i].Admin && user.GuildPermissions.Administrator)
                help_str += " (admin only)";
                
            help_str += "\n\tSyntax: " + customcommand_list[i].Syntax + "\n";
        }
        
        //send DM
        await user.SendMessageAsync(help_str);
        await context.Channel.SendMessageAsync("DM sent.");
    }

    //reload bot's (this) command handler
    private async Task RoutineReloadCommands(SocketCommandContext context, SocketUserMessage msg) {
        if (LoadCommands() != 0)
            await context.Channel.SendMessageAsync("Reload failed. See console output for details.");
        else
            await context.Channel.SendMessageAsync("Reload succeeded.");
    }

    //reload bot's role handler
    private async Task RoutineReloadRoles(SocketCommandContext context, SocketUserMessage msg) {
        if (_loadrolesfn == null ||(await _loadrolesfn()) != 0) {
            if (_loadrolesfn == null)
                Console.WriteLine("ERROR: RoutineReloadRoles: _loadrolesfn() is null.");
            await context.Channel.SendMessageAsync("Reload failed. See console output for details.");
        } else
            await context.Channel.SendMessageAsync("Reload succeeded.");
    }
}