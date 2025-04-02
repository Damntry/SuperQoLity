# SuperQoLity - AI, performance, and QoL changes for Supermarket Together


## Features 
- NPC job coordination. Employees wont go for the same jobs anymore.
- Shelf/storage highlighting functionality, inherited from BetterSMT, with customizable colors. BetterSMT is not needed for this.
- Configurable employee movement speed while the store is closed. Faster and less laggy than the star perk.
- Configurable, faster item transfer to/from shelves, for players and/or employees.
- Many small and not so small performance optimisations to employees (more to come).
- You can hold the mouse button to keep scanning items in the checkout belt.
- Implemented an employee job scheduler that lets you choose their workload performance.
- Configurable storage priority for employees (labeled/unlabeled/any)
- Configurable employee wait time after finishing a job step.
- Pricing gun fixed so doubling the price no longer makes customers complain (Check 0.8.2 changelog on why I see this as a bug)
- In the blackboard, you can click anywhere on a product to add it to the shopping list, instead of just the button.
- Storage employees prioritise the closest box on the floor, instead of random one.
- And tons of my own bugs that I missed.

#### All features are optional

#### - Multiplayer

This mod is multiplayer ready. If a setting has any multiplayer requirements (only hosts, only clients, etc), its description will tell you so.

By design, and to promote fair play, the host has control over some of the allowed features. If the host doesnt have the mod, or it has the feature disabled in settings, the client wont be able to use it.
<br />

#### - About the settings

All settings have base game values by default. You need to change them to enable (most, see below*) features.<br />
If you dont have an in-game config manager, I recommend using the BepInEx5 version of BepInEx.ConfigurationManager, as it lets you change settings in-game:<br />
https://github.com/BepInEx/BepInEx.ConfigurationManager/releases

*<sub>Some features do not have an individual setting, and instead depend on its module being enabled. Since modules themselves are enabled by default, those will be active once you start the game. Examples of this are NPC coordination and Highlighting)</sub>

#### - Known bugs:
None, but the unknown are the ones that keep me up at night.