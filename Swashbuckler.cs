using Dawnsbury.Core.CharacterBuilder.AbilityScores;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Core.CharacterBuilder;
using Dawnsbury.Core.CharacterBuilder.Selections.Options;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Modding;
using System.Collections.Generic;
using System.Linq;
using Dawnsbury.Core;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Roller;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Audio;
using Dawnsbury.Core.Mechanics.Rules;

namespace Dawnsbury.Mods.Phoenix;

public class AddSwash
{
    public static Trait SwashTrait = ModManager.RegisterTrait("SwashTrait", new TraitProperties("Swashbuckler", true) { IsClassTrait = true });
    public static Trait Finisher = ModManager.RegisterTrait("Finisher", new TraitProperties("Finisher", true, "You can only use an action with the Finisher trait if you have panache, and you lose panache after performing the action.", true));
    public static QEffectId PanacheId = ModManager.RegisterEnumMember<QEffectId>("Panache");
    public static QEffect CreatePanache()
    {
        QEffect panache = new QEffect()
        {
            Id = PanacheId,
            Key = "Panache",
            Name = "Panache",
            Illustration = new ModdedIllustration("PhoenixAssets/panache.PNG"),
            Description = "You have panache. It provides a status bonus to your Speed and a circumstance bonus to certain skill checks.",
            BonusToAllSpeeds = (delegate(QEffect qfpanache)
            {
                if (qfpanache.Owner.Level < 3)
                {
                    return new Bonus(1, BonusType.Status, "Panache");
                }
                else if (qfpanache.Owner.Level >= 3 && qfpanache.Owner.Level < 7)
                {
                    return new Bonus(2, BonusType.Status, "Panache");
                }
                else return null;
            })
        };
        panache.BonusToSkills = delegate(Skill skill)
        {
            if ((skill == Skill.Acrobatics) 
                || ((panache.Owner.PersistentCharacterSheet?.Calculated.AllFeats.Any(feat => feat.Name == "Braggart") ?? false) && (skill == Skill.Intimidation)) 
                || ((panache.Owner.PersistentCharacterSheet?.Calculated.AllFeats.Any(feat => feat.Name == "Fencer") ?? false) && (skill == Skill.Deception)) 
                || ((panache.Owner.PersistentCharacterSheet?.Calculated.AllFeats.Any(feat => feat.Name == "Gymnast") ?? false) && skill == Skill.Athletics) 
                /*|| ((panache.Owner.PersistentCharacterSheet?.Calculated.AllFeats.Any(feat => feat.Name == "Wit") ?? false) && (skill == Skill.Diplomacy))*/)
            {
                return new Bonus(1, BonusType.Circumstance, "Panache");
            }
            else return null;
        };
        return panache;
    }

