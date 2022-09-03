using HarmonyLib;
using UnityEngine.UI;

namespace NPCFinder.Utils;

[HarmonyPatch(typeof(Menu), nameof(Menu.Start))]
internal static class MenuStartPatch
{
    static void Postfix(Menu __instance)
    {
        NpcFinderPlugin.Dialog = UnityEngine.Object.Instantiate(Menu.instance.m_quitDialog.gameObject,
            Hud.instance.m_rootObject.transform.parent.parent, true);
        NpcFinderPlugin.Dialog.name = "NpcFinderDialog";
        Button.ButtonClickedEvent noClicked = new();
        noClicked.AddListener(OnFindNPCsOff);
        NpcFinderPlugin.Dialog.transform.Find("dialog/Button_no").GetComponent<Button>().onClick = noClicked;
        Button.ButtonClickedEvent yesClicked = new();
        yesClicked.AddListener(OnFindNPCs);
        NpcFinderPlugin.Dialog.transform.Find("dialog/Button_yes").GetComponent<Button>().onClick = yesClicked;
    }

    private static void OnFindNPCsOff()
    {
        NpcFinderPlugin.Dialog!.SetActive(false);
        NpcFinderPlugin.SShowNpcesp = false;
    }

    private static void OnFindNPCs()
    {
        NpcFinderPlugin.Dialog!.SetActive(false);
        NpcFinderPlugin.SShowNpcesp = true;
        
    }
}

[HarmonyPatch(typeof(Menu), nameof(Menu.IsVisible))]
internal static class MenuIsVisiblePatch
{
    static void Postfix(Menu __instance, ref bool __result)
    {
        if (!NpcFinderPlugin.Dialog || NpcFinderPlugin.Dialog?.activeSelf != true) return;
        NpcFinderPlugin.Dialog!.transform.Find("dialog/Exit").GetComponent<Text>().text = $"Show NPCs?";
        __result = true;
    }
}