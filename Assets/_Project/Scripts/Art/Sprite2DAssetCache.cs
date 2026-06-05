using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.Art
{
    /// <summary>
    /// 完整 2D 像素美术资产缓存（64×64 角色 / 32×32 tile）。
    /// 100% 程序化生成——无外部依赖。所有 sprite 在此集中管理。
    /// </summary>
    public static class Sprite2DAssetCache
    {
        // ══════════════════════════════════════════════════════
        // 角色（每职业×4向×3帧+死亡帧 = 每职业13个sprite）
        // ══════════════════════════════════════════════════════
        public static readonly Dictionary<Online.OnlineProfession, ProfSpriteSet> CharacterSets = new();

        // 默认角色（fallback）
        public static Sprite CharBody_Front, CharBody_Back, CharBody_Left, CharBody_Right;
        public static Sprite CorpseMarker;
        public static Sprite CharDirectionArrow;
        public static Sprite CharGhostOverlay;

        // ══════════════════════════════════════════════════════
        // 地图 Tile（32×32）
        // ══════════════════════════════════════════════════════
        public static Sprite FloorWood, FloorConcrete, FloorMetal, FloorCarpet, FloorTile;
        public static Sprite WallBrick, WallConcrete, WallStripe;
        public static Sprite FloorTileAlt, CorridorTile;
        public static Sprite WallBlock;

        // ══════════════════════════════════════════════════════
        // 道具（32×32）
        // ══════════════════════════════════════════════════════
        public static Sprite PropCrate, PropBarrel, PropDesk, PropCabinet, PropEvidenceBox;
        public static Sprite VentIcon, CameraIcon;

        // ══════════════════════════════════════════════════════
        // VFX / 特效
        // ══════════════════════════════════════════════════════
        public static Sprite TaskGlow, BloodSplatter;

        // ══════════════════════════════════════════════════════
        // UI
        // ══════════════════════════════════════════════════════
        public static Sprite PanelBg, ButtonBg, AvatarFrame, VoteCardBg;

        private static bool _initialized;

        public static void Ensure()
        {
            if (_initialized) return;
            _initialized = true;

            // ─── 角色 ───
            GenerateAllCharacterSets();
            CharDirectionArrow = DrawArrow(16);
            CharGhostOverlay = DrawGhostOverlay(64);

            // ─── 地图 ───
            FloorWood      = DrawFloorWood(32);
            FloorConcrete  = DrawFloorConcrete(32);
            FloorMetal     = DrawFloorMetal(32);
            FloorCarpet    = DrawFloorCarpet(32);
            FloorTile      = DrawFloorTileGrout(32);
            WallBrick      = DrawWallBrick(16);
            WallConcrete   = DrawWallConcrete(16);
            WallStripe     = DrawWallStripe(16);
            FloorTileAlt   = FloorCarpet;
            CorridorTile   = FloorConcrete;
            WallBlock      = WallBrick;

            // ─── 道具 ───
            PropCrate      = DrawCrate(32);
            PropBarrel     = DrawBarrel(32);
            PropDesk       = DrawDesk(32);
            PropCabinet    = DrawCabinet(32);
            PropEvidenceBox= DrawEvidenceBox(32);
            VentIcon       = DrawVentGrate(32);
            CameraIcon     = DrawCameraHousing(32);

            // ─── VFX ───
            TaskGlow   = DrawGlowRing(32, new Color(0.15f, 0.65f, 1f));
            BloodSplatter = DrawBloodPool(32);
            CorpseMarker  = DrawCorpse(32);

            // ─── UI ───
            PanelBg    = DrawPanel(8, new Color(0.04f, 0.05f, 0.07f, 0.85f), new Color(0.25f, 0.28f, 0.32f, 1f));
            ButtonBg   = DrawPanel(8, new Color(0.10f, 0.13f, 0.18f, 1f), new Color(0.35f, 0.38f, 0.42f, 1f));
            AvatarFrame= DrawAvatarFrame(64);
            VoteCardBg = DrawPanel(4, new Color(0.08f, 0.09f, 0.12f, 0.9f), new Color(0.30f, 0.33f, 0.37f, 1f));
        }

        // ═══════════════════════════════════════════════════════════════
        // 职业角色生成器（64×64 像素，4向×3帧+死亡）
        // ═══════════════════════════════════════════════════════════════

        private static void GenerateAllCharacterSets()
        {
            foreach (Online.OnlineProfession prof in System.Enum.GetValues(typeof(Online.OnlineProfession)))
            {
                Color main = ProfessionPalette.MainColor(prof);
                Color accent = ProfessionPalette.AccentColor(prof);
                var set = new ProfSpriteSet();

                // 正面：3帧行走
                set.Front_Frame0 = DrawCharFront(main, accent, 0);
                set.Front_Frame1 = DrawCharFront(main, accent, -1);
                set.Front_Frame2 = DrawCharFront(main, accent, 1);
                // 背面
                set.Back_Frame0  = DrawCharBack(main, accent, 0);
                set.Back_Frame1  = DrawCharBack(main, accent, -1);
                set.Back_Frame2  = DrawCharBack(main, accent, 1);
                // 侧面（左右镜像）
                set.Left_Frame0  = DrawCharSide(main, accent, 0, true);
                set.Left_Frame1  = DrawCharSide(main, accent, -1, true);
                set.Left_Frame2  = DrawCharSide(main, accent, 1, true);
                set.Right_Frame0 = DrawCharSide(main, accent, 0, false);
                set.Right_Frame1 = DrawCharSide(main, accent, -1, false);
                set.Right_Frame2 = DrawCharSide(main, accent, 1, false);
                // 死亡
                set.Dead = DrawCharDead(main);

                CharacterSets[prof] = set;
            }

            // 默认fallback
            var def = CharacterSets[Online.OnlineProfession.Inspector];
            CharBody_Front = def.Front_Frame0;
            CharBody_Back  = def.Back_Frame0;
            CharBody_Left  = def.Left_Frame0;
            CharBody_Right = def.Right_Frame0;
        }

        // ─── 正面角色（64×64）───
        static Sprite DrawCharFront(Color body, Color accent, int walkOffset)
        {
            var t = NewTex(64); var s = 64; int cx = 32, footY = 48 + walkOffset;
            Color dark = Dark(body, 0.4f), skin = Hex("#e8c39e"), clear = Color.clear;
            // 头
            FillCircle(t, cx, 12, 8, skin);
            FillCircle(t, cx, 10, 6, skin);
            // 帽子/头盔
            FillRect(t, cx-7, 3, 15, 6, body);
            FillRect(t, cx-5, 8, 11, 4, accent);
            // 身体
            FillRect(t, cx-6, 18, 12, 20, body);
            // 腰带
            FillRect(t, cx-7, 35, 14, 4, accent);
            // 手臂
            FillRect(t, cx-10, 20, 3, 14, dark);
            FillRect(t, cx+7, 20, 3, 14, dark);
            // 手
            FillRect(t, cx-11, 31 + walkOffset, 5, 4, skin);
            FillRect(t, cx+6, 31 - walkOffset, 5, 4, skin);
            // 腿
            int legSep = walkOffset;
            FillRect(t, cx-4 + legSep, 38, 4, 12, dark);
            FillRect(t, cx+1 - legSep, 38, 4, 12, dark);
            // 靴子
            FillRect(t, cx-5 + legSep, 48, 6, 4, Color.black);
            FillRect(t, cx+0 - legSep, 48, 6, 4, Color.black);

            t.Apply(); return Spr(t, 64);
        }

        static Sprite DrawCharBack(Color body, Color accent, int walkOffset)
        {
            var t = NewTex(64); int cx = 32;
            Color dark = Dark(body, 0.4f), clear = Color.clear;
            FillCircle(t, cx, 12, 8, body);
            FillRect(t, cx-7, 3, 15, 6, body);
            FillRect(t, cx-6, 18, 12, 20, dark);
            FillRect(t, cx-7, 35, 14, 4, accent);
            int l = walkOffset;
            FillRect(t, cx-4 + l, 38, 4, 12, dark);
            FillRect(t, cx+1 - l, 38, 4, 12, dark);
            FillRect(t, cx-5 + l, 48, 6, 4, Color.black);
            FillRect(t, cx+0 - l, 48, 6, 4, Color.black);
            t.Apply(); return Spr(t, 64);
        }

        static Sprite DrawCharSide(Color body, Color accent, int walkOffset, bool facingLeft)
        {
            var t = NewTex(64); int cx = 32;
            Color dark = Dark(body, 0.4f), skin = Hex("#e8c39e");
            // 头
            FillCircle(t, cx, 12, 8, skin);
            if (facingLeft) FillCircle(t, cx-3, 10, 3, skin); // 鼻子
            else FillCircle(t, cx+3, 10, 3, skin);
            // 帽
            FillRect(t, cx-6, 3, 13, 6, body);
            FillRect(t, cx-4, 8, 9, 4, accent);
            // 身体
            FillRect(t, cx-5, 18, 10, 20, body);
            FillRect(t, cx-6, 35, 12, 4, accent);
            // 手臂（朝向侧）
            if (facingLeft) { FillRect(t, cx-12, 20, 4, 14, dark); FillRect(t, cx+5, 20, 3, 14, dark); }
            else { FillRect(t, cx+8, 20, 4, 14, dark); FillRect(t, cx-8, 20, 3, 14, dark); }
            // 腿
            int l = walkOffset;
            FillRect(t, cx-3 + l, 38, 4, 12, dark);
            FillRect(t, cx-1 - l, 38, 4, 12, dark);
            FillRect(t, cx-4 + l, 48, 6, 4, Color.black);
            FillRect(t, cx-2 - l, 48, 6, 4, Color.black);
            t.Apply(); return Spr(t, 64);
        }

        static Sprite DrawCharDead(Color body)
        {
            var t = NewTex(64);
            Color dark = Dark(body, 0.5f), blood = Hex("#8b0000");
            // 身体横躺
            FillRect(t, 12, 28, 40, 12, dark);
            FillRect(t, 10, 26, 42, 4, body);
            // 头
            FillCircle(t, 48, 30, 7, Hex("#e8c39e"));
            // 血迹
            for (int i = 0; i < 80; i++)
            { int x = RandomRange(15, 55, i), y = RandomRange(25, 38, i+7);
              if (t.GetPixel(x, y).a > 0.1f) t.SetPixel(x, y, blood); }
            t.Apply(); return Spr(t, 64);
        }

        // ═══════════════════════════════════════════════════════════════
        // 地图 Tile 生成器（32×32）
        // ═══════════════════════════════════════════════════════════════

        static Sprite DrawFloorWood(int sz)
        {
            var t = NewTex(sz); Color a = Hex("#6b4226"), b = Hex("#7a5236"), c = Hex("#5c3618");
            for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                int plankY = y / 8;
                Color col = plankY % 3 == 0 ? a : plankY % 3 == 1 ? b : c;
                if (y % 8 < 2) col = Dark(col, 0.3f); // plank gap
                // wood grain noise
                float n = Mathf.PerlinNoise(x * 0.3f, y * 2f);
                if (n > 0.55f) col = Dark(col, 0.08f);
                t.SetPixel(x, y, col);
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawFloorConcrete(int sz)
        {
            var t = NewTex(sz); Color baseC = Hex("#8a8d91");
            for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.4f, y * 0.4f);
                Color col = n < 0.4f ? Dark(baseC, 0.1f) : n > 0.6f ? Light(baseC, 0.1f) : baseC;
                // small crack
                if (x == 10 && y > 14 && y < 22) col = Dark(col, 0.3f);
                if (x == 11 && y > 15 && y < 20) col = Dark(col, 0.2f);
                t.SetPixel(x, y, col);
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawFloorMetal(int sz)
        {
            var t = NewTex(sz); Color baseC = Hex("#708090");
            for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                bool grid = (x % 8 == 0 || y % 8 == 0);
                float n = Mathf.PerlinNoise(x * 0.2f, y * 0.2f);
                Color col = grid ? Dark(baseC, 0.25f) : baseC;
                col = n > 0.5f ? Light(col, 0.05f) : Dark(col, 0.05f);
                t.SetPixel(x, y, col);
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawFloorCarpet(int sz)
        {
            var t = NewTex(sz); Color baseC = Hex("#2d4a6e"), stripeC = Hex("#3a5f8a");
            for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                bool stripe = (y / 4) % 4 < 2;
                float n = Mathf.PerlinNoise(x * 0.5f, y * 0.5f);
                Color col = stripe ? baseC : stripeC;
                t.SetPixel(x, y, col);
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawFloorTileGrout(int sz)
        {
            var t = NewTex(sz); Color tileC = Hex("#d4c9a8"), grout = Hex("#8a8578");
            for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                bool isGrout = (x % 8 < 1 || y % 8 < 1);
                t.SetPixel(x, y, isGrout ? grout : tileC);
            }
            t.Apply(); return Spr(t, sz);
        }

        // ─── 墙壁 ───

        static Sprite DrawWallBrick(int sz)
        {
            var t = NewTex(sz); Color brick = Hex("#8b4513"), mortar = Hex("#4a3728");
            int bh = 4;
            for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                int row = y / bh; int off = (row % 2) * (sz / 4);
                int brickX = (x + off) % (sz / 2);
                bool isMortar = (y % bh == bh - 1) || brickX < 1 || brickX >= sz / 2 - 1;
                t.SetPixel(x, y, isMortar ? mortar : brick);
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawWallConcrete(int sz)
        {
            var t = NewTex(sz); Color c = Hex("#9a9ea3");
            for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.3f, y * 0.3f);
                t.SetPixel(x, y, n > 0.5f ? Light(c, 0.05f) : Dark(c, 0.05f));
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawWallStripe(int sz)
        {
            var t = NewTex(sz); Color a = Hex("#2a2a2a"), b = Hex("#ffaa00");
            for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                bool stripe = (y / 4) % 6 < 1;
                t.SetPixel(x, y, stripe ? b : a);
            }
            t.Apply(); return Spr(t, sz);
        }

        // ═══════════════════════════════════════════════════════════════
        // 道具
        // ═══════════════════════════════════════════════════════════════

        static Sprite DrawCrate(int sz)
        {
            var t = NewTex(sz); Color wood = Hex("#8b6914"), line = Hex("#5c4510");
            FillRect(t, 6, 6, 20, 18, wood);
            FillRect(t, 6, 6, 20, 1, line);
            FillRect(t, 6, 23, 20, 1, line);
            FillRect(t, 15, 6, 1, 18, line);
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawBarrel(int sz)
        {
            var t = NewTex(sz); Color body = Hex("#6b3a1f"), rim = Hex("#8b5a30");
            FillRect(t, 8, 4, 16, 24, body);
            FillRect(t, 6, 3, 20, 3, rim);
            FillRect(t, 6, 26, 20, 3, rim);
            FillRect(t, 10, 12, 1, 8, rim);
            FillRect(t, 18, 12, 1, 8, rim);
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawDesk(int sz)
        {
            var t = NewTex(sz); Color top = Hex("#5c4033"), leg = Hex("#3d2b1f");
            FillRect(t, 2, 3, 28, 5, top);
            FillRect(t, 4, 8, 3, 20, leg);
            FillRect(t, 25, 8, 3, 20, leg);
            FillRect(t, 2, 26, 28, 3, top);
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawCabinet(int sz)
        {
            var t = NewTex(sz); Color body = Hex("#708090"), handle = Hex("#d4d4d4");
            FillRect(t, 3, 0, 26, 28, body);
            FillRect(t, 3, 13, 26, 2, Dark(body, 0.3f));
            FillRect(t, 24, 5, 3, 1, handle);
            FillRect(t, 24, 17, 3, 1, handle);
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawEvidenceBox(int sz)
        {
            var t = NewTex(sz); Color box = Hex("#c4a43c"), label = Hex("#ffffff");
            FillRect(t, 4, 4, 24, 22, box);
            FillRect(t, 7, 13, 18, 5, label);
            FillRect(t, 12, 14, 2, 3, Color.red);
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawVentGrate(int sz)
        {
            var t = NewTex(sz); Color frame = Hex("#4a4a4a"), dark = Hex("#1a1a1a");
            FillRect(t, 0, 0, sz, sz, frame);
            for (int x = 3; x < sz-3; x+=4)
                FillRect(t, x, 3, 2, sz-6, dark);
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawCameraHousing(int sz)
        {
            var t = NewTex(sz); Color body = Hex("#3a3a3a"), lens = Hex("#1a5a8a"), glow = Hex("#00aaff");
            FillRect(t, 6, 2, 20, 10, body);
            FillCircle(t, 16, 18, 12, body);
            FillCircle(t, 16, 18, 8, lens);
            FillCircle(t, 16, 18, 3, glow);
            t.Apply(); return Spr(t, sz);
        }

        // ═══════════════════════════════════════════════════════════════
        // VFX
        // ═══════════════════════════════════════════════════════════════

        static Sprite DrawGlowRing(int sz, Color glow)
        {
            var t = NewTex(sz); float cx = sz/2f, inner = cx*0.5f, outer = cx*0.95f;
            for (int y=0;y<sz;y++) for (int x=0;x<sz;x++)
            {
                float d = Mathf.Sqrt((x-cx)*(x-cx)+(y-cx)*(y-cx));
                float a = (d>inner && d<outer) ? 1f - Mathf.Abs(d-(inner+outer)*0.5f)/((outer-inner)*0.5f) : 0f;
                t.SetPixel(x,y,new Color(glow.r,glow.g,glow.b,a*0.5f));
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawBloodPool(int sz)
        {
            var t = NewTex(sz); Color blood = Hex("#8b0000"), clear = Color.clear;
            for (int y=0;y<sz;y++) for (int x=0;x<sz;x++)
            {
                float dx=x-sz/2f,dy=y-sz/2f,d=Mathf.Sqrt(dx*dx+dy*dy);
                float n=Mathf.PerlinNoise(x*0.3f,y*0.3f);
                float a=(d<sz*0.35f&&n>0.3f)?0.7f:0f;
                t.SetPixel(x,y,new Color(blood.r,blood.g,blood.b,a));
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawCorpse(int sz)
        {
            var t = NewTex(sz); Color red = Hex("#cc0000");
            FillCircle(t,sz/2,sz/2,sz/2-2, new Color(1,1,1,0));
            // X mark
            for (int i=-sz/3;i<sz/3;i++) { SetPixelSafe(t,sz/2+i,sz/2+i,red); SetPixelSafe(t,sz/2+i,sz/2-i,red); }
            t.Apply(); return Spr(t, sz);
        }

        // ═══════════════════════════════════════════════════════════════
        // UI
        // ═══════════════════════════════════════════════════════════════

        static Sprite DrawPanel(int sz, Color fill, Color border)
        {
            var t = NewTex(sz);
            for (int y=0;y<sz;y++) for (int x=0;x<sz;x++)
            {
                bool isBorder = x < 1 || x >= sz-1 || y < 1 || y >= sz-1;
                t.SetPixel(x, y, isBorder ? border : fill);
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawAvatarFrame(int sz)
        {
            var t = NewTex(sz); Color frame = Hex("#f4a236");
            for (int y=0;y<sz;y++) for (int x=0;x<sz;x++)
            {
                float dx=x-sz/2f, dy=y-sz/2f, d=Mathf.Sqrt(dx*dx+dy*dy);
                bool onRing = d>sz*0.42f && d<sz*0.5f;
                t.SetPixel(x,y,onRing?frame:Color.clear);
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawGhostOverlay(int sz)
        {
            var t = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            for (int y=0;y<sz;y++) for (int x=0;x<sz;x++)
                t.SetPixel(x,y,new Color(0.5f,0.5f,0.6f,0.25f));
            t.Apply(); return Sprite.Create(t,new Rect(0,0,sz,sz),new Vector2(0.5f,0.5f),sz);
        }

        static Sprite DrawArrow(int sz)
        {
            var t = NewTex(sz); Color fill = Color.white; int cx = sz/2;
            for (int y=0;y<sz;y++) for (int x=0;x<sz;x++)
            {
                float dx = Mathf.Abs(x-cx); bool inArrow = y>=sz*0.15f && dx < y*0.5f;
                t.SetPixel(x,y,inArrow?fill:Color.clear);
            }
            t.Apply(); return Spr(t, sz);
        }

        // ═══════════════════════════════════════════════════════════════
        // 基础绘图工具
        // ═══════════════════════════════════════════════════════════════

        static Texture2D NewTex(int sz) => new Texture2D(sz, sz, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        static Sprite Spr(Texture2D t, int ppu) => Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), ppu);

        static void FillRect(Texture2D t, int x, int y, int w, int h, Color c)
        { for (int j=y;j<y+h&&j<t.height;j++) for (int i=x;i<x+w&&i<t.width;i++) if(i>=0&&j>=0)t.SetPixel(i,j,c); }

        static void FillCircle(Texture2D t, int cx, int cy, int r, Color c)
        { for (int y=cy-r;y<cy+r;y++) for (int x=cx-r;x<cx+r;x++)
        { if(x>=0&&y>=0&&x<t.width&&y<t.height && (x-cx)*(x-cx)+(y-cy)*(y-cy)<=r*r) t.SetPixel(x,y,c); }}

        static void SetPixelSafe(Texture2D t, int x, int y, Color c)
        { if(x>=0&&y>=0&&x<t.width&&y<t.height)t.SetPixel(x,y,c); }

        static Color Dark(Color c, float amt) => new Color(c.r*(1-amt), c.g*(1-amt), c.b*(1-amt), c.a);
        static Color Light(Color c, float amt) => new Color(c.r*(1+amt), c.g*(1+amt), c.b*(1+amt), c.a);
        static Color Hex(string h) { if(h.StartsWith("#"))h=h.Substring(1);
            return new Color(int.Parse(h.Substring(0,2),System.Globalization.NumberStyles.HexNumber)/255f,
                             int.Parse(h.Substring(2,2),System.Globalization.NumberStyles.HexNumber)/255f,
                             int.Parse(h.Substring(4,2),System.Globalization.NumberStyles.HexNumber)/255f,1f); }
        static int RandomRange(int min, int max, int seed) => min + (new System.Random(seed).Next() % (max-min));

        public class ProfSpriteSet
        {
            public Sprite Front_Frame0, Front_Frame1, Front_Frame2;
            public Sprite Back_Frame0, Back_Frame1, Back_Frame2;
            public Sprite Left_Frame0, Left_Frame1, Left_Frame2;
            public Sprite Right_Frame0, Right_Frame1, Right_Frame2;
            public Sprite Dead;
        }
    }
}
