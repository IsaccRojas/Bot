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

Messages sent in guilds the bot is in will be checked for the keyword specified by 
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

>[command name];[text];[image URL(s)]
e.g.
>hi;Hi, \1!;https://www.website.com/wave.png
which can then be used as
![Custom command example.](https://imgur.com/ToUnQ8u)

The text field uses a `\[digit]` formatting, that allows you to specify a number between 0-9
in which parameters to the command will be emplaced. `\1` refers to the 1st parameter, `\2`
refers to the 2nd parameter, etc. `\0` refers to user of the command.

Multiple images can be specified by separating them with commas, and one will be randomly
selected, e.g.:

>hi;Hi, \1!;https://www.website.com/wave1.png,https://www.website.com/wave2.png

Images are also optional, the following is valid:

>hi;Hi, \1!;

### Role Assignment

WIP