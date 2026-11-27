using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 戰鬥中的音效與角色語音：
// 音效可限制最短間隔 (同一聲音太密集時略過)；角色語音同時只講一句；循環音效 (直升機、戰車) 遊戲暫停時一起停；
// 敵人受傷 / 被擊毀有音效；角色語音依選到的角色 (找不到用第一組)；
// 玩家衝刺、受傷 (倒下那一下不算)、每隔一段時間有移動就講話；戰鬥開始、落敗、勝利結算各講一句
public class GameplayAudioTests
{
    private readonly List<Object> spawned = new List<Object>();
    private readonly List<AudioClip> played = new List<AudioClip>();

    [SetUp]
    public void SetUp()
    {
        GameAudio.OnSfxPlayed += played.Add;
    }

    [TearDown]
    public void TearDown()
    {
        GameAudio.OnSfxPlayed -= played.Add;
        played.Clear();
        Time.timeScale = 1f;
        foreach (Object o in spawned)
        {
            if (o != null) Object.Destroy(o);
        }

        spawned.Clear();
        foreach (GameAudio audio in Object.FindObjectsByType<GameAudio>(FindObjectsSortMode.None)) Object.Destroy(audio.gameObject);
    }

    private T Track<T>(T o) where T : Object
    {
        spawned.Add(o);
        return o;
    }

    private AudioClip Clip(string name)
    {
        return Track(AudioClip.Create(name, 4410, 1, 44100, false));
    }

    private GameObject NewInactive(string name)
    {
        GameObject go = Track(new GameObject(name));
        go.SetActive(false);
        return go;
    }

    [Test]
    public void Sfx_WithinMinInterval_IsSkipped()
    {
        AudioClip hit = Clip("Hit");
        GameAudio.PlaySfx(hit, 0.5f, 10f);
        GameAudio.PlaySfx(hit, 0.5f, 10f);

        Assert.AreEqual(1, played.Count);
    }

    [Test]
    public void SoundEffect_PlaysItsClip()
    {
        AudioClip shot = Clip("Shot");
        new SoundEffect(shot, 0.3f).Play();

        CollectionAssert.AreEqual(new[] { shot }, played);
    }

    // 同時只講一句：新語音蓋掉還在講的那句
    [Test]
    public void Voice_ReplacesTheCurrentLine()
    {
        AudioClip first = Clip("First");
        AudioClip second = Clip("Second");
        GameAudio.PlayVoice(first, 1f);
        GameAudio.PlayVoice(second, 1f);

        Assert.AreSame(second, GameAudio.CurrentVoice);
    }

    [UnityTest]
    public IEnumerator LoopingSound_PausesWhileGameIsPaused()
    {
        GameObject go = NewInactive("Loop");
        LoopingSound loop = go.AddComponent<LoopingSound>();
        TestUtil.SetField(loop, "clip", Clip("Engine"));
        go.SetActive(true);
        loop.SetPlaying(true);
        yield return null;
        AudioSource source = go.GetComponent<AudioSource>();
        Assert.IsTrue(source.isPlaying);

        Time.timeScale = 0f;
        yield return null;
        Assert.IsFalse(source.isPlaying, "paused with the game");

        Time.timeScale = 1f;
        yield return null;
        Assert.IsTrue(source.isPlaying, "resumes");

        loop.SetPlaying(false);
        yield return null;
        Assert.IsFalse(source.isPlaying);
    }

    [Test]
    public void Enemy_Hurt_And_Destroyed_PlaySounds()
    {
        AudioClip hurt = Clip("Hurt");
        AudioClip death = Clip("Death");
        GameObject go = NewInactive("Enemy");
        EnemyHealth enemy = go.AddComponent<EnemyHealth>();
        TestUtil.SetField(enemy, "maxHealth", 2);
        TestUtil.SetField(enemy, "hurtSound", new SoundEffect(hurt, 1f));
        TestUtil.SetField(enemy, "deathSound", new SoundEffect(death, 1f));
        go.SetActive(true);

        enemy.TakeDamage(1);
        CollectionAssert.AreEqual(new[] { hurt }, played);

        enemy.TakeDamage(1);
        CollectionAssert.Contains(played, death);
    }

    // 選角場景沒選過 (直接開戰鬥) 或找不到對應的角色時，用第一組
    [Test]
    public void VoiceSet_FindsTheSelectedCharacter_OrFallsBackToFirst()
    {
        CharacterVoiceSet rio = NewVoiceSet("CH0331");
        CharacterVoiceSet toki = NewVoiceSet("CH0187");
        CharacterVoiceSet[] sets = { rio, toki };

        Assert.AreSame(toki, CharacterVoiceSet.Find(sets, "CH0187"));
        Assert.AreSame(rio, CharacterVoiceSet.Find(sets, "unknown"));
        Assert.AreSame(rio, CharacterVoiceSet.Find(sets, null));
    }

