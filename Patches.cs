﻿using System;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShipStats;

[HarmonyPatch(typeof(Hud), nameof(Hud.Awake))]
static class HudAwakePatch
{
    // Create GameObject
    public static GameObject Go = null!;
    public static GameObject Go2 = null!;
    public static TextMeshProUGUI contentText = null!;
    public static TextMeshProUGUI contentText2 = null!;

    static void Postfix(Hud __instance)
    {
        ShipStatsPlugin.ShipStatsLogger.LogDebugIfBuildDebug("Hud.Awake Postfix started");

        Go = new GameObject("AzuShipStatsPlayerControlled");
        Go.transform.SetParent(Utils.FindChild(__instance.m_shipHudRoot.transform, "Controls"), false);

        // Clone the go and add it to Hud.instance.m_rootObject
        Go2 = UnityEngine.Object.Instantiate(Go, Hud.instance.m_rootObject.transform, false);
        Go2.name = "AzuShipStatsPlayerOnBoard";
        AddTheComponents(Go);
        AddTheComponents(Go2);

        ShipStatsPlugin.ShipStatsLogger.LogDebugIfBuildDebug("UI elements created and components added");
    }

    public static void AddTheComponents(GameObject gameObject)
    {
        ShipStatsPlugin.ShipStatsLogger.LogDebugIfBuildDebug($"Adding components to {gameObject.name}");

        RectTransform rect = gameObject.AddComponent<RectTransform>();
        Image image = gameObject.AddComponent<Image>();
        image.color = ShipStatsPlugin.PanelColor.Value;

        VerticalLayoutGroup layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(5, 5, 5, 5);
        ContentSizeFitter contentFitter = gameObject.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject textObj = new($"{gameObject.name}Content");
        textObj.transform.SetParent(layout.transform);

        if (gameObject.name.EndsWith("PlayerControlled", StringComparison.Ordinal))
        {
            contentText = textObj.AddComponent<TextMeshProUGUI>();
            contentText.color = ShipStatsPlugin.TextColor.Value;
            contentText.font = MessageHud.instance.m_messageCenterText.font;
            contentText.overflowMode = TextOverflowModes.Overflow;
            contentText.fontSize = ShipStatsPlugin.FontSize.Value;
            contentText.enabled = true;
            contentText.alignment = TextAlignmentOptions.Center;
        }
        else
        {
            contentText2 = textObj.AddComponent<TextMeshProUGUI>();
            contentText2.color = ShipStatsPlugin.TextColor.Value;
            contentText2.font = MessageHud.instance.m_messageCenterText.font;
            contentText2.overflowMode = TextOverflowModes.Overflow;
            contentText2.fontSize = ShipStatsPlugin.FontSize.Value;
            contentText2.enabled = true;
            contentText2.alignment = TextAlignmentOptions.Center;
        }

        gameObject.AddComponent<UIUpdater>();
        rect.anchoredPosition = ShipStatsPlugin.AnchoredPosition.Value;
        gameObject.SetActive(false);

        ShipStatsPlugin.ShipStatsLogger.LogDebugIfBuildDebug($"Components added to {gameObject.name}");
    }
}

[HarmonyPatch(typeof(Ship), nameof(Ship.OnTriggerEnter))]
static class ShipOnTriggerEnterPatch
{
    static void Postfix(Ship __instance, Collider collider)
    {
        ShipStatsPlugin.ShipStatsLogger.LogDebugIfBuildDebug("Ship.OnTriggerEnter started");

        Player component = collider.GetComponent<Player>();
        if (!component)
        {
            ShipStatsPlugin.ShipStatsLogger.LogDebugIfBuildDebug("No player component found");
            return;
        }

        if (!(component == Player.m_localPlayer))
        {
            ShipStatsPlugin.ShipStatsLogger.LogDebugIfBuildDebug("Player is not local player");
            return;
        }

        HudAwakePatch.Go.SetActive(true);
        ShipStatsPlugin.ShipStatsLogger.LogDebugIfBuildDebug("AzuShipStatsPlayerControlled UI set active");

        // Uncomment to debug Go2 activation
        //HudAwakePatch.Go2.SetActive(true);
        //ShipStatsPlugin.ShipStatsLogger.LogWarning("AzuShipStatsPlayerOnBoard UI set active");
    }
}

