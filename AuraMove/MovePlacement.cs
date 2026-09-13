using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OttoAura.AuraMove;

// Placement ghost for AuraMove: builds and drives a private ghost of the piece being moved,
// validates the destination each frame, and tears the ghost down on cancel.
//
// We cannot use Player.SetupPlacementGhost or Player.UpdatePlacementGhost: both read
// m_buildPieces.GetSelectedPrefab(), which is null outside build mode. We also never assign
// to Player.m_placementGhost - vanilla would destroy or fight it.
internal static class MovePlacement
{
    private static GameObject? _ghost;
    private static Piece? _ghostPiece;
    private static int _rotationStep;
    private static Vector3 _sourcePosition;
    private static bool _isValid;

    // Every Material this ghost cloned. Unity does not collect materials assigned through
    // sharedMaterials when the renderer dies, so without this list each move would leak one
    // Material per submesh for the rest of the session.
    private static readonly List<Material> _clonedMaterials = new();

    // True when a ghost is alive.
    internal static bool Active => _ghost != null;

    // True when the last UpdateGhost call found a valid destination.
    internal static bool IsValid => _isValid;

    // Ghost world position as of the last UpdateGhost call.
    internal static Vector3 Position => _ghost != null ? _ghost.transform.position : Vector3.zero;

    // Ghost rotation as of the last UpdateGhost call.
    internal static Quaternion Rotation => _ghost != null ? _ghost.transform.rotation : Quaternion.identity;

    // Build a placement ghost for the given piece. Returns false (and logs a warning) if the
    // prefab cannot be resolved; safe to call while a ghost is already active (replaces it).
    internal static bool Begin(Piece piece)
    {
        Cancel();

        Player? player = Player.m_localPlayer;
        if (player == null || ZNetScene.instance == null)
        {
            return false;
        }

        string prefabName = Utils.GetPrefabName(piece.gameObject.name);
        GameObject? prefab = ZNetScene.instance.GetPrefab(prefabName);
        if (prefab == null)
        {
            OttoAuraPlugin.OttoAuraLogger.LogWarning($"AuraMove: Could not resolve prefab '{prefabName}' for placement ghost.");
            return false;
        }

        // Seed rotation from the piece's current facing.
        _rotationStep = Mathf.RoundToInt(piece.transform.rotation.eulerAngles.y / player.m_placeRotationDegrees);
        _sourcePosition = piece.transform.position;

        // Instantiate the ghost with network and terrain ops suppressed so no ZNetView or terrain
        // operations fire. This is a trimmed port of Player.SetupPlacementGhost.
        bool prevForceDisable = ZNetView.m_forceDisableInit;
        bool prevTerrainOps = TerrainOp.m_forceDisableTerrainOps;
        TerrainModifier? prefabTerrain = prefab.GetComponent<TerrainModifier>();
        bool prefabTerrainWasEnabled = prefabTerrain != null && prefabTerrain.enabled;
        try
        {
            ZNetView.m_forceDisableInit = true;
            TerrainOp.m_forceDisableTerrainOps = true;
            if (prefabTerrain != null)
            {
                prefabTerrain.enabled = false;
            }

            // Spawn on top of the real piece, not at the world origin. Awake runs during
            // Instantiate and components that cache a world position there (EffectArea caches
            // its collider bounds) would otherwise cache the origin. UpdateGhost moves it on
            // the next frame; vanilla parks its ghost on the player for the same reason
            // (Player.SetupPlacementGhost).
            _ghost = Object.Instantiate(prefab, piece.transform.position, piece.transform.rotation);
        }
        finally
        {
            ZNetView.m_forceDisableInit = prevForceDisable;
            TerrainOp.m_forceDisableTerrainOps = prevTerrainOps;
            if (prefabTerrain != null)
            {
                prefabTerrain.enabled = prefabTerrainWasEnabled;
            }
        }

        // Destroy components that would interfere with a purely visual ghost.
        foreach (TerrainModifier tm in _ghost.GetComponentsInChildren<TerrainModifier>(true))
        {
            Object.Destroy(tm);
        }
        foreach (GuidePoint gp in _ghost.GetComponentsInChildren<GuidePoint>(true))
        {
            Object.Destroy(gp);
        }
        foreach (Joint j in _ghost.GetComponentsInChildren<Joint>(true))
        {
            Object.Destroy(j);
        }
        foreach (Rigidbody rb in _ghost.GetComponentsInChildren<Rigidbody>(true))
        {
            Object.Destroy(rb);
        }
        foreach (ParticleSystemForceField psf in _ghost.GetComponentsInChildren<ParticleSystemForceField>(true))
        {
            Object.Destroy(psf);
        }
        foreach (Demister dm in _ghost.GetComponentsInChildren<Demister>(true))
        {
            Object.Destroy(dm);
        }
        foreach (LightLod ll in _ghost.GetComponentsInChildren<LightLod>(true))
        {
            Object.Destroy(ll);
        }
        foreach (LightFlicker lf in _ghost.GetComponentsInChildren<LightFlicker>(true))
        {
            Object.Destroy(lf);
        }
        foreach (Light lt in _ghost.GetComponentsInChildren<Light>(true))
        {
            Object.Destroy(lt);
        }
        foreach (WispSpawner ws in _ghost.GetComponentsInChildren<WispSpawner>(true))
        {
            Object.Destroy(ws);
        }

        // EffectArea.Awake registers the collider bounds in the static s_BurningAreas /
        // s_noMonsterAreas lists and never moves them again, so a ghost of a brazier or a
        // fireplace would burn and suppress spawns wherever it was instantiated for as long as
        // the move lasts. Nothing else in the game holds an EffectArea reference, so the ghost
        // can simply lose them.
        foreach (EffectArea ea in _ghost.GetComponentsInChildren<EffectArea>(true))
        {
            Object.Destroy(ea);
        }

        // Behaviours that should be suppressed, not destroyed.
        foreach (AudioSource au in _ghost.GetComponentsInChildren<AudioSource>(true))
        {
            au.enabled = false;
        }
        foreach (ZSFX zsfx in _ghost.GetComponentsInChildren<ZSFX>(true))
        {
            zsfx.enabled = false;
        }
        foreach (Windmill wm in _ghost.GetComponentsInChildren<Windmill>(true))
        {
            wm.enabled = false;
        }

        // MagicaCloth ships with the game's asset bundles rather than an assembly we reference,
        // so it is matched by type name.
        DisableBehaviourByTypeName(_ghost, "MagicaCloth");

        // Park the particle emitters, the same way Player.SetupPlacementGhost does: the component
        // plays on enable, so the whole child object goes away instead.
        foreach (ParticleSystem ps in _ghost.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.gameObject.SetActive(false);
        }

        // Disable colliders that are not on a layer the placement ray tests against;
        // they would interfere with the ghost's own collision queries.
        foreach (Collider col in _ghost.GetComponentsInChildren<Collider>(true))
        {
            if ((player.m_placeRayMask & (1 << col.gameObject.layer)) == 0)
            {
                col.enabled = false;
            }
        }

        // Set every transform on the ghost to the "ghost" layer.
        int ghostLayer = LayerMask.NameToLayer("ghost");
        SetLayerRecursive(_ghost.transform, ghostLayer);

        // Show _GhostOnly child if present.
        Transform? ghostOnly = _ghost.transform.Find("_GhostOnly");
        if (ghostOnly != null)
        {
            ghostOnly.gameObject.SetActive(true);
        }

        // Match the prefab's scale exactly.
        _ghost.transform.localScale = prefab.transform.localScale;

        // Clone materials on all mesh renderers (port of CleanupGhostMaterials, minus ripple tracking).
        CloneGhostMaterials(_ghost);

        _ghostPiece = _ghost.GetComponent<Piece>();
        _isValid = false;
        return true;
    }

