using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
// basically controls how the enemies behave when interacting with the player
// Idle/Patrol -> hears a shot -> Investigate -> sees Skeeter -> Chase -> loses him -> Search -> Return home.
// knights run in and swing after a red wind-up, wizards keep their distance and cast
// also handles health, getting hit, dying, ambushers and the guests at the feast
public class EnemyAI : MonoBehaviour, IDamageable
{
    private enum State { Idle, Patrol, Investigate, Chase, Search, Return, Dead }

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float investigateSpeed = 3.2f;
    [SerializeField] private float chaseSpeed = 3.8f;
    [Tooltip("Degrees per second the enemy can turn.")]
    [SerializeField] private float turnSpeed = 540f;
    [Tooltip("0 if the sprite art faces right, -90 if it faces up.")]
    [SerializeField] private float spriteFacingOffset = 0f;
    [Tooltip("Optional patrol route. Leave empty for a guard that stands still.")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolWaitTime = 1.5f;

    [Header("Vision")]
    [SerializeField] private float viewDistance = 10f;
    [Tooltip("Full width of the vision cone in degrees.")]
    [SerializeField] private float viewAngle = 120f;
    [Tooltip("Player is noticed from any direction inside this radius.")]
    [SerializeField] private float closeSenseRadius = 1.5f;
    [Tooltip("Layers that block sight (Walls, Props).")]
    [SerializeField] private LayerMask sightBlockers;

    [Header("Ambush")]
    [Tooltip("Lies in wait (crouched behind cover, ignoring noises) until Skeeter comes close, " +
             "is shot, or another ambusher nearby springs. Then everyone nearby jumps out at once.")]
    [SerializeField] private bool ambusher = false;
    [Tooltip("Springs when the player is this close with a clear line of sight (any direction).")]
    [SerializeField] private float ambushRadius = 4.5f;
    [Tooltip("When one ambusher springs, waiting ambushers within this range spring too.")]
    [SerializeField] private float ambushCallRadius = 7f;
    [Tooltip("Tint while hiding (in the shadow of the stall).")]
    [SerializeField] private Color hidingTint = new Color(0.7f, 0.7f, 0.78f);

    [Header("Combat")]
    [SerializeField] private Bullet bulletPrefab;
    [SerializeField] private MuzzleFlash muzzleFlash;
    [SerializeField] private float attackRange = 9f;
    [Tooltip("Delay between first spotting the player and the first shot.")]
    [SerializeField] private float reactionTime = 0.45f;
    [SerializeField] private float fireCooldown = 0.75f;
    [Tooltip("Random aim error in degrees either side.")]
    [SerializeField] private float aimError = 4f;
    [Tooltip("Enemy stops advancing when this close to a visible player.")]
    [SerializeField] private float preferredDistance = 4f;
    [SerializeField] private float gunshotNoiseRadius = 9f;

    [Header("Melee (sword knights)")]
    [Tooltip("Tick for knights: they charge in and swing a sword instead of shooting.")]
    [SerializeField] private bool usesMelee = false;
    [SerializeField] private float meleeRange = 1.1f;
    [Tooltip("Telegraph before the swing lands. The knight stops and glows, giving the player time to shoot or step away.")]
    [SerializeField] private float windupTime = 0.4f;
    [SerializeField] private float meleeCooldown = 0.8f;
    [Tooltip("Width of the sword arc in degrees. Outside it, the swing misses.")]
    [SerializeField] private float swingArc = 100f;
    [SerializeField] private Color windupTint = new Color(1f, 0.35f, 0.35f);
    [Tooltip("Optional slash sprite spawned in front of the knight (use TimedDestroy on it).")]
    [SerializeField] private GameObject slashEffectPrefab;
    [SerializeField] private AudioClip[] windupClips;
    [SerializeField] private AudioClip[] swingClips;

    [Header("Health")]
    [Tooltip("Hits to kill (each pellet / staff bolt does its own Damage, usually 1). Knight ~6, Wizard ~3.")]
    [Min(1)] [SerializeField] private int maxHealth = 6;
    [Tooltip("Tint flashed for a moment when hit but not killed.")]
    [SerializeField] private Color hurtTint = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private float hurtFlashTime = 0.08f;
    [Tooltip("Speed of the shove when hit. Stacks per pellet, so a close shotgun blast really moves them.")]
    [SerializeField] private float hitPushSpeed = 1.2f;
    [SerializeField] private float maxPushSpeed = 7f;
    [SerializeField] private float pushDecay = 25f;
    [SerializeField] private AudioClip[] hurtClips;
    [Tooltip("Small blood splat on non-lethal hits (optional).")]
    [SerializeField] private GameObject hitSplatPrefab;
    [Tooltip("Soft puff sprite used for the blood mist sprayed by hits during bullet time.")]
    [SerializeField] private Sprite bloodMistSprite;

    [Header("Hearing")]
    [SerializeField] private float hearingMultiplier = 1f;
    [Tooltip("Hearing range multiplier when a wall is between the enemy and the sound.")]
    [Range(0f, 1f)] [SerializeField] private float wallMuffle = 0.6f;

    [Header("Search")]
    [SerializeField] private float searchDuration = 3f;

    [Header("Feedback")]
    [Tooltip("\"!\" sprite shown when the enemy spots the player.")]
    [SerializeField] private GameObject alertIcon;
    [Tooltip("\"?\" sprite shown when the enemy hears something.")]
    [SerializeField] private GameObject curiousIcon;
    [SerializeField] private float iconHeight = 0.9f;
    [SerializeField] private float iconTime = 1.2f;
    [SerializeField] private AudioClip[] alertClips;
    [SerializeField] private AudioClip[] curiousClips;
    [SerializeField] private AudioClip[] shotClips;

    [Header("Death")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Sprite deadSprite;
    [SerializeField] private GameObject bloodSplatPrefab;
    [SerializeField] private AudioClip[] deathClips;
    [Tooltip("Corpses draw under living characters.")]
    [SerializeField] private int deadSortingOrder = -1;
    [Tooltip("How hard the body is thrown back when killed (0 = drops on the spot).")]
    [SerializeField] private float corpseLaunchSpeed = 9f;
    [Tooltip("How quickly the thrown body skids to a stop.")]
    [SerializeField] private float corpseFriction = 22f;

    private Rigidbody2D rb;
    private State state;

    private Vector2 homePosition;
    private float homeAngle;
    private int patrolIndex;

    private Vector2 lastKnownPlayerPos;
    private bool sawPlayerLastFrame;
    private float reactionTimer;
    private float nextFireTime;

    private List<Vector2> path;
    private int pathIndex;
    private float repathTimer;
    private const float RepathInterval = 0.4f;
    private const float WaypointTolerance = 0.2f;

    private float stateTimer;
    private float searchBaseAngle;
    private float iconTimer;

    private Vector2 lastPosition;
    private float stuckTimer;

    private bool windingUp;
    private float windupTimer;
    private float windupAngle;
    private Color baseTint = Color.white;

    private int health;
    private float hurtTimer;
    private float nextHurtSoundTime;
    private Vector2 pushVelocity;

    private bool waiting;
    private static readonly List<EnemyAI> waitingAmbushers = new List<EnemyAI>();

    // Update decides where to go, FixedUpdate actually moves the body
    private Vector2 desiredVelocity;
    private float desiredAngle;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodyRenderer) baseTint = bodyRenderer.color;
        health = maxHealth;
        HideIcons();
    }

