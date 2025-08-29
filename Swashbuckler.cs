using Dawnsbury.Audio;
using Dawnsbury.Core;
using Dawnsbury.Core.CharacterBuilder;
using Dawnsbury.Core.CharacterBuilder.AbilityScores;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.Feats.Features;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.Selections.Options;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Rules;
using Dawnsbury.Core.Mechanics.Targeting;
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
using System.Data;
using System.Linq;

namespace Dawnsbury.Mods.Phoenix;

public class AddSwash
{
    public static Trait SwashTrait = ModManager.RegisterTrait("SwashTrait", new TraitProperties("Swashbuckler", true) { IsClassTrait = true });
    public static Trait SwashStyle = ModManager.RegisterTrait("SwashStyle", new TraitProperties("Swashbuckler Style", false));
    public static Trait Finisher = ModManager.RegisterTrait("Finisher", new TraitProperties("Finisher", true, "You can only use an action with the Finisher trait if you have panache, and you lose panache after performing the action.", true));
    public static QEffectId PanacheId = ModManager.RegisterEnumMember<QEffectId>("Panache");
    public static QEffectId FascinatedId = ModManager.RegisterEnumMember<QEffectId>("Fascinated");
    public static QEffectId PreciseFinisherQEffectId = ModManager.RegisterEnumMember<QEffectId>("PreciseFinisherQEffectId");
    public static ActionId BonMotId = ModManager.RegisterEnumMember <ActionId>("BonMot");
    public static ActionId FascinatingPerformanceActionId = ModManager.RegisterEnumMember<ActionId>("FascinatingPerformanceAction");
    public static ActionId UnbalancingFinisherId = ModManager.RegisterEnumMember<ActionId>("UnbalancingFinisher");
    public static FeatName BattledancerStyle = ModManager.RegisterFeatName("Battledancer");
    public static FeatName BraggartStyle = ModManager.RegisterFeatName("Braggart");
    public static FeatName FencerStyle = ModManager.RegisterFeatName("Fencer");
    public static FeatName GymnastStyle = ModManager.RegisterFeatName("Gymnast");
    public static FeatName WitStyle = ModManager.RegisterFeatName("Wit");
    public static QEffect CreatePanache(Skill styleSkill)
    {
        QEffect panache = new QEffect()
        {
            Id = PanacheId,
            Key = "Panache",
            Name = "Panache",
            Illustration = new ModdedIllustration("PhoenixAssets/panache.PNG"),
            Description = "You have a status bonus to your Speed and a +1 circumstance bonus to Acrobatics and " + styleSkill.HumanizeTitleCase2() + " skill checks.\n\nYou can use finishers by spending panache.",
            ExpiresAt = ExpirationCondition.Never,
            BonusToAllSpeeds = (delegate(QEffect qfpanache)
            {
                return new Bonus(1, BonusType.Status, "Panache");
            }),
            BonusToSkillChecks = delegate (Skill skill, CombatAction action, Creature? creature)
            {
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
            }
        };
        return panache;
    }

