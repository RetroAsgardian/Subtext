# Subtext
Subtext is an open-source, secure, private, decentralized alternative chat platform, similar to Discord or Skype. It uses end-to-end PGP encryption to protect your messages. Anyone can host their own public or private instance easily.

This repository contains the server code for Subtext.

(This project is licensed under the [GNU General Public License v3](https://www.gnu.org/licenses/gpl-3.0.en.html), but I'm too lazy to add the LICENSE file right now.)

(By the way, we have a [Discord](https://discord.gg/dt5bfHB).)

## Dependencies
- .NET Core 3.0
- Microsoft SQL Server 2017 (For now...)
- A computer
- That's it

## Installing
(We'll make an installer script later.)

- Install .NET Core
- Install Microsoft SQL Server 2017
- Create the database and user
- Clone this repository
- Edit `Config.cs`
- Create `db.creds` file
- `dotnet restore` or whatever
- `dotnet ef database update`
- Write a systemd unit file or something
- Congrations

## Forking Etiquette
If you create a fork, please change `variant` in `Program.cs` to your repository name. This helps us understand and solve bug reports involving unofficial server variants, and informs users about the code running on each instance.