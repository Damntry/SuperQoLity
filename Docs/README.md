# <p align="center"> SuperQoLity </p>
## <p align="center"> AI, performance, and QoL changes for Supermarket Together </p>
<br>

## <p align="center"> 🛠️ Features 🛠️ </p>
 - NPC job coordination: restock and storage employees wont go for the same jobs anymore.
 - Many performance optimisations to employee logic to eliminate or reduce game stuttering:
	 - The expensive restock background method is now lighting fast and multithreaded. 
	 - Idle storage workers wont freeze big stores like they used to.
	 - A configurable employee job scheduler that lets you choose their CPU usage/responsiveness ratio.
	 - And too many all around to add here.
 - Shelf/storage highlighting functionality, inherited from BetterSMT, with customizable colors. BetterSMT is not needed for this.
 - Configurable employee speed when the store is closed. Faster and smoother than the star perk, and you move at normal speed so you can do work too.
 - Configurable, faster item transfer to/from shelves, for players and/or employees.
 - Security now picks up more products in an area around them, based on their level. 
	 - At level 1, they grab 1 product in a small area. Every 5 levels (5, 10, 15, etc) they can take 1 more. The area increases slightly every level.
	 - This mechanic can be disabled/changed in the settings.
 - Began adding a few changes to improve a bit immersion and polish:
	 - Employees will now try and look at the object they are interacting with. Customers are planned later.
	 - Security employees will now look around while standing guard.
 - You can hold the mouse button to keep scanning items in the checkout belt.
 - Configurable employee wait time after finishing a job step.
 - Configurable storage priority for employees (labeled/unlabeled/any).
 - Restockers will now choose a storage box based on box content quantity and distance from the product shelf, to save trip time.
 - In the Manager Blackboard, you can click anywhere on a product to add it to the shopping list, and not just the small plus button.
 - Storage employees prioritise the closest box on the floor, not a random one.
 - Pricing gun fixed so doubling the price no longer makes customers complain (Check 0.8.2 changelog for why I see this as a bug).
 - And tons of my own bugs that I missed.

**All features are optional.**

---

## <p align="center"> [<ins> 📙 Manual Installation Guide 📙 </ins>](https://www.nexusmods.com/supermarkettogether/articles/33) </p>

---

## <p align="center"> 🌐 Multiplayer 🌐 </p>
This mod is 100% multiplayer ready. If a setting has any multiplayer details or requirements, its description will tell you so.

By design, and to promote fair play, the host has control over some of the allowed features. If the host doesnt have the mod, or it has the feature disabled in the settings, the client wont be able to use it.

---

## <p align="center"> ⚙️ Settings ⚙️ </p>
All settings have, by default, base game values. You need to enable them to activate most features (see below **).  
If you dont have an in-game config manager, I very much recommend [using the BepInEx5 version of BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager/releases), as it lets you change settings in real time.  

- <sub>** A few features do not have an individual setting, and instead depend on their "parent" module setting. Since modules themselves are enabled by default, those features will be too. Examples of this are NPC coordination and Highlighting.</sub>

---
BetterSMT is 100% compatible. Be aware that its highlighting settings wont change anything since mine took over.

Many thanks to Mitche (kinda), Ika, and many others that helped get this thing out at the unofficial SMT discord.

---
### <p align="center"> [<ins> 📝 Full changelog 📝 </ins>](https://github.com/Damntry/SuperQoLity/blob/master/Docs/Changelog.md) </p>
For suggestions or bug reports, you can use the [Issues section on GitHub](https://github.com/Damntry/SuperQoLity/issues).

<br>

Like my work and want to support me or buy me a coffee?  

[Well, too bad, because you cant.]() 

You though it would be a real link didnt you?

---

## <p align="center"> 💀 Known bugs 💀 </p>
- Clients may still see products dropped from thieves, even after a security employee picks it up.
For the host those products wont exist, but the client can still pick them up, so it is in practice money duping.
This is a limitation due to network traffic. I plan to fix it for clients that have SuperQoLity installed, but Im studying what can be done for unmodded clients.  


And the unknown bugs are the ones that still keep me up at night.