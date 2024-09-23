using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Linq;
using UnityEngine;

namespace ToggleBowDrawMod
{
    [BepInPlugin(pluginGUID, pluginName, pluginVersion)]
    [BepInProcess("valheim.exe")]   

    public class ToggleBowDrawMod : BaseUnityPlugin
    {
        const string pluginGUID = "com.denizhanoter.ToggleBowDrawMod";
        const string pluginName = "Valheim - Toggle Bow Draw Mod";
        const string pluginVersion = "1.0.0";
        public static ConfigEntry<bool> EnableToggle;
        private static ConfigEntry<KeyCode> toggleKey;
        private static bool isToggleEnabled = false;
        private static bool isDrawing = false;

        private void Awake()
        {
            // Konfigürasyon seçeneklerini oluşturma
            EnableToggle = Config.Bind("Mod Config", "Enable", true, "Enable or Disable this mod");
            toggleKey = Config.Bind("General", "ToggleKey", KeyCode.N, "The key to toggle bow draw mode.");

            Harmony harmony = new Harmony("com.denizhanoter.ToggleBowDrawMod");
            harmony.PatchAll();
        }

        private void Update()
        {
            // Modun etkin olup olmadığını kontrol et
            if (!EnableToggle.Value)
            {
                return;
            }
            if (Console.IsVisible() || Menu.IsVisible() || Hud.IsPieceSelectionVisible() || Chat.instance?.IsChatDialogWindowVisible() == true || InventoryGui.IsVisible() || Player.m_localPlayer?.IsDead() == true)
            {
                return;
            }
            // ZInput'in başlatıldığından emin olun
            if (ZInput.instance == null)
                return;

            if (ZInput.GetKeyDown(toggleKey.Value))
            {
                isToggleEnabled = !isToggleEnabled;
                Logger.LogInfo($"Toggle Bow Draw is now {(isToggleEnabled ? "enabled" : "disabled")}");
            }
        }

