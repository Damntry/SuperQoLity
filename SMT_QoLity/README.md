# Mod Features and Known bugs

## Features:

- NPC job coordination (kinda). Employees acquire jobs, so its current job target (a dropped box, or a specific storage/shelf slot) is "marked" and other employees ignore it.
	With this, work gets distributed instead of everyone trying to do the same thing. Only affects storage and restock workers.
	
	** More improvements are coming to avoid some gaps in the logic, but its better than before in every way (aside from unknown bugs) **
- Fix for having too many employees causing them to work slower. This happened because of a bottleneck in the number of actions they can perform each second. A new setting was added to let you configure a multiplier of this number of actions.
- Configurable amount of items to transfer to/from a product shelf each action. From 1 (default) to 50, with an aditional option to make this happen only while the store is closed (enabled by default).
	Restocker employees are also affected by those settings.
- Configurable employee wait time after finishing a single job, or idling. I feel like it was too high by default and it might have been done more for performance, than gameplay reasons.
- When an employee searchs for a box on the floor, it gets the closest one instead of one random.

* Below are BetterSMT specific changes. BetterSMT is not required, but if detected, these extra settings show up:

	- New highlight features:
		* When you pick up a box, empty storage box slots that have the same product assigned are also highlighted
		* Color configuration for each element (storage, storage slot, shelf, and shelf label)

	- Fixes (Legacy code for old BetterSMT versions. These have been fixed in BetterSMT > 1.6.2)
		* Fixed a bug in the storage box highlighting. It stopped working for a storage slot if you removed its box.
		* Make it so the highlighting is updated when you hold an empty box, and take a different product into it.
			
* All features have a setting so you can completely disable its functionality, so if it fails in the future from game changes, it wont affect the game and you can keep using the features that still work.

## Known bugs:
	- Got a pretty ugly server desync once, while spamming box <-> shelf item transfers with a high number. No idea if it was a fluke that can happen in vanilla too or it was my mod. Need more feedback from testing.
	- "The unknown bugs are the ones that keep me up at night"