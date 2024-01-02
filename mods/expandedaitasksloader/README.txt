Hi! Welcome to the Expanded Ai Tasks Loader Mod Readme and thanks for downloading.

This mod acts as a shared loader for the ExpandedAiTasks.dll that accompanies it. 
The .dll adds aiTasks that I use to develop my mods that the base game doesn't support natively.

Getting Started:

========================================================================================================================

FOR PLAYERS:
1. Just put the zip file in your mods folder, like any other mod.
2. You're good to go!

========================================================================================================================

FOR MODDERS:
If you're planning on integrating tasks from ExpandedAiTasks.dll into your mod there are two ways of doing it: 
	1. Using this Loader Mod.
	2. Doing your own .dll integration.

There are pros and cons to both approaches:
	The Loader Mod Method:
	In this method, you let this mod do the heavy lifting for you. You simply reference the AiTasks it registers.
		Pros:
			1. If you use this loader mod, you don't have to write any code, your mod can just reference the AiTasks in your .json files and it will work.
			2. You don't have to worry about compatibility with any other mod that also uses the loader mod, since all the AiTasks are registered in the same place.
		Cons:
			1. Your mod won't work properly if players don't have the ExpandedAiTasksLoader mod installed. You'll have to point people to this mod and tell them to download it so
			they will have the AiTasks registered when you reference them. (i.e. Your mod won't be a standalone mod).

	Doing your own .dll integration:
	In this method, You include the ExpandedAiTasks.dll in your project and register it's AiTasks in your Core ModSystem.
		Pros:
			1. Your mod is totally self-contained and people won't need to download the loader for your mod to work.
		Cons:
			1. You need to know enough code to integrate the dll. (See next section).

========================================================================================================================

Integrating ExpandedAiTasks.dll into your mod

	1. Add ExpandedAiTasks.dll as a reference to your project.
	2. In your Core ModSystem .cs file include 'using ExpandedAiTasks;'
	3. In your Core ModSystem class, in the public override void Start(ICoreAPI api) function:
		a. Call ExpandedAiTasksDeployment.Deploy(api);
		
		Example:
		public override void Start(ICoreAPI api)
        	{
            		base.Start(api);
            		ExpandedAiTasksDeployment.Deploy(api);
        	}

	4. At this point, you should now be able to reference this AiTask in your .json files.
	5. When packaging your mod, do not forget to include ExpandedAiTasks.dll in your zip file (Not to be confused with ExpandedAiTasksLoader.dll)

========================================================================================================================

AI Tasks Documentation:

========================================================================================================================
	AiTaskShootProjectileAtEntity ( registered as "shootatentity"" )
		
		This AiTask tells an Ai to shoot a specified projectile at a target with the specified settings.

		Note: This system can lead moving target and arc shots properly over distance to account for gravity. 
		HOWEVER! It is important to note that this will not work properly unless you use a projectile with very specific PassivePhysics Behavior settings.
		The projectile must have a gravityFactor of 1.0 and a airDragFactor of 0.0, which none of the vanilla projectiles have.
		To accomplish this, you must create a dummy projectile entity that the Ai will fire. If you want the projectile to be a vanilla item when picked up set the projectileItem field to a vanilla projectile code.
		An example asset of dummyarrow.json is included in this zip.
		 

	json Settings

		int durationMs: The duration of the ranged attack in milliseconds.
		int releaseAtMs: The time at which the projectile is fired in milliseconds.
		float minDist: The minimum distance at which the attack can occur (measured in blocks).
		float maxDist: The maximum distance at which the attack can occur (measured in blocks).
		float minRangeDistOffTarget: The projectile's maximum distance off target at minimum range (measured in blocks).
		float maxRangeDistOffTarget: The projectile's maximum distance off target at maximum range (measured in blocks).
		float maxVelocity: The absolute maximum velocity of the projectile.
		float newTargetDistOffTarget: The projectile's maximum distance off target when the shooter has just acquired a new target (measured in blocks).
		float newTargetZeroingTime: The time (in seconds) over which a shooter zeros his aim and newTargetDistOffTarget lerps to a value of 0.0. 
		float damage: How much damage the attack deals when the projectile hits its target.
		float damageFalloffPercent: The percent of the damage value that is retained at damageFalloffEndDist.
		float damageFalloffStartDist: The distance at which damage begins to falloff (measured in blocks).
		float damageFalloffEndDist: The distance at which damage is fully reduced by damageFalloffPercent and will not falloff further (measured in blocks).
		string projectileItem: The code of the item stack for the dummy projectile.
		sting dummyProjectile: The code of a custom dummy projectile that has a gravityFactor of 1.0 and an airDragFactor of 0.0 in its PassivePhysics behavior. leadTarget and arc shots will not function unless the dummy projectile is configured properly.
		bool projectileRemainsInWorld: Determines whether or not the projectile will remain in the world after impacting a surface.
		float projectileBreakOnImpactChance: The percentage chance that a projectile will break when impacting a surface is projectileRemainInWorld is true.
		bool stopIfPredictFriendlyFire: If true the Ai will interrupt its ranged attack if it detects a friendly herd member running into the line of fire.	
		bool leadTarget: If true, the Ai will lead and fire ahead of their target based on their target's movement velocity.
      		bool arcShots: If true, if the initial velocity of the projectile is too slow to fire directly at the target, the Ai wil arc the shot upwards to compensate for gravity.	

		Use Example from OutlawMod, yeoman-archer.json
		{
			code: "shootatentity",
			entityCodes: ["player", "drifter-*", "wolf-male", "wolf-female", "hyena-male", "hyena-female", "locust-*", "bear-*", "looter", "hound-feral"],
			priority: 3.75,
			priorityForCancel: 9,
			mincooldown: 1000, 
			maxcooldown: 1500, 
			minDist: 4,
			maxDist: 40,
			maxVertDist: 30,
			minRangeDistOffTarget: 0.25,
			maxRangeDistOffTarget: 0.75,
			maxVelocity: 0.9,
			newTargetDistOffTarget: 0.75,
			newTargetZeroingTime: 5.0,
			damage: 4.0,
			damageFalloffPercent: 0.66,
			damageFalloffStartDist: 18,
			damageFalloffEndDist: 28,
			projectileItem: "arrow-copper",
			dummyProjectile: "dummyarrow-copper",
			projectileRemainsInWorld: true,
			projectileBreakOnImpactChance: 0.90,
			stopIfPredictFriendlyFire: true,
			leadTarget: true,
      			arcShots: true,
			durationMs: 2000,
			releaseAtMs: 1000,
			animationSpeed: 1.0,
			animation: "bowattack"			
		},


		Example of a Dummy Projectile that is properly configured to work with leadTarget and arcShots
		{
			code: "dummyarrow",
			class: "EntityProjectile",
			variantgroups: [
				{ code: "material", states: ["crude", "flint", "copper", "tinbronze", "bismuthbronze", "blackbronze", "gold", "silver", "iron", "steel", "meteoriciron" ] },
			],
			hitboxSize: { x: 0.125, y: 0.125 },
			client: {
				size: 0.75,
				renderer: "Shape",
				shapeByType: { 
					"dummyarrow-crude": { base: "entity/arrow/crude" },
					"dummyarrow-flint": { base: "entity/arrow/stone" },
					"dummyarrow-gold": { base: "entity/arrow/gold" },
					"dummyarrow-silver": { base: "entity/arrow/silver" },
					"*": { base: "entity/arrow/copper" }
				},
				texturesByType: {
					"dummyarrow-crude": {

					},
					"dummyarrow-flint": {
						"material": { base: "block/stone/flint" }
					},
					"*": {
						"material": { base: "block/metal/ingot/{material}" }
					}
				},
				behaviors: [
					{ code: "passivephysics",
						groundDragFactor: 1,
						airDragFactor: 0.0,
						gravityFactor: 1.0
					}, 
					{ code: "interpolateposition" }
				],
			},
			server: {
				behaviors: [
				{	 
					code: "passivephysics",
					groundDragFactor: 1,
					airDragFactor: 0.0,
					gravityFactor: 1.0
				}, 
				{ code: "despawn", minSeconds: 600 }
				],
			},
			sounds: {
			}
		}

========================================================================================================================
	AiTaskEatDeadEntities( registered as "eatdead")

		This AiTask tells an Ai to seek out and eat dead creatures in the environment. When it is done eating, the entity that has been eaten will behave as it has been harvested by a player.
		
	json Settings

		float moveSpeed: The speed at which the ai will travel towards its eat target.
            	string moveAnimation: The name of the animation the ai should play when moving to its eat target.
            	float minDist: The distance at which the ai will detect an eat target that hasn't started decaying.
            	float maxDist: The distance at which the ai will detect an eat target that is 99.99% decayed.
            	float eatDuration: The time, in seconds, that it takes for this ai to consume and eat target.
            	string eatAnimation: The name of the animation to play while eating.
            	float eatAnimMinInterval: The min interval, in seconds, between the eat animation playing.
            	float eatAnimMaxInterval: The max interval, in seconds, between the eat animation playing.
            	bool eatEveryting: If true, the ai will eat any dead entity it can find in the world.
            	bool allowCannibalism: If true, the ai will eat dead members of it's own herd.

		Use Example from OutlawMod, hound-feral.json
		{
			code: "eatdead",
			entityCodes: ["player", "chicken-*", "hare-*", "fox-*", "pig-*", "raccoon-*", "sheep-*", "bandit-*", "poacher-*", "yeoman-*", "looter", "hound-hunting"],
			priority: 1.52,
			moveSpeed: 0.006,
			moveAnimation: "walk",
			minDist: 15.0,
			maxDist: 30.0,
			eatDuration: 60.0,
			eatAnimation: "eat",
			eatAnimMinInterval: 1.0,
			eatAnimMaxInterval: 3.5,
			eatEveryting: true,
			allowCannibalism: true,
			whenNotInEmotionState: "saturated"
		},
========================================================================================================================
	AiTaskExpandedMeleeAttack( registered as "melee")
	
		This AiTask tells an Ai to deal melee damage to a target creature. Comes with additional functionality not found in vanilla melee such as 
		friendly-fire protection.
	
	json Settings
		
		float damage: amount of damage to deal on a hit.
            	float knockbackStrength: amount of knockback force to apply to a target on hit.
            	float attackDurationMs: total attack duration in miliseconds.
            	float damagePlayerAtMs: time into the attack in miliseconds at which damage should be applied to target.
		float minDist: the distance at which the attack can be executed.
            	float minVerDist the vertical distance at which the attack can be executed.
            	string damageType: the type of damage to apply.
            	int damageTier: the attack's damage tier.

	Use Example from OutlawMod, bandit-knife.json
	{
		code: "melee",
		entityCodes: ["player", "drifter-*", "wolf-male", "wolf-female", "hyena-male", "hyena-female", "locust-*", "bear-*", "looter", "hound-feral"],
		priority: 4.0,
		damage: 2.5,
		damageTier: 1,
		damageType: "SlashingAttack",
		slot: 1,
		mincooldown: 500, 
		maxcooldown: 1500, 
		attackDurationMs: 450,
		damagePlayerAtMs: 300,
		animation: "attack",
		animationSpeed: 1,
	},
========================================================================================================================
	AiTaskGuard (registered as "guard")
	
		This AiTask tells an Ai to guard a friendly target set by AiUtility.SetGuardedEntity(Entity ent, Entity entToGuarded)
		Once set, the Ai will follow its guard target and attack any enemy that attempts to harm it. 

		Note: AiTaskGuard relies on AiTaskExpandedMeleeAttack, AiTaskShootProjectileAtEntity, and AiTaskPursueAndEngage to carry out its attack.
		AiTaskGuard should alway be higher priority than these AiTasks to get the desired behavior.

	json Settings

		float detectionDistance: The max range at which the Ai can detect a thread to its guard target.
            	float maxDistance: The max distance the Ai can stray from its guard target.
            	float arriveDistance: The distance at which the Ai is considered to have returned to its guard target after straying from it.
            	float moveSpeed: The Ai's move speed when following its guard target.
            	float guardAgroDurationMs: The duration of time the Ai should attack an agressor before returning to its guard target.
            	float guardAgroChaseDist: The max distance an Ai can chance an agressor away from its guard target before returning to follow mode.
		bool guardHerd: If true the Ai will select a guard target from among its herd members, use entityCodes to specify valid entities to guard. If the guard target is killed, the Ai will try to find a replacement within its herd.
            	bool aggroOnProximity: If true the Ai will attack if a player gets within aggroProximity of the guard target. The player does not need to be agressive to trigger an attack.
            	float aggroProximity: How close a player can get to a guard target before the Ai attacks, requires that aggroOnProximity is true.

	Use Example from OutlawMod, hound-hunting.json
	{
		code: "guard",
		entityCodes: ["poacher-*"],
		priority: 2.5,
		detectionDistance: 40,
           	maxDistance: 6,
            	arriveDistance: 4,
            	moveSpeed: 0.048,
		guardAgroDurationMs: 30000,
		guardAgroChaseDist: 40,
        	guardHerd: true,
            	aggroOnProximity: true,
            	aggroProximity: 6,
		animation: "Run",
		animationSpeed: 1.5
	},
//These Ai Tasks must be included, as AiTaskGuard delegates to them to handle the agressive part of the guard behavior.
	{
		code: "melee",
		entityCodes: ["player", "chicken-*", "hare-*", "fox-*", "pig-*", "raccoon-*", "sheep-*", "bandit-*", "yeoman-*", "looter", "hound-feral"],
		priority: 2,
		damage: 4,
		damageTier: 1,
		damageType: "SlashingAttack",
		slot: 1,
		mincooldown: 1500, 
		maxcooldown: 1500, 
		attackDurationMs: 800,
		damagePlayerAtMs: 500,
		animation: "Attack",
		animationSpeed: 2.5,
		sound: "creature/wolf/attack"
	},
	{
		code: "engageentity",
		entityCodes: ["player", "chicken-*", "hare-*", "fox-*", "pig-*", "raccoon-*", "sheep-*", "bandit-*", "yeoman-*", "looter", "hound-feral"],
		priority: 1.5,		
		maxTargetHealth: 5.0,
		packHunting: "false",			
		pursueSpeed: 0.048,
		pursueRange: 15,
		pursueAnimation: "Run",
		engageSpeed: 0.035,
		engageRange: 4,
		engageAnimation: "Run",
		withdrawIfNoPath: false,
		animationSpeed: 2.2,
		alarmHerd: true,
		sound: "creature/wolf/growl",
	},
========================================================================================================================		
	AiTaskMorale (registered as "morale")

		This AiTask evaluates battlefield conditions and tells the Ai when to flee combat.
		The Ai will have a morale level that is between minMorale and maxMorale, if this value is exceeded by fear level, they will flee the battle.
		Note: This AiTask should always be top priority, or it will not function properly.

	json Settings
		float moveSpeed: The speed at which the Ai will flee the battle.
            	bool cancelOnHurt: If true the Ai will stop running when it takes damage.
           	float routDistance: The distance, in blocks, that the Ai will run from the combatant it is fleeing.
            	float rallyTimeAfterRoutMs: The time, in miliseconds, it will take for the Ai to stop fleeing and attempt to rejoin combat.
            	double minMorale: The lower bound of this Ai's possible morale (Typically 0 to 1).
            	double maxMorale: The upper bound of this Ai's possible morale (Typically 0 to 1).
            	float moraleRange: The range, in block, in which things will impact the Ai's morale.
            	bool useGroupMorale: If true, that Ai will consider the strength of its group (using total health levels) to determine its base morale.
            	bool deathsImpactMorale: If true, the Ai will take nearby deaths into account when determining its fear level. Things that scare the Ai while they are alive, will make them more confident when they are dead and things that make the Ai more confident while they are alive will scare the Ai when they are dead.
            	bool canRoutFromAnyEnemy: If true, the Ai will route from any opponent that scares them enough, other than their own herd members. Otherwise, only combatants specifed in entityCodes will scare the Ai.
		TreeAttribute entitySourcesOfFear: A tree of Entity Agents (Ai Types) that scare or bolster the Ai. See use example below for formating.
		TreeAttribute itemStackSourcesOfFear: A tree of Items of Blocks that scare or bolster the Ai when held in the player's hands or thrown into the world. See use example below for formating.
		TreeAttribute poiSourcesOfFear: A tree of Points of Intrest that scare or bolster the Ai. See use example below for formating.

	Use Example from OutlawMod, bandit-axe.json
	{
		code: "morale",
		movespeed: 0.040,
		cancelOnHurt: false,
		routDistance: 60,
		rallyTimeAfterRoutMs: 10000,
		minMorale: 0.8,
		maxMorale: 1.0,
		moraleRange: 15,
		useGroupMorale: true,
		deathsImpactMorale: true,
		canRoutFromAnyEnemy: true,

		entitySourcesOfFear: [
			{ code: "player", fearWeight: 0.1},
			{ code: "looter", fearWeight: 0.5},
			{ code: "bandit-*", fearWeight: -0.1},
			{ code: "hound-feral", fearWeight: 0.05},
		],
		itemStackSourcesOfFear: [
			{ code: "outlawhead-*", fearWeight: 0.2},
			{ code: "headonspear-*", fearWeight: 0.2},
		],
		poiSourcesOfFear: [
			{ poiType: "outlawSpawnBlocker", fearWeight: 0.2}
		],
		animation: "sprint",
		animationSpeed: 1,
	},
========================================================================================================================
	AiTaskPursueAndEngageEntity (registered as "engageentity")

		This AiTask makes an Ai pursue an entity at one speed then transition to a second engage speed within a certain distance of the target.
		It also supports "withdraw" functionality that allows Ai to observe a target they can't reach from a safe distance.

	json Settings
		float pursueSpeed: The speed at which the Ai should pursue its target.
            	float pursueRange: The distance, in blocks, at which an Ai should begin to pursue its target.
            	string pursueAnimation: The movement animation to play while pursuing a target.
            	float engageSpeed: The speed at which the Ai should engage and fight its target.
            	float engageRange: The range, in blocks, at which the Ai should transition from pursue speed to engage speed.
            	string engageAnimation: The movement animation to play while engaging a target.
		float arriveRange: The distance away from its target, in blocks, at which the Ai will halt movement. This is generally used to create a small amount of space between the Ai and its melee target so it doesn't look derpy.
            	float arriveVerticalRange: The vertical distance away from its target, in blocks at which the Ai will halt movement.
            	float maxTargetHealth: The max health of a target we are willing to fight.
		bool withdrawIfNoPath: If true, Ai will engage in a withdraw and siege behavior when it cannot get a path to target. (Does not work well with Ai that have hitboxes larget than one block)
            	float withdrawDist: The distance away from our target, in blocks, to which the Ai will withdraw and wait when we don't have a path.
            	float withdrawDistDamaged: The distance away from our target, in blocks, to which the Ai will withdraw and wait if we have taken damage.
            	float withdrawEndTime: how long the Ai should wait before timing out the withdrawl and transitioning to other AiTasks.
            	string withdrawAnimation: The waiting animation the Ai should play once it reaches its withdraw location.
            	float extraTargetDistance: extra distance padding out the size of our target.
            	float maxFollowTime: The max time, in seconds, that the Ai will pursue and engage its target.
            	bool alarmHerd: if true, the Ai will alert its whole herd to pursue and engage the target. Herd members will comply if they do not already have a target.
            	bool packHunting: if true, each individual herd member's maxTargetHealth value will equal maxTargetHealth * number of herd members. This means that larger groups will attack tougher targets.
		bool retaliateAttacks: if true the Ai will target any creature that deals it damage.

	Use Example from OutlawMod, looter.json
	{
		code: "engageentity",
		entityCodes: ["player", "drifter-*", "wolf-male", "wolf-female", "hyena-male", "hyena-female", "locust-*", "bear-*", "bandit-*", "poacher-*", "yeoman-*" ],
		priority: 1.5,
		priorityForCancel: 2.5,
		mincooldown: 0, 
		maxcooldown: 0, 
		pursueSpeed: 0.040,
		pursueRange: 22,
		pursueAnimation: "sprint",
		engageSpeed: 0.025,
		engageRange: 4,
		engageAnimation: "walk",
		withdrawIfNoPath: true,
		withdrawDist: 9.0,
	 	withdrawDistDamaged: 40.0,
		withdrawAnimation: "idle",
		animationSpeed: 1.0,
		alarmHerd: true,
		soundStartMs: 950
	},
	Use Example from OutlawMod, hound-feral.json
	{
		code: "engageentity",
		entityCodes: ["player", "chicken-*", "hare-*", "fox-*", "pig-*", "raccoon-*", "sheep-*", "bandit-*", "poacher-*", "yeoman-*", "looter", "hound-hunting"],
		priority: 1.5,
		maxTargetHealth: 5.0,
		packHunting: "true",
		pursueSpeed: 0.048,
		pursueRange: 15,
		pursueAnimation: "Run",
		engageSpeed: 0.035,
		engageRange: 4,
		engageAnimation: "Run",
		withdrawIfNoPath: false,
		animationSpeed: 2.2,
		alarmHerd: true,
		sound: "creature/wolf/growl",
		whenNotInEmotionState: "saturated"
	},
========================================================================================================================
	AiTaskStayCloseToHerd (registered as "stayclosetoherd")
		
		This AiTask assigns each herd a herd leader, who is by default, the first member of the herd to spawn. The other members of the herd then stay within a specifed range of the leader, following them wherever they might go.
	
	json Settings
		float moveSpeed: The speed at which the Ai will return to the herd leader.
            	float searchRange: The distance, in blocks, at which an Ai will search for a new herd leader when its original one is killed.
            	float maxDistance: The distance, in blocks, at which an Ai will be forced to return to its herd leader.
            	float arriveDistance: The distance, in blocks, at which an Ai will be considered to have "arrived" at its herd leader's location.
            	bool allowStrayFromHerdInCombat: If true, the Ai will not return to or follow its herd leader while it has a combat target, it will return to the leader when combat ends.
            	bool allowHerdConsolidation: If true, when an Ai is the last member of its herd or the herd is below 50% spawn strength, it can join a new herd within consolidationRange, as long as its entity code matches at lease one member of that herd.
		float consolidationRange: The range at which this Ai can consolidate into another herd.
            	array[string] consolidationEntityCodes: The array of Entity codes that are valid ai types whose herd this Ai can consolidate into.
		bool allowTeleport: If true, the Ai can teleport to its herd leader if it cannot reach it with normal pathing.
            	float teleportAfterRange: The distance, in blocks, beyond which the Ai will teleport to reach its herd leader.

	Use Example from OutlawMod, yeoman-archer.json
	{
		code: "stayclosetoherd",
		priority: 1.1,
		movespeed: 0.04,
		animationSpeed: 1.0,
		maxDistance: 15,
		arriveDistance: 8,
		searchRange: 25,
		allowStrayFromHerdInCombat: true,
		allowHerdConsolidation: true,
		consolidationRange: 40,
		animation: "sprint"
	},
========================================================================================================================
	AiTaskReactToProjectiles (registered as "reacttoprojectiles")
		
		This AiTask assigns allows Ai to aggro when someone shoots a projectile near them.
	
	json Settings
		None

	Use Example from OutlawMod, yeoman-archer.json
	{
		code: "reacttoprojectiles",
		entityCodes: ["player"],
		priority: 5.1,
	},
========================================================================================================================
	EntityBehaviorLODRepulseAgents (registered as "lodrepulseagents")
		
		This Entity Behavior functions like RepulseAgents, but it has LOD (Level of Detail) functionality.
		RepulseAgents is an incredibly expensive behavior, but it is mostly cosmetic to keep EntityAgents from interpenetrating.
		This only really matters if a player is nearby to see the entities resolving interpenetration.
		With that in mind, "lodrepulseagents" is a replacement for "repulseagents" that is recommended for Entities running ExpandedAiTasks.
		Entities using this behavior will not not collide beyond a certain distance from the player.
		As a player gets closer, the fidelity and tick rate will increase until it matches Vintage Story's native RepulseAgents behavior at close range.
		It is also important to note that "lodrepulseagents" will not run when an entity is dead, nor will it run on dead entities. This is to save performance.
	
	json Settings
		None

	Use Example from OutlawMod, yeoman-archer.json
	behaviors: [
			{ code: "lodrepulseagents" }, //Our custom repulse agents behavior with tick LODing based on player distance.
		],