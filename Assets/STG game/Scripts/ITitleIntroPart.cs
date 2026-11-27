// 開始畫面開場演出的一個元件 (光束、移動物件、粒子…)：由 TitleIntroSequence 每幀給同一個進度
public interface ITitleIntroPart
{
    /// <summary>progress 0 = 開場第一格，1 = 定格的最後一格</summary>
    void SetProgress(float progress);
}