        [HarmonyPatch(typeof(Player), "UpdateAttackBowDraw")]
        public class UpdateAttackBowDraw_Patch
        {
            static bool Prefix(Player __instance, ItemDrop.ItemData weapon, float dt)
            {
                // Modun etkin olup olmadığını kontrol et
                if (!EnableToggle.Value)
                {
                    return true;
                }
                bool hasBowEquipped = __instance.GetInventory().GetEquippedItems().Any(item => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow);
                if (!isToggleEnabled || weapon == null || !hasBowEquipped)
                    return true;
                var attackDrawTimeField = AccessTools.Field(typeof(Humanoid), "m_attackDrawTime");
                var attackHoldField = AccessTools.Field(typeof(Character), "m_attackHold");
                var semanField = AccessTools.Field(typeof(Character), "m_seman");
                var zanimField = AccessTools.Field(typeof(Character), "m_zanim");
                var blockingField = AccessTools.Field(typeof(Character), "m_blocking");

                float attackDrawTime = (float)attackDrawTimeField.GetValue(__instance);
                bool attackHold = (bool)attackHoldField.GetValue(__instance);
                SEMan seman = (SEMan)semanField.GetValue(__instance);
                ZSyncAnimation zanim = (ZSyncAnimation)zanimField.GetValue(__instance);
                bool blocking = (bool)blockingField.GetValue(__instance);

                if (blocking || __instance.InMinorAction() || __instance.IsAttached())
                {
                    attackDrawTime = -1f;
                    if (!string.IsNullOrEmpty(weapon.m_shared.m_attack.m_drawAnimationState))
                    {
                        zanim.SetBool(weapon.m_shared.m_attack.m_drawAnimationState, value: false);
                    }
                    attackDrawTimeField.SetValue(__instance, attackDrawTime);
                    return false;
                }

                float num = weapon.GetDrawStaminaDrain();
                float drawEitrDrain = weapon.GetDrawEitrDrain();
                if ((double)__instance.GetAttackDrawPercentage() >= 1.0)
                {
                    num *= 0.5f;
                }

                num += num * __instance.GetEquipmentAttackStaminaModifier();
                seman.ModifyAttackStaminaUsage(num, ref num);
                bool flag = num <= 0f || __instance.HaveStamina();
                bool flag2 = drawEitrDrain <= 0f || __instance.HaveEitr();

                if (isToggleEnabled)
                {
                    if (attackDrawTime < 0f)
                    {
                        if (!isDrawing)
                        {
                            attackDrawTime = 0f;
                        }
                    }
                    else if (isDrawing && flag && attackDrawTime >= 0f)
                    {
                        if (attackDrawTime == 0f)
                        {
                            if (!weapon.m_shared.m_attack.StartDraw(__instance, weapon))
                            {
                                attackDrawTime = -1f;
                                attackDrawTimeField.SetValue(__instance, attackDrawTime);
                                return false;
                            }

                            weapon.m_shared.m_holdStartEffect.Create(__instance.transform.position, Quaternion.identity, __instance.transform);
                        }

                        attackDrawTime += Time.fixedDeltaTime;
                        if (!string.IsNullOrEmpty(weapon.m_shared.m_attack.m_drawAnimationState))
                        {
                            zanim.SetBool(weapon.m_shared.m_attack.m_drawAnimationState, value: true);
                            zanim.SetFloat("drawpercent", __instance.GetAttackDrawPercentage());
                        }

                        __instance.UseStamina(num * dt);
                        __instance.UseEitr(drawEitrDrain * dt);
                    }
                    else if (attackDrawTime > 0f)
                    {
                        if (flag && flag2)
                        {
                            __instance.StartAttack(null, secondaryAttack: false);
                        }

                        if (!string.IsNullOrEmpty(weapon.m_shared.m_attack.m_drawAnimationState))
                        {
                            zanim.SetBool(weapon.m_shared.m_attack.m_drawAnimationState, value: false);
                        }

                        attackDrawTime = 0f;
                    }

                    if (ZInput.GetButtonDown("Attack"))
                    {
                        isDrawing = !isDrawing;
                    }
                }
                else
                {
                    if (attackDrawTime < 0f)
                    {
                        if (!attackHold)
                        {
                            attackDrawTime = 0f;
                        }
                    }
                    else if (attackHold && flag && attackDrawTime >= 0f)
                    {
                        if (attackDrawTime == 0f)
                        {
                            if (!weapon.m_shared.m_attack.StartDraw(__instance, weapon))
                            {
                                attackDrawTime = -1f;
                                attackDrawTimeField.SetValue(__instance, attackDrawTime);
                                return false;
                            }

                            weapon.m_shared.m_holdStartEffect.Create(__instance.transform.position, Quaternion.identity, __instance.transform);
                        }

                        attackDrawTime += Time.fixedDeltaTime;
                        if (!string.IsNullOrEmpty(weapon.m_shared.m_attack.m_drawAnimationState))
                        {
                            zanim.SetBool(weapon.m_shared.m_attack.m_drawAnimationState, value: true);
                            zanim.SetFloat("drawpercent", __instance.GetAttackDrawPercentage());
                        }

                        __instance.UseStamina(num * dt);
                        __instance.UseEitr(drawEitrDrain * dt);
                    }
                    else if (attackDrawTime > 0f)
                    {
                        if (flag && flag2)
                        {
                            __instance.StartAttack(null, secondaryAttack: false);
                        }

                        if (!string.IsNullOrEmpty(weapon.m_shared.m_attack.m_drawAnimationState))
                        {
                            zanim.SetBool(weapon.m_shared.m_attack.m_drawAnimationState, value: false);
                        }

                        attackDrawTime = 0f;
                    }
                }

                attackDrawTimeField.SetValue(__instance, attackDrawTime);
                return false; // Skip original method
            }
        }
    }
}