/* Bot.cs
Main bot class with entry point. Initializes CommandHandler and RoleHandler
based on Config object that is loaded with data/config.json fields. Has
listeners for incoming messages and reactions.
*/

using System;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Collections.Generic;

//bot config to be loaded with data/config.json
class Config {
    public String BotToken { get; set; }
    public String BotTrigger { get; set; }
    public bool CommandEnabled { get; set; }
    public bool RoleEnabled { get; set; }
    public String RoleGuild { get; set; }
    public String RoleChannel { get; set; }
    public bool JoinEnabled { get; set; }
    public String JoinGuild { get; set; }
    public String JoinChannel { get; set; }
}

class RateLimiter {
    private Dictionary<string, IRateLimitInfo> infos;

    public RateLimiter() {
        infos = new Dictionary<string, IRateLimitInfo>();
    }

    public void Update(IRateLimitInfo info) {
        if (!infos.ContainsKey(info.Bucket))
            infos.Add(info.Bucket, info);
        else
            infos[info.Bucket] = info;
    }

    //if no more requests can be made, stall for resettime amount of time
    public async Task Check() {
        foreach (KeyValuePair<string, IRateLimitInfo> pairs in infos) {
            //check if needed fields are null
            var remaining = pairs.Value.Remaining;
            var resettime = pairs.Value.ResetAfter;
            if (remaining.HasValue && resettime.HasValue) {
                if (remaining.Value <= 0) {
                    Console.WriteLine("Request rate limit reached. Stalling for " + ((int)Math.Ceiling(resettime.Value.TotalMilliseconds) + 1).ToString() + " milliseconds.");
                    await Task.Delay((int)Math.Ceiling(resettime.Value.TotalMilliseconds) + 1);
                }
            }
        }
    }
};

//main bot class
class Bot {
    private DiscordSocketClient _client = null;
    private CommandHandler _commandhandler = null;
    private RoleHandler _rolehandler = null;
    private JoinHandler _joinhandler = null;
    private Config _config = null;
    private RateLimiter _limiter = null;
    public RequestOptions _globaloptions = null;
    
    //use asynchronous main
    static void Main(string[] args)
        => new Bot().MainAsync().GetAwaiter().GetResult();