[HarmonyPatch(typeof(Ship))]
static class ShipOnTriggerExitDestroyedPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(Ship.OnTriggerExit))]
    static void Postfix1(Ship __instance, Collider collider)
    {
        ShipStatsPlugin.ShipStatsLogger.LogDebugIfBuildDebug("Ship.OnTriggerExit started");

        Player component = collider.GetComponent<Player>();
        if (!component)
        {
            ShipStatsPlugin.ShipStatsLogger.LogDebugIfBuildDebug("No player component found");
            return;
        }

        if (!(component == Player.m_localPlayer))
        {
            ShipStatsPlugin.ShipStatsLogger.LogDebugIfBuildDebug("Player is not local player");
            return;
        }

        HudAwakePatch.Go.SetActive(false);
        HudAwakePatch.Go2.SetActive(false);
        ShipStatsPlugin.ShipStatsLogger.LogDebugIfBuildDebug("UI elements set inactive");
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Ship.OnDestroyed))]
    static void Postfix2()
    {
        HudAwakePatch.Go.SetActive(false);
        HudAwakePatch.Go2.SetActive(false);
        ShipStatsPlugin.ShipStatsLogger.LogDebugIfBuildDebug("UI elements set inactive on ship destruction");
    }
}

public class UIUpdater : MonoBehaviour
{
    internal Vector3 size;
    internal Vector2 offset;
    internal Transform rectransform = null!;
    const float knotsMultiplier = 1.94384f;
    const float mphMultiplier = 2.237f;

    private void Start()
    {
        rectransform = GetComponent<RectTransform>();
        //rectransform.localScale = size;
    }

    private void Update()
    {
        if (Player.m_localPlayer == null)
        {
            HudAwakePatch.contentText.text = "";
            HudAwakePatch.contentText2.text = "";
            return;
        }

        Ship? controlledShip = Player.m_localPlayer.GetControlledShip() != null
            ? Player.m_localPlayer.GetControlledShip()
            : Player.m_localPlayer.GetStandingOnShip() != null
                ? Player.m_localPlayer.GetStandingOnShip()
                : null;
        if (controlledShip == null)
        {
            HudAwakePatch.contentText.text = "";
            HudAwakePatch.contentText2.text = "";
            return;
        }

        /*if (Player.m_localPlayer.IsAttachedToShip())
        {
            HudAwakePatch.Go2.SetActive(false);
        }*/

        float windSpeed = EnvMan.instance.GetWindIntensity() * knotsMultiplier * 10f;
        float shipSpeed = Mathf.Abs(controlledShip.GetSpeed());
        float speedMph = Mathf.Abs(shipSpeed * mphMultiplier); // convert m/s to mph
        float speedKnots = Mathf.Abs(shipSpeed * knotsMultiplier); // convert m/s to knots
        float shipHealth = 0.0f;
        float shipDefaultHealth = 0.0f;
        controlledShip.TryGetComponent(out WearNTear wearNTear);
        if (wearNTear)
        {
            if (controlledShip.m_nview.IsValid() && !(controlledShip.m_nview.GetZDO().GetFloat("health", wearNTear.m_health) <= 0.0))
            {
                shipDefaultHealth = wearNTear.m_health;
                shipHealth = controlledShip.m_nview.GetZDO().GetFloat("health", wearNTear.m_health);
            }
        }

        // Get Wind Direction
        string windDirectionString;
        Vector3 windDir = EnvMan.instance.GetWindDir();
        float angle = Mathf.Atan2(windDir.x, windDir.z) * Mathf.Rad2Deg + 180;
        // Use the angle to determine the direction.
        if (angle >= 337.5 || angle < 22.5) windDirectionString = "N";
        else if (angle < 67.5) windDirectionString = "NE";
        else if (angle < 112.5) windDirectionString = "E";
        else if (angle < 157.5) windDirectionString = "SE";
        else if (angle < 202.5) windDirectionString = "S";
        else if (angle < 247.5) windDirectionString = "SW";
        else if (angle < 292.5) windDirectionString = "W";
        else windDirectionString = "NW";
        Inventory? mInventory = controlledShip.GetComponentInChildren<Container>()?.m_inventory;
        string text;
        try
        {
            text = string.Format(ShipStatsPlugin.TextFormat.Value, speedKnots, speedMph, windSpeed, angle,
                windDirectionString,
                (mInventory == null
                    ? ""
                    : string.Format("Ship Inventory: {0}/{1} ({2:0.#}%)", mInventory?.m_inventory.Count,
                        mInventory?.m_width * mInventory?.m_height, mInventory?.SlotsUsedPercentage())),
                shipHealth > 0 ? string.Format("Ship Health: {0:0}/{1:0}", shipHealth, shipDefaultHealth) : "");
        }
        catch
        {
            text = "Error in Text Format";
        }

        HudAwakePatch.contentText.text = text;
        HudAwakePatch.contentText2.text = text;
        Camera mainCamera = Utils.GetMainCamera();
        if (mainCamera == null)
            return;
        HudAwakePatch.Go2.transform.position = mainCamera.WorldToScreenPoint(controlledShip.m_controlGuiPos.position);
    }
}