# Mod Features and Known bugs

## Features:

- NPC job coordination (kinda). Employees acquire jobs, so its current job target (a dropped box, or a specific storage/shelf slot) is reserved and other employees ignore it.

  With this, work gets distributed instead of everyone trying to do the same thing. Only affects storage and restock workers.

- Fix for having too many employees causing them to work slower. 
  
  This happened because of a bottleneck in the number of actions they can perform each second. 
  
  Use its setting to configure a multiplier on the actions they do.

- Configurable employee speed, but only while the store is closed.

  Works together with BetterSMT employee speed perk by applying a multiplier on top of it, though employee movement will look even goofier.

- Configurable amount of items to transfer to/from a product shelf each action. 

  From 1 (default) to 50, with an aditional option to make this happen only while the store is closed (enabled by default).

  Restocker employees are also affected by those settings.

- Configurable employee wait time after finishing a single job, or idling. 
  
  I feel like the wait after finishing a job was too high by default and it might have been done to avoid the employee actions bottleneck, more than gameplay reasons. 
  
  Idle time is fine as it is, but it can be changed too though I dont recommend it.

- When an employee searchs for a box on the floor, it gets the closest one instead of one random.


* Below are BetterSMT specific changes. BetterSMT is not required, but if detected, these extra settings show up:

	- New highlight features:
		* When you pick up a box, empty storage box slots that have the same product assigned are also highlighted.
		* Color configuration for each element (storage, storage slot, shelf, and shelf label).

	- Legacy fixes for old BetterSMT versions. These have been fixed in BetterSMT > 1.6.2 and you should update if you havent yet.
		* Fixed a bug in the storage box highlighting. It stopped working for a storage slot if you removed its box.
		* Make it so the highlighting is updated when you hold an empty box, and take a different product into it.
			

* In the settings, features are grouped into modules. Those modules can be disabled to avoid patching the base game.
  This means that if something fails in the future, you can keep enabled the features that still work, and the disabled ones that dont wont bug out the game.

## Known bugs:
	- Got a pretty ugly server desync once, while spamming box <-> shelf item transfers with a high number. No idea if it was a fluke that can happen in vanilla too or it was my mod. Need more feedback from testing.
	- "The unknown bugs are the ones that keep me up at night"