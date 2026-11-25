using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NangTani.EditorTools
{
    /// <summary>
    /// Menu "Nang Tani > 2 - Habiller la scene (nouveaux assets)".
    /// Copie NangTani_Main vers NangTani_Decor, remplace les bananiers et les maisons greybox par les
    /// modeles de Art/Models, puis ajoute maisons, foret et sous-bois d'apres Art/decor_layout.json.
    /// La scene d'origine n'est pas modifiee. Relancer le menu recree NangTani_Decor.
    /// </summary>
    public static class NangTaniDecorBuilder
    {
        const string Art = "Assets/NangTani/Art";
        const string ModelDir = Art + "/Models";
        const string TextureDir = Art + "/Textures";
        const string MaterialDir = Art + "/Materials";
        const string GeneratedDir = Art + "/Generated";
        const string LayoutPath = Art + "/decor_layout.json";
        const string MeshPath = GeneratedDir + "/Decor_Meshes.asset";
        const string WindowMaterialPath = "Assets/NangTani/Materials/FenetreChaude.mat";
        const string SourceScene = "Assets/Scenes/NangTani_Main.unity";
        const string TargetScene = "Assets/Scenes/NangTani_Decor.unity";

        const float CellSize = 16f;   // la foret est fusionnee par carres de 16 m (moins de draw calls)
        const float FarClip = 70f;    // au-dela, le brouillard masque tout

        [System.Serializable]
        class Layout
        {
            public HouseItem[] houses;
            public PlantItem[] items;
        }

        [System.Serializable]
        class HouseItem
        {
            public string model;
            public float x, z, scale, yaw;
            public bool lit;
        }

        [System.Serializable]
        class PlantItem
        {
            public string model;
            public float x, y, z, yaw, scale, tilt;
            public bool lite, shadow;
        }

        struct Part
        {
            public string name;
            public Mesh mesh;
            public int subMesh;
            public Material material;
            public Matrix4x4 local;
        }

        struct CellKey : System.IEquatable<CellKey>
        {
            public int x, z;
            public Material material;
            public bool shadow;

            public bool Equals(CellKey o) { return x == o.x && z == o.z && material == o.material && shadow == o.shadow; }
            public override bool Equals(object o) { return o is CellKey && Equals((CellKey)o); }
            public override int GetHashCode()
            {
                return ((x * 397) ^ z) * 397 ^ material.GetHashCode() ^ (shadow ? 1 : 0);
            }
        }

        static Dictionary<string, Material> _materials;
        static HashSet<string> _missing;

        // ==================================================================
        //  Point d'entree
        // ==================================================================

        [MenuItem("Nang Tani/2 - Habiller la scene (nouveaux assets)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Info("Quitte d'abord le mode Play.");
                return;
            }

            var layoutAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            if (layoutAsset == null || !File.Exists(SourceScene))
            {
                Info("Fichier introuvable :\n" + LayoutPath + "\nou\n" + SourceScene);
                return;
            }
            var layout = JsonUtility.FromJson<Layout>(layoutAsset.text);

            // Tous les modeles doivent etre la avant de toucher a la scene.
            var needed = new HashSet<string> { "Big-Banana-Tree-1", "medium-banana-tree-1" };
            foreach (var h in layout.houses) needed.Add(h.model);
            foreach (var p in layout.items) needed.Add(p.model);
            foreach (var model in needed)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(model)) != null) continue;
                Info("Modele introuvable : " + ModelPath(model));
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(TargetScene) && !EditorUtility.DisplayDialog("Nang Tani",
                    TargetScene + " existe deja.\nLa recreer ? Les retouches faites dans cette scene seront perdues.",
                    "Recreer", "Annuler")) return;

            _missing = new HashSet<string>();
            EnsureFolder(MaterialDir);
            EnsureFolder(GeneratedDir);
            ConfigureTextures();
            ConfigureModels();

            // ---- Copie de la scene -----------------------------------------
            EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            AssetDatabase.DeleteAsset(TargetScene);
            if (!AssetDatabase.CopyAsset(SourceScene, TargetScene))
            {
                Info("Impossible de copier la scene.");
                return;
            }
            var scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);

            // Apres l'ouverture de la scene : Unity peut decharger les assets inutilises en changeant de scene.
            CreateMaterials();

            var env = FindRoot(scene, "Environment (statique)");
            if (env == null)
            {
                Info("Objet 'Environment (statique)' introuvable dans la scene.");
                return;
            }

            ReplaceGreybox(env);
            foreach (var h in layout.houses) BuildHouse(env, h);
            int triangles = BuildVegetation(env, layout.items);
            LimitViewDistance(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            if (_missing.Count > 0)
                Debug.LogWarning("[NangTani] Materiaux sans texture associee : " + string.Join(", ", _missing));

            string report = "Scene creee : " + TargetScene +
                            "\n\nMaisons : " + layout.houses.Length +
                            "\nElements de vegetation et de decor ajoutes ou remplaces : " + (layout.items.Length + 2) +
                            "\nTriangles de vegetation (total scene) : " + triangles.ToString("N0");
            Debug.Log("[NangTani] " + report);

            if (EditorUtility.DisplayDialog("Nang Tani",
                    report + "\n\nUtiliser cette scene pour le build (a la place de NangTani_Main) ?",
                    "Oui", "Non"))
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(TargetScene, true) };
            }
        }

        // ==================================================================
        //  Import : textures, modeles, materiaux
        // ==================================================================

        static string ModelPath(string model)
        {
            return ModelDir + "/" + model + ".fbx";
        }

        static void ConfigureTextures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.filterMode = FilterMode.Point;            // rendu pixelise "Fears to Fathom"
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;                     // evite le scintillement au loin en VR
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        static void ConfigureModels()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;
                importer.isReadable = true;                        // necessaire pour fusionner la foret
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.importCameras = false;
                importer.importLights = false;
                importer.SaveAndReimport();
            }
        }

        static void CreateMaterials()
        {
            _materials = new Dictionary<string, Material>();
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit");

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureDir }))
            {
                var texturePath = AssetDatabase.GUIDToAssetPath(guid);
                var textureName = Path.GetFileNameWithoutExtension(texturePath);
                if (!textureName.StartsWith("T_")) continue;
                var name = textureName.Substring(2);

                var path = MaterialDir + "/" + name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(shader);
                    AssetDatabase.CreateAsset(m, path);
                }
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                m.SetColor("_BaseColor", Color.white);
                m.SetColor("_SpecColor", Color.black);
                m.SetFloat("_Smoothness", 0f);

                // Feuilles, plantes et toitures fines : visibles des deux cotes.
                bool twoSided = name.StartsWith("Plant_") || name == "Banana_Leaf" ||
                                name == "ThaiHouse_Thatch" || name == "ThaiHouse_RoofTiles";
                m.SetFloat("_Cull", twoSided ? (float)CullMode.Off : (float)CullMode.Back);
                m.doubleSidedGI = twoSided;

                EditorUtility.SetDirty(m);
                _materials[name] = m;
            }
        }

        /// <summary>Instancie un modele dans un parent vide (la rotation d'import du FBX est conservee).</summary>
        static GameObject Spawn(string model, string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(model));
            var root = new GameObject(name);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            visual.transform.SetParent(root.transform, false);

            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (_materials.TryGetValue(mats[i].name, out var replacement)) mats[i] = replacement;
                    else _missing.Add(mats[i].name);
                }
                r.sharedMaterials = mats;
            }
            return root;
        }

        // ==================================================================
        //  Remplacement du greybox
        // ==================================================================

        static Transform FindRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go.transform;
            return null;
        }

        static void ReplaceGreybox(Transform env)
        {
            // Les 86 bananiers disperses et les 3 huttes : supprimes, le plan les repose.
            for (int i = env.childCount - 1; i >= 0; i--)
            {
                var child = env.GetChild(i);
                if (child.name == "Bananier" || child.name == "Maison")
                    Object.DestroyImmediate(child.gameObject);
            }

            // Arbre sacre : on garde l'objet parent (declencheur de regard, tissus, rubans).
            var sacred = env.Find("Arbre_Sacre/Bananier_Sacre");
            if (sacred != null)
            {
                var tree = Spawn("Big-Banana-Tree-1", "Bananier_Sacre");
                // Regime de bananes tourne a l'oppose du joueur (+Z).
                float yaw = 0f;
                foreach (var r in tree.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!r.name.EndsWith("_Bananas")) continue;
                    var d = r.bounds.center;
                    yaw = -Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                }
                tree.transform.SetParent(sacred.parent, false);
                tree.transform.SetPositionAndRotation(sacred.position, Quaternion.Euler(0f, yaw, 0f));
                MakeStatic(tree);
                Object.DestroyImmediate(sacred.gameObject);
            }
            else Debug.LogWarning("[NangTani] Bananier_Sacre introuvable.");

            var torn = env.Find("Bananier_Feuille_Arrachee");
            if (torn != null)
            {
                var tree = Spawn("medium-banana-tree-1", "Bananier_Feuille_Arrachee");
                tree.transform.SetParent(env, false);
                tree.transform.SetPositionAndRotation(torn.position, torn.rotation);
                tree.transform.localScale = torn.localScale;
                MakeStatic(tree);
                Object.DestroyImmediate(torn.gameObject);
            }
            else Debug.LogWarning("[NangTani] Bananier_Feuille_Arrachee introuvable.");
        }

        // ==================================================================
        //  Maisons
        // ==================================================================

        /// <summary>Sommets (repere de la maison) des parties dont le nom contient le mot donne.</summary>
        static List<Vector3> Vertices(GameObject house, string word)
        {
            var list = new List<Vector3>();
            foreach (var filter in house.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.name.Contains(word)) continue;
                var toRoot = house.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (var v in filter.sharedMesh.vertices) list.Add(toRoot.MultiplyPoint3x4(v));
            }
            return list;
        }

        static Bounds BoundsOf(List<Vector3> points)
        {
            var b = new Bounds(points.Count > 0 ? points[0] : Vector3.zero, Vector3.zero);
            foreach (var p in points) b.Encapsulate(p);
            return b;
        }

        /// <summary>Cote de la facade : celui ou l'escalier touche le sol.</summary>
        static Vector3 FrontOf(GameObject house)
        {
            var low = Vector3.zero;
            int count = 0;
            foreach (var p in Vertices(house, "Stairs"))
            {
                if (p.y > 0.35f) continue;
                low += p;
                count++;
            }
            if (count == 0) return Vector3.forward;

            var d = low / count - BoundsOf(Vertices(house, "Floor")).center;
            if (Mathf.Abs(d.x) > Mathf.Abs(d.z)) return new Vector3(Mathf.Sign(d.x), 0f, 0f);
            return new Vector3(0f, 0f, Mathf.Sign(d.z));
        }

        static void BuildHouse(Transform env, HouseItem item)
        {
            var house = Spawn(item.model, "Maison");
            var front = FrontOf(house);
            if (item.lit) AddWindow(house, front);

            float frontYaw = Mathf.Atan2(front.x, front.z) * Mathf.Rad2Deg;
            house.transform.SetParent(env, false);
            house.transform.SetPositionAndRotation(new Vector3(item.x, 0f, item.z),
                Quaternion.Euler(0f, item.yaw - frontYaw, 0f));
            house.transform.localScale = Vector3.one * item.scale;
            MakeStatic(house);
        }

        /// <summary>Fenetre eclairee : touche chaude du CDD, sans lumiere reelle.</summary>
        static void AddWindow(GameObject house, Vector3 front)
        {
            var walls = Vertices(house, "Walls");
            var floor = Vertices(house, "Floor");
            var material = AssetDatabase.LoadAssetAtPath<Material>(WindowMaterialPath);
            if (walls.Count == 0 || floor.Count == 0 || material == null) return;

            var right = Vector3.Cross(Vector3.up, front);
            float depth = float.MinValue, halfWidth = 0f;
            foreach (var p in walls)
            {
                depth = Mathf.Max(depth, Vector3.Dot(p, front));
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(p, right)));
            }

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Fenetre";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(house.transform, false);
            quad.transform.localPosition = front * (depth + 0.03f) + right * (halfWidth * 0.45f) +
                                           Vector3.up * (BoundsOf(floor).max.y + 1.1f);
            quad.transform.localRotation = Quaternion.LookRotation(-front, Vector3.up); // un Quad est visible depuis son -Z
            quad.transform.localScale = new Vector3(0.55f, 0.55f, 1f);

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ==================================================================
        //  Vegetation et petits decors, fusionnes par zone
        // ==================================================================

        static List<Part> PartsOf(string model)
        {
            var template = Spawn(model, model);
            var parts = new List<Part>();
            foreach (var filter in template.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null) continue;
                var mats = renderer.sharedMaterials;
                for (int s = 0; s < filter.sharedMesh.subMeshCount && s < mats.Length; s++)
                {
                    parts.Add(new Part
                    {
                        name = filter.name,
                        mesh = filter.sharedMesh,
                        subMesh = s,
                        material = mats[s],
                        local = template.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix
                    });
                }
            }
            Object.DestroyImmediate(template);
            return parts;
        }

        static int BuildVegetation(Transform env, PlantItem[] items)
        {
            var cache = new Dictionary<string, List<Part>>();
            var cells = new Dictionary<CellKey, List<CombineInstance>>();
            long indices = 0;

            foreach (var item in items)
            {
                if (!cache.TryGetValue(item.model, out var parts))
                    cache[item.model] = parts = PartsOf(item.model);

                var world = Matrix4x4.TRS(new Vector3(item.x, item.y, item.z),
                    Quaternion.Euler(0f, item.yaw, 0f) * Quaternion.Euler(item.tilt, 0f, 0f),
                    Vector3.one * item.scale);

                foreach (var part in parts)
                {
                    // Version allegee des bananiers lointains : sans fruits ni tige du regime.
                    if (item.lite && (part.name.EndsWith("_Bananas") || part.name.EndsWith("_Stalk"))) continue;

                    var key = new CellKey
                    {
                        x = Mathf.FloorToInt(item.x / CellSize),
                        z = Mathf.FloorToInt(item.z / CellSize),
                        material = part.material,
                        shadow = item.shadow
                    };
                    if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance
                    {
                        mesh = part.mesh,
                        subMeshIndex = part.subMesh,
                        transform = world * part.local
                    });
                    indices += part.mesh.GetIndexCount(part.subMesh);
                }
            }

            var parent = new GameObject("Decor_Vegetation").transform;
            parent.SetParent(env, false);

            AssetDatabase.DeleteAsset(MeshPath);
            bool assetCreated = false;
            foreach (var cell in cells)
            {
                long vertexCount = 0;
                foreach (var c in cell.Value) vertexCount += c.mesh.vertexCount;

                var name = "Zone_" + cell.Key.x + "_" + cell.Key.z + "_" + cell.Key.material.name +
                           (cell.Key.shadow ? "_ombre" : "");
                var mesh = new Mesh { name = name };
                if (vertexCount > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.CombineMeshes(cell.Value.ToArray(), true, true);

                if (!assetCreated)
                {
                    AssetDatabase.CreateAsset(mesh, MeshPath);
                    assetCreated = true;
                }
                else AssetDatabase.AddObjectToAsset(mesh, MeshPath);

                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = cell.Key.material;
                renderer.shadowCastingMode = cell.Key.shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            return (int)(indices / 3);
        }

        /// <summary>
        /// Reconstruit la vegetation en posant chaque element a la hauteur donnee (utilise par le menu du sol).
        /// heightFor(modele, x, z) renvoie la hauteur du terrain, ou null pour retirer l'element.
        /// Repart toujours du plan d'origine : on peut l'appeler plusieurs fois sans cumuler les decalages.
        /// </summary>
        internal static bool RebuildVegetation(Transform env, System.Func<string, float, float, float?> heightFor)
        {
            var layoutAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            if (layoutAsset == null) return false;
            var layout = JsonUtility.FromJson<Layout>(layoutAsset.text);

            // Materiaux deja crees par le menu 2 : charges tels quels (les retouches sont conservees).
            _missing = new HashSet<string>();
            _materials = new Dictionary<string, Material>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialDir }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m != null) _materials[m.name] = m;
            }
            if (_materials.Count == 0) return false;

            var kept = new List<PlantItem>();
            foreach (var item in layout.items)
            {
                var ground = heightFor(item.model, item.x, item.z);
                if (!ground.HasValue) continue;
                item.y += ground.Value;
                kept.Add(item);
            }

            var old = env.Find("Decor_Vegetation");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            BuildVegetation(env, kept.ToArray());
            return true;
        }

        // ==================================================================
        //  Divers
        // ==================================================================

        static void LimitViewDistance(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                {
                    if (!cam.CompareTag("MainCamera")) continue;
                    cam.farClipPlane = FarClip;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(cam);
                }
            }
        }

        static void MakeStatic(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        static void Info(string message)
        {
            EditorUtility.DisplayDialog("Nang Tani", message, "OK");
        }
    }
}
