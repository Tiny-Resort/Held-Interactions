using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using Mirror;
using UnityEngine;
using HarmonyLib;
using System.Reflection;

namespace TinyResort {
    
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class HeldInteractions : BaseUnityPlugin {

        public static TRPlugin Plugin;
        public const string pluginGuid = "tinyresort.dinkum.heldinteractions";
        public const string pluginName = "Held Interactions";
        public const string pluginVersion = "1.0.8";
        
        public static ConfigEntry<KeyCode> lockInteractionHotkey;
        public static bool lockInteraction;
        public static bool wasCarryingSomething;
        
        private void Awake() {
            
            Plugin = TRTools.Initialize(this, Logger, 24, pluginGuid, pluginName, pluginVersion);
            Plugin.QuickPatch(typeof(CharMovement), "Update", typeof(HeldInteractions), "UpdatePostfix");

            // Configuration
            lockInteractionHotkey = Config.Bind<KeyCode>("Keybinds", "LockInteractionHotkey", KeyCode.None, "The hotkey you can press to force interaction to be locked even if you let go of the key. Will be unlocked if you move at all.");

        }
        
        [HarmonyPostfix]
        public static void UpdatePostfix(CharMovement __instance) {

            if (__instance.myInteract.myEquip && __instance.myInteract.myEquip.currentlyHolding && __instance.myInteract.myEquip.currentlyHolding.itemName.Contains("Shovel")) return;

            //var isCarryingSomething = __instance.myPickUp.isCarryingSomething() || __instance.myEquip.isCarrying();
            
            ControllerInputActions controls = (ControllerInputActions) AccessTools.Field(typeof(InputMaster), "controls").GetValue(InputMaster.input);

            // Checks if the player is making any other kind of input or opening a menu
            var allowLock = !(InputMaster.input.UICancel() || InputMaster.input.Journal() || InputMaster.input.OpenInventory() ||
                              InputMaster.input.Other() || InputMaster.input.Use() || InputMaster.input.Jump() || InputMaster.input.Interact() ||
                              __instance.myEquip.isInVehicle() || !__instance.grounded || StatusManager.manage.dead || __instance.myInteract.placingDeed ||
                              PhotoManager.manage.cameraViewOpen || __instance.myEquip.getDriving() || __instance.myPickUp.sitting ||
                              InputMaster.input.getLeftStick() != Vector2.zero);


            // Allow the player to toggle a lock on interaction so that it continues interacting without input
            if (Input.GetKeyDown(lockInteractionHotkey.Value) && allowLock) {
                lockInteraction = !lockInteraction;
                Debug.Log("Setting lock interaction to " + lockInteraction);
                TRTools.TopNotification("Held Interactions", "Locked Interaction is now " + (lockInteraction ? "ENABLED" : "DISABLED"));
            }

            // Disables the interaction lock if the player did something incompatible
            if (lockInteraction && !allowLock) {
                lockInteraction = false;
                TRTools.TopNotification("Held Interactions", "Locked Interaction was interrupted by other inputs");
            }
            
            // If the player is holding down the interaction key OR has an interaction lock on, keep trying to place items in a machine
            if (!InputMaster.input.Use() && (controls.Controls.Use.ReadValue<float>() > 0 || lockInteraction) && 
                __instance.myEquip.currentlyHolding && (__instance.myEquip.currentlyHolding.itemName != "Camera" || 
                !PhotoManager.manage.cameraViewOpen || !PhotoManager.manage.canMoveCam) && !__instance.myEquip.getDriving()) {
                __instance.myPickUp.pressX();
            }

        }

    }

}
