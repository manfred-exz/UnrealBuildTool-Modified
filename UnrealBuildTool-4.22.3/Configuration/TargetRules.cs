// Copyright 1998-2019 Epic Games, Inc. All Rights Reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tools.DotNETCommon;

namespace UnrealBuildTool
{
	/// <summary>
	/// The type of target
	/// </summary>
	[Serializable]
	public enum TargetType
	{
		/// <summary>
		/// Cooked monolithic game executable (GameName.exe).  Also used for a game-agnostic engine executable (UE4Game.exe or RocketGame.exe)
		/// </summary>
		Game,

		/// <summary>
		/// Uncooked modular editor executable and DLLs (UE4Editor.exe, UE4Editor*.dll, GameName*.dll)
		/// </summary>
		Editor,

		/// <summary>
		/// Cooked monolithic game client executable (GameNameClient.exe, but no server code)
		/// </summary>
		Client,

		/// <summary>
		/// Cooked monolithic game server executable (GameNameServer.exe, but no client code)
		/// </summary>
		Server,

		/// <summary>
		/// Program (standalone program, e.g. ShaderCompileWorker.exe, can be modular or monolithic depending on the program)
		/// </summary>
		Program,
	}

	/// <summary>
	/// Specifies how to link all the modules in this target
	/// </summary>
	[Serializable]
	public enum TargetLinkType
	{
		/// <summary>
		/// Use the default link type based on the current target type
		/// </summary>
		Default,

		/// <summary>
		/// Link all modules into a single binary
		/// </summary>
		Monolithic,

		/// <summary>
		/// Link modules into individual dynamic libraries
		/// </summary>
		Modular,
	}

	/// <summary>
	/// Specifies whether to share engine binaries and intermediates with other projects, or to create project-specific versions. By default,
	/// editor builds always use the shared build environment (and engine binaries are written to Engine/Binaries/Platform), but monolithic builds
	/// and programs do not (except in installed builds). Using the shared build environment prevents target-specific modifications to the build
	/// environment.
	/// </summary>
	[Serializable]
	public enum TargetBuildEnvironment
	{
		/// <summary>
		/// Use the default build environment for this target type (and whether the engine is installed)
		/// </summary>
		Default,

		/// <summary>
		/// Engine binaries and intermediates are output to the engine folder. Target-specific modifications to the engine build environment will be ignored.
		/// </summary>
		Shared,

		/// <summary>
		/// Engine binaries and intermediates are specific to this target
		/// </summary>
		Unique,
	}

	/// <summary>
	/// Attribute used to mark fields which much match between targets in the shared build environment
	/// </summary>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
	class RequiresUniqueBuildEnvironmentAttribute : Attribute
	{
	}

	/// <summary>
	/// TargetRules is a data structure that contains the rules for defining a target (application/executable)
	/// </summary>
	public abstract class TargetRules
	{
		/// <summary>
		/// Static class wrapping constants aliasing the global TargetType enum.
		/// </summary>
		public static class TargetType
		{
			/// <summary>
			/// Alias for TargetType.Game
			/// </summary>
			public const global::UnrealBuildTool.TargetType Game = global::UnrealBuildTool.TargetType.Game;

			/// <summary>
			/// Alias for TargetType.Editor
			/// </summary>
			public const global::UnrealBuildTool.TargetType Editor = global::UnrealBuildTool.TargetType.Editor;

			/// <summary>
			/// Alias for TargetType.Client
			/// </summary>
			public const global::UnrealBuildTool.TargetType Client = global::UnrealBuildTool.TargetType.Client;

			/// <summary>
			/// Alias for TargetType.Server
			/// </summary>
			public const global::UnrealBuildTool.TargetType Server = global::UnrealBuildTool.TargetType.Server;

			/// <summary>
			/// Alias for TargetType.Program
			/// </summary>
			public const global::UnrealBuildTool.TargetType Program = global::UnrealBuildTool.TargetType.Program;
		}

		/// <summary>
		/// The name of this target.
		/// </summary>
		public readonly string Name;

		/// <summary>
		/// File containing this target
		/// </summary>
		internal FileReference File;

		/// <summary>
		/// Platform that this target is being built for.
		/// </summary>
		public readonly UnrealTargetPlatform Platform;

		/// <summary>
		/// The configuration being built.
		/// </summary>
		public readonly UnrealTargetConfiguration Configuration;

		/// <summary>
		/// Architecture that the target is being built for (or an empty string for the default).
		/// </summary>
		public readonly string Architecture;

		/// <summary>
		/// Path to the project file for the project containing this target.
		/// </summary>
		public readonly FileReference ProjectFile;

		/// <summary>
		/// The current build version
		/// </summary>
		public readonly ReadOnlyBuildVersion Version;

		/// <summary>
		/// The type of target.
		/// </summary>
		public global::UnrealBuildTool.TargetType Type = global::UnrealBuildTool.TargetType.Game;

		/// <summary>
		/// Whether the target uses Steam.
		/// </summary>
		public bool bUsesSteam;

		/// <summary>
		/// Whether the target uses CEF3.
		/// </summary>
		public bool bUsesCEF3;

		/// <summary>
		/// Whether the project uses visual Slate UI (as opposed to the low level windowing/messaging, which is always available).
		/// </summary>
		public bool bUsesSlate = true;

		/// <summary>
		/// Forces linking against the static CRT. This is not fully supported across the engine due to the need for allocator implementations to be shared (for example), and TPS 
		/// libraries to be consistent with each other, but can be used for utility programs.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bUseStaticCRT = false;

		/// <summary>
		/// Enables the debug C++ runtime (CRT) for debug builds. By default we always use the release runtime, since the debug
		/// version isn't particularly useful when debugging Unreal Engine projects, and linking against the debug CRT libraries forces
		/// our third party library dependencies to also be compiled using the debug CRT (and often perform more slowly). Often
		/// it can be inconvenient to require a separate copy of the debug versions of third party static libraries simply
		/// so that you can debug your program's code.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bDebugBuildsActuallyUseDebugCRT = false;

		/// <summary>
		/// Whether the output from this target can be publicly distributed, even if it has dependencies on modules that are in folders 
		/// with special restrictions (eg. CarefullyRedist, NotForLicensees, NoRedist).
		/// </summary>
		public bool bOutputPubliclyDistributable = false;

		/// <summary>
		/// Specifies the configuration whose binaries do not require a "-Platform-Configuration" suffix.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public UnrealTargetConfiguration UndecoratedConfiguration = UnrealTargetConfiguration.Development;

		/// <summary>
		/// Build all the plugins that we can find, even if they're not enabled. This is particularly useful for content-only projects, 
		/// where you're building the UE4Editor target but running it with a game that enables a plugin.
		/// </summary>
		[Obsolete("bBuildAllPlugins has been deprecated. Use bPrecompile to build all modules which are not part of the target.")]
		public bool bBuildAllPlugins = false;

		/// <summary>
		/// Build all the modules that are valid for this target type. Used for CIS and making installed engine builds.
		/// </summary>
		[CommandLine("-AllModules")]
		public bool bBuildAllModules = false;

		/// <summary>
		/// A list of additional plugins which need to be included in this target. This allows referencing non-optional plugin modules
		/// which cannot be disabled, and allows building against specific modules in program targets which do not fit the categories
		/// in ModuleHostType.
		/// </summary>
		public List<string> AdditionalPlugins = new List<string>();

		/// <summary>
		/// Additional plugins that should be included for this target.
		/// </summary>
		[CommandLine("-EnablePlugin=", ListSeparator = '+')]
		public List<string> EnablePlugins = new List<string>();

		/// <summary>
		/// List of plugins to be disabled for this target. Note that the project file may still reference them, so they should be marked
		/// as optional to avoid failing to find them at runtime.
		/// </summary>
		[CommandLine("-DisablePlugin=", ListSeparator = '+')]
		public List<string> DisablePlugins = new List<string>();

		/// <summary>
		/// Accessor for
		/// </summary>
		[Obsolete("The ExcludePlugins setting has been renamed to DisablePlugins. Please update your code to avoid build failures in future versions of the engine.")]
		public List<string> ExcludePlugins
		{
			get { return DisablePlugins; }
		}

		/// <summary>
		/// Path to the set of pak signing keys to embed in the executable.
		/// </summary>
		public string PakSigningKeysFile = "";

		/// <summary>
		/// Allows a Program Target to specify it's own solution folder path.
		/// </summary>
		public string SolutionDirectory = String.Empty;

		/// <summary>
		/// Whether the target should be included in the default solution build configuration
		/// </summary>
		public bool? bBuildInSolutionByDefault = null;

