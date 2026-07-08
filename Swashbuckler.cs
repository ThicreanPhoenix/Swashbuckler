using Dawnsbury.Audio;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder;
using Dawnsbury.Core.CharacterBuilder.AbilityScores;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.Feats.Features;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines.Options.Reactive;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Intelligence;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.ReactiveAttacks;
using Dawnsbury.Core.Mechanics.Rules;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Display;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Display.Text;
using Dawnsbury.Modding;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Dawnsbury.Mods.Phoenix;

public class AddSwash
{
    public static Trait SwashTrait = ModManager.RegisterTrait("SwashTrait", new TraitProperties("Swashbuckler", true) { IsClassTrait = true });
    public static Trait SwashStyle = ModManager.RegisterTrait("SwashStyle", new TraitProperties("Swashbuckler Style", false));
    public static Trait Finisher = ModManager.RegisterTrait("Finisher", new TraitProperties("Finisher", true, "You can only use an action with the Finisher trait if you have panache, and you lose panache after performing the action.", true) { BackgroundColor = Color.BurlyWood });
    public static Trait OpportuneRiposteTrait = ModManager.RegisterTrait("OpportuneRiposteTrait", new TraitProperties("Opportune Riposte", false));
    public static QEffectId PanacheId = ModManager.RegisterEnumMember<QEffectId>("Panache");
    public static QEffectId PreciseStrikeEffectId = ModManager.RegisterEnumMember<QEffectId>("PreciseStrikeEffect");
    public static QEffectId FascinatedId = ModManager.RegisterEnumMember<QEffectId>("Fascinated");
    public static QEffectId PreciseFinisherQEffectId = ModManager.RegisterEnumMember<QEffectId>("PreciseFinisherQEffectId");
    public static ActionId LeadingDanceId = ModManager.RegisterEnumMember<ActionId>("LeadingDance");
    public static ActionId FascinatingPerformanceActionId = ModManager.RegisterEnumMember<ActionId>("FascinatingPerformanceAction");
    public static FeatName BattledancerStyle = ModManager.RegisterFeatName("Battledancer");
    public static FeatName BraggartStyle = ModManager.RegisterFeatName("Braggart");
    public static FeatName FencerStyle = ModManager.RegisterFeatName("Fencer");
    public static FeatName GymnastStyle = ModManager.RegisterFeatName("Gymnast");
    public static FeatName WitStyle = ModManager.RegisterFeatName("Wit");
    public static QEffect CreatePanache(Skill styleSkill)
    {
        QEffect panache = new QEffect("Panache", "You have a +5-foot status bonus to your Speed and a +1 circumstance bonus to Acrobatics and " + styleSkill.HumanizeTitleCase2() + " skill checks.\n\nYou can use finishers by spending panache.", ExpirationCondition.Never, null, new ModdedIllustration("PhoenixAssets/panache.PNG"))
        {
            Id = PanacheId,
            Key = "Panache",
            BonusToAllSpeeds = (qfpanache) => new Bonus(1, BonusType.Status, "Panache"),
            BonusToSkillChecks = (skill, action, creature) =>
            {
                //NOTE: In tabletop, the bonus from panache is applied only to checks that would grant panache. The current implementation applies it to all skills of the style's type.
                //      This is based on a misunderstanding of the text. I need to figure out whether to change it or make it an intentional buff from tabletop.
                if (skill == Skill.Acrobatics || skill == styleSkill)
                {
                    return new Bonus(1, BonusType.Status, "Panache");
                }
                else return null;
            },
            YouBeginAction = async (qfpanache, action) =>
            {
                if (action.HasTrait(Finisher) && !qfpanache.CannotExpireThisTurn)
                {
                    qfpanache.ExpiresAt = ExpirationCondition.Immediately;
                }
            },
            CountsAsABuff = true
        };
        return panache;
    }

    public static QEffect CreateFascinated(Creature source)
    {
        //IDEA: Buff Fascinated to ban all Concentrate actions, and only expire if the creature (not their allies) is targeted by a hostile action.
        List<QEffect> list = new List<QEffect>();
        foreach (Creature c in source.Battle.AllCreatures)
        {
            if (c.EnemyOf(source))
            {
                QEffect qf = new QEffect()
                {
                    AfterYouAreTargeted = async (qf, action) =>
                    {
                        if (action.IsHostileAction && action.ActionId != FascinatingPerformanceActionId)
                        {
                            foreach (QEffect qfct in list)
                            {
                                qfct.ExpiresAt = ExpirationCondition.Immediately;
                            }
                        }
                    }
                };
                list.Add(qf);
                c.AddQEffect(qf);
            }
        }

        QEffect fascinate = new QEffect()
        {
            Name = "Fascinated by " + source.Name,
            Id = FascinatedId,
            Description = "You have a -2 to Perception and skill checks, and can only use Concentrate actions if they target " + source.Name + ". Ends early if you or an ally are targeted by a hostile action.",
            Illustration = IllustrationName.RoaringApplause,
            BonusToPerception = (fct) => new Bonus(-2, BonusType.Status, "Fascinated"),
            BonusToSkillChecks = (skill, action, target) => new Bonus(-2, BonusType.Status, "Fascinated"),
            YouAreTargeted = async (fct, action) =>
            {
                if (action.IsHostileAction && action.ActionId != FascinatingPerformanceActionId)
                {
                    fct.ExpiresAt = ExpirationCondition.Immediately;
                }
            },
            PreventTakingAction = (action) =>
            {
                if (action.HasTrait(Trait.Concentrate) && !action.Targets(source))
                {
                    return "fascinated by " + source.Name;
                }
                else return null;
            },
            WhenExpires = async (qf) =>
            {
                foreach (QEffect qfct in list)
                {
                    qfct.ExpiresAt = ExpirationCondition.Immediately;
                }
            },
            CountsAsADebuff = true
        };
        list.Add(fascinate);
        return fascinate;
    }

    // TODO I want to make this apply automatically after every finisher, actually. The reason I didn't was because of two-attack finishers like Dual Finisher, but I'm smarter now.
    public static void FinisherExhaustion(Creature swash)
    {
        swash.AddQEffect(new QEffect("Used Finisher", "You can't take any Attack actions.", ExpirationCondition.ExpiresAtEndOfYourTurn, swash, IllustrationName.Fatigued)
        {
            PreventTakingAction = action => !action.HasTrait(Trait.Attack) ? null : "You used a finisher this turn."
        });
    }
    
    public static QEffect PreciseStrikeEffect(int damageDiceCount)
    {
        return new QEffect("Precise Strike", "If you have panache, you deal an extra " + damageDiceCount.ToString() + " damage. (" + damageDiceCount.ToString() + "d6 with finishers.)")
        {
            Id = PreciseStrikeEffectId,
            Value = damageDiceCount,
            YourStrikeMayDealPrecisionDamage = (qf, action, defender) =>
            {
                bool flag = action.HasTrait(Trait.Agile) || action.HasTrait(Trait.Finesse);
                bool flag2 = !action.HasTrait(Trait.Ranged) || (action.HasTrait(Trait.Thrown) && (action.Owner.HasFeat(FlyingBlade.FeatName) && (defender.DistanceTo(qf.Owner) <= action.Item!.WeaponProperties!.RangeIncrement)));
                bool flag3 = defender.IsImmuneTo(Trait.PrecisionDamage);
                if (flag && flag2 && !flag3)
                {
                    if (action.HasTrait(Finisher))
                    {
                        return DiceFormula.FromText(damageDiceCount.ToString() + "d6", "Precise Strike");
                    }
                    else if (qf.Owner.HasEffect(PanacheId)) return DiceFormula.FromText(damageDiceCount.ToString(), "Precise Strike");
                }
                return null;
            }
        };
    }

    public static int GetPreciseStrikeDamage(Creature creature)
    {
        QEffect preciseStrikeEffect = creature.FindQEffect(PreciseStrikeEffectId);
        if (preciseStrikeEffect == null) return 0;
        return preciseStrikeEffect.Value;
    }

    public static CombatAction CreateConfidentFinisher(Creature swash, Item item, bool thrown, StrikeModifiers modifiers)
    {
        CombatAction conffinish = StrikeRules.CreateStrike(swash, item, thrown ? RangeKind.Ranged : RangeKind.Melee, -1, thrown, modifiers)
            .WithActionCost(1)
            .WithExtraTrait(Finisher)
            .WithExtraTrait(Trait.Basic);

        conffinish.WithFullRename("Confident Finisher");
        conffinish.Illustration = new SideBySideIllustration(item.Illustration, IllustrationName.StarHit);
        conffinish.Description = StrikeRules.CreateBasicStrikeDescription2(conffinish.StrikeModifiers, null, null, null, "The target takes " + GetPreciseStrikeDamage(swash) + "d6/2 damage.", "You lose panache.");
        conffinish.StrikeModifiers.OnEachTarget = async delegate (Creature owner, Creature victim, CheckResult result)
        {
            int preciseDamage = GetPreciseStrikeDamage(owner);
            if (result == CheckResult.Failure)
            {
                HalfDiceFormula halfdamage = new HalfDiceFormula(DiceFormula.FromText(preciseDamage.ToString() + "d6", "Precise Strike"), "Miss with Confident Finisher");
                DiceFormula fulldamage = DiceFormula.FromText(preciseDamage.ToString() + "d6", "Miss with Precise Finisher");
                await CommonSpellEffects.DealDirectDamage(conffinish, swash.HasEffect(PreciseFinisherQEffectId) ? fulldamage : halfdamage, victim, result, conffinish.StrikeModifiers.CalculatedItem.WeaponProperties.DamageKind);
            }
            FinisherExhaustion(owner);
        };
        return conffinish;
    }

    public static CombatAction CreateUnbalancingFinisher(Creature swash, Item item, bool thrown, StrikeModifiers modifiers)
    {
        CombatAction unbal = StrikeRules.CreateStrike(swash, item, thrown ? RangeKind.Ranged : RangeKind.Melee, -1, thrown, modifiers)
            .WithDescription(StrikeRules.CreateBasicStrikeDescription2(modifiers, null, "The target is {r}flat-footed{/r} until the end of your next turn.", null, null, "You lose panache."))
            .WithActionCost(1)
            .WithExtraTrait(Finisher)
            .WithExtraTrait(Trait.Basic)
            .WithEffectOnEachTarget(async (spell, caster, target, result) =>
            {
                FinisherExhaustion(caster);
                if (result >= CheckResult.Success)
                {
                    target.AddQEffect(QEffect.FlatFooted("unbalancing finisher")
                        .WithExpirationAtEndOfSourcesNextTurn(caster, true));
                }
            });
        unbal.WithFullRename("Unbalancing Finisher");
        unbal.Illustration = new SideBySideIllustration(item.Illustration, IllustrationName.Trip);
        return unbal;
    }

    public static CombatAction CreateBleedingFinisher(Creature swash, Item item, bool thrown, StrikeModifiers modifiers)
    {
        CombatAction combatAction = StrikeRules.CreateStrike(swash, item, thrown ? RangeKind.Ranged : RangeKind.Melee, -1, thrown, modifiers)
            .WithActionCost(1)
            .WithExtraTrait(Finisher)
            .WithExtraTrait(Trait.Basic)
            .WithDescription(StrikeRules.CreateBasicStrikeDescription2(modifiers, null, "The target takes " + GetPreciseStrikeDamage(swash).ToString() + "d6 persistent bleed damage.", null, null, "You lose panache."));
        combatAction.WithFullRename("Bleeding Finisher");
        combatAction.Illustration = new SideBySideIllustration(item.Illustration, IllustrationName.BloodVendetta);
        combatAction.StrikeModifiers.OnEachTarget += async (owner, victim, result) =>
        {
            if (result >= CheckResult.Success)
            {
                victim.AddQEffect(QEffect.PersistentDamage(GetPreciseStrikeDamage(owner).ToString() + "d6", DamageKind.Bleed));
            }
            FinisherExhaustion(swash);
        };
        return combatAction;
    }

    public static CombatAction CreateStunningFinisher(Creature swash, Item item, bool thrown, StrikeModifiers modifiers)
    {
        CombatAction stun = StrikeRules.CreateStrike(swash, item, thrown ? RangeKind.Ranged : RangeKind.Melee, -1, thrown, modifiers)
            .WithDescription(StrikeRules.CreateBasicStrikeDescription2(modifiers, null, "The target makes a DC " + swash.ClassOrSpellDC() + " Fortitude save (this is an {r}incapacitation{/r} effect). On a success, it can't take reactions for 1 turn. On a failure, it is {r}stunned{/r} 1, and on a critical failure, it is stunned 3.", null, null, "You lose panache."))
            .WithActionCost(1)
            .WithExtraTrait(Finisher)
            .WithExtraTrait(Trait.Basic);
        stun.WithFullRename("Stunning Finisher");
        stun.Illustration = new SideBySideIllustration(item.Illustration, IllustrationName.Stunned);
        stun.StrikeModifiers.OnEachTarget += async (owner, victim, result) =>
        {
            FinisherExhaustion(owner);
            if (result >= CheckResult.Success)
            { 
                CombatAction theactualstun = CombatAction.CreateSimple(owner, "Stunning Finisher", [ Trait.Incapacitation ]);
                switch (await CommonSpellEffects.RollSavingThrowAsync(victim, theactualstun, Defense.Fortitude, owner.ClassOrSpellDC()))
                {
                    case CheckResult.Success:
                        victim.AddQEffect(new QEffect(ExpirationCondition.ExpiresAtStartOfYourTurn)
                        {
                            Name = "Cannot take reactions",
                            Illustration = IllustrationName.ReactionUsedUp,
                            Description = "You cannot take reactions until the start of your turn.",
                            Id = QEffectId.CannotTakeReactions
                        });
                        break;
                    case CheckResult.Failure:
                        victim.AddQEffect(QEffect.Stunned(1));
                        break;
                    case CheckResult.CriticalFailure:
                        victim.AddQEffect(QEffect.Stunned(3));
                        break;
                }
            }
        };
        return stun;
    }
    
