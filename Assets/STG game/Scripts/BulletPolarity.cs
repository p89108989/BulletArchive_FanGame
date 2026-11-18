// 子彈屬性 (極性)，之後玩家切換極性時也共用這個定義
// None = 無屬性，任何護盾都無法吸收；新值一律加在最後，避免已序列化的數值錯位
public enum BulletPolarity
{
    Blue,
    Yellow,
    None
}
