﻿using System;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using OneKeyToWin_AIO_Sebby.SebbyLib;
using SharpDX;
using SebbyLib;

namespace OneKeyToWin_AIO_Sebby.Champions
{
    class Swain : Base
    {
        private bool _ractive;

        private static string[] Spells =
        {
            "katarinar", "drain", "consume", "absolutezero", "staticfield", "reapthewhirlwind", "jinxw", "jinxr",
            "shenstandunited", "threshe", "threshrpenta", "threshq", "meditate", "caitlynpiltoverpeacemaker",
            "volibearqattack",
            "cassiopeiapetrifyinggaze", "ezrealtrueshotbarrage", "galioidolofdurand", "luxmalicecannon",
            "missfortunebullettime", "infiniteduress", "alzaharnethergrasp", "lucianq", "velkozr", "rocketgrabmissile"
        };

        public Swain()
        {
            Q = new Spell(SpellSlot.Q, 725);
            W = new Spell(SpellSlot.W, 5500);
            E = new Spell(SpellSlot.E, 850);
            R = new Spell(SpellSlot.R, 650);

            Q.SetSkillshot(0.25f, 32f, float.MaxValue, false, SkillshotType.SkillshotCone);
            W.SetSkillshot(0.25f, 325f, float.MaxValue, false, SkillshotType.SkillshotCircle);
            E.SetSkillshot(0.25f, 100f, float.MaxValue, false, SkillshotType.SkillshotLine);

            HeroMenu.SubMenu("Draw")
                .AddItem(new MenuItem("qRange", "Q range", true).SetValue(false));
            HeroMenu.SubMenu("Draw")
                .AddItem(new MenuItem("wRange", "W range", true).SetValue(false));
            HeroMenu.SubMenu("Draw")
                .AddItem(new MenuItem("eRange", "E range", true).SetValue(false));
            HeroMenu.SubMenu("Draw")
                .AddItem(new MenuItem("rRange", "R range", true).SetValue(false));
            HeroMenu.SubMenu("Draw")
                .AddItem(new MenuItem("onlyRdy", "Draw only ready spells", true).SetValue(true));

            HeroMenu.SubMenu("Q Config")
                .AddItem(new MenuItem("autoQ", "Auto Q", true).SetValue(true));
            HeroMenu.SubMenu("Q Config")
                .AddItem(new MenuItem("harassQ", "Harass Q", true).SetValue(true));

            foreach (var enemy in HeroManager.Enemies)
                HeroMenu.SubMenu("Q Config").SubMenu("Use on:")
                    .AddItem(new MenuItem("Quse" + enemy.ChampionName, enemy.ChampionName, true).SetValue(true));

            HeroMenu.SubMenu("W Config")
                .AddItem(new MenuItem("autoW", "Auto W on hard CC", true).SetValue(true));
            HeroMenu.SubMenu("W Config")
                .AddItem(new MenuItem("Wspell", "W on special spell detection", true).SetValue(true));
            HeroMenu.SubMenu("W Config")
                .AddItem(new MenuItem("Int", "W On Interruptable Target", true).SetValue(true));
            HeroMenu.SubMenu("W Config").AddItem(
                new MenuItem("WmodeCombo", "W combo mode", true).SetValue(
                    new StringList(new[] {"always", "run - cheese"}, 1)));
            HeroMenu.SubMenu("W Config")
                .AddItem(new MenuItem("Waoe", "Auto W x enemies", true).SetValue(new Slider(3, 5, 0)));
            HeroMenu.SubMenu("W Config").SubMenu("W Gap Closer").AddItem(
                new MenuItem("WmodeGC", "Gap Closer position mode", true).SetValue(
                    new StringList(new[] {"Dash end position", "My hero position"}, 0)));
            foreach (var enemy in HeroManager.Enemies)
                HeroMenu.SubMenu("W Config").SubMenu("W Gap Closer")
                    .SubMenu("Cast on enemy:")
                    .AddItem(new MenuItem("WGCchampion" + enemy.ChampionName, enemy.ChampionName, true).SetValue(true));

            HeroMenu.SubMenu("E Config")
                .AddItem(new MenuItem("autoE", "Auto E", true).SetValue(true));
            HeroMenu.SubMenu("E Config")
                .AddItem(new MenuItem("harassE", "Harass E", true).SetValue(true));
            foreach (var enemy in HeroManager.Enemies)
                HeroMenu.SubMenu("E Config").SubMenu("Use on:")
                    .AddItem(new MenuItem("Euse" + enemy.ChampionName, enemy.ChampionName, true).SetValue(true));

            HeroMenu.SubMenu("R Config")
                .AddItem(new MenuItem("autoR", "Auto R", true).SetValue(true));
            HeroMenu.SubMenu("R Config")
                .AddItem(new MenuItem("Raoe", "Auto R if x enemies in range", true).SetValue(new Slider(2, 5, 1)));

            HeroMenu.SubMenu("Farm")
                .AddItem(new MenuItem("farmW", "Lane clear W", true).SetValue(true));
            HeroMenu.SubMenu("Farm")
                .AddItem(new MenuItem("jungleE", "Jungle clear E", true).SetValue(true));
            HeroMenu.SubMenu("Farm")
                .AddItem(new MenuItem("jungleQ", "Jungle clear Q", true).SetValue(true));
            HeroMenu.SubMenu("Farm")
                .AddItem(new MenuItem("jungleW", "Jungle clear W", true).SetValue(true));
            HeroMenu.SubMenu("Farm")
                .AddItem(new MenuItem("jungleE", "Jungle clear E", true).SetValue(true));

            Drawing.OnDraw += Drawing_OnDraw;
            Game.OnUpdate += Game_OnGameUpdate;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
            Interrupter2.OnInterruptableTarget += Interrupter2_OnInterruptableTarget;
        }