    private void OnEnable() => NoiseSystem.NoiseMade += OnNoiseHeard;   // listen for gunshots and breaking props
    private void OnDisable() => NoiseSystem.NoiseMade -= OnNoiseHeard;

    private void Start()
    {
        homePosition = rb.position;
        homeAngle = rb.rotation;
        desiredAngle = homeAngle;
        lastPosition = rb.position;

        GameManager.Instance?.RegisterEnemy();   // so the level knows how many enemies there are

        if (patrolPoints != null && patrolPoints.Length > 0) EnterPatrol();
        else state = State.Idle;

        if (ambusher)
        {
            waiting = true;
            waitingAmbushers.Add(this);
            if (bodyRenderer) bodyRenderer.color = hidingTint;
        }
    }

    private void OnDestroy() => waitingAmbushers.Remove(this);

    // ambushers hide behind stuff and ignore noise. they only jump out when Skeeter gets close
    // and they can see him, when they get shot, or when another ambusher nearby jumps out
    private bool UpdateWaiting(PlayerController player)
    {
        if (!waiting) return false;
        desiredVelocity = Vector2.zero;
        desiredAngle = homeAngle;

        if (player != null && !player.IsDead)
        {
            Vector2 toPlayer = (Vector2)player.transform.position - rb.position;
            bool clearView = Physics2D.Linecast(rb.position, player.transform.position, sightBlockers).collider == null;
            if (toPlayer.magnitude <= ambushRadius && clearView) Spring(player.transform.position);
        }
        return waiting;
    }