    public static CombatAction CreateTargetingFinisherHead(Creature swash, Item item, bool thrown, StrikeModifiers modifiers)
    {
        var headModifiers = modifiers;
        var targetingHead = StrikeRules.CreateStrike(swash, item, thrown ? RangeKind.Ranged : RangeKind.Melee, -1, thrown, headModifiers)
            .WithActionCost(1)
            .WithExtraTrait(Finisher)
            .WithExtraTrait(Trait.Basic);
        targetingHead.WithFullRename("Targeting Finisher (Head)");
        targetingHead.StrikeModifiers.OnEachTarget += async (owner, victim, result) =>
        {
            if (result >= CheckResult.Success)
            {
                var headStupefied = QEffect.Stupefied(2).WithExpirationAtEndOfSourcesNextTurn(owner, true);
                if (result == CheckResult.CriticalSuccess)
                    headStupefied.WhenExpires += (qf) => { qf.Owner.AddQEffect(QEffect.Stupefied(1)); };
                victim.AddQEffect(headStupefied);
            }
            FinisherExhaustion(owner);
        };
        targetingHead.Description = StrikeRules.CreateBasicStrikeDescription2(modifiers, null, "The target is stupefied 2 until the end of your next turn.", "The target is stupefied 2 until the end of your next turn and stupefied 1 for the rest of the encounter.", additionalAftertext: "You lose panache.");

        return targetingHead;
    }
    public static CombatAction CreateTargetingFinisherArm(Creature swash, Item item, bool thrown, StrikeModifiers modifiers)
    {
        var armModifiers = modifiers;
        var targetingArm = StrikeRules.CreateStrike(swash, item, thrown ? RangeKind.Ranged : RangeKind.Melee, -1, thrown, armModifiers)
            .WithActionCost(1)
            .WithExtraTrait(Finisher)
            .WithExtraTrait(Trait.Basic);
        targetingArm.WithFullRename("Targeting Finisher (Arm)");
        targetingArm.StrikeModifiers.OnEachTarget += async (owner, victim, result) =>
        {
            if (result >= CheckResult.Success)
            {
                var armEnfeebled = QEffect.Enfeebled(2).WithExpirationAtEndOfSourcesNextTurn(owner, true);
                if (result == CheckResult.CriticalSuccess)
                    armEnfeebled.WhenExpires += (qf) => { qf.Owner.AddQEffect(QEffect.Enfeebled(1)); };
                victim.AddQEffect(armEnfeebled);
            }
            FinisherExhaustion(owner);
        };
        targetingArm.Description = StrikeRules.CreateBasicStrikeDescription2(modifiers, null, "The target is enfeebled 2 until the end of your next turn.", "The target is enfeebled 2 until the end of your next turn and enfeebled 1 for the rest of the encounter.", additionalAftertext: "You lose panache.");
        
        return targetingArm;
    }
    public static CombatAction CreateTargetingFinisherLeg(Creature swash, Item item, bool thrown, StrikeModifiers modifiers)
    {
        var legModifiers = modifiers;
        var targetingLeg = StrikeRules.CreateStrike(swash, item, thrown ? RangeKind.Ranged : RangeKind.Melee, -1, thrown, legModifiers)
            .WithActionCost(1)
            .WithExtraTrait(Finisher)
            .WithExtraTrait(Trait.Basic);
        targetingLeg.WithFullRename("Targeting Finisher (Legs)");
        targetingLeg.StrikeModifiers.OnEachTarget += async (owner, victim, result) =>
        {
            if (result >= CheckResult.Success)
            {
                var legsSpeedPenalty = QEffect.StatusSpeedReduction(2).WithExpirationAtEndOfSourcesNextTurn(owner, true);
                if (result == CheckResult.CriticalSuccess)
                    legsSpeedPenalty.WhenExpires += (qf) => { qf.Owner.AddQEffect(QEffect.StatusSpeedReduction(1)); };
                victim.AddQEffect(legsSpeedPenalty);
            }
            FinisherExhaustion(owner);
        };
        targetingLeg.Description = StrikeRules.CreateBasicStrikeDescription2(modifiers, null, "The target takes a -10-foot status penalty to its Speed until the end of your next turn.", "The target takes a -10-foot status penalty to its Speed until the end of your turn and takes a -5-foot status penalty to its Speed for the rest of the encounter.", additionalAftertext: "You lose panache.");
        return targetingLeg;
    }

    public static CombatAction CreatePerfectFinisher(Creature swash, Item item, bool thrown, StrikeModifiers modifiers)
    {
        var perfectFinisher = StrikeRules.CreateStrike(swash, item, thrown ? RangeKind.Ranged : RangeKind.Melee, -1, thrown, modifiers)
            .WithActionCost(1)
            .WithExtraTrait(Finisher)
            .WithExtraTrait(Trait.Fortune)
            .WithExtraTrait(Trait.Basic);
        perfectFinisher.WithFullRename("Perfect Finisher");
        perfectFinisher.Description = StrikeRules.CreateBasicStrikeDescription2(modifiers, additionalAttackRollText: "You roll twice and use the higher result.", additionalAftertext: "You lose panache.");
        perfectFinisher.StrikeModifiers.QEffectForStrike = new QEffect()
        {
            ProvideFortuneEffectForActiveRolls = (qf, action, target) => action == perfectFinisher ? "Perfect Finisher" : null,
            RerollActiveRoll = async (self, breakdown, action, target) => action == perfectFinisher ? RerollDirection.RerollAndKeepBest : RerollDirection.DoNothing
        };
        return perfectFinisher;
    }

    public static Feat Swashbuckler = new ClassSelectionFeat(ModManager.RegisterFeatName("Swashbuckler"), "Many warriors rely on brute force, weighty armor, or cumbersome weapons. For you, battle is a dance where you move among foes with style and grace. You dart among combatants with flair and land powerful finishing moves with a flick of the wrist and a flash of the blade, all while countering attacks with elegant ripostes that keep enemies off balance. Harassing and thwarting your foes lets you charm fate and cheat death time and again with aplomb and plenty of flair.",
        SwashTrait,
        new EnforcedAbilityBoost(Ability.Dexterity),
        10,
        [
            Trait.Fortitude,
            Trait.Simple,
            Trait.Martial,
            Trait.Unarmed,
            Trait.LightArmor,
            Trait.UnarmoredDefense
        ],
        [
            Trait.Perception,
            Trait.Reflex,
            Trait.Will
        ],
        4,
        "{b}1. Panache.{/b} You learn how to leverage your skills to enter a state of heightened ability called panache. You gain panache when you succeed on certain skill checks with a bit of flair, including Tumble Through and other checks determined by your style. While you have panache, you gain a +5 circumstance bonus to your Speed and a +1 circumstance bonus to checks that would give you panache. It also allows you to use special attacks called finishers, which cause you to lose panache when performed.\n{i}(The automatic pathfinding will normally chart a path that doesn't require a tumble through if possible. To tumble through a creature on purpose, use the step-by-step stride option in the Other actions menu.){/i}" +
        "\n{b}2. Swashbuckler style.{/b} You choose a style that represents what kind of flair you bring to a battlefield. When you choose a style, you become trained in a skill and can use certain actions using that skill to gain panache." +
        "\n{b}3. Precise Strike.{/b} While you have panache, you deal an extra 2 precision damage with your agile or finesse melee weapons. If you use a finisher, the damage increases to 2d6 instead." +
        "\n{b}4. Confident Finisher.{/b} If you have panache, you can use an action to make a Strike against an ally in melee range. If you miss, you deal half your Precise Strike damage." +
        "\n{b}5. Swashbuckler feat.{/b}",
        [
            //Subclasses. For a swashbuckler, this is their styles.
            new SwashbucklerStyle(BattledancerStyle, 
                    "To you, a fight is a kind of performance art, and you command your foes' attention with mesmerizing movements.", 
                    "You are trained in Performance and gain the Fascinating Performance skill feat. You gain panache whenever your Performance check exceeds the Will DC of an observing foe, even if that foe isn't fascinated.",
                    "When you hit with a finisher, you can Step as a free action.",
                    Skill.Performance, [ FascinatingPerformanceActionId ])
                .WithOnSheet(sheet =>
                {
                    sheet.TrainInThisOrSubstitute(Skill.Performance);
                    sheet.GrantFeat(FascinatingPerformance.FeatName);
                })
                .WithOnCreature(swash =>
                {
                    if (swash.Level >= 9)
                    {
                        swash.AddQEffect(new QEffect("Exemplary Finisher", "When you hit with a finisher, you can Step as a free action.")
                        {
                            AfterYouTakeAction = async (qf, action) =>
                            {
                                if (action.HasTrait(Finisher) && (action.CheckResult >= CheckResult.Success))
                                {
                                    await qf.Owner.StepAsync("Choose a location to Step to.", false, true);
                                }
                            }
                        });
                    }
                }),
            new SwashbucklerStyle(BraggartStyle, 
                    "You boast, taunt, and psychologically needle your foes.", 
                    "You become trained in Intimidation. You gain panache whenever you successfully Demoralize a foe.",
                    "When you hit with a finisher, you end a foe's temporary immunity to your Demoralize.",
                Skill.Intimidation, [ ActionId.Demoralize ])
                .WithOnSheet(sheet =>
                {
                    sheet.TrainInThisOrSubstitute(Skill.Intimidation);
                })
                .WithOnCreature(swash =>
                {
                    if (swash.Level >= 9)
                    {
                        swash.AddQEffect(new QEffect("Exemplary Finisher", "When you hit with a finisher, you end a foe's temporary immunity to your Demoralize.")
                        {
                            AfterYouTakeActionAgainstTarget = async (qf, action, target, result) =>
                            {
                                if (action.HasTrait(Finisher) && (action.CheckResult >= CheckResult.Success))
                                {
                                    QEffect targetEffect = target.QEffects.FirstOrDefault((QEffect q) => (q.Id == QEffectId.ImmunityToTargeting) && (q.Source == qf.Owner) && ((ActionId)q.Tag == ActionId.Demoralize));
                                    if (targetEffect != default)
                                    {
                                        targetEffect.ExpiresAt = ExpirationCondition.Immediately;
                                    }
                                }
                            }
                        });
                    }
                }),
            new SwashbucklerStyle(FencerStyle, 
                    "You move carefully, feinting and creating false openings to lead your foes into inopportune attacks.", 
                    "You become trained in Deception. You gain panache whenever you successfully Feint or Create a Diversion.",
                    "When you hit with a finisher, the target is {r}flat-footed{/r} until your next turn.",
                    Skill.Deception, [ ActionId.Feint, ActionId.CreateADiversion ])
                .WithOnSheet(sheet =>
                {
                    sheet.TrainInThisOrSubstitute(Skill.Deception);
                })
                .WithOnCreature(swash =>
                {
                    if (swash.Level >= 9)
                    {
                        swash.AddQEffect(new QEffect("Exemplary Finisher", "When you hit with a finisher, the target is {r}flat-footed{/r} until your next turn.")
                        {
                            AfterYouTakeActionAgainstTarget = async (qf, action, target, result) =>
                            {
                                if (action.HasTrait(Finisher) && (result >= CheckResult.Success))
                                {
                                    target.AddQEffect(QEffect.FlatFooted("exemplary finisher").WithExpirationAtStartOfSourcesTurn(qf.Owner, 1));
                                }
                            }
                        });
                    }
                }),
            new SwashbucklerStyle(GymnastStyle, 
                    "You reposition, maneuver, and bewilder your foes with daring feats of physical prowess.", 
                    "You become trained in Athletics. You gain panache whenever you successfully Grapple, Shove, or Trip a foe.",
                    "When you use a finisher, if the target is grabbed, restrained, or prone, you gain a circumstance bonus to damage equal to the weapon's number of damage dice.",
                    Skill.Athletics, [ ActionId.Grapple, ActionId.Shove, ActionId.Trip ])
                .WithOnSheet(sheet =>
                {
                    sheet.TrainInThisOrSubstitute(Skill.Athletics);
                })
                .WithOnCreature(swash =>
                {
                    if (swash.Level >= 9)
                    {
                        swash.AddQEffect(new QEffect("Exemplary Finisher", "Your finishers deal bonus damage to creatures that are grabbed, restrained, or prone.")
                        {
                            BonusToDamage = (qf, action, defender) =>
                            {
                                if (action.HasTrait(Finisher))
                                {
                                    if (defender.HasEffect(QEffectId.Grabbed) || defender.HasEffect(QEffectId.Restrained) || defender.HasEffect(QEffectId.Prone))
                                    {
                                        int bonus = action.Item.WeaponProperties.DamageDieCount;
                                        return new Bonus(bonus, BonusType.Circumstance, "exemplary finisher");
                                    }
                                }
                                return null;
                            }
                        });
                    }
                }),
            new SwashbucklerStyle(WitStyle, 
                    "You are friendly, clever, and full of humor, knowing just what to say in any situation. Your witticisms leave your foes unprepared for the skill and speed of your attacks.", 
                    "You become trained in Diplomacy, and you gain the Bon Mot skill feat. You gain panache whenever you successfully use Bon Mot on a foe.",
                    "When you hit with a finisher, the target takes a -2 circumstance penalty to attack rolls against you until the start of your next turn.",
                    Skill.Diplomacy, [ ActionId.BonMot ])
                .WithOnSheet(sheet =>
                {
                    sheet.TrainInThisOrSubstitute(Skill.Diplomacy);
                    sheet.GrantFeat(FeatName.BonMot);
                })
                .WithOnCreature(swash =>
                {
                    if (swash.Level >= 9)
                    {
                        swash.AddQEffect(new QEffect("Exemplary Finisher", "When you hit with a finisher, the target takes a -2 circumstance penalty to attack rolls against you until the start of your next turn.")
                        {
                            AfterYouTakeActionAgainstTarget = async (qf, action, target, result) =>
                            {
                                if (action.HasTrait(Finisher) && (result >= CheckResult.Success))
                                {
                                    target.AddQEffect(new QEffect()
                                    {
                                        BonusToAttackRolls = (fct, help, target2) =>
                                        {
                                            if (help.HasTrait(Trait.Attack) && (target == qf.Owner))
                                            {
                                                return new Bonus(-2, BonusType.Circumstance, "Exemplary Finisher");
                                            }
                                            return null;
                                        }
                                    }.WithExpirationAtStartOfSourcesTurn(qf.Owner, 1));
                                }
                            }
                        });
                    }
                })
        ])
        .WithEffectiveClassFeatures(features =>
        {
            features.AddFeature(3, "opportune riposte", "counterattack if an enemy critically fails to hit you");
            features.AddFeature(3, "vivacious speed", "the status bonus to Speed from panache increases to 10 feet and you gain half of it even if you don't have panache");
            features.AddFeature(3, WellKnownClassFeature.ExpertInFortitude);
            features.AddFeature(5, "weapon expertise", "your proficiency with simple weapons, martial weapons, and unarmed strikes increases to expert. You gain access to the {tooltip:criteffect}critical specialization effects{/} of all weapons and unarmed attacks for which you have expert proficiency.");
            features.AddFeature(5, "precise strike 3d6");
            features.AddFeature(7, WellKnownClassFeature.Evasion);
            features.AddFeature(7, "vivacious speed +15 feet");
            features.AddFeature(7, WellKnownClassFeature.WeaponSpecialization);
            features.AddFeature(9, "exemplary finisher", "you gain a special effect when you perform finishers based on your swashbuckler style");
            features.AddFeature(9, "precise strike 4d6");
            features.AddFeature(9, WellKnownClassFeature.ExpertInClassDC);
            features.AddFeature(11, "vivacious speed +20 feet");
            features.AddFeature(11, WellKnownClassFeature.MasterInPerception);
            features.AddFeature(13, "weapon mastery", "your proficiency with simple and martial weapons and unarmed attacks increases to master");
            features.AddFeature(13, WellKnownClassFeature.ImprovedEvasion);
            features.AddFeature(13, "precise strike 5d6");
            features.AddFeature(13, WellKnownClassFeature.ExpertInUnarmoredDefenseAndLightArmor);
            features.AddFeature(15, "keen flair", "all of your Strikes score a critical hit on a roll of 19 if you would normally succeed and are a master with the weapon");
            features.AddFeature(15, "vivacious speed +25 feet");
            features.AddFeature(15, WellKnownClassFeature.GreaterWeaponSpecialization);
            features.AddFeature(17, WellKnownClassFeature.Resolve);
            features.AddFeature(17, "precise strike 6d6");
            features.AddFeature(19, "vivacious speed +30 feet");
            features.AddFeature(19, "eternal confidence", "the failure condition from Confident Finisher applies to all of your Strikes made as part of finishers or Opportune Riposte");
            features.AddFeature(19, WellKnownClassFeature.MasterInUnarmoredDefenseAndLightArmor);
            features.AddFeature(19, WellKnownClassFeature.MasterInClassDC);
        })
        .WithOnSheet(sheet =>
        {
            sheet.TrainInThisOrSubstitute(Skill.Acrobatics);
            sheet.AddFeat(Confident!, null);
            sheet.AddFeat(PreciseStrike!, null);
            sheet.AddClassFeatOption("Swash1", SwashTrait, 1);
            sheet.AddAtLevel(3, values =>
            {
                values.AddFeat(VivaciousSpeed!, null);
                values.AddFeat(OpportuneRiposte!, null);
            });
            sheet.IncreaseProficiency(5, Trait.Unarmed, Proficiency.Expert);
            sheet.IncreaseProficiency(5, Trait.Simple, Proficiency.Expert);
            sheet.IncreaseProficiency(5, Trait.Martial, Proficiency.Expert);
            sheet.IncreaseProficiency(13, Trait.Unarmed, Proficiency.Master);
            sheet.IncreaseProficiency(13, Trait.Simple, Proficiency.Master);
            sheet.IncreaseProficiency(13, Trait.Martial, Proficiency.Master);
        })
        .WithOnCreature(creature =>
        {
            creature.AddQEffect(PanacheGranter());

            if (creature.Level >= 5)
            {
                creature.AddQEffect(new QEffect("Weapon Expertise", "You gain the critical specialization effect of weapons with which you have expert proficiency.")
                {
                    YouHaveCriticalSpecialization = (effect, item, _, _) => effect.Owner.Proficiencies.Get(item.Traits) >= Proficiency.Expert
                });
            }

            if (creature.Level >= 15)
            {
                creature.AddQEffect(new QEffect("Keen Flair", "You score a critical hit on a roll of 19 when you roll a success on a Strike with weapons you have master proficiency with.")
                {
                    AdjustStrikeAction = (qf, action) =>
                    {
                        if (action.HasTrait(Trait.Strike) && ((Proficiency)qf.Owner.GetProficiency(action.Item!) >= Proficiency.Master) && !action.HasTrait(Trait.Keen))
                        {
                            action.Traits.Add(Trait.Keen);
                        }
                    }
                });
            }

            if (creature.Level >= 19)
            {
                creature.AddQEffect(new QEffect("Eternal Confidence", "All of your finishers and opportune ripostes deal " + GetPreciseStrikeDamage(creature) + (creature.HasEffect(PreciseFinisherQEffectId) ? "" : "/2") + "d6 damage on a failure.")
                {
                    AdjustStrikeAction = (qf, action) =>
                    {
                        if (action.HasTrait(Finisher) || action.HasTrait(OpportuneRiposteTrait))
                        {
                            //TODO: Make an exception for Confident Finisher. It'd be dumb to apply the effect twice.
                            action.StrikeModifiers.OnEachTarget += (async (caster, target, result) =>
                            {
                                if (result == CheckResult.Failure)
                                {
                                    int preciseDamage = GetPreciseStrikeDamage(caster);
                                    HalfDiceFormula halfdamage = new HalfDiceFormula(DiceFormula.FromText(preciseDamage.ToString() + "d6", "Precise Strike"), "eternal confidence");
                                    DiceFormula fulldamage = DiceFormula.FromText(preciseDamage.ToString() + "d6", "eternal confidence");
                                    await CommonSpellEffects.DealDirectDamage(action, caster.HasEffect(PreciseFinisherQEffectId) ? fulldamage : halfdamage, target, result, action.StrikeModifiers.CalculatedItem.WeaponProperties.DamageKind);
                                }
                            });
                        }
                    }
                });
            }
        });