    public async Task MainAsync() {
        _limiter = new RateLimiter();
        Command.limiter = _limiter;
        CommandHandler.limiter = _limiter;
        RoleHandler.limiter = _limiter;
        JoinHandler.limiter = _limiter;

        //initialize and connect client
        var clientconfig = new DiscordSocketConfig() {
            GatewayIntents = GatewayIntents.DirectMessageReactions | GatewayIntents.DirectMessages | GatewayIntents.GuildBans
                           | GatewayIntents.GuildEmojis | GatewayIntents.GuildIntegrations | GatewayIntents.GuildMembers 
                           | GatewayIntents.GuildMessageReactions | GatewayIntents.GuildMessages | GatewayIntents.Guilds 
                           | GatewayIntents.MessageContent | GatewayIntents.GuildWebhooks
        };
        _client = new DiscordSocketClient(clientconfig);
        _client.Log += Log;

        //get config
        _config = new Config();
        String json_str;
        try {
            json_str = File.ReadAllText("data/config.json");
            _config = JsonSerializer.Deserialize<Config>(json_str);
        } catch (Exception e) {
            if (e is FileNotFoundException) {
                Console.WriteLine("ERROR: data/config.json not found. Creating new config.json file. Please set BotToken field.");
                File.WriteAllText("data/config.json", JsonSerializer.Serialize(_config));
            } else if (e is JsonException) {
                Console.WriteLine("ERROR: data/config.json: " + e.Message);
            } else {
                Console.WriteLine("ERROR: exception thrown when trying to load data/config.json: ");
                throw;
            }
            return;
        }

        if (_config.CommandEnabled || _config.RoleEnabled)
            Console.WriteLine("Setting up handler(s) in 4 seconds.");

        //login using generated token
        try {
            await _client.LoginAsync(
                TokenType.Bot, 
                _config.BotToken
            );
        } catch {
            Console.WriteLine("ERROR: could not log in. Ensure the BotToken field in data/config.json is valid.");
            return;
        }

        //begin running and wait 4 seconds to allow client to connect
        await _client.StartAsync();
        if (_config.CommandEnabled || _config.RoleEnabled)
            await Task.Delay(4000);

        //initialize global message options
        _globaloptions = new RequestOptions() {
            RatelimitCallback = RateLimitCallbackFunction
        };

        //set up command handler
        if (_config.CommandEnabled) {
            _commandhandler = new CommandHandler(_config);
            if (_commandhandler.LoadCommands() != 0) {
                Console.WriteLine("WARN: could not initialize command handler.");
                _commandhandler = null;
            } else {
                //hook command handler to message received event
                _client.MessageReceived += HandleMessageAsync;
                Console.WriteLine("Command handler ready.");
            }
        }

        //set up role handler
        if (_config.RoleEnabled) {
            SocketGuild foundguild = null;
            SocketTextChannel foundchannel = null;

            //try to get guilds from client 3 times
            var guilds = _client.Guilds;
            int i = 0;
            while (guilds.Count <= 0 && i < 2) {
                Console.WriteLine("WARN: retrieved 0 guilds from client. Retrying in 4 seconds.");
                await Task.Delay(4000);
                guilds = _client.Guilds;
                i += 1;
            }

            if (guilds.Count > 0) {
                //find guild in config
                foreach (SocketGuild guild in guilds) {
                    if (guild.Name == _config.RoleGuild) {
                        foundguild = guild;
                        break;
                    }
                }
                if (foundguild != null) {
                    //find channel in config
                    var channels = foundguild.TextChannels;
                    foreach(SocketTextChannel channel in channels) {
                        if (channel.Name == _config.RoleChannel) {
                            foundchannel = channel;
                            break;
                        }
                    }
                }
                //try to initialize role handler
                if (foundguild != null && foundchannel != null) {
                    _rolehandler = new RoleHandler(foundguild, foundchannel, _globaloptions);
                    if ((await _rolehandler.LoadRoles()) != 0) {
                        Console.WriteLine("WARN: could not initialize role handler.");
                        _rolehandler = null;
                    } else {
                        //give command handler reference to role handler's load method on success
                        _commandhandler.SetLoadRolesFn(_rolehandler.LoadRoles);
                        
                        //hook reaction handler to reaction events
                        _client.ReactionAdded += HandleReactionAddedAsync;
                        _client.ReactionRemoved += HandleReactionRemovedAsync;
                        Console.WriteLine("Role handler ready.");
                    }
                }
                else {
                    Console.WriteLine("WARN: config.json: could not find guild or channel specified in configuration for role handler. Either they do not exist, or client did not connect within 5 seconds.");
                    Console.WriteLine("WARN: could not initialize role handler.");
                }
            } else {
                Console.WriteLine("WARN: retrieved 0 guilds from client while trying to initialize role handler.");
                Console.WriteLine("WARN: could not initialize role handler.");
            }
        }

        //set up join handler
        if (_config.JoinEnabled) {
            SocketGuild foundguild = null;
            SocketTextChannel foundchannel = null;

            //try to get guilds from client 3 times
            var guilds = _client.Guilds;
            int i = 0;
            while (guilds.Count <= 0 && i < 2) {
                Console.WriteLine("WARN: retrieved 0 guilds from client. Retrying in 4 seconds.");
                await Task.Delay(4000);
                guilds = _client.Guilds;
                i += 1;
            }

            if (guilds.Count > 0) {
                //find guild in config
                foreach (SocketGuild guild in guilds) {
                    if (guild.Name == _config.JoinGuild) {
                        foundguild = guild;
                        break;
                    }
                }
                if (foundguild != null) {
                    //find channel in config
                    var channels = foundguild.TextChannels;
                    foreach(SocketTextChannel channel in channels) {
                        if (channel.Name == _config.JoinChannel) {
                            foundchannel = channel;
                            break;
                        }
                    }
                }
                //try to initialize join handler
                if (foundguild != null && foundchannel != null) {
                    _joinhandler = new JoinHandler(foundguild, foundchannel, _globaloptions);
                    if ((await _joinhandler.LoadJoin()) != 0) {
                        Console.WriteLine("WARN: could not initialize join handler.");
                        _joinhandler = null;
                    } else {
                        //give command handler reference to join handler's load method on success
                        _commandhandler.SetLoadJoinFn(_joinhandler.LoadJoin);

                        //hook reaction handler to join event
                        _client.UserJoined += HandleUserJoinedAsync;
                        Console.WriteLine("Join handler ready.");
                    }
                } else {
                    Console.WriteLine("WARN: config.json: could not find guild or channel specified in configuration for join handler. Either they do not exist, or client did not connect within 5 seconds.");
                    Console.WriteLine("WARN: could not initialize join handler.");
                }
            } else {
                Console.WriteLine("WARN: retrieved 0 guilds from client while trying to initialize join handler.");
                Console.WriteLine("WARN: could not initialize join handler.");
            }
        }

        //delay infinitely
        await Task.Delay(-1);
    }

