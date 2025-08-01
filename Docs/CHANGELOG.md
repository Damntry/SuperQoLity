## Changelog
### 0.8.7.6
	- New setting to control how security employees chase thieves, instead of all rushing the first one.
	- When a thief is found, the closest idle security employee will be assigned to chase him, instead of one random.
	- Added employee changes from last base game update
		- Online order employees will now drop empty boxes instead of putting them back in storage.
		- Slowed down cashier leveling.
### 0.8.7.0
	- Employees will now try and look in the direction of the object they are interacting with. 
		This will not slow them down, its only a visual change that cancels once they have a new move order.
		Customers are planned later.
	- Higher leveled security employees will now be able to pick up more than 1 stolen product from the ground, and in a bigger area around them.
		At first they can only pick up a single product in a very small area. Every extra level, the area increases very slightly, and every 5 levels (5, 10, 15, etc) they can take an extra product.
		This can be disabled or modified in the settings.
	- Fixed elusive error that sometimes made restockers not work, even though there were product shelves with missing products that were available in storage.
	- Fixed fuck up where security employees were not working at night.
	- Added fixes from previous updates of the base game that I needed to add manually:
		Technician employees receiving too little xp from their work. This wasnt fixed in the base game yet, but probably on their next update
		Packagers employees experience was being added to restockers instead.
	- Lots of reorganization and clean up of employee code, let me know if something I missed breaks catastrophically.
### 0.8.5.5
	- Fucked up my previous patch and cashiers werent working. Fixed now.
### 0.8.5.4
	- Updated employee logic to add the packager (for online orders) from last update.
### 0.8.5.0
	- Updated employee logic to add the technician from last update.
### 0.8.4.7
	- Updated baler logic to avoid exception introduced with last update.
### 0.8.4.5
	- Added missing logic from last update: 
		* Achievement to recycle 40 cardboard bales.
		* All security employees pick up ground products if no customers are left at closing hours.
### 0.8.4.0
	- Added Carboard Baler logic from last update to employees, so they make use of it.
	- Potential fix for an employee restocking problem that made them not restock sometimes. Contact me if you still get this bug so I can find how it happens.
	- Made the expensive restock background method lighting fast and multithreaded! Much faster than base game now, no longer will it bog down the rest of the game.
	- The above change made it so I can now do resource-heavy logic that wasnt viable before, and still have plenty to spare. Now restockers will decide which box to restock from, based on box content quantity and distance from the product shelf.
	- Fixed yet another exception that happened when the setting "Hold click to scan checkout products" was disabled.
### 0.8.3.2
	- Fixed exception happening after new SMT update that made restockers not work.
	- Fixed exception that happened when the setting "Hold click to scan checkout products" was disabled.
### 0.8.3.0
	- Requested by Aretha Cairn: You can now hold the mouse button to scan items in the checkout belt.
		It should make easier to play cashier for long periods of time.
	- Requested by Aretha Cairn: New setting to expand the clickable area of the product order button in the blackboard, so it can be clicked (almost) anywhere in its product square.
	- Added support for "Custom Products" mod to avoid an exception when loading the game.
	- Removed debug log from the pricing gun fix that I forgot to remove. Oh no.
#### 0.8.2.0
	- Pricing gun fixed so doubling the price no longer makes customers complain.
		This one needs a bit of an explanation on why I consider it a bug:

		A customer only complains if something is over 2 times the price.
		Yet when you double an item s market price with the pricing gun, sometimes customers still complain. 
		Why? Because the market price is a lie: The pricing gun might show you a market price of 1.25, so you would think 2.5 should be fine. 
		In reality its something like 1.2453, which doubled results in 2,4906. 
		When a customer comes and checks the price, 2.5 is greater than 2,4906, so they complain.

		All of this only happens depending on calculated market price. Some products are fine with a 200% markup and a certain inflation value, but then comes inflation day and setting the same product to 200% gets complains.
		This inconsistency is what makes it a bug, though a pretty small one.

		The pricing machine doesnt have this problem (even if you dont round down 1 cent), but not because it is doing hidden magic, but because it does calculations based on what is being shown.
			
		I changed it so what you see is what you get. If the pricing gun tells you the market price is 1.25, it really is. 
		Now you can trust the pricing gun and stop doing -1 cent on everything.

		At least until they change it to something else. Or I do.
	- Reworked the entire mod network communication. Tell me if something goes wrong with item transfer not using the host's value.
#### 0.8.1.0
	- Main restocker process optimized and shouldnt stutter anymore, alike to base game.
		Much better optimizations are already planned to make it not just faster than base game, but also have restockers find jobs sooner.
	- Fixed higher leveled employees sometimes putting more items in product shelves than what should fit.
	- Fixed that when an employee was on its way to pick up a box from storage, a player removing or adding items into it would make the employee still think it had the old amount in it.
#### 0.8.0.6
	- Fixed cache exception happening after the "Electronics II + Decoration" game update.
	- Fixed setting "Maximum workload reduction per cycle" showing default as a negative value.
#### 0.8.0.5
	- Fixed employees recycling only at the furthest recycle bin.
	- Fixed some "Host only" settings keeping previous values when you were a client, instead of going default.
	- Fixed Dev mode always disabled at game start.
	- Fixed logging not activated when Dev mode was enabled while in-game.
	- Currently, the only thing slower than base game is the main restocker process.
		I made it a bit faster for now, but I expect to finish the complete solution to this for the next update. 
		It should be a pretty decent optimization in big stores.
#### 0.8.0.2
	- Fixed bug introduced in 0.8.0.1 where if a reserved storage target wasnt valid anymore, the NPC would have no destination.
	- Added that whenever an storage worker destination was invalid, the employee looks for new storage instead of directly going to drop the box at the left over spot
#### 0.8.0.1
	- Fixed bug where box items would be lost if there were more than 2 boxes to merge by storage workers.
#### 0.8
	- Initial GitHub commit. Who knows what changed before? Not me, fish memory.
