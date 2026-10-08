using System;
using System.Reflection;
using GlobalSettings;
using TeamCherry.SharedUtils;
using UnityEngine;

namespace RandomCrestMod;

/// <summary>
/// Spawns the one-time Hornet Statuette (大黄蜂雕像 / "Fixer Idol") world pickup in Bellhart
/// (钟心镇), just inside the entrance by the map seller. This is one of the mod's two extra
/// acquisition routes for the statue that unlocks the multi-throw barrage.
///
/// <para>The pickup is <b>not</b> gated on the Chaos crest - it is a straight reward for running the
/// mod. It is taken exactly once per save: the moment it is collected we record a
/// <see cref="SceneData"/> persistent bool, so it never respawns even if the statue is later
/// consumed for shell shards.</para>
///
/// <para>Driven every frame from <see cref="RandomCrestRunner"/>: when the hero is in Belltown and
/// the pickup has not been taken, we instantiate the game's own collectable pickup prefab, point it
/// at the Fixer Idol collectable and give it the statue icon. Spawning is retried until it succeeds
/// (the scene managers are not all ready on the first frame).</para>
/// </summary>
internal static class StatuePickupService
{
    /// <summary>Unity scene name of Bellhart (the scene asset lives at Assets/Scenes/Hornet/Belltown.unity).</summary>
    private const string SceneName = "Belltown";

    /// <summary>Persistent-bool id used to remember the pickup was taken.</summary>
    private const string PickupId = "RandomCrestMod_HornetStatue";

    /// <summary>Asset name of the Hornet Statuette collectable.</summary>
    private const string ItemName = "Fixer Idol";

    /// <summary>World position in Belltown, captured in-game (map-seller area, entrance side).</summary>
    private static readonly Vector2 PickupPosition = new Vector2(97.39893f, 22.56768f);

    private static GameObject? _spawned;
    private static bool _loggedUnavailable;

    /// <summary>Per-frame spawn check. Cheap when the hero is not in Belltown.</summary>
    internal static void Tick()
    {
        var manager = GameManager.instance;
        var scene = manager != null ? manager.sceneName : null;
        if (!string.Equals(scene, SceneName, StringComparison.OrdinalIgnoreCase))
        {
            // Left the scene: the pickup (if any) was destroyed with it, so allow a re-check.
            _spawned = null;
            _loggedUnavailable = false;
            return;
        }

        // Do not fight the game when pickups are not allowed (boss arenas, memory scenes, cutscenes).
        if (manager == null || !manager.CanPickupsExist())
        {
            return;
        }

        // A destroyed pickup compares equal to null, so this also covers the game recycling it.
        if (_spawned != null || AlreadyTaken())
        {
            return;
        }

        Spawn();
    }

    private static bool AlreadyTaken()
    {
        try
        {
            var data = SceneData.instance;
            return data != null && data.PersistentBools.GetValueOrDefault(SceneName, PickupId);
        }
        catch
        {
            return false;
        }
    }

    private static void MarkTaken()
    {
        try
        {
            SceneData.instance?.PersistentBools.SetValue(new PersistentItemData<bool>
            {
                ID = PickupId,
                SceneName = SceneName,
                Value = true,
            });
            RandomCrestModPlugin.LogInfo("[StatuePickup] Hornet Statuette taken.");
        }
        catch (Exception e)
        {
            RandomCrestModPlugin.LogError("[StatuePickup] persist failed: " + e.Message);
        }
    }

    private static void Spawn()
    {
        try
        {
            var prefab = Gameplay.CollectableItemPickupPrefab;
            var item = CollectableItemManager.GetItemByName(ItemName);
            if (prefab == null || item == null)
            {
                if (!_loggedUnavailable)
                {
                    _loggedUnavailable = true;
                    RandomCrestModPlugin.Log("[StatuePickup] pickup prefab / statue item not ready yet.");
                }

                return;
            }

            var pickup = UnityEngine.Object.Instantiate(prefab);
            pickup.name = "RandomCrestMod Hornet Statue Pickup";
            pickup.transform.SetPosition2D(PickupPosition);
            pickup.SetItem(item);

            // Point the prefab's renderer at the statue icon (the generic prefab ships its own art).
            var field = typeof(CollectableItemPickup).GetField(
                "spriteRenderer", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null && field.GetValue(pickup) is SpriteRenderer renderer)
            {
                var sprite = item.GetIcon(CollectableItem.ReadSource.Inventory);
                if (sprite != null)
                {
                    renderer.sprite = sprite;
                }
            }

            pickup.OnPickup.AddListener(MarkTaken);
            _spawned = pickup.gameObject;
            RandomCrestModPlugin.LogInfo("[StatuePickup] Spawned the Hornet Statuette pickup in " + SceneName + ".");
        }
        catch (Exception e)
        {
            if (!_loggedUnavailable)
            {
                _loggedUnavailable = true;
                RandomCrestModPlugin.LogError("[StatuePickup] spawn failed: " + e.Message);
            }
        }
    }
}
