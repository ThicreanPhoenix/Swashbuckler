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

namespace Dawnsbury.Mods.Phoenix;

public class AddWeapons
{
    public static Trait Parry = ModManager.RegisterTrait("Parry", new TraitProperties("Parry", true, "This weapon can be used defensively to block attacks."));
    public static void LoadWeapons()
    {
        ModManager.RegisterNewItemIntoTheShop("Main-Gauche", itemName =>
            new Item(IllustrationName.Dagger, "main-gauche", new Trait[8] { Trait.Agile, Trait.Disarm, Trait.Finesse, Trait.VersatileS, Parry, Trait.Melee, Trait.Martial, Trait.Knife })
                {
                    ItemName = itemName,
                    ProvidesItemAction = (delegate (Creature self, Item item)
                    {
                        ActionPossibility parry = new ActionPossibility(new CombatAction(self, IllustrationName.Feint, "Parry", new Trait[0] { }, "You raise your weapon to parry oncoming attacks, granting yourself a +1 circumstance bonus to AC.", Target.Self())
                            .WithEffectOnEachTarget(async (caster, spell, target, result) =>
                            {
                                target.AddQEffect(new QEffect()
                                {
                                    Name = "Parrying",
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
                                    }
                                });
                            }));
                        return parry;
                    })
                }
                .WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Piercing))
                );;

        ModManager.RegisterNewItemIntoTheShop("Bo Staff", itemName =>
            new Item(IllustrationName.Quarterstaff, "bostaff", new Trait[7] { Trait.Monk, Parry, Trait.Trip, Trait.Martial, Trait.Staff, Trait.Melee, Trait.TwoHanded })
            {
                ItemName = itemName,
                ProvidesItemAction = (delegate (Creature self, Item item)
                {
                    ActionPossibility parry = new ActionPossibility(new CombatAction(self, IllustrationName.Feint, "Parry", new Trait[0] { }, "You raise your weapon to parry oncoming attacks, granting yourself a +1 circumstance bonus to AC.", Target.Self())
                        .WithEffectOnEachTarget(async (caster, spell, target, result) =>
                        {
                            target.AddQEffect(new QEffect()
                            {
                                Name = "Parrying",
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
                                }
                            });
                        }));
                    return parry;
                })
            }
                .WithWeaponProperties(new WeaponProperties("1d8", DamageKind.Bludgeoning)));
        
    }
}