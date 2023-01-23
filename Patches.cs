using System;
using System.Text;
using HarmonyLib;
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
    public static Text contentText = null!;
    public static Text contentText2 = null!;

    static void Postfix(Hud __instance)
    {
        // Create new UI object and add it to the HUD
        Go = new GameObject("AzuShipStatsPlayerControlled");
        Go.transform.SetParent(Utils.FindChild(__instance.m_shipHudRoot.transform, "Controls"), false);

        // Clone the go and add it to Hud.instance.m_rootObject
        Go2 = UnityEngine.Object.Instantiate(Go, Hud.instance.m_rootObject.transform, false);
        Go2.name = "AzuShipStatsPlayerOnBoard";
        AddTheComponents(Go);
        AddTheComponents(Go2);
    }

    public static void AddTheComponents(GameObject gameObject)
    {
        // Add RectTransform component
        RectTransform? rect = gameObject.AddComponent<RectTransform>();
        // Add image component and allow color change
        Image? image = gameObject.AddComponent<Image>();
        image.color = ShipStatsPlugin.PanelColor.Value;

        // Add vertical layout group component
        VerticalLayoutGroup? layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(5, 5, 5, 5);
        ContentSizeFitter contentFitter = gameObject.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject textObj = new($"{gameObject.name}Content");
        textObj.transform.SetParent(layout.transform);
        if (gameObject.name.EndsWith("PlayerControlled", StringComparison.Ordinal))
        {
            contentText = textObj.AddComponent<Text>();
            contentText.color = ShipStatsPlugin.TextColor.Value;
            contentText.font = MessageHud.instance.m_messageCenterText.font;
            contentText.horizontalOverflow = HorizontalWrapMode.Overflow;
            contentText.fontSize = ShipStatsPlugin.FontSize.Value;
            contentText.enabled = true;
            contentText.alignment = TextAnchor.MiddleCenter;
        }
        else
        {
            contentText2 = textObj.AddComponent<Text>();
            contentText2.color = ShipStatsPlugin.TextColor.Value;
            contentText2.font = MessageHud.instance.m_messageCenterText.font;
            contentText2.horizontalOverflow = HorizontalWrapMode.Overflow;
            contentText2.fontSize = ShipStatsPlugin.FontSize.Value;
            contentText2.enabled = true;
            contentText2.alignment = TextAnchor.MiddleCenter;
        }

        Outline textOutline = textObj.AddComponent<Outline>();
        textOutline.effectColor = Color.black;
        textOutline.effectDistance = new Vector2(1, -1);
        textOutline.useGraphicAlpha = true;
        gameObject.AddComponent<UIUpdater>();
        // Move go -27 lower and 200 to the right
        rect.anchoredPosition = ShipStatsPlugin.AnchoredPosition.Value;
        gameObject.SetActive(false);
    }
}

[HarmonyPatch(typeof(Ship), nameof(Ship.OnTriggerEnter))]
static class ShipOnTriggerEnterPatch
{
    static void Postfix(Ship __instance, Collider collider)
    {
        Player component = collider.GetComponent<Player>();
        if (!component)
            return;
        if (!(component == Player.m_localPlayer))
            return;
        HudAwakePatch.Go.SetActive(true);
        //HudAwakePatch.Go2.SetActive(true);
    }
}

[HarmonyPatch(typeof(Ship))]
static class ShipOnTriggerExitDestroyedPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(Ship.OnTriggerExit))]
    static void Postfix1(Ship __instance, Collider collider)
    {
        Player component = collider.GetComponent<Player>();
        if (!component)
            return;
        if (!(component == Player.m_localPlayer))
            return;
        HudAwakePatch.Go.SetActive(false);
        HudAwakePatch.Go2.SetActive(false);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Ship.OnDestroyed))]
    static void Postfix2()
    {
        HudAwakePatch.Go.SetActive(false);
        HudAwakePatch.Go2.SetActive(false);
    }
}

public class UIUpdater : MonoBehaviour
{
    internal Vector3 size;
    internal Vector2 offset;
    internal Transform rectransform;
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
        string text = string.Format(
            ShipStatsPlugin.TextFormat.Value,
            speedKnots, speedMph, windSpeed, angle, windDirectionString, (mInventory == null ? "" : string.Format("Ship Inventory: {0}/{1} ({2:0.#}%)\n", mInventory?.m_inventory.Count, mInventory?.m_width * mInventory?.m_height, mInventory?.SlotsUsedPercentage())));

        HudAwakePatch.contentText.text = text;
        HudAwakePatch.contentText2.text = text;
        Camera mainCamera = Utils.GetMainCamera();
        if (mainCamera == null)
            return;
        HudAwakePatch.Go2.transform.position = mainCamera.WorldToScreenPoint(controlledShip.m_controlGuiPos.position);
    }
}