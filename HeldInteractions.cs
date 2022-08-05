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


namespace HeldInteractions {
    
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class HeldInteractions : BaseUnityPlugin {

        public static ManualLogSource StaticLogger;
        public const string pluginGuid = "tinyresort.dinkum.heldinteractions";
        public const string pluginName = "Held Interactions";
        public const string pluginVersion = "1.0.8";
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<int> nexusID;
        public static ConfigEntry<KeyCode> lockInteractionHotkey;
        public static bool lockInteraction;
        public static bool forceClearNotification;
        public static bool wasCarryingSomething;

        public static void Dbgl(string str = "", bool pref = true) {
            if (isDebug.Value) { StaticLogger.LogInfo(str); }
        }
        
        private void Awake() {

            // Configuration
            isDebug = Config.Bind<bool>("General", "DebugMode", false, "If true, the BepinEx console will print out debug messages related to this mod.");
            nexusID = Config.Bind<int>("General", "NexusID", 24, "Nexus Mod ID. You can find it on the mod's page on nexusmods.com");
            lockInteractionHotkey = Config.Bind<KeyCode>("Keybinds", "LockInteractionHotkey", KeyCode.None, "The hotkey you can press to force interaction to be locked even if you let go of the key. Will be unlocked if you move at all.");

            #region Logging
            StaticLogger = Logger;
            BepInExInfoLogInterpolatedStringHandler handler = new BepInExInfoLogInterpolatedStringHandler(18, 1, out var flag);
            if (flag) { handler.AppendLiteral("Plugin " + pluginGuid + " (v" + pluginVersion + ") loaded!"); }
            StaticLogger.LogInfo(handler);
            #endregion

            #region Patching
            Harmony harmony = new Harmony(pluginGuid);

            MethodInfo Update = AccessTools.Method(typeof(CharMovement), "Update");
            MethodInfo UpdatePostfix = AccessTools.Method(typeof(HeldInteractions), "UpdatePostfix");
            MethodInfo makeTopNotification = AccessTools.Method(typeof(NotificationManager), "makeTopNotification");
            MethodInfo makeTopNotificationPrefix = AccessTools.Method(typeof(HeldInteractions), "makeTopNotificationPrefix");
            harmony.Patch(makeTopNotification, new HarmonyMethod(makeTopNotificationPrefix));
            harmony.Patch(Update, new HarmonyMethod(UpdatePostfix));
            #endregion

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
                forceClearNotification = true;
                NotificationManager.manage.makeTopNotification("Held Interactions", "Locked Interaction is now " + (lockInteraction ? "ENABLED" : "DISABLED"));
            }

            // Disables the interaction lock if the player did something incompatible
            if (lockInteraction && !allowLock) {
                lockInteraction = false;
                forceClearNotification = true;
                NotificationManager.manage.makeTopNotification("Held Interactions", "Locked Interaction was interrupted by other inputs");
            }
            
            // If the player is holding down the interaction key OR has an interaction lock on, keep trying to place items in a machine
            if (!InputMaster.input.Use() && (controls.Controls.Use.ReadValue<float>() > 0 || lockInteraction) && 
                __instance.myEquip.currentlyHolding && (__instance.myEquip.currentlyHolding.itemName != "Camera" || 
                !PhotoManager.manage.cameraViewOpen || !PhotoManager.manage.canMoveCam) && !__instance.myEquip.getDriving()) {
                __instance.myPickUp.pressX();
            }

        }

        // Forcibly clears the top notification so that it can be replaced immediately
        [HarmonyPrefix]
        public static bool makeTopNotificationPrefix(NotificationManager __instance) {
            
            if (forceClearNotification) {
                forceClearNotification = false;
                
                var toNotify = (List<string>)AccessTools.Field(typeof(NotificationManager), "toNotify").GetValue(__instance);
                var subTextNot = (List<string>)AccessTools.Field(typeof(NotificationManager), "subTextNot").GetValue(__instance);
                var soundToPlay = (List<ASound>)AccessTools.Field(typeof(NotificationManager), "soundToPlay").GetValue(__instance);
                var topNotificationRunning = AccessTools.Field(typeof(NotificationManager), "topNotificationRunning");
                var topNotificationRunningRoutine = topNotificationRunning.GetValue(__instance);
                
                // Clears existing notifications in the queue
                toNotify.Clear();
                subTextNot.Clear();
                soundToPlay.Clear();

                // Stops the current coroutine from continuing
                if (topNotificationRunningRoutine != null) {
                    __instance.StopCoroutine((Coroutine) topNotificationRunningRoutine);
                    topNotificationRunning.SetValue(__instance, null);
                }
                
                // Resets all animations related to the notificatin bubble appearing/disappearing
                __instance.StopCoroutine("closeWithMask");
                __instance.topNotification.StopAllCoroutines();
                var Anim = __instance.topNotification.GetComponent<WindowAnimator>();
                Anim.StopAllCoroutines();
                Anim.maskChild.enabled = false;
                Anim.contents.gameObject.SetActive(false);
                Anim.gameObject.SetActive(false);
                
                return true;
                
            } else return true;
        }

    }

}
