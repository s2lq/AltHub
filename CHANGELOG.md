# Changelog

AltHub is a fork of [Roblox MultiHub](https://github.com/Roblox-MultiHub/Roblox-MultiHub) (GPL-3.0). Everything the original app does is still here.

## 1.5.0

- **Account health dashboard** (new Health tab): status of every login (working, signed out, locked), last check, last launch, crashes in the last 7 days, and what to do. One-click recovery: check all, re-login in the browser with the username and saved password filled in, refresh cookies, unlock help, remove broken accounts, export problems as CSV. Logins are also checked quietly every few hours.
- **Automatic proxies**: paste a proxy list and hand them out one per account or several accounts per proxy (3 per proxy, ...), automatically for new accounts too, with a proxy test.
- **Maximum CPU limit** (Perf page): cap Roblox at a percentage of the whole PC, per window or all windows together, with the window you are playing left alone.
- **Roblox version viewer and changer** (Settings): see the installed and newest Roblox versions and Roblox's recent releases, download one from Roblox's own servers into AltHub's folder and launch it.
- **Event log**: every crash, kick, failed launch, relaunch, signed-out or locked account and recovery, per account, with CSV export.
- **Auto-updater**: AltHub downloads a new release, verifies its SHA-256 checksum, replaces itself and restarts (or just asks first). Optional automatic installs.
- **First-run setup**: a short wizard picks the language and theme, multi-instance, a quick config, where to keep your data (normal or portable) and how to add your first account.
- **Quick configs** (Perf page): one click from *Ultimate performance* (lowest graphics, 15 FPS, about 250 MB and 6 % CPU per window) through *Performance*, *Balanced* and *Quality* to *Best visuals* (high graphics, 120 FPS, no limits).
- **Shareable configs**: export your settings as a `.athcfg` file and import someone else's. Accounts, cookies, passwords, proxies, webhooks and private servers are never included.
- **Portable mode**: keep all data in an `AltHubData` folder next to AltHub.exe (for a USB stick), protected by a password you set (or an explicit "accept the risk" choice), and switch back any time.
- **Language packs**: Spanish, Portuguese, French, German, Italian, Turkish, Russian, Polish, Indonesian and Dutch for the main interface; more can be added as `lang\<code>.json`.
- **Themes**: 11 presets (Dark, Midnight, AMOLED, Slate, Ocean, Forest, Rose, Sunset, Light, Mint, Sand), your own colour schemes with an editor, and import / export as `.aththeme` files.
- **Compact mode** (Ctrl+Shift+K) for small screens and a **mini window** (Ctrl+Shift+W): a small always-on-top launcher with a start / stop button per account.
- **Fast launch**: after the first account, the rest start about 0.35 s apart (up to 5 at once) and their windows are found together with one shared check, instead of waiting seconds between accounts. Detection polls every 150 ms.
- **Community configs**: ready-made setups (AFK farm overnight, Low-end laptop, Alt army, Stream and play, Screenshots and showcase, Battery saver) and 10 more community themes (Dracula, Nord, Tokyo Night, Catppuccin, Gruvbox, Monokai, Cyberpunk, Solarized, Latte, Paper).
- **Code signing setup** with SignPath in the release workflow (see docs/SIGNING.md); runs once the SignPath secrets are added.

## 1.4.0

**New**
- **Private servers tab** - save named private servers (Grind, Trading ...) with their owner, give every account its own (drop-down on each account card, in Edit account, or from the tab), send a whole group or tag into one server with one click, and check that the links still work.
- **Mute button** - mute or unmute the Roblox sound of the selected accounts, before they are launched or while they run (Ctrl+Shift+M).
- **Restore a backup** (Settings > Storage) and a second protected copy of the private server list, so saved servers cannot be lost by an older build.
- **Memory limit per Roblox window** - keeps every window around 250 MB (automatically 500 MB for a window that struggles). The window you are playing can stay unlimited.
- **Tags, colours, notes and Robux** per account, with bulk actions on a tag.
- **Move to a new PC** - export / import accounts and per-account settings as one password-protected `.althub` file.
- **Per-account proxy** for the app's web requests.
- **Rejoin the same server** after a kick, disconnect or crash.
- **Tray icon**, Windows notifications and global hotkeys (Ctrl+Alt+F5 launch all, F6 stop all, F7 show / hide).
- **Keyboard shortcuts** inside the app (see Help > Keyboard shortcuts).
- **Follow Windows light / dark** and an accent colour picker.

**Interface**
- New layout: the account list on the far left, every page as a tab. Tabs always show their full name.
- Modern dark look with rounded controls, clean vector icons (no emoji), themed message boxes and dialogs.
- Tighter spacing, a fuller Home page, wider default window.

**Rebrand**
- Renamed to AltHub with a new icon. Credits to the original authors are kept in the app (Settings > Credits) and in the README.
