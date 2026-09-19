using UnityEngine;

namespace Combat.Common
{
    /// <summary>
    /// Статический поставщик общих процедурных спрайтов и текстур.
    /// Исключает дублирование создания Texture2D и Sprite в отдельных компонентах.
    /// </summary>
    public static class CombatSprites
    {
        private static Sprite _whiteBox;

        /// <summary>
        /// Кэшированный чистый белый спрайт размером 1x1 юнит с центром (0.5, 0.5).
        /// </summary>
        public static Sprite WhiteBox
        {
            get
            {
                if (_whiteBox == null)
                {
                    var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    var cols = new Color[16];
                    for (int i = 0; i < 16; i++) cols[i] = Color.white;
                    tex.SetPixels(cols);
                    tex.Apply();
                    tex.filterMode = FilterMode.Bilinear;

                    _whiteBox = Sprite.Create(
                        tex,
                        new Rect(0f, 0f, 4f, 4f),
                        new Vector2(0.5f, 0.5f),
                        4f
                    );
                    _whiteBox.name = "Combat_WhiteBox_Procedural";
                }
                return _whiteBox;
            }
        }
    }
}