		/// <summary>
		/// Whether this target should be compiled as a DLL.  Requires LinkType to be set to TargetLinkType.Monolithic.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bShouldCompileAsDLL = false;
		
		/// <summary>
		/// Subfolder to place executables in, relative to the default location.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public string ExeBinariesSubFolder = String.Empty;

		/// <summary>
		/// Allow target module to override UHT code generation version.
		/// </summary>
		public EGeneratedCodeVersion GeneratedCodeVersion = EGeneratedCodeVersion.None;

		/// <summary>
		/// Whether to enable the mesh editor.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bEnableMeshEditor = false; // {Dev-Physics:false, Dev-Destruction:true}

		/// <summary>
		/// Whether to compile the Chaos physics plugin.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bCompileChaos = false; // {Dev-Physics:false, Dev-Destruction:true}

		/// <summary>
		/// Whether to use the Chaos physics interface. This overrides the physx flags to disable APEX and NvCloth
		/// </summary>
		[RequiresUniqueBuildEnvironment]
        public bool bUseChaos = false;

		/// <summary>
		/// Whether to include the immediate mode physics interface. This overrides the physx flags to disable APEX and NvCloth
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bCompileImmediatePhysics = false;

		/// <summary>
		/// Whether scene query acceleration is done by UE4. The physx scene query structure is still created, but we do not use it.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bCustomSceneQueryStructure = false; // {Dev-Physics:false, Dev-Destruction:true}

		/// <summary>
		/// Whether to include PhysX support.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bCompilePhysX = true;

		/// <summary>
		/// Whether to include PhysX APEX support.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		[ConfigFile(ConfigHierarchyType.Engine, "/Script/BuildSettings.BuildSettings", "bCompileApex")]
		public bool bCompileAPEX = true;

		/// <summary>
		/// Whether to include NvCloth.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bCompileNvCloth = true;
        
		/// <summary>
		/// Whether to include ICU unicode/i18n support in Core.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		[ConfigFile(ConfigHierarchyType.Engine, "/Script/BuildSettings.BuildSettings", "bCompileICU")]
		public bool bCompileICU = true;

		/// <summary>
		/// Whether to compile CEF3 support.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		[ConfigFile(ConfigHierarchyType.Engine, "/Script/BuildSettings.BuildSettings", "bCompileCEF3")]
		public bool bCompileCEF3 = true;

		/// <summary>
		/// Whether to compile the editor or not. Only desktop platforms (Windows or Mac) will use this, other platforms force this to false.
		/// </summary>
		public bool bBuildEditor
		{
			get { return (Type == TargetType.Editor); }
			set { Log.TraceWarning("Setting {0}.bBuildEditor is deprecated. Set {0}.Type instead.", GetType().Name); }
		}

		/// <summary>
		/// Whether to compile code related to building assets. Consoles generally cannot build assets. Desktop platforms generally can.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bBuildRequiresCookedData = false;

		/// <summary>
		/// Whether to compile WITH_EDITORONLY_DATA disabled. Only Windows will use this, other platforms force this to false.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		[CommandLine("-NoEditorOnlyData", Value = "false")]
		public bool bBuildWithEditorOnlyData = true;

		/// <summary>
		/// Manually specified value for bBuildDeveloperTools.
		/// </summary>
		bool? bBuildDeveloperToolsOverride;

		/// <summary>
		/// Whether to compile the developer tools.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bBuildDeveloperTools
		{
			set { bBuildDeveloperToolsOverride = value; }
			get { return bBuildDeveloperToolsOverride ?? (bCompileAgainstEngine && (Type == TargetType.Editor || Type == TargetType.Program)); }
		}

		/// <summary>
		/// Whether to force compiling the target platform modules, even if they wouldn't normally be built.
		/// </summary>
		public bool bForceBuildTargetPlatforms = false;

		/// <summary>
		/// Whether to force compiling shader format modules, even if they wouldn't normally be built.
		/// </summary>
		public bool bForceBuildShaderFormats = false;

		/// <summary>
		/// Whether we should compile SQLite using the custom "Unreal" platform (true), or using the native platform (false).
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		[ConfigFile(ConfigHierarchyType.Engine, "/Script/BuildSettings.BuildSettings", "bCompileCustomSQLitePlatform")]
		public bool bCompileCustomSQLitePlatform = true;

		/// <summary>
		/// Whether to compile lean and mean version of UE.
		/// </summary>
		[Obsolete("bCompileLeanAndMeanUE is deprecated. Set bBuildDeveloperTools to the opposite value instead.")]
		public bool bCompileLeanAndMeanUE
		{
			get { return !bBuildDeveloperTools; }
			set { bBuildDeveloperTools = !value; }
		}

        /// <summary>
		/// Whether to utilize cache freed OS allocs with MallocBinned
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		[ConfigFile(ConfigHierarchyType.Engine, "/Script/BuildSettings.BuildSettings", "bUseCacheFreedOSAllocs")]
        public bool bUseCacheFreedOSAllocs = true;

        /// <summary>
        /// Enabled for all builds that include the engine project.  Disabled only when building standalone apps that only link with Core.
        /// </summary>
		[RequiresUniqueBuildEnvironment]
        public bool bCompileAgainstEngine = true;

		/// <summary>
		/// Enabled for all builds that include the CoreUObject project.  Disabled only when building standalone apps that only link with Core.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bCompileAgainstCoreUObject = true;

		/// <summary>
		/// Enabled for builds that need to initialize the ApplicationCore module. Command line utilities do not normally need this.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bCompileAgainstApplicationCore = true;

		/// <summary>
		/// Whether to compile Recast navmesh generation.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		[ConfigFile(ConfigHierarchyType.Engine, "/Script/BuildSettings.BuildSettings", "bCompileRecast")]
		public bool bCompileRecast = true;

		/// <summary>
		/// Whether to compile SpeedTree support.
		/// </summary>
		[ConfigFile(ConfigHierarchyType.Engine, "/Script/BuildSettings.BuildSettings", "bCompileSpeedTree")]
		bool? bOverrideCompileSpeedTree;

		/// <summary>
		/// Whether we should compile in support for Simplygon or not.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bCompileSpeedTree
		{
			set { bOverrideCompileSpeedTree = value; }
			get { return bOverrideCompileSpeedTree ?? Type == TargetType.Editor; }
		}

		/// <summary>
		/// Enable exceptions for all modules.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bForceEnableExceptions = false;

		/// <summary>
		/// Enable inlining for all modules.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bUseInlining = true;

		/// <summary>
		/// Enable exceptions for all modules.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bForceEnableObjCExceptions = false;

		/// <summary>
		/// Enable RTTI for all modules.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bForceEnableRTTI = false;

		/// <summary>
		/// Compile server-only code.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bWithServerCode = true;

		/// <summary>
		/// Whether to include stats support even without the engine.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bCompileWithStatsWithoutEngine = false;

		/// <summary>
		/// Whether to include plugin support.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		[ConfigFile(ConfigHierarchyType.Engine, "/Script/BuildSettings.BuildSettings", "bCompileWithPluginSupport")]
		public bool bCompileWithPluginSupport = false;

		/// <summary>
		/// Whether to allow plugins which support all target platforms.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bIncludePluginsForTargetPlatforms = false;

        /// <summary>
        /// Whether to include PerfCounters support.
        /// </summary>
		[RequiresUniqueBuildEnvironment]
		[ConfigFile(ConfigHierarchyType.Engine, "/Script/BuildSettings.BuildSettings", "bWithPerfCounters")]
        public bool bWithPerfCounters = false;

		/// <summary>
		/// Whether to enable support for live coding
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bWithLiveCoding = false;

        /// <summary>
        /// Whether to turn on logging for test/shipping builds.
        /// </summary>
		[RequiresUniqueBuildEnvironment]
        public bool bUseLoggingInShipping = false;

		/// <summary>
		/// Whether to turn on logging to memory for test/shipping builds.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bLoggingToMemoryEnabled;

		/// <summary>
		/// Whether to check that the process was launched through an external launcher.
		/// </summary>
        public bool bUseLauncherChecks = false;

		/// <summary>
		/// Whether to turn on checks (asserts) for test/shipping builds.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bUseChecksInShipping = false;

		/// <summary>
		/// True if we need FreeType support.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		[ConfigFile(ConfigHierarchyType.Engine, "/Script/BuildSettings.BuildSettings", "bCompileFreeType")]
		public bool bCompileFreeType = true;

		/// <summary>
		/// True if we want to favor optimizing size over speed.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		[ConfigFile(ConfigHierarchyType.Engine, "/Script/BuildSettings.BuildSettings", "bCompileForSize")]
		public bool bCompileForSize = false;