    public static void FinisherExhaustion(Creature swash)
    {
        swash.RemoveAllQEffects((QEffect panache) => panache.Name == "Panache");
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
        new Trait[6]
        {
            Trait.Fortitude,
            Trait.Simple,
            Trait.Martial,
            Trait.Unarmed,
            Trait.LightArmor,
            Trait.UnarmoredDefense
        },
        new Trait[3]
        {
            Trait.Perception,
            Trait.Reflex,
            Trait.Will
        },
        4,
        "{b}Tumble Through{/b} Normally, you would only tumble through your enemies if the situation was in dire need of it. You instead use such a feat as a regular tool in your belt. Using 1 action, you can choose an enemy, then make an Acrobatics check against their Reflex DC. If you succeed, you move into their space, then move an additional 5 feet. If you fail, your movement ends adjacent to the enemy.\n\n {b}1. Panache.{/b} You learn how to leverage your skills to enter a state of heightened ability called panache. You gain panache when you succeed on certain skill checks with a bit of flair, including Tumble Through and other checks determined by your style. While you have panache, you gain a +5 circumstance bonus to your Speed and a +1 circumstance bonus to checks that would give you panache. It also allows you to use special attacks called finishers, which cause you to lose panache when performed.\n{b}2. Swashbuckler style.{/b} You choose a style that represents what kind of flair you bring to a battlefield. When you choose a style, you become trained in a skill and can use certain actions using that skill to gain panache.\n{b}3. Precise Strike.{/b} While you have panache, you deal an extra 2 precision damage with your agile or finesse melee weapons. If you use a finisher, the damage increases to 2d6 instead.\n{b}4. Confident Finisher.{/b} If you have panache, you can use an action to make a Strike against an ally in melee range. If you miss, you deal half your Precise Strike damage.\n{b}5. Swashbuckler feat.{/b}\n\n{b}At Higher Levels:\nLevel 2:{/b} Swashbuckler feat.\n{b}Level 3:{/b} General feat, skill increase, Opportune Riposte{i}(counterattack if an enemy critically fails to hit you){/i}, Vivacious Speed{i}(The status bonus from panache increases and you gain half of it even if you don't have panache){/i}\n{b}Level 4:{/b} Swashbuckler feat.",
        new List<Feat>()
        {
            //Subclasses. For a swashbuckler, this is their styles.
            new Feat(ModManager.RegisterFeatName("Braggart"), "You boast, taunt, and psychologically needle your foes", "You become trained in Intimidation. You gain panache whenever you successfully Demoralize a foe.", new List<Trait>(), null)
                .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
                {
                    sheet.GrantFeat(FeatName.Intimidation);
                }),
            new Feat(ModManager.RegisterFeatName("Fencer"), "You move carefully, feinting and creating false openings to lead your foes into inopportune attacks.","You become trained in Deception. You gain panache whenever you successfully Feint or Create a Diversion.", new List<Trait>(), null)
                .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
                {
                    sheet.GrantFeat(FeatName.Deception);
                }),
            new Feat(ModManager.RegisterFeatName("Gymnast"), "You reposition, maneuver, and bewilder your foes with daring feats of physical prowess.", "You become trained in Athletics. You gain panache whenever you successfully Grapple, Shove, or Trip a foe.", new List<Trait>(), null)
                .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
                {
                    sheet.GrantFeat(FeatName.Athletics);
                })/*,
            new Feat(ModManager.RegisterFeatName("Wit"), "You are friendly, clever, and full of humor, knowing just what to say in any situation. Your witticisms leave your foes unprepared for the skill and speed of your attacks.", "You become trained in Diplomacy and gain the Bon Mot feat. You gain panache whenever you succeed at a Bon Mot against a foe.", new List<Trait>(), null)
                .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
                {
                    sheet.GrantFeat(FeatName.Diplomacy);
                    sheet.GrantFeat(BonMot.FeatName);
                })*/
        })
        .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
            {
                sheet.AddFeat(Confident!, null);
                sheet.AddFeat(TumbleThrough!, null);
                sheet.AddFeat(PreciseStrike!, null);
                sheet.AddSelectionOption(new SingleFeatSelectionOption("Swash1", "Swashbuckler feat", 1, (Feat ft) => ft.HasTrait(SwashTrait)));
            })
        .WithOnCreature(delegate (Creature creature)
        {
            creature.AddQEffect(PanacheGranter());
        })
        .WithOnSheet(delegate (CalculatedCharacterSheetValues sheet)
            {
                sheet.AddAtLevel(3, delegate (CalculatedCharacterSheetValues values)
                {
                    values.SetProficiency(Trait.Fortitude, Proficiency.Expert);
                    values.AddFeat(VivaciousSpeed!, null);
                    values.AddFeat(OpportuneRiposte!, null);
                });
            });

    public static readonly Feat OpportuneRiposte = new Feat(ModManager.RegisterFeatName("Opportune Riposte", "Opportune Riposte {icon:Reaction}"), "You take advantage of an opening from your foe's fumbled attack.", "When an enemy critically fails its Strike against you, you can use your reaction to make a melee Strike against that enemy.", new List<Trait>(), null)
        .WithPermanentQEffect("When an enemy critically fails a Strike against you, you may Strike it using a reaction.", delegate (QEffect qf)
        {
            qf.AfterYouAreTargeted = async delegate (QEffect qf, CombatAction action)
            {
                if (action.HasTrait(Trait.Attack) && action.CheckResult == CheckResult.CriticalFailure)
                {
                    Creature enemy = action.Owner;
                    Item? weapon = qf.Owner.PrimaryWeapon;
                    if (weapon == null) return;
                    CombatAction riposte = qf.Owner.CreateStrike(weapon, 0);
                    riposte.WithActionCost(0);
                    bool flag = riposte.CanBeginToUse(qf.Owner);
                    bool flag2 = flag;
                    if (flag2)
                    {
                        flag2 = await qf.Owner.Battle.AskToUseReaction(qf.Owner, enemy.Name + "has critically failed an attack against you. Would you like to use your reaction to riposte?");
                        if (flag2)
                        {
                            await qf.Owner.MakeStrike(enemy, weapon, 0);
                        }
                    }
                }
            };
        });

    //KNOWN ISSUE: Confident finisher doesn't do anything if you miss due to MAP.
    //The Precise Strike damage is currently hardcoded into Confident Finisher, since DD doesn't level to a point where Swashbucklers get more than that. If a function can be found to check the damage of Precise Strike, that'd be swell..
    public static readonly Feat Confident = new Feat(ModManager.RegisterFeatName("Confident Finisher", "Confident Finisher{icon:Action}"), "You gain an elegant finishing move that you can use when you have panache.", "If you have panache, you can make a Strike that deals damage even on a failure.", new List<Trait>(), null)
        .WithPermanentQEffect("If you have panache, you can make a Strike that deals damage even on a failure.", delegate (QEffect qf)
        {
            qf.ProvideStrikeModifier = delegate (Item item)
            {
                StrikeModifiers conf = new StrikeModifiers();
                bool flag = !item.HasTrait(Trait.Ranged) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse) || item.HasTrait(Trait.Unarmed));
                bool flag2 = qf.Owner.HasEffect(PanacheId);
                if (flag && flag2)
                {
                    CombatAction conffinish = qf.Owner.CreateStrike(item, -1, conf);

                    conffinish.Name = "Confident Finisher";
                    conffinish.Illustration = new SideBySideIllustration(item.Illustration, IllustrationName.StarHit);
                    conffinish.ActionCost = 1;
                    conffinish.WithEffectOnChosenTargets(async delegate (Creature creature, ChosenTargets targets)
                    {
                        targets.ChosenCreature!.AddQEffect(new QEffect()
                        {
                            AfterYouAreTargeted = async delegate (QEffect qfbonk, CombatAction strike)
                            {
                                if (strike.CheckResult == CheckResult.Failure)
                                {
                                    HalfDiceFormula halfdamage = new HalfDiceFormula(DiceFormula.FromText("2d6", "Precise Strike"), "Miss with Confident Finisher");
                                    await strike.Owner.DealDirectDamage(conffinish, halfdamage, targets.ChosenCreature, conffinish.CheckResult, conffinish.StrikeModifiers.CalculatedItem!.WeaponProperties!.DamageKind);
                                }
                            },
                            ExpiresAt = ExpirationCondition.Ephemeral
                        });
                        FinisherExhaustion(conffinish.Owner);
                    });
                    conffinish.Traits.Add(Finisher);
                    return conffinish;
                }
                return null;
            };
        });

    public static readonly Feat PreciseStrike = new Feat(ModManager.RegisterFeatName("PreciseStrike", "Precise Strike"), "You strike with flair.", "When you have panache and make a Strike with a melee agile or finesse weapon or an agile or finesse unarmed strike, you deal 2 extra damage. This damage is 2d6 instead if the Strike was part of a finisher.", new List<Trait>(), null)
        .WithPermanentQEffect("You deal more damage when using agile or finesse weapons.", delegate (QEffect qf)
        {
            qf.Name = "Precise Strike";
            qf.YouDealDamageWithStrike = delegate (QEffect qf, CombatAction action, DiceFormula diceFormula, Creature defender)
            {
                bool flag = action.HasTrait(Trait.Agile) || action.HasTrait(Trait.Finesse) || action.HasTrait(Trait.Unarmed);
                bool flag2 = action.Owner.HasEffect(PanacheId);
                bool flag3 = action.HasTrait(Finisher);
                bool flag4 = !action.HasTrait(Trait.Ranged) || (action.HasTrait(Trait.Thrown) && (action.Owner.PersistentCharacterSheet?.Calculated.AllFeats.Any(feat => feat.Name == "Flying Blade") ?? false) && (defender.DistanceTo(qf.Owner) <= action.Item!.WeaponProperties!.RangeIncrement));
                if (flag && flag3 && flag4)
                {
                    return diceFormula.Add(DiceFormula.FromText("2d6", "Precise Strike"));
                }
                else if (flag && flag2 && flag4)
                {
                    return diceFormula.Add(DiceFormula.FromText("2", "Precise Strike"));
                }
                return diceFormula;
            };
        });

    public static QEffect PanacheGranter()
    {
        return new QEffect()
        {
            AfterYouTakeAction = async delegate (QEffect qf, CombatAction action)
            {
                bool flag = (action.CheckResult == CheckResult.Success) || (action.CheckResult == CheckResult.CriticalSuccess);
                bool flag2 = (action.Name == "Tumble Through") 
                    || ((qf.Owner.PersistentCharacterSheet?.Calculated.AllFeats.Any(feat => feat.Name == "Braggart") ?? false) && (action.Name == "Demoralize")) 
                    || ((qf.Owner.PersistentCharacterSheet?.Calculated.AllFeats.Any(feat => feat.Name == "Fencer") ?? false) && (action.Name == "Feint" || action.Name == "Create a Diversion")) 
                    || ((qf.Owner.PersistentCharacterSheet?.Calculated.AllFeats.Any(feat => feat.Name == "Gymnast") ?? false) && (action.Name == "Grapple" || action.Name == "Shove" || action.Name == "Trip"))
                    || ((qf.Owner.PersistentCharacterSheet?.Calculated.AllFeats.Any(feat => feat.Name == "Wit") ?? false) && (action.Name == "Bon Mot"));
                bool flag3 = !qf.Owner.HasEffect(PanacheId);
                {
                    if (flag && flag2 && flag3)
                    {
                        qf.Owner.AddQEffect(CreatePanache());
                    }
                }
            }
        };
    }
  
    public static readonly Feat VivaciousSpeed = new Feat(ModManager.RegisterFeatName("Vivacious Speed"), "When you've made an impression, you move even faster than normal, darting about the battlefield with incredible speed.", "The status bonus to your Speed from panache increases to 10 feet. When you don't have panache, you still get half this status bonus to your Speeds, rounded down to the nearest 5-foot increment.", new List<Trait>(), null)
        .WithPermanentQEffect("You move quickly, even when you don't have panache.", delegate (QEffect qf)
        {
            qf.BonusToAllSpeeds = (delegate (QEffect qf)
            {
                if (!qf.Owner.HasEffect(PanacheId))
                {
                    return new Bonus(1, BonusType.Status, "Vivacious Speed");
                }
                else return null;
            });
        });

    public static readonly Feat TumbleThrough = new Feat(ModManager.RegisterFeatName("Tumble Through"), "You know how to better run circles around your enemies.", "Choose an enemy creature and attempt an Acrobatics check to Tumble Through them. If you succeed, you move into their space, then an additional 5 feet.", new List<Trait>(), null)
        .WithPermanentQEffect(null, delegate (QEffect qf)
        {
            qf.ProvideActionIntoPossibilitySection = (qftumble, section) =>
            {
                if (section.PossibilitySectionId == PossibilitySectionId.OtherManeuvers)
                {
                    return new ActionPossibility(new CombatAction(qf.Owner, IllustrationName.FleetStep, "Tumble Through", new Trait[1] { Trait.Move }, "Move to a foe, then make an Acrobatics check. If you succeed, you can move an additional 5 feet.",
                        Target.Ranged(qf.Owner.Speed - 3)
                        .WithAdditionalConditionOnTargetCreature((Creature self, Creature enemy) =>
                            ((enemy.Battle.Map.AllTiles.Where((Tile t) => ((t.DistanceTo(enemy.Occupies) == 1) && (t.GetWalkDifficulty(self) < 2) && (t.IsGenuinelyFreeTo(self)))).Count() == 0)) ? Usability.NotUsableOnThisCreature("Cannot move out of target's space") : Usability.Usable))
                        .WithEffectOnChosenTargets(async delegate (CombatAction movement, Creature self, ChosenTargets targets)
                        {
                            Tile tile = targets.ChosenCreature!.Occupies;
                            if (movement.CheckResult >= CheckResult.Success)
                            {
                                tile.PrimaryOccupant = null;
                                bool WasDifficultTerrain = tile.DifficultTerrain;
                                tile.DifficultTerrain = true;
                                await self.StrideAsync("Move to the target.", false, false, tile, false, false, false);
                                await self.StrideAsync("Choose a new space to move to.", false, true, null, false, false, false);
                                if (WasDifficultTerrain == false)
                                {
                                    tile.DifficultTerrain = false;
                                }
                                tile.PrimaryOccupant = targets.ChosenCreature;
                            }
                            else await self.StrideAsync("Choose a space to move to. You must end adjacent to the enemy you attempted to Tumble through.", false, false, tile, false, false, true);
                        })
                        .WithActionCost(1)
                        .WithActiveRollSpecification(new ActiveRollSpecification(Checks.SkillCheck(Skill.Acrobatics), Checks.DefenseDC(Defense.Reflex))));
                }
                else return null;
            };
        });

    //Implemented as far as I'm aware, but the AI will never use the retort.
    public static Feat BonMot = new TrueFeat(ModManager.RegisterFeatName("BonMot", "Bon Mot {icon:Action}"), 1, "You launch an insightful quip at a foe, distracting them.", "Using one action, choose a foe within 30 feet of you and make a Diplomacy check against their Will DC, with the following effects:\n\n{b}Critical Success:{/b} The target is distracted and takes a -3 status penalty to Perception and to Will saves for 1 minute. The target can end the effect early by using a single action to retort to your quip.\n{b}Success:{/b} As success, but the penalty is -2.\n{b}Critical Failure: {/b}Your quip is atrocious. You take the same penalty an enemy would take had you succeeded. This lasts for one minute or until you use another Bon Mot and succeed.", new Trait[6] { Trait.Auditory, Trait.Concentrate, Trait.Emotion, Trait.General, Trait.Linguistic, Trait.Mental }, null)
        .WithPermanentQEffect(null, delegate (QEffect qf)
        {
            qf.ProvideActionIntoPossibilitySection = (qfbonmot, section) =>
            {
                if (section.PossibilitySectionId == PossibilitySectionId.OtherManeuvers)
                {
                    return new ActionPossibility(new CombatAction(qf.Owner, IllustrationName.Confusion, "Bon Mot", new Trait[5] { Trait.Auditory, Trait.Concentrate, Trait.Emotion, Trait.Linguistic, Trait.Mental }, "Use Diplomacy to distract a foe.", Target.Ranged(6)
                        .WithAdditionalConditionOnTargetCreature((Creature self, Creature target) => (target.QEffects.Any((QEffect effect) => effect.Name == "Bon Mot")) ? Usability.NotUsableOnThisCreature("This enemy is already distracted.") : Usability.Usable)
                        .WithAdditionalConditionOnTargetCreature((Creature self, Creature target) => target.DoesNotSpeakCommon ? Usability.NotUsableOnThisCreature("The creature cannot understand your words.") : Usability.Usable))
                        .WithActionCost(1)
                        .WithActiveRollSpecification(new ActiveRollSpecification(Checks.SkillCheck(Skill.Diplomacy), Checks.DefenseDC(Defense.Will)))
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
                                            .WithActiveRollSpecification(new ActiveRollSpecification(Checks.SkillCheck(Skill.Diplomacy), Checks.DefenseDC(Defense.Will)))
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
                                            .WithActiveRollSpecification(new ActiveRollSpecification(Checks.SkillCheck(Skill.Diplomacy), Checks.DefenseDC(Defense.Will)))
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
                            target.AddQEffect(CreatePanache());
                        }));
                })
            };
            creature.AddQEffect(Panacheer);
        })
        .WithCustomName("Give Panache");

    //Technically implemented, but I'm not sure the AI recognizes the Recover Grip action for what it is.
    public static Feat DisarmingFlair = new TrueFeat(ModManager.RegisterFeatName("Disarming Flair", "Disarming Flair"), 1, "It's harder for foes to regain their grip when you knock their weapon partially out of their hands.", "When you succeed at an Athletics check to Disarm, the circumstance bonus and penalty from Disarm last until the end of your next turn, instead of until the beginning of the target's next turn. The target can use an Interact action to adjust their grip and remove this effect. If your swashbuckler style is gymnast and you succeed at your Athletics check to Disarm a foe, you gain panache.", new Trait[1] { SwashTrait }, null)
        .WithPermanentQEffect("Your Disarm attempts last longer.", delegate (QEffect qf)
        {
            qf.AfterYouTakeAction = async delegate (QEffect effect, CombatAction disarm)
            {
                if (disarm.Name == "Disarm" && disarm.CheckResult == CheckResult.Success)
                {
                    if ((qf.Owner.PersistentCharacterSheet?.Calculated.AllFeats.Any(feat => feat.Name == "Gymnast") ?? false) && !qf.Owner.HasEffect(PanacheId))
                    {
                        qf.Owner.AddQEffect(CreatePanache());
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

    public static Feat DuelingParry = new TrueFeat(ModManager.RegisterFeatName("DuelingParry", "Dueling Parry{icon:Action}"), 2, "You use your one-handed weapon to parry attacks.", "{b}Requirements:{/b} You are holding a one-handed melee weapon, and have the other hand free.\n\nYou gain a +2 circumstance bonus to your AC until the start of your next turn. You lose this circumstance bonus if you no longer meet this feat's requirements.", new Trait[2] { Trait.Fighter, SwashTrait })
        .WithPermanentQEffect(delegate (QEffect qf)
        {
            qf.ProvideMainAction = qftechnical =>
            {
                if (qf.Owner.HasFreeHand && qf.Owner.PrimaryItem != null)
                {
                    if (qf.Owner.PrimaryItem.HasTrait(Trait.Weapon) && qf.Owner.PrimaryItem.HasTrait(Trait.Melee))
                    {
                        return new ActionPossibility(new CombatAction(qf.Owner, IllustrationName.Swords, "Dueling Parry", new Trait[0] { }, "You use your weapon to block oncoming attacks and increase your AC.", Target.Self())
                            .WithActionCost(1)
                            .WithEffectOnEachTarget(async (caster, spell, target, result) =>
                            {
                                if (qf.Owner.PrimaryWeapon == null) return;
                                QEffect parrybonus = new QEffect();
                                parrybonus.Name = "Dueling Parry";
                                parrybonus.Illustration = qf.Owner.PrimaryWeapon.Illustration;
                                parrybonus.Description = "You have a +2 circumstance bonus to AC from using your weapon to block.";
                                parrybonus.ExpiresAt = ExpirationCondition.ExpiresAtStartOfYourTurn;
                                parrybonus.BonusToDefenses = delegate (QEffect thing, CombatAction? bonk, Defense defense)
                                {
                                    if (defense == Defense.AC)
                                    {
                                        return new Bonus(2, BonusType.Circumstance, "Dueling Parry");
                                    }
                                    else return null;
                                };
                                parrybonus.StateCheck = qfdw =>
                                {
                                    if (qfdw.Owner.PrimaryItem == null || !qfdw.Owner.HasFreeHand || !qfdw.Owner.PrimaryItem.HasTrait(Trait.Weapon))
                                    {
                                        parrybonus.Owner.RemoveAllQEffects((QEffect effect) => effect.Name == "Dueling Parry");
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

    //Grants thrown versions of Confident Finisher and Unbalancing Finisher, as long as your weapons meet the criteria. It's usually down to GM judgement which finishers Flying Blade applies to anyway.
    public static Feat FlyingBlade = new TrueFeat(ModManager.RegisterFeatName("FlyingBlade", "Flying Blade"), 1, "You've learned to apply your flashy techniques to thrown weapons just as easily as melee.", "When you have panache, you apply your additional damage from Precise Strike on ranged Strikes you make with a thrown weapon within its first range increment. The thrown weapon must be an agile or finesse weapon.", new Trait[1] { SwashTrait }, null)
        .WithPermanentQEffect(delegate (QEffect qf)
        {
            if (qf.Owner.HasFeat(Confident.FeatName))
            {
                qf.Owner.AddQEffect(new QEffect()
                {
                    ProvideStrikeModifier = delegate (Item item)
                    {
                        StrikeModifiers conf = new StrikeModifiers();
                        bool flag = (item.HasTrait(Trait.Thrown10Feet) || item.HasTrait(Trait.Thrown20Feet)) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse) || item.HasTrait(Trait.Unarmed));
                        bool flag2 = qf.Owner.HasEffect(PanacheId);
                        if (flag && flag2)
                        {
                            CombatAction confthrow = StrikeRules.CreateStrike(qf.Owner, item, RangeKind.Ranged, -1, true, conf);
                            confthrow.Name = "Confident Finisher (Thrown)";
                            confthrow.Illustration = new SideBySideIllustration(item.Illustration, IllustrationName.StarHit);
                            confthrow.ActionCost = 1;
                            confthrow.WithEffectOnChosenTargets(async delegate (Creature creature, ChosenTargets targets)
                            {
                                targets.ChosenCreature.AddQEffect(new QEffect()
                                {
                                    AfterYouAreTargeted = async delegate (QEffect qfbonk, CombatAction strike)
                                    {
                                        if (strike.CheckResult == CheckResult.Failure)
                                        {
                                            HalfDiceFormula halfdamage = new HalfDiceFormula(DiceFormula.FromText("2d6", "Precise Strike"), "Miss with Confident Finisher");
                                            await strike.Owner.DealDirectDamage(confthrow, halfdamage, targets.ChosenCreature, confthrow.CheckResult, confthrow.StrikeModifiers.CalculatedItem.WeaponProperties.DamageKind);
                                        }
                                    },
                                    ExpiresAt = ExpirationCondition.Ephemeral
                                });
                                FinisherExhaustion(confthrow.Owner);
                            });
                            confthrow.Traits.Add(Finisher);
                            return confthrow;
                        }
                        return null;
                    }
                });
            }
            if (qf.Owner.HasFeat(UnbalancingFinisher.FeatName))
            {
                qf.Owner.AddQEffect(new QEffect()
                {
                    ProvideStrikeModifier = delegate (Item item)
                    {
                        StrikeModifiers unbalancing = new StrikeModifiers();
                        bool flag = (item.HasTrait(Trait.Thrown10Feet) || item.HasTrait(Trait.Thrown20Feet)) && (item.HasTrait(Trait.Agile) || item.HasTrait(Trait.Finesse) || item.HasTrait(Trait.Unarmed));
                        bool flag2 = qf.Owner.HasEffect(PanacheId);
                        if (flag && flag2)
                        {
                            CombatAction unbal = StrikeRules.CreateStrike(qf.Owner, item, RangeKind.Ranged, -1, true, unbalancing);
                            unbal.Name = "Unbalancing Finisher";
                            unbal.Illustration = new SideBySideIllustration(item.Illustration, IllustrationName.Trip);
                            unbal.ActionCost = 1;
                            unbal.Traits.Add(Finisher);
                            return unbal;
                        }
                        else return null;
                    }
                });
            }
        })
        .WithPrerequisite(sheet => sheet.AllFeats.Contains(PreciseStrike), "Precise Strike");

    public static void ReplaceYoureNext()
    {
        AllFeats.All.RemoveAll(feat => feat.FeatName == FeatName.YoureNext);
        ModManager.AddFeat(new TrueFeat(FeatName.YoureNext, 1, "After downing a foe, you menacingly remind another foe that you're coming after them next.", "After you reduce an enemy to 0 HP, you can spend a reaction to Demoralize a single creature that you can see and that can see you. You have a +2 circumstance bonus to this check.", new Trait[5]
        {
            Trait.Emotion,
            Trait.Fear,
            Trait.Mental,
            Trait.Rogue,
            SwashTrait
        }).WithPermanentQEffect("After you reduce an enemy to 0 HP, you can spend a reaction to Demoralize one creature. You have a +2 circumstance bonus to this check.", delegate (QEffect qf)
        {
            qf.AfterYouDealDamage = async delegate (Creature rogue, CombatAction _, Creature target)
            {
                if (target.HP <= 0)
                {
                    CombatAction demoralize = CommonCombatActions.Demoralize(rogue);
                    demoralize.WithActionCost(0);
                    bool flag = demoralize.CanBeginToUse(rogue);
                    bool flag2 = flag;
                    if (flag2)
                    {
                        flag2 = await rogue.Battle.AskToUseReaction(rogue, "You just downed a foe.\nUse {i}You're Next{/i} to demoralize another enemy creature?");
                    }

                    if (flag2)
                    {
                        rogue.AddQEffect(new QEffect
                        {
                            BonusToAttackRolls = (QEffect qf, CombatAction ca, Creature? cr) => (ca.ActionId != ActionId.Demoralize) ? null : new Bonus(2, BonusType.Circumstance, "You're Next"),
                            ExpiresAt = ExpirationCondition.EphemeralAtEndOfImmediateAction
                        });
                        await rogue.Battle.GameLoop.FullCast(demoralize);
                    }
                }
            };
        }));
    }

    public static void ReplaceNimbleDodge()
    {
        AllFeats.All.RemoveAll(feat => feat.FeatName == FeatName.NimbleDodge);
        ModManager.AddFeat(new TrueFeat(FeatName.NimbleDodge, 1, "You deftly dodge out of the way.", "When a creature targets you with an attack, and you can see the attacker, you can spend a reaction to gain a +2 circumstance bonus to AC against the triggering attack.", new Trait[2]
        {
            Trait.Rogue,
            SwashTrait
        }).WithActionCost(-2).WithPermanentQEffect("You gain a +2 bonus to AC as a reaction.", delegate (QEffect qf)
        {
            qf.YouAreTargeted = async delegate (QEffect qf, CombatAction attack)
            {
                if (attack.HasTrait(Trait.Attack) && qf.Owner.CanSee(attack.Owner) && !attack.HasTrait(Trait.AttackDoesNotTargetAC) && await qf.Owner.Battle.AskToUseReaction(qf.Owner, "You're targeted by " + attack.Owner.Name + "'s " + attack.Name + ".\nUse Nimble Dodge to gain a +2 circumstance bonus to AC?"))
                {
                    qf.Owner.AddQEffect(new QEffect
                    {
                        ExpiresAt = ExpirationCondition.EphemeralAtEndOfImmediateAction,
                        BonusToDefenses = (QEffect effect, CombatAction? action, Defense defense) => (defense != 0) ? null : new Bonus(2, BonusType.Circumstance, "Nimble Dodge")
                    });
                }
            };
        }));
    }

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
                bool flag = action.Name == "Feint";
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
        });

    public static Feat OneForAll = new TrueFeat(ModManager.RegisterFeatName("One For All", "One For All {icon:Action}"), 1, "With precisely the right words of encouragement, you bolster an ally's efforts.", "Using one action, designate an ally within 30 feet. The next time that ally makes an attack roll or skill check, you may use your reaction to attempt a DC 20 Diplomacy check with the following effects:\n{b}Critical Success:{/b} You grant the ally a +2 circumstance bonus to their attack roll or skill check. If your swashbuckler style is Wit, you gain panache.\n{b}Success:{/b} You grant the ally a +1 circumstance bonus to their attack roll or skill check. If your swashbuckler style is Wit, you gain panache.\n{b}Critical Failure:{/b} The ally takes a -1 circumstance penalty to their attack roll or skill check.", new Trait[6] { Trait.Auditory, Trait.Concentrate, Trait.Emotion, Trait.Linguistic, Trait.Mental, SwashTrait })
        .WithPermanentQEffect(null, delegate (QEffect qf)
        {
            qf.ProvideActionIntoPossibilitySection = (qfoneforall, section) =>
            {
                if (section.PossibilitySectionId == PossibilitySectionId.OtherManeuvers)
                {
                    return new ActionPossibility(new CombatAction(qf.Owner, IllustrationName.SoundBurst, "One For All", new Trait[5] { Trait.Auditory, Trait.Concentrate, Trait.Emotion, Trait.Linguistic, Trait.Mental }, "Attempt to assist an ally's next skill check or attack roll.", Target.RangedFriend(6)
                    .WithAdditionalConditionOnTargetCreature((Creature self, Creature target) => (target.QEffects.Any((QEffect effect) => effect.Name == "Aided by " + qf.Owner.Name)) ? Usability.NotUsableOnThisCreature("You are already aiding this ally.") : Usability.Usable)
                    .WithAdditionalConditionOnTargetCreature((Creature self, Creature target) => (target == self) ? Usability.NotUsableOnThisCreature("You can't Aid yourself.") : Usability.Usable))
                    .WithActionCost(1)
                    .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                    {
                        QEffect aided = new QEffect();
                        aided.Name = "Aided by " + qf.Owner.Name;
                        aided.Illustration = IllustrationName.ExclamationMarkQEffect;
                        aided.Description = qf.Owner.Name + " may attempt to assist you and provide a bonus to your next attack roll or skill check.";
                        aided.BeforeYourActiveRoll = async delegate (QEffect effect, CombatAction action, Creature self)
                        {
                            CombatAction aid = new CombatAction(qf.Owner, IllustrationName.SoundBurst, "Aid", new Trait[0], "Attempt to Aid an ally.", Target.Self())
                                .WithActionCost(0)
                                .WithActiveRollSpecification(new ActiveRollSpecification(Checks.SkillCheck(Skill.Diplomacy), Checks.FlatDC(20)))
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
                                            if (caster.PersistentCharacterSheet?.Calculated.AllFeats.Any(feat => feat.Name == "Wit") ?? false)
                                            {
                                                caster.AddQEffect(CreatePanache());
                                            }
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
                                            if (caster.PersistentCharacterSheet?.Calculated.AllFeats.Any(feat => feat.Name == "Wit") ?? false)
                                            {
                                                caster.AddQEffect(CreatePanache());
                                            }
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
                    qf.Owner.AddQEffect(CreatePanache());
                }
            };
        });

    public static Feat Antagonize = new TrueFeat(ModManager.RegisterFeatName("Antagonize"), 2, "Your taunts and threats earn your foes' ire.", "When you Demoralize a foe, its frightened condition can't decrease below 1 until it takes a hostile action against you.", new Trait[1] { SwashTrait })
        .WithPermanentQEffect("Enemies can't recover from your Demoralize actions without taking hostile actions against you.", delegate (QEffect qf)
        {
            qf.AfterYouTakeAction = async delegate (QEffect fear, CombatAction demoralize)
            {
                if (demoralize.Name == "Demoralize" && demoralize.CheckResult >= CheckResult.Success)
                {
                    QEffect antagonized = new QEffect()
                    {
                        Name = "Antagonized",
                        Illustration = IllustrationName.Rage,
                        Description = "This target cannot lower their Frightened value below 1 until taking a hostile action against " + qf.Owner.Name + ".",
                        EndOfYourTurn = async delegate (QEffect fright, Creature victim)
                        {
                            if (qf.Owner.DetectionStatus.Undetected)
                            {
                                victim.RemoveAllQEffects(effect => effect.Name == "Antagonized");
                            }
                            if (victim.QEffects.Any(effect => effect.Name == "Frightened" && effect.Value == 1) && victim.QEffects.Any(effect => effect.Name == "Antagonized"))
                            {
                                QEffect fear = victim.QEffects.Single((QEffect thing) => thing.Name == "Frightened");
                                fear.Value = 2;
                            }
                        },
                        AfterYouTakeHostileAction = async delegate (QEffect effect, CombatAction action)
                        {
                            if (action.ChosenTargets.GetAllTargetCreatures().Any(creature => creature == qf.Owner))
                            {
                                effect.Owner.RemoveAllQEffects(effect => effect.Name == "Antagonized");
                            }
                        },
                        StateCheck = async delegate (QEffect effect)
                        {
                            if (!effect.Owner.HasEffect(QEffectId.Frightened))
                            {
                                effect.Owner.RemoveAllQEffects(effect => effect.Name == "Antagonized");
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
                if (action.Name == "Unbalancing Finisher")
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
                    FinisherExhaustion(self);
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
                    unbal.Illustration = new SideBySideIllustration(item.Illustration, IllustrationName.Trip);
                    unbal.ActionCost = 1;
                    unbal.Traits.Add(Finisher);
                    return unbal;
                }
                else return null;
            };
        });

    public static Feat FinishingFollowThrough = new TrueFeat(ModManager.RegisterFeatName("Finishing Follow-Through", "Finishing Follow-Through"), 2, "Finishing a foe maintains your swagger.", "You gain Panache if your finisher reduces an enemy to 0 HP.", new Trait[1] { SwashTrait }, null)
        .WithPermanentQEffect("You gain panache when your finisher defeats an enemy", delegate (QEffect qf)
        {
            qf.AfterYouDealDamage = async delegate (Creature self, CombatAction action, Creature target)
            {
                if (target.HP <= 0 && action.HasTrait(Finisher) && !self.HasEffect(PanacheId))
                {
                    self.AddQEffect(CreatePanache());
                }
            };
        });

    public static Feat CharmedLife = new TrueFeat(ModManager.RegisterFeatName("Charmed Life", "Charmed Life {icon:Reaction}"), 2, "When danger calls, you have a strange knack for coming out on top.", "Before you make a saving throw, you can spend your reaction to gain a +2 circumstance bonus to the roll.", new Trait[1] { SwashTrait })
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

    //I'll need to edit this if a better Tumble Through happens.
    public static Feat TumbleBehind = new TrueFeat(ModManager.RegisterFeatName("Tumble Behind", "Tumble Behind"), 2 ,"Your tumbling catches enemies off-guard.", "Whenever you Tumble Through an enemy, the enemy you Tumbled through is flat-footed against the next attack you make until the end of your turn.", new Trait[1] { SwashTrait }, null)
        .WithPermanentQEffect("Tumbling Through enemies makes them briefly flat-footed.", delegate (QEffect qf)
        {
            qf.AfterYouTakeAction = async delegate (QEffect effect, CombatAction action)
            {
                if (action.Name == "Tumble Through" && action.CheckResult >= CheckResult.Success && action.ChosenTargets.ChosenCreature != null)
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
                                action.ChosenTargets.ChosenCreature.RemoveAllQEffects((QEffect qf) => qf.Name == "Tumbled Behind");
                            }
                        }
                    });
                }
            };
        });

    public static Feat GuardiansDeflection = new TrueFeat(ModManager.RegisterFeatName("Guardian's Deflection", "Guardian's Deflection {icon:Reaction}"), 4, "You use your weapon to deflect an attack made against an ally.", "{b}Trigger:{/b} An ally within your melee reach is hit by an attack, you can see the attacker, and a +2 circumstance bonus to AC would turn the critical hit into a hit or the hit into a miss.\n\n{b}Requirements: {/b} You are wielding a single one-handed weapon and have your other hand free.\n\n You use your weapon to deflect the attack against your ally, granting them a +2 circumstance bonus against the triggering attack. This turns the triggering critical hit into a hit, or the triggering hit into a miss.", new Trait[2] { Trait.Fighter, SwashTrait }, null)
        .WithPermanentQEffect(delegate (QEffect qf)
        {
            qf.StateCheck = delegate (QEffect deflect)
            {
                foreach (Creature ally in qf.Owner.Battle.AllCreatures.Where((Creature friend) => (friend.DistanceTo(qf.Owner) == 1 && friend.FriendOf(qf.Owner))))
                {
                    ally.AddQEffect(new QEffect()
                    {
                        TriggeredByIncomingMeleeStrikeHitAndYouAreNotRaisingAShield = async delegate (QEffect deflection, CombatAction attack, CheckBreakdownResult breakdownresult)
                        {
                            if ((qf.Owner.HasOneWeaponAndFist && qf.Owner.PrimaryWeapon != null && qf.Owner.PrimaryWeapon.HasTrait(Trait.Melee)) && (attack.HasTrait(Trait.Attack) && !attack.HasTrait(Trait.AttackDoesNotTargetAC)) && qf.Owner.CanSee(attack.Owner) && breakdownresult.ThresholdToDowngrade <= 2)
                            {
                                if (await qf.Owner.Battle.AskToUseReaction(qf.Owner, "Would you like to block this attack?"))
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
                    return new CombatAction(qf.Owner, new SideBySideIllustration(item.Illustration, item.Illustration), "Impaling Finisher", new Trait[1] { Finisher }, "Make a bludgeoning or piercing attack against an adjacent enemy, then an enemy directly behind them in a straight line.", Target.Melee())
                    .WithActionCost(1)
                    .WithEffectOnChosenTargets(async delegate (Creature swash, ChosenTargets target)
                    {
                        int xtranslate = swash.Occupies.X - target.ChosenCreature!.Occupies.X;
                        int ytranslate = swash.Occupies.Y - target.ChosenCreature!.Occupies.Y;
                        Tile? target2 = swash.Battle.Map.GetTile(target.ChosenCreature.Occupies.X - xtranslate, target.ChosenCreature.Occupies.Y - ytranslate);
                        CombatAction impale = swash.CreateStrike(item, map);
                        impale.Traits.Add(Finisher);
                        impale.ChosenTargets = new ChosenTargets
                        {
                            ChosenCreature = target.ChosenCreature
                        };
                        if (target2 != null && target2.PrimaryOccupant != null && target2.PrimaryOccupant.EnemyOf(swash))
                        {
                            CombatAction impale2 = swash.CreateStrike(item, map);
                            impale2.Traits.Add(Finisher);
                            impale2.ChosenTargets = new ChosenTargets
                            {
                                ChosenCreature = target2.PrimaryOccupant
                            };
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

    public static Feat SwaggeringInitiative = new TrueFeat(ModManager.RegisterFeatName("SwaggeringInitiative", "Swaggering Initiative"), 4, "You swagger readily into any battle.", "You gain a +2 circumstance bonus to initiative rolls.\nIn addition, when combat begins, you can drink one potion you're holding as a free action.", new Trait[1] { SwashTrait }, null)
        .WithPermanentQEffect(delegate (QEffect qf)
        {
            qf.Owner.AddQEffect(new QEffect()
            {
                Id = QEffectId.IncredibleInitiative
            });
            qf.StartOfCombat = async delegate (QEffect swag)
            {
                if ((qf.Owner.PrimaryItem != null) && qf.Owner.PrimaryItem.HasTrait(Trait.Drinkable))
                {
                    Item potion = qf.Owner.PrimaryItem;
                    CombatAction quaff = new CombatAction(qf.Owner, potion.Illustration, "Drink", new Trait[1] { Trait.Manipulate }, "Drink your " + potion.Name + ".\n\n" + potion.Description, Target.Self())
                        .WithEffectOnEachTarget(async (spell, caster, target, result) =>
                        {
                            potion.DrinkableEffect(spell, caster);
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
                            potion.DrinkableEffect(spell, caster);
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

    public static void LoadSwash()
    {
        ModManager.AddFeat(Swashbuckler);
        //ModManager.AddFeat(AddPanache);
        //ModManager.AddFeat(BonMot);
        ModManager.AddFeat(DisarmingFlair);
        ModManager.AddFeat(DuelingParry);
        ModManager.AddFeat(FlyingBlade);
        ReplaceYoureNext();
        ReplaceNimbleDodge();
        ModManager.AddFeat(GoadingFeint);
        ModManager.AddFeat(OneForAll);
        ModManager.AddFeat(AfterYou);
        ModManager.AddFeat(Antagonize);
        ModManager.AddFeat(UnbalancingFinisher);
        ModManager.AddFeat(FinishingFollowThrough);
        ModManager.AddFeat(CharmedLife);
        ModManager.AddFeat(TumbleBehind);
        ModManager.AddFeat(GuardiansDeflection); //BASICALLY COMPLETE
        ModManager.AddFeat(ImpalingFinisher);
        ModManager.AddFeat(SwaggeringInitiative);
        ModManager.AddFeat(TwinParry);
    }
}