    public static QEffect CreateFascinated(Creature source)
    {
        List<QEffect> list = new List<QEffect>();
        foreach (Creature c in source.Battle.AllCreatures)
        {
            if (c.EnemyOf(source))
            {
                QEffect qf = new QEffect()
                {
                    AfterYouAreTargeted = async delegate (QEffect qf, CombatAction action)
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
            BonusToPerception = delegate (QEffect fct)
            {
                return new Bonus(-2, BonusType.Status, "Fascinated");
            },
            BonusToSkillChecks = delegate (Skill skill, CombatAction action, Creature? target)
            {
                return new Bonus(-2, BonusType.Status, "Fascinated");
            },
            YouAreTargeted = async (QEffect fct, CombatAction action) =>
            {
                if (action.IsHostileAction && action.ActionId != FascinatingPerformanceActionId)
                {
                    fct.ExpiresAt = ExpirationCondition.Immediately;
                }
            },
            PreventTakingAction = delegate (CombatAction action)
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
            }
        };
        list.Add(fascinate);
        return fascinate;
    }

    public static void FinisherExhaustion(Creature swash)
    {
        swash.AddQEffect(new QEffect()
        {
            Name = "Used Finisher",
            Illustration = IllustrationName.Fatigued,
            Description = "After using a finisher, you can't take any Attack actions for the rest of your turn.",
            ExpiresAt = ExpirationCondition.ExpiresAtEndOfYourTurn,
            PreventTakingAction = (CombatAction action) => !(action.HasTrait(Trait.Attack)) ? null : "You used a finisher this turn."
        });
    }
    
    public static Feat Swashbuckler = new ClassSelectionFeat(ModManager.RegisterFeatName("Swashbuckler"), "Many warriors rely on brute force, weighty armor, or cumbersome weapons. For you, battle is a dance where you move among foes with style and grace. You dart among combatants with flair and land powerful finishing moves with a flick of the wrist and a flash of the blade, all while countering attacks with elegant ripostes that keep enemies off balance. Harassing and thwarting your foes lets you charm fate and cheat death time and again with aplomb and plenty of flair.",
        SwashTrait,
        new EnforcedAbilityBoost(Ability.Dexterity),
        10,
        new Trait[]
        {
            Trait.Fortitude,
            Trait.Simple,
            Trait.Martial,
            Trait.Unarmed,
            Trait.LightArmor,
            Trait.UnarmoredDefense
        },
        new Trait[]
        {
            Trait.Perception,
            Trait.Reflex,
            Trait.Will
        },
        4,
        "{b}1. Panache.{/b} You learn how to leverage your skills to enter a state of heightened ability called panache. You gain panache when you succeed on certain skill checks with a bit of flair, including Tumble Through and other checks determined by your style. While you have panache, you gain a +5 circumstance bonus to your Speed and a +1 circumstance bonus to checks that would give you panache. It also allows you to use special attacks called finishers, which cause you to lose panache when performed.\n{i}(The automatic pathfinding will normally chart a path that doesn't require a tumble through if possible. To tumble through a creature on purpose, use the step-by-step stride option in the Other actions menu.){/i}" +
        "\n{b}2. Swashbuckler style.{/b} You choose a style that represents what kind of flair you bring to a battlefield. When you choose a style, you become trained in a skill and can use certain actions using that skill to gain panache." +
        "\n{b}3. Precise Strike.{/b} While you have panache, you deal an extra 2 precision damage with your agile or finesse melee weapons. If you use a finisher, the damage increases to 2d6 instead." +
        "\n{b}4. Confident Finisher.{/b} If you have panache, you can use an action to make a Strike against an ally in melee range. If you miss, you deal half your Precise Strike damage." +
        "\n{b}5. Swashbuckler feat.{/b}",
        new List<Feat>()
        {
            //Subclasses. For a swashbuckler, this is their styles.
            new SwashbucklerStyle(BattledancerStyle, 
                    "To you, a fight is a kind of performance art, and you command your foes' attention with mesmerizing movements.", 
                    "You are trained in Performance and gain the Fascinating Performance skill feat. You gain panache whenever your Performance check exceeds the Will DC of an observing foe, even if that foe isn't fascinated.",
                    "When you hit with a finisher, you can Step as a free action.",
                    Skill.Performance, new ActionId[] {FascinatingPerformanceActionId })
                .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
                {
                    sheet.GrantFeat(FeatName.Performance);
                    sheet.GrantFeat(FascinatingPerformance.FeatName);
                })
                .WithOnCreature(delegate (Creature swash)
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
                Skill.Intimidation, new ActionId[] { ActionId.Demoralize })
                .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
                {
                    sheet.GrantFeat(FeatName.Intimidation);
                })
                .WithOnCreature(delegate (Creature swash)
                {
                    if (swash.Level >= 9)
                    {
                        swash.AddQEffect(new QEffect("Exemplary Finisher", "When you hit with a finisher, you end a foe's temporary immunity to your Demoralize.")
                        {
                            AfterYouTakeAction = async (qf, action) =>
                            {
                                if (action.HasTrait(Finisher) && (action.CheckResult >= CheckResult.Success))
                                {
                                    QEffect targetEffect = action.ChosenTargets.ChosenCreature.QEffects.FirstOrDefault((QEffect q) => (q.Id == QEffectId.ImmunityToTargeting) && (q.Source == qf.Owner) && ((ActionId)q.Tag == ActionId.Demoralize));
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
                    "When you hit with a finisher, the target is flat-footed until your next turn.",
                    Skill.Deception, new ActionId[] { ActionId.Feint, ActionId.CreateADiversion })
                .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
                {
                    sheet.GrantFeat(FeatName.Deception);
                })
                .WithOnCreature(delegate (Creature swash)
                {
                    if (swash.Level >= 9)
                    {
                        swash.AddQEffect(new QEffect("Exemplary Finisher", "When you hit with a finisher, the target is flat-footed until your next turn.")
                        {
                            AfterYouTakeAction = async (qf, action) =>
                            {
                                if (action.HasTrait(Finisher) && (action.CheckResult >= CheckResult.Success))
                                {
                                    action.ChosenTargets.ChosenCreature.AddQEffect(QEffect.FlatFooted("Exemplary Finisher").WithExpirationAtStartOfSourcesTurn(qf.Owner, 1));
                                }
                            }
                        });
                    }
                }),
            new SwashbucklerStyle(GymnastStyle, 
                    "You reposition, maneuver, and bewilder your foes with daring feats of physical prowess.", 
                    "You become trained in Athletics. You gain panache whenever you successfully Grapple, Shove, or Trip a foe.",
                    "When you use a finisher, if the target is grabbed, restrained, or prone, you gain a circumstance bonus to damage equal to the weapon's number of damage dice.",
                    Skill.Athletics, new ActionId[] { ActionId.Grapple, ActionId.Shove, ActionId.Trip })
                .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
                {
                    sheet.GrantFeat(FeatName.Athletics);
                })
                .WithOnCreature(delegate (Creature swash)
                {
                    if (swash.Level >= 9)
                    {
                        swash.AddQEffect(new QEffect("Exemplary Finisher", "Your finishers deal bonus damage to creatures that are grabbed, restrained, or prone.")
                        {
                            BonusToDamage = delegate (QEffect qf, CombatAction action, Creature defender)
                            {
                                if (action.HasTrait(Finisher))
                                {
                                    if (defender.HasEffect(QEffectId.Grabbed) || defender.HasEffect(QEffectId.Restrained) || defender.HasEffect(QEffectId.Prone))
                                    {
                                        int bonus = action.Item.WeaponProperties.DamageDieCount;
                                        return new Bonus(bonus, BonusType.Circumstance, "Exemplary Finisher");
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
                    Skill.Diplomacy, new ActionId[] { BonMotId })
                .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
                {
                    sheet.GrantFeat(FeatName.Diplomacy);
                    sheet.GrantFeat(BonMot.FeatName);
                })
                .WithOnCreature(delegate (Creature swash)
                {
                    if (swash.Level >= 9)
                    {
                        swash.AddQEffect(new QEffect("Exemplary Finisher", "When you hit with a finisher, the target takes a -2 circumstance penalty to attack rolls against you until the start of your next turn.")
                        {
                            AfterYouTakeAction = async (qf, action) =>
                            {
                                if (action.HasTrait(Finisher) && (action.CheckResult >= CheckResult.Success))
                                {
                                    action.ChosenTargets.ChosenCreature.AddQEffect(new QEffect()
                                    {
                                        BonusToAttackRolls = delegate (QEffect fct, CombatAction help, Creature? target)
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
        })
        .WithClassFeatures(features =>
        {
            features.AddFeature(3, "opportune riposte", "counterattack if an enemy critically fails to hit you");
            features.AddFeature(3, "vivacious speed", "the status bonus to Speed from panache increases to 10 feet and you gain half of it even if you don't have panache");
            features.AddFeature(5, "precise strike 3d6");
            features.AddFeature(5, "weapon expertise", "your proficiency with simple weapons, martial weapons, and unarmed strikes increases to expert. You gain access to the {tooltip:criteffect}critical specialization effects{/} of all weapons and unarmed attacks for which you have expert proficiency.");
            features.AddFeature(7, WellKnownClassFeature.Evasion);
            features.AddFeature(7, "vivacious speed +15 feet");
            features.AddFeature(7, WellKnownClassFeature.WeaponSpecialization);
            features.AddFeature(9, "exemplary finisher", "you gain a special effect when you perform finishers based on your swashbuckler style");
            features.AddFeature(9, "precise strike 4d6");
            features.AddFeature(9, "swashbuckler expertise", "your proficiency for your swashbuckler class DC increases to expert");
            features.AddFeature(11, "vivacious speed +20 feet");
            features.AddFeature(13, "precise strike 5d6");
            features.AddFeature(15, "vivacious speed +25 feet");
            features.AddFeature(17, "precise strike 6d6");
            features.AddFeature(19, "vivacious speed +30 feet");
        })
        .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
            {
                sheet.TrainInThisOrSubstitute(Skill.Acrobatics);
                sheet.AddFeat(Confident!, null);
                sheet.AddFeat(PreciseStrike!, null);
                sheet.AddSelectionOption(new SingleFeatSelectionOption("Swash1", "Swashbuckler feat", 1, (Feat ft) => ft.HasTrait(SwashTrait)));
            })
        .WithOnCreature(delegate (Creature creature)
        {
            creature.AddQEffect(PanacheGranter());

            if (creature.Level >= 5)
            {
                creature.AddQEffect(new QEffect("Weapon Expertise", "You gain the critical specialization effect of weapons with which you have expert proficiency.")
                {
                    YouHaveCriticalSpecialization = (QEffect effect, Item item, CombatAction _, Creature _) => effect.Owner.Proficiencies.Get(item.Traits) >= Proficiency.Expert
                });
            }
            if (creature.Level >= 7)
            {
                creature.AddQEffect(QEffect.Evasion());
                creature.AddQEffect(QEffect.WeaponSpecialization());
            }
        })
        .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
            {
                sheet.AddAtLevel(3, delegate (CalculatedCharacterSheetValues values)
                {
                    values.SetProficiency(Trait.Fortitude, Proficiency.Expert);
                    values.AddFeat(VivaciousSpeed!, null);
                    values.AddFeat(OpportuneRiposte!, null);
                });
                sheet.AddAtLevel(9, delegate (CalculatedCharacterSheetValues values)
                {
                    values.SetProficiency(SwashTrait, Proficiency.Expert);
                });
            });

    //TODO: Restrict Opportune Riposte to only target the attacking weapon.
    public static readonly Feat OpportuneRiposte = new Feat(ModManager.RegisterFeatName("Opportune Riposte", "Opportune Riposte {icon:Reaction}"), "You take advantage of an opening from your foe's fumbled attack.", "When an enemy critically fails its Strike against you, you can use your reaction to make a melee Strike against that enemy or make a Disarm attempt.", new List<Trait>(), null)
        .WithPermanentQEffect("When an enemy critically fails a Strike against you, you may Strike or Disarm it using a reaction.", delegate (QEffect qf)
        {
            qf.AfterYouAreTargeted = async delegate (QEffect qf, CombatAction action)
            {
                bool canReach = (qf.Owner.DistanceTo(action.Owner) == 1) || ((qf.Owner.DistanceTo(action.Owner) <= 2) && qf.Owner.WieldsItem(Trait.Reach)); //This is a brute force method of figuring out reach. I intend to make it more efficient later.
                if (canReach && qf.Owner.Actions.CanTakeReaction() && action.HasTrait(Trait.Strike) && action.CheckResult == CheckResult.CriticalFailure)
                {
                    Item playerWeapon = (action.Owner.DistanceTo(qf.Owner) > 1) ? qf.Owner.MeleeWeapons.FirstOrDefault((Item i) => i.HasTrait(Trait.Reach)) : qf.Owner.PrimaryWeapon;
                    if (playerWeapon == null) return;
                    Item enemyWeapon = action.Item;
                    bool cannotDisarm = ((enemyWeapon == null) || enemyWeapon.HasTrait(Trait.Unarmed) || (action.Owner.QEffects.FirstOrDefault((QEffect immuneDisarm) => immuneDisarm.Id == QEffectId.ImmunityToTargeting && (ActionId)immuneDisarm.Tag == ActionId.Disarm) != default));
                    Creature enemy2 = action.Owner;
                    if ((qf.Owner.HasFreeHand || qf.Owner.WieldsItem(Trait.Disarm)) && (!cannotDisarm))
                    {
                        switch ((await qf.Owner.AskForChoiceAmongButtons(new ModdedIllustration("PhoenixAssets/panache.png"), enemy2.Name + " has critically failed an attack against you. What do you wish to do?", "Disarm", "Strike", "Do not react")).Index)
                        {
                            case 0:
                                Item disarmingWeapon = qf.Owner.HeldItems.FirstOrDefault((Item i) => i.HasTrait(Trait.Disarm));
                                if (disarmingWeapon == default)
                                {
                                    disarmingWeapon = qf.Owner.UnarmedStrike;
                                }
                                CombatAction disarm2 = CombatManeuverPossibilities.CreateDisarmAction(qf.Owner, disarmingWeapon).WithActionCost(0);
                                disarm2.Target = Target.ReachWithAnyWeapon().WithAdditionalConditionOnTargetCreature((Creature self, Creature enemy) => enemy == action.Owner ? Usability.Usable : Usability.NotUsableOnThisCreature("not attacker"));
                                disarm2.ChosenTargets = ChosenTargets.CreateSingleTarget(enemy2);
                                await disarm2.AllExecute();
                                qf.Owner.Actions.UseUpReaction();
                                break;
                            case 1:
                                await qf.Owner.MakeStrike(enemy2, playerWeapon, 0);
                                qf.Owner.Actions.UseUpReaction();
                                break;
                        }
                    }
                    else if (await qf.Owner.Battle.AskToUseReaction(qf.Owner, enemy2.Name + " has critically failed an attack against you. Would you like to use your reaction to riposte and make a Strike?"))
                    {
                        await qf.Owner.MakeStrike(enemy2, playerWeapon, 0);
                    }
                }
            };
        });

    public static readonly Feat Confident = new Feat(ModManager.RegisterFeatName("Confident Finisher", "Confident Finisher{icon:Action}"), "You gain an elegant finishing move that you can use when you have panache.", "If you have panache, you can make a Strike that deals damage even on a failure.", new List<Trait>(), null)
        .WithPermanentQEffect("If you have panache, you can make a Strike that deals damage even on a failure.", delegate (QEffect qf)
        {
            qf.ProvideStrikeModifier = delegate (Item item)
            {
                StrikeModifiers conf = new StrikeModifiers();
                bool flag = !item.HasTrait(Trait.Ranged) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse));
                bool flag2 = qf.Owner.HasEffect(PanacheId);
                if (flag && flag2)
                {
                    CombatAction conffinish = qf.Owner.CreateStrike(item, -1, conf);

                    conffinish.Name = "Confident Finisher";
                    conffinish.Illustration = new SideBySideIllustration(item.Illustration, IllustrationName.StarHit);
                    conffinish.Description = StrikeRules.CreateBasicStrikeDescription2(conffinish.StrikeModifiers, null, null, null, "The target takes " + (((qf.Owner.Level - 1) / 4) + 2) + "d6/2 damage.", "You lose panache, whether the attack succeeds or fails.");
                    conffinish.ActionCost = 1;
                    conffinish.StrikeModifiers.OnEachTarget = async delegate (Creature owner, Creature victim, CheckResult result)
                    {
                        if (result == CheckResult.Failure)
                        {
                            HalfDiceFormula halfdamage = new HalfDiceFormula(DiceFormula.FromText((((qf.Owner.Level - 1) / 4) + 2).ToString() + "d6", "Precise Strike"), "Miss with Confident Finisher");
                            DiceFormula fulldamage = DiceFormula.FromText((((qf.Owner.Level - 1) / 4) + 2).ToString() + "d6", "Miss with Precise Finisher");
                            await CommonSpellEffects.DealDirectDamage(conffinish, qf.Owner.HasEffect(PreciseFinisherQEffectId) ? fulldamage : halfdamage, victim, result, conffinish.StrikeModifiers.CalculatedItem.WeaponProperties.DamageKind);
                        }
                        FinisherExhaustion(owner);
                    };
                    conffinish.Traits.Add(Finisher);
                    return conffinish;
                }
                return null;
            };
        });

    public static readonly Feat PreciseStrike = new Feat(ModManager.RegisterFeatName("PreciseStrike", "Precise Strike"), "You strike with flair.", "When you have panache and make a Strike with a melee agile or finesse weapon or an agile or finesse unarmed strike, you deal 2 extra damage. This damage is 2d6 instead if the Strike was part of a finisher.", new List<Trait>(), null)
        .WithPermanentQEffect("While you have panache, you deal more damage when using agile or finesse weapons.", delegate (QEffect qf)
        {
            qf.Name = "Precise Strike";
            qf.YouDealDamageWithStrike = delegate (QEffect qf, CombatAction action, DiceFormula diceFormula, Creature defender)
            {
                bool flag = action.HasTrait(Trait.Agile) || action.HasTrait(Trait.Finesse);
                bool flag2 = action.Owner.HasEffect(PanacheId);
                bool flag3 = action.HasTrait(Finisher);
                bool flag4 = !action.HasTrait(Trait.Ranged) || (action.HasTrait(Trait.Thrown) && (action.Owner.PersistentCharacterSheet?.Calculated.AllFeats.Any(feat => feat.Name == "Flying Blade") ?? false) && (defender.DistanceTo(qf.Owner) <= action.Item!.WeaponProperties!.RangeIncrement));
                bool flag5 = defender.IsImmuneTo(Trait.PrecisionDamage);
                if (flag && flag3 && flag4 && (!flag5))
                {
                    return diceFormula.Add(!(qf.Owner.PersistentCharacterSheet.Class.FeatName == Swashbuckler.FeatName) ? DiceFormula.FromText("1d6") : DiceFormula.FromText((((qf.Owner.Level - 1) / 4) + 2).ToString() + "d6", "Precise Strike"));
                }
                else if (flag && flag4 && (!flag5))
                {
                    return diceFormula.Add(!(qf.Owner.PersistentCharacterSheet.Class.FeatName == Swashbuckler.FeatName) ? DiceFormula.FromText("1") : DiceFormula.FromText((((qf.Owner.Level - 1) / 4) + 2).ToString(), "Precise Strike"));
                }
                return diceFormula;
            };
        });

    public static QEffect PanacheGranter()
    {
        return new QEffect()
        {
            AfterYouTakeActionAgainstTarget = async delegate (QEffect qf, CombatAction action, Creature target, CheckResult result)
            {
                SwashbucklerStyle style = (SwashbucklerStyle)qf.Owner.PersistentCharacterSheet.Calculated.AllFeats.Find(feat => feat.HasTrait(SwashStyle));
                bool flag = (result >= CheckResult.Success);
                bool flag2 = (action.ActionId == ActionId.TumbleThrough || style.PanacheTriggers.Contains(action.ActionId));
                bool flag3 = !qf.Owner.HasEffect(PanacheId);
                {
                    if (flag && flag2 && flag3)
                    {
                        qf.Owner.AddQEffect(CreatePanache(style.Skill));
                    }
                }
            }
        };
    }
  
    public static readonly Feat VivaciousSpeed = new Feat(ModManager.RegisterFeatName("Vivacious Speed"), "When you've made an impression, you move even faster than normal, darting about the battlefield with incredible speed.", "The status bonus to your Speed from panache increases to 10 feet. When you don't have panache, you still get half this status bonus to your Speeds, rounded down to the nearest 5-foot increment. This bonus increases by 5 feet at 7th level.", new List<Trait>(), null)
        .WithPermanentQEffect("You move quickly, even when you don't have panache.", delegate (QEffect qf)
        {
            qf.BonusToAllSpeeds = (QEffect qf) => (!qf.Owner.HasEffect(PanacheId)) ? new Bonus((((qf.Owner.Level - 3) / 4) + 2)/2, BonusType.Status, "Vivacious Speed") : null;
            qf.YouAcquireQEffect = delegate (QEffect qfThis, QEffect qfGet)
            {
                if (qfGet.Id == PanacheId)
                {
                    QEffect qfNew = qfGet;
                    int i = ((qf.Owner.Level - 3) / 4) + 2;
                    qfNew.BonusToAllSpeeds = delegate (QEffect qf)
                    {
                        return new Bonus(i, BonusType.Status, "Panache");
                    };
                    return qfNew;
                }
                else return qfGet;
            };
        });

    //Implemented as far as I'm aware, but the AI will never use the retort. Needs goodness.
    public static Feat BonMot = new TrueFeat(ModManager.RegisterFeatName("BonMot", "Bon Mot {icon:Action}"), 1, "You launch an insightful quip at a foe, distracting them.", "Using one action, choose a foe within 30 feet of you and make a Diplomacy check against their Will DC, with the following effects:" + S.FourDegreesOfSuccess("The target is distracted and takes a -3 status penalty to Perception and to Will saves for 1 minute. The target can end the effect early by using a single action to retort to your quip.", "As success, but the penalty is -2.", null, "Your quip is atrocious. You take the same penalty an enemy would take had you succeeded. This lasts for one minute or until you use another Bon Mot and succeed."), new Trait[6] { Trait.Auditory, Trait.Concentrate, Trait.Emotion, Trait.General, Trait.Linguistic, Trait.Mental }, null)
        .WithPermanentQEffect(null, delegate (QEffect qf)
        {
            qf.ProvideActionIntoPossibilitySection = (qfbonmot, section) =>
            {
                if (section.PossibilitySectionId == PossibilitySectionId.OtherManeuvers)
                {
                    return new ActionPossibility(new CombatAction(qf.Owner, IllustrationName.Confusion, "Bon Mot", new Trait[] { Trait.Auditory, Trait.Concentrate, Trait.Emotion, Trait.Linguistic, Trait.Mental }, "Use Diplomacy to distract a foe. choose a foe within 30 feet of you and make a Diplomacy check against their Will DC, with the following effects:" + S.FourDegreesOfSuccess("The target is distracted and takes a -3 status penalty to Perception and to Will saves for 1 minute. The target can end the effect early by using a single action to retort to your quip.", "As success, but the penalty is -2.", null, "Your quip is atrocious. You take the same penalty an enemy would take had you succeeded. This lasts for one minute or until you use another Bon Mot and succeed."), Target.Ranged(6)
                        .WithAdditionalConditionOnTargetCreature((Creature self, Creature target) => (target.QEffects.Any((QEffect effect) => effect.Name == "Bon Mot")) ? Usability.NotUsableOnThisCreature("This enemy is already distracted.") : Usability.Usable)
                        .WithAdditionalConditionOnTargetCreature((Creature self, Creature target) => target.DoesNotSpeakCommon ? Usability.NotUsableOnThisCreature("The creature cannot understand your words.") : Usability.Usable))
                        .WithActionCost(1)
                        .WithShortDescription("Use Diplomacy to distract a foe.")
                        .WithActionId(BonMotId)
                        .WithActiveRollSpecification(new ActiveRollSpecification(TaggedChecks.SkillCheck(Skill.Diplomacy), Checks.DefenseDC(Defense.Will)))
                        .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                        {
                            switch (result)
                            {
                                case CheckResult.CriticalSuccess:
                                    if (caster.QEffects.Any((QEffect effect) => effect.Name == "Bon Mot"))
                                    {
                                        caster.RemoveAllQEffects((QEffect effect) => effect.Name == "Bon Mot");
                                    }
                                    QEffect bonmotcrit = new QEffect().WithExpirationAtStartOfSourcesTurn(caster, 10);
                                    bonmotcrit.Name = "Bon Mot";
                                    bonmotcrit.Illustration = IllustrationName.Confused;
                                    bonmotcrit.Description = "You have a -3 status penalty to Perception and to Will saves.";
                                    bonmotcrit.BonusToDefenses = delegate (QEffect thing, CombatAction something, Defense defense)
                                    {
                                        if (defense == Defense.Will || defense == Defense.Perception)
                                        {
                                            return new Bonus(-3, BonusType.Status, "Bon Mot");
                                        }
                                        else return null;
                                    };
                                    bonmotcrit.ProvideMainAction = qftechnical =>
                                    {
                                        return new ActionPossibility(new CombatAction(bonmotcrit.Owner, IllustrationName.Rage, "Retort", new Trait[3] { Trait.Concentrate, Trait.Linguistic, Trait.Mental }, "You return the quip against you and attempt to remove the penalty from Bon Mot.", Target.Ranged(6)
                                            .WithAdditionalConditionOnTargetCreature((Creature self, Creature target) => target == qf.Owner ? Usability.Usable : Usability.NotUsableOnThisCreature("Not owner")))
                                            .WithActionCost(1)
                                            .WithActiveRollSpecification(new ActiveRollSpecification(TaggedChecks.SkillCheck(Skill.Diplomacy), Checks.DefenseDC(Defense.Will)))
                                            .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                                            {
                                                if (result >= CheckResult.Success)
                                                {
                                                    bonmotcrit.Owner.RemoveAllQEffects((QEffect qf) => qf == bonmotcrit);
                                                }
                                            }));
                                    };
                                    target.AddQEffect(bonmotcrit);
                                    break;
                                case CheckResult.Success:
                                    if (caster.QEffects.Any((QEffect effect) => effect.Name == "Bon Mot"))
                                    {
                                        caster.RemoveAllQEffects((QEffect effect) => effect.Name == "Bon Mot");
                                    }
                                    QEffect bonmotwin = new QEffect().WithExpirationAtStartOfSourcesTurn(caster, 10);
                                    bonmotwin.Name = "Bon Mot";
                                    bonmotwin.Illustration = IllustrationName.Confused;
                                    bonmotwin.Description = "You have a -2 status penalty to Perception and to Will saves.";
                                    bonmotwin.BonusToDefenses = delegate (QEffect thing, CombatAction something, Defense defense)
                                    {
                                        if (defense == Defense.Will || defense == Defense.Perception)
                                        {
                                            return new Bonus(-2, BonusType.Status, "Bon Mot");
                                        }
                                        else return null;
                                    };
                                    bonmotwin.ProvideMainAction = qftechnical =>
                                    {
                                        return new ActionPossibility(new CombatAction(bonmotwin.Owner, IllustrationName.Rage, "Retort", new Trait[3] { Trait.Concentrate, Trait.Linguistic, Trait.Mental }, "You return the quip against you and attempt to remove the penalty from Bon Mot.", Target.Ranged(6)
                                            .WithAdditionalConditionOnTargetCreature((Creature self, Creature target) => self == qf.Owner ? Usability.Usable : Usability.NotUsableOnThisCreature("Not owner")))
                                            .WithActionCost(1)
                                            .WithActiveRollSpecification(new ActiveRollSpecification(TaggedChecks.SkillCheck(Skill.Diplomacy), Checks.DefenseDC(Defense.Will)))
                                            .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                                            {
                                                if (result >= CheckResult.Success)
                                                {
                                                    bonmotwin.Owner.RemoveAllQEffects((QEffect qf) => qf == bonmotwin);
                                                }
                                            }));
                                    };
                                    target.AddQEffect(bonmotwin);
                                    break;
                                case CheckResult.CriticalFailure:
                                    QEffect bonmotfumble = new QEffect().WithExpirationAtStartOfSourcesTurn(caster, 10);
                                    bonmotfumble.Name = "Bon Mot";
                                    bonmotfumble.Illustration = IllustrationName.Confused;
                                    bonmotfumble.Description = "You have a -2 status penalty to Perception and to Will saves.";
                                    bonmotfumble.BonusToDefenses = delegate (QEffect thing, CombatAction something, Defense defense)
                                    {
                                        if (defense == Defense.Will || defense == Defense.Perception)
                                        {
                                            return new Bonus(-2, BonusType.Status, "Bon Mot");
                                        }
                                        else return null;
                                    };
                                    bonmotfumble.BonusToAttackRolls = delegate (QEffect effect, CombatAction action, Creature owner)
                                    {
                                        if (action.ActionId == ActionId.Seek)
                                        {
                                            return new Bonus(-2, BonusType.Status, "Bon Mot Crit Fail");
                                        }
                                        else return null;
                                    };
                                    caster.AddQEffect(bonmotfumble);
                                    break;
                            }
                        }));
                }
                else return null;
            };
        });

    public static Feat FascinatingPerformance = new TrueFeat(ModManager.RegisterFeatName("FascinatingPerformance", "Fascinating Performance"), 1, "You can Perform to fascinate observers.", "As an action, make a Performance check against an opponent's Will DC. If you critically succeed, the target is fascinated by you (they have a -2 status penalty to skill checks and can't take concentrate actions against anyone other than you) for 1 round. The target is then immune for the rest of the encounter.\n\nIf you are an expert in Performance, you can choose up to 4 targets. If you are a master in Performance, you can choose up to 10 targets.", new Trait[] { Trait.General, Trait.SkillFeat }, null)
        .WithPermanentQEffect(delegate (QEffect qf)
        {
            qf.ProvideActionIntoPossibilitySection = delegate (QEffect effect, PossibilitySection section)
            {
                if (section.PossibilitySectionId == PossibilitySectionId.SkillActions)
                {
                    return new ActionPossibility(new CombatAction(effect.Owner, IllustrationName.RoaringApplause, "Perform", new Trait[] { Trait.Concentrate, Trait.Manipulate, Trait.Incapacitation },
                        "Perform to fascinate observers.", (effect.Owner.PersistentCharacterSheet.Calculated.GetProficiency(Trait.Performance) >= Proficiency.Master) ? Target.MultipleCreatureTargets(Target.Ranged(50), Target.Ranged(50), Target.Ranged(50), Target.Ranged(50), Target.Ranged(50), Target.Ranged(50), Target.Ranged(50), Target.Ranged(50), Target.Ranged(50), Target.Ranged(50)).WithMinimumTargets(1).WithMustBeDistinct()
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
        })
        .WithPrerequisite(sheet => sheet.GetProficiency(Trait.Performance) >= Proficiency.Trained, "You must be trained in Performance.");

    //This one's a test, originally to learn how to add stuff and later to expedite testing with Panache. I think I'll leave it in just in case someone wants to poke around in the mod.
    public static Feat AddPanache = new TrueFeat(FeatName.CustomFeat, 1, "You give yourself panache as a test.", "Test to see if the feat and condition load.", new Trait[1] { SwashTrait }, null)
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

    //Technically implemented, but the AI won't use the recovery action. Needs a goodness.
    public static Feat DisarmingFlair = new TrueFeat(ModManager.RegisterFeatName("Disarming Flair", "Disarming Flair"), 1, "It's harder for foes to regain their grip when you knock their weapon partially out of their hands.", "When you succeed at an Athletics check to Disarm, the circumstance bonus and penalty from Disarm last until the end of your next turn, instead of until the beginning of the target's next turn. The target can use an Interact action to adjust their grip and remove this effect. If your swashbuckler style is gymnast and you succeed at your Athletics check to Disarm a foe, you gain panache.", new Trait[1] { SwashTrait }, null)
        .WithPermanentQEffect("Your Disarm attempts last longer, and you gain panache when you Disarm.", delegate (QEffect qf)
        {
            qf.AfterYouTakeAction = async delegate (QEffect effect, CombatAction disarm)
            {
                if (disarm.Name == "Disarm" && disarm.CheckResult == CheckResult.Success)
                {
                    if (qf.Owner.HasFeat(GymnastStyle))
                    {
                        qf.Owner.AddQEffect(CreatePanache(Skill.Athletics));
                    }
                    QEffect disarmed = disarm.ChosenTargets.ChosenCreature!.QEffects.Single((QEffect thing) => thing.Name == "Weakened grasp");
                    disarmed.ExpiresAt = ExpirationCondition.ExpiresAtEndOfSourcesTurn;
                    disarmed.CannotExpireThisTurn = true;
                    disarmed.ProvideMainAction = (qftechnical =>
                    {
                        return new ActionPossibility(new CombatAction(disarmed.Owner, IllustrationName.Fist, "Recover Grip", new Trait[2] { Trait.Interact, Trait.Manipulate }, "You adjust your grip on your weapon and remove the penalty from Disarm.", Target.Self())
                            .WithEffectOnEachTarget(async (caster, spell, target, result) =>
                            {
                                disarmed.Owner.RemoveAllQEffects((QEffect thing) => thing.Name == "Weakened grasp");
                            }));
                    });
                }
            };
        });

    public static void AddSwashDuelingParry()
    {
        TrueFeat trueFeat = AllFeats.All.First((Feat ft) => ft.FeatName == FeatName.DuelingParry) as TrueFeat;
        Feat newFeat = new TrueFeat(ModManager.RegisterFeatName(trueFeat.FeatName.ToString() + "Swash", trueFeat.Name), 1, trueFeat.FlavorText, trueFeat.RulesText, new Trait[] { SwashTrait }, null)
            .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
            {
                sheet.GrantFeat(trueFeat.FeatName);
            });
        ModManager.AddFeat(newFeat);
    }

    //Grants thrown versions of Confident Finisher, Unbalancing Finisher, Bleeding Finisher, and Stunning Finisher, as long as your weapons meet the criteria. It's usually down to GM judgement which finishers Flying Blade applies to anyway.
    public static Feat FlyingBlade = new TrueFeat(ModManager.RegisterFeatName("FlyingBlade", "Flying Blade"), 1, "You've learned to apply your flashy techniques to thrown weapons just as easily as melee.", "When you have panache, you apply your additional damage from Precise Strike on ranged Strikes you make with a thrown weapon within its first range increment. The thrown weapon must be an agile or finesse weapon.\n\nAdditionally, if you have the following finishers available to you, you can perform them with thrown weapons: Confident Finisher, Unbalancing Finisher, Bleeding Finisher, Stunning Finisher.", new Trait[1] { SwashTrait }, null)
        .WithPermanentQEffect(delegate (QEffect qf)
        {
            if (qf.Owner.HasFeat(Confident.FeatName))
            {
                qf.Owner.AddQEffect(new QEffect
                {
                    ProvideStrikeModifier = delegate (Item item)
                    {
                        StrikeModifiers strikeModifiers8 = new StrikeModifiers();
                        bool flag23 = (item.HasTrait(Trait.Thrown10Feet) || item.HasTrait(Trait.Thrown20Feet)) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse) || item.HasTrait(Trait.Unarmed));
                        bool flag24 = qf.Owner.HasEffect(PanacheId);
                        if (flag23 && flag24)
                        {
                            CombatAction confthrow = StrikeRules.CreateStrike(qf.Owner, item, RangeKind.Ranged, -1, thrown: true, strikeModifiers8);
                            confthrow.Name = "Confident Finisher (Thrown)";
                            confthrow.Illustration = new SideBySideIllustration(item.Illustration, IllustrationName.StarHit);
                            if (qf.Owner.Level < 5)
                            {
                                confthrow.Description = StrikeRules.CreateBasicStrikeDescription(confthrow.StrikeModifiers, null, null, null, "The target takes 2d6/2 damage.", "You lose panache, whether the attack succeeds or fails. The weapon falls in the target's square.");
                            }
                            else
                            {
                                confthrow.Description = StrikeRules.CreateBasicStrikeDescription(confthrow.StrikeModifiers, null, null, null, "The target takes 3d6/2 damage.", "You lose panache, whether the attack succeeds or fails. The weapon falls in the target's square.");
                            }

                            confthrow.ActionCost = 1;
                            confthrow.StrikeModifiers.OnEachTarget = async delegate (Creature owner, Creature victim, CheckResult result)
                            {
                                if (result == CheckResult.Failure)
                                {
                                    if (qf.Owner.Level < 5)
                                    {
                                        HalfDiceFormula halfdamage2 = new HalfDiceFormula(DiceFormula.FromText("2d6", "Precise Strike"), "Miss with Confident Finisher");
                                        await CommonSpellEffects.DealDirectDamage(confthrow, halfdamage2, victim, confthrow.CheckResult, confthrow.StrikeModifiers.CalculatedItem.WeaponProperties.DamageKind);
                                    }
                                    else
                                    {
                                        HalfDiceFormula halfdamage = new HalfDiceFormula(DiceFormula.FromText("3d6", "Precise Strike"), "Miss with Confident Finisher");
                                        await CommonSpellEffects.DealDirectDamage(confthrow, halfdamage, victim, confthrow.CheckResult, confthrow.StrikeModifiers.CalculatedItem.WeaponProperties.DamageKind);
                                    }
                                }
                                FinisherExhaustion(confthrow.Owner);
                            };
                            confthrow.Traits.Add(Finisher);
                            return confthrow;
                        }

                        return null;
                    }
                });
            }

            if (qf.Owner.HasFeat(UnbalancingFinisher.FeatName))
            {
                qf.Owner.AddQEffect(new QEffect
                {
                    ProvideStrikeModifier = delegate (Item item)
                    {
                        StrikeModifiers strikeModifiers7 = new StrikeModifiers();
                        bool flag21 = (item.HasTrait(Trait.Thrown10Feet) || item.HasTrait(Trait.Thrown20Feet)) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse));
                        bool flag22 = qf.Owner.HasEffect(PanacheId);
                        if (flag21 && flag22)
                        {
                            CombatAction combatAction4 = StrikeRules.CreateStrike(qf.Owner, item, RangeKind.Ranged, -1, thrown: true, strikeModifiers7);
                            combatAction4.Name = "Unbalancing Finisher (Thrown)";
                            combatAction4.ActionId = UnbalancingFinisherId;
                            combatAction4.Illustration = new SideBySideIllustration(item.Illustration, IllustrationName.Trip);
                            combatAction4.Description = StrikeRules.CreateBasicStrikeDescription2(combatAction4.StrikeModifiers, null, "The target is flat-footed until the end of your next turn.", null, null, "You lose panache, whether the attack succeeds or fails. The weapon falls in the target's square.");
                            combatAction4.ActionCost = 1;
                            combatAction4.Traits.Add(Finisher);
                            return combatAction4;
                        }

                        return null;
                    }
                });
            }

            if (qf.Owner.HasFeat(BleedingFinisher.FeatName))
            {
                qf.Owner.AddQEffect(new QEffect
                {
                    ProvideStrikeModifier = delegate (Item item)
                    {
                        StrikeModifiers strikeModifiers6 = new StrikeModifiers();
                        bool flag18 = (item.HasTrait(Trait.Thrown10Feet) || item.HasTrait(Trait.Thrown20Feet)) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse));
                        bool flag19 = qf.Owner.HasEffect(PanacheId);
                        bool flag20 = item.WeaponProperties.DamageKind == DamageKind.Piercing || item.WeaponProperties.DamageKind == DamageKind.Slashing;
                        if (flag18 && flag19 && flag20)
                        {
                            CombatAction combatAction3 = StrikeRules.CreateStrike(qf.Owner, item, RangeKind.Ranged, -1, thrown: true, strikeModifiers6);
                            combatAction3.Name = "Bleeding Finisher (Thrown)";
                            combatAction3.Illustration = new SideBySideIllustration(item.Illustration, IllustrationName.BloodVendetta);
                            combatAction3.Description = StrikeRules.CreateBasicStrikeDescription2(combatAction3.StrikeModifiers, null, "The target takes 3d6 persistent bleed damage.", null, null, "You lose panache, whether the attack succeeds or fails. The weapon falls in the target's square.");
                            combatAction3.ActionCost = 1;
                            combatAction3.StrikeModifiers.OnEachTarget = async delegate (Creature owner, Creature victim, CheckResult result)
                            {
                                if (result >= CheckResult.Success)
                                {
                                    victim.AddQEffect(QEffect.PersistentDamage("3d6", DamageKind.Bleed));
                                }
                                FinisherExhaustion(qf.Owner);
                            };
                            combatAction3.Traits.Add(Finisher);
                            return combatAction3;
                        }

                        return null;
                    }
                });
            }

            if (qf.Owner.HasFeat(StunningFinisher.FeatName))
            {
                qf.Owner.AddQEffect(new QEffect
                {
                    ProvideStrikeModifier = delegate (Item item)
                    {
                        StrikeModifiers strikeModifiers5 = new StrikeModifiers();
                        bool flag16 = (item.HasTrait(Trait.Thrown10Feet) || item.HasTrait(Trait.Thrown20Feet)) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse));
                        bool flag17 = qf.Owner.HasEffect(PanacheId);
                        if (flag16 && flag17)
                        {
                            CombatAction stun2 = StrikeRules.CreateStrike(qf.Owner, item, RangeKind.Ranged, -1, thrown: true, strikeModifiers5);
                            stun2.Name = "Stunning Finisher (Thrown)";
                            stun2.Illustration = new SideBySideIllustration(item.Illustration, IllustrationName.Stunned);
                            stun2.Description = StrikeRules.CreateBasicStrikeDescription2(stun2.StrikeModifiers, null, "The target makes a DC " + stun2.Owner.ClassOrSpellDC() + " Fortitude save (this is an incapacitation effect). On a success, it can't take reactions for 1 turn. On a failure, it is stunned 1, and on a critical failure, it is stunned 3.", null, null, "You lose panache, whether the attack succeeds or fails. The weapon falls in the target's square.");
                            stun2.ActionCost = 1;
                            stun2.Traits.Add(Finisher);
                            stun2.StrikeModifiers.OnEachTarget = async delegate (Creature owner, Creature victim, CheckResult result)
                            {
                                FinisherExhaustion(owner);
                                if (result >= CheckResult.Success)
                                {
                                    CombatAction theactualstun2 = CombatAction.CreateSimpleIncapacitation(stun2.Owner, "Stunning Finisher", stun2.Owner.MaximumSpellRank);
                                    switch (CommonSpellEffects.RollSavingThrow(victim, theactualstun2, Defense.Fortitude, stun2.Owner.ClassOrSpellDC()))
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
                            return stun2;
                        }

                        return null;
                    }
                });
            }
        })
        .WithPrerequisite(sheet => sheet.AllFeats.Contains(PreciseStrike), "You must have the Precise Strike feature.");

    public static void ReplaceYoureNext()
    {
        TrueFeat trueFeat = AllFeats.All.First((Feat ft) => ft.FeatName == FeatName.YoureNext) as TrueFeat;
        trueFeat.WithAllowsForAdditionalClassTrait(SwashTrait);
    }

    public static void ReplaceNimbleDodge()
    {
        TrueFeat trueFeat = AllFeats.All.First((Feat ft) => ft.FeatName == FeatName.NimbleDodge) as TrueFeat;
        trueFeat.WithAllowsForAdditionalClassTrait(SwashTrait);
    }

    public static Feat FocusedFascination = new TrueFeat(ModManager.RegisterFeatName("FocusedFascination", "Focused Fascination"), 1, "Your performance can draw a foe's attention even in the districting din of combat.", "When you use Fascinating Performance, you only need a success, rather than a critical success, to fascinate your target. This works only if you are attempting to fascinate just one target.", new Trait[] { SwashTrait }, null)
        .WithPrerequisite(FascinatingPerformance.FeatName, "Fascinating Performance")
        .WithPermanentQEffect(null, delegate (QEffect qf)
        {
            qf.AfterYouTakeActionAgainstTarget = async (fct, action, target, result) =>
            {
                if ((action.ActionId == FascinatingPerformanceActionId) && (action.ChosenTargets.ChosenCreatures.Count == 1) && (result == CheckResult.Success))
                {
                    target.AddQEffect(CreateFascinated(fct.Owner));
                }
            };
        });

    public static Feat GoadingFeint = new TrueFeat(ModManager.RegisterFeatName("Goading Feint"), 1, "When you trick a foe, you can goad them into overextending their next attack.", "On a Feint, you can use the following success and critical success effects instead of any other effects you would gain when you Feint; if you do, normal abilities that apply on a Feint no longer apply.\n\n{b}Critical Success:{/b} The target takes a -2 circumstance penalty to all its attack rolls against you before the end of its next turn.\n{b}Success:{/b} The target takes a -2 circumstance penalty to the next attack roll it makes against you before the end of its next turn.", new Trait[1] { SwashTrait }, null)
        .WithPermanentQEffect("When you Feint a creature, you can give them a penalty to AC instead of the normal effects.", delegate (QEffect qf)
        {
            QEffect goaded = new QEffect()
            {
                Name = "Goaded",
                Illustration = IllustrationName.Confused,
                Description = "You are goaded and have a -2 circumstance penalty to your next attack roll against the goading creature before the end of your turn.",
                ExpiresAt = ExpirationCondition.ExpiresAtEndOfYourTurn,
                BonusToAttackRolls = delegate (QEffect bonus, CombatAction bonk, Creature? someone)
                {
                    if (someone == qf.Owner)
                    {
                        return new Bonus(-2, BonusType.Circumstance, "Goaded");
                    }
                    else return null;
                },
                AfterYouMakeAttackRoll = delegate (QEffect goaded, CheckBreakdownResult result)
                {
                    goaded.Owner.RemoveAllQEffects((QEffect provoked) => provoked.Name == "Goaded");
                }
            };
            QEffect bettergoaded = new QEffect()
            {
                Name = "Goaded",
                Illustration = IllustrationName.Confused,
                Description = "You are goaded and have a -2 circumstance penalty to attack rolls against the goading creature until the end of your turn.",
                ExpiresAt = ExpirationCondition.ExpiresAtEndOfYourTurn,
                BonusToAttackRolls = delegate (QEffect bonus, CombatAction bonk, Creature? someone)
                {
                    if (someone == qf.Owner)
                    {
                        return new Bonus(-2, BonusType.Circumstance, "Goaded");
                    }
                    else return null;
                }
            };
            qf.AfterYouTakeAction = async delegate (QEffect qfaction, CombatAction action)
            {
                bool flag = action.ActionId == ActionId.Feint;
                bool flag2 = flag;
                bool flag3 = action.CheckResult >= CheckResult.Success;
                if (flag2 && flag3)
                {
                    flag2 = await qf.Owner.Battle.AskForConfirmation(qf.Owner, IllustrationName.Action, "Would you like to goad the target?", "Goad");

                    if (flag2 && action.CheckResult == CheckResult.Success)
                    {
                        action.ChosenTargets.ChosenCreature!.AddQEffect(goaded);
                        action.ChosenTargets.ChosenCreature.RemoveAllQEffects((QEffect thing) => (thing.Name == "Flat-footed in melee") || (thing.Name == "Flat-footed to " + action.Owner.Name));
                    }
                    else if (flag2 && action.CheckResult == CheckResult.CriticalSuccess)
                    {
                        action.ChosenTargets.ChosenCreature!.AddQEffect(bettergoaded);
                        action.ChosenTargets.ChosenCreature.RemoveAllQEffects((QEffect thing) => (thing.Name == "Flat-footed in melee") || (thing.Name == "Flat-footed to " + action.Owner.Name));
                    }
                }
            };
        })
        .WithPrerequisite((CalculatedCharacterSheetValues values) => values.GetProficiency(Trait.Deception) >= Proficiency.Trained, "You must be trained in Deception");

    public static Feat OneForAll = new TrueFeat(ModManager.RegisterFeatName("One For All", "One For All {icon:Action}"), 1, "With precisely the right words of encouragement, you bolster an ally's efforts.", "Using one action, designate an ally within 30 feet. The next time that ally makes an attack roll or skill check, you may use your reaction to attempt a DC 20 Diplomacy check with the following effects:\n{b}Critical Success:{/b} You grant the ally a +2 circumstance bonus to their attack roll or skill check. If your swashbuckler style is Wit, you gain panache.\n{b}Success:{/b} You grant the ally a +1 circumstance bonus to their attack roll or skill check. If your swashbuckler style is Wit, you gain panache.\n{b}Critical Failure:{/b} The ally takes a -1 circumstance penalty to their attack roll or skill check.", new Trait[6] { Trait.Auditory, Trait.Concentrate, Trait.Emotion, Trait.Linguistic, Trait.Mental, SwashTrait })
        .WithPermanentQEffect(null, delegate (QEffect qf)
        {
            qf.ProvideActionIntoPossibilitySection = (qfoneforall, section) =>
            {
                if (section.PossibilitySectionId == PossibilitySectionId.SkillActions)
                {
                    bool aidPrepareIdExists = ModManager.TryParse<ActionId>("PrepareToAid", out ActionId aidPrepareId);
                    return new ActionPossibility(new CombatAction(qf.Owner, IllustrationName.SoundBurst, "One For All", new Trait[5] { Trait.Auditory, Trait.Concentrate, Trait.Emotion, Trait.Linguistic, Trait.Mental }, "Attempt to assist an ally's next skill check or attack roll.", Target.RangedFriend(6)
                    .WithAdditionalConditionOnTargetCreature((Creature self, Creature target) => (target.QEffects.Any((QEffect effect) => effect.Name == "Aided by " + qf.Owner.Name)) ? Usability.NotUsableOnThisCreature("You are already aiding this ally.") : Usability.Usable)
                    .WithAdditionalConditionOnTargetCreature((Creature self, Creature target) => (target == self) ? Usability.NotUsableOnThisCreature("You can't Aid yourself.") : Usability.Usable))
                    .WithActionCost(1)
                    .WithActionId(aidPrepareId)
                    .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                    {
                        QEffect aided = new QEffect();
                        aided.Name = "Aided by " + qf.Owner.Name;
                        aided.Illustration = IllustrationName.Guidance;
                        aided.Description = qf.Owner.Name + " may attempt to assist you and provide a bonus to your next attack roll or skill check.";
                        aided.BeforeYourActiveRoll = async delegate (QEffect effect, CombatAction action, Creature self)
                        {
                            bool aidIdExists = ModManager.TryParse<ActionId>("AidReaction", out ActionId aidId);
                            CombatAction aid = new CombatAction(qf.Owner, IllustrationName.SoundBurst, "Aid", new Trait[0], "Attempt to Aid an ally.", Target.Self())
                                .WithActionCost(0)
                                .WithActionId(aidId)
                                .WithActiveRollSpecification(new ActiveRollSpecification(TaggedChecks.SkillCheck(Skill.Diplomacy), Checks.FlatDC(20)))
                                .WithEffectOnSelf(async (spell, self) =>
                                {
                                    if (self.HasFeat(WitStyle) && spell.CheckResult >= CheckResult.Success)
                                    {
                                        self.AddQEffect(CreatePanache(Skill.Diplomacy));
                                    }
                                })
                                .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                                {
                                    switch (result)
                                    {
                                        case CheckResult.CriticalSuccess:
                                            aided.Owner.AddQEffect(new QEffect()
                                            {
                                                BonusToAllChecksAndDCs = delegate (QEffect thing)
                                                {
                                                    return new Bonus(2, BonusType.Circumstance, "One For All");
                                                },
                                                ExpiresAt = ExpirationCondition.Ephemeral
                                            });
                                            break;
                                        case CheckResult.Success:
                                            aided.Owner.AddQEffect(new QEffect()
                                            {
                                                BonusToAllChecksAndDCs = delegate (QEffect thing)
                                                {
                                                    return new Bonus(1, BonusType.Circumstance, "One For All");
                                                },
                                                ExpiresAt = ExpirationCondition.Ephemeral
                                            });
                                            break;
                                        case CheckResult.CriticalFailure:
                                            aided.Owner.AddQEffect(new QEffect()
                                            {
                                                BonusToAllChecksAndDCs = delegate (QEffect thing)
                                                {
                                                    return new Bonus(-1, BonusType.Circumstance, "One For All");
                                                },
                                                ExpiresAt = ExpirationCondition.Ephemeral
                                            });
                                            break;
                                    }
                                    aided.Owner.RemoveAllQEffects((QEffect thing) => thing.Name == "Aided by " + qf.Owner.Name);
                                });
                            if (await qf.Owner.Battle.AskToUseReaction(qf.Owner, "Would you like to roll a Diplomacy check to Aid your ally's check?"))
                            {
                                await qf.Owner.Battle.GameLoop.FullCast(aid);
                            }
                        };
                        target.AddQEffect(aided);
                    }));
                }
                else return null;
            };
        });

    public static Feat AfterYou = new TrueFeat(ModManager.RegisterFeatName("After You"), 2, "You allow your foes to make the first move in a show of incredible confidence.", "When a battle begins, instead of rolling initiative, you may voluntarily go last. When you do so, you gain panache.", new Trait[1] { SwashTrait }, null)
        .WithPermanentQEffect("You can let your enemies go first to gain panache.", delegate (QEffect qf)
        {
            qf.StartOfCombat = async delegate (QEffect afteryou)
            {
                if (await qf.Owner.Battle.AskForConfirmation(qf.Owner, new ModdedIllustration("PhoenixAssets/Phoenix_Swashbuckler_Icon.PNG"), "Would you like to move last in initiative and gain panache?", "Yes, move last", "No, roll initiative normally"))
                {
                    Creature target = qf.Owner.Battle.InitiativeOrder.Last();
                    int goal = target.Battle.InitiativeOrder.IndexOf(target);
                    qf.Owner.Battle.MoveInInitiativeOrder(qf.Owner, goal + 1);
                    SwashbucklerStyle style = (SwashbucklerStyle)qf.Owner.PersistentCharacterSheet.Calculated.AllFeats.Find(feat => feat.HasTrait(SwashStyle));
                    qf.Owner.AddQEffect(CreatePanache(style.Skill));
                }
            };
        });

    public static Feat Antagonize = new TrueFeat(ModManager.RegisterFeatName("Antagonize"), 2, "Your taunts and threats earn your foes' ire.", "When you Demoralize a foe, its frightened condition can't decrease below 1 until it takes a hostile action against you or it cannot see you.", new Trait[1] { SwashTrait })
        .WithPermanentQEffect("Enemies can't recover from your Demoralize actions without taking hostile actions against you.", delegate (QEffect qf)
        {
            qf.AfterYouTakeAction = async delegate (QEffect fear, CombatAction demoralize)
            {
                if (demoralize.ActionId == ActionId.Demoralize && demoralize.CheckResult >= CheckResult.Success)
                {
                    QEffect antagonized = new QEffect()
                    {
                        Name = "Antagonized",
                        Id = QEffectId.DirgeOfDoomFrightenedSustainer,
                        Illustration = IllustrationName.Rage,
                        Description = "This target cannot lower their Frightened value below 1 until taking a hostile action against " + qf.Owner.Name + ".",
                        ExpiresAt = ExpirationCondition.Never,
                        EndOfYourTurn = async delegate (QEffect fright, Creature victim)
                        {
                            if (qf.Owner.DetectionStatus.Undetected)
                            {
                                fright.ExpiresAt = ExpirationCondition.Immediately;
                            }
                        },
                        AfterYouTakeHostileAction = async delegate (QEffect effect, CombatAction action)
                        {
                            if (action.ChosenTargets.GetAllTargetCreatures().Any(creature => creature == qf.Owner))
                            {
                                effect.ExpiresAt = ExpirationCondition.Immediately;
                            }
                        },
                        StateCheck = async delegate (QEffect effect)
                        {
                            if (!effect.Owner.HasEffect(QEffectId.Frightened))
                            {
                                effect.ExpiresAt = ExpirationCondition.Immediately;
                            }
                        }
                    };
                    demoralize.ChosenTargets.ChosenCreature!.AddQEffect(antagonized);
                }
            };
        });

    public static Feat UnbalancingFinisher = new TrueFeat(ModManager.RegisterFeatName("Unbalancing Finisher", "Unbalancing Finisher {icon:Action}"), 2, "You attack with a flashy assault that leaves your target off balance.", "Make a melee Strike. If you hit and deal damage, your target is flat-footed until the end of your next turn.", new Trait[2] { SwashTrait, Finisher })
        .WithPermanentQEffect(null, delegate (QEffect qf)
        {
            qf.AfterYouDealDamage = async delegate (Creature self, CombatAction action, Creature target)
            {
                if (action.ActionId == UnbalancingFinisherId)
                {
                    target.AddQEffect(new QEffect()
                    {
                        Name = "Unbalanced",
                        Description = "You are flat-footed until the end of " + target.Name + "'s next turn.",
                        Illustration = IllustrationName.Flatfooted,
                        IsFlatFootedTo = (QEffect all, Creature? everyone, CombatAction? everything) => "Unbalancing Finisher",
                        ExpiresAt = ExpirationCondition.ExpiresAtEndOfSourcesTurn,
                        Source = qf.Owner,
                        CannotExpireThisTurn = true
                    });
                }
            };
            qf.AfterYouTakeAction = async delegate (QEffect effect, CombatAction action)
            {
                if (action.ActionId == UnbalancingFinisherId)
                {
                    FinisherExhaustion(action.Owner);
                }
            };
            qf.ProvideStrikeModifier = delegate (Item item)
            {
                StrikeModifiers unbalancing = new StrikeModifiers();
                bool flag = item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse) || item.HasTrait(Trait.Unarmed);
                bool flag2 = qf.Owner.HasEffect(PanacheId);
                if (flag && flag2)
                {
                    CombatAction unbal = qf.Owner.CreateStrike(item, -1, unbalancing);
                    unbal.Name = "Unbalancing Finisher";
                    unbal.ActionId = UnbalancingFinisherId;
                    unbal.Illustration = new SideBySideIllustration(item.Illustration, IllustrationName.Trip);
                    unbal.Description = StrikeRules.CreateBasicStrikeDescription2(unbal.StrikeModifiers, null, "The target is flat-footed until the end of your next turn.", null, null, "You lose panache, whether the attack succeeds or fails.");
                    unbal.ActionCost = 1;
                    unbal.Traits.Add(Finisher);
                    return unbal;
                }
                else return null;
            };
        });

    public static Feat FinishingFollowThrough = new TrueFeat(ModManager.RegisterFeatName("Finishing Follow-Through", "Finishing Follow-Through"), 2, "Finishing a foe maintains your swagger.", "You gain Panache if your finisher reduces an enemy to 0 HP.", new Trait[1] { SwashTrait }, null)
        .WithPermanentQEffect("You gain panache when your finisher defeats an enemy.", delegate (QEffect qf)
        {
            qf.AfterYouDealDamage = async delegate (Creature you, CombatAction action, Creature target)
            {
                if (target.HP <= 0 && action.HasTrait(Finisher))
                {
                    SwashbucklerStyle style = (SwashbucklerStyle)qf.Owner.PersistentCharacterSheet.Calculated.AllFeats.Find(feat => feat.HasTrait(SwashStyle));
                    qf.Owner.AddQEffect(CreatePanache(style.Skill).WithCannotExpireThisTurn());
                }
            };
        });

    public static Feat CharmedLife = new TrueFeat(ModManager.RegisterFeatName("Charmed Life", "Charmed Life {icon:Reaction}"), 2, "When danger calls, you have a strange knack for coming out on top.", "Before you make a saving throw, you can spend your reaction to gain a +2 circumstance bonus to the roll.", new Trait[] { SwashTrait })
        .WithPrerequisite(sheet => (sheet.FinalAbilityScores.TotalScore(Ability.Charisma) >= 14), "Charisma 14")
        .WithPermanentQEffect("You can add a +2 circumstance bonus to a saving throw using a reaction.", delegate (QEffect qf)
        {
            qf.BeforeYourSavingThrow = async delegate (QEffect charm, CombatAction action, Creature self)
            {
                if (await self.Battle.AskToUseReaction(self, "You are about to make a saving throw. Would you like to use a reaction to gain a +2 circumstance bonus?"))
                {
                    self.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                    {
                        BonusToDefenses = (QEffect effect, CombatAction? something, Defense save) => new Bonus(2, BonusType.Circumstance, "Charmed Life")
                    });
                }
            };
        });

    public static Feat TumbleBehind = new TrueFeat(ModManager.RegisterFeatName("Tumble Behind", "Tumble Behind"), 2 ,"Your tumbling catches enemies off-guard.", "Whenever you Tumble Through an enemy, the enemy you Tumbled through is flat-footed against the next attack you make until the end of your turn.\n\n{i}(The automatic pathfinding will normally chart a path that doesn't require a tumble through if possible. To tumble through a creature on purpose, use the step-by-step stride option in the Other actions menu.){/i}", new Trait[] { SwashTrait }, null)
        .WithPermanentQEffect("Tumbling Through enemies makes them briefly flat-footed.", delegate (QEffect qf)
        {
            qf.AfterYouTakeAction = async delegate (QEffect effect, CombatAction action)
            {
                if (action.ActionId == ActionId.TumbleThrough && action.CheckResult >= CheckResult.Success && action.ChosenTargets.ChosenCreature != null)
                {
                    action.ChosenTargets.ChosenCreature.AddQEffect(new QEffect
                    {
                        Name = "Tumbled Behind",
                        Illustration = IllustrationName.Flatfooted,
                        Description = "You're flat-footed to the next attack that " + qf.Owner.Name + " makes before the end of the turn.",
                        IsFlatFootedTo = (QEffect all, Creature? everyone, CombatAction? everything) => "Tumble Behind",
                        ExpiresAt = ExpirationCondition.ExpiresAtEndOfSourcesTurn,
                        Source = qf.Owner,
                        AfterYouAreTargeted = async delegate (QEffect effect, CombatAction strike)
                        {
                            if (strike.HasTrait(Trait.Strike))
                            {
                                effect.ExpiresAt = ExpirationCondition.Immediately;
                            }
                        }
                    });
                }
            };
        });

    public static Feat DramaticCatch = new TrueFeat(ModManager.RegisterFeatName("DramaticCatch", "Dramatic Catch {icon:Reaction}"), 4, "You catch your wounded ally as they fall, prompting them to stay on their feet.", "When an ally adjacent to you takes damage that would reduce them to 0 Hit Points, if you have panache, you can use your reaction to catch them. When you do so, you lose panache, but the triggering ally remains at 1 Hit Point, and their wounded value increases by 1.\nYou can't use this ability if you don't have a free hand, or if you've already used Dramatic Catch on the same ally before taking a long rest.", new Trait[2]
        {
            SwashTrait,
            Trait.Homebrew
        }).WithPermanentQEffect("You can save an ally about to fall.", delegate (QEffect qf)
        {
            qf.StateCheck = async delegate
            {
                foreach (Creature ally2 in qf.Owner.Battle.AllCreatures.Where((Creature friend) => friend.DistanceTo(qf.Owner) == 1 && friend.FriendOf(qf.Owner)))
                {
                    ally2.AddQEffect(new QEffect
                    {
                        YouAreDealtLethalDamage = async delegate (QEffect effect, Creature attacker, DamageStuff stuff, Creature defender)
                        {
                            if (qf.Owner.HasEffect(PanacheId) && qf.Owner.HasFreeHand && !defender.PersistentUsedUpResources.UsedUpActions.Contains("Dramatic Catch") && defender.HP > 0)
                            {
                                if (await qf.Owner.Battle.AskToUseReaction(qf.Owner, defender.Name + " is about to take potentially lethal damage! Would you like to spend your panache to keep them standing at 1 Hit Point?"))
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

    public static Feat GuardiansDeflection = new TrueFeat(ModManager.RegisterFeatName("Guardian's Deflection", "Guardian's Deflection {icon:Reaction}"), 4, "You use your weapon to deflect an attack made against an ally.", "{b}Trigger:{/b} An ally within your melee reach is hit by an attack, you can see the attacker, and a +2 circumstance bonus to AC would turn the critical hit into a hit or the hit into a miss.\n\n{b}Requirements: {/b} You are wielding a single one-handed weapon and have your other hand free.\n\n You use your weapon to deflect the attack against your ally, granting them a +2 circumstance bonus against the triggering attack. This turns the triggering critical hit into a hit, or the triggering hit into a miss.", new Trait[] { SwashTrait }, null)
        .WithPermanentQEffect(delegate (QEffect qf)
        {
            qf.StateCheck = delegate (QEffect deflect)
            {
                foreach (Creature ally in qf.Owner.Battle.AllCreatures.Where((Creature friend) => (friend.DistanceTo(qf.Owner) == 1 && friend.FriendOf(qf.Owner))))
                {
                    ally.AddQEffect(new QEffect()
                    {
                        YouAreTargetedByARoll = async delegate (QEffect deflection, CombatAction attack, CheckBreakdownResult breakdownresult)
                        {
                            if ((qf.Owner.HasOneWeaponAndFist && qf.Owner.PrimaryWeapon != null && qf.Owner.PrimaryWeapon.HasTrait(Trait.Melee)) && (attack.HasTrait(Trait.Attack) && !attack.HasTrait(Trait.AttackDoesNotTargetAC)) && qf.Owner.CanSee(attack.Owner) && breakdownresult.ThresholdToDowngrade <= 2 && (breakdownresult.CheckResult == CheckResult.Success || breakdownresult.CheckResult == CheckResult.CriticalSuccess))
                            {
                                CheckResult result = breakdownresult.CheckResult;
                                if (await qf.Owner.Battle.AskToUseReaction(qf.Owner, ally.Name + " is about to be hit by an attack. Would you like to use Guardian's Deflection to downgrade the " + result.HumanizeTitleCase2() + " to a " + result.WorsenByOneStep().HumanizeTitleCase2() + "?"))
                                {
                                    ally.AddQEffect(new QEffect()
                                    {
                                        ExpiresAt = ExpirationCondition.EphemeralAtEndOfImmediateAction,
                                        BonusToDefenses = (QEffect effect, CombatAction? action, Defense defense) => (defense != 0) ? null : new Bonus(2, BonusType.Circumstance, "Guardian's Deflection")
                                    });
                                    return true;
                                }
                            }
                            return false;
                        },
                        ExpiresAt = ExpirationCondition.Ephemeral
                    });
                }
            };
        });

    public static void GiveGuardiansDeflectionToFighters()
    {
        TrueFeat trueFeat = AllFeats.All.First((Feat ft) => ft.FeatName == GuardiansDeflection.FeatName) as TrueFeat;
        Feat newFeat = new TrueFeat(ModManager.RegisterFeatName(trueFeat.FeatName.ToString() + "Fighter", trueFeat.Name), 6, trueFeat.FlavorText, trueFeat.RulesText, new Trait[] { Trait.Fighter }, null)
            .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
            {
                sheet.GrantFeat(trueFeat.FeatName);
            });
        ModManager.AddFeat(newFeat);
    }

    public static Feat ImpalingFinisher = new TrueFeat(ModManager.RegisterFeatName("Impaling Finisher", "Impaling Finisher {icon:Action}"), 4, "You stab two foes with one thrust or bash them together with one punch.", "Make a bludgeoning or piercing melee Strike, then make an additional Strike against a creature directly behind them in a straight line.", new Trait[2] { SwashTrait, Finisher }, null)
        .WithPermanentQEffect(delegate (QEffect qf)
        {
            qf.ProvideStrikeModifier = delegate (Item item)
            {
                StrikeModifiers imp = new StrikeModifiers();
                bool flag = !item.HasTrait(Trait.Ranged) && (item.WeaponProperties!.DamageKind == DamageKind.Bludgeoning || item.WeaponProperties.DamageKind == DamageKind.Piercing);
                bool flag2 = qf.Owner.HasEffect(PanacheId);
                if (flag && flag2)
                {
                    int map = qf.Owner.Actions.AttackedThisManyTimesThisTurn;
                    return new CombatAction(qf.Owner, new SideBySideIllustration(item.Illustration, item.Illustration), "Impaling Finisher", new Trait[] { Trait.Attack, Trait.AttackDoesNotIncreaseMultipleAttackPenalty, Finisher }, "Make a bludgeoning or piercing attack against an adjacent enemy, then an enemy directly behind them in a straight line.", Target.Melee())
                    .WithActionCost(1)
                    .WithEffectOnChosenTargets(async delegate (Creature swash, ChosenTargets target)
                    {
                        int xtranslate = swash.Occupies.X - target.ChosenCreature!.Occupies.X;
                        int ytranslate = swash.Occupies.Y - target.ChosenCreature!.Occupies.Y;
                        Tile? target2 = swash.Battle.Map.GetTile(target.ChosenCreature.Occupies.X - xtranslate, target.ChosenCreature.Occupies.Y - ytranslate);
                        CombatAction impale = swash.CreateStrike(item, map).WithActionCost(0);
                        impale.Traits.Add(Finisher);
                        impale.ChosenTargets = ChosenTargets.CreateSingleTarget(target.ChosenCreature);
                        if (target2 != null && target2.PrimaryOccupant != null && target2.PrimaryOccupant.EnemyOf(swash))
                        {
                            CombatAction impale2 = swash.CreateStrike(item, map).WithActionCost(0);
                            impale2.Traits.Add(Finisher);
                            impale2.Target = Target.Distance(4);
                            impale2.ChosenTargets = ChosenTargets.CreateSingleTarget(target2.PrimaryOccupant);
                            await impale.AllExecute();
                            await impale2.AllExecute();
                            FinisherExhaustion(swash);
                        }
                        else if (await swash.Battle.AskForConfirmation(swash, IllustrationName.ExclamationMark, "There is not an eligible creature in a straight line. Do you still wish to use Impaling Finisher?", "Use Impaling Finisher", "Cancel"))
                        {
                            await impale.AllExecute();
                        }
                        else swash.Actions.ActionsLeft++;
                    });
                }
                else return null;
            };
        });

    public static Feat LeadingDance = new TrueFeat(ModManager.RegisterFeatName("LeadingDance", "Leading Dance {icon:Action}"), 4, "You sweep your foe into your dance.", "Attempt a Performance check against an adjacent enemy's Will DC. If you have the Battledancer swashbuckler style and you succeed, you gain panache." + S.FourDegreesOfSuccess("Your foe is swept up in your dance. You move up to 10 feet, and the enemy follows you. Your movement doesn't trigger reactions (and the enemy's movement doesn't trigger reactions because it's forced movement).", "As critical success, but you both only move 5 feet.", "The foe doesn't follow your steps. You can move 5 feet if you choose, but this movement triggers reactions normally.", "You stumble, falling prone in your space."), new Trait[] { SwashTrait, Trait.Move }, null)
        .WithPermanentQEffect(delegate (QEffect qf)
        {
            qf.ProvideActionIntoPossibilitySection = delegate (QEffect effect, PossibilitySection section)
            {
                if (section.PossibilitySectionId == PossibilitySectionId.SkillActions)
                {
                    return new ActionPossibility(new CombatAction(effect.Owner, IllustrationName.WarpStep, "Leading Dance", new Trait[] { Trait.Move }, "Attempt a Performance check against an adjacent enemy's Will DC. If you have the Battledancer swashbuckler style and you succeed, you gain panache." + S.FourDegreesOfSuccess("Your foe is swept up in your dance. You move up to 10 feet, and the enemy follows you. Your movement doesn't trigger reactions (and the enemy's movement doesn't trigger reactions because it's forced movement).", "As critical success, but you both only move 5 feet.", "The foe doesn't follow your steps. You can move 5 feet if you choose, but this movement triggers reactions normally.", "You stumble, falling prone in your space."),
                            Target.Touch())
                        .WithActionCost(1)
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
                                    await caster.StrideAsync("Choose a location to move to.", false, true, null, false, true, false);
                                    await caster.StrideAsync("Choose a location to move to.", false, true, null, false, true, false);
                                    await caster.PullCreature(target);
                                    caster.RemoveAllQEffects((QEffect qf) => qf == noReactions);
                                    break;
                                case CheckResult.Success:
                                    caster.AddQEffect(noReactions);
                                    await caster.StrideAsync("Choose a location to move to.", false, true, null, false, true, false);
                                    await caster.PullCreature(target);
                                    caster.RemoveAllQEffects((QEffect qf) => qf == noReactions);
                                    break;
                                case CheckResult.Failure:
                                    await caster.StrideAsync("Choose a location to move to.", false, true, null, false, true, false);
                                    break;
                                case CheckResult.CriticalFailure:
                                    await caster.FallProne();
                                    break;
                            }
                            if ((result >= CheckResult.Success) && caster.HasFeat(BattledancerStyle))
                            {
                                caster.AddQEffect(CreatePanache(Skill.Performance));
                            }
                        }));
                }
                else return null;
            };
        })
        .WithPrerequisite((CalculatedCharacterSheetValues values) => values.GetProficiency(Trait.Performance) >= Proficiency.Trained, "You must be trained in Performance.");

    public static Feat SwaggeringInitiative = new TrueFeat(ModManager.RegisterFeatName("SwaggeringInitiative", "Swaggering Initiative"), 4, "You swagger readily into any battle.", "You gain a +2 circumstance bonus to initiative rolls.\nIn addition, when combat begins, you can drink one potion you're holding as a free action.", new Trait[1] { SwashTrait }, null)
        .WithPermanentQEffect(delegate (QEffect qf)
        {
            qf.Owner.AddQEffect(new QEffect()
            {
                BonusToInitiative = delegate (QEffect qf) { return new Bonus(2, BonusType.Circumstance, "Swaggering Initiative"); }
            });
            qf.StartOfCombat = async delegate (QEffect swag)
            {
                if ((qf.Owner.PrimaryItem != null) && qf.Owner.PrimaryItem.HasTrait(Trait.Drinkable))
                {
                    Item potion = qf.Owner.PrimaryItem;
                    CombatAction quaff = new CombatAction(qf.Owner, potion.Illustration, "Drink", new Trait[1] { Trait.Manipulate }, "Drink your " + potion.Name + ".\n\n" + potion.Description, Target.Self())
                        .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                        {
                            await potion.WhenYouDrink(spell, caster);
                            Sfxs.Play(SfxName.DrinkPotion);
                            qf.Owner.HeldItems.Remove(potion);
                        });
                    if (await qf.Owner.Battle.AskForConfirmation(qf.Owner, potion.Illustration, "Would you like to quickly drink your " + potion.Name + "?", "Drink")) 
                    {
                        await qf.Owner.Battle.GameLoop.FullCast(quaff);
                    }
                }
                else if ((qf.Owner.SecondaryItem != null) && qf.Owner.SecondaryItem.HasTrait(Trait.Drinkable))
                {
                    Item potion = qf.Owner.SecondaryItem;
                    CombatAction quaff = new CombatAction(qf.Owner, potion.Illustration, "Drink", new Trait[1] { Trait.Manipulate }, "Drink your " + potion.Name + ".\n\n" + potion.Description, Target.Self())
                        .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                        {
                            await potion.WhenYouDrink(spell, caster);
                            Sfxs.Play(SfxName.DrinkPotion);
                            qf.Owner.HeldItems.Remove(potion);
                        });
                    if (await qf.Owner.Battle.AskForConfirmation(qf.Owner, potion.Illustration, "Would you like to quickly drink your " + potion.Name + "?", "Drink"))
                    {
                        await qf.Owner.Battle.GameLoop.FullCast(quaff);
                    }
                }
            };
        });

    public static Feat TwinParry = new TrueFeat(ModManager.RegisterFeatName("Twin Parry", "Twin Parry {icon:Action}"), 4, "You use your two weapons to parry attacks.", "You gain a +1 circumstance bonus to your AC until the start of your next turn, or a +2 circumstance bonus if either of the weapons you hold have the parry trait. You lose this circumstance bonus if you no longer meet this feat's requirements.", new Trait[3] { Trait.Fighter, Trait.Ranger, SwashTrait })
        .WithPermanentQEffect("You gain a circumstance bonus to AC using your two held weapons.", delegate (QEffect qf)
        {
                qf.ProvideMainAction = qftechnical =>
                {
                    if (qf.Owner.PrimaryItem != null && qf.Owner.SecondaryItem != null)
                    {
                        if (qf.Owner.PrimaryItem.HasTrait(Trait.Weapon) && (qf.Owner.SecondaryItem.HasTrait(Trait.Weapon)))
                        {
                            return new ActionPossibility(new CombatAction(qf.Owner, IllustrationName.Swords, "Twin Parry", new Trait[0] { }, "You use your weapons to block oncoming attacks and increase your AC.", Target.Self())
                                .WithActionCost(1)
                                .WithEffectOnEachTarget(async (caster, spell, target, result) =>
                                {
                                    QEffect parrybonus = new QEffect();
                                    parrybonus.Name = "Twin Parry";
                                    parrybonus.Illustration = IllustrationName.Swords;
                                    parrybonus.Description = "You have a +2 circumstance bonus to AC from using your weapons to block.";
                                    parrybonus.ExpiresAt = ExpirationCondition.ExpiresAtStartOfYourTurn;
                                    parrybonus.BonusToDefenses = delegate (QEffect thing, CombatAction? bonk, Defense defense)
                                    {
                                        if (parrybonus.Owner.PrimaryItem == null || parrybonus.Owner.SecondaryItem == null) return null;
                                        if (defense == Defense.AC)
                                        {
                                            if (parrybonus.Owner.PrimaryItem.HasTrait(AddWeapons.Parry) || parrybonus.Owner.SecondaryItem.HasTrait(AddWeapons.Parry))
                                            {
                                                return new Bonus(2, BonusType.Circumstance, "Twin Parry");
                                            }
                                            else return new Bonus(1, BonusType.Circumstance, "Twin Parry");
                                        }
                                        else return null;
                                    };
                                    parrybonus.StateCheck = qfdw =>
                                    {
                                        if (qfdw.Owner.PrimaryItem == null || qfdw.Owner.SecondaryItem == null || qfdw.Owner.HasFreeHand)
                                        {
                                            parrybonus.Owner.RemoveAllQEffects((QEffect effect) => effect.Name == "Twin Parry");
                                        }
                                        else if (!(qfdw.Owner.PrimaryItem.HasTrait(Trait.Weapon) && qfdw.Owner.SecondaryItem.HasTrait(Trait.Weapon)))
                                        {
                                            parrybonus.Owner.RemoveAllQEffects((QEffect effect) => effect.Name == "Twin Parry");
                                        }
                                    };
                                    target.AddQEffect(parrybonus);
                                })
                                .WithSoundEffect(SfxName.RaiseShield));
                        }
                    }
                    return null;
                };
        });

        public static void ReplaceOpportunityAttack()
        {
            TrueFeat trueFeat = AllFeats.All.First((Feat ft) => ft.FeatName == FeatName.AttackOfOpportunity) as TrueFeat;
            trueFeat.WithAllowsForAdditionalClassTrait(SwashTrait);
        }

        public static Feat AgileManeuvers = new TrueFeat(ModManager.RegisterFeatName("AgileManeuvers", "Agile Maneuvers"), 6, "You easily maneuver against your foes.", "Your Grapple, Trip, and Shove actions have a lower multiple attack penalty: -4 instead of -5 if they're the second attack on your turn, or -8 instead of -10 if they're the third or subsequent attack on your turn.", new Trait[1] { SwashTrait }).WithPermanentQEffect(null, delegate (QEffect qf)
        {
            qf.BonusToAttackRolls = delegate (QEffect effect, CombatAction action, Creature target)
            {
                if (action == null)
                {
                    return null;
                }

                if (action.ActionId == ActionId.Grapple || action.ActionId == ActionId.Shove || action.ActionId == ActionId.Trip)
                {
                    if (effect.Owner.Actions.AttackedThisManyTimesThisTurn == 1)
                    {
                        return new Bonus(1, BonusType.Untyped, "MAP reduction (Agile Maneuvers)");
                    }

                    if (effect.Owner.Actions.AttackedThisManyTimesThisTurn >= 2)
                    {
                        return new Bonus(2, BonusType.Untyped, "MAP reduction (Agile Maneuvers)");
                    }

                    return null;
                }

                return null;
            };
        }).WithPrerequisite((CalculatedCharacterSheetValues sheet) => sheet.HasFeat(FeatName.ExpertAthletics), "You must be an expert in Athletics.");

        public static Feat CombinationFinisher = new TrueFeat(ModManager.RegisterFeatName("CombinationFinisher", "Combination Finisher"), 6, "You combine a series of attacks with a powerful blow.", "Your finishers' Strikes have a lower multiple attack penalty: -4 (or -3 with an agile weapon) instead of -5 if they're the second attack on your turn, or -8 (or -6 with an agile weapon) instead of -10 if they're the third or subsequent attack on your turn.", new Trait[1] { SwashTrait }).WithPermanentQEffect(null, delegate (QEffect qf)
        {
            qf.BonusToAttackRolls = delegate (QEffect effect, CombatAction action, Creature target)
            {
                if (action == null)
                {
                    return null;
                }

                if (action.HasTrait(Finisher))
                {
                    if (effect.Owner.Actions.AttackedThisManyTimesThisTurn == 1)
                    {
                        return new Bonus(1, BonusType.Untyped, "MAP reduction (Agile Maneuvers)");
                    }

                    if (effect.Owner.Actions.AttackedThisManyTimesThisTurn >= 2)
                    {
                        return new Bonus(2, BonusType.Untyped, "MAP reduction (Agile Maneuvers)");
                    }

                    return null;
                }

                return null;
            };
        });

        public static Feat PreciseFinisher = new TrueFeat(ModManager.RegisterFeatName("PreciseFinisher", "Precise Finisher"), 6, "Even when your foe avoids your Confident Finisher, you can still hit a vital spot.", "On a failure with Confident Finisher, you apply your full Precise Strike damage instead of half.", new Trait[1] { SwashTrait }).WithPermanentQEffect(delegate (QEffect qf)
        {
            qf.Id = PreciseFinisherQEffectId;
        }).WithPrerequisite((CalculatedCharacterSheetValues sheet) => sheet.HasFeat(Confident), "You must have Confident Finisher.");

        public static Feat BleedingFinisher = new TrueFeat(ModManager.RegisterFeatName("BleedingFinisher", "Bleeding Finisher {icon:Action}"), 8, "Your blow inflicts profuse bleeding.", "Make a piercing or slashing Strike with a weapon or unarmed attack that allows you to add your Precise Strike damage. If you hit, the target takes persistent bleed damage equal to your Precise Strike finisher damage.", new Trait[2] { SwashTrait, Finisher }).WithPermanentQEffect(delegate (QEffect qf)
        {
            qf.ProvideStrikeModifier = delegate (Item item)
            {
                StrikeModifiers strikeModifiers2 = new StrikeModifiers();
                bool flag5 = !item.HasTrait(Trait.Ranged) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse));
                bool flag6 = qf.Owner.HasEffect(PanacheId);
                bool flag7 = item.WeaponProperties.DamageKind == DamageKind.Piercing || item.WeaponProperties.DamageKind == DamageKind.Slashing;
                if (flag5 && flag6 && flag7)
                {
                    CombatAction combatAction = qf.Owner.CreateStrike(item, -1, strikeModifiers2);
                    combatAction.Name = "Bleeding Finisher";
                    combatAction.Illustration = new SideBySideIllustration(item.Illustration, IllustrationName.BloodVendetta);
                    combatAction.Description = StrikeRules.CreateBasicStrikeDescription2(combatAction.StrikeModifiers, null, "The target takes 3d6 persistent bleed damage.", null, null, "You lose panache, whether the attack succeeds or fails.");
                    combatAction.ActionCost = 1;
                    combatAction.StrikeModifiers.OnEachTarget = async delegate (Creature owner, Creature victim, CheckResult result)
                    {
                        if (result >= CheckResult.Success)
                        {
                            victim.AddQEffect(QEffect.PersistentDamage("3d6", DamageKind.Bleed));
                        }
                        FinisherExhaustion(qf.Owner);
                    };
                    combatAction.Traits.Add(Finisher);
                    return combatAction;
                }
                return null;
            };
        });

        public static Feat DualFinisher = new TrueFeat(ModManager.RegisterFeatName("DualFinisher", "Dual Finisher {icon:Action}"), 8, "You split your attacks.", "Make two melee Strikes, one with each required weapon, each against a different foe. If the second Strike is made with a non-agile weapon, it takes a -2 penalty. Increase your multiple attack penalty only after attempting both Strikes.", new Trait[2] { SwashTrait, Finisher }).WithPermanentQEffect(delegate (QEffect qf)
        {
            qf.ProvideMainAction = delegate
            {
                bool flag3 = qf.Owner.HasEffect(PanacheId);
                bool flag4 = qf.Owner.PrimaryItem != null && qf.Owner.SecondaryItem != null;
                return (flag3 && flag4) ? ((qf.Owner.PrimaryItem.HasTrait(Trait.Weapon) && qf.Owner.SecondaryItem.HasTrait(Trait.Weapon)) ? new ActionPossibility(new CombatAction(qf.Owner, new SideBySideIllustration(qf.Owner.PrimaryItem.Illustration, qf.Owner.SecondaryItem.Illustration), "Dual Finisher", new Trait[1] { Finisher }, "Make two attacks, one with each of your two weapons, each against a different target. You lose panache and increase your multiple attack penalty after performing both attacks.", Target.MultipleCreatureTargets(Target.Melee(), Target.Melee()).WithMustBeDistinct().WithMinimumTargets(2)).WithActionCost(1).WithEffectOnChosenTargets(async delegate (Creature swash, ChosenTargets target)
                {
                    QEffect penalty = new QEffect
                    {
                        BonusToAttackRolls = (QEffect thing, CombatAction bonk, Creature them) => new Bonus(-2, BonusType.Untyped, "Dual Finisher penalty")
                    };
                    QEffect precisionflag = new QEffect
                    {
                        YouBeginAction = async delegate (QEffect thing, CombatAction action)
                        {
                            action.Traits.Add(Finisher);
                        }
                    };
                    swash.AddQEffect(precisionflag);
                    int map = qf.Owner.Actions.AttackedThisManyTimesThisTurn;
                    if (qf.Owner.HeldItems.Count >= 1)
                    {
                        await qf.Owner.MakeStrike(target.ChosenCreatures[0], qf.Owner.PrimaryItem, map);
                    }

                    if (!qf.Owner.SecondaryItem.HasTrait(Trait.Agile))
                    {
                        swash.AddQEffect(penalty);
                    }

                    if (qf.Owner.HeldItems.Count >= 2)
                    {
                        await qf.Owner.MakeStrike(target.ChosenCreatures[1], qf.Owner.SecondaryItem, map);
                    }

                    swash.RemoveAllQEffects((QEffect efct) => efct == penalty);
                    swash.RemoveAllQEffects((QEffect efct) => efct == precisionflag);
                    FinisherExhaustion(swash);
                })) : null) : null;
            };
        });

        public static Feat FlamboyantCruelty = new TrueFeat(ModManager.RegisterFeatName("FlamboyantCruelty", "Flamboyant Cruelty"), 8, "You love to kick your enemies when they're down, and look fabulous when you do so.", "When you make a melee Strike against a foe with at least two of the following conditions, you gain a circumstance bonus to your damage roll equal to the number of conditions the target has. The qualifying conditions are {b}clumsy, drained, enfeebled, frightened, sickened, and stupefied{/b}. If you hit such a foe, you gain a +1 circumstance bonus to skill checks to Tumble Through and perform your style's panache-granting actions until the end of your turn.", new Trait[1] { SwashTrait }).WithPermanentQEffect("You deal more damage hitting enemies affected by certain adverse conditions.", delegate (QEffect qf)
        {
            qf.BonusToDamage = delegate (QEffect effect, CombatAction action, Creature me)
            {
                int num = 0;
                if (action.ChosenTargets.ChosenCreature.HasEffect(QEffectId.Clumsy))
                {
                    num++;
                }

                if (action.ChosenTargets.ChosenCreature.HasEffect(QEffectId.Drained))
                {
                    num++;
                }

                if (action.ChosenTargets.ChosenCreature.HasEffect(QEffectId.Enfeebled))
                {
                    num++;
                }

                if (action.ChosenTargets.ChosenCreature.HasEffect(QEffectId.Frightened))
                {
                    num++;
                }

                if (action.ChosenTargets.ChosenCreature.HasEffect(QEffectId.Sickened))
                {
                    num++;
                }

                if (action.ChosenTargets.ChosenCreature.HasEffect(QEffectId.Stupefied))
                {
                    num++;
                }

                return (num >= 2) ? new Bonus(num, BonusType.Circumstance, "Flamboyant Cruelty") : null;
            };
            qf.AfterYouDealDamage = async delegate (Creature attacker, CombatAction action, Creature defender)
            {
                int conditions = 0;
                if (action.ChosenTargets.ChosenCreature.HasEffect(QEffectId.Clumsy))
                {
                    conditions++;
                }

                if (action.ChosenTargets.ChosenCreature.HasEffect(QEffectId.Drained))
                {
                    conditions++;
                }

                if (action.ChosenTargets.ChosenCreature.HasEffect(QEffectId.Enfeebled))
                {
                    conditions++;
                }

                if (action.ChosenTargets.ChosenCreature.HasEffect(QEffectId.Frightened))
                {
                    conditions++;
                }

                if (action.ChosenTargets.ChosenCreature.HasEffect(QEffectId.Sickened))
                {
                    conditions++;
                }

                if (action.ChosenTargets.ChosenCreature.HasEffect(QEffectId.Stupefied))
                {
                    conditions++;
                }

                if (conditions >= 2)
                {
                    attacker.AddQEffect(new QEffect
                    {
                        Name = "Flamboyant Cruelty",
                        Illustration = new ModdedIllustration("PhoenixAssets/panache.PNG"),
                        Description = "You have a +1 circumstance bonus to Tumble Through and to perform actions that would give you panache until the end of your turn.",
                        BonusToSkillChecks = delegate (Skill skill, CombatAction action, Creature target)
                        {
                            SwashbucklerStyle style = (SwashbucklerStyle)qf.Owner.PersistentCharacterSheet.Calculated.AllFeats.Find(feat => feat.HasTrait(SwashStyle));
                            if (action.ActionId == ActionId.TumbleThrough || style.PanacheTriggers.Contains(action.ActionId))
                            {
                                return new Bonus(1, BonusType.Circumstance, "Flamboyant Cruelty");
                            }
                            else if (qf.Owner.HasFeat(DisarmingFlair.FeatName) && action.ActionId == ActionId.Disarm)
                            {
                                return new Bonus(1, BonusType.Circumstance, "Flamboyant Cruelty");
                            }
                            else return null;
                        },
                        ExpiresAt = ExpirationCondition.ExpiresAtEndOfYourTurn
                    });
                }
            };
        });

        public static void ReplaceNimbleRoll()
        {
            TrueFeat trueFeat = AllFeats.All.First((Feat ft) => ft.FeatName == FeatName.NimbleRoll) as TrueFeat;
            trueFeat.WithAllowsForAdditionalClassTrait(SwashTrait);
        }

        public static Feat StunningFinisher = new TrueFeat(ModManager.RegisterFeatName("StunningFinisher", "Stunning Finisher {icon:Action}"), 8, "You attempt a dizzying blow.", "Make a melee Strike. If you hit, your target must make a Fortitude save against your class DC with the following results: this save has the incapacitation trait." + S.FourDegreesOfSuccess("The target is unaffected.", "The target can't take reactions until its next turn.", "The creature is stunned 1.", "The creature is stunned 3."), new Trait[2] { SwashTrait, Finisher }).WithPermanentQEffect(null, delegate (QEffect qf)
        {
            qf.ProvideStrikeModifier = delegate (Item item)
            {
                StrikeModifiers strikeModifiers = new StrikeModifiers();
                bool flag = item.HasTrait(Trait.Melee);
                bool flag2 = qf.Owner.HasEffect(PanacheId);
                if (flag && flag2)
                {
                    CombatAction stun = qf.Owner.CreateStrike(item, -1, strikeModifiers);
                    stun.Name = "Stunning Finisher";
                    stun.Illustration = new SideBySideIllustration(item.Illustration, IllustrationName.Stunned);
                    stun.Description = StrikeRules.CreateBasicStrikeDescription2(stun.StrikeModifiers, null, "The target makes a DC " + stun.Owner.ClassOrSpellDC() + " Fortitude save (this is an incapacitation effect). On a success, it can't take reactions for 1 turn. On a failure, it is stunned 1, and on a critical failure, it is stunned 3.", null, null, "You lose panache, whether the attack succeeds or fails.");
                    stun.ActionCost = 1;
                    stun.Traits.Add(Finisher);
                    stun.StrikeModifiers.OnEachTarget = async delegate (Creature owner, Creature victim, CheckResult result)
                    {
                        FinisherExhaustion(owner);
                        if (result >= CheckResult.Success)
                        {
                            CombatAction theactualstun = CombatAction.CreateSimpleIncapacitation(stun.Owner, "Stunning Finisher", stun.Owner.MaximumSpellRank);
                            switch (CommonSpellEffects.RollSavingThrow(victim, theactualstun, Defense.Fortitude, stun.Owner.ClassOrSpellDC()))
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
                return null;
            };
        });

        public static Feat VivaciousBravado = new TrueFeat(ModManager.RegisterFeatName("VivaciousBravado", "Vivacious Bravado {icon:Action}"), 8, "Your ego swells, granting you a temporary reprieve from your pain.", "{b}Requirements: {/b}You gained panache this turn. \n\nYou gain temporary Hit Points equal to your level plus your Charisma modifier.", new Trait[1] { SwashTrait }).WithPermanentQEffect(delegate (QEffect qf)
        {
            qf.AfterYouAcquireEffect = async (qf, qf2) =>
            {
                if (qf2.Id == PanacheId)
                {
                    qf.Owner.AddQEffect(new QEffect()
                    {
                        ProvideMainAction = delegate
                        {
                            int hpgained = qf.Owner.Level + qf.Owner.Abilities.Charisma;
                            return new ActionPossibility(new CombatAction(qf.Owner, IllustrationName.WinningStreak, "Vivacious Bravado", new Trait[0], "You gain " + hpgained + " temporary Hit Points.", Target.Self()).WithActionCost(1).WithEffectOnEachTarget(async delegate (CombatAction spell, Creature caster, Creature target, CheckResult result)
                            {
                                caster.GainTemporaryHP(hpgained);
                            }).WithSoundEffect(SfxName.NaturalHealing));
                        },
                        ExpiresAt = ExpirationCondition.ExpiresAtEndOfYourTurn
                    });
                }
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
        }
    }
    public static void LoadSwash()
    {
        ModManager.AddFeat(Swashbuckler);
        //ModManager.AddFeat(AddPanache);
        ModManager.AddFeat(BonMot);
        ModManager.AddFeat(FascinatingPerformance);
        ModManager.AddFeat(DisarmingFlair);
        AddSwashDuelingParry();
        ModManager.AddFeat(FlyingBlade);
        ModManager.AddFeat(FocusedFascination);
        ModManager.AddFeat(GoadingFeint);
        ReplaceNimbleDodge();
        ModManager.AddFeat(OneForAll);
        ReplaceYoureNext();
        ModManager.AddFeat(AfterYou);
        ModManager.AddFeat(Antagonize);
        ModManager.AddFeat(CharmedLife);
        ModManager.AddFeat(FinishingFollowThrough);
        ModManager.AddFeat(TumbleBehind);
        ModManager.AddFeat(UnbalancingFinisher);
        ModManager.AddFeat(DramaticCatch);
        ModManager.AddFeat(GuardiansDeflection);
        GiveGuardiansDeflectionToFighters();
        ModManager.AddFeat(ImpalingFinisher);
        ModManager.AddFeat(LeadingDance);
        ModManager.AddFeat(SwaggeringInitiative);
        ModManager.AddFeat(TwinParry);
        ReplaceOpportunityAttack();
        ModManager.AddFeat(PreciseFinisher);
        ModManager.AddFeat(BleedingFinisher);
        ModManager.AddFeat(DualFinisher);
        ModManager.AddFeat(FlamboyantCruelty);
        ReplaceNimbleRoll();
        ModManager.AddFeat(StunningFinisher);
        ModManager.AddFeat(VivaciousBravado);
    }
}