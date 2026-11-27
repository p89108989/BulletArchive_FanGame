#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// NGUI 圖集定義的讀取與座標轉換 (純邏輯)：蔚藍檔案匯出的 *.asset 因為沒有 NGUI 程式碼無法直接載入，
// 這裡直接讀 YAML 文字裡的 mSprites 清單 (名稱、像素範圍、九宮格邊界)。
// NGUI 的像素座標原點在左上；Unity sprite 的 rect 原點在左下、border 順序為 (左, 下, 右, 上)
public static class NguiAtlas
{
    public struct Entry
    {
        public string name;
        public RectInt rect;
        public int borderLeft;
        public int borderRight;
        public int borderTop;
        public int borderBottom;
    }

    private static readonly Regex SpritePattern = new Regex(
        @"- name: (?<name>[^\r\n]*)\s+x: (?<x>\d+)\s+y: (?<y>\d+)\s+width: (?<w>\d+)\s+height: (?<h>\d+)" +
        @"\s+borderLeft: (?<bl>\d+)\s+borderRight: (?<br>\d+)\s+borderTop: (?<bt>\d+)\s+borderBottom: (?<bb>\d+)");

    public static Entry[] Parse(string atlasYaml)
    {
        List<Entry> entries = new List<Entry>();
        foreach (Match match in SpritePattern.Matches(atlasYaml))
        {
            entries.Add(new Entry
            {
                name = match.Groups["name"].Value.Trim(),
                rect = new RectInt(Int(match, "x"), Int(match, "y"), Int(match, "w"), Int(match, "h")),
                borderLeft = Int(match, "bl"),
                borderRight = Int(match, "br"),
                borderTop = Int(match, "bt"),
                borderBottom = Int(match, "bb"),
            });
        }

        return entries.ToArray();
    }

    public static Rect ToUnityRect(RectInt nguiRect, int textureHeight)
    {
        return new Rect(nguiRect.x, textureHeight - nguiRect.y - nguiRect.height, nguiRect.width, nguiRect.height);
    }

    public static Vector4 ToUnityBorder(Entry entry)
    {
        return new Vector4(entry.borderLeft, entry.borderBottom, entry.borderRight, entry.borderTop);
    }

    private static int Int(Match match, string group) => int.Parse(match.Groups[group].Value);
}
#endif
