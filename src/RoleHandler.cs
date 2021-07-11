/* RoleHandler.cs
Class for monitoring and managing special "role message". Loads list of
default emotes from data/emotes.csv, and roles to be accessible from this
 message from data/roles.txt. Spawns a "role message" and manages it via
 calls from bot's reaction listener.
*/

using System;
//using System.Reflection;
//using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
//using System.Text;
using Discord;
using Discord.WebSocket;
using Discord.Rest;

//class encapsulating standard emote information
class StandardEmote {
    public StandardEmote(String name, String unicode) {
        Name = name;
        Unicode = unicode;
    }
    public String Name { get; set; }
    public String Unicode { get; set; }
}

//role handler class
class RoleHandler {
    private static List<StandardEmote> _standardemotes_list = null;

    private EmbedBuilder embed = null;

    private SocketGuild _homeguild = null;
    private SocketTextChannel _homechannel = null;
    private RestUserMessage _message = null;

    private List<IRole> _roles_list = new List<IRole>();
    private List<IEmote> _emotes_list = new List<IEmote>();

    public RoleHandler(SocketGuild homeguild, SocketTextChannel homechannel) {
        //load standard emotes from .csv
        if (_standardemotes_list == null) {
            _standardemotes_list = new List<StandardEmote>();
            try {
                //push all standard emotes to list
                StreamReader fr = File.OpenText("data/emotes.csv");
                String line;
                while ((line = fr.ReadLine()) != null) {
                    String[] line_arr = line.Split(',');
                    //ensure number of elements is correct, and has unicode (has X if not)
                    if (line_arr.Length >= 2 && line_arr[1] != "X") {
                        //push to _standardemotes_list
                        _standardemotes_list.Add(
                            new StandardEmote(
                                line_arr[0],
                                line_arr[1]
                            )
                        );
                    }
                }
            } catch (FileNotFoundException) {
                Console.WriteLine("WARN: data/emotes.csv not found.");
            }
        }

        _homeguild = homeguild;
        _homechannel = homechannel;
    }

    public ulong GetMessageId() {
        if (_message != null)
            return _message.Id;
        else
            return 0;
    }
    
    public ulong GetChannelId() {
        if (_homechannel != null)
            return _homechannel.Id;
        else
            return 0;
    }