    // Called once per frame while a move is in progress. Updates the ghost's position, rotation,
    // and the IsValid flag. Runs a port of the relevant sections of Player.UpdatePlacementGhost.
    internal static void UpdateGhost(Piece piece)
    {
        if (_ghost == null || _ghostPiece == null)
        {
            return;
        }

        Player? player = Player.m_localPlayer;
        if (player == null)
        {
            return;
        }

        // Accumulate rotation input.
        if (_ghostPiece.m_canRotate)
        {
            _rotationStep += MoveTargeting.ConsumeRotationSteps();
        }

        Quaternion rotation = Quaternion.Euler(0f, player.m_placeRotationDegrees * _rotationStep, 0f);

        // PieceRayTest is public and works outside build mode.
        bool water = _ghostPiece.m_waterPiece || _ghostPiece.m_noInWater;
        bool hit = player.PieceRayTest(out Vector3 point, out Vector3 normal, out Piece _, out Heightmap heightmap, out Collider waterSurface, water);

        if (!hit)
        {
            _ghost.SetActive(false);
            _isValid = false;
            return;
        }

        // Preserve the raw normal for surface-angle checks before vanilla's override.
        Vector3 rawNormal = normal;

        // Ensure the ghost is active so GetComponentsInChildren can see its children during the
        // lift-mode ClosestPoint queries. (It may have been deactivated by a prior failed ray test.)
        _ghost.SetActive(true);

        // Determine world position using the same two-branch logic as vanilla.
        Vector3 ghostPosition;
        bool useStraightDrop = (_ghostPiece.m_groundPiece || _ghostPiece.m_clipGround) && heightmap != null
                               || _ghostPiece.m_clipEverything;

        if (useStraightDrop)
        {
            // Ground/clip mode: set directly at the hit point.
            ghostPosition = point;
        }
        else
        {
            // Lift mode: raise the ghost 50 units on the normal, find the lowest contact point
            // across all enabled non-trigger child colliders, then lower the ghost to just rest on
            // the surface. This is how vanilla prevents pieces floating above angled surfaces.
            Vector3 raised = point + normal * 50f;
            _ghost.transform.SetPositionAndRotation(raised, rotation);

            Vector3 closestPoint = raised;
            float closestDist = float.MaxValue;

            foreach (Collider col in _ghost.GetComponentsInChildren<Collider>(false))
            {
                if (!IsSolidConvex(col))
                {
                    continue;
                }

                Vector3 cp = col.ClosestPoint(point);
                float dist = Vector3.Distance(cp, point);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestPoint = cp;
                }
            }

            ghostPosition = point + (raised - closestPoint);
        }