        /// <summary>
        /// Whether to compile development automation tests.
        /// </summary>
        public bool bForceCompileDevelopmentAutomationTests = false;

        /// <summary>
        /// Whether to compile performance automation tests.
        /// </summary>
        public bool bForceCompilePerformanceAutomationTests = false;

		/// <summary>
		/// If true, event driven loader will be used in cooked builds. @todoio This needs to be replaced by a runtime solution after async loading refactor.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		public bool bEventDrivenLoader;

		/// <summary>
		/// Whether the XGE controller worker and modules should be included in the engine build.
		/// These are required for distributed shader compilation using the XGE interception interface.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bUseXGEController = true;

		/// <summary>
		/// Whether to use backwards compatible defaults for this module. By default, engine modules always use the latest default settings, while project modules do not (to support
		/// an easier migration path).
		/// </summary>
		public bool bUseBackwardsCompatibleDefaults = true;

		/// <summary>
		/// Enables "include what you use" by default for modules in this target. Changes the default PCH mode for any module in this project to PCHUsageModule.UseExplicitOrSharedPCHs.
		/// </summary>
		[CommandLine("-IWYU")]
		public bool bIWYU = false;

		/// <summary>
		/// Enforce "include what you use" rules; warns if monolithic headers (Engine.h, UnrealEd.h, etc...) are used, and checks that source files include their matching header first.
		/// </summary>
		public bool bEnforceIWYU = true;

		/// <summary>
		/// Whether the final executable should export symbols.
		/// </summary>
		public bool bHasExports = false;

		/// <summary>
		/// Make static libraries for all engine modules as intermediates for this target.
		/// </summary>
		[CommandLine("-Precompile")]
		public bool bPrecompile = false;

		/// <summary>
		/// Whether we should compile with support for OS X 10.9 Mavericks. Used for some tools that we need to be compatible with this version of OS X.
		/// </summary>
		public bool bEnableOSX109Support = false;

		/// <summary>
		/// True if this is a console application that's being built.
		/// </summary>
		public bool bIsBuildingConsoleApplication = false;

		/// <summary>
		/// True if debug symbols that are cached for some platforms should not be created.
		/// </summary>
		public bool bDisableSymbolCache = true;

		/// <summary>
		/// Whether to unify C++ code into larger files for faster compilation.
		/// </summary>
		[CommandLine("-DisableUnity", Value = "false")]
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bUseUnityBuild = true;

		/// <summary>
		/// Whether to force C++ source files to be combined into larger files for faster compilation.
		/// </summary>
		[CommandLine("-ForceUnity")]
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bForceUnityBuild = false;

		/// <summary>
		/// Use a heuristic to determine which files are currently being iterated on and exclude them from unity blobs, result in faster
		/// incremental compile times. The current implementation uses the read-only flag to distinguish the working set, assuming that files will
		/// be made writable by the source control system if they are being modified. This is true for Perforce, but not for Git.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bUseAdaptiveUnityBuild = true;

		/// <summary>
		/// Disable optimization for files that are in the adaptive non-unity working set.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bAdaptiveUnityDisablesOptimizations = false;

		/// <summary>
		/// Disables force-included PCHs for files that are in the adaptive non-unity working set.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bAdaptiveUnityDisablesPCH = false;

		/// <summary>
		/// Backing storage for bAdaptiveUnityDisablesProjectPCH.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		bool? bAdaptiveUnityDisablesProjectPCHForProjectPrivate;

		/// <summary>
		/// Whether to disable force-included PCHs for project source files in the adaptive non-unity working set. Defaults to bAdaptiveUnityDisablesPCH;
		/// </summary>
		public bool bAdaptiveUnityDisablesPCHForProject
		{
			get { return bAdaptiveUnityDisablesProjectPCHForProjectPrivate ?? bAdaptiveUnityDisablesPCH; }
			set { bAdaptiveUnityDisablesProjectPCHForProjectPrivate = value; }
		}

		/// <summary>
		/// Creates a dedicated PCH for each source file in the working set, allowing faster iteration on cpp-only changes.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bAdaptiveUnityCreatesDedicatedPCH = false;

		/// <summary>
		/// Creates a dedicated PCH for each source file in the working set, allowing faster iteration on cpp-only changes.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bAdaptiveUnityEnablesEditAndContinue = false;

		/// <summary>
		/// The number of source files in a game module before unity build will be activated for that module.  This
		/// allows small game modules to have faster iterative compile times for single files, at the expense of slower full
		/// rebuild times.  This setting can be overridden by the bFasterWithoutUnity option in a module's Build.cs file.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public int MinGameModuleSourceFilesForUnityBuild = 32;

		/// <summary>
		/// Forces shadow variable warnings to be treated as errors on platforms that support it.
		/// </summary>
		[CommandLine("-ShadowVariableErrors")]
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bShadowVariableErrors = false;

		/// <summary>
		/// Forces the use of undefined identifiers in conditional expressions to be treated as errors.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bUndefinedIdentifierErrors = true;

		/// <summary>
		/// New Monolithic Graphics drivers have optional "fast calls" replacing various D3d functions
		/// </summary>
		[CommandLine("-FastMonoCalls", Value = "true")]
		[CommandLine("-NoFastMonoCalls", Value = "false")]
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bUseFastMonoCalls = true;

		/// <summary>
		/// New Xbox driver supports a "fast semantics" context type. This switches it on for the immediate and deferred contexts
		/// Try disabling this if you see rendering issues and/or crashes inthe Xbox RHI.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bUseFastSemanticsRenderContexts = true;

		/// <summary>
		/// An approximate number of bytes of C++ code to target for inclusion in a single unified C++ file.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public int NumIncludedBytesPerUnityCPP = 384 * 1024;

		/// <summary>
		/// Whether to stress test the C++ unity build robustness by including all C++ files files in a project from a single unified file.
		/// </summary>
		[CommandLine("-StressTestUnity")]
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bStressTestUnity = false;

		/// <summary>
		/// Whether to force debug info to be generated.
		/// </summary>
		[CommandLine("-ForceDebugInfo")]
		public bool bForceDebugInfo = false;

		/// <summary>
		/// Whether to globally disable debug info generation; see DebugInfoHeuristics.cs for per-config and per-platform options.
		/// </summary>
		[CommandLine("-NoDebugInfo")]
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bDisableDebugInfo = false;

		/// <summary>
		/// Whether to disable debug info generation for generated files. This improves link times for modules that have a lot of generated glue code.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bDisableDebugInfoForGeneratedCode = false;

		/// <summary>
		/// Whether to disable debug info on PC in development builds (for faster developer iteration, as link times are extremely fast with debug info disabled).
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bOmitPCDebugInfoInDevelopment = false;

		/// <summary>
		/// Whether PDB files should be used for Visual C++ builds.
		/// </summary>
		[CommandLine("-NoPDB", Value = "false")]
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bUsePDBFiles = false;

		/// <summary>
		/// Whether PCH files should be used.
		/// </summary>
		[CommandLine("-NoPCH", Value = "false")]
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bUsePCHFiles = true;

		/// <summary>
		/// The minimum number of files that must use a pre-compiled header before it will be created and used.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public int MinFilesUsingPrecompiledHeader = 6;

		/// <summary>
		/// When enabled, a precompiled header is always generated for game modules, even if there are only a few source files
		/// in the module.  This greatly improves compile times for iterative changes on a few files in the project, at the expense of slower
		/// full rebuild times for small game projects.  This can be overridden by setting MinFilesUsingPrecompiledHeaderOverride in
		/// a module's Build.cs file.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bForcePrecompiledHeaderForGameModules = true;

		/// <summary>
		/// Whether to use incremental linking or not. Incremental linking can yield faster iteration times when making small changes.
		/// Currently disabled by default because it tends to behave a bit buggy on some computers (PDB-related compile errors).
		/// </summary>
		[CommandLine("-IncrementalLinking")]
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bUseIncrementalLinking = false;

		/// <summary>
		/// Whether to allow the use of link time code generation (LTCG).
		/// </summary>
		[CommandLine("-LTCG")]
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bAllowLTCG = false;

        /// <summary>
        /// Whether to enable Profile Guided Optimization (PGO) instrumentation in this build.
        /// </summary>
        [CommandLine("-PGOProfile", Value = "true")]
        [XmlConfigFile(Category = "BuildConfiguration")]
        public bool bPGOProfile = false;

        /// <summary>
        /// Whether to optimize this build with Profile Guided Optimization (PGO).
        /// </summary>
        [CommandLine("-PGOOptimize", Value = "true")]
        [XmlConfigFile(Category = "BuildConfiguration")]
        public bool bPGOOptimize = false;

