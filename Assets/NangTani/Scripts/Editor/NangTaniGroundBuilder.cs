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
    /// Menu "Nang Tani > 4 - Sol (relief, boue, mares)".
    /// Remplace le sol plat de NangTani_Decor par un terrain marecageux (Art/Sol/sol.bytes), ajoute l'eau,
    /// les herbes, roseaux, riz et lotus, puis repose la foret et le sous-bois sur le relief.
    /// Les lieux ou les personnages marchent restent au niveau 0.
    /// On peut le relancer sans cumul. A relancer apres le menu 2, qui recree la scene.
    /// </summary>
    public static class NangTaniGroundBuilder
    {
        const string Dir = "Assets/NangTani/Art/Sol";
        const string GeneratedDir = Dir + "/Generated";
        const string MeshPath = GeneratedDir + "/Sol_Maillages.asset";
        const string ScenePath = "Assets/Scenes/NangTani_Decor.unity";
        const string RootName = "Sol_Relief";

        // ==================================================================
        //  Donnees du terrain
        // ==================================================================

        class Relief
        {
            public int chunksX, chunksZ, cells, ratio, nx, nz;
            public float x0, z0, step, water;
            public byte[] fine;
            public float[] height;
            public byte[] splat;    // 4 octets par point : herbe, terre, boue, feuilles
            public byte[] extra;    // 3 octets par point : humidite, ombre de contact, teinte

            public bool Fine(int i, int k) { return fine[k * chunksX + i] != 0; }
            public int Stride(int i, int k) { return Fine(i, k) ? 1 : ratio; }
            public float H(int ix, int iz) { return height[iz * nx + ix]; }

            /// <summary>Hauteur du sol telle qu'elle est dessinee (memes triangles que le maillage).</summary>
            public float Sample(float x, float z)
            {
                float size = cells * step;
                int i = Mathf.Clamp(Mathf.FloorToInt((x - x0) / size), 0, chunksX - 1);
                int k = Mathf.Clamp(Mathf.FloorToInt((z - z0) / size), 0, chunksZ - 1);
                int s = Stride(i, k);
                float gx = Mathf.Clamp((x - x0) / step, 0f, nx - 1.001f);
                float gz = Mathf.Clamp((z - z0) / step, 0f, nz - 1.001f);
                int ix = Mathf.Min((int)(gx / s) * s, nx - 1 - s);
                int iz = Mathf.Min((int)(gz / s) * s, nz - 1 - s);
                float u = (gx - ix) / s, v = (gz - iz) / s;
                float a = H(ix, iz), b = H(ix + s, iz), c = H(ix, iz + s), d = H(ix + s, iz + s);
                return u > v ? a + (b - a) * u + (d - b) * v : a + (d - c) * u + (c - a) * v;
            }

            public Vector3 Normal(int ix, int iz, int s)
            {
                float left = H(Mathf.Max(ix - s, 0), iz), right = H(Mathf.Min(ix + s, nx - 1), iz);
                float back = H(ix, Mathf.Max(iz - s, 0)), front = H(ix, Mathf.Min(iz + s, nz - 1));
                return new Vector3(left - right, 2f * s * step, back - front).normalized;
            }
        }

        static Relief Load(byte[] bytes)
        {
            using (var r = new BinaryReader(new MemoryStream(bytes)))
            {
                if (new string(r.ReadChars(4)) != "NTG1") return null;
                var t = new Relief
                {
                    chunksX = r.ReadInt32(), chunksZ = r.ReadInt32(), cells = r.ReadInt32(), ratio = r.ReadInt32(),
                    x0 = r.ReadSingle(), z0 = r.ReadSingle(), step = r.ReadSingle(), water = r.ReadSingle()
                };
                t.nx = t.chunksX * t.cells + 1;
                t.nz = t.chunksZ * t.cells + 1;
                int n = t.nx * t.nz;
                t.fine = r.ReadBytes(t.chunksX * t.chunksZ);
                t.height = new float[n];
                for (int i = 0; i < n; i++) t.height[i] = r.ReadSingle();
                t.splat = r.ReadBytes(n * 4);
                t.extra = r.ReadBytes(n * 3);
                return t.extra.Length == n * 3 ? t : null;
            }
        }

        [System.Serializable]
        class PlantFile
        {
            public Plant[] plants;
            public float[] paddy;   // riziere : x min, x max, z min, z max
        }

        [System.Serializable]
        class Plant
        {
            public int t;           // 0 roseaux, 1 herbe, 2 herbe seche, 3 riz, 4 lotus, 5 feuilles mortes
            public float x, z, yaw, s;
        }

        // ==================================================================
        //  Point d'entree
        // ==================================================================

        [MenuItem("Nang Tani/4 - Sol (relief, boue, mares)")]
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

            var data = AssetDatabase.LoadAssetAtPath<TextAsset>(Dir + "/sol.bytes");
            var plantData = AssetDatabase.LoadAssetAtPath<TextAsset>(Dir + "/sol_plantes.json");
            var groundShader = Shader.Find("NangTani/Ground");
            var waterShader = Shader.Find("NangTani/Water");
            var foliageShader = Shader.Find("NangTani/Foliage");
            if (data == null || plantData == null || groundShader == null || waterShader == null || foliageShader == null)
            {
                Info("Fichiers du sol introuvables dans " + Dir + " (ou shader en erreur : regarde la Console).");
                return;
            }
            var terrain = Load(data.bytes);
            if (terrain == null)
            {
                Info("Fichier sol.bytes illisible.");
                return;
            }
            var plants = JsonUtility.FromJson<PlantFile>(plantData.text);

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ConfigureTextures();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Apres l'ouverture de la scene : Unity peut decharger les assets inutilises en changeant de scene.
            var env = FindRoot(scene, "Environment (statique)");
            if (env == null)
            {
                Info("Objet 'Environment (statique)' introuvable dans la scene.");
                return;
            }
            groundShader = Shader.Find("NangTani/Ground");
            waterShader = Shader.Find("NangTani/Water");
            foliageShader = Shader.Find("NangTani/Foliage");
            var ground = GroundMaterial(groundShader);
            var water = LoadOrCreate(Dir + "/Eau.mat", waterShader);
            var foliage = LoadOrCreate(Dir + "/Plantes.mat", foliageShader);
            foliage.SetTexture("_BaseMap", LoadTexture("Sol_Plantes"));
            EditorUtility.SetDirty(water);
            EditorUtility.SetDirty(foliage);

            foreach (var go in scene.GetRootGameObjects())
                if (go.name == RootName) Object.DestroyImmediate(go);

            EnsureFolder(GeneratedDir);
            AssetDatabase.DeleteAsset(MeshPath);
            _assetCreated = false;

            var root = new GameObject(RootName).transform;
            BuildGround(terrain, Child(root, "Terrain"), ground);
            BuildWater(terrain, Child(root, "Eau"), water);
            BuildPlants(terrain, plants, Child(root, "Plantes"), foliage);
            // Regroupement des objets immobiles seulement : un calcul de lumiere ne doit pas toucher a ces maillages.
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

            // L'ancien sol plat du greybox est desactive (pas supprime).
            foreach (var name in new[] { "Sol", "Chemin_Village" })
            {
                var old = env.Find(name);
                if (old != null) old.gameObject.SetActive(false);
            }

            bool replanted = NangTaniDecorBuilder.RebuildVegetation(env, (model, x, z) =>
            {
                var p = plants.paddy;
                if (x > p[0] - 0.3f && x < p[1] + 0.3f && z > p[2] - 0.3f && z < p[3] + 0.3f) return null;
                float h = terrain.Sample(x, z);
                bool tree = model.Contains("anana-tree") || model.Contains("anana-Tree");
                if (!tree && h < terrain.water - 0.02f) return null;      // pas de rochers ni de touffes noyes
                return h - (tree ? 0.05f : 0.025f);                       // legerement enfonce : rien ne flotte
            });

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Info("Sol en relief, eau et " + plants.plants.Length + " plantes ajoutes a NangTani_Decor." +
                 (replanted ? "" : "\n\nAttention : la foret n'a pas pu etre reposee sur le relief (lance le menu 2, puis 3 et 4)."));
        }

        // ==================================================================
        //  Textures et materiaux
        // ==================================================================

        static Texture2D LoadTexture(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/" + name + ".png");
        }

        static void ConfigureTextures()
        {
            foreach (var name in new[] { "Sol_Herbe", "Sol_Terre", "Sol_Boue", "Sol_Feuilles", "Sol_Plantes" })
            {
                var importer = AssetImporter.GetAtPath(Dir + "/" + name + ".png") as TextureImporter;
                if (importer == null) continue;
                bool atlas = name == "Sol_Plantes";
                importer.filterMode = FilterMode.Point;                 // rendu pixelise "Fears to Fathom"
                importer.wrapMode = atlas ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;                          // evite le scintillement au loin en VR
                importer.anisoLevel = 4;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                // Sol : l'alpha est un relief, pas une transparence.
                importer.alphaIsTransparency = atlas;
                importer.mipMapsPreserveCoverage = atlas;               // les herbes ne s'effacent pas au loin
                if (atlas) importer.alphaTestReferenceValue = 0.5f;
                importer.SaveAndReimport();
            }
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

        // Les reglages manuels (luminosite, reflets, tailles) sont conserves d'un lancement a l'autre.
        static Material GroundMaterial(Shader shader)
        {
            var m = LoadOrCreate(Dir + "/Sol.mat", shader);
            m.SetTexture("_GrassMap", LoadTexture("Sol_Herbe"));
            m.SetTexture("_DirtMap", LoadTexture("Sol_Terre"));
            m.SetTexture("_MudMap", LoadTexture("Sol_Boue"));
            m.SetTexture("_LitterMap", LoadTexture("Sol_Feuilles"));
            EditorUtility.SetDirty(m);
            return m;
        }

        // ==================================================================
        //  Maillages
        // ==================================================================

        static bool _assetCreated;

        static void AddMesh(Mesh mesh, Transform parent, Material material, bool receiveShadows)
        {
            mesh.RecalculateBounds();
            var bounds = mesh.bounds;
            bounds.Expand(0.2f);
            mesh.bounds = bounds;

            if (!_assetCreated)
            {
                AssetDatabase.CreateAsset(mesh, MeshPath);
                _assetCreated = true;
            }
            else AssetDatabase.AddObjectToAsset(mesh, MeshPath);

            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = receiveShadows;
        }

        static void BuildGround(Relief t, Transform parent, Material material)
        {
            for (int k = 0; k < t.chunksZ; k++)
            {
                for (int i = 0; i < t.chunksX; i++)
                {
                    int s = t.Stride(i, k);
                    int side = t.cells / s + 1;
                    var vertices = new List<Vector3>(side * side);
                    var normals = new List<Vector3>(side * side);
                    var colors = new List<Color32>(side * side);
                    var surface = new List<Vector2>(side * side);
                    var tint = new List<Vector2>(side * side);
                    var triangles = new List<int>();

                    for (int lz = 0; lz <= t.cells; lz += s)
                    {
                        for (int lx = 0; lx <= t.cells; lx += s)
                        {
                            int ix = i * t.cells + lx, iz = k * t.cells + lz;

                            // Bord d'un carre fin touchant un carre grossier : on se cale sur ses points (pas de fente).
                            int ax = ix, az = iz, bx = ix, bz = iz;
                            float f = 0f;
                            bool alongZ = (lx == 0 && i > 0 && !t.Fine(i - 1, k)) ||
                                          (lx == t.cells && i < t.chunksX - 1 && !t.Fine(i + 1, k));
                            bool alongX = (lz == 0 && k > 0 && !t.Fine(i, k - 1)) ||
                                          (lz == t.cells && k < t.chunksZ - 1 && !t.Fine(i, k + 1));
                            bool stitched = false;
                            if (s == 1 && alongZ && lz % t.ratio != 0)
                            {
                                az = iz - lz % t.ratio; bz = az + t.ratio; f = (lz % t.ratio) / (float)t.ratio;
                                stitched = true;
                            }
                            else if (s == 1 && alongX && lx % t.ratio != 0)
                            {
                                ax = ix - lx % t.ratio; bx = ax + t.ratio; f = (lx % t.ratio) / (float)t.ratio;
                                stitched = true;
                            }
                            int ns = stitched || (s == 1 && (alongX || alongZ)) ? t.ratio : s;

                            int a = az * t.nx + ax, b = bz * t.nx + bx;
                            vertices.Add(new Vector3(t.x0 + ix * t.step, Mathf.Lerp(t.height[a], t.height[b], f),
                                t.z0 + iz * t.step));
                            normals.Add(Vector3.Lerp(t.Normal(ax, az, ns), t.Normal(bx, bz, ns), f).normalized);
                            colors.Add(Color32.Lerp(Splat(t, a), Splat(t, b), f));
                            surface.Add(new Vector2(Mathf.Lerp(t.extra[a * 3], t.extra[b * 3], f) / 255f,
                                                    Mathf.Lerp(t.extra[a * 3 + 1], t.extra[b * 3 + 1], f) / 255f));
                            tint.Add(new Vector2(Mathf.Lerp(t.extra[a * 3 + 2], t.extra[b * 3 + 2], f) / 255f, 0f));
                        }
                    }

                    for (int z = 0; z < side - 1; z++)
                        for (int x = 0; x < side - 1; x++)
                            Quad(triangles, z * side + x, z * side + x + 1, (z + 1) * side + x, (z + 1) * side + x + 1);

                    var mesh = new Mesh { name = "Sol_" + i + "_" + k };
                    mesh.SetVertices(vertices);
                    mesh.SetNormals(normals);
                    mesh.SetColors(colors);
                    mesh.SetUVs(0, surface);
                    mesh.SetUVs(1, tint);
                    mesh.SetTriangles(triangles, 0);
                    AddMesh(mesh, parent, material, true);
                }
            }
        }

        static Color32 Splat(Relief t, int index)
        {
            int j = index * 4;
            return new Color32(t.splat[j], t.splat[j + 1], t.splat[j + 2], t.splat[j + 3]);
        }

        /// <summary>Carre horizontal vu du dessus : a=(0,0) b=(1,0) c=(0,1) d=(1,1).</summary>
        static void Quad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(d); triangles.Add(b);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        /// <summary>L'eau ne couvre que les carres dont au moins un coin est sous le niveau d'eau.</summary>
        static void BuildWater(Relief t, Transform parent, Material material)
        {
            for (int k = 0; k < t.chunksZ; k++)
            {
                for (int i = 0; i < t.chunksX; i++)
                {
                    int s = t.Stride(i, k);
                    var vertices = new List<Vector3>();
                    var depth = new List<Vector2>();
                    var triangles = new List<int>();
                    var index = new Dictionary<int, int>();

                    System.Func<int, int, int> vertex = (ix, iz) =>
                    {
                        int key = iz * t.nx + ix;
                        if (index.TryGetValue(key, out var found)) return found;
                        index[key] = vertices.Count;
                        vertices.Add(new Vector3(t.x0 + ix * t.step, t.water, t.z0 + iz * t.step));
                        depth.Add(new Vector2(Mathf.Max(t.water - t.H(ix, iz), 0f), 0f));
                        return vertices.Count - 1;
                    };

                    for (int z = k * t.cells; z < (k + 1) * t.cells; z += s)
                    {
                        for (int x = i * t.cells; x < (i + 1) * t.cells; x += s)
                        {
                            float low = Mathf.Min(Mathf.Min(t.H(x, z), t.H(x + s, z)),
                                                  Mathf.Min(t.H(x, z + s), t.H(x + s, z + s)));
                            if (low >= t.water) continue;
                            Quad(triangles, vertex(x, z), vertex(x + s, z), vertex(x, z + s), vertex(x + s, z + s));
                        }
                    }
                    if (triangles.Count == 0) continue;

                    var mesh = new Mesh { name = "Eau_" + i + "_" + k };
                    mesh.SetVertices(vertices);
                    mesh.SetUVs(0, depth);
                    mesh.SetTriangles(triangles, 0);
                    AddMesh(mesh, parent, material, false);
                }
            }
        }

        // ==================================================================
        //  Plantes : cartes decoupees, fusionnees par carre de 12 m
        // ==================================================================

        class Cards
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<Vector2> data = new List<Vector2>();   // ondulation, ombrage
            public readonly List<int> triangles = new List<int>();

            /// <summary>Coins dans l'ordre : bas gauche, bas droit, haut droit, haut gauche.</summary>
            public void Add(Vector3[] corners, Rect rect, Vector2 low, Vector2 high)
            {
                int first = vertices.Count;
                vertices.AddRange(corners);
                uv.Add(new Vector2(rect.xMin, rect.yMin));
                uv.Add(new Vector2(rect.xMax, rect.yMin));
                uv.Add(new Vector2(rect.xMax, rect.yMax));
                uv.Add(new Vector2(rect.xMin, rect.yMax));
                data.Add(low); data.Add(low); data.Add(high); data.Add(high);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
                triangles.Add(first); triangles.Add(first + 3); triangles.Add(first + 2);
            }
        }

        /// <summary>Zone de l'atlas 512 x 256, donnee en pixels depuis le coin haut gauche (1 px de marge).</summary>
        static Rect Atlas(int x, int y, int width, int height)
        {
            return Rect.MinMaxRect((x + 1) / 512f, 1f - (y + height - 1) / 256f,
                                   (x + width - 1) / 512f, 1f - (y + 1) / 256f);
        }

        static void BuildPlants(Relief t, PlantFile file, Transform parent, Material material)
        {
            var rects = new[]
            {
                Atlas(0, 0, 128, 256), Atlas(128, 0, 128, 128), Atlas(128, 128, 128, 128),
                Atlas(256, 0, 128, 128), Atlas(256, 128, 128, 128), Atlas(384, 0, 128, 128)
            };
            var cells = new SortedDictionary<int, Cards>();
            float size = t.cells * t.step;

            foreach (var p in file.plants)
            {
                int i = Mathf.FloorToInt((p.x - t.x0) / size), k = Mathf.FloorToInt((p.z - t.z0) / size);
                int key = k * t.chunksX + i;
                if (!cells.TryGetValue(key, out var cards)) cells[key] = cards = new Cards();
                float yaw = p.yaw * Mathf.Deg2Rad;

                if (p.t >= 4)
                {
                    // Posees a plat : lotus sur l'eau, feuilles mortes sur le sol.
                    var u = new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw)) * (p.s * 0.5f);
                    var v = new Vector2(-u.y, u.x);
                    var corners = new[]
                    {
                        Flat(t, p, -u - v), Flat(t, p, u - v), Flat(t, p, u + v), Flat(t, p, -u + v)
                    };
                    var shade = new Vector2(0f, p.t == 4 ? 1f : 0.75f);
                    cards.Add(corners, rects[p.t], shade, shade);
                }
                else
                {
                    // Deux cartes verticales croisees.
                    float width = p.s * (p.t == 0 ? 0.55f : p.t == 3 ? 0.8f : 1.25f);
                    float height = p.s * (p.t == 0 ? 1.15f : 1f);
                    float sway = p.t == 3 ? 0.5f : 1f;
                    float y = t.Sample(p.x, p.z) - 0.025f;
                    for (int j = 0; j < 2; j++)
                    {
                        float a = yaw + j * Mathf.PI * 0.5f;
                        var half = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (width * 0.5f);
                        var lean = new Vector3(-half.z, 0f, half.x) * 0.18f;
                        var foot = new Vector3(p.x, y, p.z);
                        var top = lean + Vector3.up * height;
                        var corners = new[] { foot - half, foot + half, foot + half + top, foot - half + top };
                        cards.Add(corners, rects[p.t], new Vector2(0f, 0.45f), new Vector2(sway, 1f));
                    }
                }
            }

            foreach (var cell in cells)
            {
                var mesh = new Mesh { name = "Plantes_" + (cell.Key % t.chunksX) + "_" + (cell.Key / t.chunksX) };
                if (cell.Value.vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(cell.Value.vertices);
                mesh.SetUVs(0, cell.Value.uv);
                mesh.SetUVs(1, cell.Value.data);
                mesh.SetTriangles(cell.Value.triangles, 0);
                AddMesh(mesh, parent, material, false);
            }
        }

        static Vector3 Flat(Relief t, Plant p, Vector2 offset)
        {
            float x = p.x + offset.x, z = p.z + offset.y;
            float y = p.t == 4 ? t.water + 0.008f : t.Sample(x, z) + 0.014f;
            return new Vector3(x, y, z);
        }

        // ==================================================================
        //  Divers
        // ==================================================================

        static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        static Transform FindRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go.transform;
            return null;
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