    private void Spring(Vector2 playerPosition)
    {
        if (!waiting) return;
        waiting = false;
        waitingAmbushers.Remove(this);
        RefreshTint();

        if (state != State.Dead)
        {
            lastKnownPlayerPos = playerPosition;
            EnterChase();
            reactionTimer = 0.1f;
        }

        // wake up the other hidden guards nearby (ToArray because Spring removes from the list)
        foreach (EnemyAI other in waitingAmbushers.ToArray())
            if (other != null && Vector2.Distance(other.rb.position, rb.position) <= ambushCallRadius)
                other.Spring(playerPosition);
    }

    private void Update()
    {
        if (state == State.Dead) return;

        PlayerController player = PlayerController.Instance;
        if (UpdateWaiting(player)) return;

        bool seesPlayer = CanSeePlayer(player);

        if (seesPlayer)
        {
            lastKnownPlayerPos = player.transform.position;
            if (state != State.Chase) EnterChase();
        }

        desiredVelocity = Vector2.zero;

        // run whatever state we're in
        switch (state)
        {
            case State.Idle:        desiredAngle = homeAngle; break;
            case State.Patrol:      UpdatePatrol(); break;
            case State.Investigate: UpdateInvestigate(); break;
            case State.Chase:       UpdateChase(player, seesPlayer); break;
            case State.Search:      UpdateSearch(); break;
            case State.Return:      UpdateReturn(); break;
        }

        sawPlayerLastFrame = seesPlayer;

        if (hurtTimer > 0f)
        {
            hurtTimer -= Time.deltaTime;
            if (hurtTimer <= 0f) RefreshTint();
        }

        iconTimer -= Time.deltaTime;
        if (iconTimer <= 0f) HideIcons();
    }

    private void FixedUpdate()
    {
        if (state == State.Dead) return;

        PhysicsUtil.SetVelocity(rb, desiredVelocity + pushVelocity);
        pushVelocity = Vector2.MoveTowards(pushVelocity, Vector2.zero, pushDecay * Time.fixedDeltaTime);
        rb.angularVelocity = 0f;
        rb.MoveRotation(Mathf.MoveTowardsAngle(rb.rotation, desiredAngle, turnSpeed * Time.fixedDeltaTime));

        // if it's been trying to move but hasn't for a second it's stuck, give up on this path
        bool tryingToMove = desiredVelocity.sqrMagnitude > 0.01f;
        bool barelyMoved = (rb.position - lastPosition).sqrMagnitude < 0.0001f;
        stuckTimer = tryingToMove && barelyMoved ? stuckTimer + Time.fixedDeltaTime : 0f;
        if (stuckTimer > 1f)
        {
            stuckTimer = 0f;
            if (state == State.Chase && !sawPlayerLastFrame) EnterSearch();
            else path = null;
        }
        lastPosition = rb.position;
    }