        /// <summary>
        /// Whether to allow the use of ASLR (address space layout randomization) if supported. Only
        /// applies to shipping builds.
        /// </summary>
        [XmlConfigFile(Category = "BuildConfiguration")]
		public bool bAllowASLRInShipping = true;

		/// <summary>
		/// Whether to support edit and continue.  Only works on Microsoft compilers.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bSupportEditAndContinue = false;

		/// <summary>
		/// Whether to omit frame pointers or not. Disabling is useful for e.g. memory profiling on the PC.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bOmitFramePointers = true;

		/// <summary>
		/// Whether to strip iOS symbols or not (implied by bGeneratedSYMFile).
		/// </summary>
		[Obsolete("bStripSymbolsOnIOS has been deprecated. Use IOSPlatform.bStripSymbols instead.")]
		public bool bStripSymbolsOnIOS
		{
			get { return IOSPlatform.bStripSymbols; }
			set { IOSPlatform.bStripSymbols = value; }
		}

		/// <summary>
		/// If true, then a stub IPA will be generated when compiling is done (minimal files needed for a valid IPA).
		/// </summary>
		[Obsolete("bCreateStubIPA has been deprecated. Use IOSPlatform.bCreateStubIPA instead.")]
		public bool bCreateStubIPA
		{
			get { return IOSPlatform.bCreateStubIPA; }
			set { IOSPlatform.bCreateStubIPA = value; }
		}

		/// <summary>
		/// If true, then enable memory profiling in the build (defines USE_MALLOC_PROFILER=1 and forces bOmitFramePointers=false).
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bUseMallocProfiler = false;

		/// <summary>
		/// Enables "Shared PCHs", a feature which significantly speeds up compile times by attempting to
		/// share certain PCH files between modules that UBT detects is including those PCH's header files.
		/// </summary>
		[CommandLine("-NoSharedPCH", Value = "false")]
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bUseSharedPCHs = true;

		/// <summary>
		/// True if Development and Release builds should use the release configuration of PhysX/APEX.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bUseShippingPhysXLibraries = false;

        /// <summary>
        /// True if Development and Release builds should use the checked configuration of PhysX/APEX. if bUseShippingPhysXLibraries is true this is ignored.
        /// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
        public bool bUseCheckedPhysXLibraries = false;

		/// <summary>
		/// Tells the UBT to check if module currently being built is violating EULA.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bCheckLicenseViolations = true;

		/// <summary>
		/// Tells the UBT to break build if module currently being built is violating EULA.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bBreakBuildOnLicenseViolation = true;

		/// <summary>
		/// Whether to use the :FASTLINK option when building with /DEBUG to create local PDBs on Windows. Fast, but currently seems to have problems finding symbols in the debugger.
		/// </summary>
		[CommandLine("-FastPDB")]
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool? bUseFastPDBLinking;

		/// <summary>
		/// Outputs a map file as part of the build.
		/// </summary>
		[CommandLine("-MapFile")]
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bCreateMapFile = false;

		/// <summary>
		/// Bundle version for Mac apps.
		/// </summary>
		[CommandLine("-BundleVersion")]
		public string BundleVersion = null;

		/// <summary>
		/// Whether to deploy the executable after compilation on platforms that require deployment.
		/// </summary>
		[CommandLine("-Deploy")]
		[CommandLine("-SkipDeploy", Value = "false")]
		public bool bDeployAfterCompile = false;

		/// <summary>
		/// When enabled, allows XGE to compile pre-compiled header files on remote machines.  Otherwise, PCHs are always generated locally.
		/// </summary>
		public bool bAllowRemotelyCompiledPCHs = false;

		/// <summary>
		/// Whether headers in system paths should be checked for modification when determining outdated actions.
		/// </summary>
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bCheckSystemHeadersForModification;

		/// <summary>
		/// Whether to disable linking for this target.
		/// </summary>
		[CommandLine("-NoLink")]
		public bool bDisableLinking = false;

		/// <summary>
		/// Indicates that this is a formal build, intended for distribution. This flag is automatically set to true when Build.version has a changelist set.
		/// The only behavior currently bound to this flag is to compile the default resource file separately for each binary so that the OriginalFilename field is set correctly. 
		/// By default, we only compile the resource once to reduce build times.
		/// </summary>
		[CommandLine("-Formal")]
		public bool bFormalBuild = false;

		/// <summary>
		/// Whether to clean Builds directory on a remote Mac before building.
		/// </summary>
		[CommandLine("-FlushMac")]
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bFlushBuildDirOnRemoteMac = false;

		/// <summary>
		/// Whether to write detailed timing info from the compiler and linker.
		/// </summary>
		[CommandLine("-Timing")]
		[XmlConfigFile(Category = "BuildConfiguration")]
		public bool bPrintToolChainTimingInfo = false;

		/// <summary>
		/// Whether to hide symbols by default on POSIX platforms
		/// </summary>
		[CommandLine("-HideSymbolsByDefault")]
		public bool bHideSymbolsByDefault;

		/// <summary>
		/// Allows overriding the toolchain to be created for this target. This must match the name of a class declared in the UnrealBuildTool assembly.
		/// </summary>
		[CommandLine("-ToolChain")]
		public string ToolChainName = null;

		/// <summary>
		/// Whether to allow engine configuration to determine if we can load unverified certificates.
		/// </summary>
		public bool bDisableUnverifiedCertificates = false;

		/// <summary>
		/// Whether to load generated ini files in cooked build, (GameUserSettings.ini loaded either way)
		/// </summary>
		public bool bAllowGeneratedIniWhenCooked = true;

		/// <summary>
		/// Whether to load non-ufs ini files in cooked build, (GameUserSettings.ini loaded either way)
		/// </summary>
		public bool bAllowNonUFSIniWhenCooked = true;

		/// <summary>
		/// Add all the public folders as include paths for the compile environment.
		/// </summary>
		public bool bLegacyPublicIncludePaths = true;

		/// <summary>
		/// Which C++ stanard to use for compiling this target
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		[XmlConfigFile(Category = "BuildConfiguration")]
		public CppStandardVersion CppStandard = CppStandardVersion.Latest;

		/// <summary>
		/// Do not allow manifest changes when building this target. Used to cause earlier errors when building multiple targets with a shared build environment.
		/// </summary>
		[CommandLine("-NoManifestChanges")]
		internal bool bNoManifestChanges = false;

		/// <summary>
		/// The build version string
		/// </summary>
		[CommandLine("-BuildVersion")]
		public string BuildVersion;

		/// <summary>
		/// Specifies how to link modules in this target (monolithic or modular). This is currently protected for backwards compatibility. Call the GetLinkType() accessor
		/// until support for the deprecated ShouldCompileMonolithic() override has been removed.
		/// </summary>
		public TargetLinkType LinkType
		{
			get
			{
				return (LinkTypePrivate != TargetLinkType.Default) ? LinkTypePrivate : ((Type == global::UnrealBuildTool.TargetType.Editor) ? TargetLinkType.Modular : TargetLinkType.Monolithic);
			}
			set
			{
				LinkTypePrivate = value;
			}
		}

		/// <summary>
		/// Backing storage for the LinkType property.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		[CommandLine("-Monolithic", Value ="Monolithic")]
		[CommandLine("-Modular", Value ="Modular")]
		TargetLinkType LinkTypePrivate = TargetLinkType.Default;

		/// <summary>
		/// Macros to define globally across the whole target.
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		[CommandLine("-Define:")]
		public List<string> GlobalDefinitions = new List<string>();

		/// <summary>
		/// Macros to define across all macros in the project.
		/// </summary>
		public List<string> ProjectDefinitions = new List<string>();

		/// <summary>
		/// Specifies the name of the launch module. For modular builds, this is the module that is compiled into the target's executable.
		/// </summary>
		public string LaunchModuleName
		{
			get
			{
				return (LaunchModuleNamePrivate == null && Type != global::UnrealBuildTool.TargetType.Program)? "Launch" : LaunchModuleNamePrivate;
			}
			set
			{
				LaunchModuleNamePrivate = value;
			}
		}

		/// <summary>
		/// Backing storage for the LaunchModuleName property.
		/// </summary>
		private string LaunchModuleNamePrivate;

		/// <summary>
		/// List of additional modules to be compiled into the target.
		/// </summary>
		public List<string> ExtraModuleNames = new List<string>();

		/// <summary>
		/// Path to a manifest to output for this target
		/// </summary>
		[CommandLine("-Manifest")]
		public List<FileReference> ManifestFileNames = new List<FileReference>();

