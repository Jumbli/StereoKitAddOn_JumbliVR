using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using StereoKit;
using static StbTrueTypeSharp.StbTrueType;

namespace JumbliVR
{
    static internal class IconCache
    {
        private const int maxAtlasWidth = 2048;
        private const int maxAtlasHeight = 1024;

        static public IconDetails generatingIcon = new IconDetails();
        static private Tex? atlasTexture = null;
        static public Color32[]? atlasTextureColors = null;
        static public int[] atlasTextureDimensions =  { 512, 512 };
        static private Vec2 uvSize = new Vec2(0, 0);
        static private int[] slots = { 1, 1 };
        static private int slotsCount = 16;
        
        public class Config
        {
            public int sdfSize = 64;
            public int minWidth = 512;
            public int maxWidth = 2048;
            public int minHeight = 512;
            public int maxHeight = 1024;
            public float timeout = 60;
        }

        private static Config config = new Config();

    // Key is Font.Id + ":" + codePoint
        static private Dictionary<string, IconDetails> icons = new Dictionary<string, IconDetails>();
        static public Shader shader;
        static public Shader shaderPBR;

        static public int SdfSize
        {
            get {return config.sdfSize; }
            set { config.sdfSize = value; SetConfig(config, false,true); }
        }

        static public void SetConfig(Config? newConfig = null, bool increaseSize = false, bool sdfSizeChanged = false)
        {
            int[] oldDimensions = new int[2];

            if (newConfig != null)
            {
                if (sdfSizeChanged)
                    GenerateSDF.ClearCache();
                config = newConfig;
            }
            
            if (config.sdfSize % 2 == 1) // ensure an even number is used
                config.sdfSize++;

            // Currently not using mips because the texture may be frequently updated
            // Using mips may make drawing smaller icons more efficient in the shader
            if (atlasTexture == null)
                atlasTexture = new Tex(TexType.ImageNomips, TexFormat.Rgba32);

            if (increaseSize)
            {
                oldDimensions = new int[] { atlasTextureDimensions[0], atlasTextureDimensions[1] };
                if (atlasTextureDimensions[0] == atlasTextureDimensions[1])
                    atlasTextureDimensions[0] *= 2;
                else
                    atlasTextureDimensions[1] = atlasTextureDimensions[0];
            }
            else
            {
                atlasTextureDimensions[0] = config.minWidth;
                atlasTextureDimensions[1] = config.minHeight;
            }

            slots[0] = atlasTextureDimensions[0] / config.sdfSize;
            slots[1] = atlasTextureDimensions[1] / config.sdfSize;
            slotsCount = slots[0] * slots[1];

            // Remap existing icons
            if (increaseSize && atlasTextureColors != null)
            {
                Color32[] textureColorsNew = new Color32[atlasTextureDimensions[0] * atlasTextureDimensions[1]];
                int destInx = 0;
                int srcInx = 0;
                nextSlot = 0;
                for (int i = 0; i < icons.Count; i++)
                {
                    IconDetails icon = icons.ElementAt(i).Value;
                    
                    int len = config.sdfSize * config.sdfSize;
                    int newRow = icon.slot / slots[0];
                    int newCol = icon.slot % slots[0];
                    for (int r = 0; r < config.sdfSize; r++)
                    {
                        srcInx = oldDimensions[0] * (icon.row * config.sdfSize)
                            + icon.col * config.sdfSize + r * oldDimensions[0];
                        destInx = atlasTextureDimensions[0] * (newRow * config.sdfSize)
                            + newCol * config.sdfSize + r * atlasTextureDimensions[0];

                        for (int v = 0; v < config.sdfSize; v++)
                        {
                            textureColorsNew[destInx++] = atlasTextureColors[srcInx++];
                        }
                    }
                    
                    icon.row = newRow;
                    icon.col = newCol;
                    nextSlot = Math.Max(nextSlot, icon.slot);

                }
                nextSlot++;
                atlasTextureColors = textureColorsNew;
                atlasTexture.SetColors(atlasTextureDimensions[0], atlasTextureDimensions[1], atlasTextureColors);
            }
            else
            {
                atlasTextureColors = new Color32[atlasTextureDimensions[0] * atlasTextureDimensions[1]];
                nextSlot = 0;
                icons.Clear();
            }


            uvSize.x = (float)config.sdfSize / (float)atlasTextureDimensions[0];
            uvSize.y = (float)config.sdfSize / (float)atlasTextureDimensions[1];

        }

        static public void ClearCache()
        {
            SetConfig(config);
            GenerateSDF.ClearCache();
        }


        // We have finished generating a new SDF icon
        static public void IconGenerated(string fontFile, int codePoint)
        {
            if (atlasTexture == null)
                SetConfig(null);

            if (atlasTexture != null)
                atlasTexture.SetColors(atlasTextureDimensions[0], atlasTextureDimensions[1], atlasTextureColors);

            // Set the icon as ready to draw
            string key = fontFile + ":" + codePoint;
            if (icons.ContainsKey(key))
                icons[key].iconStatus = IconStatus.readyToDraw;
            
            // We are now ready to process another icon
            generating = false;
        }