    private void LateUpdate()
    {
        PlaceIcon(alertIcon);
        PlaceIcon(curiousIcon);
    }

    private void EnterPatrol()
    {
        state = State.Patrol;
        stateTimer = 0f;
        SetDestination(patrolPoints[patrolIndex].position);
    }

    private void UpdatePatrol()
    {
        if (!PathFinished)
        {
            FollowPath(patrolSpeed);
            return;
        }

        stateTimer += Time.deltaTime;
        if (stateTimer >= patrolWaitTime)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            EnterPatrol();
        }
    }

    private void EnterInvestigate(Vector2 soundPosition)
    {
        if (state != State.Investigate)
        {
            ShowIcon(curiousIcon);
            AudioManager.PlayRandom(curiousClips, 0.6f, 0.1f);
        }
        state = State.Investigate;
        SetDestination(soundPosition);
    }

    private void UpdateInvestigate()
    {
        FollowPath(investigateSpeed);
        if (PathFinished) EnterSearch();
    }

    private void EnterChase()
    {
        bool wasAlreadyHunting = state == State.Search || state == State.Investigate;
        reactionTimer = wasAlreadyHunting ? reactionTime * 0.6f : reactionTime;   // short delay before attacking so the player has a second to react

        state = State.Chase;
        path = null;
        repathTimer = 0f;
        ShowIcon(alertIcon);
        AudioManager.PlayRandom(alertClips, 0.7f, 0.1f);
    }

    private void UpdateChase(PlayerController player, bool seesPlayer)
    {
        if (seesPlayer)
        {
            if (!sawPlayerLastFrame) reactionTimer = Mathf.Max(reactionTimer, reactionTime * 0.5f);

            Vector2 toPlayer = (Vector2)player.transform.position - rb.position;
            desiredAngle = AngleOf(toPlayer);

            float stopDistance = usesMelee ? meleeRange * 0.8f : preferredDistance;
            if (toPlayer.magnitude > stopDistance)
            {
                // run straight at the player if there's room, otherwise path around stuff
                if (GridPathfinder.Instance == null || GridPathfinder.Instance.HasClearance(rb.position, player.transform.position))
                {
                    desiredVelocity = toPlayer.normalized * chaseSpeed;
                }
                else
                {
                    RepathPeriodically(player.transform.position);
                    FollowPath(chaseSpeed);
                    desiredAngle = AngleOf(toPlayer);
                }
            }

            reactionTimer -= Time.deltaTime;

            if (usesMelee)
            {
                UpdateMelee(player, toPlayer);
            }
            else
            {
                bool facingPlayer = Mathf.Abs(Mathf.DeltaAngle(rb.rotation, desiredAngle)) < 25f;
                if (reactionTimer <= 0f && facingPlayer && toPlayer.magnitude <= attackRange && Time.time >= nextFireTime)
                    Shoot(toPlayer);
            }
        }
        else
        {
            CancelWindup();
            RepathPeriodically(lastKnownPlayerPos);
            FollowPath(chaseSpeed);
            if (PathFinished) EnterSearch();
        }
    }

    private void EnterSearch()
    {
        state = State.Search;
        stateTimer = 0f;
        searchBaseAngle = rb.rotation;
        path = null;
    }

    private void UpdateSearch()
    {
        stateTimer += Time.deltaTime;
        desiredAngle = searchBaseAngle + Mathf.Sin(stateTimer * 2.5f) * 75f;   // look left and right

        if (stateTimer >= searchDuration) EnterReturn();
    }

    private void EnterReturn()
    {
        state = State.Return;
        SetDestination(homePosition);
    }

    private void UpdateReturn()
    {
        FollowPath(patrolSpeed);
        if (!PathFinished) return;

        if (patrolPoints != null && patrolPoints.Length > 0) EnterPatrol();
        else
        {
            state = State.Idle;
            desiredAngle = homeAngle;
        }
    }

    // sight: in range, inside the vision cone (unless really close or already fighting)
    // and nothing in the way
    private bool CanSeePlayer(PlayerController player)
    {
        if (player == null || player.IsDead) return false;

        Vector2 toPlayer = (Vector2)player.transform.position - rb.position;
        float distance = toPlayer.magnitude;
        if (distance > viewDistance) return false;

        if (distance > closeSenseRadius && state != State.Chase)
        {
            Vector2 facing = DirectionOf(rb.rotation);
            if (Vector2.Angle(facing, toPlayer) > viewAngle * 0.5f) return false;
        }

        RaycastHit2D hit = Physics2D.Linecast(rb.position, player.transform.position, sightBlockers);
        return hit.collider == null || hit.collider.isTrigger;
    }

    // hearing: a noise in range pulls it over to check it out, walls muffle it
    private void OnNoiseHeard(Vector2 position, float radius)
    {
        if (state == State.Dead || state == State.Chase || waiting) return;

        float range = radius * hearingMultiplier;

        RaycastHit2D wall = Physics2D.Linecast(rb.position, position, sightBlockers);
        if (wall.collider != null && !wall.collider.isTrigger) range *= wallMuffle;

        if (Vector2.Distance(rb.position, position) <= range)
        {
            lastKnownPlayerPos = position;
            EnterInvestigate(position);
        }
    }

    private void Shoot(Vector2 toPlayer)
    {
        Vector2 direction = Quaternion.Euler(0f, 0f, Random.Range(-aimError, aimError)) * toPlayer.normalized;   // a little random aim so wizards don't hit every time

        Bullet bullet = Instantiate(bulletPrefab, rb.position, Quaternion.identity);
        bullet.Fire(direction);

        if (muzzleFlash) muzzleFlash.Flash();
        AudioManager.PlayRandom(shotClips, 0.75f);

        NoiseSystem.Emit(rb.position, gunshotNoiseRadius);

        nextFireTime = Time.time + fireCooldown * Random.Range(0.85f, 1.25f);
    }

    // knights stop and glow red before the swing lands so the player can shoot or move
    private void UpdateMelee(PlayerController player, Vector2 toPlayer)
    {
        if (windingUp)
        {
            desiredVelocity = Vector2.zero;
            desiredAngle = windupAngle;

            windupTimer -= Time.deltaTime;
            if (windupTimer <= 0f) ResolveSwing(player);
            return;
        }

        if (reactionTimer <= 0f && toPlayer.magnitude <= meleeRange && Time.time >= nextFireTime)
            StartWindup();
    }

    private void StartWindup()
    {
        windingUp = true;
        windupTimer = windupTime;
        windupAngle = desiredAngle;
        RefreshTint();
        AudioManager.PlayRandom(windupClips, 0.7f, 0.1f);
    }

    private void ResolveSwing(PlayerController player)
    {
        CancelWindup();
        nextFireTime = Time.time + meleeCooldown;

        Vector2 facing = DirectionOf(windupAngle);
        AudioManager.PlayRandom(swingClips, 0.8f, 0.1f);
        if (slashEffectPrefab)
            Instantiate(slashEffectPrefab, rb.position + facing * (meleeRange * 0.6f),
                        Quaternion.Euler(0f, 0f, windupAngle - spriteFacingOffset));

        if (player == null || player.IsDead) return;
        Vector2 toPlayer = (Vector2)player.transform.position - rb.position;
        bool inRange = toPlayer.magnitude <= meleeRange * 1.25f;
        bool inArc = Vector2.Angle(facing, toPlayer) <= swingArc * 0.5f;   // the swing only hits if Skeeter's still in front, in range, and not behind a wall
        bool noWall = Physics2D.Linecast(rb.position, player.transform.position, sightBlockers).collider == null;

        if (inRange && inArc && noWall)
            player.TakeHit(toPlayer.normalized, 1);
    }

    private void CancelWindup()
    {
        windingUp = false;
        RefreshTint();
    }

    private void RefreshTint()
    {
        if (!bodyRenderer) return;
        if (hurtTimer > 0f) bodyRenderer.color = hurtTint;
        else if (windingUp) bodyRenderer.color = windupTint;
        else bodyRenderer.color = baseTint;
    }

    // bullet time only: blood mist sprays out the back of the hit and hangs in the air
    private void SprayBlood(Vector2 hitDirection, int count, float size)
    {
        if (bloodMistSprite == null) return;
        Vector2 dir = hitDirection.sqrMagnitude > 0.001f ? hitDirection.normalized : Vector2.right;
        for (int i = 0; i < count; i++)
        {
            Vector2 spread = Quaternion.Euler(0f, 0f, Random.Range(-35f, 35f)) * dir;
            Color c = Color.Lerp(new Color(0.55f, 0.05f, 0.07f, 0.85f), new Color(0.8f, 0.12f, 0.12f, 0.85f), Random.value);
            SmokePuff mist = SmokePuff.Spawn(bloodMistSprite, rb.position + spread * 0.2f, c,
                                             Random.Range(0.12f, 0.3f) * size, 7);
            mist.velocity = spread * Random.Range(1.5f, 4.5f);
            mist.growth = 0.5f * size;
            mist.drag = 4f;
            mist.lifetime = Random.Range(0.5f, 0.9f);
        }
    }

    public void TakeHit(Vector2 hitDirection, int damage)
    {
        if (state == State.Dead) return;

        if (waiting && PlayerController.Instance != null) Spring(PlayerController.Instance.transform.position);   // getting shot blows the ambush

        GameStats.Hits++;
        health -= damage;
        if (health <= 0) Die(hitDirection);
        else Hurt(hitDirection);
    }

    // not dead yet: flash, get shoved back, cancel the swing, and turn to fight
    private void Hurt(Vector2 hitDirection)
    {
        if (SmokeBreak.Active) SprayBlood(hitDirection, 5, 0.6f);
        hurtTimer = hurtFlashTime;
        CancelWindup();
        nextFireTime = Mathf.Max(nextFireTime, Time.time + 0.35f);

        pushVelocity = Vector2.ClampMagnitude(pushVelocity + hitDirection.normalized * hitPushSpeed, maxPushSpeed);

        if (Time.time >= nextHurtSoundTime)   // one hurt sound per blast not one per pellet
        {
            nextHurtSoundTime = Time.time + 0.1f;
            AudioManager.PlayRandom(hurtClips, 0.7f, 0.1f);
            if (hitSplatPrefab)
                Instantiate(hitSplatPrefab, transform.position, Quaternion.Euler(0f, 0f, AngleOf(hitDirection)));
        }

        PlayerController player = PlayerController.Instance;
        if (player != null && !player.IsDead)
        {
            lastKnownPlayerPos = player.transform.position;
            if (state != State.Chase) EnterChase();
        }
    }

    // dead: stop colliding, swap to the corpse sprite, blood, tell GameManager
    // then send the body flying away from the shot
    private void Die(Vector2 hitDirection)
    {
        if (SmokeBreak.Active)
        {
            SprayBlood(hitDirection, 14, 1f);
            if (bloodSplatPrefab)
                Instantiate(bloodSplatPrefab, rb.position + hitDirection.normalized * 0.6f,
                            Quaternion.Euler(0f, 0f, Mathf.Atan2(hitDirection.y, hitDirection.x) * Mathf.Rad2Deg));
        }
        state = State.Dead;
        hurtTimer = 0f;

        desiredVelocity = Vector2.zero;
        PhysicsUtil.SetVelocity(rb, Vector2.zero);
        rb.simulated = false;
        foreach (Collider2D c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
        HideIcons();
        CancelWindup();

        float angle = AngleOf(hitDirection);
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        if (bodyRenderer)
        {
            if (deadSprite) bodyRenderer.sprite = deadSprite;
            bodyRenderer.sortingOrder = deadSortingOrder;
        }
        if (bloodSplatPrefab) Instantiate(bloodSplatPrefab, transform.position, Quaternion.Euler(0f, 0f, angle));

        AudioManager.PlayRandom(deathClips, 0.9f, 0.1f);
        CameraFollow.Shake(0.15f, 0.12f);

        GameManager.Instance?.EnemyKilled();
        enabled = false;

        if (corpseLaunchSpeed > 0f) StartCoroutine(LaunchCorpse(hitDirection.normalized));
    }

    // body slides and spins away from the shot, leaves a blood trail, stops at walls
    private System.Collections.IEnumerator LaunchCorpse(Vector2 direction)
    {
        float speed = corpseLaunchSpeed * Random.Range(0.85f, 1.15f);
        float spin = Random.Range(-360f, 360f);
        float trailTimer = 0f;

        while (speed > 0.05f)
        {
            float step = speed * Time.deltaTime;

            RaycastHit2D wall = Physics2D.Raycast(transform.position, direction, step + 0.25f, sightBlockers);
            if (wall.collider != null && !wall.collider.isTrigger) break;

            transform.position += (Vector3)(direction * step);
            transform.Rotate(0f, 0f, spin * Time.deltaTime);

            trailTimer -= Time.deltaTime;
            if (bloodSplatPrefab && trailTimer <= 0f)
            {
                Instantiate(bloodSplatPrefab, transform.position, Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
                trailTimer = 0.22f;
            }

            speed = Mathf.Max(0f, speed - corpseFriction * Time.deltaTime);
            spin = Mathf.MoveTowards(spin, 0f, 720f * Time.deltaTime);
            yield return null;
        }
    }

    private bool PathFinished => path == null || pathIndex >= path.Count;

    // ask the pathfinder for a route, if there isn't one just go straight there
    private void SetDestination(Vector2 destination)
    {
        path = GridPathfinder.Instance != null ? GridPathfinder.Instance.FindPath(rb.position, destination) : null;
        if (path == null) path = new List<Vector2> { destination };
        pathIndex = 0;
        stuckTimer = 0f;
    }

    // the player keeps moving so redo the route every so often instead of every frame
    private void RepathPeriodically(Vector2 destination)
    {
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f || path == null)
        {
            SetDestination(destination);
            repathTimer = RepathInterval;
        }
    }

    private void FollowPath(float speed)
    {
        if (PathFinished) return;

        Vector2 toWaypoint = path[pathIndex] - rb.position;
        if (toWaypoint.magnitude < WaypointTolerance)
        {
            pathIndex++;
            if (PathFinished) return;
            toWaypoint = path[pathIndex] - rb.position;
        }

        desiredVelocity = toWaypoint.normalized * speed;
        desiredAngle = AngleOf(toWaypoint);
    }

    private float AngleOf(Vector2 direction) =>
        Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + spriteFacingOffset;

    private Vector2 DirectionOf(float angle)
    {
        float rad = (angle - spriteFacingOffset) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private void ShowIcon(GameObject icon)
    {
        HideIcons();
        if (icon) icon.SetActive(true);
        iconTimer = iconTime;
    }

    private void HideIcons()
    {
        if (alertIcon) alertIcon.SetActive(false);
        if (curiousIcon) curiousIcon.SetActive(false);
    }

    // keep the ! and ? above the head and upright no matter which way the body faces
    private void PlaceIcon(GameObject icon)
    {
        if (!icon || !icon.activeSelf) return;
        icon.transform.SetPositionAndRotation(transform.position + Vector3.up * iconHeight, Quaternion.identity);
    }

    // editor only: draws the view range and vision cone when it's selected
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, viewDistance);
        Gizmos.color = new Color(1f, 1f, 0.3f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, closeSenseRadius);

        float baseAngle = transform.eulerAngles.z - spriteFacingOffset;
        for (int side = -1; side <= 1; side += 2)
        {
            float rad = (baseAngle + side * viewAngle * 0.5f) * Mathf.Deg2Rad;
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
            Gizmos.DrawLine(transform.position, transform.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad)) * viewDistance);
        }
    }
}