		/// <summary>
		/// Path to a list of dependencies for this target, when precompiling
		/// </summary>
		[CommandLine("-DependencyList")]
		public List<FileReference> DependencyListFileNames = new List<FileReference>();

		/// <summary>
		/// Specifies the build environment for this target. See TargetBuildEnvironment for more information on the available options.
		/// </summary>
		[CommandLine("-SharedBuildEnvironment", Value = "Shared")]
		[CommandLine("-UniqueBuildEnvironment", Value = "Unique")]
		public TargetBuildEnvironment BuildEnvironment = TargetBuildEnvironment.Default;

		/// <summary>
		/// Specifies a list of steps which should be executed before this target is built, in the context of the host platform's shell.
		/// The following variables will be expanded before execution: 
		/// $(EngineDir), $(ProjectDir), $(TargetName), $(TargetPlatform), $(TargetConfiguration), $(TargetType), $(ProjectFile).
		/// </summary>
		public List<string> PreBuildSteps = new List<string>();

		/// <summary>
		/// Specifies a list of steps which should be executed after this target is built, in the context of the host platform's shell.
		/// The following variables will be expanded before execution: 
		/// $(EngineDir), $(ProjectDir), $(TargetName), $(TargetPlatform), $(TargetConfiguration), $(TargetType), $(ProjectFile).
		/// </summary>
		public List<string> PostBuildSteps = new List<string>();

		/// <summary>
		/// Specifies additional build products produced as part of this target.
		/// </summary>
		public List<string> AdditionalBuildProducts = new List<string>();

		/// <summary>
		/// Additional arguments to pass to the compiler
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		[CommandLine("-CompilerArguments=")]
		public string AdditionalCompilerArguments;

		/// <summary>
		/// Additional arguments to pass to the linker
		/// </summary>
		[RequiresUniqueBuildEnvironment]
		[CommandLine("-LinkerArguments=")]
		public string AdditionalLinkerArguments;

		/// <summary>
		/// When generating project files, specifies the name of the project file to use when there are multiple targets of the same type.
		/// </summary>
		public string GeneratedProjectName;

		/// <summary>
		/// Android-specific target settings.
		/// </summary>
		public AndroidTargetRules AndroidPlatform = new AndroidTargetRules();

		/// <summary>
		/// HTML5-specific target settings.
		/// </summary>
		public HTML5TargetRules HTML5Platform = new HTML5TargetRules();

		/// <summary>
		/// IOS-specific target settings.
		/// </summary>
		public IOSTargetRules IOSPlatform = new IOSTargetRules();

		/// <summary>
		/// Lumin-specific target settings.
		/// </summary>
		public LuminTargetRules LuminPlatform = new LuminTargetRules();

		/// <summary>
		/// Linux-specific target settings.
		/// </summary>
		public LinuxTargetRules LinuxPlatform = new LinuxTargetRules();

		/// <summary>
		/// Mac-specific target settings.
		/// </summary>
		public MacTargetRules MacPlatform = new MacTargetRules();

		/// <summary>
		/// PS4-specific target settings.
		/// </summary>
		public PS4TargetRules PS4Platform = new PS4TargetRules();

		/// <summary>
		/// Switch-specific target settings.
		/// </summary>
		public SwitchTargetRules SwitchPlatform = new SwitchTargetRules();

		/// <summary>
		/// Windows-specific target settings.
		/// </summary>
		public WindowsTargetRules WindowsPlatform = new WindowsTargetRules();

		/// <summary>
		/// Xbox One-specific target settings.
		/// </summary>
		public XboxOneTargetRules XboxOnePlatform = new XboxOneTargetRules();

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="Target">Information about the target being built</param>
		public TargetRules(TargetInfo Target)
		{
			this.Name = Target.Name;
			this.Platform = Target.Platform;
			this.Configuration = Target.Configuration;
			this.Architecture = Target.Architecture;
			this.ProjectFile = Target.ProjectFile;
			this.Version = Target.Version;

			// Read settings from config files
			foreach(object ConfigurableObject in GetConfigurableObjects())
			{
				ConfigCache.ReadSettings(DirectoryReference.FromFile(ProjectFile), Platform, ConfigurableObject);
				XmlConfig.ApplyTo(ConfigurableObject);
			}

			// Allow the build platform to set defaults for this target
			if(Platform != UnrealTargetPlatform.Unknown)
			{
				UEBuildPlatform.GetBuildPlatform(Platform).ResetTarget(this);
			}

			// If we've got a changelist set, set that we're making a formal build
			bFormalBuild = (Version.Changelist != 0 && Version.IsPromotedBuild);

			// Set the default build version
			if(String.IsNullOrEmpty(BuildVersion))
			{
				if(String.IsNullOrEmpty(Target.Version.BuildVersionString))
				{
					BuildVersion = String.Format("{0}-CL-{1}", Target.Version.BranchName, Target.Version.Changelist);
				}
				else
				{
					BuildVersion = Target.Version.BuildVersionString;
				}
			}

			// Setup macros for signing and encryption keys
			EncryptionAndSigning.CryptoSettings CryptoSettings = EncryptionAndSigning.ParseCryptoSettings(DirectoryReference.FromFile(ProjectFile), Platform);
			if (CryptoSettings.IsAnyEncryptionEnabled())
			{
				ProjectDefinitions.Add(String.Format("IMPLEMENT_ENCRYPTION_KEY_REGISTRATION()=UE_REGISTER_ENCRYPTION_KEY({0})", FormatHexBytes(CryptoSettings.EncryptionKey.Key)));
			}
			else
			{
				ProjectDefinitions.Add("IMPLEMENT_ENCRYPTION_KEY_REGISTRATION()=");
			}

			if (CryptoSettings.IsPakSigningEnabled())
			{
				ProjectDefinitions.Add(String.Format("IMPLEMENT_SIGNING_KEY_REGISTRATION()=UE_REGISTER_SIGNING_KEY(UE_LIST_ARGUMENT({0}), UE_LIST_ARGUMENT({1}))", FormatHexBytes(CryptoSettings.SigningKey.PublicKey.Exponent), FormatHexBytes(CryptoSettings.SigningKey.PublicKey.Modulus)));
			}
			else
			{
				ProjectDefinitions.Add("IMPLEMENT_SIGNING_KEY_REGISTRATION()=");
			}
		}

		/// <summary>
		/// Formats an array of bytes as a sequence of values
		/// </summary>
		/// <param name="Data">The data to convert into a string</param>
		/// <returns>List of hexadecimal bytes</returns>
		private static string FormatHexBytes(byte[] Data)
		{
			return String.Join(",", Data.Select(x => String.Format("0x{0:X2}", x)));
		}

		/// <summary>
		/// Override any settings required for the selected target type
		/// </summary>
		internal void SetOverridesForTargetType()
		{
			if(Type == global::UnrealBuildTool.TargetType.Game)
			{
				// Do not include the editor
				bBuildWithEditorOnlyData = false;

				// Require cooked data
				bBuildRequiresCookedData = true;

				// Compile the engine
				bCompileAgainstEngine = true;

				// only have exports in modular builds
				bHasExports = (LinkType == TargetLinkType.Modular);

				// Tag it as a 'Game' build
				GlobalDefinitions.Add("UE_GAME=1");
			}
			else if(Type == global::UnrealBuildTool.TargetType.Client)
			{
				// Do not include the editor
				bBuildWithEditorOnlyData = false;

				// Require cooked data
				bBuildRequiresCookedData = true;

				// Compile the engine
				bCompileAgainstEngine = true;

				// Disable server code
				bWithServerCode = false;

				// only have exports in modular builds
				bHasExports = (LinkType == TargetLinkType.Modular);

				// Tag it as a 'Game' build
				GlobalDefinitions.Add("UE_GAME=1");
			}
			else if(Type == global::UnrealBuildTool.TargetType.Editor)
			{
				// Do not include the editor
				bBuildWithEditorOnlyData = true;

				// Require cooked data
				bBuildRequiresCookedData = false;

				// Compile the engine
				bCompileAgainstEngine = true;

				//enable PerfCounters
				bWithPerfCounters = true;

				// Include all plugins
				bIncludePluginsForTargetPlatforms = true;

				// only have exports in modular builds
				bHasExports = (LinkType == TargetLinkType.Modular);

				// Tag it as a 'Editor' build
				GlobalDefinitions.Add("UE_EDITOR=1");
			}
			else if(Type == global::UnrealBuildTool.TargetType.Server)
			{
				// Do not include the editor
				bBuildWithEditorOnlyData = false;

				// Require cooked data
				bBuildRequiresCookedData = true;

				// Compile the engine
				bCompileAgainstEngine = true;
			
				//enable PerfCounters
				bWithPerfCounters = true;

				// only have exports in modular builds
				bHasExports = (LinkType == TargetLinkType.Modular);

				// Tag it as a 'Server' build
				GlobalDefinitions.Add("UE_SERVER=1");
				GlobalDefinitions.Add("USE_NULL_RHI=1");
			}
		}

