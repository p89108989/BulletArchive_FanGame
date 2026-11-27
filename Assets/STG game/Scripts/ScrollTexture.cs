using UnityEngine;

// 讓貼圖持續捲動 (例如流沙往場地下方流)：每幀改 _BaseMap_ST 的偏移量。
// 用 MaterialPropertyBlock，不會動到共用的材質資產
[RequireComponent(typeof(Renderer))]
public class ScrollTexture : MonoBehaviour
{
    [SerializeField, Tooltip("貼圖重複次數 (X, Y)")] private Vector2 tiling = Vector2.one;
    [SerializeField, Tooltip("每秒捲動的量 (貼圖 UV，1 = 一整張)")] private Vector2 speed = new Vector2(0f, -1f);

    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

    private Renderer target;
    private MaterialPropertyBlock block;
    private Vector2 offset;

    private void Awake()
    {
        target = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
    }

    private void Update()
    {
        // 只保留小數部分，長時間捲動也不會累積成很大的數字而失去精度
        offset.x = Mathf.Repeat(offset.x + speed.x * Time.deltaTime, 1f);
        offset.y = Mathf.Repeat(offset.y + speed.y * Time.deltaTime, 1f);

        target.GetPropertyBlock(block);
        block.SetVector(BaseMapStId, new Vector4(tiling.x, tiling.y, offset.x, offset.y));
        target.SetPropertyBlock(block);
    }
}
