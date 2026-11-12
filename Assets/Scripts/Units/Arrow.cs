using System.Collections;
using Mirror;
using UnityEngine;

namespace RTSokak.Network
{
    /// <summary>
    /// Стрела лучника: полёт по прямой, swept-hit по цели, партиклы при промахе.
    /// </summary>
    public class Arrow : NetworkBehaviour
    {
        [Header("Arrow Properties")]
        [SerializeField] private float speed = 15f;
        [SerializeField] private int damage = 20;
        [SerializeField] private float hitRadius = 1.1f;
        [SerializeField] private float buildingHitRadius = 2.2f;
        [SerializeField] private float targetBodyHeight = 1.0f;
        [SerializeField] private float maxLifetime = 10f;
        [SerializeField] private float despawnSafetyMargin = 3f;

        [Header("Visual")]
        [SerializeField] private GameObject arrowMesh;

        [Header("Particles")]
        [SerializeField] private GameObject missParticlesPrefab;

        [SyncVar(hook = nameof(OnFlightChanged))] private Vector3 startPosition;
        [SyncVar(hook = nameof(OnFlightChanged))] private Vector3 targetPosition;
        [SyncVar(hook = nameof(OnFlightChanged))] private Vector3 flightDirection;
        [SyncVar] private float predictedHitTime;
        [SyncVar] private uint targetNetId;
        [SyncVar] private Vector3 knockbackDir;
        [SyncVar] private int arrowDamage;
        [SyncVar] private uint shooterNetId;
        [SyncVar] private string shooterOwnerNickname = "";
        [SyncVar] private int shooterOwnerPlayerIndex = -1;
        [SyncVar] private int shooterOwnerConnectionId = -1;

        private float spawnTime;
        private bool hasHit;
        private bool destroying;
        private Vector3 velocity;
        private bool flightReady;
        private float maxFlightDistance;
        private Vector3 previousPosition;

        public override void OnStartClient()
        {
            base.OnStartClient();
            var nt = GetComponent<NetworkTransformBase>();
            if (nt != null)
                nt.enabled = false;
            ApplyFlightVisual(true);
            previousPosition = transform.position;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            var nt = GetComponent<NetworkTransformBase>();
            if (nt != null)
                nt.enabled = false;
            spawnTime = Time.time;
            previousPosition = transform.position;
        }

        [Server]
        public void SetShooter(RTSUnit shooter)
        {
            if (shooter == null) return;
            shooterNetId = shooter.netId;
            shooterOwnerNickname = shooter.OwnerNickname ?? "";
            shooterOwnerPlayerIndex = shooter.OwnerPlayerIndex;
            shooterOwnerConnectionId = shooter.OwnerConnectionId;
        }

        [Server]
        public void Initialize(Vector3 start, Vector3 target, float hitTime, uint netId, int dmg, Vector3 kbDir)
        {
            Initialize(start, target, hitTime, netId, dmg, kbDir, maxFlightOverride: -1f, shooter: null);
        }