        private void Interrupter2_OnInterruptableTarget(Obj_AI_Hero sender,
            Interrupter2.InterruptableTargetEventArgs args)
        {
            if (W.IsReady() && MainMenu.Item("Int", true).GetValue<bool>() && sender.IsValidTarget(W.Range))
                W.Cast(sender.Position);
        }

        private void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!W.IsReady() || sender.IsMinion || !sender.IsEnemy || !MainMenu.Item("Wspell", true).GetValue<bool>() ||
                !sender.IsValid<Obj_AI_Hero>() || !sender.IsValidTarget(W.Range))
                return;

            var foundSpell = Spells.Find(x => args.SData.Name.ToLower() == x);
            if (foundSpell != null)
            {
                W.Cast(sender.Position);
            }
        }

        private void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (W.IsReady() && Player.Mana > RMANA + WMANA)
            {
                var t = gapcloser.Sender;
                if (t.IsValidTarget(W.Range) && MainMenu.Item("WGCchampion" + t.ChampionName, true).GetValue<bool>())
                {
                    if (MainMenu.Item("WmodeGC", true).GetValue<StringList>().SelectedIndex == 0)
                        W.Cast(gapcloser.End);
                    else
                        W.Cast(Player.ServerPosition);
                }
            }
        }

        private void Game_OnGameUpdate(EventArgs args)
        {
            if (Program.LagFree(0))
            {
                SetMana();
                _ractive = Player.HasBuff("SwainMetamorphism");
                Jungle();
            }

            if (Program.LagFree(1) && E.IsReady() && MainMenu.Item("autoE", true).GetValue<bool>())
                LogicE();

            if (Program.LagFree(2) && Q.IsReady() && MainMenu.Item("autoQ", true).GetValue<bool>())
                LogicQ();

            if (Program.LagFree(3) && W.IsReady())
                LogicW();

            if (Program.LagFree(4) && R.IsReady() && MainMenu.Item("autoR", true).GetValue<bool>())
                LogicR();
        }

        private void LogicR()
        {
            var countAoe = Player.CountEnemiesInRange(R.Range);
            if (countAoe > 0)
            {
                if (Program.Combo && MainMenu.Item("autoR", true).GetValue<bool>())
                    R.Cast();
                else if (Program.Harass && MainMenu.Item("harassR", true).GetValue<bool>())
                    R.Cast();
                else if (countAoe >= MainMenu.Item("Raoe", true).GetValue<Slider>().Value)
                    R.Cast();
            }
        }

        private void LogicW()
        {
            var t = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Magical);
            if (t.IsValidTarget())
            {
                if (Program.Combo)
                {
                    if (MainMenu.Item("WmodeCombo", true).GetValue<StringList>().SelectedIndex == 1)
                    {
                        if (W.GetPrediction(t).CastPosition.Distance(t.Position) > 100)
                        {
                            if (Player.Position.Distance(t.ServerPosition) > Player.Position.Distance(t.Position))
                            {
                                if (t.Position.Distance(Player.ServerPosition) < t.Position.Distance(Player.Position))
                                    Program.CastSpell(W, t);
                            }
                            else
                            {
                                if (t.Position.Distance(Player.ServerPosition) > t.Position.Distance(Player.Position))
                                    Program.CastSpell(W, t);
                            }
                        }
                    }
                    else
                    {
                        Program.CastSpell(W, t);
                    }
                }

                W.CastIfWillHit(t, MainMenu.Item("Waoe", true).GetValue<Slider>().Value);
            }
            else if (FarmSpells && MainMenu.Item("farmW", true).GetValue<bool>())
            {
                var minionList = Cache.GetMinions(Player.ServerPosition, W.Range);
                var farmPosition = W.GetCircularFarmLocation(minionList, W.Width);

                if (farmPosition.MinionsHit >= FarmMinions)
                    W.Cast(farmPosition.Position);
            }

            if (MainMenu.Item("autoW", true).GetValue<bool>())
                foreach (var enemy in HeroManager.Enemies.Where(enemy =>
                    enemy.IsValidTarget(W.Range) && !OktwCommon.CanMove(enemy)))
                    W.Cast(enemy, true);
        }

        private void LogicQ()
        {
            var t = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Magical);
            if (t.IsValidTarget())
            {
                if (t.Health < OktwCommon.GetKsDamage(t, Q) + E.GetDamage(t))
                    Program.CastSpell(Q, t);
                if (!MainMenu.Item("Quse" + t.ChampionName, true).GetValue<bool>())
                    return;
                if (Program.Combo && Player.Mana > RMANA + EMANA)
                    Program.CastSpell(Q, t);
                else if (Program.Harass && MainMenu.Item("harassQ", true).GetValue<bool>() &&
                         Player.Mana > RMANA + EMANA + WMANA + EMANA &&
                         MainMenu.Item("Harass" + t.ChampionName).GetValue<bool>())
                    Program.CastSpell(Q, t);
                else if ((Program.Combo || Program.Harass))
                {
                    foreach (var enemy in HeroManager.Enemies.Where(enemy =>
                        enemy.IsValidTarget(Q.Range) && !OktwCommon.CanMove(enemy)))
                        Program.CastSpell(Q, enemy);
                }
            }
        }

        private void LogicE()
        {
            var t = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Magical);
            if (t.IsValidTarget())
            {
                if (t.Health < E.GetDamage(t) + OktwCommon.GetKsDamage(t, Q))
                    Program.CastSpell(Q, t);
                if (!MainMenu.Item("Euse" + t.ChampionName, true).GetValue<bool>())
                    return;
                if (Program.Combo && Player.Mana > RMANA + EMANA)
                    Program.CastSpell(Q, t);
                else if (Program.Harass && MainMenu.Item("harassE", true).GetValue<bool>() &&
                         Player.Mana > RMANA + EMANA + WMANA + EMANA &&
                         MainMenu.Item("Harass" + t.ChampionName).GetValue<bool>())
                    Program.CastSpell(Q, t);
            }
        }

        private void Jungle()
        {
            if (Program.LaneClear)
            {
                var mobs = Cache.GetMinions(Player.ServerPosition, Q.Range, MinionTeam.Neutral);
                if (mobs.Count > 0)
                {
                    var mob = mobs[0];

                    if (W.IsReady() && MainMenu.Item("jungleW", true).GetValue<bool>())
                    {
                        W.Cast(mob.ServerPosition);
                        return;
                    }

                    if (E.IsReady() && MainMenu.Item("jungleE", true).GetValue<bool>())
                    {
                        E.CastOnUnit(mob);
                        return;
                    }

                    if (Q.IsReady() && MainMenu.Item("jungleQ", true).GetValue<bool>())
                    {
                        Q.CastOnUnit(mob);
                        return;
                    }
                }
            }
        }

        private void SetMana()
        {
            if ((MainMenu.Item("manaDisable", true).GetValue<bool>() && Program.Combo) || Player.HealthPercent < 20)
            {
                QMANA = 0;
                WMANA = 0;
                EMANA = 0;
                RMANA = 0;
                return;
            }

            QMANA = Q.Instance.ManaCost;
            WMANA = W.Instance.ManaCost;
            EMANA = E.Instance.ManaCost;

            if (!R.IsReady())
                RMANA = WMANA - Player.PARRegenRate * W.Instance.Cooldown;
            else
                RMANA = R.Instance.ManaCost;
        }

        public static void drawLine(Vector3 pos1, Vector3 pos2, int bold, System.Drawing.Color color)
        {
            var wts1 = Drawing.WorldToScreen(pos1);
            var wts2 = Drawing.WorldToScreen(pos2);

            Drawing.DrawLine(wts1[0], wts1[1], wts2[0], wts2[1], bold, color);
        }

        private void Drawing_OnDraw(EventArgs args)
        {
            if (MainMenu.Item("qRange", true).GetValue<bool>())
            {
                if (MainMenu.Item("onlyRdy", true).GetValue<bool>())
                {
                    if (Q.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, Q.Range, System.Drawing.Color.Cyan, 1, 1);
                }
                else
                    Utility.DrawCircle(ObjectManager.Player.Position, Q.Range, System.Drawing.Color.Cyan, 1, 1);
            }

            if (MainMenu.Item("wRange", true).GetValue<bool>())
            {
                if (MainMenu.Item("onlyRdy", true).GetValue<bool>())
                {
                    if (W.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, W.Range, System.Drawing.Color.Orange, 1, 1);
                }
                else
                    Utility.DrawCircle(ObjectManager.Player.Position, W.Range, System.Drawing.Color.Orange, 1, 1);
            }

            if (MainMenu.Item("eRange", true).GetValue<bool>())
            {
                if (MainMenu.Item("onlyRdy", true).GetValue<bool>())
                {
                    if (E.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.Yellow, 1, 1);
                }
                else
                    Utility.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.Yellow, 1, 1);
            }

            if (MainMenu.Item("rRange", true).GetValue<bool>())
            {
                if (MainMenu.Item("onlyRdy", true).GetValue<bool>())
                {
                    if (R.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, R.Range, System.Drawing.Color.Gray, 1, 1);
                }
                else
                    Utility.DrawCircle(ObjectManager.Player.Position, R.Range, System.Drawing.Color.Gray, 1, 1);
            }
        }
    }
}