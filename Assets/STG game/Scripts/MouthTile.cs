using UnityEngine;

// BA 角色的嘴型編號 (動畫事件 SetMouthTile 的參數)：百位數 = 第幾欄 (嘴型種類)、個位數 = 由上往下第幾列 (張開程度)，
// 都從 0 起算，對應 8×8 的共用嘴型圖集 (Character_Mouth_High 等)。
// 圖集每一欄是同一種嘴型、往下越張越開，所以說話時 401 ⇄ 402 就是同一種嘴型小開 ⇄ 大一點
public static class MouthTile
{
    public const int GridSize = 8;

    public static bool IsValid(int code)
    {
        int column = code / 100;
        int row = code % 100;
        return code >= 0 && column < GridSize && row < GridSize;
    }

    // 圖集上的像素範圍，原點在左上 (圖片裁切用)；atlasSize 為圖集邊長 (正方形)
    public static RectInt ToAtlasPixels(int code, int atlasSize)
    {
        int tileSize = atlasSize / GridSize;
        int column = code / 100;
        int row = code % 100;
        return new RectInt(column * tileSize, row * tileSize, tileSize, tileSize);
    }
}