        _ghost.transform.SetPositionAndRotation(ghostPosition, rotation);

        // ---- Validity checks ----
        // Start valid; any failed check clears the flag and continues so we end up at the
        // SetInvalidPlacementHeightlight call regardless of which check failed.
        _isValid = true;
        Vector3 position = _ghost.transform.position;

        // 1. Distance from the piece's current position.
        if (Vector3.Distance(position, _sourcePosition) > OttoAuraPlugin.AuraMoveMaxDistance.Value)
        {
            _isValid = false;
        }

        // 2. No-build location.
        if (Location.IsInsideNoBuildLocation(position))
        {
            _isValid = false;
        }

        // 3. Ward access at the destination. Do NOT flash the ward here - this runs every frame.
        PrivateArea? ghostPrivateArea = _ghostPiece.GetComponent<PrivateArea>();
        float ghostWardRadius = ghostPrivateArea != null ? ghostPrivateArea.m_radius : 0f;
        if (!PrivateArea.CheckAccess(position, ghostWardRadius, false, ghostPrivateArea != null))
        {
            _isValid = false;
        }

        // 4. Ground and cultivation rules.
        if ((_ghostPiece.m_groundOnly || _ghostPiece.m_groundPiece) && heightmap == null)
        {
            _isValid = false;
        }

        if (_ghostPiece.m_cultivatedGroundOnly && (heightmap == null || !heightmap.IsCultivated(point)))
        {
            _isValid = false;
        }

        // 5. Water rules.
        if (_ghostPiece.m_waterPiece && waterSurface == null)
        {
            _isValid = false;
        }

        if (_ghostPiece.m_noInWater && waterSurface != null)
        {
            _isValid = false;
        }

        // 6. Surface-angle rules (tested against the raw normal before vanilla's heightmap override).
        if (_ghostPiece.m_notOnTiltingSurface && rawNormal.y < 0.8f)
        {
            _isValid = false;
        }

        if (_ghostPiece.m_inCeilingOnly && rawNormal.y > -0.5f)
        {
            _isValid = false;
        }

        if (_ghostPiece.m_notOnFloor && rawNormal.y > 0.1f)
        {
            _isValid = false;
        }

        // 7. Biome restriction.
        if (_ghostPiece.m_onlyInBiome != Heightmap.Biome.None
            && (Heightmap.FindBiome(position) & _ghostPiece.m_onlyInBiome) == 0)
        {
            _isValid = false;
        }

        // 8. Players in the way (port of Player.CheckPlacementGhostVSPlayers, but against our ghost).
        if (_isValid && CheckGhostVSPlayers(player))
        {
            _isValid = false;
        }

        // 9. Clipping into other pieces, excluding the piece being moved itself because it is
        //    still standing at the source position and would otherwise block every nearby
        //    destination. Only pieces that vanilla itself refuses to clip are tested; anything
        //    else may legitimately overlap and denying it would be stricter than the hammer.
        if (_isValid && _ghostPiece.m_noClipping && CheckGhostClipping(piece, player))
        {
            _isValid = false;
        }

