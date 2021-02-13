﻿using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using static LeagueSharp.Common.Packet;

namespace SebbyLib
{
  

    public enum TeleportStatus
    {
        Recall = 0,
        Teleport = 1,
        TwistedFate = 2,
        Shen = 3,
        Unknown = 4
    }

    public class HeroInfo
    {
        public Obj_AI_Hero org;
        public int last_visible_tick = 0;
        public Vector3 last_visible_position = new Vector3();
        public Vector3 last_position = new Vector3();
        public float last_visible_real = 0;
        public float teleport_start_tick = 0;
        public float teleport_end_tick = 0;
        public float teleport_abort_tick = 0;
        public float teleport_finish_tick = 0;
        public S2C.Teleport.Type teleport_type = S2C.Teleport.Type.Recall;
        public float respawn_time = 0;
        public bool killable_with_baseult = false;
        public float travel_baseult_time = 0;
        public float exp = 0;
        public int invisible_allies = 0;
        public int visible_allies = 0;
        public bool is_jungler = false;
        public int detected_changes_in_row = 0;
        public bool is_fogofwar = false;
        public bool old_dead = false;
        public HeroInfo() { }
    };

    public class OktwCommon
    {
        private static Obj_AI_Hero Player { get { return ObjectManager.Player; } }

        private static int LastAATick = Utils.GameTimeTickCount;
        public static bool YasuoInGame = false;
        public static Obj_SpawnPoint EnemySpawnPoint;

        public static bool
            blockMove = false,
            blockAttack = false,
            blockSpells = false;

        private static List<UnitIncomingDamage> IncomingDamageList = new List<UnitIncomingDamage>();
        private static List<Obj_AI_Hero> ChampionList = new List<Obj_AI_Hero>();
        private static YasuoWall yasuoWall = new YasuoWall();

        static OktwCommon()
        {
            foreach (var hero in ObjectManager.Get<Obj_AI_Hero>())
            {
                ChampionList.Add(hero);
                if (hero.IsEnemy && hero.ChampionName == "Yasuo")
                    YasuoInGame = true;
            }
            EnemySpawnPoint = ObjectManager.Get<Obj_SpawnPoint>().FirstOrDefault(x => x.IsEnemy);
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
            Obj_AI_Base.OnIssueOrder += Obj_AI_Base_OnIssueOrder;
            Spellbook.OnCastSpell += Spellbook_OnCastSpell;
            Obj_AI_Base.OnDoCast += Obj_AI_Base_OnDoCast;
            Game.OnWndProc += Game_OnWndProc;
        }

        public static void debug(string msg)
        {
            if (true)
            {
                Console.WriteLine(msg);
            }
            if (false)
            {
                Game.PrintChat(msg);
            }
        }

        public bool ShouldWaitForMinion(float delay)
        {
            var minionListAA = ObjectManager.Get<Obj_AI_Minion>().Where(minion => minion.IsValidTarget()
                        && minion.Team != GameObjectTeam.Neutral && Orbwalking.InAutoAttackRange(minion) && MinionManager.IsMinion(minion, false));

            var minionsAlly = ObjectManager.Get<Obj_AI_Minion>().Where(minion => !minion.IsDead
                        && minion.IsAlly && minion.Distance(Player) < 600 && MinionManager.IsMinion(minion, false));

            int countAlly = minionsAlly.Count();

            if (minionListAA.Count() == 1 && countAlly > 3 && minionListAA.Any(x => x.Health < Player.TotalAttackDamage * 2))
                return true;

            if (countAlly > 2 && minionListAA.Any(x => x.IsMoving && x.Health < Player.TotalAttackDamage * 2))
                return true;

            var t = (int)(Player.AttackCastDelay * 1000) - 20 + 1000 * (int)Math.Max(0, 500) / (int)Orbwalking.GetMyProjectileSpeed();
            float laneClearDelay = delay * 1000 + t;
            return
                ObjectManager.Get<Obj_AI_Minion>()
                    .Any(
                        minion =>
                        minion.IsValidTarget() && minion.Team != GameObjectTeam.Neutral
                        && Orbwalking.InAutoAttackRange(minion) && MinionManager.IsMinion(minion, false)
                        && HealthPrediction.LaneClearHealthPrediction(minion,(int)(laneClearDelay),0) <= Player.GetAutoAttackDamage(minion));
        }