    private CharacterVoiceSet NewVoiceSet(string id)
    {
        CharacterVoiceSet set = Track(ScriptableObject.CreateInstance<CharacterVoiceSet>());
        TestUtil.SetField(set, "characterId", id);
        TestUtil.SetField(set, "battleStart", new SoundEffect(Clip(id + "_in"), 1f));
        TestUtil.SetField(set, "dash", new SoundEffect(Clip(id + "_dash"), 1f));
        TestUtil.SetField(set, "move", new SoundEffect(Clip(id + "_move"), 1f));
        TestUtil.SetField(set, "victory", new SoundEffect(Clip(id + "_victory"), 1f));
        TestUtil.SetField(set, "select", new SoundEffect(Clip(id + "_select"), 1f));
        TestUtil.SetField(set, "defeat", new SoundEffect(Clip(id + "_defeat"), 1f));
        TestUtil.SetField(set, "damage", new SoundEffect(Clip(id + "_damage"), 1f));
        TestUtil.SetField(set, "basicAttack", new SoundEffect(Clip(id + "_shot"), 1f));
        return set;
    }

    private PlayerVoice CreatePlayer(CharacterVoiceSet set, out PlayerHealth health, out PlayerMovement movement, float moveInterval)
    {
        GameObject go = NewInactive("Player");
        go.AddComponent<Rigidbody>();
        go.AddComponent<Animator>();
        go.AddComponent<CapsuleCollider>();
        health = go.AddComponent<PlayerHealth>();
        movement = go.AddComponent<PlayerMovement>();
        PlayerVoice voice = go.AddComponent<PlayerVoice>();
        TestUtil.SetField(voice, "voiceSets", new[] { set });
        TestUtil.SetField(voice, "moveCheckInterval", moveInterval);
        go.SetActive(true);
        return voice;
    }

    [UnityTest]
    public IEnumerator Player_Dash_And_Damage_Speak()
    {
        CharacterVoiceSet rio = NewVoiceSet("CH0331");
        CreatePlayer(rio, out PlayerHealth health, out PlayerMovement movement, 100f);
        yield return null;

        // 先受傷：衝刺有短暫無敵，順序反過來傷害會被擋掉
        health.TakeDamage(1);
        Assert.AreSame(rio.Damage.Clip, GameAudio.CurrentVoice);

        yield return new WaitForSeconds(health.InvincibilityDuration + 0.1f);
        movement.TryStartDash();
        Assert.AreSame(rio.Dash.Clip, GameAudio.CurrentVoice);
    }

    // 倒下那一下講的是落敗語音 (戰鬥流程負責)，不講受傷
    [UnityTest]
    public IEnumerator Player_FatalHit_DoesNotSayDamage()
    {
        CharacterVoiceSet rio = NewVoiceSet("CH0331");
        CreatePlayer(rio, out PlayerHealth health, out _, 100f);
        yield return null;

        health.TakeDamage(100);

        CollectionAssert.DoesNotContain(played, rio.Damage.Clip);
    }

    [UnityTest]
    public IEnumerator Player_MovedDuringTheInterval_SaysMoveLine()
    {
        CharacterVoiceSet rio = NewVoiceSet("CH0331");
        PlayerVoice voice = CreatePlayer(rio, out _, out _, 0.2f);
        voice.GetComponent<Rigidbody>().isKinematic = true;
        yield return new WaitForSeconds(0.3f);
        CollectionAssert.DoesNotContain(played, rio.Move.Clip, "standing still");

        voice.transform.position += Vector3.right * 5f;
        yield return new WaitForSeconds(0.3f);
        CollectionAssert.Contains(played, rio.Move.Clip);
    }

    [UnityTest]
    public IEnumerator CharacterSelect_PickingACharacter_SaysTheirSelectLine()
    {
        CharacterVoiceSet rio = NewVoiceSet("CH0331");
        CharacterVoiceSet toki = NewVoiceSet("CH0187");
        GameObject tokiObject = NewInactive("CH0187");
        SelectableCharacter tokiCharacter = tokiObject.AddComponent<SelectableCharacter>();
        TestUtil.SetField(tokiCharacter, "characterId", "CH0187");
        tokiObject.SetActive(true);
        GameObject go = NewInactive("Screen");
        CharacterSelectScreen screen = go.AddComponent<CharacterSelectScreen>();
        TestUtil.SetField(screen, "characters", new[] { tokiCharacter });
        TestUtil.SetField(screen, "voiceSets", new[] { rio, toki });
        TestUtil.SetField(screen, "gameSceneName", "");
        go.SetActive(true);
        yield return null;

        screen.PickCharacter(tokiCharacter);

        Assert.AreSame(toki.Select.Clip, GameAudio.CurrentVoice);
    }

    [UnityTest]
    public IEnumerator Flow_RaisesStartDefeatAndVictoryEvents()
    {
        GameObject go = NewInactive("Flow");
        BattleFlow flow = go.AddComponent<BattleFlow>();
        int started = 0;
        int victories = 0;
        flow.OnBattleStarted += () => started++;
        flow.OnVictoryResultsShown += () => victories++;
        go.SetActive(true);
        yield return null;
        yield return null;
        Assert.AreEqual(1, started);

        flow.ShowVictory();
        Assert.AreEqual(1, victories);
    }

    [UnityTest]
    public IEnumerator Flow_PlayerDies_RaisesDefeat()
    {
        GameObject playerObject = Track(new GameObject("Player"));
        PlayerHealth health = playerObject.AddComponent<PlayerHealth>();
        GameObject go = NewInactive("Flow");
        BattleFlow flow = go.AddComponent<BattleFlow>();
        TestUtil.SetField(flow, "playerHealth", health);
        int defeats = 0;
        flow.OnDefeated += () => defeats++;
        go.SetActive(true);
        yield return null;
        yield return null;

        health.TakeDamage(100);

        Assert.AreEqual(1, defeats);
    }
}