    public static readonly Feat OpportuneRiposte = new Feat(ModManager.RegisterFeatName("Opportune Riposte", "Opportune Riposte {icon:Reaction}"), 
            "You take advantage of an opening from your foe's fumbled attack.", "When an enemy critically fails its Strike against you, you can use your reaction to make a melee Strike against that enemy or make a Disarm attempt.", new List<Trait>(), null)
        .WithPermanentQEffect("When an enemy critically fails a Strike against you, you may Strike or Disarm it using a reaction.", qf =>
        {
            qf.AfterYouAreTargetedReaction = (qf2, action, result) =>
            {
                return null;
            };
            qf.AfterYouAreTargeted = async (qf, action) =>
            {
                bool IsStrikeOk(CombatAction strike)
                {
                    if ((bool)strike.CanBeginToUse(qf.Owner) && strike.Target is CreatureTarget creatureTarget)
                    {
                        return creatureTarget.IsLegalTarget(qf.Owner, action.Owner);
                    }
                    return false;
                }

                CombatAction CreateOpportuneRiposteFromWeapon(Item weapon)
                {
                    CombatAction combatAction2 = qf.Owner.CreateStrike(weapon, -1)
                        .WithActionCost(0)
                        .WithExtraTrait(OpportuneRiposteTrait)
                        .WithExtraTrait(Trait.ReactiveAttack);
                    return combatAction2;
                }

                CombatAction CreateOpportuneDisarmFromWeapon(Item weapon)
                {
                    CombatAction combatAction2 = CombatManeuverPossibilities.CreateDisarmAction(qf.Owner, weapon)
                        .WithActionCost(0)
                        .WithExtraTrait(OpportuneRiposteTrait)
                        .WithExtraTrait(Trait.ReactiveAttack);
                    return combatAction2;
                }

                List<CombatAction> possibleDisarms = qf.Owner.MeleeWeapons.Select(CreateOpportuneDisarmFromWeapon).Where(IsStrikeOk).ToList();
                List<CombatAction> possibleStrikes = qf.Owner.MeleeWeapons.Select(CreateOpportuneRiposteFromWeapon).Where(IsStrikeOk).ToList();

                if (action.HasTrait(Trait.Strike) && (action.CheckResult == CheckResult.CriticalFailure))
                {
                    if (possibleDisarms.Any() && possibleStrikes.Any())
                    {
                        switch(await qf.Owner.Battle.AskToUseReaction(qf.Owner, action.Owner.Name + " critically failed a Strike against you. Spend your {icon:Reaction}reaction to respond using Opportune Riposte?", new ModdedIllustration("PhoenixAssets/panache.png"), [ OpportuneRiposteTrait ], "Disarm", "Strike"))
                        {
                            case 0:
                                CombatAction disarm = possibleDisarms[0];
                                disarm.ChosenTargets = ChosenTargets.CreateSingleTarget(action.Owner);
                                //Item heldWeapon = action.Owner.HeldItems.FirstOrDefault((Item i) => i != action.Item);
                                //if (heldWeapon != default) action.Owner.HeldItems.Remove(heldWeapon);
                                await disarm.AllExecute();
                                //if (heldWeapon != default) action.Owner.AddHeldItem(heldWeapon);
                                break;
                            case 1:
                                CombatAction strike = possibleStrikes[0];
                                strike.ChosenTargets = ChosenTargets.CreateSingleTarget(action.Owner);
                                await strike.AllExecute();
                                break;
                        }
                    }
                    else await CommonCombatActions.OfferAndMakeReactiveStrike(qf.Owner, action.Owner, action.Owner + " critically failed a Strike against you. Spend your {icon:Reaction} reaction to make a Strike using Opportune Riposte?", "opportune riposte", 1, [ Trait.ReactiveAttack, OpportuneRiposteTrait ]);
                }
            };
        });

