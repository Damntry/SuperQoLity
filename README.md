# SuperQoLity - AI, performance, and QoL changes for Supermarket Together


## Features 
- NPC job coordination. Employees wont go for the same jobs anymore.
- Many performance optimisations to employees to remove freezes. The expensive restock background method is now lighting fast and multithreaded. Idle storage workers wont freeze big stores like they used to, and more.
- Shelf/storage highlighting functionality, inherited from BetterSMT, with customizable colors. BetterSMT is not needed for this.
- Configurable employee movement speed while the store is closed. Faster, less laggy than the star perk, and you move at normal speed so you can actually do work when you want to.
- Configurable, faster item transfer to/from shelves, for players and/or employees.
- You can hold the mouse button to keep scanning items in the checkout belt
- Configurable storage priority for employees (labeled/unlabeled/any)
- Restockers will now decide which box to restock from, based on box content quantity and distance from the product shelf.
- Configurable employee wait time after finishing a job step.
- Added an employee job scheduler that lets you choose their workload performance. This is the foundation for future employee behaviour customization.
- In the Manager Blackboard, you can click anywhere on a product to add it to the shopping list, instead of just the plus button.
- Storage employees prioritise the closest box on the floor, not a random one.
- Pricing gun fixed so doubling the price no longer makes customers complain (Check 0.8.2 changelog on why I see this as a bug).
- And tons of my own bugs that I missed.

#### All features are optional

#### - Multiplayer

This mod is multiplayer ready. If a setting has any multiplayer requirements (only hosts, only clients, etc), its description will tell you so.

By design, and to promote fair play, the host has control over some of the allowed features. If the host doesnt have the mod, or it has the feature disabled in the settings, the client wont be able to use it.
<br />

#### - About the settings

All settings have base game values by default. You need to change them to enable (most, see below*) features.<br />
If you dont have an in-game config manager, I recommend using the BepInEx5 version of BepInEx.ConfigurationManager, as it lets you change settings in-game:<br />
https://github.com/BepInEx/BepInEx.ConfigurationManager/releases

*<sub>Some features do not have an individual setting, and instead depend on its module being enabled. Since modules themselves are enabled by default, those will be active once you start the game. Examples of this are NPC coordination and Highlighting)</sub>

#### - Known bugs:
None, but the unknown are the ones that keep me up at night.