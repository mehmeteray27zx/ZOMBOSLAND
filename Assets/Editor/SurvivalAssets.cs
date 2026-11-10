using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zombosland;

// Item / recipe / database ScriptableObjects (created only when missing, so editor edits persist) and the generated pictogram icons.
public static class SurvivalAssets
{
    public const string ItemsFolder = "Assets/Data/Items";
    public const string RecipesFolder = "Assets/Data/Recipes";
    public const string DatabasePath = "Assets/Data/ItemDatabase.asset";
    public const string IconFolder = "Assets/Generated/Icons";

    struct ItemSpec
    {
        public string id, name, description;
        public ItemKind kind;
        public int stack;
        public float heal, damage, range, seconds, noise;
        public ItemSpec(string id, string name, string description, ItemKind kind, int stack, float heal = 0f, float damage = 0f, float range = 0f, float seconds = 0f, float noise = 0f)
        {
            this.id = id; this.name = name; this.description = description; this.kind = kind; this.stack = stack;
            this.heal = heal; this.damage = damage; this.range = range; this.seconds = seconds; this.noise = noise;
        }
    }

    static readonly ItemSpec[] Items =
    {
        new ItemSpec("cloth", "Kumaş", "Eski giysilerden ve perdelerden kalan temiz kumaş parçaları. Bandaj ve dikkat çekici için işe yarar.", ItemKind.Resource, 10),
        new ItemSpec("scrap", "Hurda", "Paslı levha, cıvata ve tel parçaları. Tamir ve barikat malzemesi.", ItemKind.Resource, 10),
        new ItemSpec("wood", "Odun", "Kırık tahta ve dal parçaları. Sopa, barikat ve sandık yapımında kullanılır.", ItemKind.Resource, 10),
        new ItemSpec("tape", "Bant", "Geniş koli bandı. Her şeyi bir arada tutar.", ItemKind.Resource, 5),
        new ItemSpec("stone", "Taş", "Elde rahat tutulan sağlam bir taş.", ItemKind.Resource, 10),
        new ItemSpec("bandage", "Bandaj", "Sıkı sarılmış temiz bandaj. Kullanınca can yeniler.", ItemKind.Consumable, 5, heal: 25f),
        new ItemSpec("lure", "Dikkat taşı", "Kumaşa sarılı taş. Atıldığı yere zombileri çeker.", ItemKind.Throwable, 5, noise: 16f),
        new ItemSpec("bat", "Ahşap sopa", "Bantla sarılmış sağlam bir sopa. Yakın dövüşte işe yarar.", ItemKind.Melee, 1, damage: 40f, range: 1.9f, seconds: 0.7f),
        new ItemSpec("axe", "Balta", "Yıpranmış demir başlı, ağır bir balta. Yakın dövüşte güçlüdür.", ItemKind.Melee, 1, damage: 60f, range: 2.0f, seconds: 1.0f),
        new ItemSpec("repair_kit", "Tamir kiti", "Hurda ve bantla hazırlanmış basit tamir takımı. Hasarlı barikatı onarır.", ItemKind.Kit, 3),
        new ItemSpec("barricade_kit", "Barikat kiti", "Avlunun kapı geçidine barikat kurmak için hazır tahtalar. Belirlenen noktada kullanılır.", ItemKind.Kit, 2),
        new ItemSpec("chest_kit", "Sandık kiti", "Avluda belirlenen noktaya depo sandığı kurar. Depoya 12 yuva ekler.", ItemKind.Kit, 1),
        new ItemSpec("supply_cache", "Erzak çantası", "Dükkândaki dolapta bırakılmış ağır bir erzak çantası. Avluya götürülmeli.", ItemKind.Resource, 1)
    };

    struct RecipeSpec
    {
        public string id, name, description, output;
        public int outputCount;
        public string[] inputs; // "id:count"
        public float seconds;
        public bool needsBench, available;
    }

    static RecipeSpec Recipe(string id, string name, string description, string output, string[] inputs, float seconds, bool bench)
    {
        return new RecipeSpec { id = id, name = name, description = description, output = output, outputCount = 1, inputs = inputs, seconds = seconds, needsBench = bench, available = true };
    }

