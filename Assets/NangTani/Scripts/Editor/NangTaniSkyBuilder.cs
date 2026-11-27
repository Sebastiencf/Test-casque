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
    /// Menu "Nang Tani > 3 - Brume et lune".
    /// Ajoute a NangTani_Decor les nappes de brume (Art/Sky/mist_layout.json), la lune dans le ciel et
    /// un eclairage argente-bleute. Ne touche a rien d'autre : on peut le relancer sans perdre ses retouches.
    /// A relancer apres le menu 2, qui recree la scene.
    /// </summary>
    public static class NangTaniSkyBuilder
    {
        const string SkyDir = "Assets/NangTani/Art/Sky";
        const string ScenePath = "Assets/Scenes/NangTani_Decor.unity";
        const string MistObject = "Brume_Forets";
        const string MoonObject = "Lune_Ciel";

        // Position de la lune vue du joueur : 0 = devant, negatif = a gauche.
        const float MoonAzimuth = -48f;
        const float MoonElevation = 28f;

        // Eclairage argente-bleute (multiplie les couleurs de chaque etat d'ambiance).
        static readonly Color MoonTint = new Color(0.80f, 0.89f, 1.00f);
        static readonly Color AmbientTint = new Color(1.05f, 1.20f, 1.45f);
        const float MoonBoost = 1.3f;

        [System.Serializable]
        class Layout
        {
            public Card[] cards;
        }

        [System.Serializable]
        class Card
        {
            public int zone;
            public float x, z, width, height, alpha, phase;
            public float y;       // hauteur du terrain sous la nappe (0 si absente)
        }

        [MenuItem("Nang Tani/3 - Brume et lune")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Info("Quitte d'abord le mode Play.");
                return;
            }
            if (!File.Exists(ScenePath))
            {
                Info("Scene introuvable : " + ScenePath + "\nLance d'abord le menu 2.");
                return;
            }

            var layoutAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(SkyDir + "/mist_layout.json");
            var mistShader = Shader.Find("NangTani/Mist");
            var moonShader = Shader.Find("NangTani/Moon");
            if (layoutAsset == null || mistShader == null || moonShader == null)
            {
                Info("Fichiers de brume ou de lune introuvables dans " + SkyDir +
                     " (ou shader en erreur : regarde la Console).");
                return;
            }
            var layout = JsonUtility.FromJson<Layout>(layoutAsset.text);

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ConfigureTexture(SkyDir + "/Mist.png");
            ConfigureTexture(SkyDir + "/Moon.png");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (var go in scene.GetRootGameObjects())
                if (go.name == MistObject || go.name == MoonObject) Object.DestroyImmediate(go);

            BuildMist(layout, MistMaterial(mistShader));
            BuildMoon(MoonMaterial(moonShader));
            bool lit = TuneMoonLight(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Info("Brume (" + layout.cards.Length + " nappes) et lune ajoutees a NangTani_Decor." +
                 (lit ? "" : "\n\nAttention : lumiere de lune non reglee (objet 'Experience' introuvable).") +
                 "\n\nDans le jeu, la lune est devant a gauche du joueur.");
        }

        static void ConfigureTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static Material LoadOrCreate(string path, Shader shader)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            return m;
        }

        // Les reglages manuels du materiau de brume (teinte, opacite, derive) sont conserves.
        static Material MistMaterial(Shader shader)
        {
            var m = LoadOrCreate(SkyDir + "/Brume.mat", shader);
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(SkyDir + "/Mist.png"));
            EditorUtility.SetDirty(m);
            return m;
        }

        static Vector3 MoonDirection()
        {
            return Quaternion.Euler(-MoonElevation, MoonAzimuth, 0f) * Vector3.forward;
        }

        static Material MoonMaterial(Shader shader)
        {
            var m = LoadOrCreate(SkyDir + "/Lune.mat", shader);
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(SkyDir + "/Moon.png"));
            var d = MoonDirection();
            m.SetVector("_Direction", new Vector4(d.x, d.y, d.z, 0f));
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---- Brume : un maillage par zone (peu de draw calls) ----------------

        static void BuildMist(Layout layout, Material material)
        {
            var parent = new GameObject(MistObject).transform;
            var zones = new SortedDictionary<int, List<Card>>();
            foreach (var card in layout.cards)
            {
                if (!zones.TryGetValue(card.zone, out var list)) zones[card.zone] = list = new List<Card>();
                list.Add(card);
            }

            foreach (var zone in zones)
            {
                var vertices = new List<Vector3>();
                var uv = new List<Vector2>();
                var offsets = new List<Vector2>();
                var data = new List<Vector2>();
                var triangles = new List<int>();
                var bounds = new Bounds(new Vector3(zone.Value[0].x, zone.Value[0].y, zone.Value[0].z), Vector3.zero);

                foreach (var card in zone.Value)
                {
                    int first = vertices.Count;
                    var center = new Vector3(card.x, card.y - 0.15f, card.z);
                    for (int corner = 0; corner < 4; corner++)
                    {
                        float u = corner == 1 || corner == 2 ? 1f : 0f;
                        float v = corner >= 2 ? 1f : 0f;
                        vertices.Add(center);
                        uv.Add(new Vector2(u, v));
                        offsets.Add(new Vector2((u - 0.5f) * card.width, v * card.height));
                        data.Add(new Vector2(card.alpha, card.phase));
                    }
                    triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
                    // Les coins sont deplaces par le shader : la boite englobante en tient compte.
                    bounds.Encapsulate(new Bounds(center + Vector3.up * (card.height * 0.5f),
                        new Vector3(card.width + 2f, card.height, card.width + 2f)));
                }

                var path = SkyDir + "/Brume_Zone" + zone.Key + ".asset";
                AssetDatabase.DeleteAsset(path);
                var mesh = new Mesh { name = "Brume_Zone" + zone.Key };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uv);
                mesh.SetUVs(1, offsets);
                mesh.SetUVs(2, data);
                mesh.SetTriangles(triangles, 0);
                mesh.bounds = bounds;
                AssetDatabase.CreateAsset(mesh, path);

                CreateRenderer(mesh.name, parent, mesh, material);
            }
        }

        /// <summary>Replace les nappes de brume deja presentes (apres un changement de terrain, menu 4).</summary>
        internal static void RebuildMist(Scene scene)
        {
            var layoutAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(SkyDir + "/mist_layout.json");
            var mistShader = Shader.Find("NangTani/Mist");
            if (layoutAsset == null || mistShader == null) return;

            bool found = false;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name != MistObject) continue;
                Object.DestroyImmediate(go);
                found = true;
            }
            // Sans brume dans la scene, on n'en ajoute pas : c'est le role du menu 3.
            if (found) BuildMist(JsonUtility.FromJson<Layout>(layoutAsset.text), MistMaterial(mistShader));
        }

        static void CreateRenderer(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ---- Lune -------------------------------------------------------------

        static void BuildMoon(Material material)
        {
            var path = SkyDir + "/Lune_Quad.asset";
            AssetDatabase.DeleteAsset(path);
            var mesh = new Mesh { name = "Lune_Quad" };
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(-1f, 1f, 0f)
            });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
            mesh.SetTriangles(new List<int> { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2000f); // placee par le shader : toujours dessinee
            AssetDatabase.CreateAsset(mesh, path);

            CreateRenderer(MoonObject, null, mesh, material);
        }

        /// <summary>La lumiere vient du cote de la lune ; sa hauteur reste pilotee par l'ambiance (ombres longues).</summary>
        static bool TuneMoonLight(Scene scene)
        {
            AtmosphereController atmosphere = null;
            foreach (var go in scene.GetRootGameObjects())
                if (atmosphere == null) atmosphere = go.GetComponentInChildren<AtmosphereController>(true);
            if (atmosphere == null || atmosphere.moon == null) return false;

            atmosphere.moonYaw = MoonAzimuth + 180f;
            atmosphere.moonTint = MoonTint;
            atmosphere.ambientTint = AmbientTint;
            atmosphere.moonBoost = MoonBoost;

            // Apercu hors mode Play : memes valeurs que l'etat calme.
            var calm = AtmosphereState.Calm;
            var moon = atmosphere.moon;
            moon.color = calm.moonColor * MoonTint;
            moon.intensity = calm.moonIntensity * MoonBoost;
            moon.transform.rotation = Quaternion.Euler(calm.moonPitch, atmosphere.moonYaw, 0f);
            RenderSettings.ambientLight = calm.ambient * AmbientTint;

            EditorUtility.SetDirty(atmosphere);
            EditorUtility.SetDirty(moon);
            return true;
        }

        static void Info(string message)
        {
            EditorUtility.DisplayDialog("Nang Tani", message, "OK");
        }
    }
}