		/// <summary>
		/// Checks whether nativization is enabled for this target, and determine the path for the nativized plugin
		/// </summary>
		/// <returns>The nativized plugin file, or null if nativization is not enabled</returns>
		internal FileReference GetNativizedPlugin()
		{
			if (ProjectFile != null && (Type == TargetType.Game || Type == TargetType.Client || Type == TargetType.Server))
			{
				// Read the config files for this project
				ConfigHierarchy Config = ConfigCache.ReadHierarchy(ConfigHierarchyType.Game, ProjectFile.Directory, BuildHostPlatform.Current.Platform);
				if (Config != null)
				{
					// Determine whether or not the user has enabled nativization of Blueprint assets at cook time (default is 'Disabled')
					string NativizationMethod;
					if (Config.TryGetValue("/Script/UnrealEd.ProjectPackagingSettings", "BlueprintNativizationMethod", out NativizationMethod) && NativizationMethod != "Disabled")
					{
						string PlatformName;
						if (Platform == UnrealTargetPlatform.Win32 || Platform == UnrealTargetPlatform.Win64)
						{
							PlatformName = "Windows";
						}
						else
						{
							PlatformName = Platform.ToString();
						}

						// Temp fix to force platforms that only support "Game" configurations at cook time to the correct path.
						string ProjectTargetType;
						if (Platform == UnrealTargetPlatform.Win32 || Platform == UnrealTargetPlatform.Win64 || Platform == UnrealTargetPlatform.Linux || Platform == UnrealTargetPlatform.Mac)
						{
							ProjectTargetType = Type.ToString();
						}
						else
						{
							ProjectTargetType = "Game";
						}

						FileReference PluginFile = FileReference.Combine(ProjectFile.Directory, "Intermediate", "Plugins", "NativizedAssets", PlatformName, ProjectTargetType, "NativizedAssets.uplugin");
						if (FileReference.Exists(PluginFile))
						{
							return PluginFile;
						}
						else
						{
							Log.TraceWarning("{0} is configured for nativization, but is missing the generated code plugin at \"{1}\". Make sure to cook {2} data before attempting to build the {3} target. If data was cooked with nativization enabled, this can also mean there were no Blueprint assets that required conversion, in which case this warning can be safely ignored.", Name, PluginFile.FullName, Type.ToString(), Platform.ToString());
						}
					}
				}
			}
			return null;
		}

		/// <summary>
		/// Gets a list of platforms that this target supports
		/// </summary>
		/// <returns>Array of platforms that the target supports</returns>
		internal UnrealTargetPlatform[] GetSupportedPlatforms()
		{
			// Otherwise take the SupportedPlatformsAttribute from the first type in the inheritance chain that supports it
			for (Type CurrentType = GetType(); CurrentType != null; CurrentType = CurrentType.BaseType)
			{
				object[] Attributes = GetType().GetCustomAttributes(typeof(SupportedPlatformsAttribute), false);
				if (Attributes.Length > 0)
				{
					return Attributes.OfType<SupportedPlatformsAttribute>().SelectMany(x => x.Platforms).Distinct().ToArray();
				}
			}

			// Otherwise, get the default for the target type
			if (Type == TargetType.Program)
			{
				return Utils.GetPlatformsInClass(UnrealPlatformClass.Desktop);
			}
			else if (Type == TargetType.Editor)
			{
				return Utils.GetPlatformsInClass(UnrealPlatformClass.Editor);
			}
			else
			{
				return Utils.GetPlatformsInClass(UnrealPlatformClass.All);
			}
		}

		/// <summary>
		/// Finds all the subobjects which can be configured by command line options and config files
		/// </summary>
		/// <returns>Sequence of objects</returns>
		internal IEnumerable<object> GetConfigurableObjects()
		{
			yield return this;
			yield return AndroidPlatform;
			yield return HTML5Platform;
			yield return IOSPlatform;
			yield return LuminPlatform;
			yield return LinuxPlatform;
			yield return MacPlatform;
			yield return PS4Platform;
			yield return SwitchPlatform;
			yield return WindowsPlatform;
			yield return XboxOnePlatform;
		}

		/// <summary>
		/// Gets the host platform being built on
		/// </summary>
		public UnrealTargetPlatform HostPlatform
		{
			get { return BuildHostPlatform.Current.Platform; }
		}

		/// <summary>
		/// Expose the bGenerateProjectFiles flag to targets, so we can modify behavior as appropriate for better intellisense
		/// </summary>
		public bool bGenerateProjectFiles
		{
			get { return ProjectFileGenerator.bGenerateProjectFiles; }
		}

		/// <summary>
		/// Expose a setting for whether or not the engine is installed
		/// </summary>
		/// <returns>Flag for whether the engine is installed</returns>
		public bool bIsEngineInstalled
		{
			get { return UnrealBuildTool.IsEngineInstalled(); }
		}
	}

	/// <summary>
	/// Read-only wrapper around an existing TargetRules instance. This exposes target settings to modules without letting them to modify the global environment.
	/// </summary>
	public partial class ReadOnlyTargetRules
	{
		/// <summary>
		/// The writeable TargetRules instance
		/// </summary>
		TargetRules Inner;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="Inner">The TargetRules instance to wrap around</param>
		public ReadOnlyTargetRules(TargetRules Inner)
		{
			this.Inner = Inner;
			AndroidPlatform = new ReadOnlyAndroidTargetRules(Inner.AndroidPlatform);
			HTML5Platform = new ReadOnlyHTML5TargetRules(Inner.HTML5Platform);
			IOSPlatform = new ReadOnlyIOSTargetRules(Inner.IOSPlatform);
			LuminPlatform = new ReadOnlyLuminTargetRules(Inner.LuminPlatform);
			LinuxPlatform = new ReadOnlyLinuxTargetRules(Inner.LinuxPlatform);
			MacPlatform = new ReadOnlyMacTargetRules(Inner.MacPlatform);
			PS4Platform = new ReadOnlyPS4TargetRules(Inner.PS4Platform);
			SwitchPlatform = new ReadOnlySwitchTargetRules(Inner.SwitchPlatform);
			WindowsPlatform = new ReadOnlyWindowsTargetRules(Inner.WindowsPlatform);
			XboxOnePlatform = new ReadOnlyXboxOneTargetRules(Inner.XboxOnePlatform);
		}

		/// <summary>
		/// Accessors for fields on the inner TargetRules instance
		/// </summary>
		#region Read-only accessor properties 
		#if !__MonoCS__
		#pragma warning disable CS1591
		#endif

		public string Name
		{
			get { return Inner.Name; }
		}

		internal FileReference File
		{
			get { return Inner.File; }
		}

		public UnrealTargetPlatform Platform
		{
			get { return Inner.Platform; }
		}

		public UnrealTargetConfiguration Configuration
		{
			get { return Inner.Configuration; }
		}

		public string Architecture
		{
			get { return Inner.Architecture; }
		}

		public FileReference ProjectFile
		{
			get { return Inner.ProjectFile; }
		}

		public ReadOnlyBuildVersion Version
		{
			get { return Inner.Version; }
		}

		public TargetType Type
		{
			get { return Inner.Type; }
		}

		public bool bUsesSteam
		{
			get { return Inner.bUsesSteam; }
		}

		public bool bUsesCEF3
		{
			get { return Inner.bUsesCEF3; }
		}

		public bool bUsesSlate
		{
			get { return Inner.bUsesSlate; }
		}

		public bool bUseStaticCRT
		{
			get { return Inner.bUseStaticCRT; }
		}

		public bool bDebugBuildsActuallyUseDebugCRT
		{
			get { return Inner.bDebugBuildsActuallyUseDebugCRT; }
		}

		public bool bOutputPubliclyDistributable
		{
			get { return Inner.bOutputPubliclyDistributable; }
		}

		public UnrealTargetConfiguration UndecoratedConfiguration
		{
			get { return Inner.UndecoratedConfiguration; }
		}

		[Obsolete("bBuildAllPlugins has been deprecated. Use bPrecompile to build all modules which are not part of the target.")]
		public bool bBuildAllPlugins
		{
			get { return Inner.bBuildAllPlugins; }
		}

		public bool bBuildAllModules
		{
			get { return Inner.bBuildAllModules; }
		}

		public IEnumerable<string> AdditionalPlugins
		{
			get { return Inner.AdditionalPlugins; }
		}

