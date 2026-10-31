using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDefense.Game
{
    /// <summary>pixelfx 스프라이트 시트 하나(프레임 스프라이트 + 재생 정보).</summary>
    public sealed class PixelFxClip
    {
        public string name;
        public Sprite[] frames;
        public float fps;
        public bool loop;

        public float Duration { get { return frames.Length / fps; } }

        /// <summary>시작 후 t초의 프레임. 한 번 재생 효과가 끝났으면 null.</summary>
        public Sprite FrameAt(float t)
        {
            int i = Mathf.FloorToInt(t * fps);
            if (loop) i = ((i % frames.Length) + frames.Length) % frames.Length;
            else if (i >= frames.Length) return null;
            return frames[Mathf.Max(0, i)];
        }
    }

    /// <summary>
    /// pixelfx(https://github.com/patch-gg/pixelfx, MIT)의 스프라이트 시트를 읽는다.
    /// Resources/PixelFx/ 에 pixelfx의 `tools/render.mjs &lt;id&gt; --sheet` 결과(이름.png + 이름.json)를 그대로 넣으면 된다.
    /// 시트가 없으면 null을 돌려주고, 게임은 기존 도형 연출로 대신한다.
    /// </summary>
    public static class PixelFxLibrary
    {
        public const string ResourceFolder = "PixelFx/";
        /// <summary>시트 1픽셀의 월드 크기 기준(칸 하나 = 48px). 효과마다 배율을 따로 준다.</summary>
        public const float PixelsPerUnit = 48f;

        // 게임이 쓰는 효과 이름(시트 파일 이름)
        public const string HitSpark = "hit-spark";
        public const string Poof = "poof";
        public const string SnowflakeBurst = "snowflake-burst";
        public const string Splash = "splash";
        public const string CollectBurst = "collect-burst";
        public const string HeartLost = "heart-lost";
        public const string Burning = "burning";
        public const string Frozen = "frozen";

        static readonly Dictionary<string, PixelFxClip> cache = new Dictionary<string, PixelFxClip>();

        // pixelfx 시트 JSON(Aseprite 배열 형식)에서 필요한 부분만
        [Serializable] public class SheetJson { public FrameJson[] frames; public MetaJson meta; }
        [Serializable] public class FrameJson { public RectJson frame; public int duration; }
        [Serializable] public class RectJson { public int x; public int y; public int w; public int h; }
        [Serializable] public class SizeJson { public int w; public int h; }
        [Serializable] public class MetaJson { public SizeJson size; public PixelFxJson pixelfx; }
        [Serializable] public class PixelFxJson { public string id; public float fps; public bool loop; public int frames; public float[] pivot; public int columns; }

        /// <summary>효과 시트를 가져온다. 없으면 null(한 번 없으면 다시 찾지 않는다).</summary>
        public static PixelFxClip Get(string name)
        {
            PixelFxClip clip;
            if (cache.TryGetValue(name, out clip)) return clip;
            clip = Load(name);
            cache[name] = clip;
            return clip;
        }

        static PixelFxClip Load(string name)
        {
            var json = Resources.Load<TextAsset>(ResourceFolder + name);
            var tex = Resources.Load<Texture2D>(ResourceFolder + name);
            if (json == null || tex == null) return null;

            SheetJson sheet;
            try
            {
                sheet = JsonUtility.FromJson<SheetJson>(json.text);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KitchenDefense] pixelfx 시트 JSON을 읽지 못했습니다(" + name + "): " + e.Message);
                return null;
            }
            if (sheet == null || sheet.frames == null || sheet.frames.Length == 0) return null;

            // 픽셀 아트는 점 필터로. (임포트 설정은 Editor/PixelFxImport가 맞추지만 혹시 몰라 한 번 더)
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            // 텍스처가 임포트 중 다른 크기로 바뀌었어도 프레임이 맞도록 비율로 계산한다.
            var meta = sheet.meta;
            float srcW = meta != null && meta.size != null && meta.size.w > 0 ? meta.size.w : tex.width;
            float srcH = meta != null && meta.size != null && meta.size.h > 0 ? meta.size.h : tex.height;
            float sx = tex.width / srcW;
            float sy = tex.height / srcH;
            var info = meta != null ? meta.pixelfx : null;

            var frames = new Sprite[sheet.frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                var f = sheet.frames[i].frame;
                if (f == null || f.w <= 0 || f.h <= 0) return null;
                // JSON은 왼쪽 위 원점, Unity 텍스처는 왼쪽 아래 원점
                var rect = new Rect(f.x * sx, tex.height - (f.y + f.h) * sy, f.w * sx, f.h * sy);
                var pivot = new Vector2(0.5f, 0.5f);
                if (info != null && info.pivot != null && info.pivot.Length >= 2)
                    pivot = new Vector2(Mathf.Clamp01(info.pivot[0] / f.w), Mathf.Clamp01(1f - info.pivot[1] / f.h));
                frames[i] = Sprite.Create(tex, rect, pivot, PixelsPerUnit * sx, 0, SpriteMeshType.FullRect);
            }

            float fps = info != null && info.fps > 0f ? info.fps
                : (sheet.frames[0].duration > 0 ? 1000f / sheet.frames[0].duration : 30f);
            return new PixelFxClip { name = name, frames = frames, fps = fps, loop = info != null && info.loop };
        }
    }
}