    public static readonly Feat Confident = new Feat(ModManager.RegisterFeatName("Confident Finisher", "Confident Finisher{icon:Action}"), 
            "You make an incredibly graceful attack, piercing your foe's defenses.", "Make a Strike with a weapon that would qualify for Precise Strike. You deal half your Precise Strike damage to the target on a failure.", [Finisher], null)
        .WithPermanentQEffect(null, (qf) =>
        {
            qf.ProvideStrikeModifier = (item) =>
            {
                StrikeModifiers conf = new StrikeModifiers();
                bool flag = !item.HasTrait(Trait.Ranged) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse));
                bool flag2 = qf.Owner.HasEffect(PanacheId);
                if (flag && flag2)
                {
                    return CreateConfidentFinisher(qf.Owner, item, false, conf);
                }
                return null;
            };
        });

    public static readonly Feat PreciseStrike = new Feat(ModManager.RegisterFeatName("PreciseStrike", "Precise Strike"), 
            "You strike with flair.", "When you have panache and make a Strike with a melee agile or finesse weapon or an agile or finesse unarmed strike, you deal 2 extra damage. This damage is 2d6 instead if the Strike was part of a finisher.\nThis additional damage increases by 1 (1d6 for finishers) at 5th, 9th, 13th, and 17th levels.", new List<Trait>(), null)
        .WithOnCreature(creature =>
        {
            creature.AddQEffect(PreciseStrikeEffect(((creature.Level - 1) / 4) + 2));
        });

    public static QEffect PanacheGranter()
    {
        QEffect preciseStrike = new QEffect("Panache", "You gain panache when you use the following moves: Tumble Through")
        {
            Key = "PanacheGranter",
            Tag = new List<ActionId>() { ActionId.TumbleThrough },
            AfterYouTakeActionAgainstTarget = async (qf, action, target, result) =>
            {
                SwashbucklerStyle style = (SwashbucklerStyle)qf.Owner.PersistentCharacterSheet.Calculated.AllFeats.Find(feat => feat.HasTrait(SwashStyle));
                var list = (List<ActionId>)qf.Tag;
                bool flag = (result >= CheckResult.Success);
                bool flag2 = list.Contains(action.ActionId);
                bool flag3 = !qf.Owner.HasEffect(PanacheId);
                {
                    if (flag && flag2 && flag3)
                    {
                        qf.Owner.AddQEffect(CreatePanache(style.Skill));
                    }
                }
            }
        };
        return preciseStrike;
    }
  
    public static readonly Feat VivaciousSpeed = new Feat(ModManager.RegisterFeatName("Vivacious Speed"), 
            "When you've made an impression, you move even faster than normal, darting about the battlefield with incredible speed.", "The status bonus to your Speed from panache increases to 10 feet. When you don't have panache, you still get half this status bonus to your Speeds, rounded down to the nearest 5-foot increment. This bonus increases by 5 feet at 7th, 11th, 15th, and 19th level.", [], null)
        .WithPermanentQEffect("You move quickly, even when you don't have panache.", qf =>
        {
            qf.BonusToAllSpeeds = qfSpeed => new Bonus((((qfSpeed.Owner.Level - 3) / 4) + 2)/2, BonusType.Status, "Vivacious Speed");
            qf.YouAcquireQEffect = (qfThis, qfGet) =>
            {
                if (qfGet.Id == PanacheId)
                {
                    QEffect qfNew = qfGet;
                    int i = ((qf.Owner.Level - 3) / 4) + 2;
                    qfNew.BonusToAllSpeeds = qf => new Bonus(i, BonusType.Status, "Panache");
                    qfNew.Description = qfNew.Description!.Replace("+5-foot", $"+{(i*5)}-foot");
                    return qfNew;
                }
                else return qfGet;
            };
        });
    
    public static Feat FascinatingPerformance = new TrueFeat(ModManager.RegisterFeatName("FascinatingPerformance", "Fascinating Performance"), 1, 
            "You can Perform to fascinate observers.", "As an action, make a Performance check against an opponent's Will DC. If you critically succeed, the target is fascinated by you (they have a -2 status penalty to skill checks and can't take concentrate actions against anyone other than you) for 1 round. The target is then immune for the rest of the encounter.\n\nIf you are an expert in Performance, you can choose up to 4 targets. If you are a master in Performance, you can choose up to 10 targets.", [ Trait.General, Trait.Skill ])
        .WithActionCost(1)
        .WithPrerequisite(sheet => sheet.GetProficiency(Trait.Performance) >= Proficiency.Trained, "You must be trained in Performance.")
        .WithPermanentQEffect(null, (qf) =>
        {
            qf.ProvideActionIntoPossibilitySection = (effect, section) =>
            {
                if (section.PossibilitySectionId == PossibilitySectionId.SkillActions)
                {
                    return new ActionPossibility(new CombatAction(effect.Owner, IllustrationName.RoaringApplause, "Fascinating Performance", [ Trait.Concentrate, Trait.Manipulate, Trait.Incapacitation ],
                        "Make a Performance check (" + S.SkillBonus(qf.Owner, Skill.Performance) + ") against " + 
                            (effect.Owner.PersistentCharacterSheet.Calculated.GetProficiency(Trait.Performance) >= Proficiency.Master ? "up to 10" 
                            : (effect.Owner.PersistentCharacterSheet.Calculated.GetProficiency(Trait.Performance) == Proficiency.Expert) ? "up to 4"
                            : "an") + " opponent's Will DC. If you " + (!qf.Owner.HasFeat(FocusedFascination.FeatName) ? "critically " : "") + "succeed, the target is fascinated by you (they have a -2 status penalty to skill checks and can't take concentrate actions against anyone other than you) for 1 round. The target is then immune for the rest of the encounter.",
                        (effect.Owner.PersistentCharacterSheet.Calculated.GetProficiency(Trait.Performance) >= Proficiency.Master) ? Target.MultipleCreatureTargets(Target.Ranged(50), Target.Ranged(50), Target.Ranged(50), Target.Ranged(50), Target.Ranged(50), Target.Ranged(50), Target.Ranged(50), Target.Ranged(50), Target.Ranged(50), Target.Ranged(50)).WithMinimumTargets(1).WithMustBeDistinct()
                                : (effect.Owner.PersistentCharacterSheet.Calculated.GetProficiency(Trait.Performance) == Proficiency.Expert) ? Target.MultipleCreatureTargets(Target.Ranged(50), Target.Ranged(50), Target.Ranged(50), Target.Ranged(50)).WithMinimumTargets(1).WithMustBeDistinct()
                                : Target.Ranged(50))  
                            .WithActiveRollSpecification(new ActiveRollSpecification(TaggedChecks.SkillCheck(Skill.Performance), Checks.DefenseDC(Defense.Will)))
                            .WithActionCost(1)
                            .WithActionId(FascinatingPerformanceActionId)
                            .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                            {
                                if (result == CheckResult.CriticalSuccess)
                                {
                                    target.AddQEffect(CreateFascinated(caster).WithExpirationOneRoundOrRestOfTheEncounter(caster, false));
                                }
                                target.AddQEffect(QEffect.ImmunityToTargeting(spell.ActionId, caster));
                            }));
                }
                else return null;
            };
        });

    //This one's a test, originally to learn how to add stuff and later to expedite testing with Panache. I think I'll leave it in just in case someone wants to poke around in the mod.
    public static Feat AddPanache = new TrueFeat(FeatName.CustomFeat, 1, "You give yourself panache as a test.", "Test to see if the feat and condition load.", new Trait[] { SwashTrait }, null)
        .WithOnCreature((sheet, creature) =>
        {
            QEffect Panacheer = new QEffect()
            {
                ProvideMainAction = (qftechnical =>
                {
                    return new ActionPossibility(new CombatAction(creature, IllustrationName.ExclamationMark, "Give Panache", new Trait[1] { Trait.Concentrate }, "You give yourself panache.", Target.Self())
                        .WithActionCost(1)
                        .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                        {
                            SwashbucklerStyle style = (SwashbucklerStyle)caster.PersistentCharacterSheet.Calculated.AllFeats.Find(feat => feat.HasTrait(SwashStyle));
                            target.AddQEffect(CreatePanache(style.Skill));
                        }));
                })
            };
            creature.AddQEffect(Panacheer);
        })
        .WithCustomName("Give Panache");

    public static Feat DisarmingFlair = new TrueFeat(ModManager.RegisterFeatName("Disarming Flair", "Disarming Flair"), 1, 
            "It's harder for foes to regain their grip when you knock their weapon partially out of their hands.", "When you succeed at an Athletics check to Disarm, the circumstance bonus and penalty from Disarm last until the end of your next turn, instead of until the beginning of the target's next turn. The target can use an Interact action to adjust their grip and remove this effect. If your swashbuckler style is gymnast and you succeed at your Athletics check to Disarm a foe, you gain panache.", [ SwashTrait ])
        .WithPermanentQEffect("Your Disarm effects last longer.", (qf) =>
        {
            qf.CharacterSheetBecomesCreature = (sheet, creature) =>
            {
                if (creature.HasFeat(GymnastStyle))
                {
                    QEffect panacheGranter = creature.QEffects.First((QEffect fct) => fct.Key == "PanacheGranter");
                    List<ActionId> list = (List<ActionId>)panacheGranter.Tag;
                    list.Add(ActionId.Disarm);
                    panacheGranter.Description += ", Disarm";
                }
            };
            qf.AfterYouTakeAction = async (effect, disarm) =>
            {
                if (disarm.ActionId == ActionId.Disarm && disarm.CheckResult == CheckResult.Success)
                {
                    QEffect disarmed = disarm.ChosenTargets.ChosenCreature!.QEffects.Single((thing) => thing.Name == "Weakened grasp");
                    disarmed.ExpiresAt = ExpirationCondition.ExpiresAtEndOfSourcesTurn;
                    disarmed.CannotExpireThisTurn = true;
                    disarmed.ProvideMainAction = (qftechnical =>
                    {
                        return new ActionPossibility(new CombatAction(disarmed.Owner, IllustrationName.Fist, "Recover Grip", [ Trait.Interact, Trait.Manipulate ], "You adjust your grip on your weapon and remove the penalty from Disarm.", Target.Self())
                            .WithGoodness((tg, self, foe) => self.AI.AlwaysIfSmartAndTakingCareOfSelf)
                            .WithEffectOnEachTarget(async (caster, spell, target, result) =>
                            {
                                disarmed.Owner.RemoveAllQEffects((QEffect thing) => thing.Name == "Weakened grasp");
                            }));
                    });
                }
            };
        });
    
    public static Feat DuelingParrySwashbuckler = CommonFeatTemplates.CreateDuplicateFeatForDifferentClass(FeatName.DuelingParry, ModManager.RegisterFeatName(FeatName.DuelingParry.ToString() + "Swash", "Dueling Parry"), 1, SwashTrait);

    // Grants thrown versions of Confident Finisher, Basic Finisher, Unbalancing Finisher, Bleeding Finisher, and Stunning Finisher, as long as your weapons meet the criteria. It's usually down to GM judgement which finishers Flying Blade applies to, so I choose to allow it for any finisher without a complicated targeting scheme.
    public static Feat FlyingBlade = new TrueFeat(ModManager.RegisterFeatName("FlyingBlade", "Flying Blade"), 1, 
            "You've learned to apply your flashy techniques to thrown weapons just as easily as melee.", "When you have panache, you apply your additional damage from Precise Strike on ranged Strikes you make with a thrown weapon within its first range increment. The thrown weapon must be an agile or finesse weapon.\n\nAdditionally, if you have the following finishers available to you, you can perform them with thrown weapons (within the weapon's first range increment): Confident Finisher, Basic Finisher, {link:Unbalancing Finisher}Unbalancing Finisher{/}, {link:BleedingFinisher}Bleeding Finisher{/}, {link:StunningFinisher}Stunning Finisher{/}, {link:PerfectFinisher}Perfect Finisher{/}.", [ SwashTrait ])
        .WithPrerequisite(sheet => sheet.HasFeat(PreciseStrike.FeatName), "You must have the Precise Strike feature.")
        .WithPermanentQEffect("You can use Precise Strike and finishers within the first range increment of thrown weapons.", qf =>
        {
            if (qf.Owner.HasFeat(Confident.FeatName))
            {
                qf.Owner.AddQEffect(new QEffect
                {
                    ProvideStrikeModifier = (item) =>
                    {
                        StrikeModifiers strikeModifiers8 = new StrikeModifiers();
                        bool flag23 = (item.HasTrait(Trait.Thrown10Feet) || item.HasTrait(Trait.Thrown20Feet)) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse));
                        bool flag24 = qf.Owner.HasEffect(PanacheId);
                        if (flag23 && flag24)
                        {
                            CombatAction thrown = CreateConfidentFinisher(qf.Owner, item, true, strikeModifiers8);
                            thrown.Name += " (Thrown)";
                            thrown.Target = Target.Ranged(item.WeaponProperties!.RangeIncrement);
                            return thrown;
                        }
                        return null;
                    }
                });
            }

            if (qf.Owner.HasFeat(AddMulticlassSwash.FinishingPrecision.FeatName))
            {
                qf.Owner.AddQEffect(new QEffect
                {
                    ProvideStrikeModifier = (item) =>
                    {
                        StrikeModifiers basic = new StrikeModifiers();
                        bool isValidWeapon = (item.HasTrait(Trait.Thrown10Feet) || item.HasTrait(Trait.Thrown20Feet)) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse));
                        bool hasPanache = qf.Owner.HasEffect(PanacheId);
                        if (isValidWeapon && hasPanache)
                        {
                            CombatAction basicThrown = AddMulticlassSwash.CreateBasicFinisher(qf.Owner, item, true, basic);
                            basicThrown.Name += " (Thrown)";
                            basicThrown.Target = Target.Ranged(item.WeaponProperties!.RangeIncrement);
                            return basicThrown;
                        }
                        else return null;
                    }
                });
            }

            if (qf.Owner.HasFeat(UnbalancingFinisher.FeatName))
            {
                qf.Owner.AddQEffect(new QEffect
                {
                    ProvideStrikeModifier = (item) =>
                    {
                        StrikeModifiers strikeModifiers7 = new StrikeModifiers();
                        bool isValidWeapon = (item.HasTrait(Trait.Thrown10Feet) || item.HasTrait(Trait.Thrown20Feet)) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse));
                        bool hasPanache = qf.Owner.HasEffect(PanacheId);
                        if (isValidWeapon && hasPanache)
                        {
                            CombatAction unbalThrown = CreateUnbalancingFinisher(qf.Owner, item, true, strikeModifiers7);
                            unbalThrown.Name += " (Thrown)";
                            unbalThrown.Target = Target.Ranged(item.WeaponProperties!.RangeIncrement);
                            return unbalThrown;
                        }

                        return null;
                    }
                });
            }

            if (qf.Owner.HasFeat(BleedingFinisher.FeatName))
            {
                qf.Owner.AddQEffect(new QEffect
                {
                    ProvideStrikeModifier = (item) =>
                    {
                        StrikeModifiers strikeModifiers6 = new StrikeModifiers();
                        bool isValidWeapon = (item.HasTrait(Trait.Thrown10Feet) || item.HasTrait(Trait.Thrown20Feet)) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse));
                        bool hasPanache = qf.Owner.HasEffect(PanacheId);
                        bool isValidDamageKind = item.WeaponProperties!.DamageKind == DamageKind.Piercing || item.WeaponProperties.DamageKind == DamageKind.Slashing;
                        if (isValidWeapon && hasPanache && isValidDamageKind)
                        {
                            CombatAction bleedThrown = CreateBleedingFinisher(qf.Owner, item, true, strikeModifiers6);
                            bleedThrown.Name += " (Thrown)";
                            bleedThrown.Target = Target.Ranged(item.WeaponProperties.RangeIncrement);
                            return bleedThrown;
                        }

                        return null;
                    }
                });
            }

            if (qf.Owner.HasFeat(StunningFinisher.FeatName))
            {
                qf.Owner.AddQEffect(new QEffect
                {
                    ProvideStrikeModifier = (item) =>
                    {
                        StrikeModifiers strikeModifiers5 = new StrikeModifiers();
                        bool isValidWeapon = (item.HasTrait(Trait.Thrown10Feet) || item.HasTrait(Trait.Thrown20Feet)) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse));
                        bool hasPanache = qf.Owner.HasEffect(PanacheId);
                        if (isValidWeapon && hasPanache)
                        {
                            CombatAction stunThrown = CreateStunningFinisher(qf.Owner, item, true, strikeModifiers5);
                            stunThrown.Name += " (Thrown)";
                            stunThrown.Target = Target.Ranged(item.WeaponProperties!.RangeIncrement);
                            return stunThrown;
                        }
                        return null;
                    }
                });
            }
            
            if (qf.Owner.HasFeat(TargetingFinisher.FeatName))
            {
                qf.Owner.AddQEffect(new QEffect
                {
                    ProvideStrikeModifierAsPossibilities = (qfThis, item) =>
                    {
                        bool isThrown = (item.HasTrait(Trait.Thrown10Feet) || item.HasTrait(Trait.Thrown20Feet)) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse));
                        bool hasPanache = qf.Owner.HasEffect(PanacheId);
                        var actions = new List<ActionPossibility>();
                        if (isThrown && hasPanache)
                        {
                            actions.AddRange(
                                new ActionPossibility(CreateTargetingFinisherHead(qfThis.Owner, item, false, new StrikeModifiers())),
                                new ActionPossibility(CreateTargetingFinisherArm(qfThis.Owner, item, false, new StrikeModifiers())),
                                new ActionPossibility(CreateTargetingFinisherLeg(qfThis.Owner, item, false, new StrikeModifiers()))
                            );
                            foreach (ActionPossibility a in actions)
                            {
                                a.CombatAction.Name += " (Thrown)";
                                a.CombatAction.Target = Target.Ranged(item.WeaponProperties!.RangeIncrement);
                            }
                        }
                        return actions;
                    }
                });
            }
            
            if (qf.Owner.HasFeat(PerfectFinisher.FeatName))
            {
                qf.Owner.AddQEffect(new QEffect
                {
                    ProvideStrikeModifier = (item) =>
                    {
                        bool isThrown = (item.HasTrait(Trait.Thrown10Feet) || item.HasTrait(Trait.Thrown20Feet)) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse));
                        bool hasPanache = qf.Owner.HasEffect(PanacheId);
                        if (isThrown && hasPanache)
                        {
                            var perfectThrown = CreatePerfectFinisher(qf.Owner, item, true, new StrikeModifiers());
                            perfectThrown.Name += " (Thrown)";
                            perfectThrown.Target = Target.Ranged(item.WeaponProperties!.RangeIncrement);
                            return perfectThrown;
                        }
                        return null;
                    }
                });
            }
        });

    public static void ReplaceYoureNext()
    {
        TrueFeat trueFeat = AllFeats.GetFeatByFeatName(FeatName.YoureNext) as TrueFeat;
        trueFeat!.WithAllowsForAdditionalClassTrait(SwashTrait);
    }

    public static void ReplaceNimbleDodge()
    {
        TrueFeat trueFeat = AllFeats.GetFeatByFeatName(FeatName.NimbleDodge) as TrueFeat;
        trueFeat!.WithAllowsForAdditionalClassTrait(SwashTrait);
    }

    public static Feat FocusedFascination = new TrueFeat(ModManager.RegisterFeatName("FocusedFascination", "Focused Fascination"), 1, 
            "Your performance can draw a foe's attention even in the districting din of combat.", "When you use Fascinating Performance, you only need a success, rather than a critical success, to fascinate your target. This works only if you are attempting to fascinate just one target.", [ SwashTrait ])
        .WithPrerequisite(FascinatingPerformance.FeatName, "Fascinating Performance")
        .WithPermanentQEffect(null, (qf) =>
        {
            qf.AfterYouTakeActionAgainstTarget = async (fct, action, target, result) =>
            {
                if ((action.ActionId == FascinatingPerformanceActionId) && (action.ChosenTargets.ChosenCreatures.Count == 1) && (result == CheckResult.Success))
                {
                    target.AddQEffect(CreateFascinated(fct.Owner));
                }
            };
        });

    public static Feat GoadingFeint = new TrueFeat(ModManager.RegisterFeatName("Goading Feint"), 1, 
            "When you trick a foe, you can goad them into overextending their next attack.", "On a Feint, you can use the following success and critical success effects instead of any other effects you would gain when you Feint; if you do, normal abilities that apply on a Feint no longer apply.\n\n{b}Critical Success:{/b} The target takes a -2 circumstance penalty to all its attack rolls against you before the end of its next turn.\n{b}Success:{/b} The target takes a -2 circumstance penalty to the next attack roll it makes against you before the end of its next turn.", [ SwashTrait ])
        .WithPrerequisite(values => values.GetProficiency(Trait.Deception) >= Proficiency.Trained, "You must be trained in Deception")
        .WithPermanentQEffect("When you Feint a creature, you can give them a penalty to AC instead of the normal effects.", qf =>
        {
            qf.AfterYouTakeActionAgainstTarget = async (qfaction, action, target, result) =>
            {
                bool flag = action.ActionId == ActionId.Feint;
                bool flag2 = flag;
                bool flag3 = action.CheckResult >= CheckResult.Success;
                if (flag2 && flag3)
                {
                    flag2 = await qf.Owner.Battle.AskForConfirmation(qf.Owner, IllustrationName.Action, "Would you like to goad the target?", "Goad");

                    if (flag2 && action.CheckResult >= CheckResult.Success)
                    {
                        QEffect goaded = new QEffect("Goaded", "You have a -2 circumstance penalty to your next attack roll against " + qfaction.Owner.Name + ".", ExpirationCondition.ExpiresAtEndOfYourTurn, qfaction.Owner, IllustrationName.Confused)
                        {
                            BonusToAttackRolls = (bonus, bonk, someone) => someone == qfaction.Owner ? new Bonus(-2, BonusType.Circumstance, "Goading Feint") : null,
                            AfterYouMakeAttackRoll = (goaded, result) =>
                            {
                                goaded.ExpiresAt = ExpirationCondition.Immediately;
                            },
                            CountsAsADebuff = true
                        };
                        if (result == CheckResult.Success)
                        {
                            goaded.Description = "You have a -2 circumstance penalty on all attack rolls against " + qfaction.Owner.Name + ".";
                            goaded.AfterYouMakeAttackRoll = null;
                        }
                        target.AddQEffect(goaded);
                        target.RemoveAllQEffects((thing) => (thing.Name == "Flat-footed in melee") || (thing.Name == "Flat-footed to " + action.Owner.Name));
                    }
                }
            };
        });

    public static Feat OneForAll = new TrueFeat(ModManager.RegisterFeatName("One For All", "One For All"), 1, 
            "With precisely the right words of encouragement, you bolster an ally's efforts.", "Choose an ally within 30 feet. The next time that ally makes an attack roll or skill check, you may use your reaction to attempt a DC 20 Diplomacy check with the following effects:\n{b}Critical Success:{/b} You grant the ally a +2 circumstance bonus to their attack roll or skill check. If your swashbuckler style is Wit, you gain panache.\n{b}Success:{/b} You grant the ally a +1 circumstance bonus to their attack roll or skill check. If your swashbuckler style is Wit, you gain panache.\n{b}Critical Failure:{/b} The ally takes a -1 circumstance penalty to their attack roll or skill check.", [ Trait.Auditory, Trait.Concentrate, Trait.Emotion, Trait.Linguistic, Trait.Mental, SwashTrait ])
        .WithActionCost(1)
        .WithPrerequisite((values) => values.GetProficiency(Trait.Diplomacy) >= Proficiency.Trained, "You must be trained in Diplomacy.")
        .WithPermanentQEffect(null, (qf) =>
        {
            qf.ProvideActionIntoPossibilitySection = (qfoneforall, section) =>
            {
                if (section.PossibilitySectionId == PossibilitySectionId.SkillActions)
                {
                    bool aidPrepareIdExists = ModManager.TryParse<ActionId>("PrepareToAid", out ActionId aidPrepareId);
                    return new ActionPossibility(new CombatAction(qf.Owner, IllustrationName.SoundBurst, "One For All", [ Trait.Auditory, Trait.Concentrate, Trait.Emotion, Trait.Linguistic, Trait.Mental ], "Attempt to assist an ally's next skill check or attack roll.", Target.RangedFriend(6)
                        .WithAdditionalConditionOnTargetCreature((self, target) => (target.QEffects.Any((QEffect effect) => effect.Name == "Aided by " + qf.Owner.Name)) ? Usability.NotUsableOnThisCreature("You are already aiding this ally.") : Usability.Usable)
                        .WithAdditionalConditionOnTargetCreature((self, target) => (target == self) ? Usability.NotUsableOnThisCreature("You can't Aid yourself.") : Usability.Usable))
                    .WithActionCost(1)
                    .WithActionId(aidPrepareId)
                    .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                    {
                        QEffect aided = new QEffect("Aided by " + qf.Owner.Name, qf.Owner.Name + " may attempt to assist you and provide a bonus to your next attack roll or skill check.", ExpirationCondition.Never, caster, IllustrationName.Guidance)
                        {
                            BeforeYourActiveRoll = async delegate (QEffect effect, CombatAction action, Creature target2)
                            {
                                bool aidIdExists = ModManager.TryParse<ActionId>("AidReaction", out ActionId aidId);
                                CombatAction aid = new CombatAction(caster, IllustrationName.SoundBurst, "Aid", [], "Attempt to Aid an ally.", Target.Self())
                                    .WithActionCost(0)
                                    .WithActionId(aidId)
                                    .WithActiveRollSpecification(new ActiveRollSpecification(TaggedChecks.SkillCheck(Skill.Diplomacy), Checks.FlatDC(20)))
                                    .WithEffectOnSelf(async (spell2, self) =>
                                    {
                                        if (self.HasFeat(WitStyle) && spell2.CheckResult >= CheckResult.Success)
                                        {
                                            self.AddQEffect(CreatePanache(Skill.Diplomacy));
                                        }
                                    })
                                    .WithEffectOnEachTarget(async (spell2, caster2, target3, result2) =>
                                    {
                                        switch (result2)
                                        {
                                            case CheckResult.CriticalSuccess:
                                                target3.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                                                {
                                                    BonusToAllChecksAndDCs = (thing) => new Bonus(2, BonusType.Circumstance, "One For All")
                                                });
                                                break;
                                            case CheckResult.Success:
                                                target3.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                                                {
                                                    BonusToAllChecksAndDCs = (thing) => new Bonus(1, BonusType.Circumstance, "One For All")
                                                });
                                                break;
                                            case CheckResult.CriticalFailure:
                                                target3.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                                                {
                                                    BonusToAllChecksAndDCs = (thing) => new Bonus(-1, BonusType.Circumstance, "One For All")
                                                });
                                                break;
                                        }
                                        effect.ExpiresAt = ExpirationCondition.Immediately;
                                    });
                                aid.ChosenTargets = ChosenTargets.CreateSingleTarget(target);
                                if (await qf.Owner.Battle.AskToUseReaction(qf.Owner, "Spend your {icon:Reaction} reaction to attempt a Diplomacy check to Aid " + target.Name + "'s check?"))
                                {
                                    await aid.AllExecute();
                                }
                            }
                        };
                        target.AddQEffect(aided);
                    }));
                }
                else return null;
            };
        });

    public static Feat StylishEntrance = new TrueFeat(ModManager.RegisterFeatName("StylishEntrance", "Stylish Entrance"), 1, 
            "You bring your flair into the very act of readying yourself for combat.", "You can use the skill associated with your swashbuckler's style for initiative rolls instead of Perception.", [ SwashTrait, Trait.Homebrew ])
        .WithPermanentQEffect("You roll your swashbuckler style's skill for initiative.", (qf) =>
        {
            SwashbucklerStyle style = (SwashbucklerStyle)qf.Owner.PersistentCharacterSheet.Calculated.AllFeats.Find(feat => feat.HasTrait(SwashStyle));
            if (style != null)
            {
                Skill skill = style.Skill;
                qf.Description = "You roll " + skill.HumanizeTitleCase2() + " for initiative.";
                qf.OfferAlternateSkillForInitiative = qf2 => skill;
            }
        });

    public static Feat AfterYou = new TrueFeat(ModManager.RegisterFeatName("After You"), 2, 
            "You allow your foes to make the first move in a show of incredible confidence.", "When a battle begins, instead of rolling initiative, you may voluntarily go last. When you do so, you gain panache.", [ SwashTrait ])
        .WithActionCost(0)
        .WithPermanentQEffect("You can let your enemies go first to gain panache.", (qf) =>
        {
            qf.StartOfCombatReaction = qfAfterYou =>
            {
                return ReactionOption.CreateCustom("After You", "Move last in initiative and gain panache.", new ModdedIllustration("PhoenixAssets/panache.PNG"), qfAfterYou.Owner,
                        async () =>
                        {
                            Creature target = qfAfterYou.Owner.Battle.InitiativeOrder.Last(); 
                            int goal = target.Battle.InitiativeOrder.IndexOf(target);
                            qfAfterYou.Owner.Battle.MoveInInitiativeOrder(qfAfterYou.Owner, goal + 1);
                            SwashbucklerStyle style = (SwashbucklerStyle)qfAfterYou.Owner.PersistentCharacterSheet.Calculated.AllFeats.Find(feat => feat.HasTrait(SwashStyle));
                            qfAfterYou.Owner.AddQEffect(CreatePanache(style.Skill));
                        })
                    .WithIsFreeAction();
            };
            /*
            qf.StartOfCombat = async (qfAfterYou) =>
            {
                if (await qf.Owner.Battle.AskForConfirmation(qf.Owner, new ModdedIllustration("PhoenixAssets/panache.PNG"), "Move last in initiative and gain panache?", "Yes, move last", "No, roll initiative normally"))
                {
                    Creature target = qf.Owner.Battle.InitiativeOrder.Last();
                    int goal = target.Battle.InitiativeOrder.IndexOf(target);
                    qf.Owner.Battle.MoveInInitiativeOrder(qf.Owner, goal + 1);
                    SwashbucklerStyle style = (SwashbucklerStyle)qf.Owner.PersistentCharacterSheet.Calculated.AllFeats.Find(feat => feat.HasTrait(SwashStyle));
                    qf.Owner.AddQEffect(CreatePanache(style.Skill));
                }
            };
            */
        });

    public static Feat Antagonize = new TrueFeat(ModManager.RegisterFeatName("Antagonize"), 2, 
            "Your taunts and threats earn your foes' ire.", "When you Demoralize a foe, its frightened condition can't decrease below 1 until it takes a hostile action against you or it cannot see you.", [ SwashTrait ])
        .WithPermanentQEffect("Enemies can't recover from your Demoralize actions without taking hostile actions against you.", (qf) =>
        {
            qf.AfterYouTakeActionAgainstTarget = async (fear, demoralize, target, result) =>
            {
                if (demoralize.ActionId == ActionId.Demoralize && result >= CheckResult.Success)
                {
                    QEffect antagonized = new QEffect("Antagonized", "This target cannot lower their Frightened value below 1 until taking a hostile action against " + qf.Owner.Name + ".", ExpirationCondition.Never, demoralize.Owner, IllustrationName.Rage)
                    {
                        Id = QEffectId.DirgeOfDoomFrightenedSustainer,
                        EndOfYourTurnBeneficialEffect = async (fright, victim) =>
                        {
                            if (qf.Owner.DetectionStatus.Undetected)
                            {
                                fright.ExpiresAt = ExpirationCondition.Immediately;
                            }
                        },
                        AfterYouTakeHostileAction = async (effect, action) =>
                        {
                            if (action.ChosenTargets.GetAllTargetCreatures().Any(creature => creature == qf.Owner))
                            {
                                effect.ExpiresAt = ExpirationCondition.Immediately;
                            }
                        },
                        StateCheck = (effect) =>
                        {
                            if (!effect.Owner.HasEffect(QEffectId.Frightened))
                            {
                                effect.ExpiresAt = ExpirationCondition.Immediately;
                            }
                        }
                    };
                    target.AddQEffect(antagonized);
                }
            };
        });

    public static Feat UnbalancingFinisher = new TrueFeat(ModManager.RegisterFeatName("Unbalancing Finisher", "Unbalancing Finisher"), 2, 
            "You attack with a flashy assault that leaves your target off balance.", "Make a melee Strike. If you hit and deal damage, your target is {r}flat-footed{/r} until the end of your next turn.", [ SwashTrait, Finisher ])
        .WithActionCost(1)
        .WithPermanentQEffect(null, (qf) =>
        {
            qf.ProvideStrikeModifier = item =>
            {
                StrikeModifiers unbalancing = new StrikeModifiers();
                bool flag = item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse);
                bool flag2 = qf.Owner.HasEffect(PanacheId);
                if (flag && flag2)
                {
                    return CreateUnbalancingFinisher(qf.Owner, item, false, unbalancing);
                }
                else return null;
            };
        });

    public static Feat FinishingFollowThrough = new TrueFeat(ModManager.RegisterFeatName("Finishing Follow-Through", "Finishing Follow-Through"), 2, 
            "Finishing a foe maintains your swagger.", "You gain panache if your finisher reduces an enemy to 0 HP.", [ SwashTrait ])
        .WithPermanentQEffectAndSameRulesText(qf =>
        {
            qf.AfterYouDealDamage = async (you, action, target) =>
            {
                if (target.HP <= 0 && action.HasTrait(Finisher))
                {
                    SwashbucklerStyle style = (SwashbucklerStyle)qf.Owner.PersistentCharacterSheet.Calculated.AllFeats.Find(feat => feat.HasTrait(SwashStyle));
                    qf.Owner.AddQEffect(CreatePanache(style.Skill).WithCannotExpireThisTurn());
                }
            };
        });

    public static Feat CharmedLife = new TrueFeat(ModManager.RegisterFeatName("Charmed Life", "Charmed Life"), 2, 
            "When danger calls, you have a strange knack for coming out on top.", "Before you make a saving throw, you can spend your {icon:Reaction}reaction to gain a +2 circumstance bonus to the roll.", [ SwashTrait ])
        .WithActionCost(Constants.ACTION_COST_REACTION)
        .WithPrerequisite(sheet => sheet.FinalAbilityScores.TotalScore(Ability.Charisma) >= 14, "Charisma 14")
        .WithPermanentQEffect("You can add a +2 circumstance bonus to a saving throw using a reaction.", (qf) =>
        {
            qf.BeforeYourSavingThrow = async (charm, action, self) =>
            {
                if (await self.Battle.AskToUseReaction(self, "You are about to make a saving throw. Spend your {icon:Reaction} reaction to gain a +2 circumstance bonus?"))
                {
                    self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                    {
                        BonusToDefenses = (effect, action2, save) => new Bonus(2, BonusType.Circumstance, "Charmed Life")
                    });
                }
            };
        });
    
    public static Feat TumbleBehind = new TrueFeat(ModManager.RegisterFeatName("Tumble Behind", "Tumble Behind"), 2, 
            "Your tumbling catches enemies off-guard.", "Whenever you Tumble Through an enemy, the enemy you Tumbled through is {r}flat-footed{/r} against the next attack you make until the end of your turn.\n\n{i}(The automatic pathfinding will normally chart a path that doesn't require a tumble through if possible. To tumble through a creature on purpose, use the step-by-step stride option in the Other actions menu.){/i}", [ SwashTrait ])
        .WithPermanentQEffect("Tumbling Through enemies makes them briefly {r}flat-footed{/r}.", (qf) =>
        {
            qf.AfterYouTakeActionAgainstTarget = async (effect, action, target, result) =>
            {
                if (action.ActionId == ActionId.TumbleThrough && result >= CheckResult.Success)
                {
                    target.AddQEffect(new QEffect("Tumbled Behind", "You're flat-footed to the next attack that " + qf.Owner.Name + " makes.", ExpirationCondition.ExpiresAtEndOfSourcesTurn, effect.Owner, IllustrationName.Flatfooted)
                    {
                        IsFlatFootedTo = (fct, attacker, attack) => attacker == action.Owner ? "Tumble Behind" : null,
                        AfterYouAreTargeted = async (effect2, strike) =>
                        {
                            if (strike.HasTrait(Trait.Strike) && (strike.Owner == action.Owner))
                            {
                                effect.ExpiresAt = ExpirationCondition.Immediately;
                            }
                        },
                        CountsAsADebuff = true
                    });
                }
            };
        });

    public static Feat DazzlingDiversion = new TrueFeat(ModManager.RegisterFeatName("Dazzling Diversion", "Dazzling Diversion"), 4, 
            "You've learned techniques to temporarily blind your opponents.", "When you successfully Feint a creature, it becomes dazzled until the end of your turn. If you critically succeed, the creature becomes dazzled until the start of your next turn instead. It can use an action to Interact and remove the condition.", [ Trait.Rogue, SwashTrait ])
        .WithPrerequisite((values) => values.GetProficiency(Trait.Deception) >= Proficiency.Trained, "You must be trained in Deception")
        .WithPermanentQEffect("Foes become dazzled when you Feint them.", qf =>
        {
            qf.AfterYouTakeActionAgainstTarget = async (qf2, action, target, result) =>
            {
                if (action.ActionId == ActionId.Feint && result >= CheckResult.Success && !target.IsImmuneTo(Trait.Visual))
                {
                    QEffect dazzled = QEffect.Dazzled();
                    dazzled.Source = qf2.Owner;
                    if (result == CheckResult.CriticalSuccess)
                    {
                        dazzled.ExpiresAt = ExpirationCondition.ExpiresAtStartOfSourcesTurn;
                        dazzled.ProvideContextualAction = effect =>
                        {
                            return new ActionPossibility(new CombatAction(effect.Owner, IllustrationName.RubEyes, "Rub Eyes", [ Trait.Interact, Trait.Manipulate ], "Rub your eyes to remove the dazzled condition.",
                                Target.Self())
                                    .WithActionCost(1)
                                    .WithGoodness((foe, self, _) => self.AI.AlwaysIfSmartAndTakingCareOfSelf)
                                    .WithEffectOnSelf(async (self) =>
                                    {
                                        dazzled.ExpiresAt = ExpirationCondition.Immediately;
                                    }));
                        };
                    }
                    else
                    {
                        dazzled.ExpiresAt = ExpirationCondition.ExpiresAtEndOfSourcesTurn;
                    }
                    target.AddQEffect(dazzled);
                }
            };
        });

    public static Feat DramaticCatch = new TrueFeat(ModManager.RegisterFeatName("DramaticCatch", "Dramatic Catch"), 4, 
            "You catch your wounded ally as they fall, prompting them to stay on their feet.", "When an ally adjacent to you takes damage that would reduce them to 0 HP, if you have panache, you can use your {icon:Reaction}reaction to catch them. When you do so, you lose panache, but the triggering ally remains at 1 Hit Point, and their wounded value increases by 1.\nYou can't use this ability if you don't have a free hand, or if you've already used Dramatic Catch on the same ally before taking a long rest.", [ SwashTrait, Trait.Homebrew ])
        .WithActionCost(Constants.ACTION_COST_REACTION)
        .WithPermanentQEffect("You can save an ally about to fall.", (qf) =>
        {
            qf.StateCheck = fct =>
            {
                foreach (Creature ally2 in qf.Owner.Battle.AllCreatures.Where((friend) => friend.IsAdjacentTo(qf.Owner) && friend.FriendOfAndNotSelf(qf.Owner)))
                {
                    ally2.AddQEffect(new QEffect
                    {
                        YouAreDealtLethalDamage = async (effect, attacker, stuff, defender) =>
                        {
                            if (qf.Owner.HasEffect(PanacheId) && qf.Owner.HasFreeHand && !defender.PersistentUsedUpResources.UsedUpActions.Contains("Dramatic Catch") && defender.HP > 0)
                            {
                                if (await qf.Owner.Battle.AskToUseReaction(qf.Owner, defender.Name + " is about to take potentially lethal damage. Spend your {icon:Reaction} reaction and panache to keep them standing at 1 Hit Point?"))
                                {
                                    defender.PersistentUsedUpResources.UsedUpActions.Add("Dramatic Catch");
                                    qf.Owner.RemoveAllQEffects((QEffect fct) => fct.Id == PanacheId);
                                    int HPedit = defender.HP - 1;
                                    defender.IncreaseWounded();
                                    return new SetToTargetNumberModification(HPedit, "Dramatic Catch");
                                }
                                return null;
                            }
                            return null;
                        },
                        ExpiresAt = ExpirationCondition.Ephemeral
                    });
                }
            };
        });

    public static Feat GuardiansDeflection = new TrueFeat(ModManager.RegisterFeatName("Guardian's Deflection", "Guardian's Deflection"), 4, 
            "You use your weapon to deflect an attack made against an ally.", "{b}Trigger:{/b} An ally within your melee reach is hit by an attack, you can see the attacker, and a +2 circumstance bonus to AC would turn the critical hit into a hit or the hit into a miss.\n\n{b}Requirements: {/b} You are wielding a single one-handed weapon and have your other hand free.\n\n You use your weapon to deflect the attack against your ally, granting them a +2 circumstance bonus against the triggering attack. This turns the triggering critical hit into a hit, or the triggering hit into a miss.", [ SwashTrait ])
        .WithActionCost(Constants.ACTION_COST_REACTION)
        .WithPermanentQEffect((qf) =>
        {
            //Might be worth reworking to use zones. They're better for aura effects like this.
            //TODO: Rework to account for reach. I originally built this before Reach was implemented.
            qf.StateCheck = (deflect) =>
            {
                foreach (Creature ally in deflect.Owner.Battle.AllCreatures.Where((friend) => (friend.DistanceTo(deflect.Owner) == 1 && friend.FriendOf(deflect.Owner))))
                {
                    ally.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                    {
                        YouAreTargetedByARoll = async (deflection, attack, breakdownresult) =>
                        {
                            if ((qf.Owner.HasOneWeaponAndFist && qf.Owner.PrimaryWeapon != null && qf.Owner.PrimaryWeapon.HasTrait(Trait.Melee)) && (attack.HasTrait(Trait.Attack) && !attack.HasTrait(Trait.AttackDoesNotTargetAC)) && qf.Owner.CanSee(attack.Owner) && breakdownresult.ThresholdToDowngrade <= 2 && (breakdownresult.CheckResult == CheckResult.Success || breakdownresult.CheckResult == CheckResult.CriticalSuccess))
                            {
                                CheckResult result = breakdownresult.CheckResult;
                                if (await qf.Owner.Battle.AskToUseReaction(qf.Owner, ally.Name + " is about to be hit by an attack. Use Guardian's Deflection to downgrade the " + result.HumanizeTitleCase2() + " to a " + result.WorsenByOneStep().HumanizeTitleCase2() + "?"))
                                {
                                    ally.AddQEffect(new QEffect()
                                    {
                                        ExpiresAt = ExpirationCondition.EphemeralAtEndOfImmediateAction,
                                        BonusToDefenses = (effect, action, defense) => (defense != Defense.AC) ? null : new Bonus(2, BonusType.Circumstance, "Guardian's Deflection")
                                    });
                                    return true;
                                }
                            }
                            return false;
                        }
                    });
                }
            };
        });

    public static void GiveGuardiansDeflectionToFighters()
    {
        TrueFeat trueFeat = AllFeats.GetFeatByFeatName(GuardiansDeflection.FeatName) as TrueFeat;
        Feat newFeat = new TrueFeat(ModManager.RegisterFeatName(trueFeat.FeatName.ToString() + "Fighter", trueFeat.Name), 6, trueFeat.FlavorText, trueFeat.RulesText, [ Trait.Fighter ])
            .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
            {
                sheet.GrantFeat(trueFeat.FeatName);
            });
        ModManager.AddFeat(newFeat);
    }

    /*
    public static Feat GuardiansDeflectionFighter = CommonFeatTemplates.CreateDuplicateFeatForDifferentClass(GuardiansDeflection.FeatName, ModManager.RegisterFeatName(GuardiansDeflection.FeatName.ToString() + "Fighter", GuardiansDeflection.Name), 6, Trait.Fighter);
    */

    public static Feat ImpalingFinisher = new TrueFeat(ModManager.RegisterFeatName("Impaling Finisher", "Impaling Finisher"), 4, 
            "You stab two foes with one thrust or bash them together with one punch.", "Make a bludgeoning or piercing melee Strike, then make an additional Strike against a creature directly behind them in a straight line.", [ SwashTrait, Finisher ])
        .WithActionCost(1)
        .WithPermanentQEffect((qf) =>
        {
            qf.ProvideStrikeModifier = (item) =>
            {
                StrikeModifiers imp = new StrikeModifiers();
                bool flag = !item.HasTrait(Trait.Ranged) && (item.WeaponProperties!.DamageKind == DamageKind.Bludgeoning || item.WeaponProperties.DamageKind == DamageKind.Piercing);
                bool flag2 = qf.Owner.HasEffect(PanacheId);
                if (flag && flag2)
                {
                    int map = qf.Owner.Actions.AttackedThisManyTimesThisTurn;
                    return new CombatAction(qf.Owner, new SideBySideIllustration(item.Illustration, item.Illustration),
                            "Impaling Finisher",
                            [ Trait.AlwaysHits, Trait.IsHostile, Trait.Attack, Trait.AttackDoesNotIncreaseMultipleAttackPenalty, Finisher, Trait.Basic ],
                            "Make a bludgeoning or piercing attack against an adjacent enemy, then an enemy directly behind them in a straight line.",
                            Target.MultipleCreatureTargets(Target.Touch(), Target.Ranged(4))
                                .WithAdditionalRestrictionsOnEachTarget((caster, previousCreature, newCreature) =>
                                {
                                    if (!previousCreature.Any()) return true;
                                    int xtranslate = caster.Space.TopLeftTile.X - previousCreature[0].Space.TopLeftTile.X;
                                    int ytranslate = caster.Space.TopLeftTile.Y - previousCreature[0].Space.TopLeftTile.Y;
                                    bool straightLine = (newCreature.Space.AnyTile((Tile t) => 
                                        (xtranslate == (previousCreature[0].Space.TopLeftTile.X - t.X)) && (ytranslate == (previousCreature[0].Space.TopLeftTile.Y - t.Y))
                                        ));
                                    return previousCreature.All(acr => acr != newCreature && straightLine);
                                })
                                .WithMustBeDistinct()
                                .WithMinimumTargets(2))
                        .WithActionCost(1)
                        .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                        {
                            CombatAction impale = caster.CreateStrike(item, map)
                                .WithActionCost(0)
                                .WithExtraTrait(Finisher)
                                .WithExtraTrait(Trait.ActionDoesNotRequireLegalTarget);
                            impale.ChosenTargets = ChosenTargets.CreateSingleTarget(target);
                            await impale.AllExecute();
                        })
                        .WithEffectOnSelf(async (spell, self) =>
                        {
                            FinisherExhaustion(self);
                        });
                }
                else return null;
            };
        });

    public static Feat LeadingDance = new TrueFeat(ModManager.RegisterFeatName("LeadingDance", "Leading Dance"), 4, 
            "You sweep your foe into your dance.", "Attempt a Performance check against an adjacent enemy's Will DC. If you have the Battledancer swashbuckler style and you succeed, you gain panache." + S.FourDegreesOfSuccess("You move up to 10 feet, and the enemy follows you. Your movement doesn't trigger reactions (and the enemy's movement doesn't trigger reactions because it's forced movement).", "As critical success, but you both only move 5 feet.", "You can move 5 feet if you choose, but this movement triggers reactions normally.", "You stumble, falling prone in your space."), [ SwashTrait, Trait.Move ])
        .WithActionCost(1)
        .WithPrerequisite(values => values.GetProficiency(Trait.Performance) >= Proficiency.Trained, "You must be trained in Performance.")
        .WithPermanentQEffect(null, qf =>
        {
            qf.CharacterSheetBecomesCreature = (sheet, creature) =>
            {
                if (creature.HasFeat(BattledancerStyle))
                {
                    QEffect panacheGranter = creature.QEffects.First((QEffect fct) => fct.Key == "PanacheGranter");
                    List<ActionId> list = (List<ActionId>)panacheGranter.Tag;
                    list.Add(LeadingDanceId);
                    panacheGranter.Description += ", Leading Dance" + LeadingDanceId.HumanizeTitleCase2();
                }
            };
            qf.ProvideActionIntoPossibilitySection = (effect, section) =>
            {
                if (section.PossibilitySectionId == PossibilitySectionId.SkillActions)
                {
                    return new ActionPossibility(new CombatAction(effect.Owner, IllustrationName.WarpStep, "Leading Dance", [ Trait.Move ], "Attempt a Performance check (" + S.SkillBonus(qf.Owner, Skill.Performance) + ") against an adjacent enemy's Will DC. If you have the Battledancer swashbuckler style and you succeed, you gain panache." + S.FourDegreesOfSuccess("You move up to 10 feet, and the enemy follows you. Your movement doesn't trigger reactions (and the enemy's movement doesn't trigger reactions because it's forced movement).", "As critical success, but you both only move 5 feet.", "You can move 5 feet if you choose, but this movement triggers reactions normally.", "You stumble, falling prone in your space."),
                            Target.Touch())
                        .WithActionCost(1)
                        .WithActionId(LeadingDanceId)
                        .WithShortDescription("Attempt to move yourself and a foe.")
                        .WithActiveRollSpecification(new ActiveRollSpecification(TaggedChecks.SkillCheck(Skill.Performance), Checks.DefenseDC(Defense.Will)))
                        .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                        {
                            QEffect noReactions = new QEffect()
                            {
                                Id = QEffectId.IgnoreAoOWhenMoving
                            };
                            switch (result)
                            {
                                case CheckResult.CriticalSuccess:
                                    caster.AddQEffect(noReactions);
                                    await caster.StrideOrStepAdvancedAsync("Choose a location to move to:", maximumSpeed: 2, allowPass: true);
                                    await caster.PullCreature(target);
                                    noReactions.ExpiresAt = ExpirationCondition.Immediately;
                                    break;
                                case CheckResult.Success:
                                    caster.AddQEffect(noReactions);
                                    await caster.StrideAsync("Choose a location to move to.", false, true, null, false, true);
                                    await caster.PullCreature(target);
                                    noReactions.ExpiresAt = ExpirationCondition.Immediately;
                                    break;
                                case CheckResult.Failure:
                                    await caster.StrideAsync("Choose a location to move to.", false, true, null, false, true);
                                    break;
                                case CheckResult.CriticalFailure:
                                    await caster.FallProne();
                                    break;
                            }
                        }));
                }
                else return null;
            };
        });

    public static Feat SwaggeringInitiative = new TrueFeat(ModManager.RegisterFeatName("SwaggeringInitiative", "Swaggering Initiative"), 4, 
            "You swagger readily into any battle.", "You gain a +2 circumstance bonus to initiative rolls.\nIn addition, when combat begins, you can drink one potion you're holding as a free action.", [ SwashTrait ])
        .WithPermanentQEffect((qf) =>
        {
            qf.Owner.AddQEffect(new QEffect()
            {
                BonusToInitiative = _ => new Bonus(2, BonusType.Circumstance, "Swaggering Initiative")
            });
            qf.StartOfCombat = async (qfSwag) =>
            {
                if ((qfSwag.Owner.PrimaryItem != null) && qfSwag.Owner.PrimaryItem.HasTrait(Trait.Drinkable))
                {
                    Item potion = qfSwag.Owner.PrimaryItem;
                    CombatAction quaff = new CombatAction(qfSwag.Owner, potion.Illustration, "Drink", [ Trait.Manipulate ], "Drink your " + potion.Name + ".\n\n" + potion.Description, Target.Self())
                        .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                        {
                            await potion.WhenYouDrink(spell, caster);
                            Sfxs.Play(SfxName.DrinkPotion);
                            qfSwag.Owner.HeldItems.Remove(potion);
                        });
                    if (await qfSwag.Owner.Battle.AskForConfirmation(qfSwag.Owner, potion.Illustration, "Would you like to quickly drink your " + potion.Name + "?", "Drink")) 
                    {
                        await qfSwag.Owner.Battle.GameLoop.FullCast(quaff);
                    }
                }
                else if ((qfSwag.Owner.SecondaryItem != null) && qfSwag.Owner.SecondaryItem.HasTrait(Trait.Drinkable))
                {
                    Item potion = qfSwag.Owner.SecondaryItem;
                    CombatAction quaff = new CombatAction(qfSwag.Owner, potion.Illustration, "Drink", [ Trait.Manipulate ], "Drink your " + potion.Name + ".\n\n" + potion.Description, Target.Self())
                        .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                        {
                            await potion.WhenYouDrink(spell, caster);
                            Sfxs.Play(SfxName.DrinkPotion);
                            qfSwag.Owner.HeldItems.Remove(potion);
                        });
                    if (await qfSwag.Owner.Battle.AskForConfirmation(qfSwag.Owner, potion.Illustration, "Would you like to quickly drink your " + potion.Name + "?", "Drink"))
                    {
                        await qfSwag.Owner.Battle.GameLoop.FullCast(quaff);
                    }
                }
            };
        });

    public static Feat TwinParry = new TrueFeat(ModManager.RegisterFeatName("Twin Parry", "Twin Parry"), 4, 
            "You use your two weapons to parry attacks.", "You gain a +1 circumstance bonus to your AC until the start of your next turn, or a +2 circumstance bonus if either of the weapons you hold have the parry trait. You lose this circumstance bonus if you no longer meet this feat's requirements.", [ Trait.Fighter, Trait.Ranger, SwashTrait ])
        .WithActionCost(1)
        .WithPermanentQEffect(null, qf =>
        {
            qf.ProvideMainAction = qftechnical =>
            {
                if ((qf.Owner.HeldItems.Count((i) => i.HasTrait(Trait.Weapon) && i.HasTrait(Trait.Melee)) == 2) && !qf.Owner.QEffects.Any((fct) => fct.Key == "TwinParry"))
                {
                    return new ActionPossibility(new CombatAction(qf.Owner, IllustrationName.Swords, "Twin Parry", [Trait.Basic], "You use your weapons to block oncoming attacks and gain a +1 bonus to AC (+2 if one of your weapons has the parry trait).",
                            Target.Self().WithAdditionalRestriction((Creature you) => you.QEffects.Any((QEffect fct) => fct.Key == "TwinParry") ? "already parrying" : null))
                        .WithActionCost(1)
                        .WithGoodness((tg, you, _) => you.AI.GainBonusToAC(you.HeldItems.Any((Item it) => it.HasTrait(AddWeapons.Parry)) ? 2 : 1))
                        .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                        {
                            QEffect parrybonus = new QEffect("Twin Parry", "You have a +" + (caster.HeldItems.Any((Item it) => it.HasTrait(AddWeapons.Parry)) ? "2" : "1") + " circumstance bonus to AC.", ExpirationCondition.ExpiresAtStartOfYourTurn, caster, IllustrationName.Swords)
                            {
                                Key = "TwinParry",
                                BonusToDefenses = (thing, bonk, defense) =>
                                {
                                    if (defense == Defense.AC)
                                    {
                                        if (thing.Owner.HeldItems.Any((Item it) => it.HasTrait(AddWeapons.Parry) && it.HasTrait(Trait.Melee)))
                                        {
                                            return new Bonus(2, BonusType.Circumstance, "Twin Parry");
                                        }
                                        else return new Bonus(1, BonusType.Circumstance, "Twin Parry");
                                    }
                                    else return null;
                                },
                                StateCheck = qfdw =>
                                {
                                    if ((qfdw.Owner.HeldItems.Count((Item i) => i.HasTrait(Trait.Weapon)) != 2) || qfdw.Owner.HasFreeHand)
                                    {
                                        qfdw.ExpiresAt = ExpirationCondition.Immediately;
                                    }
                                },
                                CountsAsABuff = true
                            };
                            target.AddQEffect(parrybonus);
                        })
                        .WithSoundEffect(SfxName.RaiseShield)); 
                }
                return null;
            };
        }); 
    
    public static void ReplaceOpportunityAttack()
        {
            TrueFeat trueFeat = AllFeats.GetFeatByFeatName(FeatName.AttackOfOpportunity) as TrueFeat;
            trueFeat!.WithAllowsForAdditionalClassTrait(SwashTrait);
        }

    public static Feat AgileManeuvers = new TrueFeat(ModManager.RegisterFeatName("AgileManeuvers", "Agile Maneuvers"), 6, 
            "You easily maneuver against your foes.", "Your Grapple, Trip, and Shove actions have a lower multiple attack penalty: -4 instead of -5 if they're the second attack on your turn, or -8 instead of -10 if they're the third or subsequent attack on your turn.", [ SwashTrait ])
        .WithPrerequisite(sheet => sheet.GetProficiency(Trait.Athletics) >= Proficiency.Expert, "You must be an expert in Athletics.")
        .WithPermanentQEffect(null, (qf) =>
        {
            qf.ModifyActionPossibility = (qf2, action) =>
            {
                if ((action.ActionId == ActionId.Trip 
                        || action.ActionId == ActionId.Shove 
                        || action.ActionId == ActionId.Grapple)
                    && !action.HasTrait(Trait.Agile))
                {
                    action.Traits.Add(Trait.Agile);
                }
            };
        });

    public static Feat CombinationFinisher = new TrueFeat(ModManager.RegisterFeatName("CombinationFinisher", "Combination Finisher"), 6, 
            "You combine a series of attacks with a powerful blow.", "Your finishers' Strikes have a lower multiple attack penalty: -4 (or -3 with an agile weapon) instead of -5 if they're the second attack on your turn, or -8 (or -6 with an agile weapon) instead of -10 if they're the third or subsequent attack on your turn.", [ SwashTrait ])
        .WithPermanentQEffect(null, (qf) =>
        {
            qf.BonusToAttackRolls = (effect, action, target) => 
            {
                if (action == null)
                {
                    return null;
                }
                if (action.HasTrait(Finisher))
                {
                    return new Bonus(Math.Min(effect.Owner.Actions.AttackedThisManyTimesThisTurn, 2), BonusType.Untyped, "MAP reduction (Agile Maneuvers)");
                }
                return null;
            };
        });

    public static Feat PreciseFinisher = new TrueFeat(ModManager.RegisterFeatName("PreciseFinisher", "Precise Finisher"), 6, 
            "Even when your foe avoids your Confident Finisher, you can still hit a vital spot.", "On a failure with Confident Finisher, you apply your full Precise Strike damage instead of half.", [ SwashTrait ])
        .WithPrerequisite((sheet) => sheet.HasFeat(Confident), "You must have Confident Finisher.")
        .WithPermanentQEffectAndSameRulesText(qf => { qf.Id = PreciseFinisherQEffectId; });

        public static Feat BleedingFinisher = new TrueFeat(ModManager.RegisterFeatName("BleedingFinisher", "Bleeding Finisher"), 8, 
                "Your blow inflicts profuse bleeding.", "Make a piercing or slashing Strike with a weapon or unarmed attack that allows you to add your Precise Strike damage. If you hit, the target takes persistent bleed damage equal to your Precise Strike finisher damage.", [ SwashTrait, Finisher ])
        .WithActionCost(1)
        .WithPermanentQEffect((qf) =>
        {
            qf.ProvideStrikeModifier = (item) =>
            {
                StrikeModifiers strikeModifiers2 = new StrikeModifiers();
                bool flag5 = !item.HasTrait(Trait.Ranged) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse));
                bool flag6 = qf.Owner.HasEffect(PanacheId);
                bool flag7 = item.WeaponProperties.DamageKind == DamageKind.Piercing || item.WeaponProperties.DamageKind == DamageKind.Slashing;
                if (flag5 && flag6 && flag7)
                {
                    return CreateBleedingFinisher(qf.Owner, item, false, strikeModifiers2);
                }
                return null;
            };
        });

    public static Feat DualFinisher = new TrueFeat(ModManager.RegisterFeatName("DualFinisher", "Dual Finisher"), 8, 
            "You split your attacks.", "Make two melee Strikes, each with a different weapon against a different foe. If the second Strike is made with a non-agile weapon, it takes a -2 penalty. Increase your multiple attack penalty only after attempting both Strikes.", [ SwashTrait, Finisher ])
        .WithActionCost(1)
        .WithPermanentQEffect(qf =>
        {
            qf.ProvideMainAction = delegate
            {
                bool hasPanache = qf.Owner.HasEffect(PanacheId);
                bool twoWeapons = qf.Owner.HeldItems.Count(i => i.HasTrait(Trait.Weapon) && i.HasTrait(Trait.Melee)) == 2;
                return (hasPanache && twoWeapons) ? new ActionPossibility(new CombatAction(qf.Owner, new SideBySideIllustration(qf.Owner.HeldItems[0].Illustration, qf.Owner.HeldItems[1].Illustration), "Dual Finisher", [ Trait.Attack, Trait.IsHostile, Trait.AttackDoesNotIncreaseMultipleAttackPenalty, Trait.AlwaysHits, Finisher, Trait.Basic ], 
                        "Make two attacks, one with each of your two weapons, each against a different target. You lose panache and increase your multiple attack penalty after performing both attacks.", 
                        Target.MultipleCreatureTargets(Target.Reach(qf.Owner.HeldItems[0]), Target.Reach(qf.Owner.HeldItems[1])).WithMustBeDistinct().WithMinimumTargets(2))
                    .WithActionCost(1)
                    .WithEffectOnChosenTargets(async (swash, target) =>
                {
                    QEffect penalty = new QEffect
                    {
                        BonusToAttackRolls = (_, _, _) => new Bonus(-2, BonusType.Untyped, "Dual Finisher penalty")
                    };
                    int map = qf.Owner.Actions.AttackedThisManyTimesThisTurn;
                    if (qf.Owner.HeldItems.Count >= 1)
                    {
                        CombatAction strike = swash.CreateStrike(swash.HeldItems[0], map)
                            .WithActionCost(0)
                            .WithExtraTrait(Finisher);
                        strike.ChosenTargets = ChosenTargets.CreateSingleTarget(target.ChosenCreatures[0]);
                        await strike.AllExecute();
                    }

                    if (!qf.Owner.HeldItems[1].HasTrait(Trait.Agile))
                    {
                        swash.AddQEffect(penalty);
                    }

                    if (qf.Owner.HeldItems.Count >= 2)
                    {
                        CombatAction strike2 = swash.CreateStrike(swash.HeldItems[1], map)
                            .WithActionCost(0)
                            .WithExtraTrait(Finisher);
                        strike2.ChosenTargets = ChosenTargets.CreateSingleTarget(target.ChosenCreatures[1]);
                        await strike2.AllExecute();
                    }

                    penalty.ExpiresAt = ExpirationCondition.Immediately;
                    FinisherExhaustion(swash);
                })) : null;
            };
        });

    public static Feat FlamboyantCruelty = new TrueFeat(ModManager.RegisterFeatName("FlamboyantCruelty", "Flamboyant Cruelty"), 8, 
            "You love to kick your enemies when they're down, and look fabulous when you do so.", "Whenever you make a melee Strike against a foe with at least two of the following conditions, you gain a circumstance bonus to your damage roll equal to the number of conditions the target has. The qualifying conditions are {r}clumsy{/r}, {r}drained{/r}, {r}enfeebled{/r}, {r}frightened{/r}, {r}sickened{/r}, and {r}stupefied{/r}. If you hit such a foe, you gain a +1 circumstance bonus to skill checks to Tumble Through and perform your style's panache-granting actions until the end of your turn.", [ SwashTrait ])
        .WithPermanentQEffect("You deal more damage hitting enemies affected by certain adverse conditions.", (qf) =>
        {
            qf.BonusToDamage = (effect, action, defender) =>
            {
                int num = 0;
                if (defender.HasEffect(QEffectId.Clumsy)) num++;
                if (defender.HasEffect(QEffectId.Drained)) num++;
                if (defender.HasEffect(QEffectId.Enfeebled)) num++;
                if (defender.HasEffect(QEffectId.Frightened)) num++;
                if (defender.HasEffect(QEffectId.Sickened)) num++;
                if (defender.HasEffect(QEffectId.Stupefied)) num++;

                return (num >= 2) ? new Bonus(num, BonusType.Circumstance, "Flamboyant Cruelty") : null;
            };
            qf.AfterYouDealDamage = async (attacker, strike, defender) =>
            {
                int conditions = 0;
                if (defender.HasEffect(QEffectId.Clumsy)) conditions++;
                if (defender.HasEffect(QEffectId.Drained)) conditions++;
                if (defender.HasEffect(QEffectId.Enfeebled)) conditions++;
                if (defender.HasEffect(QEffectId.Frightened)) conditions++;
                if (defender.HasEffect(QEffectId.Sickened)) conditions++;
                if (defender.HasEffect(QEffectId.Stupefied)) conditions++;

                if (conditions >= 2)
                {
                    attacker.AddQEffect(new QEffect("Flamboyant Cruelty", "You have a +1 circumstance bonus to Tumble Through and to perform actions that would give you panache.", ExpirationCondition.ExpiresAtEndOfYourTurn, attacker, new ModdedIllustration("PhoenixAssets/panache.PNG"))
                    {
                        BonusToSkillChecks = (skill, action, target) =>
                        {
                            QEffect panacheGranter = qf.Owner.QEffects.First((fct) => fct.Key == "PanacheGranter");
                            var list = (List<ActionId>)panacheGranter.Tag;
                            if (list.Contains(action.ActionId))
                            {
                                return new Bonus(1, BonusType.Circumstance, "Flamboyant Cruelty");
                            }
                            else return null;
                        },
                        CountsAsABuff = true
                    });
                }
            };
        });

    public static void ReplaceNimbleRoll()
    {
        TrueFeat trueFeat = AllFeats.GetFeatByFeatName(FeatName.NimbleRoll) as TrueFeat;
        trueFeat.WithAllowsForAdditionalClassTrait(SwashTrait);
    }

    public static Feat StunningFinisher = new TrueFeat(ModManager.RegisterFeatName("StunningFinisher", "Stunning Finisher"), 8, 
            "You attempt a dizzying blow.", "Make a melee Strike. If you hit, your target must make a Fortitude save against your class DC with the following results: this save has the {r}incapacitation{/r} trait." + S.FourDegreesOfSuccess("The target is unaffected.", "The target can't take reactions until its next turn.", "The creature is {r}stunned{/r} 1.", "The creature is stunned 3."), [ SwashTrait, Finisher ])
        .WithActionCost(1)
        .WithPermanentQEffect(null, (qf) =>
        {
            qf.ProvideStrikeModifier = (item) =>
            {
                StrikeModifiers strikeModifiers = new StrikeModifiers();
                bool flag = item.HasTrait(Trait.Melee);
                bool flag2 = qf.Owner.HasEffect(PanacheId);
                if (flag && flag2)
                {
                    return CreateStunningFinisher(qf.Owner, item, false, strikeModifiers);
                }
                return null;
            };
        });

    public static Feat VivaciousBravado = new TrueFeat(ModManager.RegisterFeatName("VivaciousBravado", "Vivacious Bravado"), 8,
            "Your ego swells, granting you a temporary reprieve from your pain.", "{b}Requirements: {/b}You gained panache this turn. \n\nYou gain temporary HP equal to your level plus your Charisma modifier.", [ SwashTrait ])
        .WithActionCost(1)
        .WithPermanentQEffect((qf) =>
        {
            qf.AfterYouAcquireEffect = async (qfThis, qfGet) =>
            {
                if (qfGet.Id == PanacheId)
                {
                    qfThis.Owner.AddQEffect(new QEffect()
                    {
                        Key = "VivaciousBravado",
                        ProvideMainAction = qfVivacious =>
                        {
                            int hpgained = qfVivacious.Owner.Level + qfVivacious.Owner.Abilities.Charisma;
                            return new ActionPossibility(new CombatAction(qfVivacious.Owner, IllustrationName.WinningStreak, "Vivacious Bravado", [], "You gain " + hpgained + " temporary HP.", Target.Self())
                                .WithActionCost(1)
                                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                                {
                                    caster.GainTemporaryHP(hpgained);
                                })
                                .WithSoundEffect(SfxName.NaturalHealing));
                        },
                        ExpiresAt = ExpirationCondition.ExpiresAtEndOfYourTurn
                    });
                }
            };
        });
    
    public static void ReplaceDazzlingDisplay()
    {
        TrueFeat trueFeat = AllFeats.GetFeatByFeatName(FeatName.DazzlingDisplay) as TrueFeat;
        trueFeat!.WithAllowsForAdditionalClassTrait(SwashTrait);
    }
    
    public static Feat DerringDo = new TrueFeat(ModManager.RegisterFeatName("DerringDo", "Derring-Do"), 10, 
            "When you compound panache with even more derring-do, it somehow tends to work out.", "When you already have panache, you can roll twice and use the higher result on checks that would normally give you panache.", [ SwashTrait ])
        .WithPermanentQEffectAndSameRulesText(qf =>
        {
            qf.RerollActiveRoll = async (fct, result, action, target) =>
            {
                if (fct.Owner.HasEffect(PanacheId))
                {
                    QEffect panacheGranter = fct.Owner.QEffects.First((QEffect fct) => fct.Key == "PanacheGranter");
                    List<ActionId> list = (List<ActionId>)panacheGranter.Tag;
                    if (list.Contains(action.ActionId))
                    {
                        return RerollDirection.RerollAndKeepBest;
                    }
                }
                return RerollDirection.DoNothing;
            };
        });
    
    public static Feat DuelingDanceSwashbuckler = CommonFeatTemplates.CreateDuplicateFeatForDifferentClass(FeatName.DuelingDance, ModManager.RegisterFeatName("DuelingDanceSwashbuckler", "Dueling Dance"), 10, SwashTrait);
    
    public static Feat ReflexiveRiposte = new TrueFeat(ModManager.RegisterFeatName("ReflexiveRiposte", "Reflexive Riposte"), 10,
            "You can riposte almost without a thought.", "At the start of each of your turns, you gain an additional reaction that you can only use to make an Opportune Riposte.", [SwashTrait])
        .WithPrerequisite(OpportuneRiposte.FeatName, "Opportune Riposte")
        .WithPermanentQEffectAndSameRulesText(qf =>
        {
            qf.OfferExtraReaction = (qfThis, reactionText, reactionTraits) => reactionTraits.Contains(OpportuneRiposteTrait) ? "Reflexive Riposte" : null;
        });
    
    public static Feat TargetingFinisher = new TrueFeat(ModManager.RegisterFeatName("TargetingFinisher", "Targeting Finisher"), 10,
            "Your attack harms and hinders your foe.",
            "Choose a hinderance and make a Strike. On a success, apply the chosen effect. This hinderance lasts until the end of your next turn. On a critical success, you also apply a lesser effect for the rest of the encounter." +
                "\n\n{b}Arm{/b}: The target is {r}enfeebled{/r} 2. On a critical success, it is enfeebled 1 for the rest of the encounter." +
                "\n\n{b}Head{/b}: The target is {r}stupefied{/r} 2. On a critical success, it is stupefied 1 for the rest of the encounter." +
                "\n\n{b}Legs{/b}: The target takes a -10-foot status penalty to its Speed. On a critical success, it takes a -5-foot status penalty to its Speed for the rest of the encounter.",
            [SwashTrait, Finisher])
        .WithActionCost(1)
        .WithPermanentQEffect(qf =>
        {
            qf.ProvideStrikeModifierAsPossibilities = (qfThis, item) =>
            {
                var actions = new List<ActionPossibility>();
                if (qfThis.Owner.HasEffect(PanacheId) && item.HasTrait(Trait.Melee))
                {
                    actions.AddRange(
                        new ActionPossibility(CreateTargetingFinisherHead(qfThis.Owner, item, false, new StrikeModifiers())),
                        new ActionPossibility(CreateTargetingFinisherArm(qfThis.Owner, item, false, new StrikeModifiers())),
                        new ActionPossibility(CreateTargetingFinisherLeg(qfThis.Owner, item, false, new StrikeModifiers()))
                    );
                }
                return actions;
            };
        });
    
    public static Feat CheatDeath = new TrueFeat(ModManager.RegisterFeatName("CheatDeath", "Cheat Death"), 12,
            "Somehow you always escape the reaper by a hair's breadth.",
            "When you are reduced to 0 HP, you can spend your {icon:Reaction}reaction to remain at 1 HP. If you do, you become {r}doomed{/r} 1, or increase your doomed value by 1 if you were already doomed.", [SwashTrait])
        .WithActionCost(Constants.ACTION_COST_REACTION)
        .WithPermanentQEffect(qf =>
        {
            qf.YouAreDealtLethalDamage = async (qfCheatDeath, attacker, damageStuff, defender) =>
            {
                if (await defender.AskToUseReaction("You are about to be reduced to 0 HP. Use Cheat Death to remain at 1 HP and become doomed?"))
                {
                    defender.Overhead("cheat death!", Color.Red, defender.ToColoredBoldedName() + " resists dying through Cheat Death.");
                    int permissibleDamage = defender.HP - 1;
                    if (defender.FindQEffect(QEffectId.Doomed) != null)
                    {
                        defender.FindQEffect(QEffectId.Doomed)!.Value++;
                    }
                    else defender.AddQEffect(QEffect.Doomed(1));
                    return new SetToTargetNumberModification(permissibleDamage, "Cheat Death");
                }
                return null;
            };
        });
    
    public static Feat MobileFinisher = new TrueFeat(ModManager.RegisterFeatName("MobileFinisher", "Mobile Finisher"), 12,
            "You attack in motion.",
            "Stride, then make a Strike.", [SwashTrait, Finisher])
        .WithActionCost(1)
        .WithPermanentQEffect(qf =>
        {
            qf.ProvideStrikeModifier = item =>
            {
                if (qf.Owner.HasEffect(PanacheId))
                {
                    var mobileFinisher = new CombatAction(qf.Owner, new SideBySideIllustration(IllustrationName.Walk, item.Illustration),
                            "Mobile Finisher", [Trait.Attack, Trait.AttackDoesNotIncreaseMultipleAttackPenalty, Trait.Basic], "Stride, then Strike.\n\nYou lose panache.",
                            Target.Self())
                        .WithActionCost(1)
                        .WithEffectOnChosenTargets(async (spell, caster, targets) =>
                        {
                            var qfTechnical = new QEffect() 
                            { 
                                AdjustStrikeAction = (qfThis, strike) =>
                                {
                                    strike.WithExtraTrait(Finisher);
                                }
                            };
                            qf.Owner.AddQEffect(qfTechnical);
                            if (!await caster.StrideAsync("Stride. Then you will Strike.", allowCancel: true))
                            {
                                spell.RevertRequested = true;
                                return;
                            }
                            await CommonCombatActions.StrikeAnyCreature(caster, null, false); 
                            qf.Owner.RemoveAllQEffects(fct => fct == qfTechnical);
                        });
                    return mobileFinisher;
                }
                return null;
            };
        });
    /*
    public static Feat ImpossibleRiposte = new TrueFeat(ModManager.RegisterFeatName("ImpossibleRiposte", "Impossible Riposte"), 14,
            "Your ripostes can deflect attacks back at their source.",
            "You can use Opportune Riposte when an enemy critically fails an attack roll against you.", [SwashTrait])
        .WithActionCost(Constants.ACTION_COST_REACTION)
        .WithPermanentQEffect(qf =>
        {
            qf.AddGrantingOfTechnical(cr => cr.EnemyOf(qf.Owner), qfTechnical =>
            {
                qfTechnical.AfterYouTakeActionAgainstTargetReaction = (qfThis, spell, target, result) =>
                {
                    if (target == qf.Owner && result == CheckResult.CriticalFailure)
                    {
                        return ReactionOption.CreateCustom("Impossible Riposte", "[RIPOSTE]", new ModdedIllustration("PhoenixAssets/panache.PNG"), qf.Owner,
                                async () =>
                                {
                                })
                            .WithIsReaction()
                            .WithTraits(OpportuneRiposteTrait);
                    }
                    return null;
                };
            });
        })
        .WithPrerequisite(values => values.HasFeat(OpportuneRiposte), "You must have the Opportune Riposte feature.");
    */
    public static Feat PerfectFinisher = new TrueFeat(ModManager.RegisterFeatName("PerfectFinisher", "Perfect Finisher"), 14,
            "You focus your panache into an impeccable assault.",
            "Make a Strike, rolling the attack roll twice and using the higher result.", [SwashTrait, Finisher, Trait.Fortune])
        .WithActionCost(1)
        .WithPermanentQEffect(qf =>
        {
            qf.ProvideStrikeModifier = item =>
            {
                if (qf.Owner.HasEffect(PanacheId) && item.HasTrait(Trait.Melee))
                {
                    return CreatePerfectFinisher(qf.Owner, item, false, new StrikeModifiers());
                }
                return null;
            };
        });
        
    public class SwashbucklerStyle : Feat
    {
        public Skill Skill { get; set; }
        public ActionId[] PanacheTriggers { get; set; }
        public SwashbucklerStyle(FeatName featName, string flavor, string rules, string exemplaryEffect, Skill styleSkill, ActionId[] panacheTriggers)
            : base(featName, flavor, rules + "\n{b}Exemplary Finisher{/b}: " + exemplaryEffect, new List<Trait>() { SwashStyle }, null)
        {
            Skill = styleSkill;
            PanacheTriggers = panacheTriggers;
            this.WithPermanentQEffect(null, (qf) =>
            {
                qf.CharacterSheetBecomesCreature = (sheet, creature) =>
                {
                    QEffect panacheGranter = creature.QEffects.First((fct) => fct.Key == "PanacheGranter");
                    List<ActionId> list = (List<ActionId>)panacheGranter.Tag;
                    foreach (ActionId id in PanacheTriggers)
                    {
                        list.Add(id);
                        panacheGranter.Description += ", " + id.HumanizeTitleCase2();
                    }
                };
            });
        }
    }
    public static void LoadSwash()
    {
        ModManager.AddFeat(Swashbuckler);
        //ModManager.AddFeat(AddPanache);
        ModManager.AddFeat(FascinatingPerformance);
        ModManager.AddFeat(DisarmingFlair);
        ModManager.AddFeat(DuelingParrySwashbuckler);
        ModManager.AddFeat(FlyingBlade);
        ModManager.AddFeat(FocusedFascination);
        ModManager.AddFeat(GoadingFeint);
        ReplaceNimbleDodge();
        ModManager.AddFeat(OneForAll);
        ReplaceYoureNext();
        ModManager.AddFeat(StylishEntrance);
        ModManager.AddFeat(AfterYou);
        ModManager.AddFeat(Antagonize);
        ModManager.AddFeat(CharmedLife);
        ModManager.AddFeat(FinishingFollowThrough);
        ModManager.AddFeat(TumbleBehind);
        ModManager.AddFeat(UnbalancingFinisher);
        ModManager.AddFeat(DazzlingDiversion);
        ModManager.AddFeat(DramaticCatch);
        ModManager.AddFeat(GuardiansDeflection);
        GiveGuardiansDeflectionToFighters();
        //ModManager.AddFeat(GuardiansDeflectionFighter);
        ModManager.AddFeat(ImpalingFinisher);
        ModManager.AddFeat(LeadingDance);
        ModManager.AddFeat(SwaggeringInitiative);
        ModManager.AddFeat(TwinParry);
        //ModManager.AddFeat(AgileManeuvers);
        //ModManager.AddFeat(CombinationFinisher);
        ReplaceOpportunityAttack();
        ModManager.AddFeat(PreciseFinisher);
        ModManager.AddFeat(BleedingFinisher);
        ModManager.AddFeat(DualFinisher);
        ModManager.AddFeat(FlamboyantCruelty);
        ReplaceNimbleRoll();
        ModManager.AddFeat(StunningFinisher);
        ModManager.AddFeat(VivaciousBravado);
        
        ModManager.AddFeat(DerringDo);
        ReplaceDazzlingDisplay();
        ModManager.AddFeat(DuelingDanceSwashbuckler);
        ModManager.AddFeat(ReflexiveRiposte);
        ModManager.AddFeat(TargetingFinisher);
        ModManager.AddFeat(CheatDeath);
        ModManager.AddFeat(MobileFinisher);
        ModManager.AddFeat(PerfectFinisher);
    }
}