        public enum IconStatus
        {
            waiting,
            generating,
            readyToDraw,
            drawing

        }
        public class IconDetails
        {
            public int slot;
            public int row;
            public int col;
            public float timeLastDrawn;
            public IconStatus iconStatus = IconStatus.waiting;
        }

        static IconCache()
        {
            shader = Shader.FromFile("sdf.hlsl");
            shaderPBR = Shader.FromFile("sdfPBR.hlsl");
            SetConfig(null);
        }

        static public bool generating = false;
        static async Task Generate(string fontFile, int codePoint)
        {
            generating = true;
            Font f = Font.FromFile(fontFile);
            await Task.Run(() => GenerateSDF.Generate(fontFile, codePoint));
        }

        static private int nextSlot = 0;
        // Decide where the icon will be stored in the atlas
        static void AssignSlot(string key)
        {
            int newSlot = 0;
            // If there are slots free, use them
            if (icons.Count < slotsCount)
            {
                newSlot = nextSlot;
                nextSlot++;
            }
            else
            {
                // No slots are free, so are their any slots that have not been used for a while
                string oldestKey = "";
                float oldestTime = float.MaxValue;

                foreach (KeyValuePair<string, IconDetails> kvp in icons)
                {
                    if (kvp.Value.timeLastDrawn < oldestTime)
                    {
                        oldestTime = kvp.Value.timeLastDrawn;
                        oldestKey = kvp.Key;
                        newSlot = kvp.Value.slot;
                    }
                }
                // If one of the slots is now redundant, reuse that slot
                if (Time.Totalf - oldestTime > config.timeout || (atlasTextureDimensions[0] == maxAtlasWidth && atlasTextureDimensions[1] == maxAtlasHeight)) // Max size of 2048x1024 = 128 icons
                    icons.Remove(oldestKey);
                else
                {
                    // Grow the atlas texture size
                    SetConfig(config, true);
                    newSlot = nextSlot++;
                }
            }

            // Assign the reused or new slot
            icons.Add(key, new IconDetails() {
                slot = newSlot,
                col = newSlot % slots[0],
                row =  newSlot / slots[0]
            });

        }

        // Draw an icon and return true if the material has been recreated so any additional settings can be added
        // If you change useLighting, you also need to set material to null to force it to be recreated 
        static public bool DrawIcon(ref Material? material, string fontFile, int codePoint, Matrix matrix, Color color, bool useLighting = false)
        {
            bool materialCreated = false;

            // An icon can be reused so we store its key
            string key = fontFile + ":" + codePoint;
            int requestedSlot = -1;
            if (icons.ContainsKey(key))
                requestedSlot = icons[key].slot;
            else
                AssignSlot(key);
            
            IconDetails icon = icons[key];

            // Generate the sdf if require and we are not already creating one
            if (icon.iconStatus == IconStatus.waiting && generating == false)
            {
                icon.iconStatus = IconStatus.generating;
                generatingIcon = icon;
                _ = Generate(fontFile, codePoint);
            }

            if (icon.iconStatus >= IconStatus.readyToDraw)
            {
                // Create the new material if required
                if (material == null)
                {
                    if (useLighting)
                        material = new Material(shaderPBR);
                    else
                        material = new Material(shader);
                    material.SetTexture("sdfTexture", atlasTexture);
                    material.Transparency = Transparency.Blend;
                    material.FaceCull = Cull.None;
                    material.DepthWrite = false;
                    material.DepthTest = default;
                    material.Transparency = Transparency.Blend;
                    materialCreated= true;
                }
                icon.iconStatus = IconStatus.drawing;
            }



            // Draw the icon if we have a valid material
            if (material != null)
            {
                // We update the slot if a new icon has just finished being generated
                if (icon.iconStatus == IconStatus.readyToDraw)
                {
                    material.SetFloat("slot", icon.slot);
                    icon.iconStatus = IconStatus.drawing;
            }

                // The material "slot" variable stores the slot we are actually drawing
                // We continue drawing from an old slot while a new sdf and slot are being prepared
                // This prevents ficker when we change between icons
                int s = (int)material.GetFloat("slot");
                if (requestedSlot != s && icon.iconStatus == IconStatus.drawing)
                    material.SetFloat("slot", requestedSlot);

                // We always reset these details because the atlasTexture size
                // may have changed and moved the icons around
                
                material.SetFloat("row", s / slots[0]);
                material.SetFloat("col", s % slots[0]);
                material.SetFloat("rowHeight", uvSize.y);
                material.SetFloat("colWidth", uvSize.x);
                
                material.SetColor("color", color);
                Mesh.Quad.Draw(material, Matrix.TRS(matrix.Pose.position, matrix.Pose.orientation, matrix.Scale));
                icons[key].timeLastDrawn = Time.Totalf;
            }

            return materialCreated;

        }
    }