        /// <param name="maxFlightOverride">Если &gt; 0 — деспавн по этой дистанции (для FPS / shootRange).</param>
        [Server]
        public void Initialize(Vector3 start, Vector3 target, float hitTime, uint netId, int dmg, Vector3 kbDir, float maxFlightOverride, RTSUnit shooter = null)
        {
            if (shooter != null)
                SetShooter(shooter);

            // Цель — «грудь», не ноги (только для AI-выстрелов при maxFlightOverride <= 0f)
            Vector3 aim = target;
            if (maxFlightOverride <= 0f)
            {
                if (aim.y < start.y - 0.2f || Mathf.Abs(aim.y - start.y) < 0.15f)
                    aim.y = start.y;
            }

            startPosition = start;
            targetPosition = aim;
            predictedHitTime = hitTime;
            targetNetId = netId;
            arrowDamage = dmg;
            knockbackDir = kbDir.sqrMagnitude > 0.001f ? kbDir.normalized : (aim - start).normalized;

            Vector3 direction = aim - start;
            if (direction.sqrMagnitude < 0.0001f)
                direction = transform.forward;
            flightDirection = direction.normalized;

            if (maxFlightOverride > 0f)
                maxFlightDistance = maxFlightOverride + despawnSafetyMargin;
            else
                maxFlightDistance = Vector3.Distance(start, aim) + despawnSafetyMargin;

            transform.position = start;
            previousPosition = start;
            velocity = flightDirection * speed;
            flightReady = true;

            if (velocity.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(velocity);

            spawnTime = Time.time;
        }

        private void OnFlightChanged(Vector3 oldVal, Vector3 newVal)
        {
            ApplyFlightVisual(false);
        }

        private void ApplyFlightVisual(bool snapToStart)
        {
            Vector3 dir = flightDirection;
            if (dir.sqrMagnitude < 0.0001f && targetPosition != Vector3.zero && startPosition != Vector3.zero)
                dir = (targetPosition - startPosition).normalized;
            if (dir.sqrMagnitude < 0.0001f && targetPosition != Vector3.zero)
                dir = (targetPosition - transform.position).normalized;

            if (dir.sqrMagnitude < 0.0001f)
                return;

            flightDirection = dir;
            velocity = dir * speed;
            flightReady = true;

            if (snapToStart && startPosition != Vector3.zero)
            {
                transform.position = startPosition;
                previousPosition = startPosition;
            }

            transform.rotation = Quaternion.LookRotation(dir);
        }

        private void Update()
        {
            if (!flightReady)
            {
                ApplyFlightVisual(false);
                if (!flightReady)
                    return;
            }

            previousPosition = transform.position;
            transform.position += velocity * Time.deltaTime;

            if (!isServer || destroying)
                return;

            float elapsed = Time.time - spawnTime;

            if (!hasHit)
                CheckHitSwept();

            float distanceTraveled = Vector3.Distance(startPosition, transform.position);
            if (!hasHit && (distanceTraveled >= maxFlightDistance || elapsed >= maxLifetime))
                DestroyArrow(false);
        }

        [Server]
        private bool IsFriendlyUnit(RTSUnit unit)
        {
            if (unit == null) return false;
            if (shooterNetId != 0 && unit.netId == shooterNetId) return true;
            if (!string.IsNullOrEmpty(shooterOwnerNickname) && unit.IsOwnedByNickname(shooterOwnerNickname))
                return true;
            if (shooterOwnerConnectionId != -1 && unit.OwnerConnectionId == shooterOwnerConnectionId)
                return true;
            if (shooterOwnerPlayerIndex != -1 && unit.OwnerPlayerIndex == shooterOwnerPlayerIndex)
                return true;
            return false;
        }

        [Server]
        private void CheckHitSwept()
        {
            // Приоритет — исходная цель (с учётом высоты тела)
            if (targetNetId != 0 && NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity targetIdentity))
            {
                RTSUnit targetUnit = targetIdentity.GetComponent<RTSUnit>();
                if (targetUnit != null && targetUnit.IsAlive)
                {
                    if (targetUnit.netId != shooterNetId && !IsFriendlyUnit(targetUnit))
                    {
                        if (SweptHitUnit(targetUnit, hitRadius))
                        {
                            Vector3 kb = knockbackDir.sqrMagnitude > 0.01f
                                ? knockbackDir
                                : (targetUnit.transform.position - startPosition).normalized;
                            targetUnit.ServerTakeDamage(arrowDamage, kb, null);
                            hasHit = true;
                            DestroyArrow(true);
                            return;
                        }
                    }
                }

                Buildings.Building targetBuilding = targetIdentity.GetComponent<Buildings.Building>();
                if (targetBuilding != null && targetBuilding.CurrentHealth > 0)
                {
                    if (targetBuilding.CanBeAttackedBy(shooterOwnerNickname, shooterOwnerPlayerIndex, shooterOwnerConnectionId))
                    {
                        Vector3 buildingPoint = targetBuilding.transform.position + Vector3.up * 0.5f;
                        if (DistancePointToSegment(buildingPoint, previousPosition, transform.position) <= buildingHitRadius)
                        {
                            targetBuilding.TakeDamage(arrowDamage, null, out _);
                            hasHit = true;
                            DestroyArrow(true);
                            return;
                        }
                    }
                }
            }

            // Fallback без целевого netId: physics overlap по пути стрелы
            Vector3 mid = (previousPosition + transform.position) * 0.5f;
            float seg = Vector3.Distance(previousPosition, transform.position);
            Collider[] hits = Physics.OverlapSphere(mid, hitRadius + seg * 0.5f);
            for (int i = 0; i < hits.Length; i++)
            {
                RTSUnit unit = hits[i].GetComponentInParent<RTSUnit>();
                if (unit != null)
                {
                    if (!unit.IsAlive)
                        continue;
                    if (unit.netId == shooterNetId)
                        continue;
                    if (IsFriendlyUnit(unit))
                        continue;
                    if (!SweptHitUnit(unit, hitRadius))
                        continue;

                    Vector3 kb = unit.transform.position - startPosition;
                    kb.y = 0f;
                    if (kb.sqrMagnitude < 0.001f)
                        kb = knockbackDir;
                    unit.ServerTakeDamage(arrowDamage, kb.normalized, null);
                    hasHit = true;
                    DestroyArrow(true);
                    return;
                }

                Buildings.Building building = hits[i].GetComponentInParent<Buildings.Building>();
                if (building != null && building.CurrentHealth > 0)
                {
                    if (!building.CanBeAttackedBy(shooterOwnerNickname, shooterOwnerPlayerIndex, shooterOwnerConnectionId))
                        continue;

                    Vector3 buildingPoint = building.transform.position + Vector3.up * 0.5f;
                    if (DistancePointToSegment(buildingPoint, previousPosition, transform.position) <= buildingHitRadius)
                    {
                        building.TakeDamage(arrowDamage, null, out _);
                        hasHit = true;
                        DestroyArrow(true);
                        return;
                    }
                }
            }
        }