        public static bool CanHarras()
        {
            if (!Player.IsWindingUp && !Player.UnderTurret(true) && Orbwalking.CanMove(50))
                return true;
            else
                return false;
        }

        public static bool ShouldWait()
        {
            var attackCalc = (int)(Player.AttackDelay * 1000);
            return
                Cache.GetMinions(Player.Position, 0).Any(
                    minion => HealthPrediction.LaneClearHealthPrediction(minion, attackCalc, 30) <= Player.GetAutoAttackDamage(minion));
        }

        public static float GetEchoLudenDamage(Obj_AI_Hero target)
        {
            float totalDamage = 0;

            if (Player.HasBuff("itemmagicshankcharge"))
            {
                if (Player.GetBuff("itemmagicshankcharge").Count == 100)
                {
                    totalDamage += (float)Player.CalcDamage(target, Damage.DamageType.Magical, 100 + 0.1 * Player.FlatMagicDamageMod);
                }
            }
            return totalDamage;
        }

        public static bool IsSpellHeroCollision(Obj_AI_Hero t, Spell QWER, int extraWith = 50)
        {
            foreach (var hero in HeroManager.Enemies.FindAll(hero => hero.IsValidTarget(QWER.Range + QWER.Width, true, QWER.RangeCheckFrom) && t.NetworkId != hero.NetworkId))
            {
                var prediction = QWER.GetPrediction(hero);
                var powCalc = Math.Pow((QWER.Width + extraWith + hero.BoundingRadius), 2);
                if (prediction.UnitPosition.To2D().Distance(QWER.From.To2D(), QWER.GetPrediction(t).CastPosition.To2D(), true, true) <= powCalc)
                {
                    return true;
                }
                else if (prediction.UnitPosition.To2D().Distance(QWER.From.To2D(), t.ServerPosition.To2D(), true, true) <= powCalc)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool CanHitSkillShot(Obj_AI_Base target, Vector3 Start, Vector3 End, SpellData SData)
        {
            if (target.IsValidTarget(float.MaxValue,false))
            {

                var pred = Prediction.GetPrediction(target, 0.25f).CastPosition;
                if (pred == null)
                    return false;

                if (SData.LineWidth > 0)
                {
                    var powCalc = Math.Pow(SData.LineWidth + target.BoundingRadius, 2);
                    if (pred.To2D().Distance(End.To2D(), Start.To2D(), true, true) <= powCalc || target.ServerPosition.To2D().Distance(End.To2D(), Start.To2D(), true, true) <= powCalc)
                    {
                        return true;
                    } 
                }
                else if (target.Distance(End) < 50 + target.BoundingRadius || pred.Distance(End) < 50 + target.BoundingRadius)
                {
                    return true;
                }  
            }
            return false;
        }

        public static float GetKsDamage(Obj_AI_Hero t, Spell QWER, bool includeIncomingDamage = true)
        {
            var totalDmg = QWER.GetDamage(t) - t.AllShield;
            totalDmg += GetEchoLudenDamage(t);
            totalDmg -= t.HPRegenRate;

            if (totalDmg > t.Health)
            {
                if (Player.HasBuff("summonerexhaust"))
                    totalDmg = totalDmg * 0.6f;

                if (t.HasBuff("ferocioushowl"))
                    totalDmg = totalDmg * 0.7f;

                if (t.ChampionName == "Blitzcrank" && !t.HasBuff("BlitzcrankManaBarrierCD") && !t.HasBuff("ManaBarrier"))
                {
                    totalDmg -= t.Mana / 2f;
                }
            }
            if(includeIncomingDamage)
                totalDmg += (float)GetIncomingDamage(t);

            return totalDmg;
        }

        public static bool ValidUlt(Obj_AI_Hero target)
        {
            if (target.HasBuffOfType(BuffType.PhysicalImmunity) || target.HasBuffOfType(BuffType.SpellImmunity)
                || target.IsZombie || target.IsInvulnerable || target.HasBuffOfType(BuffType.Invulnerability) || target.HasBuff("kindredrnodeathbuff")
                || target.HasBuffOfType(BuffType.SpellShield) || target.Health - GetIncomingDamage(target, 2) < 1)
                return false;
            else
                return true;
        }

        public static bool CanMove(Obj_AI_Hero target)
        {
            if ( (!target.IsWindingUp && target.IsRooted && !target.CanMove) || target.MoveSpeed < 50 || target.IsStunned || target.HasBuffOfType(BuffType.Stun) || target.HasBuffOfType(BuffType.Fear) || target.HasBuffOfType(BuffType.Snare) || target.HasBuffOfType(BuffType.Knockup)  || target.HasBuff("Recall") ||
                target.HasBuffOfType(BuffType.Knockback) || target.HasBuffOfType(BuffType.Charm) || target.HasBuffOfType(BuffType.Taunt) || target.HasBuffOfType(BuffType.Suppression))
            {
                return false;
            }
            else
                return true;
        }

        public static int GetBuffCount(Obj_AI_Base target, string buffName)
        {
            foreach (var buff in target.Buffs.Where(buff => buff.Name.ToLower() == buffName.ToLower()))
            {
                if (buff.Count == 0)
                    return 1;
                else
                    return buff.Count;
            }
            return 0;
        }

        public static int CountEnemyMinions(Obj_AI_Base target, float range)
        {
            var allMinions = Cache.GetMinions(target.Position, range);
            if (allMinions != null)
                return allMinions.Count;
            else
                return 0;
        }

        public static float GetPassiveTime(Obj_AI_Base target, string buffName)
        {
            return
                target.Buffs.OrderByDescending(buff => buff.EndTime - Game.Time)
                    .Where(buff => buff.Name.ToLower() == buffName.ToLower())
                    .Select(buff => buff.EndTime)
                    .FirstOrDefault() - Game.Time;
        }

        public static Vector3 GetTrapPos(float range)
        {
            foreach (var enemy in HeroManager.Enemies.Where(enemy => enemy.IsValid && enemy.Distance(Player.Position) < range ))
            {
                if(enemy.HasBuffOfType(BuffType.Invulnerability))
                    return enemy.Position;

                if(enemy.HasBuff("BardRStasis") || enemy.HasBuff("zhonyasringshield") || enemy.HasBuff("Meditate"))
                    return enemy.Position;
            }

            foreach (var obj in ObjectManager.Get<Obj_GeneralParticleEmitter>().Where(obj => obj.IsValid && obj.Position.Distance(Player.Position) < range ))
            {
                var name = obj.Name.ToLower();
                
                if (name.Contains("GateMarker_red.troy".ToLower()) || name.Contains("global_ss_teleport_target_red.troy".ToLower()) || name.Contains("R_indicator_red.troy".ToLower()))
                    return obj.Position;

                if (name.Contains("3026_Buff_Revive".ToLower()))
                    foreach (var enemy in HeroManager.Enemies.Where(enemy => enemy.IsValid && enemy.Distance(obj.Position) < 100))
                        return obj.Position;
            }
            return Vector3.Zero;
        }

        public static bool IsMovingInSameDirection(Obj_AI_Base source, Obj_AI_Base target)
        {
            var sourceLW = source.GetWaypoints().Last().To3D();

            if (sourceLW == source.Position || !source.IsMoving)
                return false;

            var targetLW = target.GetWaypoints().Last().To3D();

            if (targetLW == target.Position || !target.IsMoving)
                return false;

            Vector2 pos1 = sourceLW.To2D() - source.Position.To2D();
            Vector2 pos2 = targetLW.To2D() - target.Position.To2D();
            var getAngle = pos1.AngleBetween(pos2);

            if(getAngle < 20)
                return true;
            else
                return false;
        }

        public static bool CollisionYasuo(Vector3 from, Vector3 to)
        {
            if (!YasuoInGame)
                return false;

            if (Game.Time - yasuoWall.CastTime > 4)
                return false;

            var level = yasuoWall.WallLvl;
            var wallWidth = (350 + 50 * level);
            var wallDirection = (yasuoWall.CastPosition.To2D() - yasuoWall.YasuoPosition.To2D()).Normalized().Perpendicular();
            var wallStart = yasuoWall.CastPosition.To2D() + wallWidth / 2f * wallDirection;
            var wallEnd = wallStart - wallWidth * wallDirection;

            if (wallStart.Intersection(wallEnd, to.To2D(), from.To2D()).Intersects)
            {
                return true;
            }
            return false;
        }

        public static void DrawTriangleOKTW(float radius, Vector3 position, System.Drawing.Color color, float bold = 1)
        {
            var positionV2 = Drawing.WorldToScreen(position);
            Vector2 a = new Vector2(positionV2.X + radius, positionV2.Y + radius / 2);
            Vector2 b = new Vector2(positionV2.X - radius, positionV2.Y + radius / 2);
            Vector2 c = new Vector2(positionV2.X, positionV2.Y - radius);
            Drawing.DrawLine(a[0], a[1], b[0], b[1], bold, color);
            Drawing.DrawLine(b[0], b[1], c[0], c[1], bold, color);
            Drawing.DrawLine(c[0], c[1], a[0], a[1], bold, color);
        }

        public static void DrawLineRectangle(Vector3 start2, Vector3 end2, int radius, float width, System.Drawing.Color color)
        {
            Vector2 start = start2.To2D();
            Vector2 end = end2.To2D();
            var dir = (end - start).Normalized();
            var pDir = dir.Perpendicular();

            var rightStartPos = start + pDir * radius;
            var leftStartPos = start - pDir * radius;
            var rightEndPos = end + pDir * radius;
            var leftEndPos = end - pDir * radius;

            var rStartPos = Drawing.WorldToScreen(new Vector3(rightStartPos.X, rightStartPos.Y, ObjectManager.Player.Position.Z));
            var lStartPos = Drawing.WorldToScreen(new Vector3(leftStartPos.X, leftStartPos.Y, ObjectManager.Player.Position.Z));
            var rEndPos = Drawing.WorldToScreen(new Vector3(rightEndPos.X, rightEndPos.Y, ObjectManager.Player.Position.Z));
            var lEndPos = Drawing.WorldToScreen(new Vector3(leftEndPos.X, leftEndPos.Y, ObjectManager.Player.Position.Z));

            Drawing.DrawLine(rStartPos, rEndPos, width, color);
            Drawing.DrawLine(lStartPos, lEndPos, width, color);
            Drawing.DrawLine(rStartPos, lStartPos, width, color);
            Drawing.DrawLine(lEndPos, rEndPos, width, color);
        }

        public static List<Vector3> CirclePoints(float CircleLineSegmentN, float radius, Vector3 position)
        {
            List<Vector3> points = new List<Vector3>();
            for (var i = 1; i <= CircleLineSegmentN; i++)
            {
                var angle = i * 2 * Math.PI / CircleLineSegmentN;
                var point = new Vector3(position.X + radius * (float)Math.Cos(angle), position.Y + radius * (float)Math.Sin(angle), position.Z);
                points.Add(point);
            }
            return points;
        }

        private static void Game_OnWndProc(WndEventArgs args)
        {
            if (args.Msg == 123 && blockMove)
            {
                blockMove = false;
                blockAttack = false;
                Orbwalking.Attack = true;
                Orbwalking.Move = true;
                Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
            }
        }

        public static double GetIncomingDamage(Obj_AI_Hero target, float time = 0.5f, bool skillshots = true)
        {
            double totalDamage = 0;

            foreach (var damage in IncomingDamageList.Where(damage => damage.TargetNetworkId == target.NetworkId && Game.Time - time < damage.Time))
            {
                if (skillshots)
                {
                    totalDamage += damage.Damage;
                }
                else 
                {
                    if (!damage.Skillshot)
                        totalDamage += damage.Damage;
                }
            }
            double damage2 = 0;
            
            foreach (var missile in Cache.MissileList.Where(missile => missile.IsValid && missile.SpellCaster != null && missile.SData != null && missile.SpellCaster.Team != target.Team))
            {
                if (missile.Target != null)
                {
                    if (missile.Target.NetworkId == target.NetworkId)
                    {
                        var damageExtra = missile.SpellCaster.GetSpellDamage((Obj_AI_Base)missile.Target, missile.SData.Name);
                        if(damageExtra == 0)
                            damageExtra += target.Level * 3;
                        damage2 = damageExtra;
                    }
                }


                else if (skillshots)
                {
                    if (target.HasBuffOfType(BuffType.Slow) || target.IsWindingUp || !CanMove(target))
                    {
                        if (CanHitSkillShot(target, missile.StartPosition, missile.EndPosition, missile.SData))
                        {
                            damage2 += missile.SpellCaster.GetSpellDamage((Obj_AI_Base)target, missile.SData.Name);
                        }
                    }
                }
            }

            foreach (var hero in HeroManager.AllHeroes.Where(x=> x.Team != target.Team && x.NetworkId != target.NetworkId ))
            {
                if(!hero.IsDead && hero.Distance(target) < hero.AttackRange + 200 )
                {
                    totalDamage += hero.GetAutoAttackDamage(hero);
                }
            }

            if (damage2 > totalDamage)
                totalDamage = damage2;

            if (target.HasBuffOfType(BuffType.Poison))
                totalDamage += target.Level * 5;
            if (target.HasBuffOfType(BuffType.Damage))
                totalDamage += target.Level * 6;

            return totalDamage;
        }


        private static void Obj_AI_Base_OnDoCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (args.Target != null && args.SData != null)
            {
                if (args.Target.Type == GameObjectType.obj_AI_Hero && !sender.IsMelee && args.Target.Team != sender.Team)
                {
                    IncomingDamageList.Add(new UnitIncomingDamage { Damage = sender.GetSpellDamage((Obj_AI_Base)args.Target, args.SData.Name), TargetNetworkId = args.Target.NetworkId, Time = Game.Time, Skillshot = false });
                }
            }
        }

        private static void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (args.SData == null)
            {
                return;
            }
            /////////////////  HP prediction
            var targed = args.Target as Obj_AI_Base;
            
            if (targed != null)
            {
                if (targed.Type == GameObjectType.obj_AI_Hero && targed.Team != sender.Team && (sender.IsMelee || !args.SData.IsAutoAttack()))
                {
                    IncomingDamageList.Add(new UnitIncomingDamage { Damage = sender.GetSpellDamage(targed, args.SData.Name), TargetNetworkId = args.Target.NetworkId, Time = Game.Time, Skillshot = false });
                }
            }
            else
            {
                foreach (var champion in ChampionList.Where(champion => !champion.IsDead && champion.IsVisible && champion.Team != sender.Team && champion.Distance(sender) < 2000))
                {
                    if (champion.HasBuffOfType(BuffType.Slow) || champion.IsWindingUp || !CanMove(champion))
                    {
                        if (CanHitSkillShot(champion, args.Start, args.End, args.SData))
                        {
                            IncomingDamageList.Add(new UnitIncomingDamage { Damage = sender.GetSpellDamage(champion, args.SData.Name), TargetNetworkId = champion.NetworkId, Time = Game.Time, Skillshot = true });
                        }
                    }
                }
                
                if (!YasuoInGame)
                    return;

                if (!sender.IsEnemy || sender.IsMinion || args.SData.IsAutoAttack() || sender.Type != GameObjectType.obj_AI_Hero)
                    return;

                if (args.SData.Name.Contains("YasuoWMovingWall"))
                {
                    yasuoWall.CastTime = Game.Time;
                    yasuoWall.CastPosition = sender.Position.Extend(args.End, 400);
                    yasuoWall.YasuoPosition = sender.Position;
                    yasuoWall.WallLvl = sender.Spellbook.Spells[1].Level;
                }
            }
        }

        private static void Spellbook_OnCastSpell(Spellbook sender, SpellbookCastSpellEventArgs args)
        {
            if (blockSpells)
            {
                args.Process = false;
            }
        }

        private static void Obj_AI_Base_OnIssueOrder(Obj_AI_Base sender, GameObjectIssueOrderEventArgs args)
        {
            if (!sender.IsMe)
                return;

            if (blockMove && args.Order != GameObjectOrder.AttackUnit)
            {
                args.Process = false;
            }
            if (blockAttack && args.Order == GameObjectOrder.AttackUnit)
            {
                args.Process = false;
            }
        }

    }

    class UnitIncomingDamage
    {
        public int TargetNetworkId { get; set; }
        public float Time { get; set; }
        public double Damage { get; set; }
        public bool Skillshot { get; set; }
    }

    class YasuoWall
    {
        public Vector3 YasuoPosition { get; set; }
        public float CastTime { get; set; }
        public Vector3 CastPosition { get; set; }
        public float WallLvl { get; set; }

        public YasuoWall()
        {
            CastTime = 0;
        }
    }
}
