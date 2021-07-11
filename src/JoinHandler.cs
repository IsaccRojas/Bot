/* JoinHandler.cs
Class for responding to and handling user joins to a specified guild. 
Loads list of default emotes from data/emotes.csv, and a custom join
text message from data/joinmessage.txt.
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

//join handler class
class JoinHandler {
    private static List<StandardEmote> _standardemotes_list = null;

    private SocketGuild _homeguild = null;
    private SocketTextChannel _homechannel = null;

    private List<IEmote> _emotes_list = new List<IEmote>();

    public JoinHandler(SocketGuild homeguild, SocketTextChannel homechannel) {
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
    
    public ulong GetChannelId() {
        if (_homechannel != null)
            return _homechannel.Id;
        else
            return 0;
    }

    //send join message
    public async Task ExecuteJoin() {}
}