		public IEnumerable<string> EnablePlugins
		{
			get { return Inner.EnablePlugins; }
		}

		public IEnumerable<string> DisablePlugins
		{
			get { return Inner.DisablePlugins; }
		}

		public string PakSigningKeysFile
		{
			get { return Inner.PakSigningKeysFile; }
		}

		public string SolutionDirectory
		{
			get { return Inner.SolutionDirectory; }
		}

		public bool? bBuildInSolutionByDefault
		{
			get { return Inner.bBuildInSolutionByDefault; }
		}

		public string ExeBinariesSubFolder
		{
			get { return Inner.ExeBinariesSubFolder; }
		}

		public EGeneratedCodeVersion GeneratedCodeVersion
		{
			get { return Inner.GeneratedCodeVersion; }
		}
		public bool bEnableMeshEditor
		{
			get { return Inner.bEnableMeshEditor; }
		}

		public bool bCompileChaos
		{
			get { return Inner.bCompileChaos; }
		}

        public bool bUseChaos
        {
            get { return Inner.bUseChaos; }
        }

        public bool bCompileImmediatePhysics
		{
			get { return Inner.bCompileImmediatePhysics; }
		}

		public bool bCustomSceneQueryStructure
		{
			get { return Inner.bCustomSceneQueryStructure; }
		}

		public bool bCompilePhysX
		{
			get { return Inner.bCompilePhysX; }
		}

		public bool bCompileAPEX
		{
			get { return Inner.bCompileAPEX; }
		}

		public bool bCompileNvCloth
		{
			get { return Inner.bCompileNvCloth; }
		}

		public bool bCompileICU
		{
			get { return Inner.bCompileICU; }
		}

		public bool bCompileCEF3
		{
			get { return Inner.bCompileCEF3; }
		}

		public bool bBuildEditor
		{
			get { return Inner.bBuildEditor; }
		}

		public bool bBuildRequiresCookedData
		{
			get { return Inner.bBuildRequiresCookedData; }
		}

		public bool bBuildWithEditorOnlyData
		{
			get { return Inner.bBuildWithEditorOnlyData; }
		}

		public bool bBuildDeveloperTools
		{
			get { return Inner.bBuildDeveloperTools; }
		}

		public bool bForceBuildTargetPlatforms
		{
			get { return Inner.bForceBuildTargetPlatforms; }
		}

		public bool bForceBuildShaderFormats
		{
			get { return Inner.bForceBuildShaderFormats; }
		}

		public bool bCompileCustomSQLitePlatform
		{
			get { return Inner.bCompileCustomSQLitePlatform; }
		}

		[Obsolete("bCompileLeanAndMeanUE is deprecated. Use bBuildDeveloperTools instead.")]
		public bool bCompileLeanAndMeanUE
		{
			get { return Inner.bCompileLeanAndMeanUE; }
		}

        public bool bUseCacheFreedOSAllocs
        {
            get { return Inner.bUseCacheFreedOSAllocs; }
        }

        public bool bCompileAgainstEngine
		{
			get { return Inner.bCompileAgainstEngine; }
		}

		public bool bCompileAgainstCoreUObject
		{
			get { return Inner.bCompileAgainstCoreUObject; }
		}

		public bool bCompileAgainstApplicationCore
		{
			get { return Inner.bCompileAgainstApplicationCore; }
		}

		public bool bCompileRecast
		{
			get { return Inner.bCompileRecast; }
		}

		public bool bCompileSpeedTree
		{
			get { return Inner.bCompileSpeedTree; }
		}

		public bool bForceEnableExceptions
		{
			get { return Inner.bForceEnableExceptions; }
		}

		public bool bForceEnableObjCExceptions
		{
			get { return Inner.bForceEnableObjCExceptions; }
		}

		public bool bForceEnableRTTI
		{
			get { return Inner.bForceEnableRTTI; }
		}

		public bool bUseInlining
		{
			get { return Inner.bUseInlining; }
		}

		public bool bWithServerCode
		{
			get { return Inner.bWithServerCode; }
		}

		public bool bCompileWithStatsWithoutEngine
		{
			get { return Inner.bCompileWithStatsWithoutEngine; }
		}

		public bool bCompileWithPluginSupport
		{
			get { return Inner.bCompileWithPluginSupport; }
		}

		public bool bIncludePluginsForTargetPlatforms
		{
			get { return Inner.bIncludePluginsForTargetPlatforms; }
		}

        public bool bWithPerfCounters
		{
			get { return Inner.bWithPerfCounters; }
		}

		public bool bWithLiveCoding
		{
			get { return Inner.bWithLiveCoding; }
		}

        public bool bUseLoggingInShipping
		{
			get { return Inner.bUseLoggingInShipping; }
		}

		public bool bLoggingToMemoryEnabled
		{
			get { return Inner.bLoggingToMemoryEnabled; }
		}

        public bool bUseLauncherChecks
		{
			get { return Inner.bUseLauncherChecks; }
		}

		public bool bUseChecksInShipping
		{
			get { return Inner.bUseChecksInShipping; }
		}

		public bool bCompileFreeType
		{
			get { return Inner.bCompileFreeType; }
		}

		public bool bCompileForSize
		{
			get { return Inner.bCompileForSize; }
		}

        public bool bForceCompileDevelopmentAutomationTests
		{
			get { return Inner.bForceCompileDevelopmentAutomationTests; }
		}

        public bool bForceCompilePerformanceAutomationTests
		{
			get { return Inner.bForceCompilePerformanceAutomationTests; }
		}

		public bool bUseXGEController
		{
			get { return Inner.bUseXGEController; }
		}

		public bool bEventDrivenLoader
		{
			get { return Inner.bEventDrivenLoader; }
		}

		public bool bUseBackwardsCompatibleDefaults
		{
			get { return Inner.bUseBackwardsCompatibleDefaults; }
		}

		public bool bIWYU
		{
			get { return Inner.bIWYU; }
		}

		public bool bEnforceIWYU
		{
			get { return Inner.bEnforceIWYU; }
		}

		public bool bHasExports
		{
			get { return Inner.bHasExports; }
		}

		public bool bPrecompile
		{
			get { return Inner.bPrecompile; }
		}

		public bool bEnableOSX109Support
		{
			get { return Inner.bEnableOSX109Support; }
		}

		public bool bIsBuildingConsoleApplication
		{
			get { return Inner.bIsBuildingConsoleApplication; }
		}

		public bool bDisableSymbolCache
		{
			get { return Inner.bDisableSymbolCache; }
		}

		public bool bUseUnityBuild
		{
			get { return Inner.bUseUnityBuild; }
		}

		public bool bForceUnityBuild
		{
			get { return Inner.bForceUnityBuild; }
		}

		public bool bAdaptiveUnityDisablesOptimizations
		{
			get { return Inner.bAdaptiveUnityDisablesOptimizations; }
		}

		public bool bAdaptiveUnityDisablesPCH
		{
			get { return Inner.bAdaptiveUnityDisablesPCH; }
		}

		public bool bAdaptiveUnityDisablesPCHForProject
		{
			get { return Inner.bAdaptiveUnityDisablesPCHForProject; }
		}

		public bool bAdaptiveUnityCreatesDedicatedPCH
		{
			get { return Inner.bAdaptiveUnityCreatesDedicatedPCH; }
		}

		public bool bAdaptiveUnityEnablesEditAndContinue
		{
			get { return Inner.bAdaptiveUnityEnablesEditAndContinue; }
		}

		public int MinGameModuleSourceFilesForUnityBuild
		{
			get { return Inner.MinGameModuleSourceFilesForUnityBuild; }
		}

		public bool bShadowVariableErrors
		{
			get { return Inner.bShadowVariableErrors; }
		}

		public bool bUndefinedIdentifierErrors
		{
			get { return Inner.bUndefinedIdentifierErrors; }
		}

		public bool bUseFastMonoCalls
		{
			get { return Inner.bUseFastMonoCalls; }
		}

		public bool bUseFastSemanticsRenderContexts
		{
			get { return Inner.bUseFastSemanticsRenderContexts; }
		}

		public int NumIncludedBytesPerUnityCPP
		{
			get { return Inner.NumIncludedBytesPerUnityCPP; }
		}

		public bool bStressTestUnity
		{
			get { return Inner.bStressTestUnity; }
		}

		public bool bDisableDebugInfo
		{
			get { return Inner.bDisableDebugInfo; }
		}

		public bool bDisableDebugInfoForGeneratedCode
		{
			get { return Inner.bDisableDebugInfoForGeneratedCode; }
		}

		public bool bOmitPCDebugInfoInDevelopment
		{
			get { return Inner.bOmitPCDebugInfoInDevelopment; }
		}

