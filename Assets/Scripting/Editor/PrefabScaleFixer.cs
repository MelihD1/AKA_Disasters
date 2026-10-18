
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Finds and repairs a classic Unity trap in a prefab: a root object with a
/// non-uniform scale (like Hall 1's 9, 1, 2). Any child that rotates under such
/// a parent gets sheared - doors stop swinging and start deforming - so the
/// scale gets baked down into the children and the root goes back to 1, 1, 1.
/// The geometry ends up pixel identical, it just stops shearing.
/// </summary>
public static class PrefabScaleFixer
{
    /// <summary>Reports the root scale and the shear on a rotating child.</summary>
    public static string Measure(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        try
        {
            StringBuilder log = new StringBuilder();

            Vector3 scale = root.transform.localScale;
            Vector3 assetScale = ReadAssetRootScale(path);

            log.AppendLine("prefab: " + path);
            log.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "root '{0}' scale loaded=({1:F3}, {2:F3}, {3:F3}) " +
                "asset=({4:F3}, {5:F3}, {6:F3}) uniform={7}",
                root.name, scale.x, scale.y, scale.z,
                assetScale.x, assetScale.y, assetScale.z,
                IsUniform(assetScale)));

            log.AppendLine("direct children: " + root.transform.childCount);

            for (int i = 0; i < root.transform.childCount && i < 6; i++)
            {
                Transform child = root.transform.GetChild(i);

                log.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "   [{0}] '{1}' local pos=({2:F2}, {3:F2}, {4:F2}) " +
                    "scale=({5:F3}, {6:F3}, {7:F3}) lossy=({8:F3}, {9:F3}, {10:F3})",
                    i, child.name,
                    child.localPosition.x, child.localPosition.y, child.localPosition.z,
                    child.localScale.x, child.localScale.y, child.localScale.z,
                    child.lossyScale.x, child.lossyScale.y, child.lossyScale.z));
            }

            Transform door = FindRotatingChild(root.transform);

            if (door == null)
            {
                log.AppendLine("no child named like a door to test.");
                return log.ToString();
            }

            log.AppendLine("test child: '" + door.name + "'");

            Quaternion original = door.localRotation;

            log.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "  shear when closed: {0:F4}",
                ShearOf(door)));

            door.localRotation = original * Quaternion.Euler(0f, 45f, 0f);

            log.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "  shear when swung 45 degrees: {0:F4}  <- anything but ~0 " +
                "means the door deforms when it opens",
                ShearOf(door)));

            door.localRotation = original;

            log.AppendLine("world bounds of the whole prefab: " +
                BoundsText(WorldBounds(root)));

            return log.ToString();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>The root scale straight from the asset file.</summary>
    private static Vector3 ReadAssetRootScale(string path)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        return asset != null ? asset.transform.localScale : Vector3.one;
    }

    /// <summary>
    /// Bakes the root scale into the children and resets it to 1. Every direct
    /// child's world matrix is compared before and after, and the prefab is
    /// only written when nothing moved.
    /// </summary>
    public static string BakeRootScale(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        try
        {
            StringBuilder log = new StringBuilder();
            Vector3 scale = root.transform.localScale;
            Vector3 assetScale = ReadAssetRootScale(path);

            log.AppendLine("prefab: " + path);
            log.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "root scale loaded=({0:F3}, {1:F3}, {2:F3}) asset=({3:F3}, {4:F3}, {5:F3})",
                scale.x, scale.y, scale.z,
                assetScale.x, assetScale.y, assetScale.z));

            if (IsUniform(assetScale))
            {
                log.AppendLine("scale is already uniform, nothing to do.");
                return log.ToString();
            }

            if (Vector3.Distance(scale, assetScale) > 0.0001f)
            {
                log.AppendLine("STOPPED: the loaded root scale does not match the " +
                    "asset, refusing to touch it.");
                return log.ToString();
            }

            List<Transform> children = new List<Transform>();

            for (int i = 0; i < root.transform.childCount; i++)
                children.Add(root.transform.GetChild(i));

            foreach (Transform child in children)
            {
                if (!IsAxisAligned(child.localRotation))
                {
                    log.AppendLine("STOPPED: '" + child.name + "' is rotated at an " +
                        "angle, baking would move geometry. Fix that prefab by hand.");
                    return log.ToString();
                }
            }

            // Remember exactly where everything sits in the world.
            List<Matrix4x4> beforeMatrices = new List<Matrix4x4>();

            foreach (Transform child in children)
                beforeMatrices.Add(child.localToWorldMatrix);

            Bounds beforeBounds = WorldBounds(root);

            foreach (Transform child in children)
            {
                child.localPosition = Vector3.Scale(child.localPosition, scale);

                // A rotated child's local axes point along different parent
                // axes, so its scale has to take the parent scale in that
                // order - otherwise 90 degree walls get stretched sideways.
                child.localScale = Vector3.Scale(
                    child.localScale, PermutedScale(child.localRotation, scale));
            }

            root.transform.localScale = Vector3.one;

            // Verify: nothing in the world may have moved.
            float worst = 0f;

            for (int i = 0; i < children.Count; i++)
            {
                Matrix4x4 now = children[i].localToWorldMatrix;

                for (int e = 0; e < 16; e++)
                    worst = Mathf.Max(worst, Mathf.Abs(now[e] - beforeMatrices[i][e]));
            }

            Bounds afterBounds = WorldBounds(root);

            log.AppendLine("children baked: " + children.Count);
            log.AppendLine("bounds before: " + BoundsText(beforeBounds));
            log.AppendLine("bounds after:  " + BoundsText(afterBounds));
            log.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "biggest world-matrix change: {0:F6} (must be ~0)",
                worst));

            if (worst > 0.0005f)
            {
                log.AppendLine("STOPPED: the bake would have moved geometry, " +
                    "so the prefab was left untouched.");
                return log.ToString();
            }

            Transform door = FindRotatingChild(root.transform);

            if (door != null)
            {
                Quaternion original = door.localRotation;
                door.localRotation = original * Quaternion.Euler(0f, 45f, 0f);

                log.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "shear on a 45 degree swing after the fix: {0:F4}",
                    ShearOf(door)));

                door.localRotation = original;
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            log.AppendLine("saved: " + path);

            return log.ToString();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Helpers ---------------------------------------------------------------

    /// <summary>
    /// The parent scale reordered so that entry i is the parent's scale along
    /// the axis the child's local axis i points down.
    /// </summary>
    private static Vector3 PermutedScale(Quaternion rotation, Vector3 parentScale)
    {
        return new Vector3(
            ScaleAlong(rotation * Vector3.right, parentScale),
            ScaleAlong(rotation * Vector3.up, parentScale),
            ScaleAlong(rotation * Vector3.forward, parentScale));
    }

    private static float ScaleAlong(Vector3 axisInParent, Vector3 parentScale)
    {
        Vector3 abs = new Vector3(
            Mathf.Abs(axisInParent.x),
            Mathf.Abs(axisInParent.y),
            Mathf.Abs(axisInParent.z));

        // The axis is a unit axis inside the parent once rotations are right
        // angles, so this picks the matching part of the parent scale.
        float x = abs.x >= abs.y && abs.x >= abs.z ? parentScale.x : 0f;
        float y = abs.y > abs.x && abs.y >= abs.z ? parentScale.y : 0f;
        float z = abs.z > abs.x && abs.z > abs.y ? parentScale.z : 0f;

        return x + y + z;
    }

    private static Transform FindRotatingChild(Transform root)
    {
        foreach (Transform child in root)
        {
            if (child.name.ToLowerInvariant().StartsWith("door"))
                return child;
        }

        return root.childCount > 0 ? root.GetChild(0) : null;
    }

    /// <summary>
    /// How far a transform's matrix is from a clean position/rotation/scale
    /// matrix. Anything but ~0 means the object is sheared.
    /// </summary>
    private static float ShearOf(Transform target)
    {
        Matrix4x4 actual = target.localToWorldMatrix;
        Matrix4x4 clean = Matrix4x4.TRS(
            target.position, target.rotation, target.lossyScale);

        float worst = 0f;

        for (int i = 0; i < 16; i++)
            worst = Mathf.Max(worst, Mathf.Abs(actual[i] - clean[i]));

        return worst;
    }

    private static Bounds WorldBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    private static string BoundsText(Bounds bounds)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "centre=({0:F3}, {1:F3}, {2:F3}) size=({3:F3}, {4:F3}, {5:F3})",
            bounds.center.x, bounds.center.y, bounds.center.z,
            bounds.size.x, bounds.size.y, bounds.size.z);
    }

    private static float BoundsError(Bounds a, Bounds b)
    {
        return Vector3.Distance(a.center, b.center) +
            Vector3.Distance(a.size, b.size);
    }

    private static bool IsUniform(Vector3 scale)
    {
        return Mathf.Abs(scale.x - scale.y) < 0.0001f &&
            Mathf.Abs(scale.y - scale.z) < 0.0001f;
    }

    private static bool IsAxisAligned(Quaternion rotation)
    {
        Vector3 forward = rotation * Vector3.forward;
        Vector3 up = rotation * Vector3.up;
        Vector3 right = rotation * Vector3.right;

        return IsAxis(forward) && IsAxis(up) && IsAxis(right);
    }

    private static bool IsAxis(Vector3 direction)
    {
        return Mathf.Abs(Mathf.Abs(direction.x) - 1f) < 0.001f ||
            Mathf.Abs(Mathf.Abs(direction.y) - 1f) < 0.001f ||
            Mathf.Abs(Mathf.Abs(direction.z) - 1f) < 0.001f;
    }
}