    private async Task HandleReactionAddedAsync(Cacheable<IUserMessage, ulong> cachemessage, Cacheable<IMessageChannel, ulong> cachechannel, SocketReaction reaction) {
        if (_rolehandler == null)
            return;
        
        //get channel and check if it is role channel
        IMessageChannel channel = await cachechannel.GetOrDownloadAsync();
        if (channel.Id != _rolehandler.GetChannelId())
            return;

        //get message
        IUserMessage msg = await cachemessage.GetOrDownloadAsync();
        ulong id = cachemessage.Id;

        //check if reaction is not from self
        if (reaction.UserId == _client.CurrentUser.Id)
                return;

        //check if message is role message
        if (msg == null)
            if (id != _rolehandler.GetMessageId())
                return;
        else
            if (msg.Id != _rolehandler.GetMessageId())
                return;
        
        await _rolehandler.ExecuteReactionAdded(reaction);
    }
    
    private async Task HandleReactionRemovedAsync(Cacheable<IUserMessage, ulong> cachemessage, Cacheable<IMessageChannel, ulong> cachechannel, SocketReaction reaction) {
        if (_rolehandler == null)
            return;

        //get channel and check if it is role channel
        IMessageChannel channel = await cachechannel.GetOrDownloadAsync();
        if (channel.Id != _rolehandler.GetChannelId())
            return;

        //get message
        IUserMessage msg = await cachemessage.GetOrDownloadAsync();
        ulong id = cachemessage.Id;

        //check if reaction is not from self
        if (reaction.UserId == _client.CurrentUser.Id)
                return;

        //check if message is role message
        if (msg == null)
            if (id != _rolehandler.GetMessageId())
                return;
        else
            if (msg.Id != _rolehandler.GetMessageId())
                return;
        
        await _rolehandler.ExecuteReactionRemoved(reaction);
    }

    private async Task HandleMessageAsync(SocketMessage message) {
        var msg = message as SocketUserMessage;

        //check if message is not from self or other bot
        if (msg == null) 
            return;
        if (msg.Author.Id == _client.CurrentUser.Id || msg.Author.IsBot)
            return;

        //check if message is calling a command
        int pos = 0;
        if (!msg.HasStringPrefix(_config.BotTrigger, ref pos))
            return;

        //send command context for message to command handler
        if (_commandhandler != null)
            _commandhandler.Execute(new SocketCommandContext(_client, msg), msg, _globaloptions);
    }

    private async Task HandleUserJoinedAsync(SocketGuildUser user) {
        if (_joinhandler == null)
            return;

        //check if joining user is not self or other bot
        if (user.Id == _client.CurrentUser.Id || user.IsBot)
            return;
        
        await _joinhandler.ExecuteJoin(user);
        return;
    }

    private async Task RateLimitCallbackFunction(IRateLimitInfo info) {
        _limiter.Update(info);
    }

    private Task Log(LogMessage log) {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }
}