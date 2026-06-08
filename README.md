#3D Horror Survival Game
A first-person forest survival horror game made in Unity. The player explores a generated forest map, gathers resources, survives hunger and stamina drain, crafts items at the crafting table, avoids the monster at night, and tries to beat their personal day record.

Project Status
This project is playable, but still in active development. Some systems are fully usable, while others are placeholders designed so better prefabs, art, recipes, and multiplayer features can be added later.

Main Features
First-person player controller with walking, sprinting, crouching, jumping, stamina, hunger, and health.
Day/night cycle:
Day lasts 2 minutes 30 seconds.
Night lasts 1 minute 30 seconds.
The day counter increases every time night turns back into day.
Monster system:
Monster spawns only at night.
Monster chases the player.
Campfire and tent areas are protected zones.
Touching the monster starts a jumpscare before the death screen.
Campfire system:
Wood can be added to the campfire.
A lit campfire creates a safer, brighter area.
Inventory/hotbar system:
Number keys select inventory slots.
Wood and food can appear in inventory slots.
Flashlight, weapons, food, and wood have hand/equipped behavior.
Crafting table:
Press E on the crafting table to open the menu.
Crafting recipes include wooden barrier, farm plot, recipe unlocking, torch, crafting table level 2, and sack upgrade.
Sack upgrades increase max wood capacity.
Guns:
Gun can be equipped in inventory.
Left click shoots.
Right click aims/focuses.
R reloads gun.
Ammo pickups can be found on the map.
Bunnies:
Bunnies spawn around the forest and near camp.
Killing a bunny can drop food/meat.
Minimap and UI:
Minimap shows time remaining, campfire info, and personal record.
FPS bar can be toggled.
Loading screen hides startup map generation.
Main menu:
Play opens Solo Play / Multiplayer Lobby options.
Shop displays class options, gem costs, stars, locks, and class descriptions.
Settings are available from the menu and pause screen.
How To Open The Project
Open Unity Hub.

Click Add or Open.

Select this project folder:

A 3d horror survival-2026-05-21-13-43-33-2026-05-21-13-43-33
Open the project in Unity.

Wait for Unity to import assets and compile scripts.

Open the main menu scene or gameplay scene.

Press Play.

Build Settings
Make sure these scenes are added to Unity Build Settings:

MainMenu
GameScene
Settings
The script MainMenuManager expects those names unless you change them in the Inspector.

To add scenes:

Open File > Build Settings.
Open each scene in Unity.
Click Add Open Scenes.
Make sure the scene names match the fields on MainMenuManager.
Basic Controls
Action	Control
Move	WASD
Look around	Mouse
Jump	Space
Sprint	Left Shift
Crouch	Left Control
Interact	E
Pick up wood only	F
Drop wood / drop active item	Q
Select inventory slot	1 through 0
Toggle flashlight	R when flashlight is selected / no gun equipped
Reload gun	R when gun is equipped
Shoot gun	Left mouse
Aim/focus gun	Right mouse
Open pause menu	Esc
Toggle FPS UI	Z
Gameplay Loop
Start from the main menu.
Click Play.
Choose Solo Play.
Explore the forest during the day.
Pick up wood, food, ammo, and weapons.
Use the campfire and crafting table to prepare.
Survive the night when the monster appears.
When night turns back into day, your day counter increases.
Try to beat your personal record.
Day And Night
The day/night system is controlled by:

Assets/Code/Map #1/Day Night Cycle/day-night change.cs
Current timing:

Day: 150 seconds.
Night: 90 seconds.
The personal record is saved with PlayerPrefs using:

personal_best_days
Gems are saved using:

class_gems
Monster
Important scripts:

Assets/Code/Map #1/Monster/MonsterManager.cs
Assets/Code/Map #1/Monster/MonsterAI.cs
Assets/Code/Map #1/Monster/JumpscareManager.cs
Monster behavior:

The monster only spawns at night.
It despawns/hides when day returns.
It avoids the campfire and tent protected areas.
If it touches the player, a jumpscare starts before the death screen.
Setup notes:

Add MonsterManager to a scene object.
Assign the monster prefab to monsterPrefab.
If using the mutant monster prefab, make sure it has:
MonsterAI
Animator
NavMeshAgent
Colliders
Renderer/skinned mesh
Campfire
Important scripts:

Assets/Code/Map #1/World/Campfire.cs
Assets/Code/Map #1/Wood/CampfireWoodReceiver.cs
Assets/Code/Map #1/UI/CampfireUI.cs
How it works:

Pick up wood with F.
Interact with the campfire using E.
Wood is added as fuel.
The campfire safe zone is active at night when the fire is lit.
The monster should avoid the campfire safe radius.
Wood And Sack Upgrades
Important script:

Assets/Code/Map #1/Wood/PlayerWoodCarry.cs
Default sack levels:

Sack Level	Max Wood
1	5
2	10
3	20
4	35
5	50
Default upgrade costs:

Upgrade	Cost
Level 1 to 2	5 wood
Level 2 to 3	10 wood
Level 3 to 4	20 wood
Level 4 to 5	35 wood
The sack upgrade appears in the crafting table menu. Buying it spends carried wood and increases max wood capacity.

