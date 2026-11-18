using System;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class EnemyBullet : MonoBehaviour
{
    [Header("Bullet")]
    [SerializeField, Tooltip("子彈飛行速度 (m/s)，由 Spawner 發射時覆寫")] private float speed = 15f;
    [SerializeField, Tooltip("子彈存活時間，超過會自動歸還物件池 (秒)")] private float lifeTime = 6f;
    [SerializeField, Tooltip("判定命中的目標 Tag")] private string hitTag = "Player";
    [SerializeField, Tooltip("命中後造成的傷害值")] private int damage = 1;

    private Vector3 moveDirection;
    private float lifeTimer;
    private Action<EnemyBullet> onDespawn;
    private BulletPolarityVisual polarityVisual;

    public BulletPolarity Polarity { get; private set; }

    private void Awake()
    {
        TryGetComponent(out polarityVisual);
    }

    // 由 Spawner 在從物件池取出後呼叫，設定飛行方向、速度、屬性與歸還物件池的回呼
    public void Launch(Vector3 direction, float launchSpeed, BulletPolarity polarity, Action<EnemyBullet> despawnCallback)
    {
        moveDirection = direction.normalized;
        speed = launchSpeed;
        onDespawn = despawnCallback;
        lifeTimer = lifeTime;
        transform.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);

        Polarity = polarity;
        if (polarityVisual != null) polarityVisual.Apply(polarity);
    }

    // 沿發射方向直線飛行，並倒數存活時間
    private void FixedUpdate()
    {
        transform.position += moveDirection * (speed * Time.fixedDeltaTime);

        lifeTimer -= Time.fixedDeltaTime;
        if (lifeTimer <= 0f)
        {
            Despawn();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(hitTag)) return;

        // 同極性的護盾會吸收子彈：直接回收，不造成傷害
        if (other.TryGetComponent(out IPolarityAbsorber absorber) && absorber.TryAbsorb(Polarity))
        {
            Despawn();
            return;
        }

        if (other.TryGetComponent(out IDamageable damageable))
        {
            damageable.TakeDamage(damage);
        }

        Despawn();
    }

    // 被玩家雷射等攻擊攔截時呼叫：直接回收，不造成任何效果
    public void Intercept()
    {
        Despawn();
    }

    private void Despawn()
    {
        onDespawn?.Invoke(this);
    }
}
