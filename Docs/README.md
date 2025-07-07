# SuperQoLity - AI, performance, and QoL changes for Supermarket Together


## - Features 
- NPC job coordination. Restock & storage employees wont go for the same jobs anymore.
- Many performance optimisations to employe logic to remove game stuttering:
	- The expensive restock background method is now lighting fast and multithreaded. 
	- Idle storage workers wont freeze big stores like they used to.
	- A configurable employee job scheduler that lets you choose their CPU usage/responsiveness ratio.
	- And too many all around to add here.
- Shelf/storage highlighting functionality, inherited from BetterSMT, with customizable colors. BetterSMT is not needed for this.
- Configurable employee movement speed while the store is closed. Faster, less laggy than the star perk, and you move at normal speed so you can actually do work when you want to.
- Configurable, faster item transfer to/from shelves, for players and/or employees.
- You can hold the mouse button to keep scanning items in the checkout belt.
- Configurable storage priority for employees (labeled/unlabeled/any).
- Restockers will now choose a storage box based on box content quantity and distance from the product shelf, to save trip time.
- Configurable employee wait time after finishing a job step.
- In the Manager Blackboard, you can click anywhere on a product to add it to the shopping list, and not just the small plus button.
- Storage employees prioritise the closest box on the floor, not a random one.
- Pricing gun fixed so doubling the price no longer makes customers complain (Check 0.8.2 changelog for why I see this as a bug).
- And tons of my own bugs that I missed.

#### **All** features are optional.

---

### - Multiplayer

This mod is 100% multiplayer ready. If a setting has any multiplayer details or requirements, its description will tell you so.

By design, and to promote fair play, the host has control over some of the allowed features. If the host doesnt have the mod, or it has the feature disabled in the settings, the client wont be able to use it.

---

### - Settings

All settings have, by default, base game values. You need to change them to enable (most, see below*) features.<br />
If you dont have an in-game config manager, I very much recommend using the BepInEx5 version of BepInEx.ConfigurationManager, as it lets you change settings in-game:<br />
https://github.com/BepInEx/BepInEx.ConfigurationManager/releases

* <sub>A few features do not have an individual setting, and instead depend on their "parent" module setting. Since modules themselves are enabled by default, those features will be too. Examples of this are NPC coordination, and Highlighting.</sub>

---

BetterSMT is 100% compatible. Be aware that its highlighting settings wont change anything since mine took over.

For a full changelog of the mod: <br/>
https://github.com/Damntry/SuperQoLity/blob/master/Changelog.md

Many thanks to Mitche (kinda), Ika, and many others that helped get this thing out at the unofficial SMT discord.

---

### - Known bugs:
None, but the unknown are the ones that keep me up at night.