    static readonly RecipeSpec[] Recipes =
    {
        Recipe("bandage", "Bandaj", "İki kumaş parçasını sıkıca sararak bandaj yapar.", "bandage", new[] { "cloth:2" }, 3f, false),
        Recipe("lure", "Dikkat taşı", "Taşı kumaşa sarar. Atınca ses çıkarır.", "lure", new[] { "stone:1", "cloth:1" }, 2f, false),
        Recipe("bat", "Ahşap sopa", "Üç tahtayı bantla birleştirir.", "bat", new[] { "wood:3", "tape:1" }, 4f, false),
        Recipe("repair_kit", "Tamir kiti", "Hurda ve bantla tamir takımı hazırlar. Tezgâh gerekir.", "repair_kit", new[] { "scrap:3", "tape:1" }, 6f, true),
        Recipe("barricade_kit", "Barikat kiti", "Tahta ve hurdayla barikat parçaları hazırlar. Tezgâh gerekir.", "barricade_kit", new[] { "wood:5", "scrap:2" }, 8f, true),
        Recipe("chest_kit", "Sandık kiti", "Depo sandığı için parçalar hazırlar. Tezgâh gerekir.", "chest_kit", new[] { "wood:6", "scrap:2" }, 8f, true)
    };

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    public static ItemDef LoadItem(string id) { return AssetDatabase.LoadAssetAtPath<ItemDef>(ItemsFolder + "/" + id + ".asset"); }

    // Creates what is missing, refreshes icons, returns the database. Report lines are appended to `report`.
    public static ItemDatabase EnsureAll(List<string> report)
    {
        EnsureFolder(ItemsFolder);
        EnsureFolder(RecipesFolder);
        EnsureFolder(IconFolder);
        var icons = IconPainter.PaintAll();

        int created = 0;
        var items = new List<ItemDef>();
        foreach (ItemSpec s in Items)
        {
            string path = ItemsFolder + "/" + s.id + ".asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDef>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDef>();
                item.id = s.id; item.displayName = s.name; item.description = s.description; item.kind = s.kind; item.maxStack = s.stack;
                item.healAmount = s.heal; item.meleeDamage = s.damage; item.meleeRange = s.range; item.meleeSeconds = s.seconds; item.noiseRadius = s.noise;
                AssetDatabase.CreateAsset(item, path);
                created++;
            }
            Texture2D icon;
            if (icons.TryGetValue(s.id, out icon)) item.icon = icon;
            EditorUtility.SetDirty(item);
            items.Add(item);
        }

        var recipes = new List<RecipeDef>();
        foreach (RecipeSpec s in Recipes)
        {
            string path = RecipesFolder + "/" + s.id + ".asset";
            var recipe = AssetDatabase.LoadAssetAtPath<RecipeDef>(path);
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<RecipeDef>();
                recipe.id = s.id; recipe.displayName = s.name; recipe.description = s.description;
                recipe.output = items.Find(i => i.id == s.output); recipe.outputCount = s.outputCount;
                var inputs = new ItemAmount[s.inputs.Length];
                for (int k = 0; k < inputs.Length; k++)
                {
                    string[] parts = s.inputs[k].Split(':');
                    inputs[k] = new ItemAmount { item = items.Find(i => i.id == parts[0]), count = int.Parse(parts[1]) };
                }
                recipe.inputs = inputs;
                recipe.seconds = s.seconds; recipe.needsBench = s.needsBench; recipe.available = s.available;
                AssetDatabase.CreateAsset(recipe, path);
                created++;
            }
            EditorUtility.SetDirty(recipe);
            recipes.Add(recipe);
        }

        var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<ItemDatabase>();
            db.items = items.ToArray();
            db.recipes = recipes.ToArray();
            AssetDatabase.CreateAsset(db, DatabasePath);
            created++;
        }
        else
        {
            // Keep the existing order and any extra editor-added entries; append what is missing.
            var itemList = new List<ItemDef>(db.items ?? new ItemDef[0]);
            foreach (ItemDef i in items) if (!itemList.Contains(i)) itemList.Add(i);
            var recipeList = new List<RecipeDef>(db.recipes ?? new RecipeDef[0]);
            foreach (RecipeDef r in recipes) if (!recipeList.Contains(r)) recipeList.Add(r);
            db.items = itemList.ToArray();
            db.recipes = recipeList.ToArray();
        }
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        report.Add("survival data: " + items.Count + " items, " + recipes.Count + " recipes, database " + DatabasePath + " (" + created + " assets newly created, existing ones untouched), "
                   + icons.Count + " icons under " + IconFolder);
        return db;
    }
}

