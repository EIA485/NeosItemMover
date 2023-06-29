using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using FrooxEngine.UIX;
using BaseX;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace ItemMover
{
    public class ItemMover : NeosMod
    {
        public override string Name => "ItemMover";
        public override string Author => "eia485";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/eia485/NeosItemMover/";
        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony("net.eia485.ItemMover");
            harmony.PatchAll();
        }

        static FieldInfo itemInfo = AccessTools.Field(typeof(InventoryItemUI), "Item");
        static FieldInfo directoryInfo = AccessTools.Field(typeof(InventoryItemUI), "Directory");
        static FieldInfo currentInteractableInfo = AccessTools.Field(typeof(Canvas.InteractionData), "_currentInteractable");
        static FieldInfo currentInteractionsInfo = AccessTools.Field(typeof(Canvas), "_currentInteractions");
        static MethodInfo onGoUpInfo = AccessTools.Method(typeof(BrowserDialog), "OnGoUp");

        [HarmonyPatch(typeof(Canvas), nameof(Canvas.TryGrab))]
        class ItemMoverPatch
        {
            static IGrabbable Postfix(IGrabbable curval, Component grabber, Canvas __instance)
            {
                if (curval != null) return curval;
                var interactables = (Dictionary<Component, Canvas.InteractionData>)currentInteractionsInfo.GetValue(__instance);
                if (interactables == null) return curval;
                interactables.TryGetValue(grabber, out var value);
                if (value == null) return curval;
                var interactable = (IUIInteractable)currentInteractableInfo.GetValue(value);
                if (interactable == null) return curval;
                var item = interactable.Slot.GetComponentInParents<InventoryItemUI>();
                if (item == null) return curval;
                var record = itemInfo.GetValue(item);
                if (record == null) return curval;
                var proxy = ReferenceProxy.Construct(__instance.World, item.Slot.GetComponentInChildren<StaticTexture2D>(), false);
                proxy.Slot.AttachComponent<ReferenceField<InventoryItemUI>>().Reference.Target = item;
                var text = proxy.Slot[1].GetComponent<TextRenderer>();
                if (text != null)
                {
                    text.Text.Value = item.ItemName;
                    text.Color.Value = color.White;
                }
                return proxy;
            }
            [HarmonyPostfix]
            [HarmonyPatch(nameof(Canvas.Release))]
            static void ReleasePostfix(IEnumerable<IGrabbable> items, Component grabber, Canvas __instance)
            {

                var interactables = (Dictionary<Component, Canvas.InteractionData>)currentInteractionsInfo.GetValue(__instance);
                if (interactables == null) return;
                interactables.TryGetValue(grabber, out var value);
                if (value == null) return;
                var interactable = (IUIInteractable)currentInteractableInfo.GetValue(value);
                if (interactable == null) return;
                var item = interactable.Slot.GetComponentInParents<InventoryItemUI>();
                RecordDirectory dir = null;
                if (item != null)
                {
                    dir = (RecordDirectory)directoryInfo.GetValue(item);
                }
                else
                {
                    var comp = interactable.Slot.GetComponentInChildrenOrParents<ButtonRelay<int>>();
                    if (comp == null || comp.ButtonPressed.Target == null || comp.ButtonPressed.Target.Method != onGoUpInfo) return;
                    var levels = comp.Argument.Value;
                    dir = comp.Slot.GetComponentInParents<InventoryBrowser>().CurrentDirectory;
                    for (int i = 0; i < levels; i++)
                    {
                        if (dir.ParentDirectory != null)
                        {
                            dir = dir.ParentDirectory;
                        }
                    }
                }
                if (dir == null) return;
                InventoryItemUI soruce = null;
                foreach (var i in items)
                {
                    var comp = i.Slot.GetComponent<ReferenceField<InventoryItemUI>>();
                    if (comp == null || comp.Reference.Target == null) continue;
                    soruce = comp.Reference.Target;
                    break;
                }
                if (soruce == null) return;
                Record recToMove = (Record)itemInfo.GetValue(soruce);
                if (recToMove == null || soruce == null) return;
                Msg($"moving {recToMove.Name} from: {recToMove.Path} to: {dir.Path} ({recToMove.RecordId})");
                var oldPar = soruce.Slot.GetComponentInParents<InventoryBrowser>().CurrentDirectory;
                recToMove.Path = dir.Path;
                __instance.World.RunSynchronously(async delegate { await __instance.Engine.RecordManager.SaveRecord(recToMove).ConfigureAwait(continueOnCapturedContext: false); });
                soruce.Slot.Destroy();
                ((List<Record>)oldPar.Records).Remove(recToMove);
            }
        }
    }
}