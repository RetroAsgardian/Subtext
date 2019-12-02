#!/bin/bash
echo -e "\x1b[1m\x1b[95mSubtext Installer\x1b[0m"

# .NET Core
echo -e "\x1b[35mInstalling \x1b[1m\x1b[95m.NET Core\x1b[0m"

if which dotnet >/dev/null && \
	[ $(dotnet --list-sdks | grep -e '^3\.0' | wc -l) -gt 0 ] && \
	[ $(dotnet --list-runtimes | grep -e '^Microsoft\.NETCore\.App 3\.0' | wc -l) -gt 0 ] && \
	[ $(dotnet --list-runtimes | grep -e '^Microsoft\.AspNetCore\.App 3\.0' | wc -l) -gt 0 ]; then
	echo -e "Found a suitable .NET Core installation"
else
	if which apt >/dev/null; then
		wget -nv https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
		sudo apt install ./packages-microsoft-prod.deb
		sudo apt update
		sudo apt install dotnet-sdk-3.0 aspnetcore-runtime-3.0 dotnet-runtime-3.0
		rm ./packages-microsoft-prod.deb
	else
		echo -e "\x1b[1m\x1b[91mCannot find a supported package manager\x1b[0m" >&2
		exit 1
	fi
fi