        [Server]
        private bool SweptHitUnit(RTSUnit unit, float radius)
        {
            Vector3 body = unit.transform.position + Vector3.up * targetBodyHeight;
            float dist = DistancePointToSegment(body, previousPosition, transform.position);
            if (dist <= radius)
                return true;

            // Доп. проверка по горизонтали (если высота чуть не совпала)
            Vector3 bodyFlat = body;
            bodyFlat.y = transform.position.y;
            return DistancePointToSegment(bodyFlat, previousPosition, transform.position) <= radius * 0.85f;
        }

        private static float DistancePointToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 0.000001f)
                return Vector3.Distance(point, a);

            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lenSq);
            Vector3 closest = a + ab * t;
            return Vector3.Distance(point, closest);
        }

        [Server]
        private void DestroyArrow(bool wasHit)
        {
            if (destroying)
                return;
            destroying = true;

            if (!wasHit)
            {
                // Rpc ДО Destroy — и с задержкой, иначе ClientRpc часто не доходит
                Vector3 pos = transform.position;
                RpcSpawnMissParticles(pos);
                // Хост тоже видит сразу (includeOwner на Rpc, но Destroy может обогнать)
                SpawnMissParticlesLocal(pos);
                StartCoroutine(DestroyAfterDelay(0.12f));
            }
            else
            {
                NetworkServer.Destroy(gameObject);
            }
        }

        [Server]
        private IEnumerator DestroyAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (this != null && gameObject != null)
                NetworkServer.Destroy(gameObject);
        }

        [ClientRpc(includeOwner = true)]
        private void RpcSpawnMissParticles(Vector3 position)
        {
            // На хосте уже заспавнили в DestroyArrow
            if (NetworkServer.active && NetworkClient.active)
                return;
            SpawnMissParticlesLocal(position);
        }

        private void SpawnMissParticlesLocal(Vector3 position)
        {
            if (missParticlesPrefab != null)
            {
                GameObject particles = Instantiate(missParticlesPrefab, position, Quaternion.identity);
                float life = 2f;
                ParticleSystem ps = particles.GetComponent<ParticleSystem>();
                if (ps == null)
                    ps = particles.GetComponentInChildren<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    life = main.duration + main.startLifetime.constantMax + 0.5f;
                    if (!ps.isPlaying)
                        ps.Play(true);
                }
                Destroy(particles, Mathf.Max(1.5f, life));
                return;
            }

            // Fallback если префаб не назначен — простой всплеск
            CreateFallbackDust(position);
        }

        private static void CreateFallbackDust(Vector3 position)
        {
            GameObject go = new GameObject("ArrowMissDust");
            go.transform.position = position;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.35f;
            main.loop = false;
            main.startLifetime = 0.45f;
            main.startSpeed = 2.5f;
            main.startSize = 0.12f;
            main.startColor = new Color(0.75f, 0.7f, 0.55f, 0.85f);
            main.maxParticles = 24;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            Destroy(go, 1.5f);
        }
    }
}