    static internal class IconCacheTest
    {
        private class DrawTestIcon
        {
            public int codePoint;
            public Pose windowPose;
            public string fontFile;
            public Material? material;
            public DrawTestIcon(string font, int code, Pose pose)
            {
                fontFile=font;
                material = null;
                codePoint = code;
                windowPose = pose;
            }
        }

        static private List<DrawTestIcon>? testIcons = null;
        static private Bounds textIconBounds = new Bounds(V.XYZ(.2f,.2f,.01f));
        static public void AddTextIcon(string fontFile, int codePoint, Pose pose)
        {
            if (testIcons == null) {
                testIcons = new List<DrawTestIcon>();

            }
            testIcons.Add(new DrawTestIcon(fontFile, codePoint, pose));
        }

        static public void DrawTestIcons()
        {
            if (testIcons == null)
                return;

            int i=0;
            foreach (DrawTestIcon icon in testIcons)
            {
                IconCache.DrawIcon(ref icon.material,icon.fontFile,icon.codePoint,icon.windowPose.ToMatrix(.2f),Color.HSV(.5f,.5f,.5f),false);
                UI.Handle("textIcon" + i++, ref icon.windowPose,textIconBounds);
            }

        }        
    }    
    unsafe static internal class GenerateSDF
    {
        const bool cacheFont = true;
        static public void ClearCache()
        {
            fonts.Clear();
        }

        struct FontCache
        {
            public stbtt_fontinfo info;
            public float scale;
            public byte[] bytes;
        }
        static private Dictionary<string, FontCache> fonts = new Dictionary<string, FontCache>();
        static public void Generate(string fontFile, int codePoint)
        {
            if (IconCache.atlasTextureColors == null)
                return;

            byte onedge_value = 128;
            int pixelDistScale = 80;
            int sdfSize = IconCache.SdfSize;
            if (sdfSize <= 64)
                pixelDistScale = 100;
            int padding = 3;

            FontCache fontCache = new FontCache();
            // Load and initialise the font file if not previously used
            if (fonts.ContainsKey(fontFile))
                fontCache = fonts[fontFile];
            else
            {
                fontCache.bytes = Platform.ReadFileBytes(fontFile);

                fontCache.info = new stbtt_fontinfo();
                fixed (byte* ptr = fontCache.bytes)
                {
                    fontCache.info.data = ptr;
                    var res = stbtt_InitFont(fontCache.info, ptr, 0);
                    fontCache.scale = stbtt_ScaleForPixelHeight(fontCache.info, sdfSize);
                }
                if (cacheFont)
                    fonts.Add(fontFile, fontCache); 
            }

            int xoff, yoff, width, height;
            byte* generatedTexture;
            fixed (byte* ptr = fontCache.bytes)
            {
                fontCache.info.data = ptr;
                generatedTexture = stbtt_GetCodepointSDF(fontCache.info, fontCache.scale, codePoint, padding, onedge_value, pixelDistScale, &width, &height, &xoff, &yoff);
            }

            if (width == 0 || height == 0)
                return;

            // Pad or truncate to even bytes to ensure it works on Android
            int sourceSize = width * height;
            int adjustWidthAt = sdfSize;
            int adjustmentForWidth = 0;

            adjustmentForWidth = width - sdfSize;
            // If w = 65 and config.sdfSize = 64, we need to ignore 1 byte in the source
            // If w = 63 and config.sdfSize = 64, we need to add an extra byte to the target
            if (width < sdfSize)
                adjustWidthAt = width;

            int targetSize = sdfSize * sdfSize;
            
            int col;
            int row;
            
            int destInx = IconCache.atlasTextureDimensions[0] * (IconCache.generatingIcon.row * sdfSize) 
                + IconCache.generatingIcon.col * sdfSize;
            
            int rowStepSize = IconCache.atlasTextureDimensions[0] - sdfSize;

            int srcInx = 0;
            int i = 0;
            while (i < targetSize && destInx < IconCache.atlasTextureDimensions[0] * IconCache.atlasTextureDimensions[1])
            {
                col = srcInx % width;
                row = srcInx / width;
                if (srcInx < sourceSize)
                    IconCache.atlasTextureColors[destInx] = new Color32(generatedTexture[srcInx], generatedTexture[srcInx], generatedTexture[srcInx],255);
                else
                    IconCache.atlasTextureColors[destInx] = Color32.Black;
                destInx++;
                srcInx++;
                i++;

                // Ensure texture is padded/trunctated to even number - for Android but do it always to ensure tested
                if (col == adjustWidthAt -1)
                {
                    if (adjustmentForWidth > 0)
                        srcInx += adjustmentForWidth; //Ignore extra width
                    if (adjustmentForWidth < 0)
                    {
                        for (int j = 0; j < Math.Abs(adjustmentForWidth); j++)
                        {
                            IconCache.atlasTextureColors[destInx] = Color32.Black;
                            destInx++;
                            i++;
                        }
                    }
                    destInx += rowStepSize;
                }

            }
            IconCache.IconGenerated(fontFile, codePoint);

        }

    }


}
