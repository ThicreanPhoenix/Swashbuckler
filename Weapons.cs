using Dawnsbury.Audio;
using Dawnsbury.Core;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.TargetingRequirements;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Modding;
using System.Collections.Generic;
using System.Linq;

namespace Dawnsbury.Mods.Phoenix;

public class AddWeapons
{
    //TODO: Parry is offered twice for the bostaff.
    //TODO: Apparently there are issues with ParryEffect between this mod and the Remaster Swash?
    public static QEffectId ParryEffect = ModManager.RegisterEnumMember<QEffectId>("ParryEffect");
    public static Trait Parry = ModManager.RegisterTrait("Parry", new TraitProperties("Parry", true, "This weapon can be used defensively to block attacks."));

    public static QEffect Parrying(Item item)
    {
        return new QEffect()
        {
            Name = "Parrying with " + item.Name,
            Id = ParryEffect,
            Illustration = IllustrationName.Swords,
            Description = "You have a +1 circumstance bonus to AC.",
            ExpiresAt = ExpirationCondition.ExpiresAtStartOfYourTurn,
            BonusToDefenses = delegate (QEffect parrying, CombatAction? bonk, Defense defense)
            {
                if (defense == Defense.AC)
                {
                    return new Bonus(1, BonusType.Circumstance, "Parry");
                }
                else return null;
            },
            Tag = item,
            StateCheck = (qf) =>
            {
                if (!qf.Owner.HeldItems.Contains(item))
                {
                    qf.ExpiresAt = ExpirationCondition.Immediately;
                }
            }
        };
    }
    
    public static ItemName MainGauche = ModManager.RegisterNewItemIntoTheShop("Main-Gauche", (itemName) =>
    {
        return new Item(itemName, new ModdedIllustration("PhoenixAssets/MainGauche.png"), "main-gauche", 0, 0, new Trait[] { Trait.Agile, Trait.Disarm, Trait.Finesse, Trait.VersatileS, Parry, Trait.Martial, Trait.Knife })
            .WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Piercing));
    });

    public static ItemName BoStaff = ModManager.RegisterNewItemIntoTheShop("Bo Staff", (itemName) =>
    {
        return new Item(itemName, new ModdedIllustration("PhoenixAssets/BoStaff.png"), "bostaff", 0, 0, new Trait[] { Parry, Trait.Trip, Trait.Martial, Trait.Staff, Trait.TwoHanded, Trait.Reach, Trait.Club, Trait.MonkWeapon, Trait.NonMetallic })
            .WithWeaponProperties(new WeaponProperties("1d8", DamageKind.Bludgeoning));
    });

    public static ItemName DuelingCape = ModManager.RegisterNewItemIntoTheShop("DuelingCape", (itemName) =>
    {
        return new Item(itemName, new ModdedIllustration("PhoenixAssets/DuelingCape.png"), "dueling cape", 0, 0, new Trait[] { })
        {
            ProvidesItemAction = delegate (Creature you, Item cape2)
            {
                if (!you.QEffects.Any((QEffect qf) => qf.Name == "Raised Cape"))
                {
                    return new ActionPossibility(new CombatAction(you, cape2.Illustration, "Brandish Cape", new Trait[] { Trait.Interact }, "Hold the dueling cape defensively, giving yourself a +1 circumstance bonus to AC and Deception checks to Feint until the start of your next turn.",
                            Target.Self())
                        .WithActionCost(1)
                        .WithGoodness((tg, self, _) => self.AI.GainBonusToAC(1))
                        .WithSoundEffect(SfxName.RaiseShield)
                        .WithEffectOnSelf(async (me) =>
                        {
                            me.AddQEffect(new QEffect("Raised Cape", "You have a +1 circumstance bonus to AC and to Deception checks to Feint.", ExpirationCondition.ExpiresAtStartOfYourTurn, me, cape2.Illustration)
                            {
                                StateCheck = async (qf2) =>
                                {
                                    if (!qf2.Owner.HeldItems.Contains(cape2))
                                    {
                                        qf2.ExpiresAt = ExpirationCondition.Immediately;
                                    }
                                },
                                BonusToDefenses = delegate (QEffect qf, CombatAction action, Defense defense)
                                {
                                    if (defense == Defense.AC)
                                    {
                                        return new Bonus(1, BonusType.Circumstance, "raised dueling cape");
                                    }
                                    else return null;
                                },
                                BonusToSkillChecks = delegate (Skill skill, CombatAction action, Creature target)
                                {
                                    if (action.ActionId == ActionId.Feint && skill == Skill.Deception)
                                    {
                                        return new Bonus(1, BonusType.Circumstance, "raised dueling cape");
                                    }
                                    else return null;
                                },
                                CountsAsABuff = true
                            });
                        }));
                }
                else return null;
            }
        }.WithDescription("While wielding this cape, you can spend an action to hold it in a defensive position, giving you a +1 circumstance bonus to AC and to Deception checks to Feint until the start of your next turn.");
    });

    
    public static void LoadWeapons()
    {
        List<ItemName> items = new List<ItemName>() { MainGauche, BoStaff, DuelingCape };
        ModManager.RegisterActionOnEachCreature((creature) =>
        {
            creature.AddQEffect(new QEffect()
            {
                StateCheck = (qf) =>
                {
                    foreach (Item i in creature.HeldItems)
                    {
                        if (i.HasTrait(Parry) && (creature.QEffects.FirstOrDefault((QEffect qf) => (qf.Id == ParryEffect) && (qf.Tag == i)) == default))
                        {
                            creature.AddQEffect(new QEffect(ExpirationCondition.Ephemeral)
                            {
                                ProvideActionIntoPossibilitySection = delegate (QEffect effect, PossibilitySection section)
                                {
                                    if (section.PossibilitySectionId == PossibilitySectionId.ItemActions)
                                    {
                                        ActionPossibility parry = new ActionPossibility(new CombatAction(effect.Owner, new SideBySideIllustration(IllustrationName.Shield, i.Illustration), "Parry", new Trait[0] { }, "You raise your weapon to parry oncoming attacks, granting yourself a +1 circumstance bonus to AC.", Target.Self())
                                            .WithSoundEffect(SfxName.RaiseShield)
                                            .WithActionCost(1)
                                            .WithEffectOnEachTarget(async (caster, spell, target, result) =>
                                            {
                                                target.AddQEffect(Parrying(i));
                                            }));
                                        return parry;
                                    }
                                    else return null;
                                }
                            });
                        }
                    }
                }
            });
        });
    }
}