    public async Task<int> LoadRoles() {      
        List<IRole> _roles_list_tmp = new List<IRole>();
        List<IEmote> _emotes_list_tmp = new List<IEmote>();

        try {
            //read roles.txt for role;emote pairs
            StreamReader fr = File.OpenText("data/roles.txt");
            var roles = _homechannel.Guild.Roles;
            var emotes = _homechannel.Guild.Emotes;
            String line;
            IRole foundrole = null;
            IEmote foundemote = null;
            bool role_found = false;
            bool emote_found = false;
            int i = 1;
            while ((line = fr.ReadLine()) != null) {
                //get role:emote pairs in file
                String[] line_arr = line.Split(';');
                if (line_arr.Length == 2) {
                    //find role in guild's roles
                    foreach (IRole role in roles) {
                        if (role.Name == line_arr[0]) {
                            foundrole = role;
                            role_found = true;
                            break;
                        }
                    }
                    if (!role_found) {
                        Console.WriteLine("WARN: data/roles.txt: role '" + line_arr[0] + "' could not be found (line " + i + ").");
                        continue;
                    }

                    //find emote in guild's emotes
                    foreach (IEmote emote in emotes) {
                        if (emote.Name == line_arr[1]) {
                            foundemote = emote;
                            emote_found = true;
                        }
                    }
                    if (!emote_found) {
                        //find emote in standard
                        if (_standardemotes_list != null) {
                            foreach (StandardEmote stdemote in _standardemotes_list) {
                                if (stdemote.Name == line_arr[1]) {
                                    //convert unicode str to utf32 int to utf16 str
                                    int unicode_utf32_int = int.Parse(stdemote.Unicode, System.Globalization.NumberStyles.HexNumber);
                                    String unicode_utf16_str = Char.ConvertFromUtf32(unicode_utf32_int);
                                    foundemote = (IEmote)(new Emoji(unicode_utf16_str));
                                    emote_found = true;
                                }
                            }
                        }
                        
                    }
                    if (!emote_found) {
                        Console.WriteLine("WARN: data/roles.txt: emote '" + line_arr[1] + "' could not be found (line " + i + ").");
                        continue;
                    }

                    _roles_list_tmp.Add(foundrole);
                    _emotes_list_tmp.Add(foundemote);
                    role_found = false;
                    emote_found = false;
                }
                i += 1;
            }
            fr.Close();
        } catch (FileNotFoundException) {
            Console.WriteLine("WARN: data/roles.txt not found. Creating new empty roles.txt file.");
            File.CreateText("data/roles.txt");
            return -1;
        }

        if (_roles_list_tmp.Count == 0) {
            Console.WriteLine("WARN: data/roles.txt: could not find any valid 'role;emote' pairs.");
            return -1;
        } else
            Console.WriteLine("LoadRoles(): " + _roles_list_tmp.Count + " roles found.");

        //get stored message id
        ulong id = 0;
        try {
            byte[] data = File.ReadAllBytes("data/rolesdata.bin");
            if (data.Length == 8)
                id = Util.DecodeUInt64(data);
        } catch (FileNotFoundException) {
            Console.WriteLine("WARN: data/rolesdata.bin not found. Creating new rolesdata.bin file (after new role message is sent).");
        }

        //get message from retrieved id
        RestUserMessage message = null;
        if (id > 0) {
            message = (RestUserMessage)(await _homechannel.GetMessageAsync(id));
            if (message == null)
                 Console.WriteLine("WARN: data/rolesdata.bin: id is not valid. Creating new message.");
        }

        //build message
        embed = new EmbedBuilder{ Title = "Role List" };
        String desc = "Please react with any of the following emotes to receive its corresponding role.\n";
        for (int i = 0; i < _roles_list_tmp.Count; i++)
            desc += "\n**" + _roles_list_tmp[i].Name + "**: " + _emotes_list_tmp[i].ToString() + "\n";
        embed.WithDescription(desc);

        if (message != null) {
            //modify message and store
            await message.ModifyAsync(x => x.Embed = embed.Build());
            _message = message;

            //add new reactions
            for (int i = 0; i < _emotes_list_tmp.Count; i++) {
                await _message.AddReactionAsync(_emotes_list_tmp[i]);
                await Task.Delay(1000);
            }

        } else {
            //send message and store
            message = (RestUserMessage)(await _homechannel.SendMessageAsync(embed: embed.Build()));
            _message = message;

            //add initial reactions
            for (int i = 0; i < _emotes_list_tmp.Count; i++)
                await _message.AddReactionAsync(_emotes_list_tmp[i]);

            //store new message id
            File.WriteAllBytes("data/rolesdata.bin", BitConverter.GetBytes(_message.Id));
        }

        _roles_list = _roles_list_tmp;
        _emotes_list = _emotes_list_tmp;

        return 0;
    }

    //give role based on reaction
    public async Task ExecuteReactionAdded(SocketReaction reaction) {
        //look for reaction emote in stored emote list
        for (int i = 0; i < _emotes_list.Count; i++) {
            if (_emotes_list[i].ToString() == reaction.Emote.ToString()) {
                //get user and add role
                SocketGuildUser user = _homeguild.GetUser(reaction.UserId);
                if (user != null)
                    await user.AddRoleAsync(_roles_list[i]);
                return;
            }
        }
    }

    //remove role based on reaction
    public async Task ExecuteReactionRemoved(SocketReaction reaction) {
        //look for reaction emote in stored emote list
        for (int i = 0; i < _emotes_list.Count; i++) {
            if (_emotes_list[i].ToString() == reaction.Emote.ToString()) {
                //get user and remove role
                SocketGuildUser user = _homeguild.GetUser(reaction.UserId);
                if (user != null)
                    await user.RemoveRoleAsync(_roles_list[i]);
                return;
            }
        }
    }
}