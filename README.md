# Bot

A simple client-side Discord bot using the Discord.Net API. Has minimal functionality
and is intended to be as simple to use as possible.

## Configuration

The configuration is specified by the `data/config.json` file, and has the following fields:

- `BotToken`: Bot token string provided by client created via the [Discord developer site](https://discord.com/developers/applications).
- `BotTrigger`: Keyword the bot will respond to for commands.
- `CommandEnabled`: true/false value for if command functionality should be enabled.
- `RoleEnabled`: true/false value for if role assignment functionality should be enabled.
- `RoleGuild`: Name of guild (Discord server) the role assignment message will be created in (if RoleEnabled is true).
- `RoleChannel`: Name of channel within guild the role assignment message will be created in (if RoleEnabled is true).

## Usage

### Commands

Messages sent in guilds the bot client is in will be checked for the keyword specified by 
the `data/config.json`'s `BotTrigger` keyword, followed by a space:

>[BotTrigger] [...]

e.g.

>!bot help

The following commands are built into the bot by default:

- `help`: Sends the user a private message containing a list of all available commands.
- `reloadcommands`: Reloads the command handler.
- `reloadroles`: Reloads the role handler.

### Custom Commands

Custom embed message commands can be created via the `data/commands.txt` file. Each line
will specify a new custom command, which can be loaded on startup and reloaded via the
built-in `reloadroles` command. Custom commands have the following syntax:

>[command name];[text];[image URL(s)];[admin]

e.g.

	hi;Hi, \1!;https://www.website.com/wave.png;false

which can then be used as

![Custom command example.](https://i.imgur.com/qZhsXpD.png)

The text field uses a `\[digit]` formatting, that allows you to specify a number between 0-9
in which parameters to the command will be emplaced. `\1` refers to the 1st parameter, `\2`
refers to the 2nd parameter, etc. `\0` refers to user of the command.

Multiple images can be specified by separating them with commas, and one will be randomly
selected, e.g.:

	hi;Hi, \1!;https://www.website.com/wave1.png,https://www.website.com/wave2.png;false

Text, images, and administrator privileges are optional (defaults to true). The following examples are valid:

	blank;;;false
	blankadmin;;;
	textonly;Here's some text.;;false
	textonlyadmin;Here's some text, only for admins.;;true
	twoimages;;https://www.website.com/wave1.png,https://www.website.com/wave2.png;false
	oneimageadmin;;https://www.website.com/wave1.png;

### Role Assignment

Support for role assignment is implemented in the form of a special message sent and managed
in a guild's channel specified by the `data/config.json`'s `RoleGuild` and `RoleChannel`
fields.

Reactions added and removed on this message will be used to add and remove roles on the reacting
users. The roles to allow assignment/unassignment of with a corresponding emote to react
with are specified via the `data/roles.txt` file, per line, with the following syntax:

>[role name];[emote]

e.g.

	Red;red_circle
	Blue;large_blue_circle

which creates the following message:

![Role message example.](https://i.imgur.com/dOEKQHT.png)

At least one role must be specified for the message to be spawned. Users can then react with the
listed emotes and receive their corresponding role, and remove the reaction to have that role removed.

**Note:** this system uses the `data/emotes.csv` file to get a list of all standard Unicode
emotes. This may be outdated past the time of this writing. Last update: 2:44 P.M. CST 2/23/2021

### Additional Notes

This was created as a small project to occupy some of my free time, and was made for personal use, as
well as for friends. As such, I likely will not be consistently maintaining this software. That said,
feel free to use the bot and/or its source code as you wish.