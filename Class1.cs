using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;

namespace Thunderbolt
{
    public class ThunderboltSettings : ThunderScript
    {
        [ModOption(name: "Thunderbolt Damage", tooltip: "How much damage a normal non-merge & non-slam thunderbolt deals", valueSourceName: nameof(fiveValues), defaultValueIndex = 5, order = 1)]
        public static float Damage = 25;
        [ModOption(name: "Large Thunderbolt Damage", tooltip: "How much damage a merge/slam thunderbolt deals", valueSourceName: nameof(fiveValues), defaultValueIndex = 20, order = 2)]
        public static float LargeDamage = 100;
        [ModOption(name: "Imbue Damage", tooltip: "How much damage an imbued weapon deals", valueSourceName: nameof(fiveValues), defaultValueIndex = 2, order = 3)]
        public static float ImbueDamage = 10;
        [ModOption(name: "Thunderbolt Force", tooltip: "How much force a thunderbolt exerts when pushing something", valueSourceName: nameof(fiveValues), defaultValueIndex = 5, order = 4)]
        public static float Force = 25;
        [ModOption(name: "Thunderbolt Max Distance", tooltip: "The max distance that a thunderbolt can reach", valueSourceName: nameof(tenValues), defaultValueIndex = 100, order = 5)]
        public static float MaxDistance = 1000;
        [ModOption(name: "Chain Lightning Distance", tooltip: "The max distance that chain lighting searches for the closest enemy", valueSourceName: nameof(singleValues), defaultValueIndex = 10, order = 6)]
        public static float ChainDistance = 10;
        public static ModOptionFloat[] singleValues()
        {
            ModOptionFloat[] modOptionFloats = new ModOptionFloat[1001];
            float num = 0f;
            for (int i = 0; i < modOptionFloats.Length; ++i)
            {
                modOptionFloats[i] = new ModOptionFloat(num.ToString("0"), num);
                num += 1f;
            }
            return modOptionFloats;
        }
        public static ModOptionFloat[] fiveValues()
        {
            ModOptionFloat[] modOptionFloats = new ModOptionFloat[1001];
            float num = 0f;
            for (int i = 0; i < modOptionFloats.Length; ++i)
            {
                modOptionFloats[i] = new ModOptionFloat(num.ToString("0"), num);
                num += 5f;
            }
            return modOptionFloats;
        }
        public static ModOptionFloat[] tenValues()
        {
            ModOptionFloat[] modOptionFloats = new ModOptionFloat[1001];
            float num = 0f;
            for (int i = 0; i < modOptionFloats.Length; ++i)
            {
                modOptionFloats[i] = new ModOptionFloat(num.ToString("0"), num);
                num += 10f;
            }
            return modOptionFloats;
        }
    }
    public class ThunderboltMerge : SpellMergeData
    {
        EffectData armData;
        EffectData boltData;
        EffectData impactData;
        EffectData ragdollData;
        ThunderboltSpell thunderboltSpell;
        EffectInstance effectInstance;
        List<Creature> closestCreatures = new List<Creature>();
        public override void Load(Mana mana)
        {
            base.Load(mana);
            armData = Catalog.GetData<EffectData>("ThunderboltChargeArms");
            boltData = Catalog.GetData<EffectData>("ThunderboltBolt");
            impactData = Catalog.GetData<EffectData>("ThunderboltImpact");
            thunderboltSpell = Catalog.GetData<ThunderboltSpell>("Thunderbolt");
            ragdollData = Catalog.GetData<EffectData>("ImbueLightningRagdoll");
        }
        public override void Update()
        {
            base.Update();
            if (effectInstance != null && effectInstance.isPlaying)
                effectInstance.SetIntensity(currentCharge);
        }
        public override void Merge(bool active)
        {
            base.Merge(active);
            if (active)
            {
                effectInstance = armData.Spawn(mana.creature.GetRendererForVFX().transform, null, true);
                effectInstance.SetRenderer(mana.creature.GetRendererForVFX(), false);
                effectInstance.SetIntensity(0);
                effectInstance.Play();
            }
            else
            {
                if (effectInstance.isPlaying) effectInstance.Stop();
            }
        }
        public override void Throw(Vector3 velocity)
        {
            ShootBolt(mana.mergePoint, velocity);
        }
        public void ShootBolt(Transform source, Vector3 direction)
        {
            EffectInstance boltInstance = boltData.Spawn(source.position, source.rotation);
            boltInstance.SetSource(source);
            GameObject impact = new GameObject("Impact");
            boltInstance.SetTarget(impact.transform);
            GameObject.Destroy(impact, 1);
            foreach (Creature creature1 in Creature.allActive)
            {
                if (creature1 != Player.local.creature)
                {
                    creature1?.ragdoll?.AddPhysicToggleModifier(this);
                }
            }
            if (Physics.Raycast(source.position, direction, out RaycastHit hit, ThunderboltSettings.MaxDistance, -1, QueryTriggerInteraction.Ignore) && hit.point != null && hit.collider != null)
            {
                impact.transform.position = hit.point;
                CollisionInstance collision = new CollisionInstance(new DamageStruct(DamageType.Energy, ThunderboltSettings.LargeDamage))
                {
                    contactPoint = hit.point,
                    contactNormal = hit.normal,
                    casterHand = mana.casterRight ?? null,
                    targetCollider = hit.collider ?? null,
                    targetColliderGroup = hit.collider?.GetComponentInParent<ColliderGroup>() ?? null
                };
                Impact(hit, collision);
                if (hit.collider?.GetComponentInParent<RagdollPart>() is RagdollPart part)
                {
                    collision.damageStruct.hitRagdollPart = part;
                    part.ragdoll.creature.Damage(collision);
                    if (!part.ragdoll.creature.isPlayer)
                    {
                        part.ragdoll.creature.TryPush(Creature.PushType.Magic, (hit.point - source.position).normalized, 3, part.type);
                        part.physicBody.AddForceAtPosition((hit.point - source.position).normalized * ThunderboltSettings.Force, hit.point, ForceMode.Impulse);
                        part.ragdoll.creature.TryElectrocute(10, 5, true, false, ragdollData);
                    }
                    part.ragdoll.creature.lastInteractionTime = Time.time;
                    part.ragdoll.creature.lastInteractionCreature = mana?.creature ?? null;
                }
                else if (hit.collider?.GetComponentInParent<Creature>() is Creature creature)
                {
                    collision.damageStruct.hitRagdollPart = creature.ragdoll.rootPart;
                    creature.Damage(collision);
                    if (!creature.isPlayer)
                    {
                        creature.TryPush(Creature.PushType.Magic, (hit.point - source.position).normalized, 3);
                        creature.TryElectrocute(10, 5, true, false, ragdollData);
                    }
                    creature.lastInteractionTime = Time.time;
                    creature.lastInteractionCreature = mana?.creature ?? null;
                }
                else if (hit.collider?.GetComponentInParent<Item>() is Item item)
                {
                    if (item.IsHanded() && item.mainHandler?.creature?.player == null)
                    {
                        item.mainHandler?.creature?.TryPush(Creature.PushType.Magic, (hit.point - source.position).normalized, 2, item.mainHandler.type);
                        item.mainHandler?.TryRelease();
                    }
                    item.physicBody.AddForceAtPosition((hit.point - source.position).normalized * ThunderboltSettings.Force, hit.point, ForceMode.Impulse);
                    if (hit.collider?.GetComponentInParent<Breakable>() is Breakable breakable && !breakable.IsBroken) BreakItem(breakable, hit);
                    if (hit.collider?.GetComponentInParent<ColliderGroup>() is ColliderGroup colliderGroup && colliderGroup?.imbue?.GetModifier()?.imbueType != ColliderGroupData.ImbueType.None)
                        colliderGroup?.imbue?.Transfer(thunderboltSpell, 25);
                }
                if (GetClosestCreature(collision) is Creature enemy)
                {
                    GameObject origin = new GameObject("Origin");
                    origin.transform.position = collision.contactPoint;
                    origin.transform.rotation = Quaternion.LookRotation(collision.contactNormal);
                    ShootBolt(origin.transform, enemy.ragdoll.targetPart.transform.position - collision.contactPoint);
                    GameObject.Destroy(origin, 5);
                }
                else
                {
                    closestCreatures.Clear();
                }
            }
            else
            {
                impact.transform.position = source.position + (direction.normalized * Mathf.Min(ThunderboltSettings.MaxDistance, 100));
            }
            boltInstance.Play();
            foreach (Creature creature1 in Creature.allActive)
            {
                if (creature1 != Player.local.creature)
                {
                    creature1?.ragdoll?.RemovePhysicToggleModifier(this);
                }
            }
        }
        public void BreakItem(Breakable breakable, RaycastHit hit)
        {
            if (ThunderboltSettings.Force * ThunderboltSettings.Force < breakable.neededImpactForceToDamage)
                return;
            float sqrMagnitude = ThunderboltSettings.Force * ThunderboltSettings.Force;
            --breakable.hitsUntilBreak;
            if (breakable.canInstantaneouslyBreak && sqrMagnitude >= breakable.instantaneousBreakVelocityThreshold)
                breakable.hitsUntilBreak = 0;
            breakable.onTakeDamage?.Invoke(sqrMagnitude);
            if (breakable.IsBroken || breakable.hitsUntilBreak > 0)
                return;
            breakable.Break();
            for (int index = 0; index < breakable.subBrokenItems.Count; ++index)
            {
                Rigidbody rigidBody = breakable.subBrokenItems[index].physicBody.rigidBody;
                if (rigidBody)
                {
                    rigidBody.AddExplosionForce(ThunderboltSettings.Force, hit.point, 2, 0.0f, ForceMode.VelocityChange);
                }
            }
            for (int index = 0; index < breakable.subBrokenBodies.Count; ++index)
            {
                PhysicBody subBrokenBody = breakable.subBrokenBodies[index];
                if (subBrokenBody)
                {
                    subBrokenBody.rigidBody.AddExplosionForce(ThunderboltSettings.Force, hit.point, 2, 0.0f, ForceMode.VelocityChange);
                }
            }
        }
        public void Impact(RaycastHit hit, CollisionInstance collision = null)
        {
            EffectInstance instance = impactData.Spawn(hit.point, Quaternion.LookRotation(hit.normal), collision?.targetColliderGroup?.transform ?? null, collision ?? null, true, null, false);
            instance.SetIntensity(1f);
            instance.Play();
        }
        public Creature GetClosestCreature(CollisionInstance collisionInstance)
        {
            Creature closestCreature = null;
            foreach (Creature enemy in Creature.allActive)
            {
                if (!enemy.isKilled && enemy != Player.local.creature && (closestCreature == null || Vector3.Distance(enemy.ragdoll.targetPart.transform.position, collisionInstance.contactPoint) <
                    Vector3.Distance(closestCreature.ragdoll.targetPart.transform.position, collisionInstance.contactPoint)) && !closestCreatures.Contains(enemy) &&
                    Vector3.Distance(enemy.ragdoll.targetPart.transform.position, collisionInstance.contactPoint) <= Mathf.Min(ThunderboltSettings.ChainDistance, ThunderboltSettings.MaxDistance))
                    closestCreature = enemy;
            }
            closestCreatures.Add(closestCreature);
            return closestCreature;
        }
    }
    public class ThunderboltSpell : SpellCastCharge
    {
        EffectData armData;
        EffectData boltData;
        EffectData impactData;
        EffectData largeBoltData;
        EffectData imbueHitData;
        EffectData ragdollData;
        List<EffectInstance> armInstances = new List<EffectInstance>();
        public override void Load(SpellCaster spellCaster, Level level)
        {
            base.Load(spellCaster, level);
            armData = Catalog.GetData<EffectData>("ThunderboltChargeArms");
            boltData = Catalog.GetData<EffectData>("ThunderboltBolt");
            impactData = Catalog.GetData<EffectData>("ThunderboltImpact");
            largeBoltData = Catalog.GetData<EffectData>("ThunderboltLargeBolt");
            ragdollData = Catalog.GetData<EffectData>("ImbueLightningRagdoll");
            imbueHitData = Catalog.GetData<EffectData>("HitImbueLightning");
        }
        public override void Load(Imbue imbue, Level level)
        {
            base.Load(imbue, level);
            armData = Catalog.GetData<EffectData>("ThunderboltChargeArms");
            boltData = Catalog.GetData<EffectData>("ThunderboltBolt");
            impactData = Catalog.GetData<EffectData>("ThunderboltImpact");
            largeBoltData = Catalog.GetData<EffectData>("ThunderboltLargeBolt");
            ragdollData = Catalog.GetData<EffectData>("ImbueLightningRagdoll");
            imbueHitData = Catalog.GetData<EffectData>("HitImbueLightning");
        }
        public override bool OnImbueCollisionStart(CollisionInstance collisionInstance)
        {
            EffectInstance effectInstance = imbueHitData.Spawn(collisionInstance.contactPoint, Quaternion.LookRotation(collisionInstance.contactNormal, collisionInstance.sourceColliderGroup.transform.up));
            effectInstance.SetIntensity(collisionInstance.intensity);
            effectInstance.Play();
            if(collisionInstance?.targetColliderGroup?.collisionHandler?.ragdollPart?.ragdoll?.creature is Creature creature && !creature.isPlayer)
            {
                creature.TryElectrocute(1, 2, true, true, ragdollData);
                CollisionInstance instance = new CollisionInstance(new DamageStruct(DamageType.Energy, ThunderboltSettings.ImbueDamage)
                {
                    hitRagdollPart = collisionInstance.targetColliderGroup.collisionHandler.ragdollPart,
                    hitBack = Vector3.Dot((collisionInstance.contactPoint - collisionInstance.targetColliderGroup.collisionHandler.ragdollPart.transform.position).normalized, collisionInstance.targetColliderGroup.collisionHandler.ragdollPart.forwardDirection) < 0,
                    pushLevel = 1,
                    damageType = DamageType.Energy
                })
                {
                    impactVelocity = collisionInstance.impactVelocity,
                    contactPoint = collisionInstance.contactPoint,
                    contactNormal = collisionInstance.contactNormal,
                    targetColliderGroup = collisionInstance.targetColliderGroup
                };
                creature.Damage(instance);
            }
            else if (collisionInstance?.targetColliderGroup?.imbue is Imbue itemImbue && collisionInstance?.targetColliderGroup?.modifier?.imbueType != ColliderGroupData.ImbueType.None)
            {
                itemImbue.Transfer(this, 25 * collisionInstance.intensity);
            }
            return base.OnImbueCollisionStart(collisionInstance);
        }
        public override void Fire(bool active)
        {
            base.Fire(active);
            if (active)
            {
                foreach (Creature.RendererData renderer in spellCaster.ragdollHand.renderers)
                {
                    if (!spellCaster.ragdollHand.otherHand.renderers.Contains(renderer) && renderer != spellCaster.ragdollHand.ragdoll.rootPart.renderers[0])
                    {
                        EffectInstance effectInstance = armData.Spawn(renderer.renderer.transform, null, true);
                        effectInstance.SetRenderer(renderer.renderer, false);
                        effectInstance.SetIntensity(0);
                        effectInstance.Play();
                        armInstances.Add(effectInstance);
                    }
                }
            }
            else
            {
                foreach (EffectInstance effectInstance in armInstances)
                {
                    effectInstance.Stop();
                    effectInstance.Despawn();
                }
                armInstances.Clear();
            }
        }
        public override void UpdateCaster()
        {
            base.UpdateCaster();
            if (spellCaster.isFiring)
            {
                foreach (EffectInstance effectInstance in armInstances)
                {
                    effectInstance.SetIntensity(currentCharge);
                }
            }
        }
        public override void Throw(Vector3 velocity)
        {
            ShootBolt(spellCaster.magic, velocity);
        }
        public void ShootBolt(Transform source, Vector3 direction)
        {
            EffectInstance boltInstance = boltData.Spawn(source.position, source.rotation);
            boltInstance.SetSource(source);
            GameObject impact = new GameObject("Impact");
            boltInstance.SetTarget(impact.transform);
            GameObject.Destroy(impact, 1);
            foreach(Creature creature1 in Creature.allActive)
            {
                if (creature1 != Player.local.creature)
                {
                    creature1?.ragdoll?.AddPhysicToggleModifier(this);
                }
            }
            if (Physics.Raycast(source.position, direction, out RaycastHit hit, ThunderboltSettings.MaxDistance, -1, QueryTriggerInteraction.Ignore) && hit.point != null && hit.collider != null)
            {
                impact.transform.position = hit.point;
                CollisionInstance collision = new CollisionInstance(new DamageStruct(DamageType.Energy, ThunderboltSettings.Damage))
                {
                    contactPoint = hit.point,
                    contactNormal = hit.normal,
                    casterHand = spellCaster ?? null,
                    targetCollider = hit.collider ?? null,
                    targetColliderGroup = hit.collider?.GetComponentInParent<ColliderGroup>() ?? null
                };
                Impact(hit, collision);
                if (hit.collider?.GetComponentInParent<RagdollPart>() is RagdollPart part)
                {
                    collision.damageStruct.hitRagdollPart = part;
                    part.ragdoll.creature.Damage(collision);
                    if (!part.ragdoll.creature.isPlayer)
                    {
                        part.ragdoll.creature.TryPush(Creature.PushType.Magic, (hit.point - source.position).normalized, 3, part.type);
                        part.physicBody.AddForceAtPosition((hit.point - source.position).normalized * ThunderboltSettings.Force, hit.point, ForceMode.Impulse);
                        part.ragdoll.creature.TryElectrocute(10, 5, true, false, ragdollData);
                    }
                    part.ragdoll.creature.lastInteractionTime = Time.time;
                    part.ragdoll.creature.lastInteractionCreature = spellCaster?.mana?.creature ?? imbue?.colliderGroup?.collisionHandler?.item?.mainHandler?.ragdoll?.creature ?? null;
                }
                else if (hit.collider?.GetComponentInParent<Creature>() is Creature creature)
                {
                    collision.damageStruct.hitRagdollPart = creature.ragdoll.rootPart;
                    creature.Damage(collision);
                    if (!creature.isPlayer)
                    {
                        creature.TryPush(Creature.PushType.Magic, (hit.point - source.position).normalized, 3);
                        creature.TryElectrocute(10, 5, true, false, ragdollData);
                    }
                    creature.lastInteractionTime = Time.time;
                    creature.lastInteractionCreature = spellCaster?.mana?.creature ?? imbue?.colliderGroup?.collisionHandler?.item?.mainHandler?.ragdoll?.creature ?? null;
                }
                else if (hit.collider?.GetComponentInParent<Item>() is Item item)
                {
                    if (item.IsHanded() && item.mainHandler?.creature?.player == null)
                    {
                        item.mainHandler?.creature?.TryPush(Creature.PushType.Magic, (hit.point - source.position).normalized, 2, item.mainHandler.type);
                        item.mainHandler?.TryRelease();
                    }
                    item.physicBody.AddForceAtPosition((hit.point - source.position).normalized * ThunderboltSettings.Force, hit.point, ForceMode.Impulse);
                    if (hit.collider?.GetComponentInParent<Breakable>() is Breakable breakable && !breakable.IsBroken) BreakItem(breakable, hit);
                    if (hit.collider?.GetComponentInParent<ColliderGroup>() is ColliderGroup colliderGroup && colliderGroup?.imbue?.GetModifier()?.imbueType != ColliderGroupData.ImbueType.None)
                        colliderGroup?.imbue?.Transfer(this, 25);
                }
            }
            else
            {
                impact.transform.position = source.position + (direction.normalized * Mathf.Min(ThunderboltSettings.MaxDistance, 100));
            }
            boltInstance.Play();
            foreach (Creature creature1 in Creature.allActive)
            {
                if (creature1 != Player.local.creature)
                {
                    creature1?.ragdoll?.RemovePhysicToggleModifier(this);
                }
            }
        }
        public void BreakItem(Breakable breakable, RaycastHit hit)
        {
            if (ThunderboltSettings.Force * ThunderboltSettings.Force < breakable.neededImpactForceToDamage)
                return;
            float sqrMagnitude = ThunderboltSettings.Force * ThunderboltSettings.Force;
            --breakable.hitsUntilBreak;
            if (breakable.canInstantaneouslyBreak && sqrMagnitude >= breakable.instantaneousBreakVelocityThreshold)
                breakable.hitsUntilBreak = 0;
            breakable.onTakeDamage?.Invoke(sqrMagnitude);
            if (breakable.IsBroken || breakable.hitsUntilBreak > 0)
                return;
            breakable.Break();
            for (int index = 0; index < breakable.subBrokenItems.Count; ++index)
            {
                Rigidbody rigidBody = breakable.subBrokenItems[index].physicBody.rigidBody;
                if (rigidBody)
                {
                    rigidBody.AddExplosionForce(ThunderboltSettings.Force, hit.point, 2, 0.0f, ForceMode.VelocityChange);
                }
            }
            for (int index = 0; index < breakable.subBrokenBodies.Count; ++index)
            {
                PhysicBody subBrokenBody = breakable.subBrokenBodies[index];
                if (subBrokenBody)
                {
                    subBrokenBody.rigidBody.AddExplosionForce(ThunderboltSettings.Force, hit.point, 2, 0.0f, ForceMode.VelocityChange);
                }
            }
        }
        public void Impact(RaycastHit hit, CollisionInstance collision = null)
        {
            EffectInstance instance = impactData.Spawn(hit.point, Quaternion.LookRotation(hit.normal), collision?.targetColliderGroup?.transform ?? null, collision ?? null, true, null, false);
            instance.SetIntensity(1f);
            instance.Play();
        }
        public override bool OnCrystalUse(RagdollHand hand, bool active)
        {
            if (active)
            {
                ShootBolt(imbue.colliderGroup.imbueShoot, imbue.colliderGroup.imbueShoot.forward);
                imbue.colliderGroup.collisionHandler.item.physicBody.AddForce(-imbue.colliderGroup.imbueShoot.forward * ThunderboltSettings.Force, ForceMode.Impulse);
            }
            return true;
        }
        public override bool OnCrystalSlam(CollisionInstance collisionInstance)
        {
            Creature creature = GetClosestCreature(collisionInstance);
            GameObject source = new GameObject("Source");
            GameObject impact = new GameObject("Impact");
            EffectInstance boltInstance;
            EffectInstance impactInstance;
            if (creature != null)
            {
                boltInstance = largeBoltData.Spawn(creature.transform.position, Quaternion.LookRotation(Vector3.up));
                source.transform.position = creature.transform.position + (Vector3.up * 100);
                impact.transform.position = creature.transform.position;
                CollisionInstance collision = new CollisionInstance(new DamageStruct(DamageType.Energy, ThunderboltSettings.LargeDamage));
                collision.contactPoint = creature.transform.position;
                collision.contactNormal = Vector3.up;
                collision.targetCollider = creature.ragdoll.rootPart.colliderGroup.colliders[0];
                collision.targetColliderGroup = creature.ragdoll.rootPart.colliderGroup;
                collision.damageStruct.hitRagdollPart = creature.ragdoll.rootPart;
                creature.Damage(collision);
                creature.TryPush(Creature.PushType.Magic, Vector3.down, 3);
                creature.TryElectrocute(10, 5, true, false, ragdollData);
                creature.lastInteractionTime = Time.time;
                creature.lastInteractionCreature = imbue?.colliderGroup?.collisionHandler?.item?.mainHandler?.ragdoll?.creature;
                impactInstance = impactData.Spawn(collision?.targetColliderGroup?.transform.position ?? creature.transform.position, Quaternion.LookRotation(Vector3.up), collision?.targetColliderGroup?.transform ?? null, collision ?? null, true, null, false);
                foreach(Creature creature1 in Creature.allActive)
                {
                    if(!creature1.isKilled && creature1 != Player.local.creature && Vector3.Distance(creature1.ragdoll.targetPart.transform.position, collisionInstance.contactPoint) <= Mathf.Min(ThunderboltSettings.ChainDistance, ThunderboltSettings.MaxDistance))
                    {
                        ShootBolt(creature.ragdoll.targetPart.transform, creature1.ragdoll.targetPart.transform.position - creature.ragdoll.targetPart.transform.position);
                    }
                }
            }
            else
            {
                boltInstance = largeBoltData.Spawn(collisionInstance.contactPoint, Quaternion.LookRotation(Vector3.up));
                source.transform.position = collisionInstance.contactPoint + (Vector3.up * 100);
                impact.transform.position = collisionInstance.contactPoint;
                impactInstance = impactData.Spawn(collisionInstance.contactPoint, Quaternion.LookRotation(Vector3.up), collisionInstance?.targetColliderGroup?.transform ?? null, collisionInstance ?? null, true, null, false);
            }
            impactInstance.SetIntensity(1f);
            impactInstance.Play();
            boltInstance.SetSource(source.transform);
            boltInstance.SetTarget(impact.transform);
            boltInstance.SetIntensity(1f);
            boltInstance.Play();
            GameObject.Destroy(source, 5);
            GameObject.Destroy(impact, 5);
            return true;
        }
        public Creature GetClosestCreature(CollisionInstance collisionInstance)
        {
            Creature closestCreature = null;
            foreach (Creature enemy in Creature.allActive)
            {
                if (!enemy.isKilled && enemy != Player.local.creature && (closestCreature == null || Vector3.Distance(enemy.ragdoll.targetPart.transform.position, collisionInstance.contactPoint) <
                    Vector3.Distance(closestCreature.ragdoll.targetPart.transform.position, collisionInstance.contactPoint)))
                    closestCreature = enemy;
            }
            return closestCreature;
        }
    }
}
