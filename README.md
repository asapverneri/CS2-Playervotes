<div align="center">
  <img src="https://i.ibb.co/qQRyk5T/CS2-Playervotes2.png" width="500"/>
  <h3>🎮 CS2 Playervotes</h3>
  <p>Lightweight and efficient voting system for CS2 without anything pointless, allowing players to initiate votes for kicking, banning, and muting players. 
  <br>The plugin ensures fair play by requiring a configurable percentage of votes to pass before an action is taken.
  <br>Plugin uses commands like css_ban, css_kick, css_mute so actions are stored into your site that shows punishments.</p>
</div>
<div align="center">
  <img src="https://img.shields.io/github/v/tag/asapverneri/CS2-Playervotes?style=for-the-badge&label=Version" alt="GitHub tag (with filter)" />
  <img src="https://img.shields.io/github/last-commit/asapverneri/CS2-Playervotes?style=for-the-badge" alt="Last Commit" />
  <blockquote>
    <strong>⚠️ <span style="color:red;">CAUTION</span></strong>  
    <br><span style="color:red;">This plugin should work with almost any admin plugin.</span>
  </blockquote>
</div>

---

## 📋 Features / Roadmap

<p>✅ Votekick</p>
<p>✅ Voteban</p>
<p>✅ Votemute</p>
<p>✅ Votegag</p>
<p>✅ Votesilence</p>
<p>✅ Immunity for VIPs</p>
<p>✅ Configurable Vote Threshold (per vote type)</p>
<p>✅ One vote at a time, shown to everyone on a Panorama panel (press <b>E</b> to vote, or <code>!yes</code> / <code>!no</code>)</p>
<p>✅ Votes last 30 s, threshold follows the player count, and a voted player is protected from the same vote for 5 min</p>
<p>✅ Easy usage</p>
<br>
<p>⬜ ...</p>

---

## 📦 Installion

- Install latest [CounterStrike Sharp](https://github.com/roflmuffin/CounterStrikeSharp) & [Metamod:Source](https://www.sourcemm.net/downloads.php/?branch=master)
- Install all requirements listed below.
- Download the latest release from the releases tab and copy it into the csgo folder.

**Requirements**
- [MenuManager](https://genesis-cs.space/menuconstructor/menumanager/api/index.html#start) (`MenuManagerApi.dll` in `addons/counterstrikesharp/shared/MenuManagerApi/`). The player picks the menu type with `!menu`.
- PanoramaManager (0.4.4) on the server, and the compiled vote panel (`panorama/layout/custom_game/pv_vote.vxml_c` + `panorama/styles/custom_game/pv_vote.vcss_c`, sources in `src/panorama/`) in an addon the clients load. Without the panel, votes still work through `!yes` / `!no`.

**How a vote works**
- `!votekick` (etc.) opens the player list; picking a player starts the vote. The starter counts as YES.
- Only one vote runs at a time. Every player sees the panel; pressing **E** makes it clickable, choosing YES/NO (or **E** / ×) returns to normal play.
- The vote passes when YES reaches `ceil(percentage × players in game)` (the target counts as a player whose vote is NO, unless `AllowSelfVote` lets them choose), and fails when that can no longer happen or after `General.VoteDurationSeconds`. The count is redone after every vote, every second and after a disconnect. If the target leaves, the vote is cancelled - except a voteban, which goes on (the target's vote still counts) and, if it passes, bans by SteamID with `Ban.OfflineCommand` (default is CS2-SimpleAdmin's `css_addban`).
- After a vote ends (passed or failed), the same vote type can't target that player for `General.TargetProtectionMinutes`.

**Example config** (`addons/counterstrikesharp/configs/plugins/Playervotes/Playervotes.json`):
```json
{
  "ConfigVersion": 2,
  "General": {
    "RequiredPlayers": 2,               // Min players in the game to allow playervotes
    "DefaultPercentage": 80,            // Used by every vote type whose Percentage is 0
    "VoteDurationSeconds": 30,
    "TargetProtectionMinutes": 5,       // Same vote type can't target a player again for this long
    "AllowSelfVote": false,             // Players may start a vote against themselves and choose freely in it. false = the target's vote counts as NO
    "VoteImmunity": "@css/vip, @css/vvip",
    "HideFromMenu": "@css/root, @css/generic"    // "@css/flag" or "#group", comma separated
  },
  "Kick": {
    "Enabled": true,                    // Needs a plugin reload to take effect
    "Permission": "",                   // "@css/vip" or "#group"; empty = everyone
    "Percentage": 0,                    // 0 = General.DefaultPercentage
    "Reason": "Votekicked"
  },
  "Ban": {
    "Enabled": true,
    "Permission": "",
    "Percentage": 0,
    "Duration": 120,                    // Minutes
    "Reason": "Votebanned",
    "OfflineCommand": "css_addban {steamid64} {duration} \"{reason}\""    // Target left during a voteban that passed. Empty = cancel the vote when the target leaves
  },
  "Mute":    { "Enabled": true, "Permission": "", "Percentage": 0, "Duration": 60, "Reason": "Votemuted" },
  "Gag":     { "Enabled": true, "Permission": "", "Percentage": 0, "Duration": 60, "Reason": "Votegagged" },
  "Silence": { "Enabled": true, "Permission": "", "Percentage": 0, "Duration": 60, "Reason": "Votesilenced" },
  "EnableDebugLogs": false,
  "EnablePerfMonitor": false,
  "PerfSlowSectionMs": 2
}
```
A config from an older version (all settings at the top level) is moved into this layout automatically on load, keeping its values; the old file is saved as `Playervotes.json.v1.bak`.

---

## ⌨️ Commands
| Command         | Description                                                          | Permissions |
|-----------------|----------------------------------------------------------------------|-------------|
| !votemute       | Command for listing players and voting for mute                      | -           |
| !voteban        | Command for listing players and voting for ban                       | -           |
| !votekick       | Command for listing players and voting for kick                      | -           |
| !votegag        | Command for listing players and voting for gag                       | -           |
| !votesilence    | Command for listing players and voting for silence                   | -           |
| !yes / !no      | Vote in the current vote (same as the panel buttons)                 | -           |
| !cancelvote     | Cancel the current vote                                              | @css/generic |

---

## 📫 Contact

<div align="center">
  <a href="https://discordapp.com/users/367644530121637888">
    <img src="https://img.shields.io/badge/Discord-7289DA?style=for-the-badge&logo=discord&logoColor=white" alt="Discord" />
  </a>
  <a href="https://steamcommunity.com/id/vvernerii/">
    <img src="https://img.shields.io/badge/Steam-000000?style=for-the-badge&logo=steam&logoColor=white" alt="Steam" />
  </a>
</div>

---

## 💖 Support My Work

<div align="center">
  <a href="https://www.paypal.com/paypalme/PeliluolaCS2">
    <img src="https://img.shields.io/badge/Donate-PayPal-00457C?style=for-the-badge&logo=paypal&logoColor=white" alt="Donate via PayPal" />
  </a>
  <a href="https://buy.stripe.com/cN2dThbavflW05G7sz">
    <img src="https://img.shields.io/badge/Donate-Stripe-635BFF?style=for-the-badge&logo=stripe&logoColor=white" alt="Donate via Stripe" />
  </a>
</div>