		public bool bUsePDBFiles
		{
			get { return Inner.bUsePDBFiles; }
		}

		public bool bUsePCHFiles
		{
			get { return Inner.bUsePCHFiles; }
		}

		public int MinFilesUsingPrecompiledHeader
		{
			get { return Inner.MinFilesUsingPrecompiledHeader; }
		}

		public bool bForcePrecompiledHeaderForGameModules
		{
			get { return Inner.bForcePrecompiledHeaderForGameModules; }
		}

		public bool bUseIncrementalLinking
		{
			get { return Inner.bUseIncrementalLinking; }
		}

		public bool bAllowLTCG
		{
			get { return Inner.bAllowLTCG; }
		}
        public bool bPGOProfile
        {
            get { return Inner.bPGOProfile; }
        }

        public bool bPGOOptimize
        {
            get { return Inner.bPGOOptimize; }
        }

        public bool bAllowASLRInShipping
		{
			get { return Inner.bAllowASLRInShipping; }
		}

		public bool bSupportEditAndContinue
		{
			get { return Inner.bSupportEditAndContinue; }
		}

		public bool bOmitFramePointers
		{
			get { return Inner.bOmitFramePointers; }
		}

		[Obsolete("bStripSymbolsOnIOS has been deprecated. Use IOSPlatform.bStripSymbols instead.")]
		public bool bStripSymbolsOnIOS
		{
			get { return IOSPlatform.bStripSymbols; }
		}

		public bool bUseMallocProfiler
		{
			get { return Inner.bUseMallocProfiler; }
		}

		public bool bUseSharedPCHs
		{
			get { return Inner.bUseSharedPCHs; }
		}

		public bool bUseShippingPhysXLibraries
		{
			get { return Inner.bUseShippingPhysXLibraries; }
		}

        public bool bUseCheckedPhysXLibraries
		{
			get { return Inner.bUseCheckedPhysXLibraries; }
		}

		public bool bCheckLicenseViolations
		{
			get { return Inner.bCheckLicenseViolations; }
		}

		public bool bBreakBuildOnLicenseViolation
		{
			get { return Inner.bBreakBuildOnLicenseViolation; }
		}

		public bool? bUseFastPDBLinking
		{
			get { return Inner.bUseFastPDBLinking; }
		}

		public bool bCreateMapFile
		{
			get { return Inner.bCreateMapFile; }
		}

		public string BundleVersion
		{
			get { return Inner.BundleVersion; }
		}

		public bool bDeployAfterCompile
		{
			get { return Inner.bDeployAfterCompile; }
		}

		[Obsolete("bCreateStubIPA has been deprecated. Use IOSPlatform.bCreateStubIPA instead.")]
		public bool bCreateStubIPA
		{
			get { return IOSPlatform.bCreateStubIPA; }
		}

		public bool bAllowRemotelyCompiledPCHs
		{
			get { return Inner.bAllowRemotelyCompiledPCHs; }
		}

		public bool bCheckSystemHeadersForModification
		{
			get { return Inner.bCheckSystemHeadersForModification; }
		}

		public bool bDisableLinking
		{
			get { return Inner.bDisableLinking; }
		}

		public bool bFormalBuild
		{
			get { return Inner.bFormalBuild; }
		}

		public bool bUseAdaptiveUnityBuild
		{
			get { return Inner.bUseAdaptiveUnityBuild; }
		}

		public bool bFlushBuildDirOnRemoteMac
		{
			get { return Inner.bFlushBuildDirOnRemoteMac; }
		}

		public bool bPrintToolChainTimingInfo
		{
			get { return Inner.bPrintToolChainTimingInfo; }
		}

		public bool bHideSymbolsByDefault
		{
			get { return Inner.bHideSymbolsByDefault; }
		}

		public string ToolChainName
		{
			get { return Inner.ToolChainName; }
		}

		public bool bLegacyPublicIncludePaths
		{
			get { return Inner.bLegacyPublicIncludePaths; }
		}

		public CppStandardVersion CppStandard
		{
			get { return Inner.CppStandard; }
		}

		internal bool bNoManifestChanges
		{
			get { return Inner.bNoManifestChanges; }
		}

		public string BuildVersion
		{
			get { return Inner.BuildVersion; }
		}

		public TargetLinkType LinkType
		{
			get { return Inner.LinkType; }
		}

		public IReadOnlyList<string> GlobalDefinitions
		{
			get { return Inner.GlobalDefinitions.AsReadOnly(); }
		}

		public IReadOnlyList<string> ProjectDefinitions
		{
			get { return Inner.ProjectDefinitions.AsReadOnly(); }
		}

		public string LaunchModuleName
		{
			get { return Inner.LaunchModuleName; }
		}

		public IReadOnlyList<string> ExtraModuleNames
		{
			get { return Inner.ExtraModuleNames.AsReadOnly(); }
		}

		public IReadOnlyList<FileReference> ManifestFileNames
		{
			get { return Inner.ManifestFileNames.AsReadOnly(); }
		}

		public IReadOnlyList<FileReference> DependencyListFileNames
		{
			get { return Inner.DependencyListFileNames.AsReadOnly(); }
		}

		public TargetBuildEnvironment BuildEnvironment
		{
			get { return Inner.BuildEnvironment; }
		}

		public IReadOnlyList<string> PreBuildSteps
		{
			get { return Inner.PreBuildSteps; }
		}

		public IReadOnlyList<string> PostBuildSteps
		{
			get { return Inner.PostBuildSteps; }
		}

		public IReadOnlyList<string> AdditionalBuildProducts
		{
			get { return Inner.AdditionalBuildProducts; }
		}

		public string AdditionalCompilerArguments
		{
			get { return Inner.AdditionalCompilerArguments; }
		}

		public string AdditionalLinkerArguments
		{
			get { return Inner.AdditionalLinkerArguments; }
		}

		public string GeneratedProjectName
		{
			get { return Inner.GeneratedProjectName; }
		}

		public ReadOnlyAndroidTargetRules AndroidPlatform
		{
			get;
			private set;
		}

		public ReadOnlyLuminTargetRules LuminPlatform
		{
			get;
			private set;
		}

		public ReadOnlyLinuxTargetRules LinuxPlatform
		{
			get;
			private set;
		}

		public ReadOnlyHTML5TargetRules HTML5Platform
		{
			get;
			private set;
		}

		public ReadOnlyIOSTargetRules IOSPlatform
		{
			get;
			private set;
		}

		public ReadOnlyMacTargetRules MacPlatform
		{
			get;
			private set;
		}

		public ReadOnlyPS4TargetRules PS4Platform
		{
			get;
			private set;
		}

		public ReadOnlySwitchTargetRules SwitchPlatform
		{
			get;
			private set;
		}

		public ReadOnlyWindowsTargetRules WindowsPlatform
		{
			get;
			private set;
		}

		public ReadOnlyXboxOneTargetRules XboxOnePlatform
		{
			get;
			private set;
		}

		public bool bShouldCompileAsDLL
		{
			get { return Inner.bShouldCompileAsDLL; }
		}

		public bool bGenerateProjectFiles
		{
			get { return Inner.bGenerateProjectFiles; }
		}

		public bool bIsEngineInstalled
		{
			get { return Inner.bIsEngineInstalled; }
		}

		#if !__MonoCS__
		#pragma warning restore C1591
		#endif
		#endregion

		/// <summary>
		/// Provide access to the RelativeEnginePath property for code referencing ModuleRules.BuildConfiguration.
		/// </summary>
		public string RelativeEnginePath
		{
			get { return UnrealBuildTool.EngineDirectory.MakeRelativeTo(DirectoryReference.GetCurrentDirectory()); }
		}

		/// <summary>
		/// Provide access to the UEThirdPartySourceDirectory property for code referencing ModuleRules.UEBuildConfiguration.
		/// </summary>
		public string UEThirdPartySourceDirectory
		{
			get { return "ThirdParty/"; }
		}

		/// <summary>
		/// Provide access to the UEThirdPartyBinariesDirectory property for code referencing ModuleRules.UEBuildConfiguration.
		/// </summary>
		public string UEThirdPartyBinariesDirectory
		{
			get { return "../Binaries/ThirdParty/"; }
		}

		/// <summary>
		/// Checks if current platform is part of a given platform group
		/// </summary>
		/// <param name="Group">The platform group to check</param>
		/// <returns>True if current platform is part of a platform group</returns>
		public bool IsInPlatformGroup(UnrealPlatformGroup Group)
		{
			return UEBuildPlatform.IsPlatformInGroup(Platform, Group);
		}
	}
}