Inventory
Important scripts:

Assets/Code/Map #1/Inventory/PlayerInventory.cs
Assets/Code/Map #1/Inventory/InventoryUI.cs
Assets/Code/Map #1/Inventory/PlayerFlashlightController.cs
Assets/Code/Map #1/Inventory/FlashlightItem.cs
Notes:

Inventory slots are selected with number keys.
Slot 0 means slot 10.
Wood has special slot behavior.
Food can be stored and held.
Gun/weapon items can be dropped and picked up.
Flashlight can be toggled with R when it is selected and a gun is not equipped.
Crafting Table
Important scripts:

Assets/Code/Map #1/Crafting/CraftingTable.cs
Assets/Code/Map #1/Crafting/CraftingTableUI.cs
Assets/Code/Map #1/Crafting/CraftedItemPrefabCatalog.cs
Assets/Code/Map #1/Crafting/PlayerCraftingMaterials.cs
Open the crafting table by looking at it and pressing E.

Current recipes:

Recipe	Cost
Wooden Barrier	5 wood
Farm Plot	10 wood
Recipe Unlocking	30 wood, 5 scraps
Torch	15 wood, 1 scrap
Crafting Table Level 2	50 wood, 10 scraps
Sack Upgrade	Dynamic wood cost
Prefab setup:

Add CraftedItemPrefabCatalog to a GameObject in the scene if you want to assign custom craft result prefabs.
Assign:
woodenBarrierPrefab
farmPlotPrefab
recipeBookPrefab
torchPrefab
craftingTableLevel2Prefab
If no prefab is assigned, the game creates a simple fallback object so crafting still works.

Weapons And Ammo
Important scripts:

Assets/Code/Map #1/Weapons/GunWeapon.cs
Assets/Code/Map #1/Weapons/GunManager.cs
Assets/Code/Map #1/Weapons/AmmoPickup.cs
Assets/Code/Map #1/Weapons/WeaponPickup.cs
Assets/Code/Map #1/Weapons/BulletProjectile.cs
Gun controls:

Left click shoots.
Right click aims/focuses.
R reloads.
Gun setup:

Add GunWeapon to the gun prefab.
Add/keep Weapon on the same prefab.
Assign muzzle if possible.
Assign gun prefab through GunManager, not by manually placing it in the hierarchy.
Ammo pickups can be spawned around the map.
Inspector values worth tuning:

damage
range
fireCooldown
bulletSpeed
magazineSize
reserveAmmo
hipBulletSpreadDegrees
focusBulletSpreadDegrees
focusedDamageMultiplier
Bunnies And Food
Important scripts:

Assets/Code/Map #1/Bunny code/BunnyAI.cs
Assets/Code/Map #1/Bunny code/BunnyHealth.cs
Assets/Code/Map #1/Bunny code/BunnyVisuals.cs
Assets/Code/Map #1/Hunger/FoodPickup.cs
How it works:

Bunnies spawn in forest chunks and near camp.
Killing a bunny can spawn food.
Food can be picked up or stored in inventory.
If inventory is full, food should fall in front of the player.
If bunnies do not appear:

Check InfiniteForestWorld.
Make sure bunniesPerChunkMin is at least 1.
Make sure bunnySpawnChancePerChunk is not too low.
Make sure the ground mask can raycast to the terrain.
Make sure the bunny prefab is valid, active, and has visible renderers.
Map Generation And Performance
Important scripts:

Assets/Code/Map #1/World/InfiniteTerrainStreamer.cs
Assets/Code/Map #1/World/InfiniteForestWorld.cs
Assets/Code/Settings/WorldStreamSettings.cs
Current approach:

The terrain builds a fixed map at startup.
The forest also builds chunks.
Distant forest object chunks can be disabled for performance.
Terrain render quality is made cheaper using terrain pixel error and basemap distance.
Grass shadows and grass colliders are disabled/removed to save FPS.
Useful Inspector settings:

InfiniteTerrainStreamer:

fixedMapChunksX
fixedMapChunksZ
maxChunkSpawnsPerFrame
terrainPixelError
terrainBasemapDistance
heightmapResolution
InfiniteForestWorld:

fixedMapChunksX
fixedMapChunksZ
maxChunkSpawnsPerFrame
cullDistantForestChunks
forestChunkRenderRadius
chunkCullInterval
treesPerChunkMin
treesPerChunkMax
grassPerChunkMin
grassPerChunkMax
bunniesPerChunkMin
bunniesPerChunkMax
For better FPS:

Lower tree and grass counts.
Keep forestChunkRenderRadius around 2.
Keep grass shadows disabled.
Avoid adding NavMesh carving to many spawned objects.
Use simpler prefabs for trees, grass, and pickups.
Loading Screen
Important script:

Assets/Code/Map #1/UI/LoadingScreenUI.cs
The loading screen appears while terrain and forest chunks are being generated. It uses 8 segmented bars:

Green means completed.
Red means remaining.
If the loading screen takes too long:

