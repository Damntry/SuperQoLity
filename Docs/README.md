# <p align="center"> SuperQoLity </p>
## <p align="center"> AI, performance, and QoL changes for Supermarket Together </p>
<br>

## <p align="center"> 🛠️ Features 🛠️ </p>

**Every feature is optional and disabled by default:**
<br><br>
 - **NPC job coordination**. 
	 - Employees wont choose the same jobs (Manufacturing pending).
	 - Restockers do less and shorter trips with an improved box choosing logic.
	 - Fixed game bug where products were being put in wrong shelves when other shelves are deleted.
 - **Performance optimizations**:
	 - The job choosing logic for restockers is faster, multithreaded, and takes better decisions.
	 - Idle storage workers wont cause stutters on big stores.
	 - A configurable job scheduler to balance CPU usage vs employee/customer responsiveness.
	 - And too many all around to add here.
 - **Stuck customers fix**
	- They can now free themselves when trapped.
 - **Product highlighting**, 
	- 4 visual modes to choose from, with customizable colors and brightness.
	- Highlights shelves, shelf labels, and boxes on the ground or being held.
	- Enabled when picking up a box, or with a hotkey.
 - Configurable employee move speed when the store is closed. Faster and smoother than the star perk, and you move at normal speed so you can do work too.
 - Configurable, faster shelf product placing, for players and/or employees.
 - Broom shotgun!
 - Quickly equip any tool using a visual equipment wheel and optional hotkeys. Tools wont cheat themselves in, there must be a free one on the map or organizer.
 - Restrict customers to only shop for products that had been assigned to shelves, instead of every unlocked product. 
	- Enables shop specialization, and customers will still complain if the assigned product runs out of stock.
 - Security employee improvements:
	 - They can pick up multiple dropped products in an area around them, as they level up.
	 - Configurable thief-chasing behaviour.
	 - Nearest available guard will chase thieves, and not one at random.
 - Immersion tweaks:
	 - Employees now try to look at the object they are interacting with.
	 - Security employees look around while standing guard.
 - You can hold the mouse button to keep scanning products in the checkout belt.
 - Configurable employee wait time after finishing a job step.
 - Hotkeys to build or move the buildable you are looking at.
 - Adjustable % of customers paying with card vs cash.
 - Configurable storage priority for employees (labeled/unlabeled/any).
 - In the Manager Blackboard, you can click anywhere on a product panel to add it to the shopping list.
 - Storage employees prioritise the closest box on the floor, not a random one.
 - Setting to increase the limit of buildables and decorations that the game can load, from 5000 to 100000.
 - Customers wont complain when doubling the market price with the pricing gun (Check 0.8.2 changelog for why I see this as a bug).
 - And tons of my own bugs that I missed and you ll learn to love.

---

## <p align="center"> [<ins> 📙 Manual Installation Guide 📙 </ins>](https://www.nexusmods.com/supermarkettogether/articles/33) </p>

---

## <p align="center"> 🌐 Multiplayer 🌐 </p>
This mod is 100% multiplayer ready. If a setting has any multiplayer details or requirements, its description will tell you so.

By design, and to promote fair play, the host has control over some of the allowed features. If the host doesnt have the mod, or it has the feature disabled in the settings, the client wont be able to use it.

---

## <p align="center"> ⚙️ Settings ⚙️ </p>
All settings have, by default, base game values. You need to enable them to activate features (except npc coordination always being active when the employee module is enabled).
If you dont have an in-game config manager, I very much recommend [using the BepInEx5 version of BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager/releases), as it lets you change settings in real time.  

---
BetterSMT is 100% compatible. Note that its "Quick stocking" and "Quick removing" settings have priority over my item transfer feature.

Many thanks to Mitche (kinda), Ika, and many others that helped get this thing out at the unofficial SMT discord.

---
### <p align="center"> [<ins> 📝 Full changelog 📝 </ins>](https://github.com/Damntry/SuperQoLity/blob/master/Docs/Changelog.md) </p>
<br>

**Want to get in contact with me, request mod ideas, or get some help in your modding journey? [Come join us at the semi-official SMT modding discord](https://discord.gg/qEZpeJqppY).**

<br>

For suggestions or bug reports, you can use the [Issues section on GitHub](https://github.com/Damntry/SuperQoLity/issues).

Like my work and want to support me or buy me a coffee?  

[Well, too bad, because you cant.](#) 

You though it would be a real link didnt you?

---

## <p align="center"> 💀 Known bugs 💀 </p>
- Clients may still see products dropped from thieves even after a security employee picks it up.
For the host those products wont exist, but the client can still pick them up, so it is in practice money duping, though rare enough not to matter.
This is a limitation due to network traffic. I plan to fix it for clients that have SuperQoLity installed, but Im studying what can be done for unmodded clients.  


And the unknown bugs are the ones that still keep me up at night.