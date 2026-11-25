using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NangTani.EditorTools
{
    /// <summary>
    /// Menu "Nang Tani > 1 - Construire la scene greybox".
    /// Genere la scene complete (village, arbre sacre, clairiere du temple, personnages en capsules,
    /// joueur assis) et branche tous les scripts. On peut le relancer a volonte : il recree la scene
    /// Assets/Scenes/NangTani_Main.unity a partir de zero. Si tu retouches la scene a la main,
    /// duplique-la d'abord (sinon elle sera ecrasee).
    /// </summary>
    public static class NangTaniGreyboxBuilder
    {
        const string Root = "Assets/NangTani";
        const string MatDir = Root + "/Materials";
        const string TexDir = Root + "/Textures";
        const string ScenePath = "Assets/Scenes/NangTani_Main.unity";
        const string RigPrefabPath = "Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab";

        // Centre de la clairiere (le village est a l'origine, la clairiere 60 m plus loin : jamais visible depuis le village).
        static readonly Vector3 Clearing = new Vector3(60f, 0f, 0f);

        static Dictionary<string, Material> _materials;
        static System.Random _rng;

        // ==================================================================
        //  Point d'entree
        // ==================================================================

        [MenuItem("Nang Tani/1 - Construire la scene greybox")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            if (rigPrefab == null)
            {
                EditorUtility.DisplayDialog("Nang Tani",
                    "Prefab du joueur introuvable :\n" + RigPrefabPath +
                    "\n\nLe template VR a-t-il ete modifie ou deplace ?", "OK");
                return;
            }

            EnsureFolders();
            _materials = new Dictionary<string, Material>();
            _rng = new System.Random(20260211);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var env = new GameObject("Environment (statique)").transform;
            var props = new GameObject("Props (dynamiques)").transform;
            var points = new GameObject("Reperes").transform;
            var audio = new GameObject("Audio").transform;

            // ---- Lumiere, brouillard -------------------------------------
            var calm = AtmosphereState.Calm;
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = calm.fogColor;
            RenderSettings.fogDensity = calm.fogDensity;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = calm.ambient;

            var moonGo = new GameObject("Moon");
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = calm.moonColor;
            moon.intensity = calm.moonIntensity;
            moon.shadows = LightShadows.Soft;
            moonGo.transform.rotation = Quaternion.Euler(calm.moonPitch, -30f, 0f);

            // ---- Joueur assis ----------------------------------------------
            var rigGo = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rigGo.name = "XR Origin (joueur assis)";
            var origin = rigGo.GetComponentInChildren<XROrigin>(true);
            if (origin == null || origin.Camera == null)
            {
                EditorUtility.DisplayDialog("Nang Tani",
                    "Le prefab du joueur ne contient pas de XR Origin avec camera. Abandon.", "OK");
                return;
            }

            var seated = rigGo.AddComponent<SeatedRig>();
            seated.origin = origin;

            var cam = origin.Camera;
            cam.gameObject.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = calm.fogColor;
            cam.farClipPlane = 150f;
            var camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData != null) camData.renderPostProcessing = true;

            var fader = rigGo.AddComponent<ScreenFader>();
            fader.overlay = CreateFadeOverlay(cam.transform);

            // ---- Sous-titres -----------------------------------------------
            var subtitles = CreateSubtitles();

            // ---- Reperes ---------------------------------------------------
            var anchorVillage = Point(points, "Ancre_Village", Vector3.zero, 0f);
            var anchorClearing = Point(points, "Ancre_Clairiere", Clearing, 0f);

            var treePos = new Vector3(0f, 0f, 7.5f);
            var ramVillageSeat = Point(points, "Siege_Ram_Village", new Vector3(1.2f, 0.45f, 0.1f), -9f);
            var mingVillageSeat = Point(points, "Siege_Ming_Village", new Vector3(-1.2f, 0.45f, 0.1f), 9f);
            var ramLeafPickup = Point(points, "Ram_Prend_Feuille", new Vector3(6.2f, 0f, 4.8f), 0f);
            var ramVillageExit = Point(points, "Ram_Sortie_Village", new Vector3(22f, 0f, 3.2f), 0f);
            var voiceVillage = Point(points, "Voix_Village", new Vector3(13f, 1.6f, -8f), 0f);

            var ramClearingSeat = Point(points, "Siege_Ram_Clairiere", Clearing + new Vector3(0.9f, 0f, 2.8f), 0f);
            var mingClearingSeat = Point(points, "Siege_Ming_Clairiere", Clearing + new Vector3(-1.2f, 0.5f, 0.1f), 0f);
            var spiritSpawn = Point(points, "NangTani_Apparition", Clearing + new Vector3(6f, 0f, 16f), 0f);
            var spiritStop = Point(points, "NangTani_Arret", Clearing + new Vector3(1.1f, 0f, 5.8f), 0f);
            var offeringsPoint = Point(points, "Offrandes", Clearing + new Vector3(0.95f, 0f, 4.5f), 0f);

            var ramEpilogue = Point(points, "Epilogue_Ram", new Vector3(-1.0f, 0f, 5.2f), 23f);
            var mingEpilogue = Point(points, "Epilogue_Ming", new Vector3(1.4f, 0f, 5.2f), -31f);

            // ---- Sol -------------------------------------------------------
            Prim(PrimitiveType.Plane, "Sol", env, new Vector3(30f, 0f, 0f), new Vector3(40f, 1f, 40f),
                Lit("Sol", Palette.ForestGreen * 0.75f, 0.02f));
            Prim(PrimitiveType.Cube, "Chemin_Village", env, new Vector3(18f, 0.012f, 3.2f), new Vector3(30f, 0.02f, 2.2f),
                Lit("Terre", new Color(0.20f, 0.17f, 0.13f), 0.02f));

            // ---- Village ---------------------------------------------------
            Prim(PrimitiveType.Cube, "Banc", env, new Vector3(0f, 0.225f, 0.1f), new Vector3(4.2f, 0.45f, 0.55f),
                Lit("Bois", new Color(0.20f, 0.15f, 0.11f)));

            var sacredTree = BuildSacredTree(env, treePos);
            var gazeAim = new GameObject("Point_Regard").transform;
            gazeAim.SetParent(sacredTree.transform, false);
            gazeAim.localPosition = new Vector3(0f, 2.0f, 0f);
            var gaze = sacredTree.AddComponent<GazeDwellTarget>();
            gaze.aimPoint = gazeAim;
            gaze.requiredSeconds = 2f;
            gaze.maxAngleDegrees = 9f;

            BuildOfferingsAtTree(props, treePos);

            var greenCloth = Prim(PrimitiveType.Cylinder, "Tissu_Vert_Epilogue", props,
                treePos + new Vector3(0f, 2.55f, 0f), new Vector3(0.52f, 0.07f, 0.52f),
                Lit("TissuVert", Palette.SpiritGreen * 0.8f));
            greenCloth.SetActive(false);

            var leafAtTree = Prim(PrimitiveType.Cube, "Feuille_Pied_Arbre", props,
                treePos + new Vector3(0.55f, 0.04f, -0.9f), new Vector3(0.5f, 0.02f, 1.2f),
                Lit("Feuille", new Color(0.18f, 0.36f, 0.22f)));
            leafAtTree.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
            leafAtTree.SetActive(false);

            Hut(env, new Vector3(-11f, 0f, 9f), 100f);
            Hut(env, new Vector3(12f, 0f, 16f), -110f);
            Hut(env, new Vector3(-16f, 0f, -4f), 70f);

            // Villageois rassembles autour de l'arbre (cote et arriere, pour ne pas masquer l'arbre)
            float[] villagerAngles = { 0f, 28f, 62f, 118f, 152f, 180f };
            for (int i = 0; i < villagerAngles.Length; i++)
            {
                float a = villagerAngles[i] * Mathf.Deg2Rad;
                Vector3 p = treePos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 3.3f;
                float yaw = Mathf.Atan2(treePos.x - p.x, treePos.z - p.z) * Mathf.Rad2Deg;
                Color cloth = Color.Lerp(new Color(0.22f, 0.18f, 0.16f), new Color(0.16f, 0.22f, 0.26f), (i % 3) / 2f);
                Person(props, "Villageois_" + (i + 1), p, yaw, cloth, 1.55f + 0.08f * (i % 3), false);
            }

            // Banane sacree "voisine" dont Ram arrache la feuille
            var tearTree = BananaTree(env, new Vector3(7.0f, 0f, 5.6f), 1.15f, 20f);
            tearTree.name = "Bananier_Feuille_Arrachee";
            var leafOnTree = Prim(PrimitiveType.Cube, "Feuille_Sur_Arbre", props, new Vector3(6.55f, 2.45f, 5.25f),
                new Vector3(0.62f, 0.03f, 2.1f), Lit("FeuilleClaire", new Color(0.24f, 0.46f, 0.28f)));
            leafOnTree.transform.localRotation = Quaternion.Euler(35f, 215f, 0f);

            // ---- Clairiere -------------------------------------------------
            var log = Prim(PrimitiveType.Cylinder, "Tronc_Siege", env, Clearing + new Vector3(0f, 0.25f, 0.1f),
                new Vector3(0.5f, 1.6f, 0.5f), Lit("BoisTronc", new Color(0.17f, 0.13f, 0.10f)));
            log.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            Temple(env, Clearing + new Vector3(-8.5f, 0f, 11f), -38f);
            Chedi(env, Clearing + new Vector3(-17f, 0f, 2f));

            var leafOnGround = Prim(PrimitiveType.Cube, "Feuille_Au_Sol", props, ramClearingSeat.position + new Vector3(0.6f, 0.03f, -0.2f),
                new Vector3(0.6f, 0.02f, 1.4f), Lit("FeuilleClaire", new Color(0.24f, 0.46f, 0.28f)));
            leafOnGround.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
            leafOnGround.SetActive(false);

            // Bananiers de part et d'autre du chemin de Nang Tani ("entre les arbres")
            Vector3 sA = spiritSpawn.position, sB = spiritStop.position;
            Vector3 along = (sB - sA).normalized;
            Vector3 side = new Vector3(-along.z, 0f, along.x);
            float[] ts = { 0.2f, 0.4f, 0.6f, 0.8f };
            for (int i = 0; i < ts.Length; i++)
            {
                Vector3 c = Vector3.Lerp(sA, sB, ts[i]);
                BananaTree(env, c + side * Rf(2.5f, 3.2f), Rf(0.95f, 1.3f), Rf(0, 360f));
                BananaTree(env, c - side * Rf(2.5f, 3.2f), Rf(0.95f, 1.3f), Rf(0, 360f));
            }

            // ---- Forets (bananiers disperses) ------------------------------
            ScatterBananas(env, Vector3.zero, 38, 7f, 34f, p =>
                Flat(p, treePos) < 5f ||
                (p.z > 1.0f && p.z < 6.0f && p.x > 2f && p.x < 28f) ||
                Flat(p, ramLeafPickup.position) < 2.5f ||
                Flat(p, new Vector3(-11f, 0f, 9f)) < 4f || Flat(p, new Vector3(12f, 0f, 16f)) < 4f ||
                Flat(p, new Vector3(-16f, 0f, -4f)) < 4f);

            ScatterBananas(env, Clearing + new Vector3(0f, 0f, 8f), 40, 6f, 32f, p =>
                DistToSegment(p, sA, sB) < 3.4f ||
                Flat(p, ramClearingSeat.position) < 3f || Flat(p, Clearing) < 4f ||
                Flat(p, Clearing + new Vector3(-8.5f, 0f, 11f)) < 8f ||
                Flat(p, Clearing + new Vector3(-17f, 0f, 2f)) < 4f);

            MakeStatic(env.gameObject);

            // ---- Personnages -----------------------------------------------
            var ram = Person(props, "Ram", ramVillageSeat.position, -9f, new Color(0.35f, 0.22f, 0.15f), 1.75f, true);
            var ming = Person(props, "Ming", mingVillageSeat.position, 9f, new Color(0.16f, 0.27f, 0.38f), 1.65f, true);

            var ramLeaf = Prim(PrimitiveType.Cube, "Feuille_Dans_La_Main", ram.transform, new Vector3(0.28f, 0.95f, 0.32f),
                new Vector3(0.45f, 0.02f, 1.1f), Lit("FeuilleClaire", new Color(0.24f, 0.46f, 0.28f)));
            ramLeaf.transform.localRotation = Quaternion.Euler(20f, 15f, 0f);
            ramLeaf.SetActive(false);

            var spirit = BuildSpirit(props);

            // ---- Offrandes du joueur (clairiere) -----------------------------
            var offerings = BuildPlayerOfferings(props, offeringsPoint.position);

            // ---- Audio -----------------------------------------------------
            var wind = AudioSrc("Vent", audio, false);
            var crickets = AudioSrc("Insectes", audio, false);
            var voice = AudioSrc("Voix_NangTani", voiceVillage, true);
            voice.loop = false;

            var turn = voiceVillage.gameObject.AddComponent<HeadTurnTrigger>();
            turn.aimPoint = voiceVillage;
            turn.requiredSeconds = 0.35f;
            turn.maxAngleDegrees = 28f;

            // ---- Systemes + chef d'orchestre ---------------------------------
            var systems = new GameObject("Experience");
            var atmos = systems.AddComponent<AtmosphereController>();
            atmos.moon = moon;
            atmos.wind = wind;
            atmos.crickets = crickets;
            var tap = systems.AddComponent<TapInput>();
            var spawner = systems.AddComponent<OfferingSpawner>();
            spawner.offeringsRoot = offerings.root;
            spawner.smoke = offerings.smoke;
            spawner.emberLight = offerings.ember;
            var director = systems.AddComponent<ExperienceDirector>();

            director.rig = seated;
            director.fader = fader;
            director.subtitles = subtitles;
            director.atmosphere = atmos;
            director.tapInput = tap;
            director.anchorVillage = anchorVillage;
            director.anchorClearing = anchorClearing;
            director.sacredTreeGaze = gaze;
            director.voiceTurn = turn;
            director.ram = ram;
            director.ming = ming;
            director.spirit = spirit;
            director.offerings = spawner;
            director.ramLeafInHand = ramLeaf;
            director.leafOnTree = leafOnTree;
            director.leafOnGround = leafOnGround;
            director.leafAtSacredTree = leafAtTree;
            director.greenClothOnTree = greenCloth;
            director.ramVillageSeat = ramVillageSeat;
            director.mingVillageSeat = mingVillageSeat;
            director.ramLeafPickup = ramLeafPickup;
            director.ramVillageExit = ramVillageExit;
            director.voiceVillagePoint = voiceVillage;
            director.ramClearingSeat = ramClearingSeat;
            director.mingClearingSeat = mingClearingSeat;
            director.spiritSpawn = spiritSpawn;
            director.spiritStop = spiritStop;
            director.ramEpilogueSpot = ramEpilogue;
            director.mingEpilogueSpot = mingEpilogue;
            director.voiceSource = voice;

            // ---- Sauvegarde ------------------------------------------------
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            Selection.activeGameObject = systems;
            EditorUtility.DisplayDialog("Nang Tani",
                "Scene greybox generee :\n" + ScenePath +
                "\n\nAppuie sur Play. Touches : 1-6 = aller a l'etape, R = recommencer, " +
                "clic droit + souris = regarder, Espace = gachette.", "OK");
        }

        // ==================================================================
        //  Dossiers, materiaux, textures
        // ==================================================================

        static void EnsureFolders()
        {
            foreach (var path in new[] { Root, MatDir, TexDir, "Assets/Scenes" })
            {
                if (AssetDatabase.IsValidFolder(path)) continue;
                int slash = path.LastIndexOf('/');
                AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
            }
        }

        static Material Lit(string name, Color color, float smoothness = 0.08f)
        {
            string key = "L_" + name;
            if (_materials.TryGetValue(key, out var cached)) return cached;

            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            _materials[key] = m;
            return m;
        }

        static Material Unlit(string name, Color color)
        {
            string key = "U_" + name;
            if (_materials.TryGetValue(key, out var cached)) return cached;

            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            _materials[key] = m;
            return m;
        }

        static Material FadeMaterial()
        {
            var m = Unlit("FadeOverlay", Color.black);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Overlay;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material SmokeMaterial()
        {
            string path = MatDir + "/FumeeEncens.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.mainTexture = SoftCircle();
            EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D SoftCircle()
        {
            string path = TexDir + "/SoftCircle.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f;
                    float dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ==================================================================
        //  Helpers de construction
        // ==================================================================

        static float Rf(float a, float b)
        {
            return a + (float)_rng.NextDouble() * (b - a);
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            return go;
        }

        static Transform Point(Transform parent, string name, Vector3 pos, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }

        static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        static float DistToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            p.y = 0f; a.y = 0f; b.y = 0f;
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
            return Vector3.Distance(p, a + ab * t);
        }

        static void MakeStatic(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        }

        static AudioSource AudioSrc(string name, Transform parent, bool spatial)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.loop = true;
            a.spatialBlend = spatial ? 1f : 0f;
            if (spatial)
            {
                a.rolloffMode = AudioRolloffMode.Linear;
                a.minDistance = 3f;
                a.maxDistance = 45f;
            }
            return a;
        }

        // ==================================================================
        //  Fondu et sous-titres
        // ==================================================================

        static Renderer CreateFadeOverlay(Transform camera)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Fondu_Noir";
            var col = quad.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            quad.transform.SetParent(camera, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.25f);
            quad.transform.localScale = new Vector3(3f, 3f, 1f);

            var mr = quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial = FadeMaterial();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mr;
        }

        static SubtitleDisplay CreateSubtitles()
        {
            var root = new GameObject("Sous-titres");
            var display = root.AddComponent<SubtitleDisplay>();

            var textGo = new GameObject("Texte");
            textGo.transform.SetParent(root.transform, false);
            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.text = "";
            tmp.fontSize = 0.8f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.color = Palette.MoonWhite;
            tmp.rectTransform.sizeDelta = new Vector2(1.9f, 0.6f);

            display.text = tmp;
            return display;
        }

        // ==================================================================
        //  Vegetation : bananiers
        // ==================================================================

        static GameObject BananaTree(Transform parent, Vector3 pos, float scale, float yaw)
        {
            var root = new GameObject("Bananier");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            root.transform.localScale = Vector3.one * scale;

            const float h = 2.6f;
            Prim(PrimitiveType.Cylinder, "Tronc", root.transform, new Vector3(0f, h * 0.5f, 0f),
                new Vector3(0.28f, h * 0.5f, 0.28f), Lit("BananierTronc", new Color(0.20f, 0.27f, 0.18f)));

            var leafMat = Lit("BananierFeuille", new Color(0.13f, 0.30f, 0.19f));
            for (int i = 0; i < 9; i++)
            {
                bool upright = i >= 7;
                float yawA = i * (360f / 7f) + Rf(-12f, 12f);
                float pitch = upright ? Rf(-70f, -50f) : Rf(25f, 60f);
                float len = upright ? Rf(1.3f, 1.8f) : Rf(1.7f, 2.5f);

                var pivot = new GameObject("Feuille" + i).transform;
                pivot.SetParent(root.transform, false);
                pivot.localPosition = new Vector3(0f, h, 0f);
                pivot.localRotation = Quaternion.Euler(0f, yawA, 0f) * Quaternion.Euler(pitch, 0f, 0f);
                Prim(PrimitiveType.Cube, "Limbe", pivot, new Vector3(0f, 0f, len * 0.5f),
                    new Vector3(0.55f, 0.02f, len), leafMat);
            }
            return root;
        }

        static void ScatterBananas(Transform parent, Vector3 center, int count, float rMin, float rMax,
            System.Func<Vector3, bool> excluded)
        {
            int placed = 0, attempts = 0;
            while (placed < count && attempts < count * 12)
            {
                attempts++;
                float a = Rf(0f, Mathf.PI * 2f);
                float r = Rf(rMin, rMax);
                Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (excluded(p)) continue;
                BananaTree(parent, p, Rf(0.85f, 1.35f), Rf(0f, 360f));
                placed++;
            }
        }

        // ==================================================================
        //  Arbre sacre et offrandes du village
        // ==================================================================

        static GameObject BuildSacredTree(Transform parent, Vector3 pos)
        {
            var root = new GameObject("Arbre_Sacre");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;

            var tree = BananaTree(root.transform, pos, 1.6f, 0f);
            tree.name = "Bananier_Sacre";

            Color[] cloths =
            {
                new Color(0.65f, 0.23f, 0.20f), new Color(0.85f, 0.63f, 0.24f), new Color(0.71f, 0.35f, 0.54f),
                new Color(0.23f, 0.44f, 0.66f), new Color(0.24f, 0.54f, 0.35f)
            };

            for (int i = 0; i < 4; i++)
            {
                Prim(PrimitiveType.Cylinder, "Tissu" + i, root.transform, new Vector3(0f, 1.0f + 0.38f * i, 0f),
                    new Vector3(0.52f, 0.07f, 0.52f), Lit("Tissu" + i, cloths[i]));
            }

            // Rubans qui pendent
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                var strip = Prim(PrimitiveType.Cube, "Ruban" + i, root.transform,
                    new Vector3(Mathf.Cos(a) * 0.27f, 1.75f, Mathf.Sin(a) * 0.27f),
                    new Vector3(0.05f, 0.55f, 0.012f), Lit("Tissu" + (i % 5), cloths[i % 5]));
                strip.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
            }
            return root;
        }

        static CandleFlame Candle(Transform parent, Vector3 pos)
        {
            var root = new GameObject("Bougie").transform;
            root.SetParent(parent, false);
            root.position = pos;

            Prim(PrimitiveType.Cylinder, "Cire", root, new Vector3(0f, 0.09f, 0f), new Vector3(0.05f, 0.09f, 0.05f),
                Lit("Cire", new Color(0.78f, 0.72f, 0.60f)));
            var flame = Prim(PrimitiveType.Sphere, "Flamme", root, new Vector3(0f, 0.2f, 0f),
                new Vector3(0.03f, 0.05f, 0.03f), Unlit("Flamme", Palette.CandleWarm));

            var lightGo = new GameObject("Lumiere");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.26f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = Palette.CandleWarm;
            l.range = 3.2f;
            l.intensity = 2f;
            l.shadows = LightShadows.None;

            var candle = root.gameObject.AddComponent<CandleFlame>();
            candle.flameLight = l;
            candle.flameRenderer = flame.GetComponent<Renderer>();
            candle.baseIntensity = 2f;
            return candle;
        }

        static void BuildOfferingsAtTree(Transform parent, Vector3 treePos)
        {
            // 6 bougies sur l'arc tourne vers le joueur
            for (int i = 0; i < 6; i++)
            {
                float a = (205f + i * 26f) * Mathf.Deg2Rad;
                Candle(parent, treePos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.0f);
            }

            // Encens
            for (int i = 0; i < 3; i++)
            {
                float a = (235f + i * 35f) * Mathf.Deg2Rad;
                Vector3 p = treePos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.72f;
                Prim(PrimitiveType.Cylinder, "Encens", parent, p + Vector3.up * 0.14f,
                    new Vector3(0.012f, 0.14f, 0.012f), Lit("Encens", new Color(0.30f, 0.12f, 0.08f)));
                Prim(PrimitiveType.Sphere, "BraiseEncens", parent, p + Vector3.up * 0.29f,
                    new Vector3(0.025f, 0.025f, 0.025f), Unlit("Braise", new Color(1f, 0.35f, 0.12f)));
            }

            // Plateau de feuille de bananier + fleurs
            Prim(PrimitiveType.Cylinder, "Plateau", parent, treePos + new Vector3(0f, 0.01f, -0.75f),
                new Vector3(0.9f, 0.01f, 0.9f), Lit("Feuille", new Color(0.18f, 0.36f, 0.22f)));
            Color[] petals =
            {
                new Color(0.88f, 0.63f, 0.19f), new Color(0.90f, 0.90f, 0.85f), new Color(0.80f, 0.35f, 0.50f)
            };
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                Vector3 p = treePos + new Vector3(Mathf.Cos(a) * 0.28f, 0.05f, -0.75f + Mathf.Sin(a) * 0.28f);
                Prim(PrimitiveType.Sphere, "Fleur", parent, p, Vector3.one * 0.09f, Lit("Fleur" + (i % 3), petals[i % 3]));
            }

            // Bol de nourriture
            Prim(PrimitiveType.Cylinder, "Bol", parent, treePos + new Vector3(0.95f, 0.05f, -0.45f),
                new Vector3(0.28f, 0.05f, 0.28f), Lit("Bol", new Color(0.35f, 0.25f, 0.18f)));
            Prim(PrimitiveType.Sphere, "Riz", parent, treePos + new Vector3(0.95f, 0.1f, -0.45f),
                new Vector3(0.22f, 0.12f, 0.22f), Lit("Riz", new Color(0.80f, 0.78f, 0.70f)));
        }

        // ==================================================================
        //  Batiments : maison sur pilotis, temple abandonne, chedi
        // ==================================================================

        static void Hut(Transform parent, Vector3 pos, float yaw)
        {
            var root = new GameObject("Maison").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

            var wood = Lit("Bois", new Color(0.20f, 0.15f, 0.11f));
            var dark = Lit("BoisSombre", new Color(0.13f, 0.10f, 0.08f));
            var roof = Lit("Toit", new Color(0.16f, 0.13f, 0.12f));

            foreach (float sx in new[] { -1.4f, 1.4f })
                foreach (float sz in new[] { -1.1f, 1.1f })
                    Prim(PrimitiveType.Cylinder, "Pilotis", root, new Vector3(sx, 0.9f, sz), new Vector3(0.2f, 0.9f, 0.2f), dark);

            Prim(PrimitiveType.Cube, "Plancher", root, new Vector3(0f, 1.85f, 0f), new Vector3(3.6f, 0.14f, 3.0f), wood);
            Prim(PrimitiveType.Cube, "Murs", root, new Vector3(0f, 2.75f, 0f), new Vector3(3.2f, 1.7f, 2.6f), wood);

            var left = Prim(PrimitiveType.Cube, "ToitG", root, new Vector3(-1.05f, 4.05f, 0f), new Vector3(2.4f, 0.1f, 3.8f), roof);
            left.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
            var right = Prim(PrimitiveType.Cube, "ToitD", root, new Vector3(1.05f, 4.05f, 0f), new Vector3(2.4f, 0.1f, 3.8f), roof);
            right.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);

            // Fenetres eclairees : les "touches chaudes" du CDD, sans lumiere reelle (cout nul sur Quest)
            Prim(PrimitiveType.Quad, "Fenetre", root, new Vector3(0.7f, 2.8f, -1.31f), new Vector3(0.55f, 0.55f, 1f),
                Unlit("FenetreChaude", Palette.CandleWarm * 0.8f));
        }

        static void Temple(Transform parent, Vector3 pos, float yaw)
        {
            var root = new GameObject("Temple_Abandonne").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

            var stone = Lit("PierreTemple", new Color(0.30f, 0.33f, 0.34f));
            var dark = Lit("ToitTemple", new Color(0.16f, 0.18f, 0.19f));
            var aged = Lit("StatueTemple", new Color(0.33f, 0.27f, 0.16f));

            Prim(PrimitiveType.Cube, "Plateforme", root, new Vector3(0f, 0.3f, 0f), new Vector3(9f, 0.6f, 6.5f), stone);
            Prim(PrimitiveType.Cube, "Marches", root, new Vector3(0f, 0.15f, -3.9f), new Vector3(3.2f, 0.3f, 1.3f), stone);

            float[] xs = { -3.6f, -1.2f, 1.2f, 3.6f };
            foreach (float z in new[] { -2.4f, 2.4f })
            {
                foreach (float x in xs)
                {
                    bool broken = z > 0f && x > 3f;
                    float ph = broken ? 1.5f : 3.4f;
                    Prim(PrimitiveType.Cylinder, broken ? "Pilier_Brise" : "Pilier", root,
                        new Vector3(x, 0.6f + ph * 0.5f, z), new Vector3(0.5f, ph * 0.5f, 0.5f), stone);
                }
            }

            Prim(PrimitiveType.Cube, "Toit", root, new Vector3(0f, 4.3f, 0f), new Vector3(10f, 0.35f, 7.5f), dark);
            Prim(PrimitiveType.Cube, "ToitHaut", root, new Vector3(0f, 4.85f, 0f), new Vector3(7f, 0.35f, 5.2f), dark);
            Prim(PrimitiveType.Cube, "ToitSommet", root, new Vector3(0f, 5.35f, 0f), new Vector3(3.5f, 0.35f, 2.6f), dark);
            Prim(PrimitiveType.Cube, "MurFond", root, new Vector3(0f, 2.2f, 3.0f), new Vector3(8.4f, 3.2f, 0.4f), stone);

            // Statue assise (silhouette)
            Prim(PrimitiveType.Cube, "SocleStatue", root, new Vector3(0f, 0.85f, 2.3f), new Vector3(1.8f, 0.5f, 1.4f), stone);
            Prim(PrimitiveType.Sphere, "CorpsStatue", root, new Vector3(0f, 1.7f, 2.3f), new Vector3(1.5f, 1.4f, 1.0f), aged);
            Prim(PrimitiveType.Sphere, "TeteStatue", root, new Vector3(0f, 2.65f, 2.3f), Vector3.one * 0.55f, aged);
        }

        static void Chedi(Transform parent, Vector3 pos)
        {
            var root = new GameObject("Chedi").transform;
            root.SetParent(parent, false);
            root.position = pos;

            var stone = Lit("PierreTemple", new Color(0.30f, 0.33f, 0.34f));
            Prim(PrimitiveType.Cube, "Base", root, new Vector3(0f, 0.3f, 0f), new Vector3(4f, 0.6f, 4f), stone);
            Prim(PrimitiveType.Cylinder, "Etage1", root, new Vector3(0f, 1.0f, 0f), new Vector3(3.0f, 0.4f, 3.0f), stone);
            Prim(PrimitiveType.Cylinder, "Etage2", root, new Vector3(0f, 1.8f, 0f), new Vector3(2.3f, 0.4f, 2.3f), stone);
            Prim(PrimitiveType.Sphere, "Cloche", root, new Vector3(0f, 3.1f, 0f), new Vector3(2.0f, 2.2f, 2.0f), stone);
            Prim(PrimitiveType.Cylinder, "Col", root, new Vector3(0f, 4.4f, 0f), new Vector3(0.8f, 0.4f, 0.8f), stone);
            Prim(PrimitiveType.Cylinder, "Fleche1", root, new Vector3(0f, 5.4f, 0f), new Vector3(0.5f, 0.9f, 0.5f), stone);
            Prim(PrimitiveType.Cylinder, "Fleche2", root, new Vector3(0f, 7.0f, 0f), new Vector3(0.2f, 0.8f, 0.2f), stone);
        }

        // ==================================================================
        //  Personnages (silhouettes greybox)
        // ==================================================================

        static CharacterActor Person(Transform parent, string name, Vector3 pos, float yaw, Color shirt, float height, bool actor)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            float bodyH = height - 0.3f;
            Prim(PrimitiveType.Capsule, "Corps", visual, new Vector3(0f, bodyH * 0.5f, 0f),
                new Vector3(0.42f, bodyH * 0.5f, 0.42f), Lit("Vetement_" + ColorUtility.ToHtmlStringRGB(shirt), shirt));
            Prim(PrimitiveType.Sphere, "Tete", visual, new Vector3(0f, height - 0.14f, 0f), Vector3.one * 0.24f,
                Lit("Peau", new Color(0.36f, 0.26f, 0.20f)));

            if (!actor) return null;

            var a = root.AddComponent<CharacterActor>();
            a.visual = visual;
            a.groundY = 0f;
            return a;
        }

        static SpiritController BuildSpirit(Transform parent)
        {
            var root = new GameObject("Nang_Tani");
            root.transform.SetParent(parent, false);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            // Robe : le seul vert sature de la scene (CDD). Visage et cheveux : presque noirs (dans l'ombre).
            Prim(PrimitiveType.Capsule, "Robe", visual, new Vector3(0f, 0.8f, 0f), new Vector3(0.52f, 0.8f, 0.52f),
                Unlit("EspritRobe", Palette.SpiritGreen * 1.1f));
            Prim(PrimitiveType.Sphere, "Visage", visual, new Vector3(0f, 1.72f, 0f), Vector3.one * 0.26f,
                Unlit("EspritVisage", new Color(0.01f, 0.015f, 0.015f)));
            Prim(PrimitiveType.Capsule, "Cheveux", visual, new Vector3(0f, 1.3f, -0.14f), new Vector3(0.3f, 0.55f, 0.18f),
                Unlit("EspritCheveux", new Color(0.02f, 0.03f, 0.03f)));

            var lightGo = new GameObject("Lueur");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.2f, 0.3f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = Palette.SpiritGreen;
            l.range = 4f;
            l.intensity = 0f;
            l.shadows = LightShadows.None;

            var spirit = root.AddComponent<SpiritController>();
            spirit.renderers = root.GetComponentsInChildren<Renderer>(true);
            spirit.glow = l;
            spirit.glowIntensity = 1.1f;

            root.SetActive(false);
            return spirit;
        }

        // ==================================================================
        //  Offrandes du joueur (clairiere)
        // ==================================================================

        struct OfferingParts
        {
            public GameObject root;
            public ParticleSystem smoke;
            public Light ember;
        }

        static OfferingParts BuildPlayerOfferings(Transform parent, Vector3 pos)
        {
            var root = new GameObject("Offrandes_Joueur");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            var t = root.transform;

            var leafMat = Lit("FeuilleClaire", new Color(0.24f, 0.46f, 0.28f));
            var leaf = Prim(PrimitiveType.Cube, "Feuille", t, new Vector3(0f, 0.03f, 0f), new Vector3(0.6f, 0.02f, 1.4f), leafMat);
            leaf.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);

            Color[] petals =
            {
                new Color(0.88f, 0.63f, 0.19f), new Color(0.90f, 0.90f, 0.85f), new Color(0.80f, 0.35f, 0.50f)
            };
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Prim(PrimitiveType.Sphere, "Fleur", t, new Vector3(Mathf.Cos(a) * 0.18f, 0.08f, Mathf.Sin(a) * 0.18f),
                    Vector3.one * 0.08f, Lit("Fleur" + (i % 3), petals[i % 3]));
            }

            Prim(PrimitiveType.Cylinder, "Encens", t, new Vector3(0.3f, 0.14f, 0.05f), new Vector3(0.012f, 0.14f, 0.012f),
                Lit("Encens", new Color(0.30f, 0.12f, 0.08f)));
            Prim(PrimitiveType.Sphere, "Braise", t, new Vector3(0.3f, 0.29f, 0.05f), Vector3.one * 0.025f,
                Unlit("Braise", new Color(1f, 0.35f, 0.12f)));

            // Fumee : monte droit (aucun vent), s'elargit et s'efface
            var smokeGo = new GameObject("Fumee");
            smokeGo.transform.SetParent(t, false);
            smokeGo.transform.localPosition = new Vector3(0.3f, 0.3f, 0.05f);
            var ps = smokeGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 6f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
            main.startColor = new Color(0.80f, 0.85f, 0.90f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;

            var emission = ps.emission;
            emission.rateOverTime = 7f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.005f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0f);
            vel.y = new ParticleSystem.MinMaxCurve(0.32f);
            vel.z = new ParticleSystem.MinMaxCurve(0f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 3.2f));

            var colorLife = ps.colorOverLifetime;
            colorLife.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1f) });
            colorLife.color = g;

            smokeGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = SmokeMaterial();

            // Petite lueur ambree (braise)
            var emberGo = new GameObject("Braise_Lumiere");
            emberGo.transform.SetParent(t, false);
            emberGo.transform.localPosition = new Vector3(0.3f, 0.35f, 0.05f);
            var ember = emberGo.AddComponent<Light>();
            ember.type = LightType.Point;
            ember.color = Palette.Amber;
            ember.range = 3.5f;
            ember.intensity = 0f;
            ember.shadows = LightShadows.None;

            root.SetActive(false);
            return new OfferingParts { root = root, smoke = ps, ember = ember };
        }
    }
}
