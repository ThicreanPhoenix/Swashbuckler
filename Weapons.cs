using Dawnsbury.Core;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Modding;
using Dawnsbury.Core.Possibilities;
using System.Linq;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Audio;

namespace Dawnsbury.Mods.Phoenix;

public class AddWeapons
{
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
    public static void LoadWeapons()
    {
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
                            creature.AddQEffect(new QEffect()
                            {
                                ExpiresAt = ExpirationCondition.Ephemeral,
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

        ModManager.RegisterNewItemIntoTheShop("Main-Gauche", itemName =>
            new Item(IllustrationName.Dagger, "main-gauche", new Trait[] { Trait.Agile, Trait.Disarm, Trait.Finesse, Trait.VersatileS, Parry, Trait.Melee, Trait.Martial, Trait.Knife })
                {
                    ItemName = itemName
                }
                .WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Piercing))
                );;

        ModManager.RegisterNewItemIntoTheShop("Bo Staff", itemName =>
            new Item(IllustrationName.Quarterstaff, "bostaff", new Trait[] { Parry, Trait.Trip, Trait.Martial, Trait.Staff, Trait.Melee, Trait.TwoHanded, Trait.Reach, Trait.Club })
            {
                ItemName = itemName
            }
                .WithWeaponProperties(new WeaponProperties("1d8", DamageKind.Bludgeoning)));
        
    }
}