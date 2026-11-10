using UnityEngine;

namespace EightBitMode
{
    public static class TextureProcessor
    {
        private struct PresetInfo
        {
            public int divisor;   // >1: shrink by this factor
            public int maxSide;   // >0: cap the longest side
            public int bitsR, bitsG, bitsB; // 0 = no color reduction
        }

        private static PresetInfo Get(Preset p)
        {
            switch (p)
            {
                case Preset.Half:     return new PresetInfo { divisor = 2 };
                case Preset.Quarter:  return new PresetInfo { divisor = 4 };
                case Preset.EightBit: return new PresetInfo { maxSide = 32, bitsR = 3, bitsG = 3, bitsB = 2 };
                case Preset.FourBit:  return new PresetInfo { maxSide = 16, bitsR = 1, bitsG = 2, bitsB = 1 };
                default:              return new PresetInfo();
            }
        }

        public static Texture2D Process(Texture2D src, string name, Preset preset, bool destroyOriginal = true)
        {
            PresetInfo p = Get(preset);
            int w = src.width, h = src.height;
            int nw = w, nh = h;

            if (p.divisor > 1)
            {
                nw = Mathf.Max(1, w / p.divisor);
                nh = Mathf.Max(1, h / p.divisor);
            }
            else if (p.maxSide > 0 && Mathf.Max(w, h) > p.maxSide)
            {
                // Keep the aspect ratio. Main texture and its _m mask shrink identically.
                float s = (float)p.maxSide / Mathf.Max(w, h);
                nw = Mathf.Max(1, Mathf.RoundToInt(w * s));
                nh = Mathf.Max(1, Mathf.RoundToInt(h * s));
            }

            // Masks (_m) store channel data, not colors, so never quantize them.
            bool isMask = name != null && name.EndsWith("_m");
            bool quantize = p.bitsR > 0 && !isMask;

            if (nw == w && nh == h && !quantize) return src;

            RenderTexture prev = RenderTexture.active;
            FilterMode oldFilter = src.filterMode;
            src.filterMode = FilterMode.Bilinear;

            // Halve repeatedly with bilinear sampling (a clean 2x2 average each step)
            // so big reductions don't alias, then do the final resize.
            Texture source = src;
            RenderTexture current = null;
            int cw = w, ch = h;
            while (cw / 2 >= nw && ch / 2 >= nh)
            {
                cw /= 2; ch /= 2;
                RenderTexture next = RenderTexture.GetTemporary(cw, ch, 0, RenderTextureFormat.ARGB32);
                next.filterMode = FilterMode.Bilinear;
                Graphics.Blit(source, next);
                if (current != null) RenderTexture.ReleaseTemporary(current);
                current = next;
                source = next;
            }

            RenderTexture final = RenderTexture.GetTemporary(nw, nh, 0, RenderTextureFormat.ARGB32);
            final.filterMode = FilterMode.Bilinear;
            Graphics.Blit(source, final);
            if (current != null) RenderTexture.ReleaseTemporary(current);

            var result = new Texture2D(nw, nh, TextureFormat.RGBA32, false);
            RenderTexture.active = final;
            result.ReadPixels(new Rect(0, 0, nw, nh), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(final);

            if (quantize)
            {
                Color32[] px = result.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    px[i].r = Q(px[i].r, p.bitsR);
                    px[i].g = Q(px[i].g, p.bitsG);
                    px[i].b = Q(px[i].b, p.bitsB);
                }
                result.SetPixels32(px);
            }

            result.name = src.name;
            result.filterMode = FilterMode.Point;   // crisp pixels
            result.wrapMode = TextureWrapMode.Clamp;
            result.anisoLevel = 0;
            result.Apply(false, true);              // no mipmaps, drop CPU copy to save RAM

            if (destroyOriginal) Object.Destroy(src); else src.filterMode = oldFilter;                    // free the full-size original
            return result;
        }

        // Snap a channel to 2^bits levels, rounding to nearest (a plain bit-shift would darken everything).
        private static byte Q(byte v, int bits)
        {
            if (bits >= 8) return v;
            int levels = (1 << bits) - 1;
            return (byte)(Mathf.RoundToInt(v / 255f * levels) * 255 / levels);
        }
    }
}
