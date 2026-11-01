using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Reflection;

namespace TestFight.LevelGeneration
{
    /// <summary>
    /// Generates 2D shadow caster geometry from a CompositeCollider2D on a Tilemap.
    /// Blocks torch and point light from penetrating solid walls, platforms, and ground.
    /// Automatically self-heals in Play mode if casters are missing or desynchronized.
    /// </summary>
    [RequireComponent(typeof(CompositeCollider2D))]
    public class TilemapShadowCaster2D : MonoBehaviour
    {
        private static readonly FieldInfo ShapePathField = typeof(ShadowCaster2D).GetField("m_ShapePath", BindingFlags.NonPublic | BindingFlags.Instance);

        private CompositeCollider2D _compositeCollider;

#if UNITY_EDITOR
        private bool IsPartOfPrefabAssetOrInvalidScene()
        {
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return true;
            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return true;
            return false;
        }
#endif

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (IsPartOfPrefabAssetOrInvalidScene()) return;
#endif
            _compositeCollider = GetComponent<CompositeCollider2D>();
            RebuildShadows();
        }

        private void Start()
        {
            RebuildShadows();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (IsPartOfPrefabAssetOrInvalidScene()) return;

            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall -= DelayRebuild;
                UnityEditor.EditorApplication.delayCall += DelayRebuild;
            }
        }

        private void DelayRebuild()
        {
            if (this != null && !Application.isPlaying)
            {
                if (IsPartOfPrefabAssetOrInvalidScene()) return;
                RebuildShadows();
            }
        }
#endif

        [ContextMenu("Rebuild Shadows")]
        public void RebuildShadows()
        {
#if UNITY_EDITOR
            if (IsPartOfPrefabAssetOrInvalidScene()) return;
#endif

            if (_compositeCollider == null)
                _compositeCollider = GetComponent<CompositeCollider2D>();

            if (_compositeCollider == null || ShapePathField == null) return;

            Transform container = transform.Find("ShadowCasters");
            if (container == null)
            {
                var go = new GameObject("ShadowCasters");
                go.transform.SetParent(transform, false);
                if (go.transform.parent != transform)
                {
                    if (Application.isPlaying) Destroy(go);
                    else DestroyImmediate(go);
                    return;
                }
                container = go.transform;
            }

            // In Play mode: if casters already match collider paths exactly, keep them
            if (Application.isPlaying && container.childCount == _compositeCollider.pathCount && _compositeCollider.pathCount > 0)
            {
                return;
            }

            // Safe removal: backwards loop, works both in Play mode and Edit mode
            for (int k = container.childCount - 1; k >= 0; k--)
            {
                var child = container.GetChild(k).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }

            for (int i = 0; i < _compositeCollider.pathCount; i++)
            {
                int ptCount = _compositeCollider.GetPathPointCount(i);
                if (ptCount < 3) continue;

                var pts2D = new Vector2[ptCount];
                _compositeCollider.GetPath(i, pts2D);

                var pts3D = new Vector3[ptCount];
                for (int j = 0; j < ptCount; j++)
                {
                    pts3D[j] = new Vector3(pts2D[j].x, pts2D[j].y, 0f);
                }

                var scGo = new GameObject($"ShadowCaster_Path_{i}");
                scGo.transform.SetParent(container, false);
                if (scGo.transform.parent != container)
                {
                    // Parenting failed - destroy immediately to avoid orphan in active scene
                    if (Application.isPlaying) Destroy(scGo);
                    else DestroyImmediate(scGo);
                    continue;
                }

                var sc = scGo.AddComponent<ShadowCaster2D>();
                sc.selfShadows = false;
                sc.castsShadows = true;
                ShapePathField.SetValue(sc, pts3D);
                sc.Update();
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Rebuilds shadow casters inside a prefab asset on disk using LoadPrefabContents / SaveAsPrefabAsset.
        /// </summary>
        public static bool RebuildPrefabShadows(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath)) return false;
            GameObject root = UnityEditor.PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null) return false;
            try
            {
                var casterComp = root.GetComponentInChildren<TilemapShadowCaster2D>(true);
                if (casterComp != null)
                {
                    var comp = casterComp.GetComponent<CompositeCollider2D>();
                    if (comp != null) comp.GenerateGeometry();
                    casterComp.RebuildShadows();
                    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    return true;
                }
                return false;
            }
            finally
            {
                UnityEditor.PrefabUtility.UnloadPrefabContents(root);
            }
        }
#endif
    }
}
