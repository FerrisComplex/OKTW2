﻿using System;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using OneKeyToWin_AIO_Sebby.SebbyLib;
using SebbyLib;

namespace OneKeyToWin_AIO_Sebby.Champions
{
    class Garen : Base
    {
        public Garen()
        {
            Q = new Spell(SpellSlot.Q);
            W = new Spell(SpellSlot.W);
            E = new Spell(SpellSlot.E, 325);
            R = new Spell(SpellSlot.R, 400);

            HeroMenu.SubMenu("Draw")
                .AddItem(new MenuItem("qRange", "Q range", true).SetValue(false));
            HeroMenu.SubMenu("Draw")
                .AddItem(new MenuItem("eRange", "E range", true).SetValue(false));
            HeroMenu.SubMenu("Draw")
                .AddItem(new MenuItem("rRange", "R range", true).SetValue(false));
            HeroMenu.SubMenu("Draw")
                .AddItem(new MenuItem("onlyRdy", "Draw when skill rdy", true).SetValue(true));

            foreach (var enemy in HeroManager.Enemies)
                HeroMenu.SubMenu("E Config").SubMenu("Use E on")
                    .AddItem(new MenuItem("Eon" + enemy.ChampionName, enemy.ChampionName, true).SetValue(true));

            HeroMenu.SubMenu("R option")
                .AddItem(new MenuItem("autoR", "Auto R", true).SetValue(true));
            HeroMenu.SubMenu("R option").AddItem(
                new MenuItem("useR", "Semi-manual cast R key", true).SetValue(new KeyBind("T".ToCharArray()[0],
                    KeyBindType.Press))); //32 == space

            HeroMenu.SubMenu("Farm")
                .AddItem(new MenuItem("farmE", "Farm W", true).SetValue(true));
            HeroMenu.SubMenu("Farm")
                .AddItem(new MenuItem("farmQ", "Farm Q", true).SetValue(true));

            Game.OnUpdate += Game_OnGameUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            Orbwalking.AfterAttack += afterAttack;
            Interrupter2.OnInterruptableTarget += Interrupter2OnOnInterruptableTarget;
        }

        private void Interrupter2OnOnInterruptableTarget(Obj_AI_Hero sender,
            Interrupter2.InterruptableTargetEventArgs args)
        {
            if (Q.IsReady() && sender.IsValidTarget(E.Range))
            {
                Q.Cast();
                Player.IssueOrder(GameObjectOrder.AutoAttack, sender);
            }
        }

        private void afterAttack(AttackableUnit unit, AttackableUnit target)
        {
            if (!Q.IsReady() || !unit.IsMe)
                return;

            var t = target as Obj_AI_Base;

            if (t.IsValidTarget())
                Q.Cast();
        }

        private void Game_OnGameUpdate(EventArgs args)
        {
            if (R.IsReady() && MainMenu.Item("useR", true).GetValue<KeyBind>().Active)
            {
                var targetR = TargetSelector.GetTarget(R.Range, TargetSelector.DamageType.True);
                if (targetR.IsValidTarget())
                {
                    R.Cast(targetR, true);
                }
            }

            if (MainMenu.Item("autoR", true).GetValue<bool>())
                LogicR();

            if (Program.LagFree(2) && Q.IsReady())
                LogicQ();
            if (Program.LagFree(1) && W.IsReady())
                LogicW();
            if (Program.LagFree(2) && E.IsReady())
                LogicE();
        }

        private void LogicQ()
        {
            if (Program.LaneClear)
            {
                Q.Cast();
            }
        }

        private void LogicW()
        {
            double dmg = OktwCommon.GetIncomingDamage(Player);

            int nearEnemys = Player.CountEnemiesInRange(800);

            int sensitivity = 20;

            double hpPercentage = (dmg * 100) / Player.Health;

            if (Player.HasBuffOfType(BuffType.Poison))
            {
                W.Cast();
            }

            nearEnemys = (nearEnemys == 0) ? 1 : nearEnemys;

            if (dmg > 100 + Player.Level * sensitivity)
                W.Cast();
            else if (Player.Health - dmg < nearEnemys * Player.Level * sensitivity)
                W.Cast();
            else if (hpPercentage >= 5)
                W.Cast();
        }

        private void LogicE()
        {
            var target = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Physical);
            if (target.IsValidTarget() && E.IsInRange(target))
            {
                E.Cast();
            }
            else if (Program.LaneClear)
            {
                E.Cast();
            }
        }

        public double GetRTargetDamage(Obj_AI_Hero hero)
        {
            return (R.Level * 150) + (hero.MaxHealth - hero.Health) * (0.15 + 0.05 * R.Level);
        }

        private void LogicR()
        {
            foreach (var target in HeroManager.Enemies.Where(target =>
                target.IsValidTarget(R.Range) && OktwCommon.ValidUlt(target)))
            {
                var dmgR = GetRTargetDamage(target);

                if (dmgR > target.Health)
                {
                    R.Cast(target);
                }
            }
        }

        private void Drawing_OnDraw(EventArgs args)
        {
            if (MainMenu.Item("qRange", true).GetValue<bool>())
            {
                if (MainMenu.Item("onlyRdy", true).GetValue<bool>() && Q.IsReady())
                    if (Q.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, Q.Range, System.Drawing.Color.Cyan, 1, 1);
                    else
                        Utility.DrawCircle(ObjectManager.Player.Position, Q.Range, System.Drawing.Color.Cyan, 1, 1);
            }

            if (MainMenu.Item("eRange", true).GetValue<bool>())
            {
                if (MainMenu.Item("onlyRdy", true).GetValue<bool>() && E.IsReady())
                    if (E.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.Orange, 1, 1);
                    else
                        Utility.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.Orange, 1, 1);
            }

            if (MainMenu.Item("rRange", true).GetValue<bool>())
            {
                if (MainMenu.Item("onlyRdy", true).GetValue<bool>() && R.IsReady())
                    if (R.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, R.Range, System.Drawing.Color.Red, 1, 1);
                    else
                        Utility.DrawCircle(ObjectManager.Player.Position, R.Range, System.Drawing.Color.Red, 1, 1);
            }
        }
    }
}