Reduce fixed map size.
Reduce tree/grass/bunny counts.
Increase maxChunkSpawnsPerFrame if your laptop can handle a bigger startup burst.
Lower terrain heightmap resolution.
Main Menu And Shop
Important script:

Assets/Code/Main Menu/MainMenuManager.cs
Menu flow:

Main menu opens.
Click Play.
Choose Solo Play or Multiplayer Lobby.
Solo Play loads the game scene.
Multiplayer is currently a placeholder.
Class shop:

Noob
Camper
Lumberjack
Cook
Medic
Farmer
Mapper
Feline
Witch
Arsonist
Some classes are locked behind personal record days. Gems are used as currency. Some class effects are descriptions/placeholders and still need final gameplay implementation.

Pause Menu And Settings
Important scripts:

Assets/Code/Map #1/UI/PauseMenuUI_New.cs
Assets/Code/Settings/SettingsMenu.cs
Assets/Code/Settings/GameplayPauseSettings.cs
Assets/Code/Settings/RunStatePersistence.cs
Pause menu:

Press Esc.
Resume.
Go to menu/settings.
Adjust sensitivity.
Adjust graphics/world quality.
Settings persistence:

Player position, hunger, health, stamina, wood, sack level, and inventory can be saved/restored when moving through settings.
UI Systems
Important scripts:

Assets/Code/Map #1/UI/ForestMinimapUI.cs
Assets/Code/Map #1/UI/DaysPassedAutoUI.cs
Assets/Code/Map #1/UI/FPSBarUI.cs
Assets/Code/Map #1/UI/CampfireUI.cs
Assets/Code/Map #1/Health/HealthUI.cs
Assets/Code/Map #1/Hunger/HungerUI.cs
Assets/Code/Map #1/Stamina/StaminaUI.cs
UI includes:

Health
Hunger
Stamina
Inventory hotbar
Minimap
Day display
Campfire display
FPS display
Loading screen
Prefab Checklist
Use this checklist when setting up or replacing assets.

Player:

controller
PlayerInventory
PlayerWoodCarry
PlayerHunger
Health
PlayerFlashlightController
Camera child
CharacterController
Monster:

MonsterAI
Animator
NavMeshAgent
Collider
Renderer/skinned mesh
Crafting table:

Collider
CraftingTable
Name contains crafting table if using auto setup
Campfire:

Campfire
CampfireWoodReceiver
Trigger collider for receiver
Optional safe-zone indicator
Optional fuel UI prefab
Wood:

WoodPickup
Collider
Tag Wood if possible
Optional Rigidbody for dropped wood
Food:

FoodPickup
Collider
Optional Weapon if stored as inventory item
Gun:

Weapon
GunWeapon
WeaponPickup for world pickup version
Muzzle transform if available
Rigidbody/collider for dropped version
Ammo:

AmmoPickup
Collider
Common Problems
Display 1 No Cameras Rendering
Check that:

The player camera exists.
The camera GameObject is active.
The Camera component is enabled.
The camera target texture is empty/null.
The active scene has the player/camera object.
Inventory Slot Does Not Click
Check that:

There is an EventSystem in the scene.
InventoryUI exists.
Slot button target graphics are assigned.
The slot actually contains an item.
Bunnies Do Not Spawn
Check that:

InfiniteForestWorld exists in the scene.
bunniesPerChunkMin is at least 1.
bunnySpawnChancePerChunk is above 0.
groundMask can hit the terrain/ground.
The bunny prefab is active and valid.
Terrain finished generating.
Monster Does Not Spawn
Check that:

MonsterManager exists.
monsterPrefab is assigned.
DayNightCycle exists.
It is nighttime.
Monster prefab has MonsterAI.
Crafting Table Does Not Open
Check that:

The table has a collider.
The table has CraftingTable, or its name contains crafting table for auto setup.
The player is looking at the table.
Press E.
Loading Takes Too Long
Try:

Lowering map chunk count.
Lowering tree/grass/bunny counts.
Lowering heightmapResolution.
Increasing maxChunkSpawnsPerFrame slightly.
Keeping forestChunkRenderRadius low.
Low FPS
Try:

Lower graphics/world quality in pause settings.
Reduce treesPerChunkMax.
Reduce grassPerChunkMax.
Keep grass shadows off.
Keep NavMesh carving off for spawned objects.
Use simpler prefabs.
Keep forestChunkRenderRadius at 2 or lower.
Important Folders
Assets/Code/Map #1/controller
Assets/Code/Map #1/World
Assets/Code/Map #1/Monster
Assets/Code/Map #1/Inventory
Assets/Code/Map #1/Crafting
Assets/Code/Map #1/Weapons
Assets/Code/Map #1/UI
Assets/Code/Map #1/Wood
Assets/Code/Map #1/Hunger
Assets/Code/Settings
Assets/Code/Main Menu
Assets/Code/Solo lobby
Development Notes
Many UI elements are generated by scripts at runtime.
Several systems auto-create missing helper objects, like inventory UI, crafting UI, FPS UI, loading UI, and EventSystem.
Some class shop effects are descriptions only and still need gameplay implementation.
Multiplayer lobby is currently a placeholder.
The game uses PlayerPrefs for personal record, gems, settings, and some run-state persistence.
