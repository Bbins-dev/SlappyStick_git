# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SlappyStick is a Unity 2D physics puzzle game built in Unity 2023.3+. The project name is "StickIt" (company: DoubleB) and focuses on stick-based physics gameplay where players manipulate objects to solve levels.

### Core Game Mechanics
- **Stick Movement**: Drag to position, hold to charge, release to launch
- **Physics-Based**: Realistic 2D physics with Rigidbody2D
- **Level Progression**: Sequential level unlocking system
- **Replay System**: Record and playback player actions
- **Auto-Reset Logic**: Idle/stuck detection with configurable timeouts

## Commands

### Unity Operations
- **Build**: Open Unity Editor and use File > Build Settings to build the project
- **Test Play**: Use Unity Editor Play mode to test gameplay
- **Level Editor**: Use the MakingScene.unity for level creation and testing

### Key Scenes
- `MenuScene.unity`: Main menu
- `LevelSelectScene.unity`: Level selection UI
- `PlayScene.unity`: Main gameplay scene
- `MakingScene.unity`: Level creation/editing scene
- `PlayUIOnly.unity`: UI-only testing scene

## Architecture

### Core Systems
- **GameManager**: Singleton managing game state, level progression, and save/load (`Assets/Scripts/Managers/GameManager.cs`)
- **LevelManager**: Handles level loading, spawning, and reset functionality (`Assets/Scripts/Managers/LevelManager.cs`)
- **ReplaySystem**: Records and plays back gameplay for replay functionality (`Assets/Scripts/Replay/`)

### Data Systems
- **LevelData**: ScriptableObject-based level data system (`Assets/Scripts/Data/LevelData.cs`)
- **LevelDatabase**: Collection of all level data (`Assets/Scripts/Data/LevelDatabase.cs`)

### Level Creation Workflow
- Levels are created in MakingScene.unity using editor tools
- LevelConfigurator editor tool saves level data with prefab references
- All prefabs must be placed in Resources subfolders:
  - Sticks: `Resources/Sticks/` (prefix: St_*)
  - Obstacles: `Resources/Obstacles/` (prefix: Ob_*)
  - Targets: `Resources/Targets/` (prefix: Ta_*)
  - Fulcrums: `Resources/Fulcrums/` (prefix: Fu_*)

### Reset System
- Resettable2D component handles object state restoration
- ResetBus manages coordinated reset operations
- All dynamic objects should implement reset functionality

## Development Guidelines

### Prefab & LevelData Workflow
- Use prefabName (string) field only for prefab references in LevelData
- Never use enums or hardcoded lists for prefab selection
- Load prefabs at runtime using Resources.Load with prefabName
- Use LevelConfigurator's "Save To LevelData (Prefab Spawns)" function
- Follow consistent prefab naming conventions

### Code Standards (MUST FOLLOW)

#### 1. KISS & DRY Principles (Critical Priority)
- Keep It Simple: Avoid over-engineering solutions
- Don't Repeat Yourself: Extract shared logic into reusable methods
- Clear Naming: Use intention-revealing names for variables and methods
- Single Purpose: Each method/class should have one clear responsibility

#### 2. Unity Lifecycle Safety (High Priority)
- Never duplicate lifecycle methods (Update, Start, Awake, OnDestroy)
- Never create GameObjects in OnDestroy - use cleanup patterns instead
- Use [SerializeField] private fields with property accessors when needed
- Follow Unity's execution order for initialization

#### 3. Null Safety & Error Logging (High Priority)
- Always null-check before accessing objects/components
- Log unexpected states with Debug.LogError() or Debug.LogWarning()
- Prevent NullReferenceException in production code
- Use TryGetComponent when possible instead of GetComponent

#### 4. Short Methods & Single Responsibility (Medium Priority)
- Keep methods under ~30 lines
- One method = one clear responsibility
- Break complex logic into smaller helper methods
- Extract repeated patterns into utility methods

#### 5. Component Caching (Medium Priority)
- Never use GetComponent/Find in Update or FixedUpdate
- Cache component references in Start/Awake
- Prefer TryGetComponent over GetComponent
- Use [RequireComponent] attribute when appropriate

#### 6. Basic Error Prevention (High Priority)
- Check for missing using statements
- Verify method names exist before calling
- Ensure linter shows no errors before committing
- Validate parameters in public methods

#### 7. Side-Effect Awareness (Medium Priority)
- Consider impact on existing code when making changes
- Review interactions with other scripts
- Mention potential risks in code proposals
- Test changes thoroughly before integration

### Key Components
- **StickMove**: Main player controller - handles mouse input, physics movement, auto-reset logic, UI feedback
- **StickItCamera**: Camera management for gameplay
- **ReplayManager/ReplayPlayer**: Replay system - records transform data, saves/loads replay files, binary format
- **UIOverlayKeeper**: UI state management

### Editor Tools
Located in `Assets/Scripts/EditorTools/`:
- LevelConfigurator: Main level creation tool
- SettingsUI/SettingsMenuFactory: Level editor settings
- TipTrigger system for level hints
- ClearPopup system for level completion

## Naming Conventions

### Classes & Scripts
- PascalCase for class names: StickMove, GameManager
- Descriptive names that indicate purpose: LevelSelectUI, ReplayManager
- Suffix with type when needed: LevelData, ReplayData

### Variables & Methods
- camelCase for private fields: holdTime, startPosition
- PascalCase for public properties: CurrentLevel, TotalLevels
- PascalCase for methods: BeginRecording(), StageClear()
- Boolean prefixes: isHolding, hasLaunched, canReset

### Unity-Specific
- SerializeField for inspector-exposed private fields
- Header attributes for organization: [Header("Launch Settings")]
- Tooltip attributes for documentation: [Tooltip("Maximum launch force")]

## Common Pitfalls to Avoid
- Don't modify StickMove.cs without understanding physics implications
- Don't bypass the GameManager for level progression
- Don't create GameObjects in OnDestroy methods
- Don't forget to null-check before accessing components
- Don't use GetComponent in Update loops
- Don't ignore Unity's execution order
- Don't hardcode values that should be configurable

## Performance Guidelines
- Cache component references in Start/Awake
- Use object pooling for frequently created/destroyed objects
- Minimize allocations in Update loops
- Use efficient data structures for large datasets
- Profile before optimizing - measure first!

## Mobile Platform
- Configured for mobile deployment
- Uses Unity's Mobile Notifications package
- Includes NativeShare package for sharing functionality
- Supports adaptive performance optimization