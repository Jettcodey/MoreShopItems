using System;
using System.Linq;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MoreShopItems
{
	public class MoreShopItemsSplash : MonoBehaviour
	{
		public static bool BlockSplashSkip = false;
		private Harmony _patcher;

		// Incompatible plugin GUIDs
		private static readonly string[] IncompatibleGuids = new[]
		{
			"Traktool.SharedUpgrades",
			"Empress.SharedUpgradesCompat",
			"Omniscye.SharedUpgradesHelper",
			//"Dj_Haski.AdvanceMyShop",	// Not sure if it'll conflict, but likely - added for later if needed.
		};

		private bool incompatibilityDetected;
		private bool overlayShown;
		private GameObject overlayRoot;

		private float previousTimeScale = 1f;
		private bool previousCursorVisible = false;
		private CursorLockMode previousCursorLockState = CursorLockMode.Locked;

		private void Awake()
		{
			DontDestroyOnLoad(gameObject);
		}

		private void Start()
		{
			incompatibilityDetected = IncompatibleGuids.Any(g => Chainloader.PluginInfos.ContainsKey(g));
			if (!incompatibilityDetected)
			{
				Destroy(this);
				return;
			}

			Plugin.Logger.LogInfo($"Incompatible plugin(s) detected: {string.Join(", ", IncompatibleGuids.Where(g => Chainloader.PluginInfos.ContainsKey(g)))}.");
			_patcher = new Harmony("MoreShopItems.SplashBlock");
			_patcher.PatchAll();
			BlockSplashSkip = true;
		}

		private void Update()
		{
			if (!incompatibilityDetected || overlayShown)
				return;

			try
			{
				var ui = SplashScreenUI.instance;
				if (ui != null && ui.warningTransform != null && ui.warningTransform.gameObject.activeInHierarchy)
				{
					ShowOverlay();
				}
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogError("MoreShopItemsSplash Update error: " + ex);
				Destroy(this);
			}
		}

		private void ShowOverlay()
		{
			BlockSplashSkip = true;
			overlayShown = true;

			// Save previous states
			previousTimeScale = Time.timeScale;
			previousCursorVisible = Cursor.visible;
			previousCursorLockState = Cursor.lockState;

			// Pause the game and show cursor
			Time.timeScale = 0f;
			Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;

			// Build UI root
			overlayRoot = new GameObject("MoreShopItems_IncompatibilityOverlay", typeof(RectTransform));
			DontDestroyOnLoad(overlayRoot);

			var canvas = overlayRoot.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = 10000;

			var scaler = overlayRoot.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920, 1080);
			scaler.matchWidthOrHeight = 0.5f;

			overlayRoot.AddComponent<GraphicRaycaster>();

			// Background dim
			var bg = CreateUIObject("Background", overlayRoot.transform);
			var bgImg = bg.AddComponent<Image>();
			bgImg.color = new Color(0f, 0f, 0f, 0.85f);
			var bgRt = bg.GetComponent<RectTransform>();
			bgRt.anchorMin = Vector2.zero;
			bgRt.anchorMax = Vector2.one;
			bgRt.offsetMin = Vector2.zero;
			bgRt.offsetMax = Vector2.zero;

			// Panel container
			var panel = CreateUIObject("Panel", overlayRoot.transform);
			var panelImg = panel.AddComponent<Image>();
			panelImg.color = new Color(0.51f, 0.10f, 0.10f, 0.95f);

			var panelRt = panel.GetComponent<RectTransform>();
			panelRt.anchorMin = new Vector2(0.1f, 0.1f);
			panelRt.anchorMax = new Vector2(0.9f, 0.85f);
			panelRt.pivot = new Vector2(0.5f, 0.5f);
			panelRt.anchoredPosition = Vector2.zero;
			panelRt.sizeDelta = new Vector2(0f, 0f);
			panelRt.localScale = Vector3.one;

			// Title area
			var title = CreateText("Title", 38, FontStyle.Bold, panel.transform);
			var titleRt = title.GetComponent<RectTransform>();
			titleRt.anchorMin = new Vector2(0.05f, 0.86f);
			titleRt.anchorMax = new Vector2(0.95f, 0.97f);
			titleRt.offsetMin = Vector2.zero;
			titleRt.offsetMax = Vector2.zero;
			title.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
			title.GetComponent<Text>().text = 
				"MoreShopItems - Mod Conflict Detected!\n" +
				"PLEASE READ CAREFULLY";

			// Message area
			var msg = CreateText("Message", 28, FontStyle.Normal, panel.transform);
			var msgRt = msg.GetComponent<RectTransform>();
			msgRt.anchorMin = new Vector2(0.05f, 0.38f);
			msgRt.anchorMax = new Vector2(0.95f, 0.86f);
			msgRt.offsetMin = Vector2.zero;
			msgRt.offsetMax = Vector2.zero;
			var msgText = msg.GetComponent<Text>();
			msgText.alignment = TextAnchor.UpperCenter;
			msgText.horizontalOverflow = HorizontalWrapMode.Wrap;
			msgText.verticalOverflow = VerticalWrapMode.Overflow;
			msgText.text =
				$"Detected installed mod(s): {string.Join(", ", IncompatibleGuids.Where(g => Chainloader.PluginInfos.ContainsKey(g)))}\n\n" +
				"Using these mods together with MoreShopItems CAN and WILL cause serious issues, including:\n" +
				"- Getting stuck on the loading screen\n" +
				"- Player upgrades not applying correctly or disappearing\n" +
				"- Unpredictable Game behavior and Game instability\n" +
				"- Corrupted Save files and Game crashes\n\n" +
				"You MUST disable / uninstall the listed mods above to use MoreShopItems without issues!\n" +
				"Compatible and recommended mods to use with MoreShopItems instead:\n" +
				"- Empress SharedUpgrades by Omniscye\n\n" +
				"Choose how you want to proceed:";

			// Buttons container anchored
			var buttons = CreateUIObject("Buttons", panel.transform);
			var buttonsRt = buttons.GetComponent<RectTransform>();
			buttonsRt.anchorMin = new Vector2(0.05f, 0.08f);
			buttonsRt.anchorMax = new Vector2(0.95f, 0.26f);
			buttonsRt.offsetMin = Vector2.zero;
			buttonsRt.offsetMax = Vector2.zero;
			buttonsRt.localScale = Vector3.one;

			// Create buttons
			var disableBtn = CreateButton("DisableThisPlugin", buttons.transform);
			var continueBtn = CreateButton("ContinueAnyway", buttons.transform);
			var quitBtn = CreateButton("QuitGame", buttons.transform);

			// Arrange buttons
			var disableRt = disableBtn.GetComponent<RectTransform>();
			var continueRt = continueBtn.GetComponent<RectTransform>();
			var quitRt = quitBtn.GetComponent<RectTransform>();

			// Anchor each button inside buttons container
			disableRt.anchorMin = new Vector2(0.02f, 0.1f);
			disableRt.anchorMax = new Vector2(0.32f, 0.9f);
			disableRt.offsetMin = Vector2.zero;
			disableRt.offsetMax = Vector2.zero;

			continueRt.anchorMin = new Vector2(0.34f, 0.1f);
			continueRt.anchorMax = new Vector2(0.66f, 0.9f);
			continueRt.offsetMin = Vector2.zero;
			continueRt.offsetMax = Vector2.zero;

			quitRt.anchorMin = new Vector2(0.68f, 0.1f);
			quitRt.anchorMax = new Vector2(0.98f, 0.9f);
			quitRt.offsetMin = Vector2.zero;
			quitRt.offsetMax = Vector2.zero;

			// Set texts
			disableBtn.GetComponentInChildren<Text>().text = "Disable MoreShopItems";
			continueBtn.GetComponentInChildren<Text>().text = "Continue anyway";
			quitBtn.GetComponentInChildren<Text>().text = "Quit Game";

			// Button callbacks
			disableBtn.onClick.AddListener(() =>
			{
				Plugin.Logger.LogWarning("User chose to disable MoreShopItems due to incompatibility.");
				TryUnpatchAndDisablePlugin();
				CloseOverlay();
			});

			continueBtn.onClick.AddListener(() =>
			{
				Plugin.Logger.LogWarning("User chose to continue with potential incompatibility.");
				CloseOverlay();
			});

			quitBtn.onClick.AddListener(() =>
			{
				Plugin.Logger.LogWarning("User chose to quit because of incompatibility.");
				CloseOverlay();
#if !UNITY_EDITOR
				Application.Quit();
#else
				UnityEditor.EditorApplication.isPlaying = false;
#endif
			});
		}

		private void TryUnpatchAndDisablePlugin()
		{
			try
			{
				Harmony.UnpatchID("MoreShopItems");
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogError("Error while unpatching Harmony patches: " + ex);
			}

			try
			{
				if (Plugin.Instance != null)
				{
					Destroy(Plugin.Instance);
					Plugin.Logger.LogInfo("Plugin component destroyed.");
				}
				else
				{
					Plugin.Logger.LogWarning("Plugin instance was null when attempting to disable.");
				}
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogError("Error while destroying plugin instance: " + ex);
			}
		}

		private void CloseOverlay()
		{
			BlockSplashSkip = false;

			if (overlayRoot != null)
			{
				Destroy(overlayRoot);
				overlayRoot = null;
			}

			// Restore time scale and cursor state
			Time.timeScale = previousTimeScale;
			Cursor.visible = previousCursorVisible;
			Cursor.lockState = previousCursorLockState;

			Destroy(this);
		}

		// UI helper
		private GameObject CreateUIObject(string name, Transform parent)
		{
			var go = new GameObject(name, typeof(RectTransform));
			go.transform.SetParent(parent, false);
			var rt = go.GetComponent<RectTransform>();
			rt.localScale = Vector3.one;
			return go;
		}

		// Text stuff
		private GameObject CreateText(string name, int fontSize, FontStyle style, Transform parent)
		{
			var go = CreateUIObject(name, parent);
			var text = go.AddComponent<Text>();
			text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			text.fontSize = fontSize;
			text.fontStyle = style;
			text.color = Color.white;
			text.alignment = TextAnchor.MiddleCenter;
			var rt = go.GetComponent<RectTransform>();
			rt.sizeDelta = new Vector2(600f, 40f);
			return go;
		}

		// Button stuff
		private Button CreateButton(string name, Transform parent)
		{
			var go = CreateUIObject(name, parent);
			var image = go.AddComponent<Image>();
			image.color = new Color(0.22f, 0.22f, 0.22f, 0.95f);

			var btn = go.AddComponent<Button>();
			var rt = go.GetComponent<RectTransform>();
			rt.sizeDelta = new Vector2(200f, 56f);

			// Button label
			var label = CreateText("Label", 24, FontStyle.Normal, go.transform);
			var labelRt = label.GetComponent<RectTransform>();
			labelRt.anchorMin = new Vector2(0f, 0f);
			labelRt.anchorMax = new Vector2(1f, 1f);
			labelRt.offsetMin = Vector2.zero;
			labelRt.offsetMax = Vector2.zero;

			return btn;
		}

		private void OnDestroy()
		{
			if (Time.timeScale == 0f)
				Time.timeScale = 1f;

			Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;
		}

		[HarmonyPatch(typeof(SplashScreen), "SkipLogic")]
		private static class SplashScreen_SkipLogic_Patch
		{
			// Prefix returning false prevents the original SkipLogic from running
			private static bool Prefix()
			{
				// when true, we block SkipLogic so the splash is not skipped
				return !MoreShopItemsSplash.BlockSplashSkip;
			}
		}

		private void LateUpdate()
		{
			// "ESC" Key acts as "Continue anyway"
			if (overlayShown && overlayRoot != null && Input.GetKeyDown(KeyCode.Escape))
			{
				Plugin.Logger.LogInfo("Escape pressed (continue anyway) - closing MoreShopItems incompatibility overlay.");
				CloseOverlay();
			}
		}
	}
}
