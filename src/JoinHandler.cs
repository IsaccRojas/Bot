/* JoinHandler.cs
Class for responding to and handling user joins to a specified guild. 
Loads list of default emotes from data/emotes.csv, and a custom join
text message from data/joinmessage.txt.
*/

using System;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using Discord;
using Discord.WebSocket;

//join handler class
class JoinHandler {
    private static List<StandardEmote> _standardemotes_list = null;
    public static RateLimiter limiter = null;

    private SocketGuild _homeguild = null;
    private SocketTextChannel _homechannel = null;

    private String _joinmessage = null;

    private List<IEmote> _emotes_list = new List<IEmote>();

    private RequestOptions _options = null;

    public JoinHandler(SocketGuild homeguild, SocketTextChannel homechannel, RequestOptions options) {
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
        _options = options;
    }

    public async Task<int> LoadJoin() {
        //load join message
        try {
            StreamReader fr = File.OpenText("data/joinmessage.txt");
            if ((_joinmessage = fr.ReadToEnd()) == null) {
                Console.WriteLine("WARN: unknown error attempting to read data/joinmessage.txt.");
                return -1;
            }
        } catch (FileNotFoundException) {
            Console.WriteLine("WARN: data/joinmessage.txt not found.");
            return -1;
        }

        return 0;
    }
    
    public ulong GetChannelId() {
        if (_homechannel != null)
            return _homechannel.Id;
        else
            return 0;
    }

    //send join message
    public async Task ExecuteJoin(SocketGuildUser user) {
        //emplace username into \0
        String joinmessage = _joinmessage.Replace("\\0", user.Mention);

        //emplace random guild emotes into \r's, if they exist
        if (joinmessage.Contains("\\r")) {
            var emotes = _homechannel.Guild.Emotes;

            bool srch = false;
            char c;
            for (int i = 0; i < joinmessage.Length; i += 1) {
                c = joinmessage[i];
                if (srch) {
                    if (c == 'r') {
                        //remove \r
                        joinmessage = joinmessage.Remove(i - 1, 2);

                        //get random emote and place it into joinmessage
                        Random rand = new Random();
                        int j_rand = rand.Next(emotes.Count);
                        int j = 0;
                        foreach (IEmote emote in emotes) {
                            if (j == j_rand) {
                                joinmessage = joinmessage.Insert(i - 1, emote.ToString());
                                i -= 2;
                                break;
                            }
                            j += 1;
                        }
                    }
                    srch = false;
                }
                if (c == '\\') {
                    srch = true;
                }
            }
        }

        await _homechannel.SendMessageAsync(joinmessage, options: _options);
        await limiter.Check();
    }
}