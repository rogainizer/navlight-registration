# Navlight Registration

Navlight is a Windows desktop registration system for a rogaine event. It uses a shared MySQL database on the local network so multiple client PCs can work against the same event data.

## Install options

- `HostAndClient`: installs the desktop app and a local MySQL database on the host PC
- `SingleUser`: installs the desktop app and a local MySQL database on one PC with no router setup
- `ClientOnly`: installs only the desktop app and connects to the host PC over the local network

## Before you install

- Windows is required
- all PCs should be on the same local network
- if you are installing `HostAndClient`, the host PC should have a DHCP reservation configured on the router so its IP address stays stable
- if you are installing a client PC, you need the host PC hostname, normally `navlighthost`

Navlight does not change the PC network configuration during install. For a host install, create the router DHCP reservation first. The installer will stop if that has not been done.

For host installs, the installer also checks that the configured hostname resolves locally and that `ping navlighthost` succeeds before the install continues.

For `SingleUser` installs, no router or network host setup is required. The app and MySQL database are installed on the same PC and the app connects to `localhost`.

To see the current host PC IPv4 and MAC address for the reservation, you can run:

```powershell
powershell -ExecutionPolicy Bypass -File .\get-dhcp-reservation-info.ps1
```

## Download the installer

Download the installer bundle from GitHub Releases:

- https://github.com/rogainizer/navlight-registration/releases

The release ZIP asset name includes the version, for example `navlight-registration-v1.0.0-win-x64.zip`.

After extracting the ZIP, the main installer is `install-navlight.ps1`.

The ZIP also includes `get-dhcp-reservation-info.ps1`, which prints the current adapter, IPv4 address, gateway, and MAC address to use when creating the router DHCP reservation for `navlighthost`.

## Host install

Use this on the PC that will hold the shared MySQL database.

1. Download the latest release ZIP from:

	https://github.com/rogainizer/navlight-registration/releases
2. Extract it to a folder.
3. Make sure the router DHCP reservation is already configured for this PC.
4. Open PowerShell.
5. Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\install-navlight.ps1 -InstallMode HostAndClient
```

The installer will:

- prompt for the install folder
- prompt for the host name that other PCs should use
- show the current adapter, IPv4 address, and MAC address to help confirm the router reservation
- ask you to confirm that the DHCP reservation already exists
- verify that the host name resolves and responds to ping before continuing
- optionally download the MySQL ZIP automatically, or let you point to a local MySQL ZIP file
- install the app and database files
- install the sample `Entries.xlsx` spreadsheet in the app's `EntryLists` folder
- create shortcuts for the app and MySQL start/stop scripts

When the install completes, the host PC is the database server that all client PCs should use.

## Single user install

Use this when the app and database will both run on one PC and no other client PCs need to connect.

1. Download the latest release ZIP from:

	https://github.com/rogainizer/navlight-registration/releases
2. Extract it to a folder.
3. Open PowerShell.
4. Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\install-navlight.ps1 -InstallMode SingleUser
```

The installer will:

- prompt for the install folder
- install the app and local MySQL database on the same PC
- configure the app to connect to `localhost`
- install the sample `Entries.xlsx` spreadsheet in the app's `EntryLists` folder
- create shortcuts for the app and MySQL start/stop scripts

## Client install

Use this on every additional PC that should connect to the host database.

1. Download the latest release ZIP from:

	https://github.com/rogainizer/navlight-registration/releases
2. Extract it to a folder.
3. Open PowerShell.
4. Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\install-navlight.ps1 -InstallMode ClientOnly
```

The installer will prompt for:

- the install folder
- the host PC hostname, normally `navlighthost`

## Notes

- the default install folder is `.\NavlightRegistration`
- elevation is only needed if you choose a protected folder such as `Program Files`
- the app creates shortcuts in the current user's Desktop and Start Menu

## Build a release package

If you are building the installer bundle locally from source, run this from the repository root:

```powershell
.\build-release.ps1 -Configuration Release
```

This creates a versioned ZIP such as `dist\navlight-registration-v1.0.0-win-x64.zip`.

## Create a GitHub release

This repository includes a manually triggered GitHub Actions workflow named `Release Package`.

From GitHub Actions:

1. open `Release Package`
2. click `Run workflow`
3. enter a tag such as `v1.0.0`
4. enter a release name such as `Navlight v1.0.0`

That workflow builds a versioned ZIP such as `navlight-registration-v1.0.0-win-x64.zip` and attaches it to the GitHub release.