// Draws 128 x 128 pictograms with alpha: filled primitives on a 256 canvas, a dark outline by alpha dilation, then a 2x downsample.
public static class IconPainter
{
    const int Big = 256;

    sealed class IconCanvas
    {
        public readonly Color[] px = new Color[Big * Big];

        public void Poly(Vector2[] pts, Color color)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector2 p in pts) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            for (int y = Mathf.Max(0, Mathf.FloorToInt(minY * 2f)); y <= Mathf.Min(Big - 1, Mathf.CeilToInt(maxY * 2f)); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(minX * 2f)); x <= Mathf.Min(Big - 1, Mathf.CeilToInt(maxX * 2f)); x++)
                    if (Inside(pts, (x + 0.5f) / 2f, (y + 0.5f) / 2f)) px[y * Big + x] = color;
        }

        static bool Inside(Vector2[] pts, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                if ((pts[i].y > y) != (pts[j].y > y) && x < (pts[j].x - pts[i].x) * (y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x) inside = !inside;
            return inside;
        }

        public void Rect(float x, float y, float w, float h, Color c) { Poly(new[] { new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h) }, c); }

        public void Disc(float cx, float cy, float r, Color c)
        {
            for (int y = Mathf.Max(0, Mathf.FloorToInt((cy - r) * 2f)); y <= Mathf.Min(Big - 1, Mathf.CeilToInt((cy + r) * 2f)); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt((cx - r) * 2f)); x <= Mathf.Min(Big - 1, Mathf.CeilToInt((cx + r) * 2f)); x++)
                {
                    float dx = (x + 0.5f) / 2f - cx, dy = (y + 0.5f) / 2f - cy;
                    if (dx * dx + dy * dy <= r * r) px[y * Big + x] = c;
                }
        }

        public void Ring(float cx, float cy, float rOuter, float rInner, Color c)
        {
            for (int y = Mathf.Max(0, Mathf.FloorToInt((cy - rOuter) * 2f)); y <= Mathf.Min(Big - 1, Mathf.CeilToInt((cy + rOuter) * 2f)); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt((cx - rOuter) * 2f)); x <= Mathf.Min(Big - 1, Mathf.CeilToInt((cx + rOuter) * 2f)); x++)
                {
                    float dx = (x + 0.5f) / 2f - cx, dy = (y + 0.5f) / 2f - cy;
                    float d2 = dx * dx + dy * dy;
                    if (d2 <= rOuter * rOuter && d2 >= rInner * rInner) px[y * Big + x] = c;
                }
        }

        // Thick line with round ends.
        public void Line(Vector2 a, Vector2 b, float width, Color c)
        {
            float r = width * 0.5f;
            float minX = Mathf.Min(a.x, b.x) - r, maxX = Mathf.Max(a.x, b.x) + r, minY = Mathf.Min(a.y, b.y) - r, maxY = Mathf.Max(a.y, b.y) + r;
            Vector2 ab = b - a;
            float len2 = Mathf.Max(0.0001f, ab.sqrMagnitude);
            for (int y = Mathf.Max(0, Mathf.FloorToInt(minY * 2f)); y <= Mathf.Min(Big - 1, Mathf.CeilToInt(maxY * 2f)); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(minX * 2f)); x <= Mathf.Min(Big - 1, Mathf.CeilToInt(maxX * 2f)); x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / 2f, (y + 0.5f) / 2f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    if ((p - (a + ab * t)).sqrMagnitude <= r * r) px[y * Big + x] = c;
                }
        }

        // Dark outline around everything drawn so far, then 2x downsample to 128.
        public Texture2D Finish(string path)
        {
            const int reach = 5;
            var outlined = new Color[Big * Big];
            Color outline = new Color(0.07f, 0.07f, 0.06f, 1f);
            for (int y = 0; y < Big; y++)
                for (int x = 0; x < Big; x++)
                {
                    Color c = px[y * Big + x];
                    if (c.a > 0.5f) { outlined[y * Big + x] = c; continue; }
                    bool near = false;
                    for (int dy = -reach; dy <= reach && !near; dy++)
                        for (int dx = -reach; dx <= reach; dx++)
                        {
                            if (dx * dx + dy * dy > reach * reach) continue;
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= Big || yy >= Big) continue;
                            if (px[yy * Big + xx].a > 0.5f) { near = true; break; }
                        }
                    outlined[y * Big + x] = near ? outline : new Color(0f, 0f, 0f, 0f);
                }
            var small = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var data = new Color[128 * 128];
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    Color sum = Color.clear;
                    float a = 0f;
                    for (int k = 0; k < 4; k++)
                    {
                        Color c = outlined[(y * 2 + k / 2) * Big + x * 2 + k % 2];
                        sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                        a += c.a;
                    }
                    data[y * 128 + x] = a > 0f ? new Color(sum.r / a, sum.g / a, sum.b / a, a / 4f) : Color.clear;
                }
            small.SetPixels(data);
            File.WriteAllBytes(path, small.EncodeToPNG());
            Object.DestroyImmediate(small);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }

    static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }
    static Vector2 V(float x, float y) { return new Vector2(x, y); }

    public static Dictionary<string, Texture2D> PaintAll()
    {
        var map = new Dictionary<string, Texture2D>();
        string folder = SurvivalAssets.IconFolder;
        System.Action<string, System.Action<IconCanvas>> make = (id, draw) =>
        {
            var canvas = new IconCanvas();
            draw(canvas);
            map[id] = canvas.Finish(folder + "/" + id + ".png");
        };

        // Folded rectangular cloth: stacked layers with a visible fold edge and a stripe.
        make("cloth", c =>
        {
            c.Poly(new[] { V(22, 34), V(104, 30), V(108, 88), V(26, 94) }, C(.55f, .58f, .52f));
            c.Poly(new[] { V(18, 44), V(100, 42), V(104, 98), V(22, 102) }, C(.78f, .73f, .60f));
            c.Poly(new[] { V(18, 44), V(100, 42), V(102, 54), V(20, 56) }, C(.88f, .83f, .70f));
            c.Line(V(28, 74), V(96, 72), 5f, C(.45f, .52f, .58f));
            c.Line(V(28, 86), V(96, 84), 3f, C(.45f, .52f, .58f));
        });

        // Tied roll: horizontal roll with end spiral, loose tail and a knot band.
        make("bandage", c =>
        {
            c.Poly(new[] { V(20, 40), V(88, 40), V(88, 88), V(20, 88) }, C(.90f, .88f, .80f));
            c.Disc(88, 64, 24f, C(.90f, .88f, .80f));
            c.Disc(20, 64, 24f, C(.80f, .77f, .68f));
            c.Ring(20, 64, 20f, 15f, C(.62f, .58f, .50f));
            c.Ring(20, 64, 10f, 5f, C(.62f, .58f, .50f));
            c.Rect(48, 36, 14, 56, C(.72f, .30f, .26f));       // tie band
            c.Poly(new[] { V(88, 40), V(116, 22), V(122, 30), V(100, 52) }, C(.82f, .79f, .70f)); // loose tail
            c.Disc(55, 92, 7f, C(.72f, .30f, .26f));           // knot
        });

        make("scrap", c =>
        {
            c.Poly(new[] { V(18, 40), V(70, 28), V(84, 70), V(30, 82) }, C(.45f, .47f, .48f));
            c.Poly(new[] { V(52, 58), V(108, 44), V(116, 84), V(60, 98) }, C(.58f, .40f, .27f));
            c.Poly(new[] { V(36, 86), V(62, 80), V(70, 106), V(42, 110) }, C(.36f, .38f, .40f));
            c.Disc(70, 64, 5f, C(.20f, .20f, .20f));
            c.Disc(102, 70, 4f, C(.28f, .22f, .18f));
            c.Line(V(24, 104), V(100, 112), 4f, C(.42f, .42f, .42f));
        });

        make("wood", c =>
        {
            Color bark = C(.50f, .36f, .22f), cut = C(.80f, .68f, .46f);
            for (int i = 0; i < 3; i++)
            {
                float y = 34 + i * 26;
                c.Rect(16, y, 84, 20, bark);
                c.Disc(100, y + 10, 10f, cut);
                c.Ring(100, y + 10, 6f, 4f, C(.62f, .48f, .30f));
                c.Line(V(24, y + 6), V(80, y + 6), 2f, C(.38f, .27f, .16f));
            }
        });

        // Tape: thick ring with the loose end peeling off.
        make("tape", c =>
        {
            c.Ring(60, 66, 38f, 18f, C(.70f, .70f, .66f));
            c.Ring(60, 66, 38f, 33f, C(.55f, .56f, .53f));
            c.Ring(60, 66, 22f, 18f, C(.40f, .41f, .40f));
            c.Poly(new[] { V(88, 40), V(114, 30), V(118, 40), V(96, 56) }, C(.70f, .70f, .66f));
        });

        make("stone", c =>
        {
            c.Poly(new[] { V(22, 44), V(46, 26), V(88, 28), V(112, 52), V(106, 88), V(72, 104), V(34, 98), V(18, 72) }, C(.55f, .55f, .52f));
            c.Poly(new[] { V(46, 26), V(88, 28), V(112, 52), V(78, 56), V(52, 52) }, C(.70f, .70f, .66f));
            c.Poly(new[] { V(106, 88), V(72, 104), V(34, 98), V(60, 80), V(96, 76) }, C(.40f, .40f, .38f));
            c.Line(V(52, 52), V(60, 80), 2.5f, C(.30f, .30f, .28f));
        });

        // Lure: stone wrapped in cloth strips, tied on top.
        make("lure", c =>
        {
            c.Poly(new[] { V(22, 42), V(44, 24), V(84, 26), V(106, 48), V(102, 86), V(70, 102), V(34, 96), V(18, 70) }, C(.55f, .55f, .52f));
            c.Poly(new[] { V(30, 40), V(96, 62), V(92, 78), V(24, 56) }, C(.80f, .75f, .62f));
            c.Poly(new[] { V(34, 74), V(100, 92), V(94, 104), V(30, 88) }, C(.80f, .75f, .62f));
            c.Poly(new[] { V(58, 20), V(78, 20), V(92, 4), V(70, 8) }, C(.70f, .66f, .54f));
            c.Disc(60, 22, 6f, C(.72f, .30f, .26f));
        });

        make("bat", c =>
        {
            c.Poly(new[] { V(18, 102), V(26, 110), V(112, 30), V(100, 16) }, C(.60f, .44f, .26f));
            c.Poly(new[] { V(100, 16), V(112, 30), V(122, 20), V(110, 8) }, C(.66f, .50f, .30f));
            c.Line(V(30, 96), V(44, 108), 8f, C(.78f, .76f, .70f)); // tape grip
            c.Line(V(38, 104), V(26, 90), 3f, C(.35f, .35f, .32f));
        });

        // Axe: long wooden haft with an iron head at the upper end.
        make("axe", c =>
        {
            c.Line(V(24, 108), V(88, 42), 9f, C(.60f, .44f, .26f));
            c.Line(V(34, 98), V(48, 84), 10f, C(.78f, .76f, .70f)); // wrapped grip
            c.Poly(new[] { V(76, 24), V(98, 30), V(116, 46), V(112, 74), V(100, 62), V(88, 56), V(80, 44) }, C(.55f, .57f, .60f)); // blade
            c.Poly(new[] { V(76, 24), V(98, 30), V(92, 40), V(80, 44) }, C(.70f, .72f, .75f)); // bevel highlight
            c.Rect(70, 38, 18, 22, C(.34f, .35f, .37f)); // socket / eye
        });

        make("repair_kit", c =>
        {
            c.Rect(16, 28, 96, 66, C(.62f, .30f, .22f));
            c.Rect(16, 28, 96, 14, C(.48f, .22f, .16f));
            c.Rect(52, 94, 24, 12, C(.40f, .40f, .40f));
            c.Line(V(36, 52), V(92, 80), 9f, C(.78f, .78f, .76f));
            c.Disc(32, 50, 12f, C(.78f, .78f, .76f));
            c.Disc(32, 50, 5f, C(.62f, .30f, .22f));
            c.Line(V(92, 52), V(40, 82), 7f, C(.55f, .56f, .58f));
        });

        make("barricade_kit", c =>
        {
            c.Rect(12, 40, 104, 14, C(.55f, .40f, .25f));
            c.Rect(12, 74, 104, 14, C(.55f, .40f, .25f));
            c.Line(V(22, 36), V(106, 98), 12f, C(.66f, .50f, .32f));
            c.Line(V(22, 98), V(106, 36), 12f, C(.50f, .36f, .22f));
            c.Disc(24, 47, 4f, C(.30f, .30f, .30f));
            c.Disc(104, 81, 4f, C(.30f, .30f, .30f));
        });

        make("chest_kit", c =>
        {
            c.Rect(14, 28, 100, 54, C(.58f, .42f, .26f));
            c.Poly(new[] { V(14, 82), V(114, 82), V(104, 106), V(24, 106) }, C(.66f, .50f, .32f));
            c.Rect(14, 28, 100, 8, C(.38f, .38f, .38f));
            c.Rect(56, 66, 16, 22, C(.78f, .70f, .36f));
            c.Line(V(14, 56), V(114, 56), 4f, C(.38f, .38f, .38f));
        });

        // Supply bag: olive duffel with two straps and a buckle.
        make("supply_cache", c =>
        {
            c.Rect(16, 40, 96, 56, C(.30f, .36f, .22f));
            c.Rect(16, 40, 96, 12, C(.22f, .27f, .16f));
            c.Rect(34, 28, 8, 70, C(.52f, .44f, .28f));
            c.Rect(86, 28, 8, 70, C(.52f, .44f, .28f));
            c.Poly(new[] { V(40, 28), V(88, 28), V(80, 14), V(48, 14) }, C(.40f, .38f, .26f));
            c.Rect(56, 62, 16, 12, C(.78f, .70f, .36f));
        });

        make("pistol", c =>
        {
            c.Rect(14, 66, 92, 22, C(.34f, .36f, .38f));
            c.Rect(14, 82, 92, 8, C(.48f, .50f, .52f));
            c.Poly(new[] { V(14, 66), V(46, 66), V(40, 22), V(18, 22) }, C(.22f, .23f, .25f));
            c.Poly(new[] { V(46, 66), V(70, 66), V(66, 46), V(52, 46) }, C(.28f, .29f, .31f));
            c.Rect(104, 70, 12, 14, C(.22f, .23f, .25f));
            c.Disc(54, 52, 6f, C(.12f, .12f, .12f));
        });

        make("ammo", c =>
        {
            for (int i = 0; i < 3; i++)
            {
                float x = 20 + i * 34;
                c.Rect(x, 22, 24, 44, C(.78f, .60f, .24f));
                c.Poly(new[] { V(x, 66), V(x + 24, 66), V(x + 18, 98), V(x + 6, 98) }, C(.70f, .42f, .22f));
                c.Rect(x - 2, 18, 28, 8, C(.58f, .44f, .18f));
                c.Rect(x + 4, 28, 5, 30, C(.92f, .80f, .50f));
            }
        });

        return map;
    }
}
