using StereoKit;
using System;
using System.Collections.Generic;
using System.Text;

namespace JumbliVR
{
    static internal class Hands
    {
        class Properties
        {
            public float visible = 1;  // 1 == visible
            public Color color = Color.White;
            public float colorOverridden = 0; // 1 == overridden
            public Material material = Material.Unlit.Copy();
        }
        static private Properties[] properties = { new Properties(), new Properties() };

        static public Color defaultColor = Color.White;

        public static void DrawHands()
        {
            Properties p;

            for (int i = 0; i < 2; i++)
            {

                p = properties[i];

                // Handle visibility
                Input.HandVisible((Handed)i, p.visible > 0);
                p.visible += Time.Stepf;

                // Handle color or opacity changes
                if (p.colorOverridden > 0 && p.colorOverridden <= .9f)
                    p.material.SetColor("color", Color.Lerp(defaultColor, p.color, MathF.Max(0, p.colorOverridden)));
                p.colorOverridden -= Time.Stepf;
            }

        }

        // Repeatedly call this routine during an interaction to keep the hand invivisble
        // Call it directly after a UI component with forceHide set to false to only hide the hand when being used
        static public void HideHand(bool forceHide = false, Handed handed = Handed.Max)
        {
            for (int i = 0; i < 2; i++) {
                if ((int)handed == i || handed == Handed.Max)
                    if (forceHide || 
                        (UI.LastElementActive.IsActive() && UI.LastElementHandUsed((Handed)i).IsActive()))
                    properties[i].visible = 0;
            }
        }

        static public void ForceHideBothHands()
        {
            properties[0].visible = properties[1].visible = 0;
        }

        static public bool IsHandVisible(Handed handed)
        {
            return properties[(int)handed].visible >= 1;
        }

        
        static public void SetHandMaterial(Material newHandMaterial, Color newDefaultColor)
        {
            newHandMaterial.SetColor("color",newDefaultColor);
            defaultColor = newDefaultColor;
            Input.Hand(Handed.Left).Material = properties[0].material = newHandMaterial.Copy();
            Input.Hand(Handed.Right).Material = properties[1].material = newHandMaterial.Copy();

        }

        // Keep calling FadeHands while you need the hand to be faded
        static public void OverrideColorAndOpacity(Color newColor, Handed handed = Handed.Max)
        {
            Properties p;
            for (int i = 0; i < 2; i++)
            {
                if ((int)handed == i || handed == Handed.Max)
                {
                    p = properties[(int)handed];
                    p.color = newColor;
                    p.colorOverridden = 1;
                    p.material.SetColor("color", newColor);
                }
            }
        }

        static public void OverrideOpacity(float opacity, Handed handed = Handed.Max)
        {
            Properties p;
            for (int i = 0; i < 2; i++)
            {
                if ((int)handed == i || handed == Handed.Max)
                {
                    p = properties[(int)handed];
                    p.color.a = opacity;
                    p.colorOverridden = 1;
                    p.material.SetColor("color", p.color);
                }
            }
        }

        static public Material CreateMaterial()
        {
            Material material = Material.Unlit.Copy();
            Gradient color_grad = new Gradient();
            color_grad.Add(new Color (1f,1f,1f,0) , 0.0f);
            color_grad.Add(new Color (1f,1f,1f,0), 0.5f);
            color_grad.Add(new Color(1f,1f,1f,.75f), .75f);

            Color32[] gradient = new Color32[16*16];
            for (int y = 0; y < 16; y++)
            {
                Color32 col = color_grad.Get32(1 - y / 15.0f);
                for (int x = 0; x < 16; x++)
                {
                    gradient[x + y * 16] = col;
                }
            }

            Tex gradient_tex = new Tex(TexType.Image,TexFormat.Rgba32);
            gradient_tex.SetColors(16, 16, gradient);
            gradient_tex.AddressMode = TexAddress.Clamp;
            material.SetTexture("diffuse", gradient_tex);
            material.QueueOffset = 10;
            material.Transparency = Transparency.Blend;

            material.SetColor("color", Color.White);
            return material;

        }
    }
}
