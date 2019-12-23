# Subtext
Subtext is an open-source, secure, private, decentralized alternative chat platform, similar to Discord or Skype. It uses end-to-end PGP encryption to protect your messages. Anyone can host their own public or private instance easily.

This repository contains the server code for Subtext.

(This project is licensed under the GNU Affero General Public License, version 3. See [LICENSE.md](LICENSE.md) for more details.)

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
	- On bionic I'm pretty sure you need to do some library symlink fuckery
	- But I don't remember exactly what that is
	- Google it i guess
- Create the database and user
	- `CREATE DATABASE Subtext`
	- `CREATE LOGIN username WITH PASSWORD='password'`
	- `USE Subtext`
	- `CREATE USER username FOR LOGIN username`
	- `ALTER ROLE db_owner ADD MEMBER username`
	- I hate MS SQL Server so much
	- Also you'll need to `GO` after each of these or whatever because Transact-SQL
- Clone this repository
- Edit `Config.cs`
- Create `db.creds` file
- `dotnet restore` or whatever
- `dotnet ef database update`
- Write a systemd unit file or something
- Congrations

## Forking Etiquette
If you create a fork/derivative of this software, please change `variant` in `Program.cs` to your repository name. This helps us understand and solve bug reports involving unofficial server variants, and informs users about the code running on each instance.

## Making a Client or Whatever Else
Because Subtext uses end-to-end encryption, a significant portion of functionality is defined at the client level. To simplify development and ensure compatibility, we highly reccommend using an official client API:

- [SubtextPy](https://github.com/RetroAsgardian/SubtextPy)
- (more will be added later)

If none of these libraries work for you, please check out the client documentation.
