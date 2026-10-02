using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using FrooxEngine.UIX;

#if DEBUG
using ResoniteHotReloadLib;
#endif

namespace DashTabTweaks;

public class DashTabTweaks : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.0"; //Changing the version here updates it in all locations needed
	public override string Name => "DashTabTweaks";
	public override string Author => "Noble";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/noblereign/ResoniteDashTabTweaks/";

	const string harmonyId = "dog.glacier.DashTabTweaks";

	public static ModConfiguration? Config;

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> ScrollableTabs = new("Scrollable Tabs", "Makes the tab list below the dash scrollable.", () => true);

	[AutoRegisterConfigKey]
	public static readonly ModConfigurationKey<bool> EnsureHome = new("Ensure Home is Default", "Makes sure the Home tab will always be the one open on startup.", () => true);

	private static ScrollRect? scroller;
	private static ContentSizeFitter? sizeFitter;

	public override void OnEngineInit() {
#if DEBUG
		HotReloader.RegisterForHotReload(this);
#endif

		Config = GetConfiguration()!;
		Config!.Save(true);
		Config.OnThisConfigurationChanged += OnConfigurationChanged;

		// Call setup method
		Setup();
	}

	static void Setup() {
		// Patch Harmony
		Harmony harmony = new Harmony(harmonyId);
		harmony.PatchAll();
	}

#if DEBUG
	// This is the method that should be used to unload your mod
	static void BeforeHotReload() {
		// Unpatch Harmony
		Harmony harmony = new Harmony(harmonyId);
		harmony.UnpatchAll(harmonyId);
	}

	// This is called in the newly loaded assembly
	static void OnHotReload(ResoniteMod modInstance) {
		// Get the config if needed
		Config = modInstance.GetConfiguration()!;
		Config!.Save(true);

		// Call setup method
		Setup();
	}
#endif

	private void OnConfigurationChanged(ConfigurationChangedEvent @event) {
		if (@event.Key == ScrollableTabs) {
			bool isEnabled = Config!.GetValue(ScrollableTabs);
			scroller?.Enabled = isEnabled;
			sizeFitter?.Enabled = isEnabled;
			foreach ((RadiantDashScreen screen, RadiantDashButton button) in Userspace.UserspaceWorld.GetRadiantDash()._dash.Target._screenButtons) {
				button._button.Target.RequireLockInToPress.Value = isEnabled;
			}
		}
	}
	private static void EnsureScroller(RadiantDash dash) {
		scroller = dash._buttonsUIroot.Target.GetComponentOrAttach<ScrollRect>();
		sizeFitter = dash._buttonsUIroot.Target.GetComponentOrAttach<ContentSizeFitter>();
		sizeFitter.HorizontalFit.Value = SizeFit.MinSize;
		scroller.Enabled = Config!.GetValue(ScrollableTabs);
		sizeFitter.Enabled = Config!.GetValue(ScrollableTabs);
		scroller!.MoveToRight();
	}

	[HarmonyPatch(typeof(RadiantDash), "OnAttach")]
	class RadiantDash_OnAttach_Patch {
		static void Postfix(RadiantDash __instance) {
			if (__instance.World == Userspace.UserspaceWorld) {
				EnsureScroller(__instance);
			}
		}
	}

	[HarmonyPatch(typeof(RadiantDashScreen), "OnAwake")]
	class RadiantDashScreen_OnAwake_Patch {
		static void Postfix(RadiantDashScreen __instance) {
			if (__instance.World == Userspace.UserspaceWorld) {
				if (Config!.GetValue(EnsureHome)) {
					__instance.RunInUpdates(1, () => {
						if (__instance == null || __instance.IsRemoved || __instance.IsDisposed) return;

						RadiantDash? dash = Userspace.UserspaceWorld.GetRadiantDash()?._dash?.Target;
						if (dash != null) {
							if (__instance is GridContainerScreen asGridContainerScreen && asGridContainerScreen.HasPreset(typeof(HomeScreenInitializer))) {
								Msg("Found home screen, ensuring");
								dash.CurrentScreen.Target = __instance;
								EnsureScroller(dash);
							}
						}
					});
				}
			}
		}
	}

	// patch the buttons to not press every time you scroll
	[HarmonyPatch(typeof(RadiantDashButton), "Setup")]
	class RadiantDashButton_Setup_Patch {
		static void Postfix(RadiantDashButton __instance) {
			if (__instance.World == Userspace.UserspaceWorld) {
				__instance._button.Target.RequireLockInToPress.Value = Config!.GetValue(ScrollableTabs);
			}
		}
	}
}