        _ghostPiece.SetInvalidPlacementHeightlight(!_isValid);
    }

    // Destroy the ghost. Safe to call when nothing is active and safe to call more than once.
    internal static void Cancel()
    {
        if (_ghost != null)
        {
            Object.Destroy(_ghost);
            _ghost = null;
        }

        foreach (Material material in _clonedMaterials)
        {
            if (material != null)
            {
                Object.Destroy(material);
            }
        }
        _clonedMaterials.Clear();

        _ghostPiece = null;
        _isValid = false;
    }

    // ---- Private helpers -------------------------------------------------------

    // Port of Player.CheckPlacementGhostVSPlayers, testing our private ghost instead of
    // m_placementGhost. ComputePenetration requires convex or primitive colliders.
    private static bool CheckGhostVSPlayers(Player player)
    {
        if (_ghost == null)
        {
            return false;
        }

        List<Character> characters = new();
        Character.GetCharactersInRange(player.transform.position, 30f, characters);

        foreach (Character character in characters)
        {
            Collider? charCollider = character.GetCollider();
            if (charCollider == null || !IsSolidConvex(charCollider))
            {
                continue;
            }

            foreach (Collider ghostCol in _ghost.GetComponentsInChildren<Collider>(false))
            {
                if (!IsSolidConvex(ghostCol))
                {
                    continue;
                }

                // Each collider is tested at its own transform, not the ghost root's: most
                // pieces keep their colliders on child objects and passing the root pose would
                // measure the penetration of a collider that is not where it says it is.
                if (Physics.ComputePenetration(ghostCol, ghostCol.transform.position, ghostCol.transform.rotation,
                    charCollider, charCollider.transform.position, charCollider.transform.rotation,
                    out _, out float depth) && depth > 0f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Port of Player.TestGhostClipping(ghost, 0.2f), with the source piece excluded so the real
    // object's colliders do not veto its own relocation.
    private static bool CheckGhostClipping(Piece sourcePiece, Player player)
    {
        if (_ghost == null)
        {
            return false;
        }

        Collider[] overlaps = Physics.OverlapSphere(_ghost.transform.position, 10f, player.m_placeRayMask);
        List<Collider> ghostColliders = new();
        foreach (Collider col in _ghost.GetComponentsInChildren<Collider>(false))
        {
            if (IsSolidConvex(col))
            {
                ghostColliders.Add(col);
            }
        }

        foreach (Collider overlap in overlaps)
        {
            if (!IsSolidConvex(overlap))
            {
                continue;
            }

            // Skip colliders that belong to the piece being moved - it is still at its old spot
            // and would otherwise reject every destination within range of itself.
            if (overlap.GetComponentInParent<Piece>() == sourcePiece)
            {
                continue;
            }

            foreach (Collider ghostCol in ghostColliders)
            {
                if (Physics.ComputePenetration(ghostCol, ghostCol.transform.position, ghostCol.transform.rotation,
                    overlap, overlap.transform.position, overlap.transform.rotation,
                    out _, out float depth) && depth > 0.2f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // A collider PhysX can answer ClosestPoint and ComputePenetration for: enabled, solid, and
    // either a primitive or a convex mesh.
    private static bool IsSolidConvex(Collider col) =>
        col.enabled && !col.isTrigger && col is not MeshCollider { convex: false };

    // Clone materials on all MeshRenderers and SkinnedMeshRenderers and configure ghost shader
    // parameters. Port of Player.CleanupGhostMaterials, minus the m_tempMaterials ripple tracking.
    private static void CloneGhostMaterials(GameObject ghost)
    {
        foreach (MeshRenderer r in ghost.GetComponentsInChildren<MeshRenderer>(true))
        {
            MakeGhostMaterials(r);
        }

        foreach (SkinnedMeshRenderer r in ghost.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            MakeGhostMaterials(r);
        }
    }

    private static void MakeGhostMaterials(Renderer renderer)
    {
        // A renderer with an empty material slot would throw out of the material constructor,
        // which is why vanilla skips these renderers too (Player.CleanupGhostMaterials).
        if (renderer.sharedMaterial == null)
        {
            return;
        }

        renderer.shadowCastingMode = ShadowCastingMode.Off;
        Material[] originals = renderer.sharedMaterials;
        Material[] cloned = new Material[originals.Length];
        for (int i = 0; i < originals.Length; i++)
        {
            if (originals[i] == null)
            {
                continue;
            }

            cloned[i] = new Material(originals[i]);
            cloned[i].SetFloat("_ValueNoise", 0f);
            cloned[i].SetFloat("_TriplanarLocalPos", 1f);
            _clonedMaterials.Add(cloned[i]);
        }

        renderer.sharedMaterials = cloned;
    }

    // Set the layer of a transform and all its descendants.
    private static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
        {
            SetLayerRecursive(t.GetChild(i), layer);
        }
    }

    // Disable all Behaviour components of the given type name, for components that live in an
    // assembly the project does not reference.
    private static void DisableBehaviourByTypeName(GameObject root, string typeName)
    {
        foreach (Behaviour b in root.GetComponentsInChildren<Behaviour>(true))
        {
            if (b.GetType().Name == typeName)
            {
                b.enabled = false;
            }
        }
    }
}
