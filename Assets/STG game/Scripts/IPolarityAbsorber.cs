// 能依極性吸收子彈的對象 (例如開著護盾的玩家)；子彈命中時先詢問，吸收成功就不造成傷害
public interface IPolarityAbsorber
{
    bool TryAbsorb(BulletPolarity bulletPolarity);
}
