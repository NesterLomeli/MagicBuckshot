using UnityEngine;

// smoke coming off the gatling staff when it's hot. the more you shoot the more it smokes
// and it keeps smoking for a bit after you stop until the barrel cools down
public class BarrelSmoke : MonoBehaviour
{
    [SerializeField] private PlayerGun gun;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Sprite puffSprite;
    [Tooltip("Puffs per second at full heat.")]
    [SerializeField] private float maxRate = 28f;
    [SerializeField] private float minHeat = 0.05f;

    private float timer;

    private void Update()
    {
        if (gun == null || muzzle == null || puffSprite == null) return;
        WeaponData w = gun.CurrentWeapon;
        float heat = gun.Heat;
        if (w == null || heat < minHeat) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = 1f / Mathf.Max(1f, maxRate * heat);   // hotter = puffs come out faster

        Color c = new Color(0.75f, 0.75f, 0.78f);
        if (w.smokeColors != null && w.smokeColors.Length > 0)
            c = Color.Lerp(w.smokeColors[Random.Range(0, w.smokeColors.Length)], c, Random.Range(0.1f, 0.5f));
        c.a = Mathf.Lerp(0.35f, 0.75f, heat);

        Vector3 pos = muzzle.position + (Vector3)(Random.insideUnitCircle * 0.08f);
        SmokePuff puff = SmokePuff.Spawn(puffSprite, pos, c, Random.Range(0.2f, 0.32f), 9);
        puff.velocity = new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(0.6f, 1.1f));
        puff.growth = 0.55f;
        puff.drag = 0.5f;
        puff.lifetime = Random.Range(0.6f, 1.1f);
    }
}
