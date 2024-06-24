Hi! Welcome to the Outlaw Mod Readme and thanks for downloading.

As of 1.1.0 Outlaw Mod has a config file now! 
For the uninitiated, the config file will appear in your VintageStoryData/ModConfig AFTER you run your game and load a map with the mod loaded for the first time.

If you can't find it there, look in VintageStory/ModConfig.

Every time you install a new version of the mod, make sure you look at your config, as options may have changed/moved around, 
I can't promise your settings will be unaltered by the update, so just give it a quick look over so we can both sleep at night.
If you are experiencing any kind of weird behavior, always check your config first, as a rule of thumb.

So what's in the config?

//ENABLE/DISABLE OUTLAW SPAWNING BY TYPE
EnableLooters: true/false
EnablePoachers: true/false
EnableBrigands: true/false
EnableYeomen: true/false
EnableDeserters: true/false	
EnableBannermen: true/false 	(This only disables Deserter Bannermen, who lead large groups of deserters into battle)
EnableFeralHounds: true/false
EnableHuntingHounds: true/false

//START SPAWN SAFE ZONE SETTINGS
So OutlawMod adds a zone around your start spawn in your survival world that blocks Outlaw spawns. This is so you can get a foothold in the world and start doing your thing.
By default, this zone starts at a specified size and shrinks at a constant rate over a set number of days until there's no safe zone left, unless you configure it to do something else.

StartingSpawnSafeZoneRadius: The Starting Radius of the Safe Area, measured in blocks.
StartingSafeZoneHasLifetime: Whether the zone has a lifetime and will despawn after a time. Effectively, do you want the safe zone to go away at some point, yes or no?
StartingSafeZoneShrinksOverLifetime: Whether the zone will shrink over the course of its lifetime. This means Outlaws will appear closer and closer to the start as the days progress.
StartingSpawnSafeZoneLifetimeInDays: The number of days it will take for the safe zone to shrink and disappear entirely. If StartingSafeZoneHasLifeTime = false, this value will do nothing.
ClaminedLandBlocksOutlawSpawns: Whether or not land claim areas block Outlaw spawns.

//CLASSIC VINTAGE STORY VOICES
By default, Outlaws will use a variety of recorded voice lines. Classic Vintage Story Voices, like the ones the traders use, are also supported. Each type of Outlaw uses its own instrument so you can tell them apart from a distance.

OutlawsUseClassicVintageStoryVoices: Set this to true if you want Classic Vintage Story Voices for Outlaws.

//STEALTH SYSTEM
SneakAttackDamageMultRanged: The amount to multiply attack damage when an unaware enemy is hit with a ranged weapon.
SneakAttackDamageMultMelee: The amount to multiply attack damage when an unaware enemy is hit with a melee weapon.

//DEVELOPER STUFF
This stuff is really just for me to work on the Mod, it's not super exciting, sorry.

DevMode: configures a bunch of stuff on the back end so I can test and debug certain things more easily. I really don't recommend enabling this, unless you're me.